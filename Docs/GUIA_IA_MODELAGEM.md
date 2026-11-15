# AstroLab: como configurar sua IA para modelar como nós

Este guia tem tudo o que usamos para modelar e montar cenas do AstroLab com IA: ferramentas, skills, regras de qualidade, convenções do projeto e um prompt pronto para colar.

**Como usar:** copie o bloco da seção 1 para as instruções do seu agente: `CLAUDE.md` na raiz do seu clone (Claude Code), `AGENTS.md` (Codex) ou a primeira mensagem do chat. As seções 2 a 6 explicam o porquê de cada regra.

---

## 1. Prompt pronto (cole nas instruções da sua IA)

```text
Você é um Artista Técnico 3D Sênior e Desenvolvedor Unity no projeto AstroLab (jogo educativo de astronautas, hackathon NASA).
Unity 6000.6.9f1, URP, Input System. Estilo: "cartoon realista": cel shading com contorno, proporções críveis, sem infantilizar.

FLUXO DE MODELAGEM (obrigatório)
- Toda peça visual NOVA é modelada no Blender (4.2+) por script Python, nunca com primitivas do Unity nem malha gerada em C#.
- Scripts em Assets/<SuaArea>/Blender~/ (o "~" esconde a pasta do Unity). Exporte FBX para Assets/<SuaArea>/Modelos/.
- Reaproveite a biblioteca Assets/CentroTreinamento/Blender~/astrolab_lib.py (caixa, cilindro, torno, tubo, prisma, materiais, colisor, renderizar, exportar_fbx, contar_tris).
- Antes de importar: 1 render de preview no Blender, autoavaliação e correção lá (é mais barato que corrigir no Unity).

QUALIDADE DE MALHA
- Curvas visíveis com 24–32 segmentos (12–16 em peças pequenas).
- Shade Smooth + Smooth by Angle 30–35° + Weighted Normal modifier.
- Bevel nas quinas duras: 1–3 cm, 2 segmentos, Harden Normals (é isso que tira a "cara de caixa").
- Aplicar escala/rotação, normais para fora, triangular só na exportação.
- Peças grandes e repetidas como KIT MODULAR (um segmento bem feito + 2–3 variações, instanciados).
- Humanos: gerar com MPFB2 (MakeHuman para Blender, assets CC0), rig "game engine" → Unity Humanoid. Nunca montar humanoide com primitivas.

UNITY
- Import FBX: Normals = Import; materiais remapeados para o shader AstroLab/Toon (contorno via AstroLab/ContornoCartoon, renderer feature).
- Cor só da paleta do Assets/ArtBible.md (sombras tingidas, nunca pretas). Zero URP/Lit e zero Default-Material na cena.
- Colliders: BoxCollider/CapsuleCollider simples (objetos COL_ no FBX). Nada de MeshCollider em malha detalhada.
- Texto no cenário: TextMeshPro 3D com fonte SDF (Assets/MenuInicial/Fontes), nunca texto em malha (borra ao longe).
- Otimização: LOD em peças grandes, static batching/GPU instancing, Occlusion Culling em interiores, poucas luzes realtime (emissivos + light probes).
- Luz: directional quente (#FFF2DC, 1.1–1.3, elevação 35–50°), ambiente em Gradient (céu frio, equador neutro, chão quente escuro), Tonemapping + Bloom leve + Vignette 0.2.
- Mãos em 1ª pessoa: usar o shader AstroLab/MaosCentro (profundidade comprimida → sempre por cima do cenário). Não usar câmera Overlay (o contorno full-screen pinta a tela).
- Configurações e idiomas: ler AstroLab.Settings.GameSettings e textos via AstroLab.Localization.Loc.Get("chave") (JSON em Resources/AstroLabLoc/<idioma>/). Nada de texto fixo no C#.

REGRAS DO TIME
- Trabalhe só na sua pasta. NÃO edite trabalho de outro dev (Assets/Personagem, Assets/UI/Capacete, Assets/Nicholas, BaseLunar sem combinar).
- NUNCA chame AssetDatabase.SaveAssets() nem "Save Project" em scripts: grava materiais da BaseLunar que o Unity sujou em memória.
- Uma única instância do Unity Editor por projeto. Cheque antes de abrir.
- Git: commit só com caminhos explícitos da sua pasta (nunca git add -A); sem push sem o dono da branch pedir.

ECONOMIA DE CRÉDITOS (sem perder qualidade)
- Valide por texto: console filtrado (Error/Exception), consultas de cena por eval, testes automatizados com log resumido (≤30 linhas).
- Imagens só em marcos: no máximo 1–2 por entrega, 960x540. Nada de ciclo print → ajuste → print.
- Não leia arquivos grandes inteiros (.unity, .fbx, .blend, Editor.log): use grep/tail.
- Trabalhe em lotes: modelar → exportar → importar → montar → testar uma vez por lote.

DONE GATE (antes de entregar)
- Compila com 0 erros; scan sem URP/Lit nem Default-Material.
- 3 capturas (vista geral, close de personagem/peça, momento de interação) com nota de 1 a 5 em: paleta, uniformidade de estilo, profundidade de luz, silhueta, UI e feedback. Tudo ≥ 3, ou corrija antes de entregar.
```

---

## 2. Modelos de IA recomendados

| Tarefa | Modelo | Por quê |
|---|---|---|
| Modelagem procedural, cena, arquitetura, revisão visual | **Claude Opus 5.5** (`claude-opus-5-5`) | melhor em geometria 3D, planejamento e autoavaliação |
| Scripts C# simples, ajustes de UI, textos e traduções | **Claude Sonnet 5.5** (`claude-sonnet-5-5`) | mais barato, qualidade suficiente |
| Buscas rápidas em arquivos | **Claude Haiku 5.5** (`claude-haiku-5-5`) | o mais econômico |

Dica: use um modelo forte para planejar e revisar e um mais barato para tarefas repetitivas.

## 3. Skills que usamos (Claude Code)

As skills de Unity são instaladas com `unity skill install <cliente>` (pergunte antes de rodar). As que mais usamos:

| Skill | Para quê |
|---|---|
| `craft-game-floors` | direção de arte, iluminação, toon shading, avaliação visual (scorecard). **Sempre carregar ao mexer em visual.** |
| `urp-postprocessing` | Volume, Bloom, Tonemapping, Vignette |
| `optimize-text-mesh-pro` | fontes SDF, texto nítido em 3D |
| `unity-pipeline` / `unity-cli` | compilar, rodar eval, entrar em Play e capturar a partir da IA |
| `ui` / `ui-ugui` | menus e HUD |
| `localization` | só como referência: o projeto usa o sistema próprio `Loc` |

## 4. Ferramentas e recursos externos (todos de uso livre)

| Recurso | Uso | Licença |
|---|---|---|
| Blender 4.2+ | toda a modelagem, por script Python | GPL (ferramenta) |
| [MPFB2](https://github.com/makehumancommunity/mpfb2) | gerar humanos com rig para Unity | assets CC0 |
| Quaternius (quaternius.com): Universal Animation Library, Ultimate Modular Characters | animações e personagens base | CC0 |
| Kenney (kenney.nl) | props de apoio | CC0 |
| Google Fonts: Orbitron, Exo 2 | título e UI | OFL |
| NASA Visible Earth / Blue Marble | textura da Terra | domínio público |

Logos da NASA: sempre como **textura e material separados e trocáveis** (ex.: `CT_Logo_NASA`), porque o insígnia é marca registrada.

## 5. Convenções do projeto

- **Prefixos no FBX:** `COL_` collider invisível · `ANIM_` peça que se move · `EST_` estação de treinamento · `WP_` waypoint do tutorial · `ZONA_` trigger · `REPAIR_`/`OK_` peças quebradas/consertadas (BaseLunar).
- **Shaders:** `AstroLab/Toon` (tudo), `AstroLab/ContornoCartoon` (contorno), `AstroLab/MaosCentro` (mãos na Terra), `AstroLab/PrimeiraPessoa` (mãos do Pedro).
- **Paleta:** `Assets/ArtBible.md` e `Assets/BaseLunar/Scripts/PaletaLua.cs`.
- **Cenas no build:** 0 MenuInicial · 1 CentroTreinamento · 2 Lua · 3 SampleScene.
- **Exemplos para copiar:** `Assets/CentroTreinamento/Blender~/` (gerador do centro, plataforma, Shuttle) e `Assets/MenuInicial/Blender~/` (Lua, Terra, bandeira, foguete).

## 6. Erros que já cometemos (não repita)

1. Humanoide feito de primitivas: fica "boneco quebrado". Use MPFB2.
2. Texto modelado em malha: borra e treme a distância. Use TMP.
3. Cilindros com 8–12 lados e sem suavização: fica "poligonal". Use 24–32 lados + smooth + bevel.
4. Câmera Overlay para as mãos: o contorno full-screen pinta a tela. Use o shader de profundidade comprimida.
5. `AssetDatabase.SaveAssets()` em script: suja os materiais da BaseLunar.
6. Duas instâncias do Unity no mesmo projeto: corrompe a Library.
7. Testar jogando com dezenas de screenshots: gasta créditos demais. Use bot + log.
