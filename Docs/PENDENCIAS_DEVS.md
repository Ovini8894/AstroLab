# AstroLab: pendências para juntar tudo e postar o jogo

Estado em 15/11/2026, ~23:10, branch `vinicius` (= `test-branch`).
Cenas no build: 0 MenuInicial · 1 CentroTreinamento · 2 Lua · 3 SampleScene · 4 NaveInterior.

## 1. O que foi entregue (compila com 0 erros; só o 403 do pacote IAP, que já existia)
- **Menu inicial** (Assets/MenuInicial): concluído.
- **Centro de Treinamento** (Assets/CentroTreinamento): L1 (recepção + elevador secreto), L2 (corredor + portais), L3 (6 salas), L4 (elevador de saída, falas pt/en/es), tutorial guiado em 7 etapas com a instrutora Lima (v4, rig Generic, 5 animações, piscar, boca, LookAt).
- **Shuttle exterior** (CentroTreinamento/Modelos/OnibusEspacial.fbx): aprovado, na plataforma de lançamento.
- **Shuttle interior** (Assets/NaveInterior): flight deck + middeck, 16 controles interativos (mira, destaque, clique, checklist), 3 postos (Tab), mãos de luva, "segure M = voltar".
- **Ligação**: escotilha da White Room → `SistemasNave.FaseInicial = Plataforma` + `LoadScene("NaveInterior")`.

## 2. NUNCA foi testado em Play (testar primeiro, nesta ordem)
1. MenuInicial → Iniciar → Centro. O Vinicius jogou a versão anterior e achou 12 bugs; as correções (portas sem folhas, elevadores, mãos, Lima, escala 100 dos mostradores) foram validadas só por log/bot.
2. Etapa 5 (Piscina) travava porque o Unity recompilou durante o Play. **Ajustar Preferences > General > Script Changes While Playing = "Recompile After Finished Playing"** e não salvar .cs durante o teste.
3. Entrada pela escotilha da White Room → carregamento da NaveInterior → checklist de plataforma → segurar M volta ao Centro.
4. Subida do elevador do L4, caminho estrada → torre → braço de acesso → escotilha.
5. Lima em jogo: pernas, crachá, olhar, legendas do corredor.

## 3. O que falta fazer (por prioridade)
| # | Item | Quem sugerido |
|---|---|---|
| 1 | Teste completo de ponta a ponta e correção dos bugs que aparecerem | todos |
| 2 | Mecânicas **jogáveis** nas estações: hoje só a est5 (3 saltos na gravidade lunar) é interativa; est0–est4 e est6 são cenas guiadas automáticas (Médica, Centrífuga, RV, NBL, traje). Ideias: segurar botão na centrífuga, tarefa na NBL, mini-exercício na RV | Pedro Scalon / Pedro Henrique |
| 3 | Escotilha lateral da nave: objeto `ANIM_EscotilhaLateral` existe, falta o clique | Nicholas Prado |
| 4 | Integrar o exterior da nave nas animações dos devs (OnibusEspacial.fbx está na branch; eixos e âncoras em `Docs/NAVE_INTERIOR_ESPEC.md`; LODs `_L1`/`_L2`) | Pedro Scalon / Nicholas |
| 5 | Torre da plataforma (`Plataforma_Torre`) tem polígonos auto-intersectantes que o Unity descarta; corrigir no gerador | quem mexer no Blender do mapa |
| 6 | Cena Lua / SampleScene: confirmar a ligação com o fim do Centro (L4 → Lua) e o fluxo pós-nave | Pedro Henrique / Nicholas |
| 7 | Build do jogo (Windows) e teste fora do Editor; conferir cenas no Build Settings | quem for postar |
| 8 | Postar o desafio: texto, vídeo/GIF, link do build e do repositório | Vinicius |

## 4. Limpeza e cuidados
- `AstroLabUnity/AgentScripts/` entrou no git por engano: ninguém cria nada novo ali; limpar quando o Vinicius pedir.
- Não commitar `LiberationSans SDF*.asset` nem `Resources/GDKEditionAutoGen` (o Unity altera sozinho).
- Nunca `AssetDatabase.SaveAssets()` / "Save Project" (suja os materiais da BaseLunar).
- `stash@{0}` antigo (settings do Unity antes do merge pedro-h): decidir se descarta.
- Lima tem só o script e o FBX no repositório; o `.blend` do centro está em `CentroTreinamento/Blender~/`.

## 5. Créditos (tela de créditos já pronta)
Vinicius Candido (Desenvolvedor e Modelador 3D), Pedro Scalon, Pedro Henrique Gonçalves, Nicholas Prado (Desenvolvedores), Prof. Matheus Valente (agradecimento, Uniube). Se MPFB2/Quaternius forem usados no build final, manter o crédito CC0 (a Lima usa MPFB2). Logos da NASA são texturas separadas e trocáveis.
