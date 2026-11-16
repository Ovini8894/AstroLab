# Inspeção do modelo final

Inspeção estática em 15/11/2026. Este documento registra evidências de origem; não substitui a medição dos `Renderer.bounds` após a importação no Unity.

- Modelo: `Assets/CentroTreinamento/Modelos/OnibusEspacial.fbx`, GUID `614bef39780f3af439767de6f4943616`.
- Commit de introdução: `7df2af459d75b497dbd7fe63edcb0f3bb44b606e`, "feat: modelo 3D externo do Space Shuttle (OnibusEspacial.fbx)". Integrado na branch `nicholas` pelo merge `af5a7f9`.
- SHA256: `0516560B97B4E3FAA6663ACE7889F16112620316EF4C101162F90DDB84E61977`. A cópia `AgentScripts/staging/Modelos/OnibusEspacial.fbx` é idêntica, portanto não representa outra versão final.
- O antigo `Assets/MenuInicial/Modelos/Foguete.fbx` não é o modelo recém-integrado.
- Importador original: escala global 1; unidades do arquivo habilitadas; materiais remapeados; animação desabilitada; sem colisores automáticos. Não alterar esse importador.

## Hierarquia e geometria

FBX binário versão 7400. A raiz reúne doze malhas e três empties. Há três níveis separados para cada conjunto exterior: `Onibus_Orbitador`, `Onibus_TanqueExterno`, `Onibus_Boosters`, com variantes `_L1` e `_L2`; não se devem renderizar simultaneamente os três níveis. Também existem `Onibus_Orbitador_Transp`, `Onibus_Interior`, `Onibus_Interior_Transp`, `NAVE_ANCORA_INTERIOR`, `SEAT_CDR_Olho` e `SEAT_PLT_Olho`.

| Malha principal | Vértices FBX | Extensão nos eixos de origem Blender, metros |
|---|---:|---|
| Orbitador | 15.257 | 23,90 × 17,95 × 40,20 |
| Boosters | 9.250 | 18,30 × 5,20 × 45,50 |
| Tanque externo | 4.426 | 8,72 × 10,16 × 47,90 |
| Interior | 20.752 | 4,95 × 5,51 × 7,40 |

O conjunto representa um **Space Shuttle completo em configuração de lançamento**, com tanque externo e dois boosters. O nariz aponta para cima na orientação de exportação. Não é um módulo lunar. A fonte geradora é `Assets/CentroTreinamento/Blender~/gerar_plataforma_lancamento.py`. A exportação desloca a origem para `(44,290,13.5)` no mapa de origem: o centro do tanque externo sobre a plataforma de lançamento. Esse pivô não representa contato com o terreno.

Há 26 materiais remapeados no `.meta`, incluindo revestimento `OR_Branco`, proteção térmica `OR_Preto`/`OR_Cinza_RCC`, `OR_Motor` e `OR_Motor_Interno`; os materiais externos são reutilizados por referência. O FBX não contém objetos `Texture`/`Video`, portanto detalhes como o logo dependem dos materiais externos existentes (ex.: `CT_Logo_NASA`). Não há `AnimationStack` no arquivo nem clips habilitados no importador.

## Bocais e pontos de RCS

A função `orbitador()` documenta a geometria real. Seu referencial local usa X para o nariz, Y para bombordo e Z para o dorso. A transformação para o espaço Blender exportado é:

```text
O(x,y,z) = (44-y, 284.3-z, 19.5+x)
```

Após conversão FBX para Y vertical e metros, o referencial esperado da raiz é `(y, 6+x, 5.7+z)` com a conversão de handedness do Unity. Confirmar os sinais com a instância importada antes de posicionar emissores.

Os três motores SSME (linhas 529–534) têm bocas em `(-3,0,4.15)` e `(-3,±1.35,2.05)` no referencial local do orbitador. No referencial métrico esperado da raiz: `(0,3,9.85)` e `(±1.35,3,7.75)`. O jato aponta para `-Y` da raiz exportada. O diâmetro das bocas é aproximadamente 2,3 m. Os dois bocais OMS, junto aos casulos traseiros, também apontam para a cauda; não são motores ventrais.

O nariz inclui oito marcas de RCS (linhas 472–476): estações X=35 e 35,4, ângulos de casco 20°,160°,60°,120°, raio geométrico 0,08 m. São pequenas esferas escuras incorporadas à malha, sem transforms próprios e sem bocal mecânico detalhado. Os sockets devem ser derivados da função `casco`/normais locais e ficar acima da superfície; não no centro da nave. Não há conjunto traseiro detalhado equivalente. Sockets de manobra adicionais precisam ser identificados como adaptação da cinemática.

## Limitação de pouso

Não existem pernas, sapatas ou trem de pouso no FBX nem na função geradora. Os "suportes do orbitador" da fonte (linhas 558–561) são conexões estruturais com o tanque externo, não pernas para o solo. Os pontos mais baixos são bocais/flape ou bocais dos boosters. Assim, encostar a malha original ao chão apoiaria a nave nos motores. Uma integração vertical necessita hardware adicional separado e documentado, ou deve registrar explicitamente a ausência de apoio físico. A configuração real do Shuttle não oferece pouso lunar vertical nem representa uma solução de engenharia validada.

## Reprodução da inspeção

```powershell
node Assets/PousoLunar/Documentacao/inspecionar-fbx.cjs Assets/CentroTreinamento/Modelos/OnibusEspacial.fbx Assets/PousoLunar/Documentacao/inspecao-fbx.json
```

O leitor binário usa apenas `fs` e `zlib` do Node; não altera o FBX. `inspecao-fbx.json` contém bounds brutos, transforms FBX, materiais e conexões para auditoria. Nenhum Unity Editor foi iniciado durante esta investigação.
