// Crowd of the stands in one mesh.
// uv0 = position of the fan inside the stand (0..1) -> used to paint the giant flag (koreografi).
// uv1.x = colour slot (0 = team primary, 1 = team secondary, 2 = neutral vertex colour, 3 = skin/vertex colour)
// uv1.y = random phase for the jump.
Shader "DTK/Crowd"
{
    Properties
    {
        _ColorA ("Team primary", Color) = (0.8,0.1,0.1,1)
        _ColorB ("Team secondary", Color) = (1,1,1,1)
        _FlagTex ("Flag", 2D) = "white" {}
        _Jump ("Jump 0-1", Range(0,1)) = 0.15
        _Choreo ("Choreo 0-1", Range(0,1)) = 0
        _Ambient ("Ambient", Float) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _ColorA;
            float4 _ColorB;
            float4 _FlagTex_ST;
            float _Jump;
            float _Choreo;
            float _Ambient;
        CBUFFER_END
        TEXTURE2D(_FlagTex); SAMPLER(sampler_FlagTex);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float2 uv0 : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
        };

        float3 Bounce(float3 p, float2 uv1)
        {
            float t = _Time.y * 9.0 + uv1.y * 6.2831;
            p.y += _Jump * abs(sin(t)) * 0.38 * (0.6 + 0.4 * frac(uv1.y * 7.31));
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 p = Bounce(i.positionOS.xyz, i.uv1);
                o.positionCS = TransformObjectToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                float slot = i.uv1.x;
                float3 c = slot < 0.5 ? _ColorA.rgb : (slot < 1.5 ? _ColorB.rgb : i.color.rgb);
                float3 flag = SAMPLE_TEXTURE2D_LOD(_FlagTex, sampler_FlagTex, i.uv0, 0).rgb;
                float k = _Choreo * (slot < 2.5 ? 1.0 : 0.85);
                o.color = float4(lerp(c, flag, k), 1);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                Light l = GetMainLight();
                float3 n = normalize(i.normalWS);
                float ndl = saturate(dot(n, l.direction)) * 0.75 + 0.25;
                float3 col = i.color.rgb * (l.color * ndl * 0.75 + _Ambient);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(Attributes i) : SV_POSITION { return TransformObjectToHClip(Bounce(i.positionOS.xyz, i.uv1)); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct V2 { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V2 vert(Attributes i)
            {
                V2 o;
                o.positionCS = TransformObjectToHClip(Bounce(i.positionOS.xyz, i.uv1));
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }
            half4 frag(V2 i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
}
