# Pouso lunar — AstroLab

Sistema cinematográfico determinístico, independente do FBX final. Implementação em `Assets/PousoLunar`, no projeto Unity localizado em `AstroLabUnity/`.

## Estado da entrega

Os scripts de runtime e Editor compilaram separadamente com Roslyn e as bibliotecas locais do Unity **6000.6.9f1**, sem erros ou avisos. A cena e o prefab já existem. O ajuste do destino foi aplicado pelas APIs do Editor na instância aberta do Unity, sem regenerar a cena. Consulte [VALIDACAO.md](VALIDACAO.md) para distinguir as verificações atuais do bloqueio de licença ocorrido na implementação inicial.

O gerador abaixo atende novas demonstrações; para a cena existente, use os comandos de ajuste de destino. Não foram escritos arquivos `.unity` manualmente, alteradas cenas de colegas, instalados pacotes Unity ou modificadas as cenas do build.

## Estrutura

```text
PousoLunar/
  Scripts/       runtime, estados, câmera, efeitos e integração
  Editor/        Inspector, gerador de assets e harness de validação
  Cenas/         destino de PousoLunar.unity (gerado pelo menu)
  Prefabs/       destino de RocketLandingRoot.prefab (gerado pelo menu)
  Blender~/      reservado; nenhum modelo novo foi necessário
  Modelos/       reservado para FBX de responsabilidade deste sistema
  Documentacao/  instruções, resultado de validação e compilação local
```

## Arquitetura e responsabilidades

| Arquivo | Responsabilidade |
|---|---|
| `LunarLandingState.cs` | Estados e tipos de UnityEvent serializáveis. |
| `LunarLandingController.cs` | Relógio único, trajetória Hermite, descida vertical, alinhamento, contato, assentamento, eventos, reset e configuração do visual. |
| `LunarLandingCamera.cs` | Quatro planos, interpolação analítica de posição/rotação/FOV e alvos configuráveis; dirige uma CinemachineCamera passiva. |
| `LunarLandingEffects.cs` | Adapter opcional para partículas, áudio e eventos de intensidade de emissão. |
| `LunarLandingTransition.cs` | API estática da viagem, fade-out, carga assíncrona e preparação antes do início; reutiliza `AstroLab.Menu.TransicaoPreta` para o fade-in. |
| `LunarLandingSceneLink.cs` | Handoff opcional para Lua e coordenação de entrada na cena. |
| `Editor/LunarLandingEditor.cs` | Inspector com comandos de teste e gerador isolado de cena/prefab. |
| `Editor/LunarLandingValidation.cs` | Harness de trajetória/eventos/material e teste de Play Mode em lote. |
| `Editor/LunarLandingSiteEditor.cs` | Ajuste mundial do destino, amostragem dos triângulos, alinhamento do terreno local e atualização do prefab existente. |
| `Editor/LunarLandingSiteValidation.cs` | Compara Lua/demonstração, testa a trajetória contra terreno/ambiente e executa os testes originais em uma cena temporária isolada. |
| `Documentacao/validar-compilacao.ps1` | Compilação externa de runtime e Editor usando referências locais; não substitui os testes no Unity. |

```text
RocketLandingRoot [Controller, Effects, SceneLink]
  RocketMotionRoot                 ← único Transform animado
    RocketVisualSocket            ← vazio no prefab reutilizável
    EngineEffectsSocket
    LandingEffectsSocket
  LandingCameraRig
    LandingOutputCamera [Camera, CinemachineBrain, AudioListener]
    LandingCinemachine [CinemachineCamera sem pipeline procedural]
    Shot1_Establishing / Shot2_Approach / Shot3_Descent / Shot4_Final
  LandingWaypoints
    Approach / Deceleration / VerticalDescent / LandingPoint
```

Não existe dependência de nome, dimensão, pivô ou estrutura do FBX. Nenhuma malha é criada por C# ou por primitivas Unity. Os scripts usam o assembly padrão do projeto; `AstroLab.Menu` e Cinemachine já estão disponíveis como referências automáticas.

## Estados e relógio

`Idle → TransitionIn → Approach → Deceleration → VerticalDescent → Touchdown → EngineShutdown → Completed`.

- `PrepareLanding()` valida e captura os waypoints, reseta efeitos, posiciona o rig e entra em `TransitionIn` sem animar.
- `BeginLanding()` prepara quando necessário e inicia a aproximação. Retorna `false` se já houver animação em andamento ou a configuração for inválida.
- Aproximação e desaceleração usam Hermite com velocidades coincidentes na junção. A desaceleração termina com a velocidade inicial da descida vertical.
- A curva vertical padrão `2t - t²` reduz a velocidade continuamente até zero. A duração vertical padrão é 8 s; com `useDescentVelocity`, o tempo deriva da altura e de `descentVelocity`.
- No contato, `touchdown` dispara uma vez; um micro rebote fica apenas acima do plano de contato e retorna à posição exata. O pivô do movimento nunca atravessa esse plano.
- `EngineShutdown` inclui a redução de efeitos durante `engineShutdownDuration` e depois a espera estacionária de `finalCinematicDuration`.
- `completed` e o evento C# `Completed` disparam somente após a tomada final. O destino final é exatamente a posição/rotação capturada de `LandingPoint`, com `finalRotationOffset` aplicado.
- `Advance(double)` permite amostrar com deltas controlados. Mesmo um delta longo percorre todos os estados e eventos; durante o jogo, apenas o controller o chama em `Update`, com `Time.unscaledDeltaTime`.
- `ResetLanding()` interrompe e limpa o estado. `RestartLanding()` reseta e inicia novamente. Desabilitar o controlador cancela a execução.

Não altere durações ou waypoints durante uma tomada: configure em `Idle` e reinicie. As referências espaciais são capturadas na preparação. Curvas devem ser monotônicas de (0,0) a (1,1); há verificação amostral de configuração. As curvas lineares padrão de aproximação/desaceleração preservam as velocidades nas junções. Personalizar as tangentes das curvas pode alterar essa continuidade.

## Criar e testar no Unity

1. Ative a licença do Unity 6000.6.9f1 e abra **este** projeto (`AstroLabUnity`). Não abra uma segunda instância do mesmo projeto.
2. Aguarde a compilação. Execute **AstroLab > Pouso Lunar > Criar demonstração isolada**.
3. O comando cria `Prefabs/RocketLandingRoot.prefab` e `Cenas/PousoLunar.unity`. Usa uma cena aditiva nova, não salva a cena previamente aberta e não chama Save Project nem `AssetDatabase.SaveAssets`.
4. Abra `Assets/PousoLunar/Cenas/PousoLunar.unity`. Entre em Play Mode. `playOnStart` inicia o pouso.
5. Selecione `RocketLandingRoot`: o Inspector mostra estado e tempo e oferece **Iniciar pouso**, **Reiniciar pouso**, **Resetar posição** e **Selecionar ponto de pouso**.
6. Ative Gizmos e selecione o rig: as curvas, waypoints e normal de contato ficam visíveis. Selecione `LandingCameraRig` para ver posições e linhas dos alvos das câmeras.
7. A demonstração inclui um preview removível de `Assets/MenuInicial/Modelos/Foguete.fbx`, já produzido em Blender. Ele não representa o futuro modelo de pouso. Remova/desative `PREVIEW_ExistingBlenderRocket_RemoveForFinalFBX` para testar sem modelo.
8. O prefab permanece sem visual. Os efeitos ficam desconectados por padrão; a tomada deve concluir mesmo assim. Observe o evento `completed` no Inspector ou conecte seu listener C#.
9. Execute **AstroLab > Pouso Lunar > Validar trajetória e eventos** para executar o harness em Edit Mode. Ele deve registrar `LANDING_VALIDATION_PASS`.

O gerador não sobrescreve uma cena ou prefab já existente. Para ajustar assets gerados, edite e salve explicitamente apenas esses arquivos. O cenário usa somente o mesh `Terreno_Lua` do FBX existente, com material de regolito, preservando seu sistema de coordenadas original. O ponto de contato acompanha a altura real do terreno. Não carrega scripts, jogadores, colliders ou gameplay da Base Lunar.

## Destino escolhido perto da base

O marcador `PontoPousoEscolhido` foi selecionado com `MissaoNicholas` e `PousoLunar` abertos aditivamente, em **world (24.2, 0, 20.3)**, rotação zero e escala unitária. Ele foi preservado. Sua posição XZ é o destino; Y foi corrigido pela interseção vertical com os triângulos reais, sem offset arbitrário.

**Contato configurado: aproximadamente world (24.2, -0.071624, 20.3), rotação (0,0,0).** Diferenças inferiores a alguns milionésimos de metro decorrem da aritmética de ponto flutuante da transformação dos vértices.

O gerador antigo tinha deslocado somente o terreno da demonstração em **Y=-0.02471396** para zerar o solo na origem. Isso deixava a superfície em cerca de **-0.096338** no novo destino, incompatível com a instância de `MissaoNicholas`, cujo terreno está em posição zero, rotação aproximadamente X=270.02° e escala 100 (conversão do FBX). O ajuste removeu esse deslocamento apenas na demonstração, copiando o TRS da referência lunar. Não altera a geometria, o asset FBX ou as cenas de gameplay.

Todos os waypoints e anchors de câmera foram transladados em coordenadas mundiais pelo mesmo delta. Os valores aproximados do rig atual são:

| Waypoint | Posição mundial |
|---|---|
| Approach | (-35.8, 64.928376, 20.3) |
| Deceleration | (4.2, 31.928376, 20.3) |
| VerticalDescent | (24.2, 17.928376, 20.3) |
| LandingPoint | (24.2, -0.071624, 20.3) |

O enquadramento final mantém altura, distância e FOV, mas posiciona o anchor no lado externo, mirando o foguete com a origem da base ao fundo. Os blends e o controlador de câmera não foram substituídos.

Para reaplicar o destino atual: **AstroLab > Pouso Lunar > Aplicar ponto escolhido (24.2, 20.3)**. Para mudar:

1. Saia do Play Mode e coloque um marcador no novo XZ, usando o Inspector em coordenadas mundiais.
2. Selecione esse marcador e execute **AstroLab > Pouso Lunar > Aplicar marcador selecionado como destino**.
3. O comando usa `MissaoNicholas`/`Lua` carregado como referência; se nenhum estiver aberto, consulta `Lua` em uma PreviewScene temporária, sem salvá-la.
4. A altura é amostrada da malha real. Waypoints/câmeras são deslocados com `Transform.position`, respeitando offsets dos pais. A cena e o prefab de pouso são salvos; outros objetos não são recriados. Alterações já existentes na cena de pouso são preservadas.
5. Inspecione o apoio das pernas e o enquadramento, e execute **AstroLab > Pouso Lunar > Validar destino e ambiente carregado**, com `MissaoNicholas` carregado para incluir a checagem do ambiente.

Para um ajuste manual, `LandingPoint` e todos os anchors continuam editáveis no Inspector. Preserve o alinhamento vertical de `VerticalDescent`; mover somente `LandingPoint` não desloca automaticamente os demais anchors. O comando de destino evita essa inconsistência e pode ser repetido sem acumular deslocamentos.

As cenas `MissaoNicholas` e `Lua` e o spawn do jogador não são alterados. Para filmar a **base** ao fundo, o ambiente da missão precisa estar carregado: a demonstração standalone contém somente o terreno existente. O ajuste não copia prédios nem gameplay para a cena de pouso.

Validação atual: teste de trajetória/câmera/estados no Editor e tomada completa em Play Mode aprovados; Play Mode registrou conclusão correta e zero erros durante o teste. Na conferência espacial, 2.561 renderers foram consultados sem conflito de volumes com prédios/painéis. Há um aviso conservador para a pequena rocha `Rocha_2123` perto do contato; conferir os apoios com o FBX final. Não foi elevado o ponto de pouso para mascarar essa irregularidade. Consulte o registro e a [captura do plano final](preview-ponto-pouso.png).

Para testar com as duas cenas aditivas, evite renderizar dois terrenos/sols coincidentes: desative temporariamente os objetos equivalentes **da demonstração**, sem salvar essa alteração se a cena também for usada sozinha. O HUD/câmera do gameplay pode aparecer nesse teste aditivo; a captura de validação usa explicitamente `LandingOutputCamera` sem overlay. O Play Mode automatizado foi executado somente com a cena de pouso.

## Configurar posição, orientação e câmeras

- O pivô de `RocketMotionRoot` representa o ponto de contato com o solo, não necessariamente o pivô do FBX.
- Posicione `LandingPoint` na superfície. Seu Y local define a normal; sua rotação define o alinhamento final.
- Posicione `VerticalDescent` diretamente acima, ao longo desse Y local. Desalinhamento lateral é rejeitado para evitar correções bruscas.
- Posicione `Approach` e `Deceleration` no trajeto, fora da hierarquia animada. A rotação de `Approach`, somada a `initialInclination`, define a atitude inicial.
- Ajuste durações, `approachSpeed`, curvas e `settlingHeight`. O valor padrão do assentamento é 5 cm, apenas para cima.
- Posicione os quatro anchors da câmera no cenário; configure target, offset, FOV e blend de cada plano. A duração efetiva do blend é limitada pela duração da fase para completar antes da próxima transição.
- O Brain dirige somente a câmera criada no rig. Nenhuma câmera de outra cena é alterada. Cinemachine 6.6 já está instalado; não houve instalação de pacotes.
- O controller protege o plano do waypoint, não a malha real do terreno. Verifique inclinação, irregularidades e apoio de todas as pernas ao conectar o modelo final.

## Integrar a viagem Terra–Lua

Inclua a cena de pouso no **Build Profiles > Scene List** do perfil utilizado. O caminho padrão é `Assets/PousoLunar/Cenas/PousoLunar.unity`. O código não altera essa lista automaticamente.

No término da viagem, em um evento único:

```csharp
using AstroLab.PousoLunar;

bool started = LunarLandingTransition.BeginLanding();
// false: outra transição está ativa ou a cena não está disponível no build.
```

Também existe `BeginLanding(string scenePath)` para uma cena configurada pela equipe. O fluxo é fade-out → carga single → `PrepareLanding()` → fade-in → `BeginLanding()`. A API usa tempo não escalado. O objeto de transição persiste somente durante a troca.

Mantenha `LunarLandingSceneLink` junto ao controller na cena de destino: ele desativa o autoplay quando a cena é reivindicada pela transição. Não dispare um segundo fade externo na mesma troca. A viagem Terra–Lua não é implementada aqui.

O fade existente `TransicaoPreta` oferece apenas a revelação após a troca. Por isso, o adapter local adiciona o escurecimento anterior, sem substituir nem modificar o sistema do menu. A revelação existente começa com dois quadros de preparação; o controller é preparado imediatamente após a carga. Essa temporização precisa da validação em Play Mode.

## Integrar Lua e devolver controle

O caminho verificado no repositório é **`Assets/BaseLunar/Cenas/Lua.unity`**, já habilitado em `ProjectSettings/EditorBuildSettings.asset`. Confira também a lista efetiva do perfil de build.

Na demonstração, `LunarLandingSceneLink.loadMoonOnCompletion` fica **desativado** para permitir inspecionar o resultado. Ative para o fluxo completo. Quando `Completed` dispara após a tomada final, o link chama `LunarLandingTransition.LoadMoon()`: fade-out → carga single da Lua → fade-in existente. O gameplay da Lua inicializa normalmente por seus próprios componentes.

Para integração gerenciada por outro sistema, deixe a carga automática desativada e escute `controller.Completed` ou `completed` no Inspector. Desinscreva listeners C# em `OnDestroy`/`OnDisable`. Também existe `ContinueToMoon()` no link, acessível por UnityEvent.

Não há transferência de jogador, inventário ou estado de missão entre cenas. Caso a equipe precise disso, conecte o evento ao gerenciador de sessão existente. Nenhum objeto ou script de `BaseLunar` precisa ser editado.

## Conectar o FBX final

1. Importe o FBX no diretório acordado com o autor; se pertencer a este módulo, use `Modelos/`.
2. Duplique/instancie o prefab do rig em uma cena de responsabilidade da equipe. Remova apenas o preview da demonstração.
3. Instancie o FBX sob `RocketVisualSocket`; não mova o controller para dentro do FBX.
4. Configure materiais existentes compatíveis com `AstroLab/Toon` e o contorno do projeto. Não use materiais padrão ou URP/Lit.
5. Ajuste posição, escala e rotação **locais do modelo**, fazendo a base das pernas coincidir com a origem de `RocketMotionRoot`. O eixo de subida do movimento é o Y de `LandingPoint`.
6. Verifique bounds, silhueta, altura da câmera e apoio das pernas. Nenhuma dimensão do modelo está embutida na animação.
7. Posicione `EngineEffectsSocket` nos motores e conecte os efeitos opcionais. Se houver vários motores, use filhos/efeitos próprios e os eventos de intensidade.
8. Teste aproximação, contato e tomada final. Não é necessário mudar a lógica C#.

Também pode configurar uma instância por código:

```csharp
controller.ConfigureVisual(model.transform, localPosition, localEulerAngles, localScale);
```

O método não destrói o visual anterior: a substituição e a propriedade do asset ficam com o chamador.

## Configurar efeitos e áudio

Todos os campos de `LunarLandingEffects` são opcionais. Conecte ParticleSystems já preparados em `engine`, `dust` e `touchdownBurst`. Configure materiais, cores, lifetime e tamanhos conforme `ArtBible.md`/`PaletaLua`.

- Engine e dust: `playOnAwake` desligado, looping conforme necessário, emission por tempo. O adapter controla `rateOverTime`; remova bursts/rate-over-distance independentes se quiser corte total de emissão.
- Dust e touchdown: simulação em **World**, para não arrastar partículas com o foguete. O adapter ancora os emissores no ponto de contato capturado.
- A emissão de dust sobe perto do fim da descida, atinge o máximo no contato e cai no shutdown. O lifetime deve permitir dissipar a poeira antes da tomada final.
- `engineAudio`: clip já existente, looping, `playOnAwake` desligado e mixer do projeto quando aplicável; o adapter controla volume e Play/Stop. Nenhum clip novo é criado.
- `engineIntensity` e `dustIntensity` permitem conectar emissão de materiais ou VFX próprios. `stateChanged` sinaliza desaceleração/shutdown e `onTouchdown` dispara feedback de contato.
- Não conecte o mesmo sistema como engine e dust. Não crie luzes extras para o motor sem necessidade.

## Validação e comandos

Compilação local, na raiz do repositório:

```powershell
& ./AstroLabUnity/Assets/PousoLunar/Documentacao/validar-compilacao.ps1
```

Requer o cache `Library/Bee` e a instalação local do Editor. A saída fica em `%TEMP%/AstroLab-PousoLunar-Compilation`, sem escrever assemblies no projeto. Compila fontes existentes de fade/paleta somente como referências de validação, sem modificá-las.

Com o Editor fechado e licenciado, validação em lote:

```text
Unity.exe -batchmode -nographics -quit -projectPath <CAMINHO>/AstroLabUnity -executeMethod AstroLab.PousoLunar.Editor.LunarLandingValidation.BuildAndValidate -logFile <LOG>
Unity.exe -batchmode -nographics -projectPath <CAMINHO>/AstroLabUnity -executeMethod AstroLab.PousoLunar.Editor.LunarLandingValidation.StartPlayValidation -logFile <LOG>
```

Não use `-quit` no segundo comando: o harness sai após conclusão ou timeout. Não rode esses comandos se o mesmo projeto já estiver aberto.

O harness verifica: início e bloqueio de duplicação, todos os estados com delta longo, eventos únicos, precisão do contato, reset/reinício, reprodução com passos distintos, posição/rotação de câmera, continuidade de posição, ausência de penetração no plano e referências/materiais dos assets gerados. O teste de Play Mode verifica conclusão e erros registrados durante a tomada. Fade entre cenas, áudio/VFX conectados e tomada visual devem ser conferidos manualmente no Editor.

## Limitações e decisões de integração

- O bloqueio de licença pertence à implementação inicial; os resultados do ajuste atual estão em `VALIDACAO.md`. Não se deve inferir uma aprovação visual do modelo final a partir do preview.
- FBX final, efeitos e áudio definitivos dependem dos responsáveis; o preview existente é somente para inspeção da animação.
- A demonstração reutiliza o mesh de terreno e um sol próprio em uma cena isolada; não adiciona sistemas de gameplay, nem duplica a cena Lua.
- `IluminacaoCartoon` e os shaders existentes preservam a direção de arte. Não foi criada uma nova família de shaders ou configuração de pós-processamento. O renderer ativo deve manter o recurso de contorno existente.
- Sem física, colisão dinâmica, correção automática para dimensões do FBX ou armazenamento de estado de missão.
- Um rig ativo por cena de pouso. O bloqueio de animação é por controller; o bloqueio da transição é global.
- Não use callbacks de estado para iniciar repetidamente novas sequências; integre conclusão com uma única troca de cena.
- Build Profiles, ativação da carga automática de Lua e ajuste visual final são configurações manuais deliberadas.

## Referências consultadas

Skill oficial `Unity-Technologies/skills`, `craft-game-floors`, commit `e5956e7e34a125982550de15a9e87f8e66af7d86`: `SKILL.md`, `art-direction.md`, `scene-lighting.md`, `toon-shading.md`, `camera-rigs.md`, `placeholder-assets.md`, `vfx.md`, `visual-scorecard.md`. As escolhas existentes do projeto e as restrições do pedido prevalecem: não foram usados placeholders primitivos, instalados pacotes ou alterados sistemas alheios. A revisão visual da skill permanece pendente pela licença.

APIs conferidas na documentação oficial: [LoadSceneAsync 6000.6](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/scenemanagement/scenemanager/loadsceneasync), [AnimationCurve 6000.6](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/animationcurve), [CinemachineCamera 6.6](https://docs.unity3d.com/Packages/com.unity.cinemachine@6.6/manual/CinemachineCamera.html), [ParticleSystem.EmissionModule 6000.6](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/ParticleSystem.EmissionModule.html). Foi inspecionada também a fonte local do pacote Cinemachine instalado.
