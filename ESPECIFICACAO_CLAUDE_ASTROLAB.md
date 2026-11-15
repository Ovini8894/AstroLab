# Especificação Técnica e Plano de Ação — AstroLab (Guia para Implementação)

Este documento descreve em detalhes a arquitetura atual do repositório **AstroLab** (`AstroLabUnity/`), o mapeamento completo de arquivos e assemblies, e o plano técnico passo a passo para alinhar 100% do jogo às metas do documento de concepção (**"BASE LUNAR — Missão de reativação"**, *TesseractEXE*, slides 01 a 11).

---

## 1. Visão Geral da Arquitetura e Organização de Arquivos

O projeto utiliza **Unity 6000.6.9f1 (URP)**, **Input System** (`UnityEngine.InputSystem`) e **TextMeshPro / uGUI**. O código está dividido em quatro frentes principais dentro de `AstroLabUnity/Assets/`:

```text
AstroLabUnity/Assets/
├── BaseLunar/                         # Cenário 3D da base quebrada e controle FPS base
│   ├── Cenas/Lua.unity                # Cena base do modelador (NÃO sobrescrever)
│   ├── Modelos/BaseLunar.fbx          # Malha 3D principal (objetos REPAIR_*, Detrito_*, Porta_*, INT_*, WP_*)
│   ├── Materiais/ e Texturas/         # Materiais URP e texturas com wrap repeat corrigido
│   └── Scripts/
│       ├── JogadorFPS.cs              # Controle FPS (WASD, Shift, Espaço, gravidade lunar 1.62 m/s²)
│       ├── PegadasLunar.cs            # Sistema de pegadas no regolito (máx. 10 passos visíveis)
│       ├── IluminacaoCartoon.cs       # Setup visual da cena
│       └── PaletaLua.cs               # Cores e constantes visuais
│
├── BaseConsertada/                    # Sistema de troca visual 3D (Quebrado -> Consertado)
│   ├── Modelos/BaseConsertada.fbx     # Malhas intactas com prefixo OK_*
│   ├── Prefabs/BaseConsertada.prefab  # Prefab que sobrepõe a BaseLunar e gerencia os objetos OK_*
│   └── Scripts/
│       ├── BaseConsertada.cs          # API: Consertar(id), ConsertarCategoria(cat), Quebrar(id), LimparDetritos(bool)
│       └── PecasDaBase.cs             # Catálogo estático das 32 peças em 7 categorias
│
├── UI/Capacete/                       # HUD holográfico em 1ª pessoa e Scanner diegético
│   ├── Prefabs/
│   │   ├── HudCapacete.prefab         # Canvas uGUI do visor (StatusTopo, Missao, Interacao, Alerta, Exploracao)
│   │   └── MaosEScanner.prefab        # Braços em 1ª pessoa e scanner portátil animado
│   └── Scripts/
│       ├── HudCapacete.cs             # Controlador principal do HUD (O2, Energia, Distância, Missão, Ficha Modal)
│       ├── GraficoCapacete.cs         # Gráficos procedurais do visor (Anel, Barras, Retículo, Molduras)
│       ├── DestaqueInterativoUI.cs    # Brilho periódico (2s, esquerda->direita) nos elementos interativos
│       ├── ScannerPrimeiraPessoa.cs   # Scanner (segurar E ou Clique Esquerdo por 1.35s; Tab guarda/equipa)
│       ├── CatalogoScanner.cs         # Descrições de fallback para objetos do cenário
│       └── AlvoExploracao.cs          # Componente de dados científicos anexado a alvos escaneáveis
│
├── Treinamentos/
│   └── CapacetePedro.unity            # Cena com BaseLunar + JogadorFPS + HudCapacete + MaosEScanner
│
└── Nicholas/                          # Motor de Missão (3 turnos), Integração NASA e Cena Integrada
    ├── Cenas/
    │   └── MissaoNicholas.unity       # CENA PRINCIPAL DO JOGO INTEGRADO (foco das alterações)
    ├── Core/                          # Assembly isolado: AstroLab.Nicholas.Core.asmdef (Puro C#, sem Unity UI)
    │   ├── MissionRuleSet.cs          # Constantes de balanceamento e catálogo dos 7 equipamentos
    │   ├── MissionTypes.cs            # Enums (EquipmentId, ArtifactId, MissionOutcome) e structs
    │   ├── MissionSnapshot.cs         # Estado serializável da partida (para Save/Load JSON)
    │   ├── MissionEngine.cs           # Máquina de estados e regras (Repair, SetPowered, StartEva, AdvanceTurn)
    │   ├── Apollo15Trail.cs           # Dados curados dos 5 artefatos históricos da Apollo 15
    │   └── MissionReport.cs           # Gerador de relatório textual de fim de partida
    ├── Integrations/
    │   └── NasaImageLibraryClient.cs  # Cliente HTTP para https://images-api.nasa.gov + cache offline JSON
    ├── Runtime/                       # Scripts MonoBehaviour (Assembly-CSharp — enxerga BaseConsertada e UI)
    │   ├── MissionController.cs       # Singleton de ponte entre MissionEngine e a cena Unity
    │   ├── MissionOverlay.cs          # Painel F2 (Controle da Missão, Energia, Inspeção, Museu Apollo 15)
    │   ├── MissionPlayerInteraction.cs# Raycast de interação (E = Inspecionar, R = Reparar, L = Eclusa)
    │   ├── MissionHelmetBridge.cs     # Sincroniza dados do MissionEngine com HudCapacete
    │   ├── MissionScannerBridge.cs    # Escuta evento aoConcluir do ScannerPrimeiraPessoa
    │   ├── MissionEquipmentTarget.cs  # Marcador anexado aos objetos REPAIR_* na cena
    │   ├── MissionHistoricalTarget.cs # Marcador anexado às 5 réplicas da Apollo 15
    │   └── OptionalTeamComponents.cs  # Helpers de reflexão
    ├── Editor/
    │   └── NicholasMissionSceneBuilder.cs # Script de geração/configuração da cena MissaoNicholas.unity
    └── Tests/EditMode/
        └── MissionEngineTests.cs      # 9 testes unitários NUnit (AstroLab.Nicholas.Core.Tests.asmdef)
```

### Detalhe Crítico sobre Assemblies (`asmdef`)
- `Assets/Nicholas/Core/AstroLab.Nicholas.Core.asmdef` compila **apenas** `Assets/Nicholas/Core/`. Ele **não** referencia `UnityEngine` nem `Assembly-CSharp`.
- `Assets/Nicholas/Runtime/`, `Assets/BaseConsertada/Scripts/` e `Assets/UI/Capacete/Scripts/` **não possuem `.asmdef` próprio** e residem todos em `Assembly-CSharp`. Portanto, `MissionController.cs`, `MissionPlayerInteraction.cs` e `MissionScannerBridge.cs` podem referenciar diretamente `BaseConsertada`, `PecasDaBase`, `HudCapacete` e `ScannerPrimeiraPessoa` sem necessidade de reflexão.

---

## 2. Lista Concisa de Tarefas (Resumo Executivo)

1. **[ALTA] Conectar `BaseConsertada` ao `MissionController`:** fazer cada reparo no `MissionEngine` trocar imediatamente os modelos 3D quebrados (`REPAIR_*` e `Detrito_*`) pelos modelos consertados (`OK_*`) na cena `MissaoNicholas.unity`.
2. **[ALTA] Resolver conflito de input (`E` / Clique Esquerdo) entre Scanner e Inspeção:** impedir que `MissionPlayerInteraction` abra a janela `F2` instantaneamente ao clicar em artefatos da Apollo 15 (exigir o scan de `1.35s` do `ScannerPrimeiraPessoa`), e impedir que o scanner inicie varredura científica ao interagir com equipamentos técnicos (`MissionEquipmentTarget`).
3. **[ALTA] Rebalancear `MissionRuleSet.cs` e atualizar `MissionEngineTests.cs`:** ajustar peças iniciais (`16 -> 12`) e consumo do traje em EVA para tornar o **Carregador de Trajes (`2` de energia)** realmente necessário, ativando na prática o dilema energético do Slide 05 (*Estufa vs. Recarga do Traje*).
4. **[MÉDIA] Exibir Checklist de Vitória no Painel `F2` e Alerta Prévio de Radiação no Turno 2:** mostrar claramente no `MissionOverlay` os 7 requisitos de `ResolveMission()` e emitir aviso de tempestade solar no início do Turno 2.
5. **[MÉDIA] Substituir os cubos brancos (`PrimitiveType.Cube`) na cena `MissaoNicholas.unity`:** vincular o `RadiationShelter` a uma malha real da base e substituir/estilizar os 5 cubos `NICHOLAS_REPLICA_*` da trilha Apollo 15.
6. **[MÉDIA] Estruturar o `MissionReport.cs` com as 4 perguntas pedagógicas do Slide 11:** responder dinamicamente *Para onde foram os recursos?*, *Qual prioridade funcionou?*, *O que faltou?* e *Como melhorar na próxima tentativa?*.
7. **[BAIXA] Polimento de Save/Load, Transição de Eclusa e Feedback de Áudio:** persistir posição do jogador e estado visual da `BaseConsertada` ao carregar (`F9`), e suavizar o teleporte da eclusa (`L`).

---

## 3. Especificação Detalhada de Cada Tarefa

### TAREFA 1 [ALTA PRIORIDADE]: Integração Visual 3D de Reparos (`BaseConsertada` <-> `MissionController`)

#### Problema Atual
- Na apresentação (Slide 06 e Slide 10), cada reparo deve alterar o estado visual do objeto 3D de quebrado para consertado.
- O projeto já possui `Assets/BaseConsertada/Prefabs/BaseConsertada.prefab` e `BaseConsertada.cs`, que controla 32 peças (`PecasDaBase.cs`).
- Porém, `MissionController.cs` **não chama `BaseConsertada`** e a cena `MissaoNicholas.unity` não instancia `BaseConsertada.prefab`. Quando o jogador repara um sistema, o texto muda para `[Operacional]`, mas a malha 3D continua quebrada.

#### Mapeamento Exato (`EquipmentId` -> `BaseConsertada`)
Em `PecasDaBase.cs`, as peças estão agrupadas em 7 categorias (`Energia`, `Suporte de Vida`, `Água`, `Estufa`, `Trajes`, `Comunicação`, `Habitação`). O mapeamento exato para `EquipmentId` é:

| `EquipmentId` (`MissionTypes.cs`) | Estado Inicial | Ação em `BaseConsertada` quando reparado |
| :--- | :--- | :--- |
| `EquipmentId.SolarPanels` | Quebrado | `baseConsertada.ConsertarCategoria("Energia")` (conserta `Painel_1..4`, `Cabos_Solares`, `Caixa_Juncao`) |
| `EquipmentId.LifeSupport` | Quebrado | `baseConsertada.ConsertarCategoria("Suporte de Vida")` (conserta `Gerador_O2`, `Tanque_O2`, `Filtro_CO2`, `Ventilacao`) |
| `EquipmentId.WaterRecycler` | Quebrado | `baseConsertada.ConsertarCategoria("Água")` (conserta `Reciclador_H2O`, `Tanque_Agua`, `Cano_Agua`) |
| `EquipmentId.Greenhouse` | Quebrado | `baseConsertada.ConsertarCategoria("Estufa")` (conserta `Estufa_Painel`, `Luz_Grow_1`, `Luz_Grow_2`, `Hidroponia_1..3`, `Irrigacao`) |
| `EquipmentId.RadiationShelter` | Quebrado | `baseConsertada.ConsertarCategoria("Habitação")` + `baseConsertada.LimparDetritos(true)` |
| `EquipmentId.SuitCharger` | Já começa reparado (`StartsRepaired = true`) | `baseConsertada.ConsertarCategoria("Trajes")` logo na inicialização |
| `EquipmentId.Communications` | Já começa reparado (`StartsRepaired = true`) | `baseConsertada.ConsertarCategoria("Comunicação")` logo na inicialização |

#### O que implementar passo a passo
1. **Na cena `MissaoNicholas.unity` e em `NicholasMissionSceneBuilder.cs`:**
   - Instanciar o prefab `Assets/BaseConsertada/Prefabs/BaseConsertada.prefab` na raiz da cena (ou localizá-lo em runtime via `FindFirstObjectByType<BaseConsertada>()`).
2. **Em `Assets/Nicholas/Runtime/MissionController.cs`:**
   - Adicionar `[SerializeField] private BaseConsertada baseConsertada;` e resolvê-lo no `Awake()` com `if (!baseConsertada) baseConsertada = FindFirstObjectByType<BaseConsertada>();`.
   - Criar um método privado `SyncVisualBase()` que percorre todos os `EquipmentId` e:
     - Se `Engine.IsRepaired(id)` for `true`, chama `ConsertarCategoria` correspondente (e `LimparDetritos(true)` se `RadiationShelter` estiver reparado).
     - Se for `false` (ex.: após `NewGame()` ou `LoadGame()`), chama `Quebrar(p.id)` para as peças daquela categoria e `LimparDetritos(false)`.
   - Chamar `SyncVisualBase()` no final do `Start()`, dentro de `Repair(EquipmentId id)` quando `result.Success == true`, em `NewGame()` e em `LoadGame()`.
3. **Atenção aos Colliders de Interação (`MissionEquipmentTarget`):**
   - Quando `BaseConsertada.Aplicar(p, true)` roda, os objetos `REPAIR_*` originais são desativados (`SetActive(false)`) e os objetos `OK_*` são ativados (`SetActive(true)`).
   - Para que o jogador continue podendo inspecionar (`E`) o equipamento **depois** de consertado (para ver seu status ou ligar/desligar energia), ao ativar os objetos `OK_*` ou no `Start()` do `MissionController`, garanta que os objetos `OK_*` equivalentes (`OK_Painel_1..4`, `OK_Gerador_O2`, `OK_Reciclador_H2O`, `OK_Estufa_Painel`, `OK_Baterias`, `OK_Antena_Prato`) também recebam `Collider` e `MissionEquipmentTarget` configurados com o mesmo `EquipmentId`!

---

### TAREFA 2 [ALTA PRIORIDADE]: Unificação do Fluxo de Input (`E` / Clique Esquerdo / Scanner)

#### Problema Atual
- Em `MissionPlayerInteraction.cs` (linhas 61–80), pressionar `E` ou o botão esquerdo do mouse (`leftButton.wasPressedThisFrame`) abre imediatamente o modal `F2` tanto para `MissionEquipmentTarget` quanto para `MissionHistoricalTarget`.
- Simultaneamente, `ScannerPrimeiraPessoa.cs` (linhas 76–81) usa `eKey.isPressed || leftButton.isPressed` para carregar a barra de `ANALISANDO XX%` durante `1.35s` (`tempoAnalise`) e ao final abrir a ficha do capacete + disparar `aoConcluir`.
- Resultado: ao tentar escanear um artefato da Apollo 15, o painel `F2` abre no primeiro frame (bloqueando o cursor e interrompendo o scanner), ou ambos os modais (`HudCapacete.FichaAberta` e `MissionOverlay.IsVisible`) competem na tela.

#### O que implementar passo a passo
1. **Em `Assets/Nicholas/Runtime/MissionPlayerInteraction.cs`:**
   - **Remover** `mouse.leftButton.wasPressedThisFrame` da variável `interact` para equipamentos técnicos — usar **apenas a tecla `E`** para inspecionar equipamentos (`MissionEquipmentTarget`) e **`R`** para reparo rápido.
   - Quando o raycast atingir um **`MissionHistoricalTarget`** (réplica da Apollo 15):
     - Atualizar `CurrentPrompt` para: `"Segure [E] ou Clique Esquerdo com o Scanner: " + historyTarget.Label`.
     - **Não** chamar `overlay.OpenArtifact(historyTarget.Artifact)` no clique instantâneo de `MissionPlayerInteraction`. Deixar que o `ScannerPrimeiraPessoa` conclua a leitura de `1.35s`.
2. **Em `Assets/UI/Capacete/Scripts/ScannerPrimeiraPessoa.cs`:**
   - No método `Update()`, antes de iniciar a contagem de `progresso` (linha 84), verificar se o `hit.collider` possui `MissionEquipmentTarget` no próprio objeto ou nos pais (`hit.collider.GetComponentInParent<AstroLab.Nicholas.MissionEquipmentTarget>() != null`). Se for um equipamento de manutenção da base e **não** tiver `MissionHistoricalTarget`, o scanner não deve abrir a ficha genérica no fim do scan, evitando conflito com a inspeção técnica da missão.
3. **Em `Assets/Nicholas/Runtime/MissionScannerBridge.cs` e `HudCapacete.cs`:**
   - Quando `ScannerPrimeiraPessoa` conclui o scan de um `MissionHistoricalTarget`, `HudCapacete.MostrarFicha` abre a ficha do visor **e** `MissionScannerBridge.Scanned()` chama `overlay.SetPendingArtifact(target.Artifact)`.
   - Para evitar duas janelas sobrepostas ao mesmo tempo: em `ScannerPrimeiraPessoa.Concluir()`, se o objeto escaneado tiver `MissionHistoricalTarget` e `MissionScannerBridge` estiver ativo na cena, deixe apenas o `MissionOverlay` (`SetPendingArtifact`) abrir a janela completa da Apollo 15 (que já exibe história, fonte oficial da NASA, miniatura da API e o quiz da Pena/Martelo), **não** abrindo o modal duplicado do `HudCapacete` por baixo.

---

### TAREFA 3 [ALTA PRIORIDADE]: Rebalanceamento de Recursos e EVA (`MissionRuleSet.cs` e Testes)

#### Problema Atual
1. **Peças de reposição sobrando:** Os 5 reparos da partida custam `2 + 3 + 2 + 2 + 3 = 12 peças`, mas `StartingSpareParts = 16`. O jogador nunca precisa priorizar peças (contradizendo o Slide 04: *"O abrigo precisa estar pronto e compete por peças e tempo"*).
2. **Carregador de Trajes inútil na prática:** O dreno de EVA é `0.18%/s` (O2) e `0.14%/s` (Bateria), dando mais de 9 minutos de autonomia em tempo real. Como a usina solar e os artefatos ficam a 15–35m da eclusa, o jogador faz todas as EVAs do jogo gastando menos de 15% do traje e nunca precisa ligar o `SuitCharger` (`2` de energia), anulando o dilema do Slide 05 (*Estufa 3 pts vs. Recarga do traje 2 pts*).

#### O que implementar passo a passo
1. **Em `Assets/Nicholas/Core/MissionRuleSet.cs`:**
   - Alterar `StartingSpareParts` de `16` para **`12`** (ou `11` caso queira que o jogador escolha entre consertar o `WaterRecycler` ou economizar água desligando a estufa em 1 turno; com `12`, o orçamento é exato para todos os 5 reparos sem sobras).
   - Ajustar o consumo de EVA para criar pressão real de retorno e recarga:
     - Aumentar `SuitOxygenDrainPerSecond` de `0.18f` para **`0.55f`** (~3 minutos de EVA contínua até 0%, entrando na faixa de alerta `< 35%` após ~115 segundos).
     - Aumentar `SuitBatteryDrainPerSecond` de `0.14f` para **`0.50f`**.
   - **Custo operacional em EVA (`MissionEngine.cs`):**
     - Em `MissionEngine.Repair(EquipmentId id)`: se `rule.RequiresEva` for `true` (ex.: `SolarPanels`), deduzir também **`18f` de `SuitBattery`** e **`12f` de `SuitOxygen`** pelo esforço físico/ferramental da manutenção externa (verificando antes se há traje suficiente).
     - Em `MissionEngine.RegisterArtifact(...)`: deduzir **`10f` de `SuitBattery`** e **`8f` de `SuitOxygen`** por cada registro científico em campo.
     - Com isso, consertar os painéis solares + escanear os 5 artefatos + caminhar consumirá mais de `90%–110%` de bateria/O2 ao longo da partida, obrigando o jogador a planejar 2 EVAs e ligar o **Carregador de Trajes (`2` de energia)** em pelo menos um momento!
2. **Em `Assets/Nicholas/Tests/EditMode/MissionEngineTests.cs`:**
   - Atualizar os testes unitários afetados:
     - Em `ReparoEAtomicoSemPecasOuTempo()`: atualizar `Assert.AreEqual(16, ...)` para `12` e `Assert.AreEqual(14, ...)` para `10`.
     - Em `EvaConsomeTrajePorTempoENaoPorFrame()`: atualizar a fórmula esperada para usar `MissionRuleSet.SuitOxygenDrainPerSecond * 0.2f` em vez do literal `0.18f * 0.2f`.
     - Garantir que `CaminhoCompletoVenceAoFimDoTerceiroTurno()` continue passando 100%.

---

### TAREFA 4 [MÉDIA PRIORIDADE]: Checklist Explícito de Vitória e Aviso Prévio da Tempestade Solar

#### Problema Atual
- As condições exatas verificadas em `MissionEngine.ResolveMission()` (linhas 233–250) só aparecem quando o jogador já perdeu ou venceu no fim do Turno 3.
- O evento de radiação solar ocorre ao avançar do Turno 2 para o Turno 3 (`if (current == 2)` em `MissionEngine.AdvanceTurn()`), sem aviso explícito no início do Turno 2.

#### O que implementar passo a passo
1. **Em `Assets/Nicholas/Core/MissionEngine.cs` (`AdvanceTurn`):**
   - Quando o turno avança de `1` para `2` (`state.Turn == 2`), registrar imediatamente um alerta no histórico e no feedback:
     ```csharp
     if (state.Turn == 2 && !IsRepaired(EquipmentId.RadiationShelter))
     {
         Record(MissionEventType.Warning, "ALERTA SOLAR: Tempestade de radiação prevista para o final do Turno 2. Repare o Abrigo de Radiação antes de encerrar este turno!");
     }
     ```
2. **Em `Assets/Nicholas/Runtime/MissionOverlay.cs` (`DrawOverview`):**
   - Adicionar um bloco visual **"METAS PARA A PRÓXIMA TRIPULAÇÃO (FIM DO TURNO 3)"** exibindo em tempo real o status (`[OK]` em verde ou `[PENDENTE]` em amarelo/vermelho) dos 7 critérios de `ResolveMission()`:
     1. Painéis Solares reparados (`engine.IsRepaired(EquipmentId.SolarPanels)`)
     2. Suporte de Vida reparado e energizado (`engine.IsPowered(EquipmentId.LifeSupport)`)
     3. Estufa Hidropônica reparada e energizada (`engine.IsPowered(EquipmentId.Greenhouse)`)
     4. Abrigo de Radiação reparado (`engine.IsRepaired(EquipmentId.RadiationShelter)`)
     5. Reserva de Ar >= 30% (`state.HabitatAir >= MissionRuleSet.MinimumEndHabitatAir`)
     6. Estoque de Água >= 1 (`state.Water >= MissionRuleSet.MinimumEndWater`)
     7. Estoque de Alimentos >= 2 (`state.Food >= MissionRuleSet.MinimumEndFood`)

---

### TAREFA 5 [MÉDIA PRIORIDADE]: Substituição dos Placeholders Geométricos (`Cube`) na Cena

#### Problema Atual
- Em `NicholasMissionSceneBuilder.cs`:
  - `AddRadiationTarget()` (linhas 196–206) cria um cubo sem material (`sharedMaterial = null`) chamado `Nicholas_Painel_Manutencao_Abrigo`.
  - `AddScienceMarkers()` (linhas 208–240) cria 5 cubos padrão (`NICHOLAS_REPLICA_Hammer`, `Feather`, `Rover`, `Retroreflector`, `FalconDescentStage`).

#### O que implementar passo a passo
1. **Abrigo de Radiação (`RadiationShelter`):**
   - Na `BaseLunar`, procurar primeiro os objetos reais do módulo de habitação (`REPAIR_Beliche_1`, `REPAIR_Beliche_2`, `REPAIR_Mesa` ou `REPAIR_Armario_1`) e anexar `MissionEquipmentTarget` (`EquipmentId.RadiationShelter`) a eles, além de estilizar o `Nicholas_Painel_Manutencao_Abrigo` com o material `M_Console_Branco.mat` / `M_Detalhes_Ciano.mat` (`Assets/BaseLunar/Materiais/`).
2. **Marcadores da Trilha Apollo 15 (`NICHOLAS_REPLICA_*`):**
   - Criar uma hierarquia visual de pedestal/baliza científica (base cilíndrica metálica + haste + topo emissivo ciano usando `M_Detalhes_Ciano.mat` e `M_Metal_Escuro.mat` de `Assets/BaseLunar/Materiais/`) ou modelos distintos para cada um dos 5 artefatos na cena `MissaoNicholas.unity` e no método `AddScienceMarkers()` de `NicholasMissionSceneBuilder.cs`.

---

### TAREFA 6 [MÉDIA PRIORIDADE]: Relatório Final Estruturado conforme o Slide 11 (`MissionReport.cs`)

#### Problema Atual
- O Slide 11 especifica que o Relatório da Missão deve responder a 4 perguntas:
  1. *Para onde foram os recursos?*
  2. *Qual prioridade funcionou?*
  3. *O que faltou?*
  4. *Como melhorar na próxima tentativa?*
- Atualmente, `MissionReport.Build()` (32 linhas) apenas lista os valores brutos e o log de eventos.

#### O que implementar passo a passo
- Atualizar `Assets/Nicholas/Core/MissionReport.cs` para incluir as 4 seções explícitas calculadas a partir de `engine.State`:
  - **1. PARA ONDE FORAM OS RECURSOS:** detalhar quantas peças foram usadas (`12 - state.SpareParts` de `12`), quantos sistemas foram reparados, o uso de potência final (`PowerUsed/PowerCapacity`) e o balanço final de Ar (`state.HabitatAir%`), Água (`state.Water`) e Alimentos (`state.Food`).
  - **2. QUAL PRIORIDADE FUNCIONOU:** destacar os acertos da partida (ex.: *"Geração solar restabelecida (+5 de potência)"*, *"Suporte de vida manteve a atmosfera segura"*, *"Abrigo protegeu a base durante a tempestade solar do Turno 2"*, *"Estufa garantiu a produção de alimentos"*).
  - **3. O QUE FALTOU:** listar os itens pendentes (ou informar *"Todos os requisitos vitais da base foram cumpridos."* + quantos artefatos da Apollo 15 faltaram catalogar: `5 - state.NextArtifactIndex`).
  - **4. COMO MELHORAR NA PRÓXIMA TENTATIVA:** gerar recomendações acionáveis específicas baseadas no estado final (ex.: se faltou água, recomendar consertar o *Reciclador de Água* até o Turno 2; se faltou comida, recomendar energizar a *Estufa* a partir do Turno 1 ou 2; se perdeu ar no Turno 2, recomendar priorizar o *Abrigo de Radiação* antes de encerrar o Turno 2; se venceu sem completar a trilha Apollo 15, sugerir usar o *Carregador de Trajes* no Turno 3 para concluir a exploração científica).

---

### TAREFA 7 [BAIXA PRIORIDADE]: Persistência Completa no Save/Load e Transição de Eclusa

1. **Save/Load completo (`MissionSnapshot.cs` e `MissionController.cs`):**
   - Adicionar campos serializáveis `PlayerPosX, PlayerPosY, PlayerPosZ, PlayerRotY` em `MissionSnapshot.cs`.
   - Em `MissionController.SaveGame()` e `LoadGame()`, persistir e restaurar a posição/rotação do jogador (permitindo inclusive salvar durante EVA) e chamar `SyncVisualBase()` ao carregar para que os modelos `OK_*` da `BaseConsertada` reflitam os equipamentos já reparados no save.
2. **Ciclo de Eclusa (`MissionController.ChangeEva`):**
   - Ao alternar entre interior e exterior (`L`), exibir mensagem clara de despressurização/pressurização no HUD (`"Ciclo da eclusa concluído — Modo EVA ativo"` / `"Eclusa pressurizada — Interior do Habitat"`).

---

## 4. Checklist de Validação Pós-Implementação

- [ ] Todos os testes em `Assets/Nicholas/Tests/EditMode/MissionEngineTests.cs` compilam e passam sem erros.
- [ ] Na cena `Assets/Nicholas/Cenas/MissaoNicholas.unity`, consertar qualquer equipamento (`R` ou botão no painel `F2`) troca imediatamente o modelo 3D quebrado (`REPAIR_*`) pelo modelo intacto (`OK_*`).
- [ ] Ainda é possível mirar e inspecionar (`E`) um equipamento após ele ter sido consertado (modelo `OK_*` possui `Collider` e `MissionEquipmentTarget`).
- [ ] Apontar para uma réplica da Apollo 15 e segurar `E` ou Clique Esquerdo carrega o scanner portátil (`1.35s`) e abre apenas o modal científico unificado, sem abrir modais duplicados no primeiro frame.
- [ ] O painel `F2` exibe o checklist claro com os 7 critérios de vitória do Turno 3.
- [ ] O início do Turno 2 emite alerta claro sobre a tempestade solar caso o Abrigo de Radiação ainda esteja quebrado.
- [ ] O relatório final (`MissionReport.Build`) apresenta as 4 seções exigidas no Slide 11.
