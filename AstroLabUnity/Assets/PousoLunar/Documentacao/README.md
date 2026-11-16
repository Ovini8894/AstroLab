# Pouso lunar — câmera elevada e base visual

## Estado e preservação

A animação e o ritmo já foram aprovados visualmente pelo usuário. Esta melhoria preserva controlador, trajetória, orientação, RCS, motor, poeira, touchdown, desligamento e APIs públicas de transição. O destino continua **world `(24.2, -0.071624, 20.3)`**, na precisão original da cena. O arquivo `preservacao-apresentacao.json` registra os hashes dos scripts protegidos no início desta melhoria.

A branch permanece `nicholas`. As alterações pendentes anteriores foram mantidas. Não houve commit, push, troca de branch, mudança do FBX ou edição de cenas/scripts dos colegas. Toda a implementação está em `Assets/PousoLunar/`.

Runtime e Editor compilaram com Roslyn e as referências reais do Unity **6000.6.9f1**, sem erros ou avisos. A validação estática verifica referências locais, novos GUIDs, quatro planos elevados, ambiente visual e preservação dos scripts protegidos/waypoints.

**A nova composição ainda não recebeu aprovação visual em Play Mode nesta sessão.** Há um Editor aberto, mas ele ainda não importou os novos scripts nem respondeu ao comando local de status. O descritor de conexão do Pipeline está inacessível neste ambiente; não foi aberta outra instância. Isso é diferente da falha de licenciamento registrada na entrega anterior: o log atual informa licença válida. Não há capturas novas desta melhoria. A aprovação anterior do movimento não deve ser confundida com aprovação dos novos enquadramentos.

## Câmera e quatro tomadas

`LunarLandingCamera.cs` continua dirigindo a **mesma CinemachineCamera**, e o Brain continua responsável pela Camera de saída. Não há outro sistema de câmera nem alteração da animação para compensar o enquadramento.

As quatro âncoras existentes acompanham o ponto observado com uma posição orbital elevada. A elevação é o ângulo para baixo em relação ao alvo; a distância é radial, não apenas horizontal. A órbita usa o referencial mundial: 0° no lado +Z, 90° no lado +X. O setor +Z/+X mostra o dorso e a lateral do orbitador e, no pouso, coloca a base atrás da nave.

| Tomada | Distância | Elevação | Órbita | FOV | Transição |
|---|---:|---:|---:|---:|---:|
| Approach — `establishing` | 28 m | 32° | 35° | 46° | 2 s |
| Deceleration — `approach` | 22 m | 30° | 55° | 44° | 2 s |
| VerticalDescent — `descent` | 19 m | 34° | 40° | 46° | 2 s |
| Touchdown/final — `final` | 23 m | 30° | 45° | 48° | 2 s |

O nome histórico do campo `approach` corresponde à segunda tomada/desaceleração. As três primeiras miram o `motionRoot`; a última mira `LandingPoint`. Todas usam offset `(0,3.8,0)`, próximo ao centro do orbitador de 8 m.

As transições interpolam posição e ponto observado e só então calculam a direção da câmera. Isso evita perder o sujeito por interpolar duas rotações independentes. O cálculo é determinístico: `Sample()` continua produzindo a mesma pose para o mesmo instante, sem acumular atraso. `smoothing` varia entre transição linear e curva suave de aceleração/desaceleração; não introduz amortecimento dependente de frame rate. A trajetória aprovada já fornece o acompanhamento contínuo.

Para ajustar: selecione `RocketLandingRoot > LandingCameraRig`, abra o componente `LunarLandingCamera` e edite `distance`, `elevation`, `orbitAngle`, `targetOffset`, `fieldOfView`, `blendDuration` e `smoothing` da tomada. `followTarget=true` usa a receita orbital e atualiza a âncora existente; `false` mantém sua posição fixa, editável manualmente. A receita tem prioridade sobre arrastar uma âncora no modo de acompanhamento.

A cena já estava salva com Brain **LateUpdate** e Blend Update **LateUpdate** quando foi inspecionada nesta melhoria. O prefab usa **SmartUpdate**, também automático e compatível. Não foram introduzidas chamadas `ManualUpdate()`. A iluminação/toon existentes foram preservados; o brilho e o enquadramento do dorso precisam da inspeção visual final.

## Base lunar visual reutilizada

A investigação confirmou que `Assets/BaseLunar/Cenas/Lua.unity` e `Assets/Nicholas/Cenas/MissaoNicholas.unity` usam `Assets/BaseLunar/Modelos/BaseLunar.fbx`, GUID `a564f47d55b9d42f8a7b4b43e84134c3`. Não existe um prefab visual independente de base nessas pastas. A raiz GameObject real do FBX tem ID `919132149155446097`, identificado na referência da cena.

O FBX contém o Hub, abrigo, eclusa, estufa, suporte de vida, usina, corredores, tanques, 31 painéis solares e rochas. Ambas as cenas mantêm a raiz na origem, rotação identidade, sem overrides de materiais ou transforms dos filhos. A montagem visual pode assim compartilhar os mesmos meshes/materiais e manter as posições originais.

A cena de pouso e o novo prefab `Prefabs/LunarBaseVisualEnvironment.prefab` possuem uma receita `LunarLandingEnvironment` ligada à raiz GameObject correta do FBX. Ao habilitar, `EnsureBuilt()` cria **somente Transform, MeshFilter e MeshRenderer** dos elementos necessários. As malhas e os materiais são compartilhados por referência; nenhum arquivo grande é copiado. Não instancia os GameObjects originais nem os seus scripts.

São excluídos terreno, helpers `COL_`/`SPAWN_`/`WP_`, céu/Terra e estrelas. As estruturas e painéis ficam nas coordenadas originais; as rochas são limitadas a um raio horizontal de 70 m do pouso, configurável por `nearbyRockRadius`. O único terreno continua sendo `LandingTerrain_ExistingBlenderMesh`. Não são criadas luzes direcionais, câmeras, AudioListeners, HUD, scanners ou sistemas de missão.

```text
PousoLunar.unity
  RocketLandingRoot
    RocketMotionRoot / RocketVisualSocket / FinalSpacecraftModel
    LandingWaypoints
    LandingCameraRig
  LandingTerrain_ExistingBlenderMesh       terreno único existente
  LandingSun / LandingCartoonStyle         iluminação existente
  LunarBaseVisualEnvironment               origem e escala unitária
    GeneratedLunarBaseVisuals              apenas transforms/meshes/renderers
```

Os filhos automáticos são transitórios (`DontSaveInEditor`/`DontSaveInBuild`); a receita e as referências ficam persistidas. São remontados ao abrir a cena/entrar em Play e não são recriados por frame. `MatchesTerrain()` compara mesh, posição, rotação e escala do terreno com a origem, além de exigir identidade do ambiente. Não mova a base visual ou o terreno para melhorar a câmera.

Opcionalmente, **AstroLab > Pouso Lunar > Ambiente > Recriar prefab visual da base** extrai um snapshot visual de `Lua.unity` em uma PreviewScene de Editor. Esse caminho também preserva três ajustes de shadow casting da cena que a receita direta do FBX não captura. Não altera `Lua`, não carrega gameplay em runtime e não chama `AssetDatabase.SaveAssets()`.

O montador de Editor pode abrir temporariamente a fonte em PreviewScene para ler referências; o Play isolado carrega somente PousoLunar. Não há carregamento aditivo do gameplay na cinemática.

## Erros investigados

| Mensagem | Evidência e correção |
|---|---|
| `sourceModel` esperava GameObject, mas recebeu Prefab | Confirmada repetidamente em `Logs/Editor.log`. O prefab usava ID `100100000`, que representa o objeto Prefab do FBX. A cena já havia sido corrigida e foi preservada. O prefab agora aponta ao GameObject real `919132149155446097`. |
| `InvalidCastException` | O stack do mesmo log aponta `Instantiate<T> → LunarLandingSpacecraft.EnsureBuilt → OnEnable`. A referência incompatível era a causa; corrigir o prefab remove essa causa, sem alterar o sistema de propulsão. A ausência de recorrência precisa do próximo Play. |
| `CinemachineBrain.ManualUpdate...` | Não encontrada no log atual. A cena já usa LateUpdate; o controlador não chama ManualUpdate. Nenhuma correção especulativa foi aplicada. |
| Dois AudioListeners | Não encontrada no log atual. A cena isolada tem um listener; o ambiente visual não pode criar outro. Nenhum listener de gameplay foi desabilitado globalmente. |

`LunarLandingModelResolver.CaptureModelIdentity()` registra o objeto real obtido por `AssetDatabase.LoadAssetAtPath<GameObject>` e seu GUID/fileID em `identidade-modelo-unity.json` quando o Editor importar os scripts. O ID é gravado como string para não perder precisão de 64 bits em JavaScript. O resolver também atua no processamento da cena para build. O menu **Reparar referências serializadas do modelo** salva somente o prefab e uma cena de pouso elegível, sem descartar cenas sujas e sem SaveAssets.

## Como validar no Unity

1. Use a única sessão aberta do Unity 6000.6.9f1. Preserve/salve seu trabalho antes de sair de Play ou trocar de cena.
2. Aguarde a importação e compilação dos scripts. Abra **somente** `Assets/PousoLunar/Cenas/PousoLunar.unity`, em modo Single. Não abra MissaoNicholas manualmente para este teste.
3. Confira a montagem da nave e de `LunarBaseVisualEnvironment`; o ambiente deve mostrar estruturas/painéis sem outro terreno, câmera, listener ou HUD.
4. Execute **AstroLab > Pouso Lunar > Validar modelo final e VFX em Play isolado**. O teste recusa sessões já em Play ou cenas com trabalho não salvo. O menu **Configurar apresentação e base visual** reaplica o preset dos quatro planos e instala o ambiente em uma cena limpa; não é necessário para o conteúdo já configurado.
5. O validador observa quadros reais e solicita capturas em 6, 11, 21 e 23,5 s: `play-approach.png`, `play-deceleration.png`, `play-descent.png`, `play-touchdown.png`, em `Documentacao/`.
6. Confere a Camera de saída contra o estado da Cinemachine, a nave inteira dentro do frustum, câmera elevada, base no frustum final, terreno único/alinhado, ambiente estritamente visual, um listener, ausência de HUD, partículas RCS/motor/poeira, destino exato e desligamento. A checagem de frustum não prova ausência de oclusão nem qualidade artística.
7. Leia `ULTIMO_TESTE_PLAY.md`, gerado somente ao executar o teste, e inspecione as quatro imagens. Confira cockpit/asas legíveis, transições sem cortes, nave sem oclusão, base ao fundo, plumas e poeira visíveis. A aprovação visual depende dessas capturas reais.

O teste ativa `runInBackground` e solicita atualizações de quadros apenas durante sua execução; restaura o valor anterior ao retornar a Edit Mode. Se ainda existir Brain manual, o harness conserva sua investigação de reprodução antes de persistir qualquer correção. Na configuração atual automática, esse trecho não é necessário.

O Inspector do controlador mantém os botões de iniciar/reiniciar/resetar e os indicadores de estado/tempo. Os testes individuais de RCS, motor e poeira continuam nos respectivos componentes e no Inspector da nave. O componente `LunarLandingEditorSession` aceita apenas comandos locais tipados de status/configuração/validação, via arquivos em Documentacao, para usar o Editor já aberto quando uma conexão de automação não estiver disponível; não avalia código arbitrário nem inicia um Editor.

## Nave e propulsão preservadas

Modelo: `Assets/CentroTreinamento/Modelos/OnibusEspacial.fbx`, commit `7df2af4`, GUID `614bef39780f3af439767de6f4943616`. `LunarLandingSpacecraft` mantém apenas o orbitador visível e normaliza sua altura para 8 m, dentro de `RocketVisualSocket`. O antigo preview não retorna. Consulte [INSPECAO_MODELO.md](INSPECAO_MODELO.md).

`LunarLandingEffects` encaminha o perfil existente ao motor e à poeira; RCS mede a mudança real de atitude. O motor mantém três bocais, núcleo/expansão/brilho, modulação e shutdown. A poeira mantém emissão pela altura/empuxo, distribuição radial e aceleração de 1,62 m/s². Seus scripts não foram alterados nesta melhoria.

A sequência e a API de integração permanecem: viagem externa → fade/load → aproximação → desaceleração/RCS → descida/motor → touchdown/poeira → shutdown/plano final → transição para gameplay, quando habilitada pela configuração existente. Nenhum spawn ou sistema de viagem foi modificado.

## Limitações e verificações reproduzíveis

- A composição, a iluminação percebida e a visibilidade da base ainda precisam do novo Play com capturas.
- A receita direta do ambiente não inclui três overrides de shadow casting específicos de Lua; use o builder de snapshot se esses detalhes forem necessários. Posições e materiais correspondem à origem verificada.
- O Shuttle original não possui sapatas nem representa um pousador lunar físico. Essa limitação anterior não foi alterada, pois o movimento foi aprovado e deve ser preservado.
- [CHECAGEM_ROCHA.md](CHECAGEM_ROCHA.md) registra a proximidade de `Rocha_2123`: o topo fica 52,59 mm acima do contato central; há sobreposição conservadora de AABBs, sem prova de colisão entre triângulos. A rocha e o destino foram preservados.

Da raiz `AstroLabUnity`:

```powershell
& Assets/PousoLunar/Documentacao/validar-compilacao.ps1
node Assets/PousoLunar/Documentacao/validar-configuracao.cjs
```

A compilação externa não substitui o Unity importando/renderizando os assets. A validação estática compara os hashes protegidos, controlador e waypoints; aceita explicitamente os ajustes da câmera e rejeita `sourceModel` apontando ao Prefab incompatível.

Arquivos desta melhoria: câmera existente; novo ambiente runtime, prefab e builder de Editor; configuração de apresentação; diagnóstico da sessão; validador em Play ampliado; resolver/receita de referências corrigidos; cena/prefab e documentação atualizados. Nenhum novo pacote foi instalado.

Referências consultadas: `craft-game-floors`, `references/camera-rigs.md`, `references/visual-scorecard.md`, `Assets/ArtBible.md`, fonte instalada do Cinemachine 6.6 e [CinemachineBrain 6.6](https://docs.unity3d.com/Packages/com.unity.cinemachine@6.6/manual/CinemachineBrain.html). Não foi atribuída pontuação visual sem frames reais da nova composição.
