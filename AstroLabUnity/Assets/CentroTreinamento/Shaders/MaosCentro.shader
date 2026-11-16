// Mãos em primeira pessoa do Centro de Treinamento (baseado no AstroLab/PrimeiraPessoa do time, que fica intocado).
// - iluminação idêntica à do shader do time
// - "depth hack": a profundidade das mãos é comprimida para perto do plano próximo, então elas nunca entram
//   em paredes/objetos (sem câmera extra nem layer novo). A ordem entre os próprios dedos é preservada.
Shader "AstroLab/MaosCentro"
{
    Properties
    {
        _BaseColor ("Cor", Color) = (0.85,0.63,0.48,1)
        _EmissionColor ("Emissão", Color) = (0,0,0,1)
        _Compressao ("Compressão da profundidade", Range(0.02, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
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
                float _Compressao;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalVS:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                #if UNITY_REVERSED_Z
                    o.positionCS.z = lerp(o.positionCS.w, o.positionCS.z, _Compressao);    // perto = z/w -> 1
                #else
                    o.positionCS.z = lerp(-o.positionCS.w, o.positionCS.z, _Compressao);   // perto = z/w -> -1
                #endif
                o.normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS), true);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // mesma luz do AstroLab/PrimeiraPessoa do time (visual aprovado), só a profundidade muda
                half3 n = normalize(input.normalVS);
                half key = saturate(dot(n, normalize(half3(-.45, .75, 1))));
                half fill = saturate(dot(n, normalize(half3(.7, .15, .6))));
                return half4(_BaseColor.rgb * (.35 + .62 * key + .10 * fill) + _EmissionColor.rgb, 1);
            }
            ENDHLSL
        }

        // profundidade e normais das mãos (mesma compressão): entram nas texturas de depth/normais da câmera,
        // então o contorno full-screen desenha a silhueta das mãos em vez das arestas do cenário atrás delas
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex VertP
            #pragma fragment FragD
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                float _Compressao;
            CBUFFER_END
            struct AttrP { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct VarP { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            VarP VertP(AttrP input)
            {
                VarP o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                #if UNITY_REVERSED_Z
                    o.positionCS.z = lerp(o.positionCS.w, o.positionCS.z, _Compressao);
                #else
                    o.positionCS.z = lerp(-o.positionCS.w, o.positionCS.z, _Compressao);
                #endif
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half FragD(VarP i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex VertP
            #pragma fragment FragN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                float _Compressao;
            CBUFFER_END
            struct AttrP { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct VarP { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            VarP VertP(AttrP input)
            {
                VarP o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                #if UNITY_REVERSED_Z
                    o.positionCS.z = lerp(o.positionCS.w, o.positionCS.z, _Compressao);
                #else
                    o.positionCS.z = lerp(-o.positionCS.w, o.positionCS.z, _Compressao);
                #endif
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 FragN(VarP i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
