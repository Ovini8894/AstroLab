using AstroLab.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AstroLab.Nave
{
    // Mira central: acha o controle que o jogador esta olhando (raycast + cone de tolerancia), realca,
    // mostra o nome traduzido e aciona com o clique. A mao direita "aperta" junto.
    public class MiraNave : MonoBehaviour
    {
        public float alcance = 1.4f;
        public float toleranciaGraus = 2.5f;
        public Graphic mira;
        public TMP_Text rotulo;
        public Transform maoDireita;

        ControleNave atual;
        Vector3 maoPos0;
        float toque = 1f;

        void Start()
        {
            if (maoDireita) maoPos0 = maoDireita.localPosition;
            Loc.OnLanguageChanged += Texto;
        }

        void OnDestroy() => Loc.OnLanguageChanged -= Texto;

        void Update()
        {
            var o = transform.position;
            var f = transform.forward;
            ControleNave alvo = null;
            if (Physics.Raycast(o, f, out var hit, alcance, ~0, QueryTriggerInteraction.Collide))
                alvo = hit.collider.GetComponentInParent<ControleNave>();
            if (!alvo)
            {
                float melhor = Mathf.Cos(toleranciaGraus * Mathf.Deg2Rad);
                foreach (var c in ControleNave.Todos)
                {
                    var d = c.transform.position - o;
                    if (d.magnitude > alcance) continue;
                    float dot = Vector3.Dot(f, d.normalized);
                    if (dot > melhor) { melhor = dot; alvo = c; }
                }
            }
            if (alvo != atual)
            {
                if (atual) atual.Realcar(false);
                atual = alvo;
                if (atual) atual.Realcar(true);
                Texto();
            }
            if (mira) mira.color = atual ? new Color(0.22f, 0.84f, 0.82f, 1f) : new Color(1f, 1f, 1f, 0.55f);
            var m = Mouse.current;
            if (atual && m != null && m.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
            {
                atual.Acionar();
                toque = 0f;
                Texto();
            }
            if (maoDireita && toque < 1f)
            {
                toque = Mathf.Min(1f, toque + Time.deltaTime / 0.22f);
                maoDireita.localPosition = maoPos0 + new Vector3(-0.02f, 0.015f, 0.07f) * Mathf.Sin(Mathf.PI * toque);
            }
        }

        void Texto()
        {
            if (!rotulo) return;
            rotulo.text = atual
                ? $"{atual.Nome}\n<size=70%><color=#{(atual.ligado ? "59FF86" : "B9D2E8")}>{Loc.Get(atual.ligado ? "nave.estado.on" : "nave.estado.off")}</color>  -  {Loc.Get("nave.prompt")}</size>"
                : "";
        }
    }
}
