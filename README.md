# AstroLab

Projeto Unity 3D com Universal Render Pipeline (URP), localizado em `AstroLabUnity/`.

- **Editor:** Unity 6000.6.9f1
- **Cena inicial:** `Assets/Scenes/SampleScene.unity`
- **Cena principal de missão:** `Assets/Nicholas/Cenas/MissaoNicholas.unity`
- **Pacotes principais:** URP (`17.6.0`), Input System (`1.20.1`), uGUI (`2.6.0`), TextMeshPro e Cinemachine (`6.6.0`)
- **Direção visual:** [AstroLab Art Bible](AstroLabUnity/Assets/ArtBible.md)

## Base Lunar (mapa da Lua)

Mapa da base lunar abandonada que o jogador precisa restaurar, modelado no Blender (1 unidade = 1 metro, jogador de 1,80 m).

- **Cena base:** `Assets/BaseLunar/Cenas/Lua.unity` (WASD anda, mouse olha, Shift corre, Espaço pula com gravidade lunar)
- **Modelo:** `Assets/BaseLunar/Modelos/BaseLunar.fbx`, com texturas em `Texturas/` e materiais URP em `Materiais/`
- **Objetos de gameplay:** peças avariadas começam com `REPAIR_` e versões restauradas (`BaseConsertada`) com `OK_`. Marcadores são `SPAWN_Player`, `WP_*` (exterior) e `INT_*` (interior)
- **Regras do time:**
  - A geometria do mapa só muda no Blender, reexportando por cima do mesmo `BaseLunar.fbx`.
  - Não renomear no Blender objetos já vinculados a scripts no Unity.
  - Trabalhar em cenas separadas para evitar conflitos de merge.

---

## Resumo das Mudanças Implementadas (`pedro-h`)

### 1. Regras da Missão, Economia e Persistência (`Assets/Nicholas/Core/` & `Tests/`)
- **Rebalanceamento (`MissionRuleSet.cs` & `MissionEngine.cs`):**
  - `StartingSpareParts` ajustado para `12` (soma exata dos 5 reparos necessários).
  - Constantes explícitas de drenagem em EVA: `SuitOxygenDrainPerSecond = 0.55f`, `SuitBatteryDrainPerSecond = 0.50f`.
  - Custos atômicos em EVA: reparo externo (`-18` bateria / `-12` O₂) e registro de artefato (`-10` bateria / `-8` O₂); se o traje não tiver saldo suficiente, nada é consumido e uma mensagem clara explica o motivo.
- **Consulta e Checklist de Vitória (`MissionTypes.cs`, `MissionEngine.cs`):**
  - Adicionado `VictoryCriterion` e método puro `GetVictoryChecklist()` (fonte única usada por `ResolveMission()` e pelo painel F2) com os 7 critérios da missão (`[OK]` / `[PENDENTE]`).
  - Adicionado `CanRepair(EquipmentId)` para validar se o reparo é possível antes do clique do jogador e devolver motivos curtos na UI.
- **Save/Load completo em EVA (`MissionSnapshot.cs`, `MissionController.cs`):**
  - Persistência de posição e rotação Y do jogador (`HasPlayerPose`, `PlayerPosX/Y/Z`, `PlayerRotY`) preservando compatibilidade com saves antigos (`SchemaVersion == 1`).
  - Permitido salvar e carregar mesmo durante EVA (`IsOutside == true`), com teleporte seguro respeitando `CharacterController`.
- **Pausa automática para leitura:** consumo do traje e cronômetro da EVA não avançam enquanto o painel/modal (`MissionOverlay.IsModalOpen`) ou ficha do capacete (`HudCapacete.FichaAberta`) estiver aberta.
- **Testes EditMode (`MissionEngineTests.cs`):** `15/15` testes unitários do núcleo cobrindo economia, atomicidade, checklist de vitória, `CanRepair` e snapshot em EVA.

### 2. Integração Visual 3D, Abrigo e Réplicas Apollo 15 (`Assets/Nicholas/Runtime/` & `Editor/`)
- **Sincronização com `BaseConsertada` (`MissionController.cs`):**
  - Mapeamento explícito de IDs reais de `PecasDaBase` por equipamento em `VisualPiecesMap` (incluindo os 23 painéis `Painel_*`, inversores `Inversor_1..3`, `Gerador_O2`, `Cano_Tanque_O2`, `Reciclador_H2O`, `Estufa_*`, `Baterias`, `Antena_Prato`, `Console_Controle`) + limpeza de `Detrito_*` ao reparar o abrigo.
  - `EnsureOkTargets()` garante `Collider`/`MeshCollider` e `MissionEquipmentTarget` nos objetos `OK_*` após o reparo para manter inspeção pós-conserto.
- **Efeito curto de conserto (`MissionRepairEffects.cs` & `MissionController.cs`):**
  - Ao reparar em gameplay (`BaseConsertada.aoConsertar`), reproduz animação não bloqueante de ~`1,5 s`: partículas/faíscas geradas por código, pulso de luz ciano, pop de escala em `OK_*` e som existente do scanner (`ScanConcluido.wav`).
  - Painéis solares disparam o efeito **uma única vez** no centro do grupo; o efeito não se repete em carga inicial, `NewGame()` ou `LoadGame()`.
- **Sinalização no mundo (`MissionTargetMarkers.cs`):**
  - Marcadores flutuantes (billboard) num raio de `12 m` com forma, cor, ícone e texto distintos por estado: triângulo âmbar (`! REPARAR`), círculo verde (`OK · VER`) e estrela ciano (`APOLLO 15` / `APOLLO · OK`), com destaque e nome completo ao mirar e agrupamento único para os painéis solares.
- **Painel do abrigo e balizas científicas (`NicholasMissionSceneBuilder.cs`, `RadiationPanelVisual.cs`):**
  - Painel de manutenção do abrigo com materiais `M_Console_Branco` e transição visual avariado/vermelho → ciano ao reparar `RadiationShelter`.
  - 5 réplicas `NICHOLAS_REPLICA_*` atualizadas para pedestais com base/haste escura (`M_Metal_Escuro`) e topo emissivo ciano (`M_Detalhes_Ciano`) distinguíveis por artefato.
  - Terminal provisório `TESTE O2` desativado por padrão (`createTutorialRepairTerminal = false`).

### 3. Guia da Eclusa, Interação Unificada, HUD e Tutorial do Turno 1
- **Eclusa e retorno em EVA (`MissionController.cs`, `MissionHelmetBridge.cs`, `HudCapacete.cs`, `MissionOverlay.cs`, `NicholasMissionSceneBuilder.cs`):**
  - Confirmado que `innerAirlock` e `outerAirlock` em `MissaoNicholas.unity` apontam para `INT_Eclusa` e `WP_Rampa_Eclusa` de `BaseLunar.fbx`; adicionado menu idempotente `AstroLab/Nicholas/Validar âncoras da eclusa`.
  - Marcador ciano visível na entrada externa da eclusa (`Nicholas_Marcador_Eclusa`) e indicação contínua no HUD (`Eclusa: X m` junto ao ícone de localização) durante EVA.
  - Pressionar `L` fora do raio (`7 m`) informa distância e direção relativa (ex.: `"A eclusa está a 23 m, à sua direita."`), mantendo a regra de distância sem retorno de emergência; dentro do raio, exibe prompt `[L]` e realiza transição da eclusa.
- **Input e Scanner (`MissionPlayerInteraction.cs`, `ScannerPrimeiraPessoa.cs`):**
  - Equipamentos: `E` inspeciona e `R` repara direto na mira exibindo custo em peças/tempos no prompt; scanner não abre equipamentos.
  - Réplicas Apollo 15: scan ajustado para `1,35 s`, acionável segurando `E` ou botão esquerdo.
- **HUD do Capacete (`HudCapacete.cs`, `HudCapacete.prefab`, `DestaqueInterativoUI.cs`, `GraficoCapacete.cs`):**
  - Adicionado efeito `DestaqueInterativoUI` e melhorias visuais em painéis/barras.
  - Nomes visíveis e completos junto às barras do capacete (`OXIGÊNIO DO TRAJE` e `BATERIA DO TRAJE`), com ajuste de wrapping/overflow do `RectTransform` para evitar corte de texto.
- **Tutorial opcional do Turno 1 (`MissionOverlay.cs` & `MissionTargetMarkers.cs`):**
  - Introdução breve opcional + 6 passos automáticos mostrados um por vez no HUD com frases de até 11 palavras: (1) olhar ícone do painel solar (com destaque R4 e seta de auxílio se obstruído), (2) abrir F2, (3) sair pela eclusa com `L`, (4) reparar painel solar com `E`/`R`, (5) voltar pela eclusa e (6) ligar o suporte de vida.
  - Atalho **`P`** (ou botão **`Pular [P]`**) oculta completamente o tutorial e destaques de guia.
- **Pegadas lunares e visual cartoon (`BaseLunar/`):**
  - Adicionado sistema de pegadas (`PegadasLunar.cs`, `PegadaLunar.shader`, `M_Pegada_Lunar.mat`, `T_Pegada_Lunar.png`) acoplado ao `JogadorFPS` e ajustes em materiais/iluminação (`EstiloCartoon.cs`, materiais `M_*`).

---

## O que Ainda Precisa Ser Feito

1. **Validação no Unity Editor (Play Mode & Build):**
   - Abrir o projeto no Unity `6000.6.9f1` (neste ambiente o Editor não estava disponível para execução) para confirmar compilação completa do `Assembly-CSharp` sem avisos/erros.
   - Testar em Play Mode na cena `MissaoNicholas.unity`: ciclo completo do tutorial Turno 1 (seguir e pular com `P`), marcadores R4 dentro do raio de `12 m`, animação/som (`ScanConcluido.wav`) ao consertar painéis solares e demais sistemas, `Save/Load` em EVA (`F5`/`F9`) e leitura dos rótulos `OXIGÊNIO DO TRAJE` / `BATERIA DO TRAJE` no `HudCapacete`.
   - Conferir se os 3 itens de menu em `AstroLab/Nicholas/` rodam sem warnings na cena aberta (incluindo `Validar âncoras da eclusa`).
2. **Itens Pendentes da Especificação / Auditoria:**
   - **Tarefa 6 do plano (`MissionReport.cs`):** revisar e estruturar o relatório final detalhado em linguagem acessível (para crianças de ~7 anos), caso ainda haja textos técnicos densos no relatório/fichas Apollo 15 ou telas do overlay.
   - **Áudio da eclusa e feedback sonoro adicional:** adicionar clipe específico para pressurização/despressurização da eclusa (`ChangeEva`), se o time produzir/escolher um asset de áudio.
   - **Potência da base no HUD uGUI:** `HudCapacete` exibe `OXIGÊNIO DO TRAJE`, `BATERIA DO TRAJE`, distância da eclusa e progresso geral; caso desejem a capacidade elétrica da base (`PowerUsed/PowerCapacity`) visível fora do painel F2 na uGUI principal, falta adicionar esse elemento no prefab `HudCapacete.prefab`.
   - **Ajuste de linguagem infantil e fontes offline:** revisar eventuais textos técnicos restantes nas descrições históricas/relatórios para leitura aos 7 anos e validar que todas as fontes TMP usadas funcionam 100% offline em build final.
