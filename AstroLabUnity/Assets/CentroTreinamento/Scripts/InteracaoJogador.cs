using AstroLab.Localization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Centro
{
    // Escolhe o Interagivel ativo mais próximo à frente da câmera, mostra o prompt e dispara com [E].
    public class InteracaoJogador : MonoBehaviour
    {
        public HudCentro hud;
        public CaixaDialogo dialogo;
        [Range(0f, 1f)] public float anguloMinimo = 0.55f;   // cosseno: só pega o que está mais ou menos na frente

        Camera cam;
        Interagivel atual;

        void Awake() => cam = GetComponentInChildren<Camera>();

        void Update()
        {
            atual = null;
            bool bloqueado = (dialogo && dialogo.Aberto) || (hud && hud.Ocupado);
            if (!bloqueado && cam)
            {
                float melhor = float.MaxValue;
                var origem = cam.transform.position;
                foreach (var it in Interagivel.Todos)
                {
                    if (!it.ativo) continue;
                    var d = it.transform.position - origem;
                    float dist = d.magnitude;                       // 3D: a torre tem paineis empilhados na vertical
                    if (dist > it.alcance) continue;
                    if (dist > 0.6f && Vector3.Dot(cam.transform.forward, d.normalized) < anguloMinimo) continue;
                    // linha de visão: não aciona painel através de parede (aceita bater no próprio móvel, a < 1 m do ponto)
                    if (Physics.Linecast(origem, it.transform.position, out var hit, ~(1 << 2), QueryTriggerInteraction.Ignore)
                        && hit.distance < d.magnitude - 1.0f) continue;
                    if (dist < melhor) { melhor = dist; atual = it; }
                }
            }
            if (hud) hud.Prompt(atual ? Loc.Get("ct.prompt", Loc.Get(atual.chaveAcao)) : null);
            var kb = Keyboard.current;
            if (atual && kb != null && kb.eKey.wasPressedThisFrame) atual.Interagir();
        }

        void OnDisable()
        {
            if (hud) hud.Prompt(null);
        }
    }
}
