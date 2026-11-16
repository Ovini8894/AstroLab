# Registro de validação

Data: 15/11/2026. Branch: `nicholas`.

## Ajuste do destino escolhido — validação na instância aberta do Unity

A cena e o prefab existentes foram atualizados com Editor APIs, sem regenerar objetos. As exclusões prévias dos três arquivos de `Assets/Resources/GDKEditionAutoGen*` foram preservadas. Os scripts de runtime, estados, transição e câmera não foram alterados neste ajuste.

| Verificação atual | Resultado |
|---|---|
| Contato mundial | PASS: `(24.200000, -0.071623, 20.300000)`, rotação zero. |
| Altura do terreno | PASS: interseção com os triângulos reais, sem offset. |
| Compatibilidade espacial | PASS: mesma malha/TRS e altura entre demonstração, `MissaoNicholas` e `Lua` (esta última consultada em PreviewScene, sem salvar). |
| Waypoints/câmeras | PASS: translação mundial conjunta; descida vertical alinhada; plano final orientado com a base ao fundo. |
| Execução analítica no Editor | PASS: 605 amostras de 0,05 s; chegada exata e conclusão única. |
| Terreno ao longo do trajeto | PASS nas amostras de superfície a cada 1 s. |
| Prédios e painéis | PASS na checagem conservadora de AABBs do preview contra 2.561 renderers do ambiente carregado; nenhuma sobreposição com estruturas. |
| Rocha próxima | AVISO: AABB de `Rocha_2123` cruza o volume conservador do preview próximo ao contato. Não equivale a prova de interseção de malhas. |
| Offsets nos pais | PASS: destino mundial mantido após deslocar o pai na cópia de teste. |
| Independência do modelo/efeitos | PASS: execução até Completed com socket visual vazio e efeitos ausentes. |
| Harness original | PASS: estados em ordem, eventos únicos, deltas longos, determinismo de posição/rotação/câmera, reset/reinício e referências/materiais. |
| Compilação local Roslyn | PASS: runtime e Editor separados, sem erros/avisos. |
| Play Mode real, cena isolada | PASS: `sawCompleted=True`, `errors=0`, fim na posição configurada. Harness encerrou o Play Mode normalmente. |
| Inspeção visual | Captura da câmera final com a base ao fundo em `preview-ponto-pouso.png`; usa o preview existente, não o FBX final. |
| Segurança de cena | `MissaoNicholas` e `PousoLunar` restauradas abertas aditivamente, sem alterações não salvas; marcador preservado. Gameplay e spawn não foram salvos/modificados. |

O maior deslocamento entre amostras de 0,05 s foi de aproximadamente 0,3495 m no foguete e 2,2340 m na câmera. Os testes exercitaram o controlador real do projeto, não uma reimplementação matemática externa.

A rocha tem centro aproximado `(24.76559, -0.08557, 21.06863)` e tamanho `(0.33715, 0.13308, 0.28842)`. O topo fica cerca de 5 cm acima do contato central. O ponto escolhido foi mantido: não foi alterada a base, removida a rocha ou adicionado offset artificial. Conferir apoio e geometria com o modelo final permanece necessário. AABB é conservador e o teste de terreno é amostral; não constituem certificação de colisão para qualquer FBX futuro.

Na captura com as duas cenas carregadas, o renderer do terreno e o sol **da demonstração** foram desativados temporariamente para evitar duplicação com os equivalentes do ambiente. O overlay foi excluído da captura. Tudo foi restaurado imediatamente, sem salvar essas alterações temporárias. Para jogar a demonstração sozinha, terreno/sol próprios continuam habilitados. O ambiente da base não foi copiado para o módulo.

Não foram retestados os fades entre cenas, áudio/VFX definitivos ou o FBX final, pois não fazem parte da mudança de destino. O teste em Play Mode usou somente PousoLunar; os testes espaciais e a captura usaram o ambiente carregado aditivamente. O setup anterior de cenas foi restaurado ao final.

Repetição: **AstroLab > Pouso Lunar > Validar destino e ambiente carregado**, com a demonstração e `MissaoNicholas` carregadas, em Edit Mode. A validação cria e fecha uma cena temporária, sem salvar gameplay.

## Histórico da implementação inicial

A tabela abaixo registra o bloqueio de licença da etapa original. As verificações de trajetória, cena/prefab e Play Mode foram posteriormente realizadas no ajuste descrito acima.

| Verificação | Resultado |
|---|---|
| Branch e alterações prévias | `nicholas`, sem alterações prévias. |
| Compilação runtime por Roslyn | PASS, Unity 6000.6.9f1, sem erros/avisos. |
| Compilação Editor por Roslyn | PASS, Unity 6000.6.9f1, sem erros/avisos. |
| Execução do Editor, sandbox e fora dele | BLOQUEADA: licença não disponível; saída 198. |
| Geração de `.unity` e `.prefab` por Editor APIs | PENDENTE; gerador implementado, não executado. |
| Harness Edit Mode | PENDENTE; implementado, não executado. |
| Harness Play Mode | PENDENTE; implementado, não executado. |
| Eventos, movimento/câmera, ausência de visual/efeitos | PENDENTE de execução; casos incluídos no harness. |
| Fade de entrada e saída para Lua | PENDENTE de Play Mode e inclusão da cena no build. |
| Visual, áudio e partículas finais | PENDENTE dos assets e inspeção no Editor. |

Mensagem do Unity nas tentativas de execução:

```text
No valid Unity Editor license found. Please activate your license.
Application will exit with return code 198
```

`validar-compilacao.ps1` usa compilador e referências reais do Unity, sem stubs. Isso verifica compilação C# e disponibilidade das APIs referenciadas, mas não comprova execução, serialização, transições ou renderização.

Nenhum commit, push, troca de branch ou instalação de pacote Unity foi realizado. A única instalação externa é a skill autorizada `craft-game-floors`, em `C:/Users/nicho/.codex/skills/`, da fonte oficial Unity-Technologies/skills. O Unity CLI e Python não estavam disponíveis; o download foi feito por clone Git esparso, seguido de cópia do diretório da skill.
