# Nicholas — Missão Lunar

Leia a documentação detalhada na raiz do repositório: `INTEGRACAO_NICHOLAS.md`.

- O código é isolado em `Assets/Nicholas/`.
- A branch `nicholas` do ZIP não contém o HUD do Pedro: por isso os adaptadores usam reflexão e FPS fallback.
- O gerador de cena segura está em `AstroLab > Nicholas > 1 - Criar cena de missão (cópia segura)`.
- Nada é alterado na `Assets/BaseLunar/` ou nos modelos Blender.
- Gameplay: `MissionEngine` puro C#, integrado por `MissionController` ao Unity.
- Ciência: cinco réplicas fictícias de objetos Apollo 15, com fatos e fontes oficiais NASA.
- Interações em primeira pessoa `E`, modo EVA `L`, painel operacional `F2`, salvar `F5`, carregar `F9`.
- Jogo/sistemas de recursos/estado da missão são abstrações didáticas e podem ser balanceados sem tocar no HUD.
