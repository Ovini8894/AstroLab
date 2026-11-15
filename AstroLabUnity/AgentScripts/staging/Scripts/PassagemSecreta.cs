using System.Collections;
using AstroLab.Localization;
using AstroLab.Settings;
using TMPro;
using UnityEngine;

namespace AstroLab.Centro
{
    // Passagem secreta da recepção: o painel da parede desliza (som + luz), revela o elevador, a instrutora entra,
    // o jogador entra, as portas fecham e a cabine DESCE 20 m até o corredor super secreto.
    // Na viagem jogador e instrutora vão presos na cabine (CharacterController desligado): sem tremedeira de física;
    // o "tremor" é só um balanço leve da câmera.
    public class PassagemSecreta : MonoBehaviour
    {
        [Header("Painel")]
        public Transform painel;
        public Vector3 deslocamentoPainel = new Vector3(-3.3f, 0, 0);
        public Collider bloqueioPainel;
        public Light luzPoco;
        [Header("Elevador")]
        public Transform cabine;
        public Transform portaE, portaD;
        public float aberturaPorta = 0.78f;
        public Transform fundo;              // WP_Elevador_Secreto_Fundo
        public Collider zonaCabine;
        public TextMeshPro mostrador;
        public float duracaoDescida = 9f;
        [Header("Instrutora")]
        public Transform pontoNpcPainel, pontoNpcCabine, pontoSaida;

        AudioSource som;
        Vector3 portaEIni, portaDIni, abreE;

        void Awake()
        {
            som = gameObject.AddComponent<AudioSource>();
            som.playOnAwake = false; som.spatialBlend = 0.6f;
            if (portaE) portaEIni = portaE.localPosition;
            if (portaD) portaDIni = portaD.localPosition;
            // a porta "E" (x menor no Blender) abre para +X do Unity; deslocamento no espaço da cabine (vale nos dois andares)
            if (cabine) abreE = cabine.InverseTransformVector(Vector3.right) * aberturaPorta;
            if (luzPoco) luzPoco.enabled = false;
            Mostrador("ct.elevador.terreo");
        }

        void Mostrador(string chave) { if (mostrador) mostrador.text = Loc.Get(chave); }

        IEnumerator Portas(bool abrir, float dur = 0.9f)
        {
            Vector3 e0 = portaE.localPosition, d0 = portaD.localPosition;
            Vector3 e1 = abrir ? portaEIni + abreE : portaEIni;
            Vector3 d1 = abrir ? portaDIni - abreE : portaDIni;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / dur);
                portaE.localPosition = Vector3.Lerp(e0, e1, k); portaD.localPosition = Vector3.Lerp(d0, d1, k);
                yield return null;
            }
            portaE.localPosition = e1; portaD.localPosition = d1;
        }

        bool Dentro(Transform t) => zonaCabine && zonaCabine.bounds.Contains(t.position + Vector3.up * 0.2f);

        public IEnumerator Rodar(TutorialCentro tut)
        {
            var npc = tut.npc; var hud = tut.hud; var jog = tut.jogador;
            // 1. a instrutora vai até o painel e conta o segredo
            hud.Objetivo(Loc.Get("ct.obj.seguir", Loc.Get("ct.local.painel")));
            bool chegou = false;
            npc.IrPara(new[] { pontoNpcPainel.position }, () => chegou = true);
            yield return new WaitUntil(() => chegou && Distancia(npc.transform, jog.transform) < 7f);
            yield return tut.Falar("npc.secreto");

            // 2. o painel desliza para dentro da parede (som grave + luz do poço)
            som.clip = Sopro(1.4f); som.volume = 0.6f * GameSettings.SfxVolume; som.Play();
            if (luzPoco) luzPoco.enabled = true;
            Vector3 p0 = painel.localPosition, p1 = p0 + deslocamentoPainel;
            for (float t = 0; t < 1.6f; t += Time.deltaTime)
            {
                painel.localPosition = Vector3.Lerp(p0, p1, Mathf.SmoothStep(0, 1, t / 1.6f));
                yield return null;
            }
            if (bloqueioPainel) bloqueioPainel.enabled = false;
            yield return Portas(true);

            // 3. instrutora entra; jogador entra
            chegou = false;
            npc.IrPara(new[] { pontoNpcCabine.position }, () => chegou = true);
            hud.Objetivo(Loc.Get("ct.obj.elevador_secreto"));
            tut.marcador.Colocar(cabine.position + Vector3.up * 2.2f);
            yield return new WaitUntil(() => chegou && Dentro(jog.transform));
            tut.marcador.gameObject.SetActive(false);

            // 4. portas fecham, cabine desce com os dois
            yield return Portas(false, 0.8f);
            var cc = jog.GetComponent<CharacterController>();
            jog.enabled = false; cc.enabled = false;
            var paiJ = jog.transform.parent; var paiN = npc.transform.parent;
            jog.transform.SetParent(cabine, true); npc.transform.SetParent(cabine, true);
            npc.enabled = false;
            var cam = jog.GetComponentInChildren<Camera>().transform; var camBase = cam.localPosition;
            som.clip = Zumbido(duracaoDescida); som.volume = 0.35f * GameSettings.SfxVolume; som.Play();
            Mostrador("ct.elevador.descendo");
            float y0 = cabine.position.y, y1 = fundo.position.y;
            for (float t = 0; t < duracaoDescida; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / duracaoDescida);
                var p = cabine.position; p.y = Mathf.Lerp(y0, y1, k); cabine.position = p;
                float trem = Mathf.Sin(Mathf.PI * t / duracaoDescida);                // treme mais no meio da viagem
                cam.localPosition = camBase + new Vector3(Mathf.PerlinNoise(t * 9f, 0) - 0.5f, Mathf.PerlinNoise(0, t * 11f) - 0.5f, 0) * 0.012f * trem;
                yield return null;
            }
            cam.localPosition = camBase;
            var pf = cabine.position; pf.y = y1; cabine.position = pf;
            Mostrador("ct.elevador.subsolo");
            jog.transform.SetParent(paiJ, true); npc.transform.SetParent(paiN, true);
            jog.transform.rotation = Quaternion.Euler(0, jog.transform.eulerAngles.y, 0);
            cc.enabled = true; jog.enabled = true; npc.enabled = true;
            yield return Portas(true);
            yield return tut.Falar("npc.subsolo");
            if (pontoSaida) npc.IrPara(new[] { pontoSaida.position });
            hud.Objetivo(Loc.Get("ct.obj.corredor"));
        }

        static float Distancia(Transform a, Transform b) { var d = a.position - b.position; d.y = 0; return d.magnitude; }

        static AudioClip Sopro(float dur)
        {
            const int hz = 22050; int n = (int)(hz * dur); var d = new float[n]; var r = new System.Random(3);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Sin(Mathf.PI * i / n);
                lp = Mathf.Lerp(lp, (float)(r.NextDouble() * 2 - 1), 0.04f);                 // ruído filtrado (ar/pneumático)
                d[i] = (lp * 0.9f + Mathf.Sin(2 * Mathf.PI * 55 * i / hz) * 0.25f) * env;
            }
            var c = AudioClip.Create("PainelSecreto", n, 1, hz, false); c.SetData(d, 0); return c;
        }

        static AudioClip Zumbido(float dur)
        {
            const int hz = 22050; int n = (int)(hz * dur); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)hz, env = Mathf.Clamp01(t * 2) * Mathf.Clamp01((dur - t) * 2);
                d[i] = (Mathf.Sin(2 * Mathf.PI * 70 * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 140 * t) * 0.15f) * env * (0.85f + 0.15f * Mathf.Sin(t * 7f));
            }
            var c = AudioClip.Create("ElevadorZumbido", n, 1, hz, false); c.SetData(d, 0); return c;
        }
    }
}
