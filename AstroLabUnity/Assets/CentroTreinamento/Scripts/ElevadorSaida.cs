using System.Collections;
using AstroLab.Localization;
using AstroLab.Settings;
using TMPro;
using UnityEngine;

namespace AstroLab.Centro
{
    // L4: elevador no fim do corredor. A instrutora leva o jogador até a tampa do corredor, a cabine SOBE 20 m
    // até o quiosque na superfície (ao lado da estrada da plataforma) e as portas abrem para a estrada.
    // Mesmo esquema do elevador secreto: jogador e instrutora presos na cabine durante a viagem.
    public class ElevadorSaida : MonoBehaviour
    {
        public Transform cabine;
        public Transform portaE, portaD;          // portas da cabine (abrem para os lados)
        public float aberturaPorta = 0.78f;
        public Transform topo;                   // WP_Elevador_Saida_Topo: posição da cabine na superfície
        public Collider zonaCabine;
        public TextMeshPro mostrador;
        public float duracaoSubida = 9f;
        public Vector3[] rotaAteElevador;        // da última sala até a frente da cabine (pelo corredor)
        public Transform pontoNpcCabine, pontoSaidaQuiosque;
        public Collider bloqueioQuiosque;        // porta do quiosque (fechada até a cabine chegar)

        AudioSource som;
        Vector3 portaEIni, portaDIni, abreE;

        void Awake()
        {
            som = gameObject.AddComponent<AudioSource>();
            som.playOnAwake = false; som.spatialBlend = 0.6f;
            if (portaE) portaEIni = portaE.localPosition;
            if (portaD) portaDIni = portaD.localPosition;
            if (cabine) abreE = cabine.InverseTransformVector(cabine.right) * aberturaPorta;
            Mostrador("ct.elevador.subsolo");
        }

        void Mostrador(string chave) { if (mostrador) mostrador.text = Loc.Get(chave); }

        IEnumerator Portas(bool abrir, float dur = 0.9f)
        {
            if (!portaE || !portaD) yield break;
            Vector3 e0 = portaE.localPosition, d0 = portaD.localPosition;
            Vector3 e1 = abrir ? portaEIni + abreE : portaEIni, d1 = abrir ? portaDIni - abreE : portaDIni;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / dur);
                portaE.localPosition = Vector3.Lerp(e0, e1, k); portaD.localPosition = Vector3.Lerp(d0, d1, k);
                yield return null;
            }
            portaE.localPosition = e1; portaD.localPosition = d1;
        }

        bool Dentro(Transform t) => zonaCabine && zonaCabine.bounds.Contains(t.position + Vector3.up * 0.2f);
        static float Distancia(Transform a, Transform b) { var d = a.position - b.position; d.y = 0; return d.magnitude; }

        public IEnumerator Rodar(TutorialCentro tut)
        {
            var npc = tut.npc; var hud = tut.hud; var jog = tut.jogador;
            // 1. pelo corredor até o fim; durante o caminho ela conta o fim da história do programa
            hud.Objetivo(Loc.Get("ct.obj.elevador_saida"));
            bool chegou = false;
            npc.IrPara(rotaAteElevador ?? new Vector3[0], () => chegou = true);
            tut.marcador.Colocar(cabine.position + Vector3.up * 2.4f);
            var narra = tut.StartCoroutine(tut.Narrar("npc.corredor.saida"));
            yield return new WaitUntil(() => chegou && Distancia(npc.transform, jog.transform) < 8f);
            tut.PararNarracao(narra);
            yield return tut.Falar("npc.saida");
            yield return Portas(true);

            // 2. os dois entram
            chegou = false;
            npc.IrPara(new[] { pontoNpcCabine.position }, () => chegou = true);
            yield return new WaitUntil(() => chegou && Dentro(jog.transform));
            tut.marcador.gameObject.SetActive(false);

            // 3. portas fecham, cabine sobe 20 m
            yield return Portas(false, 0.8f);
            var cc = jog.GetComponent<CharacterController>();
            jog.enabled = false; cc.enabled = false;
            var paiJ = jog.transform.parent; var paiN = npc.transform.parent;
            jog.transform.SetParent(cabine, true); npc.transform.SetParent(cabine, true);
            npc.enabled = false;
            var cam = jog.GetComponentInChildren<Camera>().transform; var camBase = cam.localPosition;
            som.clip = Zumbido(duracaoSubida); som.volume = 0.35f * GameSettings.SfxVolume; som.Play();
            Mostrador("ct.elevador.subindo");
            float y0 = cabine.position.y, y1 = topo.position.y;
            for (float t = 0; t < duracaoSubida; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / duracaoSubida);
                var p = cabine.position; p.y = Mathf.Lerp(y0, y1, k); cabine.position = p;
                float trem = Mathf.Sin(Mathf.PI * t / duracaoSubida);
                cam.localPosition = camBase + new Vector3(Mathf.PerlinNoise(t * 9f, 0) - 0.5f, Mathf.PerlinNoise(0, t * 11f) - 0.5f, 0) * 0.012f * trem;
                yield return null;
            }
            cam.localPosition = camBase;
            var pf = cabine.position; pf.y = y1; cabine.position = pf;
            Mostrador("ct.elevador.superficie");
            jog.transform.SetParent(paiJ, true); npc.transform.SetParent(paiN, true);
            jog.transform.rotation = Quaternion.Euler(0, jog.transform.eulerAngles.y, 0);
            cc.enabled = true; jog.enabled = true; npc.enabled = true;
            if (bloqueioQuiosque) bloqueioQuiosque.enabled = false;
            yield return Portas(true);
            yield return tut.Falar("npc.superficie");
            if (pontoSaidaQuiosque) npc.IrPara(new[] { pontoSaidaQuiosque.position });
        }

        static AudioClip Zumbido(float dur)
        {
            const int hz = 22050; int n = (int)(hz * dur); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)hz, env = Mathf.Clamp01(t * 2) * Mathf.Clamp01((dur - t) * 2);
                d[i] = (Mathf.Sin(2 * Mathf.PI * 70 * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 140 * t) * 0.15f) * env * (0.85f + 0.15f * Mathf.Sin(t * 7f));
            }
            var c = AudioClip.Create("ElevadorSaidaZumbido", n, 1, hz, false); c.SetData(d, 0); return c;
        }
    }
}
