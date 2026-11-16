# AstroLab: relatório detalhado de estado e pendências

Data: 15/11/2026, ~23:15 · Branch `vinicius` = `test-branch` · Unity 6000.6.9f1, URP.
Complementa `Docs/PENDENCIAS_DEVS.md` (versão curta). Nível de confiança de cada item: **[log]** validado por log/bot/eval, **[play]** visto em Play pelo Vinicius, **[não testado]**.

## 1. Resumo executivo
- O jogo tem fluxo completo montado: **MenuInicial → Centro de Treinamento (7 etapas guiadas pela Lima) → escotilha da White Room → interior do Shuttle** (e Lua/SampleScene já existiam).
- Tudo compila com 0 erros (o único erro do console é o 403 do pacote de compras, anterior ao trabalho de hoje).
- **A versão corrigida nunca foi jogada.** O Vinicius jogou uma versão anterior e achou 12 bugs; as correções estão validadas só por log/bot. O primeiro passo antes de postar é um teste de ponta a ponta.
- Interatividade: o Centro é um **tour guiado**, com uma única atividade interativa (3 saltos na gravidade lunar). A nave é realmente interativa (16 controles).

## 2. Build Settings (ordem das cenas)
0 MenuInicial · 1 CentroTreinamento · 2 Lua · 3 SampleScene · 4 NaveInterior.

## 3. Entregas por área

### 3.1 Menu inicial (Assets/MenuInicial), concluído
Menu com Lua, Terra, bandeira, foguete; API de configurações e idiomas (`AstroLab.Settings.GameSettings`, `AstroLab.Localization.Loc`). Sem chat ativo.

### 3.2 Centro de Treinamento (Assets/CentroTreinamento)
- **L1:** recepção + elevador secreto (desce 20 m).
- **L2:** corredor cilíndrico de 200 m. Decisão de hoje: portas em diagonal removidas, ficam só os **portais** (moldura, anel de status e placa), sem folhas nem colisor.
- **L3:** 6 salas refeitas no tamanho aprovado (Médica 14x12, RV 18x14, Trajes 14x18; Centrífuga ~29x24, NBL ~31x22 e ARGOS ~26x23 ficaram maiores porque centrífuga e piscina não cabiam nas medidas originais).
- **L4:** elevador de saída (sobe 20 m até o quiosque), falas e textos em pt-BR/en/es (120 chaves; `npc.json` com 484 linhas por idioma).
- **Tutorial guiado** (`TutorialCentro.cs`, 339 linhas): 7 etapas, marcador de objetivo, HUD de objetivo, legendas automáticas, portas e consoles por etapa.
- **Scripts:** TutorialCentro, NpcInstrutor, LimaAnimador, CaixaDialogo, HudCentro, MarcadorObjetivo, PortaHex, PortaAutomatica, PassagemSecreta, ElevadorTorre, ElevadorSaida, ControleLancamento, PonteArgos, Centrifuga, Deslizador, ZonaGravidade, PerfilGravidade, AssentoCabine, Interagivel, InteracaoJogador, EstadoVestimentaJogador, AplicadorConfiguracoesJogador.
- **Mãos:** shader `AstroLab/MaosCentro` (sempre por cima do cenário). Hoje ganhou os passes DepthOnly/DepthNormals (o contorno full-screen desenhava as arestas do cenário por cima das mãos); luvas de treino agora usam MaosCentro. Mãos nuas na Terra, luvas de treino em NBL/ARGOS, traje completo depois da Sala de Trajes.
- **Lima v4** (aprovada pelo Vinicius, sem o "canudo" = zíper até o queixo): MPFB2, ~18k tris, 1,78 m, rig Generic (sem retarget), animações Idle/Walk/Talk/Point/Wave, piscar e boca por blend shape, LookAt da cabeça. Cabelo com volume e coque, gola fina, cinto, crachá e patch colados ao peito.

### 3.3 Shuttle
- **Exterior** (`CentroTreinamento/Modelos/OnibusEspacial.fbx`, 3,2 MB): aprovado; posicionado na plataforma de lançamento (Unity −44, 13.5, −290 = Blender 44, 290, 13.5). Eixos: X = nariz, Z = teto, Y = bombordo; nave em pé na plataforma; LODs `_L1`/`_L2`. Âncora do interior: `NAVE_ANCORA_INTERIOR` (X 30, Y 0, Z 0.6). Detalhes em `Docs/NAVE_INTERIOR_ESPEC.md`.
- **Plataforma** (`PlataformaLancamento.fbx`): barra branca diagonal corrigida (era um vértice solto de bevel em `Plataforma_BracoAcesso`). Os cabos finos da torre são intencionais (para-raios).
- **Interior** (Assets/NaveInterior): flight deck (painéis F/C/O/laterais, 11 telas MEDS, glareshield, assentos com cinto de 5 pontos, manche e pedais) + middeck (36 lockers, escotilha lateral com moldura, airlock, escada, corrimãos, WCS). **199,5k triângulos** (orçamento 200k), 2.416 instâncias do kit, 27 materiais AstroLab/Toon com GPU instancing, atlas com 300+ etiquetas.
- **16 interativos** (mira 2,5 m, realce ciano pulsante, nome TMP, estado ON/OFF, animação, som por código, luz de estado): master alarm, MEDS, luz flood/dome, comm (legenda de Houston), ventilação, APU 1–3, MN A/B/C, células de combustível, portas da baía, radiadores, RMS. Checklist na HUD por fase (Plataforma / Órbita).
- **Postos (Tab):** CDR → PLT → estação de ré. **Segurar M** volta (`SistemasNave.CenaRetorno = "CentroTreinamento"`).
- **Ligação:** escotilha da White Room → `SistemasNave.FaseInicial = Plataforma` + `LoadScene("NaveInterior")`. Sem definir fase, a nave fica em órbita com a Terra na janela.

## 4. Bugs do último teste do Vinicius e status
| # | Bug | Causa / correção | Verificação |
|---|---|---|---|
| 1 | Texto/faixa laranja gigante no mapa | Mostradores dos elevadores L1/L4 com 93 m (cabine do FBX com escala 100); escala compensada | [log] |
| 2 | Bloco laranja na tela do corredor | Mesma causa do item 1 | [log] |
| 3 | Crachá/patch fora do peito da Lima | Colados à superfície, pesos da pele | [log] |
| 4 | Lima anda com pernas abertas | Pose corrigida, rig Generic sem retarget | [log] |
| 5 | Vão no elevador | Soleiras L1 (havia 45 cm) e L4; laje do quiosque não cobre o poço | [log] |
| 6 | Mãos não sobrepõem itens | Passes DepthOnly/DepthNormals em MaosCentro | [log] |
| 7 | Porta bugada | Folhas removidas, só portais | [log] |
| 8 | Jogador não entra na Centrífuga | Colisor "Bloqueio" da porta, removido junto com as folhas; bot varre cápsula do jogador (raio 0,32), 0 obstáculos | [log] |
| 9 | Travou na etapa 5 (NBL) | Recompilação de scripts durante o Play mata as corrotinas. Não é bug de lógica | **Ação:** Preferences > General > Script Changes While Playing = "Recompile After Finished Playing" |

## 5. Verificado por bot/log (não substitui jogar)
Rota L1→L4 "trajeto completo OK": 95 trechos, 0 sem piso, 0 obstáculos; 7 etapas com porta e console; 120 chaves em 3 idiomas; nave com 0 erros em modo de edição.

## 6. Nunca testado em Play (lista de teste)
1. MenuInicial → Iniciar → CentroTreinamento.
2. As 7 etapas completas; Lima falando, animando e olhando; legendas do corredor.
3. Centrífuga, NBL, RV, Trajes (3 saltos), ARGOS.
4. Subida do elevador do L4; estrada → torre → braço de acesso → escotilha.
5. LoadScene da NaveInterior; fase Plataforma; Tab entre postos; clique nos 16 controles; mãos; checklist concluído (logs `[Nave]`); segurar M volta ao Centro.
6. Estilo e contorno da Lima no Unity (nunca visto em imagem final).

## 7. O que falta (por prioridade e sugestão de dono)
1. **Teste ponta a ponta + correções.** Todos. (Passo mais importante.)
2. **Mecânicas jogáveis nas estações.** Hoje: est0 recepção (texto), est1 Médica (batimentos), est2 Centrífuga (força G automática), est3 RV (tela azul), est4 NBL (profundidade automática), est6 traje (fade) são cenas guiadas sem ação do jogador; só est5 (3 saltos) é interativa. Ideias: segurar botão na centrífuga; tarefa de posicionar algo na NBL; mini-exercício na RV. Pedro Scalon / Pedro Henrique.
3. **Clique da escotilha lateral da nave** (`ANIM_EscotilhaLateral` pronto, sem clique; fica no middeck, longe dos postos). Nicholas Prado.
4. **Integrar o exterior nas animações dos devs** (arquivo já na branch). Pedro Scalon / Nicholas.
5. **Torre da plataforma:** polígonos auto-intersectantes descartados no import; corrigir no gerador Blender. `Onibus_Interior` dentro do FBX exterior é placeholder e pode ser removido.
6. **Fluxo pós-Centro:** conferir L4 → Lua e SampleScene. Pedro Henrique / Nicholas.
7. **Build Windows** e teste fora do Editor. Quem postar.
8. **Postagem do desafio:** texto, vídeo/GIF, link do build e do repositório; incluir `Docs/AstroLab_Pitch_English_v3.pptx` (ainda não versionado) se quiser. Vinicius.

## 8. Riscos conhecidos
- Interior com 199,5k tris no teto do orçamento (static batching e occluder ligados; medir FPS no build).
- Recompilação durante Play derruba o tutorial; nenhum dev deve salvar `.cs` enquanto alguém testa.
- Um único Editor compartilhado: avisar antes de Play ou import grande.
- Lima/rig Generic: se algum dev precisar de retarget Humanoid, terá de refazer o rig.

## 9. Higiene do repositório
- `AstroLabUnity/AgentScripts/` entrou no git por engano; limpar quando o Vinicius pedir. Nada novo ali.
- Não commitar `LiberationSans SDF*.asset` nem `Resources/GDKEditionAutoGen` (o Unity altera sozinho).
- Nunca `AssetDatabase.SaveAssets()` / "Save Project" (suja os materiais da BaseLunar).
- `stash@{0}` antigo de settings do Unity: decidir se descarta.
- Commits: só caminhos explícitos; sem checkout/stash/reset/merge sem pedido do Vinicius. Fluxo: `vinicius` → `test-branch` por fast-forward.

## 10. Créditos
Vinicius Candido (Desenvolvedor e Modelador 3D), Pedro Scalon, Pedro Henrique Gonçalves, Nicholas Prado (Desenvolvedores), Prof. Matheus Valente (agradecimento, Uniube). Manter o crédito CC0 para MPFB2/Quaternius (a Lima usa MPFB2). Logos da NASA são texturas separadas e trocáveis (marca registrada).

## 11. Commits de hoje na branch
- `7df2af4` modelo 3D externo do Shuttle
- `d67da86` interior do Shuttle (102 arquivos)
- `940904e` Centro L3/L4, Lima v4, portais, ligação com a nave, Build Settings (63 arquivos)
- `182f9e6` pendências
