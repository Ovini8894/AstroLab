// AstroLab/Menu/Aditivo — brilho aditivo sem luz (traço de luz do lançamento, halos). Cor HDR para o Bloom.
Shader "AstroLab/Menu/Aditivo"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        [HDR] _Cor ("Cor", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Aditivo"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Cor;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 cor : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 cor : COLOR; };

            V Vert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.cor = v.cor;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half a = t.a * i.cor.a;
                return half4(t.rgb * i.cor.rgb * _Cor.rgb * a * _Cor.a, 1);
            }
            ENDHLSL
        }
    }
}
