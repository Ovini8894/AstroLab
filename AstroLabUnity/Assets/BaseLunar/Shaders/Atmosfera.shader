// AstroLab/Atmosfera — halo de atmosfera do planeta Terra (casca um pouco maior que o planeta).
// Brilho aditivo que cresce na borda (fresnel) e some suavemente na beirada externa da casca.
Shader "AstroLab/Atmosfera"
{
    Properties
    {
        [HDR] _Cor ("Cor do halo", Color) = (0.5, 0.83, 1, 1)
        _Potencia ("Concentração na borda", Range(0.5, 8)) = 2.5
        _Suavidade ("Sumiço na beirada", Range(0.01, 0.6)) = 0.3
        _Forca ("Força", Range(0, 4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Halo"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Cor;
                half _Potencia;
                half _Suavidade;
                half _Forca;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; };

            V Vert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                half ndv = saturate(dot(normalize(i.normalWS), normalize(GetWorldSpaceViewDir(i.positionWS))));
                half borda = pow(1.0 - ndv, _Potencia);
                half beirada = smoothstep(0.0, _Suavidade, ndv);
                return half4(_Cor.rgb * borda * beirada * _Forca, 1);
            }
            ENDHLSL
        }
    }
}
