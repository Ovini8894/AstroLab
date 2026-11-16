using AstroLab.Settings;
using UnityEngine;

namespace AstroLab.Nave
{
    // Sons do cockpit gerados por codigo (sem arquivos de audio): estalo de chave, clique de botao,
    // bipe de comunicacao e alarme. Volume = MasterVolume x SfxVolume.
    public static class SomNave
    {
        static AudioClip chave, botao, bipe, alarme;
        const int Hz = 44100;

        static float Vol => GameSettings.MasterVolume * GameSettings.SfxVolume;

        public static void Clique(Vector3 pos, TipoControle tipo)
        {
            var c = tipo == TipoControle.Chave ? (chave ??= Gerar("nave_chave", 0.05f, Estalo(2600f, 380f))) : (botao ??= Gerar("nave_botao", 0.07f, Estalo(1500f, 160f)));
            AudioSource.PlayClipAtPoint(c, pos, Vol);
        }

        public static void Bipe(Vector3 pos) =>
            AudioSource.PlayClipAtPoint(bipe ??= Gerar("nave_bipe", 0.32f, t => (t < 0.14f || t > 0.18f ? 0.35f : 0f) * Mathf.Sin(2 * Mathf.PI * (t < 0.16f ? 1320f : 1760f) * t)), pos, Vol);

        public static void Alarme(Vector3 pos) =>
            AudioSource.PlayClipAtPoint(alarme ??= Gerar("nave_alarme", 0.9f, t => 0.3f * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * (t % 0.3f < 0.15f ? 880f : 660f) * t))), pos, Vol * 0.6f);

        static System.Func<float, float> Estalo(float freq, float queda)
        {
            var rnd = new System.Random(7);
            return t => Mathf.Exp(-t * queda) * (0.55f * Mathf.Sin(2 * Mathf.PI * freq * t) + 0.45f * ((float)rnd.NextDouble() * 2f - 1f));
        }

        static AudioClip Gerar(string nome, float dur, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(dur * Hz);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Hz;
                float env = Mathf.Clamp01(t / 0.002f) * Mathf.Clamp01((dur - t) / 0.01f);
                d[i] = f(t) * env;
            }
            var c = AudioClip.Create(nome, n, 1, Hz, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
