# Mãos e scanner de primeira pessoa

Cena: `AstroLabUnity/Assets/Treinamentos/CapacetePedro.unity`.
Editor: Unity **6000.6.9f1**, URP e o novo Input System.

## Como usar

1. Entre em Play e aproxime-se de um objeto.
2. Mire no objeto e **mantenha o botão esquerdo do mouse pressionado durante 3 segundos**.
3. Acompanhe a porcentagem no capacete e no aparelho. Um feixe e um som indicam a análise em andamento.
4. Ao completar, a ficha mostra o nome, a descrição e a charada cadastrada, quando houver.
5. Feche a ficha pelo botão ou Esc. Solte o botão esquerdo antes de iniciar outro scan.

Soltar o botão antes do fim, trocar de alvo, perder a mira, pausar ou tirar o foco do jogo zera o progresso. A inspeção instantânea pela tecla E fica desativada na cena com scanner.

## Objetos aceitos

O scanner não usa uma lista de tipos permitidos: qualquer objeto com Collider, dentro do alcance de **18 metros**, pode ser identificado. O próprio jogador, as mãos e os gatilhos de área são ignorados. O objeto mais próximo bloqueia objetos atrás dele.

Na cena própria, foram adicionados Colliders às superfícies visuais do mapa que não tinham um, permitindo mirar também na estrutura e nos demais objetos. São alterações de instância na cena de demonstração; o FBX e os assets originais da Base Lunar foram preservados.

- Se o objeto tiver `AlvoExploracao`, são usados seu título, descrição, categoria, charada e evento `aoInspecionar`.
- Caso contrário, o catálogo fornece informações gerais para categorias reconhecidas pelo nome, como rochas, solo, painéis, baterias, tubulações e portas.
- Para nomes não reconhecidos, o visor identifica o objeto e informa que ainda não existe uma descrição específica cadastrada. Não é feita identificação científica ou histórica automática pela aparência.

Para descrições específicas e novas charadas, adicione `AlvoExploracao` ao objeto ou a um de seus pais na sua própria cena. Objetos criados durante o jogo também funcionam se tiverem Collider. Não é necessário serem `REPAIR_`.

## Organização

- `Assets/Personagem/PrimeiraPessoa/Prefabs/MaosEScanner.prefab`: luvas com dedos articulados, juntas, mangas e aparelho.
- `Assets/Personagem/PrimeiraPessoa/Malhas`: geometria própria, com bordas chanfradas.
- `Assets/Personagem/PrimeiraPessoa/Materiais` e `Shaders`: paleta e iluminação de primeira pessoa.
- `Assets/Personagem/PrimeiraPessoa/Audio`: sons locais de análise e confirmação, sintetizados sem downloads.
- `Assets/UI/Capacete/Scripts/ScannerPrimeiraPessoa.cs`: temporização, entrada, seleção de alvo e apresentação do resultado.
- `CatalogoScanner.cs`: descrições cadastradas e identificação genérica.
- `MovimentoMaos.cs`: respiração, movimento durante a caminhada e pose de análise.

A câmera `CameraMaos` é uma Overlay do URP, na pilha da câmera principal. Usa a camada nativa Ignore Raycast para as mãos e limpa a profundidade; dessa forma o aparelho não atravessa visualmente as paredes e não é selecionado pelo scanner. O mundo usa a câmera original.

O prefab das mãos contém sua apresentação; a cena já conecta câmera, HUD, jogador e controle do scanner. Para levar a outra cena, essas referências e a pilha de câmeras devem ser configuradas da mesma forma.

## Integração

`ScannerPrimeiraPessoa.DuracaoScan` é uma constante de **3 segundos**. O avanço usa `Time.deltaTime` e para com a pausa do jogo. `aoConcluir` entrega o GameObject identificado uma única vez por pressão contínua. Esse evento pode alimentar um diário ou uma missão; ele não realiza reparos nem resolve a charada automaticamente.

O método `AtualizarVarredura(bool pressionado, float deltaSegundos)` permite conectar outro binding de entrada sem duplicar as regras. Desative `lerMouseAutomaticamente` se outro controlador for responsável pela chamada. Na cena entregue, o componente lê `Mouse.current.leftButton.isPressed` diretamente.

Os sons ficam em um AudioSource 2D próprio, com volume 0,35 no Inspector. Nenhum mixer do projeto foi alterado.

## Validação

Testes em Play, usando o mesmo processamento de entrada e alvo do componente:

- **2,99 s:** não conclui; **3,00 s:** conclui e abre a descrição.
- Botão mantido depois do fim: não repete a conclusão.
- Soltar, trocar de objeto, perder a mira e pausar: cancelam o progresso.
- Objeto sem cadastro e objeto de outra categoria: identificados corretamente.
- Som durante a análise e movimento de respiração das mãos verificados.
- Painel solar real: captura em 50% e captura da descrição após a conclusão.
- Textos conferidos sem corte em Full HD; Console com zero erros vermelhos ao fim.

A temporização foi verificada por entrada controlada no componente em Play. O ato físico de manter o botão do mouse pressionado não foi automatizado no Windows.

Revisão visual final, limitada às mãos, aparelho e HUD:

paleta: 4/5 — branco azulado, grafite e ciano consistentes com o visor.
uniformidade: 4/5 — geometria low-poly e instrumentos seguem as referências.
iluminação: 3/5 — shader próprio mantém as luvas legíveis e evita a saturação do primeiro rascunho.
silhueta: 3/5 — mãos com cinco dedos, punhos, articulações e aparelho distinguíveis.
interface: 4/5 — os painéis foram reposicionados para não cobrir as mãos.
feedback: 4/5 — porcentagem, barra, pose, feixe, áudio e ficha confirmam o scan.

Foram consultados os guias `craft-game-floors` de placeholder-assets, character-motion, game-feel, game-audio e visual-scorecard; `ui-ugui`; a documentação local do Input System sobre mouse; e as APIs de Raycast e meshes do Unity 6000.6. Os sistemas de sobrevivência, reparo e progressão de quests ficaram fora desta alteração.

## Regras preservadas

O trabalho usa cena e branch próprias, com arquivos `.meta`. `Lua.unity`, `SampleScene.unity`, modelos, texturas e materiais do modelador não são modificados pela entrega. Nenhum menu de reconstrução da Lua foi executado. Library, Temp, Logs e UserSettings continuam fora do Git. Não foi feito push.
