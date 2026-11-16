# Checagem estática de `Rocha_2123`

Inspeção de 15/11/2026, feita sem executar o Unity. Nenhum asset do ambiente foi modificado.

A leitura binária independente de `Assets/BaseLunar/Modelos/BaseLunar.fbx` encontrou `Rocha_2123` (objeto FBX `285954669`) ligado à malha compartilhada `Rocha_Malha_0` (`145221440`, 51 vértices). O TRS foi reconstruído em metros e convertido para o referencial Unity com a inversão de handedness no eixo X. A raiz do modelo na cena `Assets/BaseLunar/Cenas/Lua.unity` possui posição zero e rotação identidade. O resultado coincide com a medição registrada anteriormente em `VALIDACAO.md`.

| Volume mundial | Mínimo XYZ (m) | Máximo XYZ (m) |
|---|---|---|
| AABB conservador equivalente a `Renderer.bounds` | `(24.59701755, -0.15211652, 20.92442696)` | `(24.93417179, -0.01903272, 21.21284310)` |
| AABB dos vértices transformados da rocha | `(24.60667138, -0.15211637, 20.93717794)` | `(24.92652532, -0.01903302, 21.20004629)` |
| Volume global do orbitador normalizado para altura 8 m | `(21.82189, -0.071624, 18.51393)` | `(26.57811, 7.928376, 22.08607)` |

O volume conservador da rocha tem centro `(24.76559467, -0.08557462, 21.06863503)` e tamanho `(0.33715423, 0.13308380, 0.28841614)`. Seu topo está **0,05259128 m acima** do ponto de contato central `(24.2,-0.071624,20.3)`.

O volume global do orbitador considera largura `4,75622 m`, altura `8 m`, profundidade `3,57214 m`, centro XZ no destino e limite inferior exatamente no Y do destino. **Os AABBs se intersectam:** a caixa de interseção mede aproximadamente `(0.33715423, 0.05259128, 0.28841614) m`. Isso identifica a região que precisa de inspeção, mas não prova interseção entre triângulos. A caixa global inclui espaços vazios entre asas, fuselagem e bocais.

Não há pernas/sapatas no modelo original, portanto não é possível afirmar que "os apoios não intersectam a rocha". Bocais não constituem apoios estruturais. Essa limitação deve permanecer visível na documentação até existir geometria apropriada. Ao criar hardware separado de pouso, verificar principalmente o retângulo relativo ao pivô: X entre `+0.397018` e `+0.734172 m`, Z entre `+0.624427` e `+0.912843 m`, até Y `+0.052592 m`. Se uma sapata ocupar essa região, ajustar a disposição do hardware dentro de `RocketVisualSocket`, após conferir estabilidade e folga; não mover o destino nem a rocha silenciosamente.

## Reprodução

```powershell
node Assets/PousoLunar/Documentacao/inspecionar-fbx.cjs Assets/BaseLunar/Modelos/BaseLunar.fbx Assets/PousoLunar/Documentacao/inspecao-rocha.json Rocha_2123
```

O JSON conserva os vértices e o TRS necessários para reproduzir a conta. Para o AABB equivalente a `Renderer.bounds`, transformar os oito cantos do AABB local, usando escala FBX, rotações Euler XYZ (matriz `Rz * Ry * Rx`) e translação; converter centímetros para metros e inverter X para o referencial Unity. Para o AABB exato dos vértices, aplicar a mesma transformação aos 51 vértices. Esta checagem não substitui uma inspeção em Play Mode, teste de triângulos ou certificação de engenharia.
