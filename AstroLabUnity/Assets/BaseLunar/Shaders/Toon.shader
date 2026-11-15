// AstroLab/Toon — cel shading da Base Lunar (URP 17, Forward e Forward+).
// 3 faixas de luz (luz / meio-tom / sombra) com sombras COLORIDAS (nunca pretas), luzes pontuais em
// "poças" de 2 degraus, borda iluminada (rim), brilho duro opcional e emissão.
// As cores de sombra/meio-tom/ambiente vêm do componente IluminacaoCartoon da cena (variáveis globais);
// sem ele o shader usa valores padrão.
Shader "AstroLab/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textura", 2D) = "white" {}
        [MainColor] _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        _RimForca ("Borda iluminada (força)", Range(0, 1)) = 0.35
        _RimLargura ("Borda iluminada (largura)", Range(0.05, 0.6)) = 0.25
        _BrilhoTamanho ("Brilho duro (tamanho, 0 = desligado)", Range(0, 0.3)) = 0
        _BrilhoForca ("Brilho duro (força)", Range(0, 2)) = 0.6
        [HDR] _EmissionColor ("Emissão", Color) = (0, 0, 0, 1)
        [Toggle] _EmissaoUsaTextura ("Emissão usa a textura (telas)", Float) = 0
        [Toggle] _SemNevoa ("Ignorar neblina (objetos do céu)", Float) = 0
        _FaixaDesloc ("Deslocar faixas (relevo suave pega meio-tom antes)", Range(0, 0.5)) = 0

        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        [HideInInspector] _Cull ("__cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _RimForca;
            half _RimLargura;
            half _BrilhoTamanho;
            half _BrilhoForca;
            half4 _EmissionColor;
            half _EmissaoUsaTextura;
            half _SemNevoa;
            half _FaixaDesloc;
            half _ZWrite;
        CBUFFER_END
        ENDHLSL

        // ------------------------------------------------------------------ cor
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // globais (IluminacaoCartoon)
            half _ToonAtivo;
            half4 _ToonSombra;
            half4 _ToonMeioTom;
            half4 _ToonCeu;
            half4 _ToonChao;
            half4 _ToonRim;
            half _ToonLimiteMeio;
            half _ToonLimiteSombra;
            half _ToonSuavidade;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // 2 degraus para luzes pontuais: borda da poça de luz + miolo mais forte
            half Poca(half atenuacao)
            {
                return smoothstep(0.015, 0.03, atenuacao) * 0.55 + smoothstep(0.2, 0.26, atenuacao) * 0.45;
            }

            half3 LuzExtra(Light luz, half3 N, half3 albedo)
            {
                half ndl = dot(N, luz.direction);
                half lado = smoothstep(-0.05, 0.05, ndl) * 0.8 + 0.2;            // "wrap" leve: nada fica 100% sem a luz da sala
                half a = luz.distanceAttenuation * lerp(0.35, 1.0, luz.shadowAttenuation);
                half intens = max(max(luz.color.r, luz.color.g), luz.color.b);
                half3 cor = luz.color / max(intens, 1e-3);
                return albedo * cor * Poca(a) * lado * min(intens, 4.0) * 0.6;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;
                half alpha = tex.a * _BaseColor.a;

                half3 N = normalize(i.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));

                // valores do estilo (globais, com padrão)
                bool ativo = _ToonAtivo > 0.5;
                half3 sombra = ativo ? _ToonSombra.rgb : half3(0.36, 0.34, 0.6);
                half3 meio = ativo ? _ToonMeioTom.rgb : half3(0.72, 0.72, 0.9);
                half3 ceu = ativo ? _ToonCeu.rgb : half3(0.28, 0.3, 0.5);
                half3 chao = ativo ? _ToonChao.rgb : half3(0.2, 0.17, 0.22);
                half3 rimCor = ativo ? _ToonRim.rgb : half3(0.75, 0.85, 1.0);
                half lMeio = ativo ? _ToonLimiteMeio : 0.35;
                half lSombra = ativo ? _ToonLimiteSombra : 0.0;
                half s = ativo ? _ToonSuavidade : 0.025;

                // luz principal (sol) com sombra projetada entrando nas faixas
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sol = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                half ndl = dot(N, sol.direction);
                half x = min(ndl - _FaixaDesloc, lerp(-1.0, 1.0, sol.shadowAttenuation));
                half emLuz = smoothstep(lMeio - s, lMeio + s, x);
                half foraSombra = smoothstep(lSombra - s, lSombra + s, x);
                half3 rampa = lerp(sombra, lerp(meio, 1.0.xxx, emLuz), foraSombra);
                half3 cor = albedo * sol.color * rampa;

                // ambiente em hemisfério (céu x chão): mantém o lado da sombra vivo
                cor += albedo * lerp(chao, ceu, N.y * 0.5 + 0.5);

                // luzes extras (lâmpadas da base)
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = N;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint qtd = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                {
                    Light ld = GetAdditionalLight(li, i.positionWS, half4(1, 1, 1, 1));
                    cor += LuzExtra(ld, N, albedo);
                }
                #endif
                LIGHT_LOOP_BEGIN(qtd)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    cor += LuzExtra(l, N, albedo);
                LIGHT_LOOP_END
                #endif

                // brilho duro (opcional, materiais lisos)
                if (_BrilhoTamanho > 0.001)
                {
                    half ndh = dot(N, normalize(sol.direction + V));
                    half b = smoothstep(1.0 - _BrilhoTamanho - 0.01, 1.0 - _BrilhoTamanho + 0.01, ndh);
                    cor += sol.color * b * emLuz * _BrilhoForca;
                }

                // borda iluminada: recorta a silhueta contra o fundo escuro
                half fres = 1.0 - saturate(dot(N, V));
                half rim = smoothstep(1.0 - _RimLargura - 0.03, 1.0 - _RimLargura + 0.03, fres);
                cor += rimCor * rim * _RimForca * (0.35 + 0.65 * foraSombra) * lerp(0.6, 1.0, saturate(albedo.r + albedo.g + albedo.b));

                cor += _EmissionColor.rgb * lerp(1.0.xxx, tex.rgb, _EmissaoUsaTextura);
                if (_SemNevoa < 0.5) cor = MixFog(cor, i.fogFactor);
                return half4(cor, alpha);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ sombra
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertSombra
            #pragma fragment FragSombra
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VS { float4 positionCS : SV_POSITION; };

            VS VertSombra(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                VS o;
                float3 pWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 dir = normalize(_LightPosition - pWS);
                #else
                    float3 dir = _LightDirection;
                #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(pWS, nWS, dir)));
                return o;
            }

            half4 FragSombra(VS i) : SV_Target
            {
                clip(_ZWrite - 0.5);   // vidro (transparente) não faz sombra
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ profundidade
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertProf
            #pragma fragment FragProf
            #pragma multi_compile_instancing

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VS { float4 positionCS : SV_POSITION; };

            VS VertProf(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                VS o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half FragProf(VS i) : SV_Target
            {
                clip(_ZWrite - 0.5);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ normais (para o contorno)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertNorm
            #pragma fragment FragNorm
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VS { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            VS VertNorm(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                VS o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 FragNorm(VS i) : SV_Target
            {
                clip(_ZWrite - 0.5);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 n = normalize(i.normalWS);
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
