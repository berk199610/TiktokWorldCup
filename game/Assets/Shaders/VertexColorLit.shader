// Simple lit shader that uses mesh vertex colours (the procedural football is black/white by vertex colour).
// Receives main-light shadows, casts shadows, has depth passes so it works with every URP renderer setting.
Shader "DTK/VertexColorLit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Gloss ("Gloss", Range(0,1)) = 0.35
        _Ambient ("Ambient", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Gloss;
            float _Ambient;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 color : COLOR; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color * _BaseColor;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light l = GetMainLight(sc);
                float3 n = normalize(i.normalWS);
                float ndl = saturate(dot(n, l.direction));
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float spec = pow(saturate(dot(n, normalize(l.direction + v))), 32) * _Gloss;
                float3 col = i.color.rgb * (l.color * ndl * l.shadowAttenuation + _Ambient) + spec * l.color * l.shadowAttenuation;
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float3 _LightDirection;
            float4 vert(Attributes i) : SV_POSITION
            {
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(i.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, nws, _LightDirection));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }
            half frag() : SV_Target { return 0; }
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
            float4 vert(Attributes i) : SV_POSITION { return TransformObjectToHClip(i.positionOS.xyz); }
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
            V2 vert(Attributes i) { V2 o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(V2 i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
}
