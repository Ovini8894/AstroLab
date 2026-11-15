Shader "AstroLab/PrimeiraPessoa"
{
    Properties
    {
        _BaseColor ("Cor", Color) = (0.72,0.82,0.91,1)
        _EmissionColor ("Luz do instrumento", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalVS:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.normalVS=TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS),true);
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half3 n=normalize(input.normalVS);
                half key=saturate(dot(n,normalize(half3(-.45,.75,1))));
                half fill=saturate(dot(n,normalize(half3(.7,.15,.6))));
                return half4(_BaseColor.rgb*(.35+.62*key+.10*fill)+_EmissionColor.rgb,1);
            }
            ENDHLSL
        }
    }
}
