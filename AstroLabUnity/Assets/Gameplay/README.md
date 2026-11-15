# Restauração e gestão da Base Lunar

O fluxo está integrado na cena **Assets/Treinamentos/CapacetePedro.unity**, a mesma usada no teste do jogador. A cena agora contém MissionController, alvos de manutenção, o prefab de peças restauradas, integração do scanner, catálogo e painel de recursos. A geometria FBX original não foi alterada.

## Controles

- **Tab:** gestão da base, com estoques, diagnóstico, custos, energia e previsão de produção/consumo do próximo turno.
- **E junto ao equipamento:** inspeção e confirmação do reparo; **R:** reparo direto quando permitido pelo motor da missão.
- **L na eclusa:** entrada/saída; o jogo começa no interior para alinhar o local físico ao estado do traje.
- **F5/F9:** salvar/carregar. A gestão também oferece esses botões.

O estoque permanece visível durante a exploração: peças, tempo, energia em uso/capacidade, água, alimentos e ar do habitat. A tela de gestão pausa o tempo e preserva os estados anteriores dos controles. Reparos pela gestão exigem que o equipamento tenha sido mirado e esteja próximo; consulta remota não libera um reparo à distância. Energia pode ser redistribuída pela gestão central.

Os valores e efeitos são lidos do MissionEngine existente. Um reparo não pode cobrar peças duas vezes. Os painéis solares restauram a usina como um conjunto; reparos visuais passam pelo BaseConsertada existente.

Os reparos agora cobrem as **47 peças modeladas**. `MissionPieceBindings` é a fonte única de vínculo entre peças e sistemas, usada tanto na configuração da cena quanto na restauração em runtime. Portas, cascos, corredores e iluminação são restaurados junto ao sistema correspondente, sem nova cobrança. Comunicações e recarga já iniciam operacionais conforme as regras da missão.

A porta do suporte de vida pode ser inspecionada pelo corredor. Reparar o suporte de vida substitui a porta travada e libera abertura automática por proximidade; modelo e collider deslizam juntos. A porta da eclusa acompanha a restauração da proteção, mantendo a travessia pelo comando L para respeitar o estado de EVA.

## Conteúdo e nuvem

`Resources/base_knowledge.json`: 7 sistemas, 12 descrições de apoio e 5 registros da Apollo 15. O conteúdo explica funcionamento, diagnóstico inicial, manutenção e efeitos na simulação. O saldo e os custos efetivos continuam no motor da missão; o catálogo remoto não substitui regras econômicas.

Cloud Save 3.4.2 foi adicionado pelo Package Manager. CloudCatalogClient inicializa UGS, identifica o convidado e lê o Custom Item `astrolab_catalog_v1`, chave `catalog`, no ambiente `production`. O cliente só lê esse catálogo; escritas de administração ficam fora do jogo. Respostas incompletas são rejeitadas e a cópia local continua disponível.

**Estado da publicação:** catálogo publicado pelo painel oficial da Unity Cloud no ambiente `production`, com gravação confirmada no dashboard e leitura validada dentro do jogo. Nenhuma credencial administrativa foi colocada no jogo.

`Cloud/Publish-Catalog.ps1` permanece disponível para manutenção futura pelo login oficial do UGS. Não acessa tokens internos do Editor. A publicação atual foi feita pela interface autenticada. Não há sincronização de partidas ou ranking novo nesta alteração.

## Apollo 15

Cinco réplicas didáticas, distintas e com collider de interação, foram distribuídas no exterior: martelo, pena, rover, retrorefletor e estágio de descida Falcon. As posições são de gameplay, não coordenadas históricas. A coleção é opcional e não faz parte da condição de vitória da restauração.

## Verificação realizada

- 17 testes EditMode do motor de missão aprovados.
- Play com frames avançando: eclusa, reparos, consumo de peças/tempo, troca dos visuais, rejeição de cobrança dupla, limite de energia, recursos por turno e vitória sem coletar os Easter Eggs.
- Mira no suporte de vida e reparo pelo painel de gestão: 12 → 9 peças, 6 → 4 tempos.
- Catálogo publicado e leitura de dados compartilhados confirmada em Play.
- `Tools/Validation/CheckBaseRepairCoverage.cs`: verifica as 47 peças, interação com a porta, abertura/fechamento, passagem física do personagem, orçamento preservado, vitória e ausência de erros durante os reparos. Executar pelo `unity command script eval --file`.
- Corrigida a configuração das partículas: o sistema é parado antes de ajustar sua duração e os efeitos são agrupados por sistema restaurado.
- Os testes restauraram a missão inicial; nenhum progresso de teste foi salvo na partida do usuário.

Documentação consultada: [Cloud Save Game Data](https://docs.unity.com/en-us/cloud-save/concepts/game-data), [Cloud Save SDK](https://docs.unity.com/en-us/cloud-save/tutorials/unity-sdk), [Authentication](https://docs.unity.com/en-us/authentication/how-authentication-works).

