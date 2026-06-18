// Gantasmo passthrough composite.
//
// Lays the virtual 3D layer (rendered by a head-center "MRC" camera into _AugTex,
// transparent where there is no virtual content) over the stitched passthrough
// (_MainTex). This is the Mixed-Reality-Capture composite: out = lerp(passthrough,
// virtual.rgb, virtual.a). Used via Graphics.Blit so _MainTex is the source
// (passthrough) and _AugTex is bound by the C# driver.
//
// _AugFlipV flips the virtual layer's V if the camera-rendered RT comes through
// upside down relative to the blit-written passthrough (RT y-flip conventions
// differ by graphics API / how the RT was produced).
Shader "Gantasmo/PassthroughComposite"
{
    Properties
    {
        _MainTex ("Passthrough", 2D) = "black" {}
        _AugTex ("Augmentation", 2D) = "black" {}
        _AugFlipV ("Flip augmentation V", Float) = 0
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _AugTex;
            float _AugFlipV;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 bg = tex2D(_MainTex, i.uv);
                float2 aUv = i.uv;
                if (_AugFlipV > 0.5) aUv.y = 1.0 - aUv.y;
                half4 fg = tex2D(_AugTex, aUv);
                half3 col = lerp(bg.rgb, fg.rgb, saturate(fg.a));
                return half4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
