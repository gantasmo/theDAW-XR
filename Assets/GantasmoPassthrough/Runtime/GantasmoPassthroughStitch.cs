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

        [Header("Augmented composite (3D content in the stream)")]
        [Tooltip("Render the scene's virtual 3D content from this stitch's head-center viewpoint and composite it over " +
                 "the passthrough, exposed as CompositeTexture for the streamer (the Mixed-Reality-Capture composite). " +
                 "Off = passthrough only. Costs a second offscreen scene render each frame.")]
        public bool composite3D = true;
        [Tooltip("Layer for the in-headset preview quad so the composite camera can exclude it. Must be a layer your MAIN " +
                 "camera still renders (so you still see the preview), but the augmentation camera does not.")]
        public int previewLayer = 31;
        [Tooltip("Flip the augmentation layer vertically if the 3D content composites upside down on your device.")]
        public bool flipAugmentationV = false;
        [Tooltip("Resolution the virtual 3D layer (MRC camera) renders at. Lower = a cheaper second render; the " +
                 "composite upscales it over the full-res passthrough, so passthrough stays sharp while GPU cost drops. " +
                 "Keep at or below outputResolution.")]
        public Vector2Int augmentationResolution = new Vector2Int(1280, 720);
        [Tooltip("Which layers the MRC camera renders into the stream. Leave as Nothing (0) to render everything " +
                 "the main camera sees (minus the preview quad). Set it to ONLY the layer(s) holding the content you " +
                 "want streamed (e.g. the MIDI surface) so the composite does not re-render the whole scene/rig — a big " +
                 "GPU saving, since the composite is otherwise a third full render of every object.")]
        public LayerMask augmentationCullingMask;
        [Tooltip("The composite shader. Falls back to Shader.Find(\"Gantasmo/PassthroughComposite\") when empty.")]
        public Shader compositeShader;

        [Header("Wiring")]
        [Tooltip("The stitch shader. Falls back to Shader.Find(\"Gantasmo/PassthroughStitch\") when empty.")]
        public Shader stitchShader;
        [Tooltip("Optional MeshRenderer to show the stitched output on. Auto-created if empty.")]
        public MeshRenderer previewRenderer;
        public bool autoCreatePreview = true;
        [Tooltip("Distance in front of the main camera to place the auto-created preview quad.")]
        public float previewDistance = 1.6f;

        /// <summary>The live stitched 16:9 passthrough image (no virtual content). Stable across the component's lifetime.</summary>
        public RenderTexture OutputTexture => _outRT;

        /// <summary>Passthrough + the virtual 3D layer composited (the MR view), or null when composite3D is off.
        /// The streamer prefers this so the VJ source carries the augmented content.</summary>
        public RenderTexture CompositeTexture => _compositeRT;

        PassthroughCameraAccess _camL;
        PassthroughCameraAccess _camR;
        Material _mat;
        Material _previewMat;
        RenderTexture _outRT;
        EnvironmentDepthManager _depth;
        const string DepthKeyword = "GANTASMO_DEPTH_STITCH";

        // Augmented composite (MRC): a head-center camera renders the virtual layer into
        // _augRT (transparent matte); _compositeMat lays it over the passthrough into _compositeRT.
        Camera _augCam;
        RenderTexture _augRT;
        RenderTexture _compositeRT;
        Material _compositeMat;
        static readonly int IdAugTex = Shader.PropertyToID("_AugTex");
        static readonly int IdAugFlipV = Shader.PropertyToID("_AugFlipV");

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
            if (composite3D) SetupComposite();
        }

        // Mixed-Reality-Capture composite: an offscreen head-center camera renders the scene's
        // virtual content (transparent where there is none) into _augRT; LateUpdate then lays
        // that over the passthrough into _compositeRT. The augmentation camera renders to a
        // RenderTexture only, so the user's in-headset view is untouched.
        void SetupComposite()
        {
            var cshader = compositeShader != null ? compositeShader : Shader.Find("Gantasmo/PassthroughComposite");
            if (cshader == null)
            {
                Debug.LogWarning("[GantasmoPassthroughStitch] 'Gantasmo/PassthroughComposite' not found; streaming passthrough only.", this);
                composite3D = false;
                return;
            }
            _compositeMat = new Material(cshader) { name = "GantasmoPassthroughComposite" };

            // The MRC layer renders at its own (typically lower) resolution; the composite
            // upscales it over the full-res passthrough. This is the main GPU saving knob.
            int augW = Mathf.Clamp(augmentationResolution.x, 64, outputResolution.x);
            int augH = Mathf.Clamp(augmentationResolution.y, 64, outputResolution.y);
            _augRT = new RenderTexture(augW, augH, 24, RenderTextureFormat.ARGB32)
            {
                name = "GantasmoAugmentationRT",
                useMipMap = false,
                autoGenerateMips = false,
            };
            _augRT.Create();
            _compositeRT = new RenderTexture(outputResolution.x, outputResolution.y, 0, RenderTextureFormat.ARGB32)
            {
                name = "GantasmoCompositeOutput",
                useMipMap = false,
                autoGenerateMips = false,
            };
            _compositeRT.Create();

            var camGo = new GameObject("GantasmoAugmentationCam");
            camGo.transform.SetParent(transform, false);
            _augCam = camGo.AddComponent<Camera>();
            _augCam.clearFlags = CameraClearFlags.SolidColor;
            _augCam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent matte: passthrough shows through
            if (augmentationCullingMask.value != 0)
            {
                // Restrict the MRC render to just the streamed-content layer(s) — avoids re-rendering
                // the whole scene/rig a third time. The preview quad layer is excluded regardless.
                _augCam.cullingMask = augmentationCullingMask.value & ~(1 << previewLayer);
            }
            else
            {
                int mainMask = Camera.main != null ? Camera.main.cullingMask : ~0;
                _augCam.cullingMask = mainMask & ~(1 << previewLayer); // render virtual content, never the preview quad
            }
            _augCam.targetTexture = _augRT; // offscreen only — does not touch the headset display
            _augCam.nearClipPlane = 0.05f;
            _augCam.farClipPlane = 1000f;
            _augCam.depth = -10f;

            // Keep the clean-passthrough preview quad out of the augmentation render.
            if (previewRenderer != null) previewRenderer.gameObject.layer = previewLayer;
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
            // A freshly-started passthrough camera can report a zero quaternion {0,0,0,0}
            // for a frame or two before its first real pose lands. IsPlaying is already true
            // by then, so without this guard Matrix4x4.TRS gets an invalid (zero-length)
            // rotation, spams "Quaternion To Matrix conversion failed", and blits a garbage
            // frame. Wait for both poses to be valid instead.
            if (!IsValidRotation(poseL.rotation) || !IsValidRotation(poseR.rotation)) return;
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

            // Augmented composite: aim the head-center MRC camera at the same virtual
            // viewpoint as the stitch, then lay the virtual layer it rendered (into _augRT)
            // over the fresh passthrough. The camera renders _augRT during the frame's render
            // phase, so the virtual layer trails the passthrough by one frame — imperceptible
            // for a VJ feed, and it avoids any render-order coupling.
            if (composite3D && _augCam != null && _compositeRT != null && _compositeMat != null)
            {
                _augCam.transform.SetPositionAndRotation(midPos, midRot);
                _augCam.aspect = aspect;
                _augCam.fieldOfView = 2f * Mathf.Atan(tanH / Mathf.Max(1e-4f, aspect)) * Mathf.Rad2Deg;
                _compositeMat.SetTexture(IdAugTex, _augRT);
                _compositeMat.SetFloat(IdAugFlipV, flipAugmentationV ? 1f : 0f);
                Graphics.Blit(_outRT, _compositeRT, _compositeMat); // _MainTex = passthrough
            }
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

        // (0,0,0,0) has zero length and is not a valid rotation; a real pose quaternion is
        // unit-length. Guards against the brief zero-pose window right after the cameras start.
        static bool IsValidRotation(Quaternion q)
            => (q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w) > 1e-6f;

        void OnDestroy()
        {
            if (_camL != null) Destroy(_camL.gameObject);
            if (_camR != null) Destroy(_camR.gameObject);
            if (_mat != null) Destroy(_mat);
            if (_previewMat != null) Destroy(_previewMat);
            if (_compositeMat != null) Destroy(_compositeMat);
            if (_augCam != null) Destroy(_augCam.gameObject);
            if (_outRT != null) { _outRT.Release(); Destroy(_outRT); }
            if (_augRT != null) { _augRT.Release(); Destroy(_augRT); }
            if (_compositeRT != null) { _compositeRT.Release(); Destroy(_compositeRT); }
        }
    }
}
