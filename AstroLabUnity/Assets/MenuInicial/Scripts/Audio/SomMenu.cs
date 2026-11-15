using AstroLab.Settings;
using UnityEngine;

namespace AstroLab.Menu
{
    /// <summary>
    /// Sons do menu gerados por código (sem arquivos de áudio): ambiente espacial em loop (volume de Música)
    /// e cliques/hover/lançamento (volume de Efeitos). Respeita GameSettings.MusicVolume/SfxVolume em tempo real.
    /// </summary>
    public sealed class SomMenu : MonoBehaviour
    {
        public enum Som { Hover, Clique, Tique, Voltar, Lancamento }

        static SomMenu instancia;

        [Range(0f, 1f)] public float volumeAmbiente = 0.55f;
        [Range(0f, 1f)] public float volumeEfeitos = 0.5f;

        AudioSource fonteAmbiente, fonteEfeitos;
        AudioClip hover, clique, tique, voltar, lancamento;
        float fadeAmbiente = 1f;
        const int Taxa = 44100;

        public static void Tocar(Som s)
        {
            if (instancia == null) return;
            var c = s switch
            {
                Som.Hover => instancia.hover,
                Som.Clique => instancia.clique,
                Som.Tique => instancia.tique,
                Som.Voltar => instancia.voltar,
                _ => instancia.lancamento,
            };
            float v = s switch { Som.Hover => 0.35f, Som.Tique => 0.25f, Som.Lancamento => 1f, _ => 0.6f };
            instancia.fonteEfeitos.PlayOneShot(c, v);
        }

        /// <summary>Abaixa o ambiente até 0 em <paramref name="duracao"/> segundos (usado no lançamento).</summary>
        public static void SumirAmbiente(float duracao)
        {
            if (instancia != null) instancia.StartCoroutine(instancia.Sumir(duracao));
        }

        System.Collections.IEnumerator Sumir(float d)
        {
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < d) { fadeAmbiente = 1f - (Time.unscaledTime - t0) / d; yield return null; }
            fadeAmbiente = 0f;
        }

        void Awake()
        {
            instancia = this;
            fonteAmbiente = gameObject.AddComponent<AudioSource>();
            fonteAmbiente.loop = true; fonteAmbiente.playOnAwake = false; fonteAmbiente.spatialBlend = 0f;
            fonteEfeitos = gameObject.AddComponent<AudioSource>();
            fonteEfeitos.playOnAwake = false; fonteEfeitos.spatialBlend = 0f;

            hover = Gerar("hover", 0.07f, (t, d) => Seno(1320f + 500f * t / d, t) * Env(t, d, 0.004f) * 0.5f);
            tique = Gerar("tique", 0.025f, (t, d) => Seno(2100f, t) * Env(t, d, 0.002f) * 0.4f);
            clique = Gerar("clique", 0.2f, (t, d) =>
                (Seno(t < 0.07f ? 880f : 1320f, t) * 0.55f + Seno(t < 0.07f ? 1760f : 2640f, t) * 0.15f) * Env(t, d, 0.004f));
            voltar = Gerar("voltar", 0.16f, (t, d) => Seno(t < 0.06f ? 1100f : 740f, t) * Env(t, d, 0.004f) * 0.5f);
            lancamento = GerarLancamento(3.6f);
            fonteAmbiente.clip = GerarAmbiente(16f);
        }

        void Start() => fonteAmbiente.Play();

        void OnDestroy() { if (instancia == this) instancia = null; }

        void Update()
        {
            fonteAmbiente.volume = volumeAmbiente * GameSettings.MusicVolume * fadeAmbiente;
            fonteEfeitos.volume = volumeEfeitos * GameSettings.SfxVolume;
        }

        // ---------------------------------------------------------------- síntese
        static float Seno(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

        static float Env(float t, float d, float ataque)
        {
            float a = Mathf.Clamp01(t / ataque);
            float r = 1f - Mathf.Clamp01(t / d);
            return a * r * r;
        }

        static AudioClip Gerar(string nome, float dur, System.Func<float, float, float> f)
        {
            int n = Mathf.CeilToInt(dur * Taxa);
            var dados = new float[n];
            for (int i = 0; i < n; i++) dados[i] = f(i / (float)Taxa, dur);
            var c = AudioClip.Create(nome, n, 1, Taxa, false);
            c.SetData(dados, 0);
            return c;
        }

        // Ambiente: acorde suave (Ré menor com 9ª) com batimentos lentos + "vento" filtrado. Loop sem emenda.
        static AudioClip GerarAmbiente(float dur)
        {
            int n = Mathf.CeilToInt(dur * Taxa);
            var dados = new float[n * 2];
            float[] notas = { 73.42f, 110.0f, 146.83f, 174.61f, 220.0f, 329.63f };
            float[] pesos = { 0.30f, 0.22f, 0.16f, 0.10f, 0.08f, 0.04f };
            var rnd = new System.Random(5);
            float lpL = 0f, lpR = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float l = 0f, r = 0f;
                for (int k = 0; k < notas.Length; k++)
                {
                    // frequências arredondadas para ciclos inteiros no loop (sem clique na emenda)
                    float f = Mathf.Round(notas[k] * dur) / dur;
                    float df = Mathf.Round(0.18f * (k + 1) * dur) / dur;
                    float lfo = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * (1f + k % 3) / dur * t + k);
                    l += pesos[k] * lfo * Seno(f, t);
                    r += pesos[k] * lfo * Seno(f + df, t);
                }
                float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lpL += (ruido - lpL) * 0.012f; lpR += ((float)(rnd.NextDouble() * 2.0 - 1.0) - lpR) * 0.012f;
                float vento = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2f / dur * t);
                dados[i * 2] = (l * 0.32f + lpL * 1.4f * vento);
                dados[i * 2 + 1] = (r * 0.32f + lpR * 1.4f * (1f - vento * 0.5f));
            }
            // emenda: 50 ms de cross-fade do fim com o começo
            int x = Taxa / 20;
            for (int i = 0; i < x; i++)
            {
                float a = i / (float)x;
                for (int c = 0; c < 2; c++)
                {
                    int fim = (n - x + i) * 2 + c, ini = i * 2 + c;
                    dados[fim] = Mathf.Lerp(dados[fim], dados[ini], a);
                }
            }
            var clip = AudioClip.Create("ambiente_menu", n, 2, Taxa, false);
            clip.SetData(dados, 0);
            return clip;
        }

        // Lançamento do foguete: ronco grave (ruído bem filtrado + 38 Hz) subindo de volume, chiado do escape que
        // sobe de tom quando ele passa perto da câmera e um estrondo curto no fim.
        static AudioClip GerarLancamento(float dur)
        {
            int n = Mathf.CeilToInt(dur * Taxa);
            var d = new float[n];
            var rnd = new System.Random(11);
            float lpR = 0f, lpR2 = 0f, lp = 0f, lp2 = 0f, fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float p = t / dur;
                float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0);
                // ronco: passa-baixa bem fechado
                lpR += (ruido - lpR) * 0.006f; lpR2 += (lpR - lpR2) * 0.006f;
                fase += 2f * Mathf.PI * Mathf.Lerp(34f, 46f, p) / Taxa;
                float ronco = (lpR2 * 9f + Mathf.Sin(fase) * 0.35f) * Mathf.SmoothStep(0f, 1f, p / 0.7f);
                // chiado do escape: abre o filtro conforme o foguete se aproxima
                float corte = Mathf.Lerp(0.02f, 0.3f, p * p);
                lp += (ruido - lp) * corte; lp2 += (lp - lp2) * corte;
                float chiado = lp2 * 1.6f * Mathf.SmoothStep(0.3f, 0.85f, p);
                float fim = p < 0.9f ? 1f : Mathf.Clamp01(1f - (p - 0.9f) / 0.1f);
                float estrondo = p > 0.78f ? Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-(t - 0.78f * dur) * 4f) * 0.45f : 0f;
                d[i] = Mathf.Clamp((ronco + chiado) * fim + estrondo, -1f, 1f);
            }
            var c = AudioClip.Create("lancamento", n, 1, Taxa, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
