// AstroLab/ContornoCartoon — contorno de desenho animado em tela cheia (Full Screen Pass Renderer Feature).
// Detecta bordas pela profundidade (silhuetas) e pelas normais (quinas). Só age quando a cena tem o
// componente IluminacaoCartoon (_ContornoCartoonAtivo = 1); nas outras cenas devolve a imagem intacta.
Shader "AstroLab/ContornoCartoon"
{
    Properties
    {
        _Cor ("Cor do contorno", Color) = (0.09, 0.08, 0.22, 1)
        _Espessura ("Espessura (pixels)", Range(0.5, 3)) = 1.3
        _LimiarProfundidade ("Sensibilidade silhueta", Range(0.01, 1)) = 0.12
        _LimiarNormal ("Sensibilidade quinas", Range(0.05, 1)) = 0.35
        _DistanciaMax ("Distância máxima (m)", Float) = 140
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "Contorno"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _Cor;
            float _Espessura;
            float _LimiarProfundidade;
            float _LimiarNormal;
            float _DistanciaMax;
            float _ContornoCartoonAtivo;   // global (IluminacaoCartoon)
            half4 _ContornoCartoonCor;      // global (IluminacaoCartoon)

            float Prof(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_ContornoCartoonAtivo < 0.5) return col;

                float d0 = Prof(uv);
                if (d0 > _DistanciaMax) return col;

                float2 px = _Espessura / _ScreenParams.xy;
                float2 ox = float2(px.x, 0), oy = float2(0, px.y);

                // profundidade inversa é linear sobre planos: a 2ª derivada só acende em silhuetas
                float iz0 = 1.0 / d0;
                float izL = 1.0 / Prof(uv - ox), izR = 1.0 / Prof(uv + ox);
                float izD = 1.0 / Prof(uv - oy), izU = 1.0 / Prof(uv + oy);
                float lap = (abs(izL + izR - 2 * iz0) + abs(izD + izU - 2 * iz0)) / iz0;
                float bordaProf = smoothstep(_LimiarProfundidade, _LimiarProfundidade * 1.6, lap);

                // quinas: diferença de normal entre vizinhos
                float3 n0 = SampleSceneNormals(uv);
                float dn = 0;
                dn = max(dn, 1 - dot(n0, SampleSceneNormals(uv + ox)));
                dn = max(dn, 1 - dot(n0, SampleSceneNormals(uv - ox)));
                dn = max(dn, 1 - dot(n0, SampleSceneNormals(uv + oy)));
                dn = max(dn, 1 - dot(n0, SampleSceneNormals(uv - oy)));
                float bordaNorm = smoothstep(_LimiarNormal, _LimiarNormal * 1.4, dn);

                float borda = max(bordaProf, bordaNorm);
                borda *= 1 - smoothstep(_DistanciaMax * 0.55, _DistanciaMax, d0);   // some aos poucos ao longe

                half4 corLinha = _ContornoCartoonCor.a > 0 ? _ContornoCartoonCor : _Cor;
                return half4(lerp(col.rgb, corLinha.rgb, borda * corLinha.a), col.a);
            }
            ENDHLSL
        }
    }
}
