# LaboratorioBruno

Esta pasta contem um gerador isolado para a sala central 3D do AstroLab. Ele usa apenas primitivas,
materiais e luzes nativos do Unity com URP. Nenhuma cena existente e alterada automaticamente.

## Como gerar a cena

1. Abra o projeto `AstroLabUnity` no Unity 6.6 (`6000.6.9f1`).
2. Aguarde a importacao e a compilacao terminarem.
3. No menu superior, escolha **AstroLab > Bruno > Gerar LaboratorioBruno**.
4. Se houver uma cena com alteracoes nao salvas, o Unity pedira para salva-la primeiro.
5. A nova cena sera aberta e salva em `Assets/BrunoLaboratorio/Scenes/LaboratorioBruno.unity`.
6. Pressione **Play** para ver a sala pela camera criada pelo gerador.

## Controles

- **WASD**: andar em relacao a direcao da camera;
- **Mouse**: orbitar e olhar livremente;
- **Shift esquerdo**: correr;
- **Esc**: liberar o cursor;
- **Clique esquerdo**: capturar novamente o cursor.

O jogador usa `CharacterController` para colidir com o piso, paredes, consoles e equipamentos. A
camera usa Cinemachine Third Person Follow e as acoes `Move`, `Look` e `Sprint` do Input System ja
existente no projeto.

## Base lunar integrada

Para montar a versao exploravel com exterior lunar, escolha **AstroLab > Bruno > Gerar Base Lunar
Integrada**. O gerador preserva a cena original e cria
`Assets/BrunoLaboratorio/Scenes/BaseLunarBruno.unity` com um unico jogador e uma unica camera.

Na cena lunar, **Espaco** pula. A gravidade externa e aproximadamente `1,62 m/s2`; dentro do
laboratorio o controlador mantem a gravidade interna. A passarela ao final do corredor leva ao
terreno procedural de 100 x 100 metros.

Executar o menu novamente pede confirmacao antes de reconstruir os arquivos desta pasta.

## Conteudo gerado

- sala principal de 24 x 18 metros e 5 metros de altura;
- corredor de entrada em escala humana;
- piso modular, paredes, vigas e acabamentos metalicos;
- quatro bancadas de controle e tres racks de servidores;
- janela panoramica com campo de estrelas e planeta estilizado;
- nucleo holografico central, mantendo uma area ampla de circulacao;
- luzes frias azul/ciano, detalhes ambar e Volume URP local da cena.
