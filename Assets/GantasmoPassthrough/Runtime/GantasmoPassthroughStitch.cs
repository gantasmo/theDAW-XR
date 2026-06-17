using Meta.XR;
using Meta.XR.EnvironmentDepth;
using UnityEngine;
using UnityEngine.Android;

namespace Gantasmo.Passthrough
{
    /// <summary>
    /// Captures the two forward passthrough RGB cameras and stitches them into a
    /// single centred 16:9 <see cref="RenderTexture"/> using <c>PassthroughStitch.shader</c>
    /// (a plane-homography reprojection + feather blend). This is the shared front of the
    /// delinQuest passthrough pipeline: the same <see cref="OutputTexture"/> later feeds the
    /// H.264 encoder/stream; for now it drives an in-headset preview quad so the stitch can be
    /// seen and dialled in before any streaming exists.
    ///
    /// Setup: drop this component on ONE GameObject in the QuestMIDI scene. It creates the two
    /// <see cref="PassthroughCameraAccess"/> instances itself and spawns a preview quad in front
    /// of the main camera. The Passthrough Camera API needs the
    /// <c>horizonos.permission.HEADSET_CAMERA</c> permission in AndroidManifest.xml — run
    /// Meta &gt; Tools &gt; Project Setup Tool to add it if a build complains. Requires a Quest 3 /
    /// 3S on Horizon OS v74+ (the camera will simply stay blank on unsupported devices).
    ///
    /// Tuning (live, in-headset): <see cref="focalDistanceMeters"/> sets the depth that lines up
    /// cleanly across the seam (objects far off it show parallax doubling), <see cref="blendWidth"/>
    /// is the central feather, <see cref="outputHorizontalFovDeg"/> the framing, and
    /// <see cref="flipY"/> corrects the camera texture origin if the image is upside down.
    /// </summary>
    [DefaultExecutionOrder(100)] // after PassthroughCameraAccess (-100) refreshes its textures
    [AddComponentMenu("GANTASMO Passthrough/Passthrough Stitch")]
    public class GantasmoPassthroughStitch : MonoBehaviour
    {
        const string CameraPermission = "horizonos.permission.HEADSET_CAMERA";

        [Header("Capture")]
        [Tooltip("Requested per-camera resolution. The SDK falls back to the nearest supported size.")]
        public Vector2Int requestedCameraResolution = new Vector2Int(1280, 960);

        [Header("Stitched output")]
        [Tooltip("Resolution of the stitched 16:9 RenderTexture handed downstream.")]
        public Vector2Int outputResolution = new Vector2Int(1920, 1080);
        [Range(40f, 110f)]
        [Tooltip("Horizontal field of view of the virtual (output) camera, in degrees.")]
        public float outputHorizontalFovDeg = 82f;
        [Min(0.2f)]
        [Tooltip("Depth (metres) of the reprojection plane. Content at this depth lines up across the seam.")]
        public float focalDistanceMeters = 1.5f;
        [Range(0f, 0.49f)]
        [Tooltip("Half-width (in output-x) of the central feather between the two cameras.")]
        public float blendWidth = 0.12f;
        [Tooltip("Flip the sampled camera image vertically if it comes through upside down.")]
        public bool flipY = false;

        [Header("Depth-aware reproject")]
        [Tooltip("Use the Quest environment depth to reproject every pixel at its true distance instead of one focal plane, " +
                 "which removes the parallax doubling at the seam. Falls back to the focal plane whenever depth is unavailable " +
                 "(unsupported device, not yet ready, or outside the depth frustum).")]
        public bool useEnvironmentDepth = true;

        [Header("Wiring")]
        [Tooltip("The stitch shader. Falls back to Shader.Find(\"Gantasmo/PassthroughStitch\") when empty.")]
        public Shader stitchShader;
        [Tooltip("Optional MeshRenderer to show the stitched output on. Auto-created if empty.")]
        public MeshRenderer previewRenderer;
        public bool autoCreatePreview = true;
        [Tooltip("Distance in front of the main camera to place the auto-created preview quad.")]
        public float previewDistance = 1.6f;

        /// <summary>The live stitched 16:9 image. Stable across the component's lifetime.</summary>
        public RenderTexture OutputTexture => _outRT;

        PassthroughCameraAccess _camL;
        PassthroughCameraAccess _camR;
        Material _mat;
        Material _previewMat;
        RenderTexture _outRT;
        EnvironmentDepthManager _depth;
        const string DepthKeyword = "GANTASMO_DEPTH_STITCH";

        // Cached shader property ids.
        static readonly int IdLeftTex = Shader.PropertyToID("_LeftTex");
        static readonly int IdRightTex = Shader.PropertyToID("_RightTex");
        static readonly int IdWorldToCamL = Shader.PropertyToID("_WorldToCamL");
        static readonly int IdWorldToCamR = Shader.PropertyToID("_WorldToCamR");
        static readonly int IdVirtualToWorld = Shader.PropertyToID("_VirtualToWorld");
        static readonly int IdIntrL = Shader.PropertyToID("_IntrL");
        static readonly int IdIntrR = Shader.PropertyToID("_IntrR");
        static readonly int IdCropL = Shader.PropertyToID("_CropL");
        static readonly int IdCropR = Shader.PropertyToID("_CropR");
        static readonly int IdTanHalfHFov = Shader.PropertyToID("_TanHalfHFov");
        static readonly int IdTanHalfVFov = Shader.PropertyToID("_TanHalfVFov");
        static readonly int IdFocalDist = Shader.PropertyToID("_FocalDist");
        static readonly int IdBlendWidth = Shader.PropertyToID("_BlendWidth");
        static readonly int IdFlipY = Shader.PropertyToID("_FlipY");

        void Awake()
        {
            // The PassthroughCameraAccess components only WAIT for the permission; something has to
            // request it. Do that first so the OS prompt is up before the cameras try to play.
            if (!Permission.HasUserAuthorizedPermission(CameraPermission))
            {
                Permission.RequestUserPermission(CameraPermission);
            }

            var shader = stitchShader != null ? stitchShader : Shader.Find("Gantasmo/PassthroughStitch");
            if (shader == null)
            {
                Debug.LogError("[GantasmoPassthroughStitch] Stitch shader not found. Assign 'Gantasmo/PassthroughStitch'.", this);
                enabled = false;
                return;
            }
            _mat = new Material(shader) { name = "GantasmoPassthroughStitch" };

            _outRT = new RenderTexture(outputResolution.x, outputResolution.y, 0, RenderTextureFormat.ARGB32)
            {
                name = "GantasmoStitchOutput",
                useMipMap = false,
                autoGenerateMips = false,
            };
            _outRT.Create();

            _camL = CreateCamera(PassthroughCameraAccess.CameraPositionType.Left, "PCA_Left");
            _camR = CreateCamera(PassthroughCameraAccess.CameraPositionType.Right, "PCA_Right");

            SetupPreview();
            TrySetupDepth();
        }

        // Bring up Meta's EnvironmentDepthManager so it publishes the global depth
        // texture + reprojection matrices our shader's depth path reads. Left at its
        // default occlusion mode so the texture is guaranteed to upload; the global
        // occlusion keyword it sets is harmless to this scene's materials. Anything
        // unsupported leaves _depth null and the stitch stays on the focal plane.
        void TrySetupDepth()
        {
            if (!useEnvironmentDepth) return;
            if (!EnvironmentDepthManager.IsSupported)
            {
                Debug.Log("[GantasmoPassthroughStitch] Environment depth unsupported here; using the focal-plane stitch.", this);
                return;
            }
            _depth = FindAnyObjectByType<EnvironmentDepthManager>();
            if (_depth == null) _depth = gameObject.AddComponent<EnvironmentDepthManager>();
            _depth.enabled = true;
        }

        PassthroughCameraAccess CreateCamera(PassthroughCameraAccess.CameraPositionType position, string label)
        {
            // Build the GameObject inactive so the camera's OnEnable reads OUR CameraPosition /
            // RequestedResolution (set below) instead of the component defaults.
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var pca = go.AddComponent<PassthroughCameraAccess>();
            pca.CameraPosition = position;
            pca.RequestedResolution = requestedCameraResolution;
            go.SetActive(true);
            return pca;
        }

        void SetupPreview()
        {
            if (previewRenderer == null && autoCreatePreview)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "GantasmoStitchPreview";
                var col = quad.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var cam = Camera.main;
                if (cam != null)
                {
                    quad.transform.SetParent(cam.transform, false);
                    quad.transform.localPosition = new Vector3(0f, 0f, previewDistance);
                }
                else
                {
                    quad.transform.position = new Vector3(0f, 1.5f, previewDistance);
                }
                // A Unity Quad's textured face normal is +Z; spin it 180deg so that face
                // points back at the camera (otherwise we'd see the culled back). The
                // texture mirror that introduces is undone on the material below.
                quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                float aspect = (float)outputResolution.x / Mathf.Max(1, outputResolution.y);
                quad.transform.localScale = new Vector3(aspect, 1f, 1f);
                previewRenderer = quad.GetComponent<MeshRenderer>();
            }

            if (previewRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Texture");
                _previewMat = new Material(sh);
                // Un-mirror the horizontal flip caused by the 180deg facing rotation.
                if (_previewMat.HasProperty("_BaseMap"))
                {
                    _previewMat.SetTexture("_BaseMap", _outRT);
                    _previewMat.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
                    _previewMat.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
                }
                _previewMat.mainTexture = _outRT; // covers the Unlit/Texture fallback
                _previewMat.mainTextureScale = new Vector2(-1f, 1f);
                _previewMat.mainTextureOffset = new Vector2(1f, 0f);
                previewRenderer.material = _previewMat;
            }
        }

        void LateUpdate()
        {
            if (_mat == null || _outRT == null || _camL == null || _camR == null) return;

            // Flip the shader's depth path on only while real depth is flowing; the
            // moment it drops out we fall back to the focal-plane reproject.
            bool depthOn = useEnvironmentDepth && _depth != null && _depth.IsDepthAvailable;
            if (depthOn) _mat.EnableKeyword(DepthKeyword);
            else _mat.DisableKeyword(DepthKeyword);

            if (!_camL.IsPlaying || !_camR.IsPlaying) return;

            var texL = _camL.GetTexture();
            var texR = _camR.GetTexture();
            if (texL == null || texR == null) return;

            _mat.SetTexture(IdLeftTex, texL);
            _mat.SetTexture(IdRightTex, texR);

            var poseL = _camL.GetCameraPose();
            var poseR = _camR.GetCameraPose();
            _mat.SetMatrix(IdWorldToCamL, Matrix4x4.TRS(poseL.position, poseL.rotation, Vector3.one).inverse);
            _mat.SetMatrix(IdWorldToCamR, Matrix4x4.TRS(poseR.position, poseR.rotation, Vector3.one).inverse);

            var midPos = (poseL.position + poseR.position) * 0.5f;
            var midRot = Quaternion.Slerp(poseL.rotation, poseR.rotation, 0.5f);
            _mat.SetMatrix(IdVirtualToWorld, Matrix4x4.TRS(midPos, midRot, Vector3.one));

            ApplyIntrinsics(_camL, IdIntrL, IdCropL);
            ApplyIntrinsics(_camR, IdIntrR, IdCropR);

            float tanH = Mathf.Tan(outputHorizontalFovDeg * Mathf.Deg2Rad * 0.5f);
            float aspect = (float)outputResolution.x / Mathf.Max(1, outputResolution.y);
            _mat.SetFloat(IdTanHalfHFov, tanH);
            _mat.SetFloat(IdTanHalfVFov, tanH / Mathf.Max(1e-4f, aspect));
            _mat.SetFloat(IdFocalDist, focalDistanceMeters);
            _mat.SetFloat(IdBlendWidth, blendWidth);
            _mat.SetFloat(IdFlipY, flipY ? 1f : 0f);

            Graphics.Blit(Texture2D.blackTexture, _outRT, _mat);
        }

        // Packs one camera's intrinsics (focal + principal) and crop region into the shader,
        // mirroring PassthroughCameraAccess.CalcSensorCropRegion so sampled UVs match the SDK.
        void ApplyIntrinsics(PassthroughCameraAccess cam, int intrId, int cropId)
        {
            var intr = cam.Intrinsics;
            _mat.SetVector(intrId, new Vector4(intr.FocalLength.x, intr.FocalLength.y, intr.PrincipalPoint.x, intr.PrincipalPoint.y));

            Vector2 sensorRes = new Vector2(intr.SensorResolution.x, intr.SensorResolution.y);
            Vector2 curRes = new Vector2(cam.CurrentResolution.x, cam.CurrentResolution.y);
            if (sensorRes.x < 1f || sensorRes.y < 1f) { _mat.SetVector(cropId, new Vector4(0, 0, 1, 1)); return; }

            Vector2 scale = new Vector2(curRes.x / sensorRes.x, curRes.y / sensorRes.y);
            float m = Mathf.Max(scale.x, scale.y);
            if (m > 0f) scale /= m;
            _mat.SetVector(cropId, new Vector4(
                sensorRes.x * (1f - scale.x) * 0.5f,
                sensorRes.y * (1f - scale.y) * 0.5f,
                sensorRes.x * scale.x,
                sensorRes.y * scale.y));
        }

        void OnDestroy()
        {
            if (_camL != null) Destroy(_camL.gameObject);
            if (_camR != null) Destroy(_camR.gameObject);
            if (_mat != null) Destroy(_mat);
            if (_previewMat != null) Destroy(_previewMat);
            if (_outRT != null) { _outRT.Release(); Destroy(_outRT); }
        }
    }
}
