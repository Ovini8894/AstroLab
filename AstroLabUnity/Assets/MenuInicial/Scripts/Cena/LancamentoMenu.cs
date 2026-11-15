using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AstroLab.Menu
{
    public enum ModoLancamento { Foguete, TracoDeLuz }

    /// <summary>
    /// Efeito do botão Iniciar. Modo <see cref="ModoLancamento.Foguete"/> (padrão): um foguete sai da Terra (perto do
    /// Brasil, lado iluminado), começa devagar e acelera numa curva até passar ao lado da câmera, crescendo na tela,
    /// com chama, rastro e luz. Modo <see cref="ModoLancamento.TracoDeLuz"/>: só o traço de luz (versão anterior).
    /// No fim chama o callback (o menu faz o fade PRETO e carrega a cena do jogo).
    /// </summary>
    public sealed class LancamentoMenu : MonoBehaviour
    {
        public ModoLancamento modo = ModoLancamento.Foguete;

        [Header("Cena")]
        public Transform terra;
        public float raioTerra = 30f;
        [Tooltip("Ponto da superfície da Terra de onde o foguete sai (filho da Terra, gira com ela).")]
        public Transform pontoDeSaida;
        public Camera cameraMenu;
        public CameraMenu cameraRig;
        public BandeiraOndulante bandeira;

        [Header("Foguete")]
        public Transform foguete;
        [Tooltip("Comprimento do foguete (m).")]
        public float tamanhoFoguete = 7f;

        [Header("Rastro / chama")]
        public Transform cometa;          // objeto com o TrailRenderer (vira o escape do foguete)
        public TrailRenderer rastro;
        public Transform cabeca;          // brilho (chama do motor ou cabeça do traço)
        public Light luz;
        public Transform claraoTerra;

        [Header("Tela")]
        public Image flash;               // só no modo TracoDeLuz

        [Header("Tempo (s)")]
        public float atrasoSaida = 0.3f;
        public float duracaoVoo = 3.1f;
        [Tooltip("Fração do voo em que o fade preto começa (o foguete continua voando por baixo do fade).")]
        [Range(0.5f, 1f)] public float inicioFade = 0.72f;

        [Tooltip("Velocidade do efeito (1 = normal; 0 congela — útil para capturar quadros).")]
        public float escalaTempo = 1f;

        public bool Rodando { get; private set; }

        float relogio;
        float Agora => relogio;
        void Update() { if (Rodando) relogio += Time.unscaledDeltaTime * escalaTempo; }

        void Awake() => Esconder();

        void Esconder()
        {
            if (cometa) cometa.gameObject.SetActive(false);
            if (foguete) foguete.gameObject.SetActive(false);
            if (claraoTerra) claraoTerra.gameObject.SetActive(false);
            if (flash) { flash.color = new Color(1, 1, 1, 0); flash.raycastTarget = false; }
        }

        /// <summary>Roda o efeito; <paramref name="aoChegar"/> é chamado quando é hora do fade/carregamento.</summary>
        public void Iniciar(Action aoChegar)
        {
            if (Rodando) return;
            StartCoroutine(Rotina(aoChegar));
        }

        IEnumerator Rotina(Action aoChegar)
        {
            Rodando = true;
            relogio = 0f;
            bool comFoguete = modo == ModoLancamento.Foguete && foguete != null;
            SomMenu.Tocar(SomMenu.Som.Lancamento);
            SomMenu.SumirAmbiente(2.4f);
            if (cameraRig) cameraRig.Influencia = 0.25f;

            var cam = cameraMenu.transform;
            Vector3 centroTerra = terra.position;
            Vector3 paraCam = (cam.position - centroTerra).normalized;
            Vector3 lado = Vector3.Cross(paraCam, cam.up).normalized;
            Vector3 inicio = pontoDeSaida
                ? pontoDeSaida.position
                : centroTerra + (paraCam * 0.75f + cam.up * 0.45f + lado * 0.35f).normalized * raioTerra * 1.02f;
            Vector3 normalSaida = (inicio - centroTerra).normalized;

            // clarão de decolagem na superfície
            if (claraoTerra) { claraoTerra.gameObject.SetActive(true); claraoTerra.position = inicio + normalSaida * raioTerra * 0.02f; }
            float t0 = Agora;
            while (Agora - t0 < atrasoSaida)
            {
                float p = (Agora - t0) / atrasoSaida;
                if (claraoTerra) Escala(claraoTerra, raioTerra * Mathf.Lerp(0.05f, 0.35f, p), cam);
                yield return null;
            }

            // caminho: sobe pela normal da Terra e faz a curva para passar ao lado da câmera (à direita, abaixo dos botões)
            float dist = Vector3.Distance(inicio, cam.position);
            Vector3 fim = cam.position + cam.forward * 1.5f + cam.right * 5.5f - cam.up * 2.2f;
            Vector3 c1 = inicio + normalSaida * dist * 0.22f;
            Vector3 c2 = fim + cam.forward * dist * 0.45f + cam.right * dist * 0.06f - cam.up * dist * 0.04f;

            if (comFoguete)
            {
                foguete.gameObject.SetActive(true);
                foguete.localScale = Vector3.one * tamanhoFoguete;
                foguete.position = inicio;
                foguete.rotation = Quaternion.LookRotation(normalSaida, cam.up);
            }
            if (cometa)
            {
                cometa.gameObject.SetActive(true);
                cometa.position = inicio;
            }
            rastro.Clear();
            rastro.emitting = true;

            bool chegou = false, balancou = false, chamouFim = false;
            Vector3 anterior = inicio;
            t0 = Agora;
            while (true)
            {
                float p = Mathf.Clamp01((Agora - t0) / duracaoVoo);
                float e = Mathf.Pow(p, 2.2f);                                 // lançamento lento, depois acelera
                Vector3 pos = Bezier(inicio, c1, c2, fim, e);
                Vector3 dir = pos - anterior;
                anterior = pos;
                float d = Vector3.Distance(pos, cam.position);

                if (comFoguete)
                {
                    foguete.position = pos;
                    if (dir.sqrMagnitude > 1e-8f)
                        foguete.rotation = Quaternion.Slerp(foguete.rotation, Quaternion.LookRotation(dir.normalized, cam.up), 0.25f);
                    // o rastro/chama ficam no pé do foguete
                    Vector3 pe = pos - foguete.forward * tamanhoFoguete * 0.02f;
                    cometa.position = pe;
                    float chama = tamanhoFoguete * Mathf.Lerp(0.35f, 0.55f, Mathf.PerlinNoise(Time.unscaledTime * 18f, 0f));
                    Escala(cabeca, chama + 0.004f * d, cam);
                    cabeca.position = pe;
                    rastro.widthMultiplier = tamanhoFoguete * 0.12f + 0.004f * d;
                    rastro.time = Mathf.Lerp(1.2f, 0.5f, p);
                }
                else
                {
                    cometa.position = pos;
                    float tamanho = Mathf.Lerp(0.004f, 0.02f, p) * d + 0.25f * p * p;
                    Escala(cabeca, tamanho * 3f, cam);
                    rastro.widthMultiplier = tamanho * 1.2f;
                    rastro.time = Mathf.Lerp(0.9f, 0.35f, p);
                    if (flash && p > 0.8f)
                    {
                        float f = Mathf.InverseLerp(0.8f, 1f, p);
                        flash.color = new Color(0.85f, 0.97f, 1f, f * f * 0.6f);
                    }
                }
                if (luz) { luz.intensity = Mathf.Lerp(0f, 14f, p * p); luz.range = Mathf.Lerp(10f, 60f, p); }

                if (claraoTerra && claraoTerra.gameObject.activeSelf)
                {
                    float q = Mathf.Clamp01(p / 0.3f);
                    Escala(claraoTerra, raioTerra * Mathf.Lerp(0.35f, 0.0f, q), cam);
                    if (q >= 1f) claraoTerra.gameObject.SetActive(false);
                }

                if (bandeira && !balancou && p > 0.55f) { balancou = true; bandeira.Impulso(2.4f); }
                if (!chegou && p > 0.8f) { chegou = true; if (cameraRig) cameraRig.Tremer(1.0f, 0.8f); }
                if (!chamouFim && p >= inicioFade) { chamouFim = true; aoChegar?.Invoke(); }
                if (p >= 1f) break;
                yield return null;
            }

            rastro.emitting = false;
            if (comFoguete) foguete.gameObject.SetActive(false);
            Rodando = false;
        }

        /// <summary>Desfaz o efeito (se a cena do jogo não puder ser carregada).</summary>
        public void Restaurar()
        {
            StopAllCoroutines();
            Rodando = false;
            if (cameraRig) cameraRig.Influencia = 1f;
            if (rastro) rastro.emitting = false;
            Esconder();
        }

        static void Escala(Transform t, float s, Transform cam)
        {
            t.localScale = Vector3.one * s;
            t.rotation = Quaternion.LookRotation(t.position - cam.position, cam.up);   // billboard
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
    }
}
