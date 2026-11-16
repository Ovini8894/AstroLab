# Restauração e gestão da Base Lunar

O fluxo está integrado na cena **Assets/Treinamentos/CapacetePedro.unity**, a mesma usada no teste do jogador. A cena agora contém MissionController, alvos de manutenção, o prefab de peças restauradas, integração do scanner, catálogo e painel de recursos. A geometria FBX original não foi alterada.

## Controles

- **Tab:** gestão da base, com estoques, diagnóstico, custos, energia e previsão de produção/consumo do próximo turno.
- **E junto ao equipamento:** inspeção e confirmação do reparo; **R:** reparo direto quando permitido pelo motor da missão.
- **L na eclusa:** entrada/saída; o jogo começa no interior para alinhar o local físico ao estado do traje.
- **C no interior:** recarga de oxigênio e bateria; também disponível na gestão. Requer suporte de vida energizado e capacidade elétrica de 6, custa uma ação e não cobra quando o traje está cheio. A prioridade temporária da estação preserva a distribuição de energia dos outros sistemas.
- **F4 ou E junto ao console do hub:** observatório de asteroides. Exibe câmera do telescópio, dados NASA e análises; comunicações precisam estar operacionais.
- **H:** alterna a orientação opcional para os cinco artefatos Apollo 15. Oxigênio ou bateria baixos priorizam o retorno à eclusa.
- **F5/F9:** salvar/carregar. A gestão também oferece esses botões.

O estoque permanece visível durante a exploração: peças, tempo, energia em uso/capacidade, água, alimentos e ar do habitat. A tela de gestão pausa o tempo e preserva os estados anteriores dos controles. Reparos pela gestão exigem que o equipamento tenha sido mirado e esteja próximo; consulta remota não libera um reparo à distância. Energia pode ser redistribuída pela gestão central.

Os valores e efeitos são lidos do MissionEngine existente. Um reparo não pode cobrar peças duas vezes. Os painéis solares restauram a usina como um conjunto; reparos visuais passam pelo BaseConsertada existente.

As **47 peças modeladas** continuam vinculadas em `MissionPieceBindings`. Por escolha de gameplay, os 13 conjuntos de arquitetura (paredes, portas, interiores, corredores e vidros) começam íntegros, sem oferecer reparo e sem gastar recursos. A manutenção fica nos equipamentos. Comunicações e recarga já iniciam operacionais conforme as regras da missão.

A porta do suporte de vida começa operacional e abre por proximidade; modelo e collider deslizam juntos. A eclusa mantém o comando L. `BaseInteriorSafety` acompanha dez volumes físicos do habitat: entrar caminhando encerra EVA e sair inicia o consumo do traje. Isso evita bloquear reparos internos quando o jogador já voltou fisicamente à base. Materiais próprios com renderização em duas faces corrigem o desaparecimento de paredes vistas pelo lado interno, sem alterar o FBX ou materiais originais.

O visor ganhou moldura periférica, indicadores identificados e uma próxima ação contextual compartilhada por `MissionGuidance` e pela gestão. O comando R só aparece quando disponível; bloqueios mostram o motivo. Resultados das ações aparecem em uma mensagem temporária. A inspeção de equipamentos usa uma única ficha, evitando duas janelas na mesma tecla E.

## Conteúdo e nuvem

`Resources/base_knowledge.json`: 7 sistemas, 12 descrições de apoio e 5 registros da Apollo 15. O conteúdo explica funcionamento, diagnóstico inicial, manutenção e efeitos na simulação. O saldo e os custos efetivos continuam no motor da missão; o catálogo remoto não substitui regras econômicas.

Cloud Save 3.4.2 foi adicionado pelo Package Manager. CloudCatalogClient inicializa UGS, identifica o convidado e lê o Custom Item `astrolab_catalog_v1`, chave `catalog`, no ambiente `production`. O cliente só lê esse catálogo; escritas de administração ficam fora do jogo. Respostas incompletas são rejeitadas e a cópia local continua disponível.

**Estado da publicação:** catálogo publicado pelo painel oficial da Unity Cloud no ambiente `production`, com gravação confirmada no dashboard e leitura validada dentro do jogo. Nenhuma credencial administrativa foi colocada no jogo.

`Cloud/Publish-Catalog.ps1` permanece disponível para manutenção futura pelo login oficial do UGS. Não acessa tokens internos do Editor. A publicação atual foi feita pela interface autenticada. Não há sincronização de partidas ou ranking novo nesta alteração.

## Apollo 15

Cinco réplicas didáticas, distintas e com collider de interação, foram distribuídas no exterior: martelo, pena, rover, retrorefletor e estágio de descida Falcon. Agora têm detalhes próprios, como rodas, assentos, antenas, superfície da pena, matriz do retrorefletor, pernas e escada do Falcon. São representações estilizadas. As posições são de gameplay, não coordenadas históricas. A coleção é opcional e não faz parte da condição de vitória da restauração.

## Lua esférica — revisão de 15/11/2026

A cena lunar usa um globo navegável de raio 220 m em escala de gameplay. Seis setores de terreno têm colisão física; o movimento, gravidade, pulo, pegadas, orientação do visor e salvamento acompanham a superfície. O platô da base fica nivelado. As cenas planas dos colegas continuam usando o CharacterController original. Não é cartografia científica da Lua.

Os modelos externos de equipamentos foram adaptados à curvatura, mantendo os modelos-fonte. As réplicas Apollo 15 ficam distribuídas a até 285 m da base. O relevo inclui crateras e rochas; o céu artístico se estende aos dois hemisférios. A cobertura de regolito do abrigo foi contida para não atravessar a estufa. Materiais estruturais danificados foram substituídos por versões limpas e removidas faces coincidentes do casco. Armários, bancada de amostras, reservas de O2, telemetria, sensores, maca e sinalização completam o interior sem ocupar as passagens centrais.

O observatório agora está instalado nesta cena, com console, telescópio e câmera. Alertas apresentam alvos não analisados; o acervo local garante conteúdo sem depender da data ou conexão. Dados de tamanho, velocidade e aproximação à Terra vêm do NeoWs. Forma, posição e movimento do modelo no céu são ilustrativos, sem simular a órbita real vista da Lua.

A análise tem duas etapas: palpite de tamanho com faixa NASA oculta e régua inicialmente zerada, seguido de pergunta com três alternativas. Aceita qualquer valor dentro da faixa estimada; seis tipos de questões usam somente os campos disponíveis. Erro dá dica, fechar/reabrir preserva tentativa e reanálise varia o assunto sem duplicar a descoberta. A barra do alvo é uma representação didática aproximada, não um diâmetro medido exato.

A revisão foi limitada à Lua e suas dependências de gameplay. MenuInicial, CentroTreinamento, NaveInterior e a configuração de cenas para build não foram modificados por esta implementação. Não houve nova instalação de pacote ou publicação de dados na nuvem nesta etapa.

## Verificação realizada

- 21 testes EditMode do motor de missão aprovados, incluindo recarga prioritária e referência de mundo no salvamento.
- 13 testes EditMode de asteroides aprovados. `CheckAsteroidAnalysis.cs` valida em Play as duas etapas e restaura a coleção anterior do usuário.
- Play com frames avançando: eclusa, reparos, consumo de peças/tempo, troca dos visuais, rejeição de cobrança dupla, limite de energia, recursos por turno e vitória sem coletar os Easter Eggs.
- Mira no suporte de vida e reparo pelo painel de gestão: 12 → 9 peças, 6 → 4 tempos.
- Catálogo publicado e leitura de dados compartilhados confirmada em Play.
- `Tools/Validation/CheckBaseRepairCoverage.cs`: verifica estrutura íntegra no início, ausência de alvos em paredes, ação compartilhada de reparo, bloqueios explicados, continuidade dos volumes internos, EVA física, porta transitável, orçamento, 47 peças finais, vitória e cinco réplicas detalhadas. Executar pelo `unity command script eval --file` em Play.
- `Tools/Validation/CheckLunarWorld.cs`: verifica seis setores físicos, parede sólida, abrigo separado da estufa, contato com o chão em três hemisférios e ação de pulo, recarga, instruções de catálogo, alerta e câmera do observatório. Os testes invocam as ações de gameplay; a automação de teclas sem foco não substitui a conferência manual dos atalhos no Editor em primeiro plano.
- Corrigida a configuração das partículas: o sistema é parado antes de ajustar sua duração e os efeitos são agrupados por sistema restaurado.
- Os testes restauraram a missão inicial; nenhum progresso de teste foi salvo na partida do usuário.

Documentação consultada: [Cloud Save Game Data](https://docs.unity.com/en-us/cloud-save/concepts/game-data), [Cloud Save SDK](https://docs.unity.com/en-us/cloud-save/tutorials/unity-sdk), [Authentication](https://docs.unity.com/en-us/authentication/how-authentication-works).

