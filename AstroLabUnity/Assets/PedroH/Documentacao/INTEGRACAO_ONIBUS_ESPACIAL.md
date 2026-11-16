# Integração do Modelo 3D do Ônibus Espacial (`OnibusEspacial.fbx`)

Este guia documenta a integração do modelo 3D externo do Space Shuttle (`Assets/CentroTreinamento/Modelos/OnibusEspacial.fbx`, originado na branch `vinicius`) ao sistema de viagem espacial da cena `Assets/Scenes/NaveEspacial.unity`, detalhando compatibilidade de materiais, encapsulamento no prefab `Assets/PedroH/Prefabs/OnibusEspacialNave.prefab`, atualização contínua a partir da branch `vinicius` e substituição segura por outros modelos futuros.

> **Nota de escopo:** A etapa de frenagem orbital (`MinigameFreioOrbitalLunar`) foi removida da cena `NaveEspacial.unity` e não faz parte deste fluxo (fica sob responsabilidade de outra cena/integrante).

---

## 1. Origem na Branch `vinicius` e Compatibilidade dos 26 Materiais/Texturas

### 1.1 Arquivos trazidos da branch `vinicius`
* **Modelo e metadados de importação (commit `7df2af4` em `vinicius`):**
  * `Assets/CentroTreinamento/Modelos/OnibusEspacial.fbx`
  * `Assets/CentroTreinamento/Modelos/OnibusEspacial.fbx.meta` (`guid: 614bef39780f3af439767de6f4943616`)
* **Base compartilhada de materiais, texturas e importador (merge `3cc04b1`):**
  * `Assets/CentroTreinamento/Materiais/*.mat` (catálogo com 42 materiais `CT_*` e 22 materiais `OR_*`)
  * `Assets/CentroTreinamento/Texturas/*.png` (incluindo `T_OR_MFD.png` e `T_Logo_NASA.png`)
  * `Assets/CentroTreinamento/Editor/CentroTreinamentoImport.cs` e `EstiloCentro.cs`

### 1.2 Por que os 26 materiais e texturas em `Assets/CentroTreinamento/` já eram compatíveis
Ao trazer `OnibusEspacial.fbx` e `OnibusEspacial.fbx.meta` da branch `vinicius`, nenhuma criação ou conversão manual de material foi necessária porque:
1. **Tabela `externalObjects` intacta no `.meta`:** O `ModelImporter` de `OnibusEspacial.fbx.meta` (`materialImportMode: 1`, `materialName: 0`, `materialSearch: 1`, `materialLocation: 1`) já mapeia por nome e **GUID** todos os **26 materiais** usados pela malha para os respectivos `.mat` em `Assets/CentroTreinamento/Materiais/`:
   * **9 materiais `CT_*`:** `CT_Azul_Escuro`, `CT_Branco`, `CT_Console`, `CT_Logo_NASA`, `CT_Luz`, `CT_Luz_Ambar`, `CT_Luz_Verde`, `CT_Metal`, `CT_Vermelho_NASA`.
   * **17 materiais `OR_*`:** `OR_Assento`, `OR_Branco`, `OR_Cinto`, `OR_Cinza_RCC`, `OR_Interior`, `OR_Laranja_ET`, `OR_Laranja_ET_Escuro`, `OR_Motor`, `OR_Motor_Interno`, `OR_Painel`, `OR_Painel_Claro`, `OR_Piso_Cabine`, `OR_Preto`, `OR_SRB`, `OR_SRB_Escuro`, `OR_Tela_MFD`, `OR_Vidro_Cabine`.
2. **GUIDs idênticos pós-merge (`3cc04b1`):** Os arquivos `.mat` e `.png` em `Assets/CentroTreinamento/` já haviam sido integrados à branch `pedro-h` no merge `3cc04b1` exatamente com os mesmos GUIDs esperados por `OnibusEspacial.fbx.meta`, sem divergências em relação à `vinicius`.
3. **Mesmo pipeline de shader e importação:** Os 26 materiais utilizam o shader `AstroLab/Toon` configurado por `EstiloCentro.cs` (com transparência em `OR_Vidro_Cabine` e `CT_Logo_NASA` e emissão/textura em `OR_Tela_MFD` com `T_OR_MFD.png`), e `CentroTreinamentoImport.cs` (`OnPreprocessModel`) padroniza qualquer `.fbx` dentro de `Assets/CentroTreinamento/Modelos/` (`globalScale = 1`, `importNormals = Import`, `importTangents = None`, sem câmeras, luzes ou animações embutidas).

---

## 2. Como o `OnibusEspacial.fbx` se Conecta à Lógica da Nave

Em vez de colocar o `.fbx` diretamente na cena ou alterar a geometria original usada pelo Centro de Treinamento, o modelo é encapsulado em **`Assets/PedroH/Prefabs/OnibusEspacialNave.prefab`**, acoplado sob `Sistema_Viagem_Lunar/PontoEncaixeModelo` e referenciado em `EncaixeModeloFoguete.modeloFogueteAtual` na cena `Assets/Scenes/NaveEspacial.unity`.

### 2.1 Cadeia Cena → Encaixe → Prefab Wrapper → FBX

```text
Sistema_Viagem_Lunar (ViagemLunarController + EncaixeModeloFoguete + TrajetoriaViagemLunar + ...)
└── PontoEncaixeModelo (EncaixeModeloFoguete.pontoEncaixeModelo)
    └── OnibusEspacialNave (Assets/PedroH/Prefabs/OnibusEspacialNave.prefab · FogueteModeloLogica)
        ├── PivotVisual (FogueteModeloLogica.pivotVisualFoguete)
        │   ├── OnibusEspacial (instância aninhada de Assets/CentroTreinamento/Modelos/OnibusEspacial.fbx)
        │   │       [pos: (0, -1.875, -6.0), rot: (-90, 0, 180), scale: (0.25, 0.25, 0.25), *_L1/*_L2 desativados]
        │   ├── PontoCabine (FogueteModeloLogica.pontoCabine · pos: (-0.155, 0.608, 3.250))
        │   │   ├── Tripulante_Astronauta (Biped_Humanoid.fbx · pos: (0, -0.30, 0.01), scale: (0.25, 0.25, 0.25))
        │   │   └── LuzCabine (Point Light · pos: (0.155, 0.22, 0.15), intensity: 1.4, range: 2.6)
        │   ├── PontoExaustaoPrincipal (FogueteModeloLogica.pontoExaustaoPrincipal · pos: (0, 0.15, -5.45))
        │   │   └── LuzMotorPrincipal (pos: (0, 0, -0.65), range: 22, intensidadeMaximaMotorPrincipal: 6.5)
        │   ├── RCS_Esquerda (pos: (-2.45, 0.20, 1.10) · intensidadeMaximaLuz: 4.5)
        │   ├── RCS_Direita  (pos: (2.45, 0.20, 1.10)  · intensidadeMaximaLuz: 4.5)
        │   ├── RCS_Cima     (pos: (0, 1.35, 3.20)     · intensidadeMaximaLuz: 4.5)
        │   └── RCS_Baixo    (pos: (0, -0.95, 3.20)    · intensidadeMaximaLuz: 4.5)
        └── PontoCameraExterna (FogueteModeloLogica.pontoCameraExterna · pos: (0, 4.15, -15.20), rot: (12.55, 0, 0))
```

### 2.2 Ajustes de Escala, Orientação, Pivô e LODs no Wrapper
* **Compensação de eixos e escala em `PivotVisual/OnibusEspacial`:** No Blender (`Assets/CentroTreinamento/Blender~/gerar_plataforma_lancamento.py`), o conjunto do Ônibus Espacial é gerado na vertical de lançamento (`-Z forward / Y up`) em escala métrica real (~47 m de altura do tanque externo) com origem na base do tanque sobre a plataforma móvel. Ao aninhar `OnibusEspacial.fbx` dentro de `PivotVisual`:
  * A instância recebe **`localPosition = (0, -1.875, -6.0)`**, **`localRotation = (-90, 0, 180)`** e **`localScale = (0.25, 0.25, 0.25)`** para centralizar fuselagem e asas no referencial da viagem (**`+Z` para frente**, **`+Y` para cima**), compatível com `Quaternion.LookRotation` de `TrajetoriaViagemLunar` e com o enquadramento de `CameraViagemEspacial` na `Main Camera` (`posicaoLocalBase = (0, 4.15, -15.20)`, `alvoLocalBase = (0, 0.65, 0.52)`, `fovBase = 60`).
  * **Desativação e controle dos nós `_L1` e `_L2`:** O FBX exporta malhas de LOD nomeadas com sufixo `_L1` (40%) e `_L2` (10%) (`Onibus_Orbitador_L1`, `Onibus_Orbitador_L2`, `Onibus_TanqueExterno_L1`, `Onibus_TanqueExterno_L2`, `Onibus_Boosters_L1`, `Onibus_Boosters_L2`), sem `LODGroup` automático. Na instância de `OnibusEspacialNave.prefab`, todos os nós `*_L1` e `*_L2` ficam **desativados**, e os irmãos LOD dos estágios descartáveis são listados em `EstagioFogueteConfig.objetosAdicionaisEstagio`. Em `FogueteModeloLogica`, `CapturarEstadosIniciaisSeNecessario()` e `RestaurarEstadosIniciais()` garantem que esses nós permaneçam desativados no reinício em Terra.
* **Inclinação, vibração e sincronização de encaixe:** `EncaixeModeloFoguete.AplicarEstadoCompletoDaNave(...)` movimenta `Sistema_Viagem_Lunar` ao longo da curva Bézier e aplica inclinações suaves (`16°` rolagem lateral, `12°` arfagem, `8°` guinada) e vibração de subida em `PontoEncaixeModelo` (ou `PivotVisual`), mantendo a `Main Camera` (`CameraViagemEspacial`) estável no referencial da nave. Além disso, `EncaixeModeloFoguete.SincronizarFogueteModeloLogica()` (público) e `DesconectarModeloAtual()` limpam referências antigas/quebradas e mantêm sincronizados os pontos opcionais e `luzesPropulsoresOpcionais`.

### 2.3 Sockets de Cabine, Exaustão, RCS e Câmera
* **`PontoCabine` `(-0.155, 0.608, 3.250)` + `Tripulante_Astronauta` + `LuzCabine`:** Instância de `Modelo_Astronauta_3D` (`Biped_Humanoid.fbx`) posicionada no assento do comandante dentro da cabine (`Onibus_Interior`) em `localPosition = (0, -0.30, 0.01)` e `localScale = (0.25, 0.25, 0.25)`, iluminada por `LuzCabine` (`localPosition = (0.155, 0.22, 0.15)`, `intensity = 1.4`, `range = 2.6`). Sincronizado com `EncaixeModeloFoguete.pontoCabineOpcional`.
* **`PontoExaustaoPrincipal` `(0, 0.15, -5.45)` + `LuzMotorPrincipal`:** Posicionados junto aos 3 motores principais (SSME) na popa do Orbitador (`LuzMotorPrincipal` em `localPosition = (0, 0, -0.65)`, `range = 22`), ligados a `FogueteModeloLogica.luzesMotorPrincipal` (`intensidadeMaximaMotorPrincipal = 6.5`) e `EncaixeModeloFoguete.luzesPropulsoresOpcionais`.
* **`RCS_Esquerda (-2.45, 0.20, 1.10)`, `RCS_Direita (2.45, 0.20, 1.10)`, `RCS_Cima (0, 1.35, 3.20)`, `RCS_Baixo (0, -0.95, 3.20)`:** Ligados a `FogueteModeloLogica.bocaisRcs` (`intensidadeMaximaLuz = 4.5`). O script aciona os canais opostos por ação e reação (ex.: comando para a direita `x > 0` ativa `luzEsquerda`; comando para cima `y > 0` ativa `luzBaixo`).
* **`PontoCameraExterna` `(0, 4.15, -15.20)` / `rot (12.55, 0, 0)`:** Transform auxiliar alinhado ao enquadramento da `CameraViagemEspacial` (em `Sistema_Viagem_Lunar/Main Camera`, com `posicaoLocalBase = (0, 4.15, -15.20)`, `alvoLocalBase = (0, 0.65, 0.52)`, `fovBase = 60`) e sincronizado com `EncaixeModeloFoguete.pontoCameraExternaOpcional`.

### 2.4 Mapeamento dos 2 Estágios (Ideia 1 — `MinigameSeparacaoEstagios`)
Em `FogueteModeloLogica.estagios`, os dois subobjetos do FBX são associados às duas janelas de `MinigameSeparacaoEstagios` durante `SubindoDaTerra` (e nomeados na cena `NaveEspacial.unity` como `"Estágio 1 — Boosters Laterais (SRB)"` e `"Estágio 2 — Tanque Externo"`):

| Índice | Estágio na Viagem / Cena | Subobjeto em `OnibusEspacial.fbx` | Parâmetros de Descarte em `OnibusEspacialNave.prefab` |
| :--- | :--- | :--- | :--- |
| **`0`** | **`"Estágio 1 — Boosters Laterais (SRB)"`** (janela `0.065..0.125`) | `Onibus_Boosters` (+ `Onibus_Boosters_L1`/`_L2` em `objetosAdicionaisEstagio`) | `direcaoDescarteLocal = (0, -0.55, -1)`, `usarEspacoDaNaveParaDescarte = true`, `velocidadeAfastamento = 7.5`, `velocidadeGiroDescarte = 20`, `tempoVidaEstagioSolto = 8` |
| **`1`** | **`"Estágio 2 — Tanque Externo"`** (janela `0.135..0.195`) | `Onibus_TanqueExterno` (+ `Onibus_TanqueExterno_L1`/`_L2` em `objetosAdicionaisEstagio`) | `direcaoDescarteLocal = (0, -0.80, -1)`, `usarEspacoDaNaveParaDescarte = true`, `velocidadeAfastamento = 6.5`, `velocidadeGiroDescarte = 16`, `tempoVidaEstagioSolto = 8` |
| **Restante** | Cruzeiro e Aproximação à Lua | `Onibus_Orbitador` + `Onibus_Orbitador_Transp` + `Onibus_Interior` | O Orbitador completo (com vidros da cabine e `Tripulante_Astronauta`) prossegue até a Lua. |

**Mecânica de descarte (`FogueteModeloLogica.ExecutarDesacoplamentoVisualSeguro`):**
* Calcula o centro visual real da peça no mundo (`CalcularCentroVisualMundo`) antes de ocultá-la, evitando giro excêntrico em nós FBX cujo pivô original fica distante da geometria.
* Oculta `objetoEstagio` e seus irmãos LOD em `objetosAdicionaisEstagio`.
* Cria um pivô `*_Separado` no centro visual orientado por `ObterReferencialDescarte` (`usarEspacoDaNaveParaDescarte = true`), instancia `*_Visual` preservando `t.lossyScale`, remove componentes bloqueantes (`Camera`, `AudioListener`, `FogueteModeloLogica`, `Collider`, `Light`) e anexa `EstagioDescartadoDeriva`.
* Ao reiniciar na Terra (`ReiniciarEstadoModelo`), as cópias em deriva são destruídas e `RestaurarEstadosIniciais()` devolve `objetoEstagio` e `objetosAdicionaisEstagio` exatamente aos seus estados ativos iniciais.

---

## 3. Como Atualizar ou Substituir o Modelo Sem Quebrar o Gameplay

### 3.1 Atualizar `OnibusEspacial.fbx` quando o artista modificar a branch `vinicius`
Siga esta sequência para absorver melhorias de modelagem/UV/materiais da branch `vinicius` preservando toda a lógica de voo:

1. **Atualize o FBX mantendo o GUID original:**
   * Copie ou faça merge de `Assets/CentroTreinamento/Modelos/OnibusEspacial.fbx` (e novos arquivos em `Assets/CentroTreinamento/Materiais/` ou `Texturas/`, se criados).
   * **Não apague nem regenere o `.meta`** se o `guid` (`614bef39780f3af439767de6f4943616`) já estiver correto, para não quebrar a instância aninhada dentro de `OnibusEspacialNave.prefab` e nem a cena `CentroTreinamento.unity`.
   * Se o artista tiver criado novos materiais no Blender, traga o `OnibusEspacial.fbx.meta` atualizado ou execute no Unity Editor o menu **`Centro de Treinamento/1. Texturas e materiais`** (`CentroTreinamentoSetup.Materiais()`, que chama `EstiloCentro.MateriaisDoModelo`).
2. **Inspecione `Assets/PedroH/Prefabs/OnibusEspacialNave.prefab` em *Prefab Mode*:**
   * Verifique se **`Onibus_Boosters`** e **`Onibus_TanqueExterno`** continuam atribuídos em `FogueteModeloLogica.estagios[0].objetoEstagio` e `estagios[1].objetoEstagio`, e se seus irmãos LOD `_L1`/`_L2` continuam listados em `objetosAdicionaisEstagio`. Caso o artista tenha renomeado algum nó no Blender, basta reatribuir os subobjetos no Inspector.
   * Confirme se os nós **`*_L1` e `*_L2`** continuam desativados na instância aninhada `OnibusEspacial` (se novos nós de LOD tiverem sido adicionados no FBX, desative-os no prefab).
   * Caso o artista altere a escala ou o ponto de origem no Blender (`gerar_plataforma_lancamento.py`), ajuste apenas o `Transform` da instância filha `OnibusEspacial` dentro de `PivotVisual` e confira o alinhamento de `PontoCabine/Tripulante_Astronauta`, `PontoExaustaoPrincipal`/`LuzMotorPrincipal` e `RCS_*`.
3. **Teste rápido em Play Mode (`pedro-h/1. Abrir Cena Existente (NaveEspacial)`):**
   * Confira se o nariz do Ônibus aponta para `+Z` (direção da rota), se o dorso está para `+Y`, se `Onibus_Boosters` e `Onibus_TanqueExterno` somem da nave e entram em deriva nas duas janelas de subida (`[E]`) e se reiniciar a viagem restaura todas as peças sem duplicação.

### 3.2 Substituir por outro modelo 3D no futuro (outro foguete/nave)
Como `ViagemLunarController` e `EncaixeModeloFoguete` conversam exclusivamente com `FogueteModeloLogica`, você pode trocar para qualquer outro modelo sem alterar os scripts de gameplay:

1. **Crie um novo Prefab wrapper em `Assets/PedroH/Prefabs/`** (ou duplique `OnibusEspacialNave.prefab`):
   * Na raiz do prefab, adicione **`FogueteModeloLogica`**.
   * Crie um filho `PivotVisual` e atribua-o em `FogueteModeloLogica.pivotVisualFoguete`.
2. **Alinhe o novo modelo dentro de `PivotVisual`:**
   * Coloque o novo `.fbx`/prefab como filho de `PivotVisual` e ajuste sua rotação, escala e deslocamento locais até que o nariz aponte para **`+Z`** e o topo da nave para **`+Y`**.
   * Mantenha a silhueta em escala compatível com a câmera (`CameraViagemEspacial.posicaoLocalBase = (0, 4.15, -15.20)` na cena) e com o raio de colisão da nave (`GerenciadorAsteroidesViagem.raioNaveMetros = 2.2f`).
3. **Configure os sockets e luzes em `FogueteModeloLogica`:**
   * Posicione `PontoCabine` (e eventual tripulante), `PontoExaustaoPrincipal` (+ luzes em `luzesMotorPrincipal`), `RCS_Esquerda/Direita/Cima/Baixo` (`bocaisRcs`) e `PontoCameraExterna` (opcional).
4. **Mapeie os estágios destacáveis:**
   * Preencha `FogueteModeloLogica.estagios` (quantidade alinhada às janelas de `MinigameSeparacaoEstagios.janelasEstagios`), atribuindo `objetoEstagio`, eventuais `objetosAdicionaisEstagio`, `direcaoDescarteLocal`, `usarEspacoDaNaveParaDescarte = true`, `velocidadeAfastamento`, `velocidadeGiroDescarte` e `tempoVidaEstagioSolto`.
5. **Conecte o novo prefab na cena `NaveEspacial.unity`:**
   * **No Editor:** substitua o filho de `Sistema_Viagem_Lunar/PontoEncaixeModelo` pelo novo prefab (com `localPosition = (0,0,0)` e `localRotation = identity`), atualize `EncaixeModeloFoguete.modeloFogueteAtual` e execute/chame `EncaixeModeloFoguete.SincronizarFogueteModeloLogica()`, que limpa referências quebradas/anteriores e sincroniza pontos opcionais e `luzesPropulsoresOpcionais`.
   * **Ou em runtime:** chame `viagemLunarController.ConectarModeloFoguete(novoPrefab, instanciarSeForPrefab: true)` (que invoca `DesconectarModeloAtual()` e `SincronizarFogueteModeloLogica()` automaticamente).

---

## 4. Resumo do Fluxo Atual de Gameplay na Cena `NaveEspacial.unity`

1. **Decolagem automática (`ProntaNaTerra` → `SubindoDaTerra`, `0.00..0.20`):**
   * Ao iniciar a cena (`iniciarAutomaticamenteAoIniciar = true`), o conjunto completo (Orbitador + Tanque Externo + 2 SRBs) decola da Terra ao longo da curva Bézier (`duracaoAlvoSegundos = 105s`).
   * **Controles ativos após `0.03` de progresso:** `WASD` / Setas para conduzir com aceleração suave e inércia espacial dentro do corredor elíptico (`26m × 14m`); `Shift` ou `Espaço` para impulso leve.
2. **Separação em 2 estágios (`MinigameSeparacaoEstagios`):**
   * O `HudViagemFoguete` exibe o aviso central `"HORA DE DESACOPLAR!"` nas duas janelas (`0.065..0.125` e `0.135..0.195`):
     * 1º toque em `[E]` / `[F]` (`"Estágio 1 — Boosters Laterais (SRB)"`): solta **`Onibus_Boosters`** com impulso bônus (`+0.45` por `2.4s`).
     * 2º toque em `[E]` / `[F]` (`"Estágio 2 — Tanque Externo"`): solta **`Onibus_TanqueExterno`** com novo impulso bônus.
   * Caso o jogador não pressione a tempo, `autoSepararAoExpirarJanela = true` realiza a separação segura ao fim da janela.
3. **Cruzeiro até a Lua (`CruzeiroEspacial`, `0.20..0.85`):**
   * Apenas o **Orbitador** (`Onibus_Orbitador` + `Onibus_Orbitador_Transp` + `Onibus_Interior` com o tripulante) segue viagem.
   * `MinigameCorrecaoRcs` avalia eventos de alinhamento/detritos, enquanto `GerenciadorAsteroidesViagem` (`raioNaveMetros = 2.2f`) monitora os 10 asteroides reais da rota (`Ambiente_Espacial/Asteroides_Rota`): quando um asteroide está diretamente na trajetória à frente (`distanciaAntecedenciaAlerta = 80m` na cena), o HUD mostra `"ASTEROIDE À FRENTE!"` com a direção sugerida de desvio (`[D / ->]`, `[A / <-]`, `[W / Cima]`, `[S / Baixo]`). Esbarrar gera apenas recuo físico suave e tremor leve, sem dano punitivo.
4. **Aproximação e chegada (`AproximandoDaLua`, `0.85..1.0` → `ChegouNaLua`):**
   * A partir de `0.85`, a nave converge suavemente para o centro da rota e conclui a travessia na Lua com o aviso `"CHEGAMOS À LUA!"` no HUD — mantendo **oxigênio sempre em 100%** e **sem etapa de frenagem orbital** nesta cena.
