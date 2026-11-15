# Base Consertada

Versão **consertada** da Base Lunar para quem programa as missões de conserto.
Cada peça consertada é um objeto `OK_*` que fica **exatamente** no lugar da peça quebrada da `BaseLunar`.
Consertar uma peça = esconder a quebrada e mostrar a `OK_`. O componente `BaseConsertada` faz isso por você.

## Como usar na sua cena

1. Sua cena precisa ter a `BaseLunar` (o objeto com esse nome).
2. Arraste `Prefabs/BaseConsertada.prefab` para a cena. Ele se alinha sozinho com a `BaseLunar` e começa com tudo escondido.
3. No seu script:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class ConsertarComTecla : MonoBehaviour
{
    public float alcance = 3f;
    BaseConsertada baseConsertada;

    void Start() => baseConsertada = FindAnyObjectByType<BaseConsertada>();

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        var cam = Camera.main.transform;
        if (Physics.Raycast(cam.position, cam.forward, out var hit, alcance)
            && baseConsertada.PodeConsertar(hit.collider.name))
        {
            baseConsertada.Consertar(hit.collider.name);   // ex.: "REPAIR_Gerador_O2"
        }
    }
}
```

## API (`Scripts/BaseConsertada.cs`)

| Método | O que faz |
|---|---|
| `Consertar("REPAIR_Gerador_O2")` | Conserta. Aceita o id (`"Gerador_O2"`), `"OK_Gerador_O2"` ou o nome do objeto quebrado. |
| `PodeConsertar(nome)` | `true` se é uma peça consertável que ainda está quebrada. |
| `EstaConsertada(nome)` | `true` se já foi consertada. |
| `Quebrar(nome)` | Volta a peça para quebrada (para reiniciar a missão). |
| `ConsertarCategoria(PecasDaBase.Categoria.Energia)` | Conserta todas as peças de uma categoria. |
| `Progresso(categoria)` | `(consertadas, total)` de uma categoria, para UI/missão. |
| `ConsertarTudo()` / `BaseToda` | Conserta tudo / `true` se a base inteira está consertada. |
| `LimparDetritos()` | Esconde os `Detrito_*` espalhados. |
| `ObjetoConsertado(nome)` | O `GameObject` `OK_*` (para partículas, som, animação). |
| `aoConsertar` / `aoQuebrar` | Eventos (Inspector ou código) com o id da peça. |
| `comecarTudoConsertado` | Caixa no Inspector para testar com a base já consertada. |

## Peças (`Scripts/PecasDaBase.cs`)

| Categoria | Peças | O que muda |
|---|---|---|
| Energia | 23 `Painel_Rx_Cy`, `Inversor_1..3`, `Baterias` | painel intacto no lugar do caído/rachado/suporte vazio; portinhola fechada; luz de status verde |
| SuporteVida | `Gerador_O2`, `Cano_Tanque_O2`, `Reciclador_H2O` | portinhola fechada; cano ligado do tanque ao gerador; vidro inteiro com água limpa |
| Comunicacao | `Antena_Prato`, `Console_Controle` | prato inteiro apontado para a Terra (esconde também `Antena_Pedaco_Caido`); telas ligadas |
| Estrutura | `Brecha_Casco`, `Hub_Torre_Vidros`, `SuporteVida_Casco` | remendo por fora e por dentro do Hub; vidros inteiros, sem ferrugem |
| Estufa | `Estufa_Painel`, `Estufa_Vidros`, `Interior_Estufa` | tela ligada, vidros inteiros, plantas vivas |
| Iluminacao | `Corredor_*`, `Interior_Eclusa/Hub/SuporteVida`, `Hub_Mastros_Farol` | luzes acesas, farol ligado, sem fios soltos na brecha |
| Portas | `Porta_Eclusa_Externa`, `Porta_SuporteVida` | vidro inteiro, trava verde (abrir a porta é com o seu script) |
| Suprimentos | `Capsula_Suprimentos` | cápsula em pé |

Obs.: as luzes de verdade da cena (`Luz_Hub_Emergencia` etc.) continuam laranja. Se quiser mudar a cor ao consertar, use o evento `aoConsertar`.

## Regras

- **Não edite** nada em `Assets/BaseLunar/` (é do modelador). Esta pasta só *referencia* os materiais de lá.
- Novos materiais desta pasta: `M_Luz_Verde`, `M_Luz_Branca`, `M_Agua`, `M_Planta_Viva`.
- **Não rode** "Missão Lunar > Montar cena".

## Regerar depois que o modelador mudar a base (Blender)

O script `Blender~/gerar_base_consertada.py` monta tudo a partir do `.blend` da base. A pasta termina com `~`, então o Unity ignora a pasta.

1. Copie `Assets/BaseLunar/Blender/BaseLunar.blend.bak` para uma pasta temporária com extensão `.blend`. **Nunca rode o script no original.**
2. Na pasta `AstroLabUnity`, rode:
   ```
   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b <copia>.blend --python "Assets/BaseConsertada/Blender~/gerar_base_consertada.py" -- "Assets/BaseConsertada/Modelos/BaseConsertada.fbx" "Assets/BaseConsertada/Blender~/BaseConsertada.blend"
   ```
3. No Unity: **AstroLab > Base Consertada > Atualizar materiais e prefab**.
4. Se uma peça nova `REPAIR_*` foi criada, adicione-a em `PecasDaBase.cs` e no script do Blender.
