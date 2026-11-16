# Radar de Descobertas

Integração opcional nas cenas lunares `CapacetePedro`, `Lua` e `MissaoNicholas`, ou cenas com `MissionController`. O radar exige comunicações reparadas; as regras atuais já começam com esse equipamento operacional. Use **F4**, ou mire no terminal e pressione **E**. **Esc** fecha e devolve os controles. A missão fica pausada durante a análise.

## Experiência

- Lista paginada: alvos recentes primeiro, depois o acervo histórico; coleção filtra os analisados.
- Comparação com um módulo de referência de 12 m e astronauta de 1,8 m. O módulo é uma régua didática, não uma medição do modelo Blender.
- Análise em duas etapas. Primeiro, compare a barra do alvo com as referências e dê um palpite em metros. A régua começa neutra em zero; é necessário movê-la para confirmar. A faixa NASA fica oculta nessa etapa. A representação usa um valor intermediário didático, não um diâmetro real medido; qualquer palpite dentro da faixa estimada é aceito. Erros indicam se o palpite ficou grande ou pequeno, sem registrar descoberta.
- Depois, interprete os dados em uma pergunta com três alternativas: escala em módulos ou astronautas, largura da incerteza, deslocamento em um minuto, conversão de velocidade ou distâncias médias Terra–Lua. Tipos e posições das alternativas variam por objeto e por nova análise. Dados ausentes excluem perguntas que dependeriam deles. Um erro dá uma dica e permite tentar novamente; só acertar as duas etapas registra a descoberta.
- Ao concluir, a ficha revela diâmetros estimados, velocidade em km/s, distância em km e em distâncias médias Terra–Lua, e data do encontro. Rever um objeto esconde o tamanho novamente e oferece outro tipo de questão.
- Conquistas locais para 1 e 10 IDs diferentes. Revisitas e novas passagens do mesmo objeto não duplicam contagem.
- Alertas espaçados sugerem o próximo alvo não analisado; indicação no céu é explicitamente ilustrativa, sem cálculo de posição ou de visibilidade lunar.

As aproximações da API são **à Terra**, não à Lua ou à base. Não há simulação de órbita, previsão de impacto ou forma real medida do asteroide.

## Acervo e rede

`Resources/asteroid_archive.json` contém dados oficiais NeoWs de 1 a 7 de janeiro de 2025, com mais de dez objetos únicos e sem credenciais. O acervo garante a conquista mesmo sem rede, sem alvos no período ou sem chave configurada. A atualização consulta hoje até seis dias depois, em UTC, no máximo uma vez por minuto por sessão. Sem atualização, a interface preserva o acervo e identifica registros históricos. Campos ausentes não são inventados.

O cliente lê a chave de `ASTROLAB_NASA_API_KEY`, ou do arquivo local `nasa_api_key.txt` em `Application.persistentDataPath`. No Editor, também aceita `UserSettings/NasaApiKey.txt`, ignorado pelo Git. Nunca colocar a chave em scripts, Resources, Inspector ou no repositório. Para distribuição pública, usar um serviço intermediário com cache: uma chave compartilhada dentro do cliente não pode ser mantida secreta. O acervo funciona em builds sem credencial.

Cache: `astrolab_asteroids_cache.json`; progresso: `astrolab_asteroid_progress.json`, ambos em `Application.persistentDataPath`. O progresso é local ao aparelho, sem sincronização de conta ou envio para ranking nesta etapa.

## Verificação

Os testes EditMode verificam: dez análises offline possíveis; repetição do mesmo objeto em datas diferentes e recarga do progresso; dia vazio; prioridade dos dados recentes; campos ausentes; filtro de encontros com a Terra; ordem das etapas; aceitação do intervalo sem medida exata inventada; variação de assuntos e alternativas; conversões científicas; e elegibilidade das questões para todo o acervo. `Tools/Validation/CheckAsteroidAnalysis.cs` exercita as ações reais do terminal em Play e restaura o progresso local do usuário.

Documentação: [NASA Open APIs](https://api.nasa.gov/), [Unity Canvas](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/UICanvas.html), [UnityWebRequest](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/networking/unitywebrequest).
