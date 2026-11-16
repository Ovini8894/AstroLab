using System.Collections;
using AstroLab.Settings;
using TMPro;
using UnityEngine;

namespace AstroLab.Centro
{
    // Porta hexagonal das salas do corredor: 6 lâminas que recolhem para dentro da moldura (íris),
    // anel de status (vermelho = trancada, verde = liberada) e colisor que some quando abre.
    public class PortaHex : MonoBehaviour
    {
        public Transform[] laminas;
        public Renderer anel;
        public Collider bloqueio;
        public TextMeshPro placa;
        public float curso = 1.35f, duracao = 1.2f;
        public bool Liberada { get; private set; }
        public bool Aberta { get; private set; }

        Vector3[] ini, dir;
        MaterialPropertyBlock mpb;
        AudioSource som;
        static readonly int Emissao = Shader.PropertyToID("_EmissionColor");
        static readonly int Cor = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            ini = new Vector3[laminas.Length]; dir = new Vector3[laminas.Length];
            for (int i = 0; i < laminas.Length; i++)
            {
                ini[i] = laminas[i].localPosition;
                // cada lâmina sai na direção do seu "meio" (centro do triângulo) a partir do centro da porta
                var r = laminas[i].GetComponent<Renderer>();
                var centroMundo = r ? r.bounds.center : laminas[i].position;
                var d = laminas[i].parent.InverseTransformVector(centroMundo - laminas[i].position);
                dir[i] = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up;
            }
            mpb = new MaterialPropertyBlock();
            som = gameObject.AddComponent<AudioSource>(); som.spatialBlend = 1f; som.minDistance = 3f; som.maxDistance = 25f; som.playOnAwake = false;
            Status(false);
        }

        public void Status(bool liberada)
        {
            Liberada = liberada;
            var c = liberada ? new Color(0.35f, 1f, 0.53f) : new Color(1f, 0.29f, 0.29f);
            if (anel)
            {
                anel.GetPropertyBlock(mpb);
                mpb.SetColor(Cor, c); mpb.SetColor(Emissao, c * 3f);
                anel.SetPropertyBlock(mpb);
            }
        }

        public void Abrir() { if (!Aberta) StartCoroutine(Mover(true)); }
        public void Fechar() { if (Aberta) StartCoroutine(Mover(false)); }

        IEnumerator Mover(bool abrir)
        {
            Aberta = abrir;
            if (abrir) Status(true);
            som.clip = Clique(); som.volume = 0.7f * GameSettings.SfxVolume; som.Play();
            for (float t = 0; t < duracao; t += Time.deltaTime)
            {
                for (int i = 0; i < laminas.Length; i++)
                {
                    float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t / duracao) * 1.3f - i * 0.05f));   // leve cascata
                    if (!abrir) k = 1 - k;
                    laminas[i].localPosition = ini[i] + dir[i] * curso * k;
                }
                yield return null;
            }
            for (int i = 0; i < laminas.Length; i++) laminas[i].localPosition = ini[i] + (abrir ? dir[i] * curso : Vector3.zero);
            if (bloqueio) bloqueio.enabled = !abrir;
        }

        static AudioClip Clique()
        {
            const int hz = 22050; int n = (int)(hz * 0.9f); var d = new float[n]; var r = new System.Random(5); float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)hz;
                lp = Mathf.Lerp(lp, (float)(r.NextDouble() * 2 - 1), 0.08f);
                d[i] = (lp * 0.6f * Mathf.Exp(-t * 3f) + Mathf.Sin(2 * Mathf.PI * (90 + 60 * t) * t) * 0.3f * Mathf.Exp(-t * 2.5f));
            }
            var c = AudioClip.Create("PortaHex", n, 1, hz, false); c.SetData(d, 0); return c;
        }
    }
}
