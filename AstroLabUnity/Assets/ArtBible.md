# AstroLab Art Bible

- **Style:** Clean orbital-laboratory sci-fi: readable forms, cool metal and indigo surfaces, cyan instrument lights, and warm task lighting.
- **Palette:** floor `#2D3545` (gunmetal); walls `#424D62` (cool steel); primary `#B9D2E8` (pale alloy); secondary `#5D738A` (slate blue); props `#20304A` (midnight); shadows `#161D2B` (deep space); accent `#37D5D0` (cyan).
- **Mood:** Soft cool orbital skylight with cyan instrument glow; reserve warm amber light for focal work areas.

Use this palette as the source for future materials, lighting, VFX, and UI. Keep the starter scene as a neutral scaffold; apply the palette to its lighting baseline.

## Base Lunar (cena `Lua.unity`) — estilo cartoon

- **Estilo:** Base lunar de desenho animado: cel shading em 3 faixas com contorno índigo, módulos creme com faixas turquesa e sinalização amarela sobre uma Lua cinza-lilás, sob um céu azul-marinho com estrelas de 4 pontas.
- **Paleta** (fonte única: `Assets/BaseLunar/Scripts/PaletaLua.cs`): módulos `#F4E9D4` (creme) · acabamentos `#3E4C78` (índigo-ardósia) · piso `#7C8CB8` (azul-lavanda) · chão da Lua `#A9A5C0` (cinza-lilás) · faixa `#2CC6C0` (turquesa) · sinalização `#FFC23A` (amarelo-gema) · sombra `#5E5A9E` (índigo, nunca preto) · contorno `#1C1A3D` · destaque/luz `#FF8C3A` (tangerina) · telas `#47E6FF` (ciano) · conserto OK `#59FF86` (verde).
- **Clima:** sol baixo e quente (`#FFF1D6`, 32°) com sombras longas índigo; ambiente em hemisfério (céu `#4A4F8C`, chão `#3A3040`); dentro dos módulos, lâmpadas âmbar e a estufa em verde; a Terra azul brilha no céu.

Implementação: shader `AstroLab/Toon` + contorno `AstroLab/ContornoCartoon` (renderer feature, só ativo em cenas com o componente `IluminacaoCartoon`). Para regerar texturas/materiais/luz: menu **Missão Lunar > Estilo Cartoon**.
