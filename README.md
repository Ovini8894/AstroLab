# AstroLab

Projeto Unity 3D com Universal Render Pipeline (URP), localizado em `AstroLabUnity/`.

- **Editor:** Unity 6000.6.9f1
- **Cena inicial:** `Assets/Scenes/SampleScene.unity`
- **Pacotes principais:** URP, Input System, uGUI e Cinemachine
- **Direção visual:** [AstroLab Art Bible](AstroLabUnity/Assets/ArtBible.md)

Abra `AstroLabUnity/` pelo Unity Hub. A cena é um scaffold vazio, pronto para receber o conceito e a gameplay; nenhum loop de jogo foi inventado nesta etapa.

## Base Lunar (mapa da Lua)

Mapa da base lunar abandonada que o jogador precisa restaurar, modelado no Blender (1 unidade = 1 metro, jogador de 1,80 m).

- **Cena:** `Assets/BaseLunar/Cenas/Lua.unity` (aperte Play: WASD anda, mouse olha, Shift corre, Espaço pula com a gravidade da Lua)
- **Modelo:** `Assets/BaseLunar/Modelos/BaseLunar.fbx`, com texturas em `Texturas/` e materiais URP em `Materiais/`
- **Objetos de gameplay:** tudo que o jogador conserta começa com `REPAIR_` (painéis solares, gerador de O₂, reciclador de H₂O, baterias, antena, console...). As portas são `Porta_*`. Os marcadores são `SPAWN_Player`, `WP_*` (exterior) e `INT_*` (salas)
- **Menu Missão Lunar** (barra de menus do Unity): recria os materiais e a cena depois que o FBX for reexportado do Blender. Ele recria `Lua.unity` do zero, então coloque gameplay numa cena separada ou use só "1. Criar materiais URP"

Regras do time para a Base Lunar:

- A forma do mapa só muda no Blender. Exporte por cima do mesmo `BaseLunar.fbx`.
- Não renomeie no Blender objetos que já têm script no Unity.
- Cada pessoa trabalha na sua própria cena para evitar conflito.