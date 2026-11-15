# Space Shuttle: especificação para o interior

Medidas oficiais do exterior (feito pelo chat do mapa), para encaixar o interior. Fonte: `Assets/CentroTreinamento/Blender~/gerar_plataforma_lancamento.py`.

## Arquivos do exterior
- FBX: `AstroLabUnity/AgentScripts/staging/Modelos/OnibusEspacial.fbx` (staging, ainda fora do projeto Unity).
- Gerador: `Assets/CentroTreinamento/Blender~/gerar_plataforma_lancamento.py` (somente leitura para quem faz o interior).
- `Onibus_Interior` dentro do FBX é só um placeholder: o interior novo o substitui.

## Eixos e origem (referencial do orbitador)
- **X** = para o nariz · **Z** = teto/baía de carga · **Y** = bombordo (esquerda).
- Na plataforma, a nave fica em pé (X do orbitador = cima no mundo).
- Origem do FBX = topo da MLP, no eixo do tanque externo. No mapa, a nave vai para a posição Blender (44, 290, 13.5).
- LODs em objetos separados: sufixos `_L1` e `_L2`.
- **Âncora do interior:** empty `NAVE_ANCORA_INTERIOR` = piso do middeck, no centro, em X=30.0, Y=0, Z=0.6, com os mesmos eixos. **O interior deve ter a origem neste ponto.**

## Flight deck
- X 26.8 → 33.2 (~6,4 m, incluindo o painel frontal). Para-brisa em X 32.45–33.25.
- Largura útil ~4,7 m (casco interno em X≈30.5: meia-largura 2,43).
- Piso Z=2.9 (+2,3 acima da âncora) · teto ~Z 5.6 (2,7 m de altura).
- Assento do comandante: X 31.0, Y +0.62 · piloto: Y −0.62 · olhos em Z 4.23.

## Middeck
- X 27.0 → 31.5 (~4,5 m) · largura ~4,6 m · piso Z 0.6 · teto ~Z 2.8 (2,2 m).

## Escotilha lateral
- Abertura real no lado de BOMBORDO (voltado para a White Room).
- Vão X 29.7–30.7 × Z 1.3–2.3 (~1×1 m), centro X 30.2, Z 1.8, Y +2.55.
- Tem moldura, e a porta abre dobrando para baixo.
