// Gantasmo passthrough stitch.
//
// Reprojects the two forward passthrough RGB cameras onto one centred virtual
// camera and feather-blends the overlap into a single 16:9 image. The reproject
// is a plane-induced homography: each output pixel is a ray from the virtual
// camera, intersected with a fronto-parallel focal plane at _FocalDist, then
// projected into each physical camera using that camera's intrinsics + crop +
// world pose (all supplied per-frame by the C# driver). Objects near the focal
// plane line up cleanly; objects far off it show the usual parallax double-image,
// which is why _FocalDist is tunable for the scene depth you care about.
//
// All maths mirrors PassthroughCameraAccess.WorldToViewportPoint /
// CalcSensorCropRegion so the sampled UV matches what the SDK would compute.
Shader "Gantasmo/PassthroughStitch"
{
    Properties
    {
        _LeftTex ("Left Camera", 2D) = "black" {}
        _RightTex ("Right Camera", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ GANTASMO_DEPTH_STITCH
            #include "UnityCG.cginc"

            sampler2D _LeftTex;
            sampler2D _RightTex;

            // World -> camera (inverse of the camera's world pose, no scale).
            float4x4 _WorldToCamL;
            float4x4 _WorldToCamR;
            // Virtual (output) camera local -> world (TRS, no scale).
            float4x4 _VirtualToWorld;

            // Per-camera intrinsics packed as (focalX, focalY, principalX, principalY)
            // and crop as (x, y, width, height), all in sensor pixels.
            float4 _IntrL;
            float4 _IntrR;
            float4 _CropL;
            float4 _CropR;

            float _TanHalfHFov;   // tan(hfov/2) of the virtual camera
            float _TanHalfVFov;   // tan(vfov/2) of the virtual camera
            float _FocalDist;     // metres to the reprojection plane
            float _BlendWidth;    // half-width of the central feather, in output-x (0..0.49)
            float _FlipY;         // 1 = flip sampled V (camera texture origin differs)

#ifdef GANTASMO_DEPTH_STITCH
            // Globals published every frame by Meta's EnvironmentDepthManager. The
            // depth texture is a per-eye array; the reprojection matrix maps a world
            // point into that eye's depth clip space, and ZBufferParams linearise the
            // sampled non-linear depth. Math mirrors Meta's SampleEnvironmentDepthLinear
            // / CalculateEnvironmentDepthOcclusion so distances match the SDK.
            UNITY_DECLARE_TEX2DARRAY(_EnvironmentDepthTexture);
            float4x4 _EnvironmentDepthReprojectionMatrices[2];
            float4 _EnvironmentDepthZBufferParams;

            // Linear eye-space depth (metres) of the real scene at one eye's depth UV.
            // Returns < 0 when the sample carries no data.
            float SceneLinearDepth(float2 uv, int eye)
            {
                float r = UNITY_SAMPLE_TEX2DARRAY(_EnvironmentDepthTexture, float3(uv, (float)eye)).r;
                float ndc = r * 2.0 - 1.0;
                if (ndc <= -1.0 + 1e-6) return -1.0;       // empty sample
                if (ndc >=  1.0 - 1e-6) return 1.0e4;      // far field (Meta clamps to ~10km)
                return (1.0 / (ndc + _EnvironmentDepthZBufferParams.y)) * _EnvironmentDepthZBufferParams.x;
            }
#endif

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Project a world point into one camera's texture. Returns rgb in .rgb
            // and validity (1 if the sample is in front of and inside that camera) in .a.
            half4 sampleCam(sampler2D tex, float4x4 worldToCam, float4 intr, float4 crop, float3 pWorld)
            {
                float3 pc = mul(worldToCam, float4(pWorld, 1.0)).xyz;
                if (pc.z <= 0.0) return half4(0,0,0,0);
                // sensor pixel = (x/z)*focal + principal
                float2 sp = float2(pc.x / pc.z, pc.y / pc.z) * intr.xy + intr.zw;
                // sensor pixel -> cropped viewport (0..1, bottom-left origin)
                float2 vp = (sp - crop.xy) / crop.zw;
                if (vp.x < 0.0 || vp.x > 1.0 || vp.y < 0.0 || vp.y > 1.0) return half4(0,0,0,0);
                float2 uv = vp;
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                half3 c = tex2D(tex, uv).rgb;
                return half4(c, 1.0);
            }

            half4 frag (v2f i) : SV_Target
            {
                // Ray for this output pixel, in virtual-camera space, then world.
                float3 dirV = float3((i.uv.x * 2.0 - 1.0) * _TanHalfHFov,
                                     (i.uv.y * 2.0 - 1.0) * _TanHalfVFov,
                                     1.0);
                float3 vPos = float3(_VirtualToWorld._m03, _VirtualToWorld._m13, _VirtualToWorld._m23);
                float3 dirW = normalize(mul((float3x3)_VirtualToWorld, dirV));

                // Distance along the ray to the surface we reproject. Default is the
                // tunable fronto-parallel focal plane (lines up one depth, doubles the
                // rest). With environment depth on, replace it per-pixel with the real
                // scene distance so every depth lines up across the seam.
                float dist = _FocalDist;
#ifdef GANTASMO_DEPTH_STITCH
                // Left half of the output trusts the left eye's depth slice, right half
                // the right eye's, keeping the sampled depth on the same side as the
                // camera that ends up owning the pixel.
                int eye = (i.uv.x < 0.5) ? 0 : 1;
                float4 ds = mul(_EnvironmentDepthReprojectionMatrices[eye], float4(vPos + dirW * 50.0, 1.0));
                if (ds.w > 1e-4)
                {
                    float2 duv = (ds.xy / ds.w) * 0.5 + 0.5;
                    if (duv.x >= 0.0 && duv.x <= 1.0 && duv.y >= 0.0 && duv.y <= 1.0)
                    {
                        float d = SceneLinearDepth(duv, eye);    // eye-space Z, metres
                        float3 fwd = normalize(mul((float3x3)_VirtualToWorld, float3(0.0, 0.0, 1.0)));
                        float cosA = dot(dirW, fwd);
                        if (d > 0.05 && d < 1.0e3 && cosA > 1e-3)
                            dist = d / cosA;                     // eye-Z -> distance along this ray
                    }
                }
#endif
                float3 pWorld = vPos + dirW * dist;

                half4 cl = sampleCam(_LeftTex, _WorldToCamL, _IntrL, _CropL, pWorld);
                half4 cr = sampleCam(_RightTex, _WorldToCamR, _IntrR, _CropR, pWorld);

                // Spatial cross-fade: left owns the left side, right owns the right,
                // feathering across a central band of half-width _BlendWidth. Validity
                // (alpha) gates each so a pixel only one camera sees uses that camera.
                float t = saturate((i.uv.x - (0.5 - _BlendWidth)) / max(2.0 * _BlendWidth, 1e-4));
                float wL = (1.0 - t) * cl.a;
                float wR = t * cr.a;
                float sum = wL + wR;
                if (sum < 1e-4) return half4(0, 0, 0, 1); // neither camera covers this pixel
                half3 col = (cl.rgb * wL + cr.rgb * wR) / sum;
                return half4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
