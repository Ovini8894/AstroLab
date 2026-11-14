# Interface do capacete — AstroLab

Primeira versão em uGUI, baseada nas três referências visuais fornecidas: moldura do visor em ciano, instrumentos no topo, campo central livre e informações contextuais nas bordas.

## Abrir e testar

1. Abra `AstroLabUnity` no Unity **6000.6.9f1**.
2. Abra `Assets/Treinamentos/CapacetePedro.unity` e entre em Play.
3. Caminhe com WASD, olhe com o mouse e aproxime-se de um objeto reparável.
4. Mire a até 4 metros de um objeto preparado para exploração. O visor mostra seu nome, a distância e **E — INSPECIONAR**.
5. Pressione E para abrir sua descrição e charada. Feche com o botão ou Esc.

A cena de demonstração é uma cópia própria do mapa com os objetos originais referenciados. `Lua.unity`, `SampleScene.unity`, o FBX, as texturas e os materiais originais não fazem parte da implementação do HUD.

## Componentes

- `Assets/UI/Capacete/Prefabs/HudCapacete.prefab`: visor reutilizável, com CanvasScaler em 1920 × 1080, match 0,5 e elementos ancorados.
- `HudCapacete`: apresentação dos recursos, missão, detecção dos alvos e ficha do objeto.
- `AlvoExploracao`: título, descrição, charada, categoria e evento `aoInspecionar`, editáveis no Inspector.
- `GraficoCapacete`: molduras e ícones desenhados como geometria de UI, sem imagens de referência embutidas.
- `TemaCapacete`: cores derivadas de `Assets/ArtBible.md`; ciano e azul escuro para instrumentos, âmbar para alertas e pistas.

Os recursos do TextMesh Pro usados pelo prefab ficam em `Assets/TextMesh Pro/`. São recursos do pacote uGUI já presente no projeto.

## Conectar a gameplay

O HUD não simula consumo de oxigênio, carga de bateria, reparos ou conclusão automática de quests. Os valores 81% / 64%, a missão e as charadas da cena são exemplos de apresentação e podem ser substituídos pelo time.

```csharp
using AstroLab.Capacete;

// Referência ao componente HudCapacete no seu controlador.
visor.DefinirOxigenio(oxigenioAtual); // percentual: 0 a 100
visor.DefinirEnergia(energiaAtual);   // percentual: 0 a 100
visor.DefinirMissao("Restaurar a base", "Inspecione o suporte de vida.", 1, 3);

// Também pode abrir uma ficha a partir de outro sistema de exploração.
visor.MostrarDescoberta(titulo, descricao, charada, categoria);
visor.FecharDescoberta();
```

Ao atingir 20% ou menos, cada recurso muda para âmbar. Oxigênio tem prioridade na mensagem quando os dois estão baixos. O alerta inclui texto, além da mudança de cor.

Para usar em outra cena, instancie o prefab e atribua `Camera Jogador`, `Controle Movimento` e `Base Referencia`. A referência de controle deve ser o Behaviour de movimento do jogador; ele é temporariamente desativado durante a ficha e restaurado ao fechar. Deixe exatamente um EventSystem com InputSystemUIInputModule na cena.

Adicione `AlvoExploracao` a uma instância do objeto com Collider na sua própria cena. Preencha os dados e use `aoInspecionar` para conectar progresso de missão ou registro de descobertas. O evento informa a inspeção; ele não realiza consertos nem considera uma charada resolvida.

## Validação desta versão

- Play com frames avançando e zero erros vermelhos ao final.
- Limites de oxigênio em 0/100, alertas de oxigênio e energia, segmentos da bateria e atualização da missão verificados.
- Abertura da ficha, texto da charada, botão de fechar e restauração do cursor/controle verificados.
- Mira sobre um painel solar real: nome, distância e prompt de inspeção verificados.
- Layout em 1920 × 1080 e 1280 × 960: painéis dentro da tela, sem sobreposição e sem textos cortados.
- Cenas e arquivos do modelador preservados.

A tecla E enviada pela automação de desktop não chegou ao fluxo de entrada do jogo, mesmo com a Game View focada e o teclado habilitado. O atalho foi implementado com `Keyboard.current.eKey.wasPressedThisFrame`; a abertura foi validada pela chamada do mesmo método usado pelo atalho. A conferência final com uma tecla física fica para o jogador. Não foi alterada a configuração do Input System para contornar essa limitação.

Avaliação visual, limitada ao HUD: paleta **4/5** (cores da direção de arte), intenção da interface **4/5** (hierarquia, bordas e alinhamento consistentes) e feedback **3/5** (mira contextual, ficha e alertas). Iluminação, silhueta e arte do mapa ficaram fora do escopo. O ajuste após a primeira captura corrigiu os gráficos invisíveis; a segunda revisão corrigiu o título cortado.

As referências de implementação consultadas foram `ui-ugui`, `craft-game-floors/references/art-direction.md`, `game-shell.md` (apenas recursos de texto e apresentação) e `visual-scorecard.md` (escopo do HUD), além da documentação local do Pipeline para scripts, captura e ciclo do Editor.

## Regras do time

Trabalhe na sua própria cena e branch. Preserve os arquivos `.meta`. Não versione Library, Temp, Logs ou UserSettings. Não edite as pastas do modelador e não execute o menu que reconstrói a cena da Lua. Envie alterações por Pull Request, sem push direto na main.
