using System;
using System.Collections.Generic;
using AstroLab.Localization;
using AstroLab.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AstroLab.Centro
{
    // Chatbox do NPC: nome + fala digitada letra a letra (com "blip" de voz).
    // As falas são CHAVES de localização (npc.*): o texto vem de Loc.Get; trocando o idioma, a linha atual reinicia.
    // [E], [Espaço], [Enter] ou clique: completa a linha / avança.
    public class CaixaDialogo : MonoBehaviour
    {
        public HudCentro hud;
        public float letrasPorSegundo = 45f;
        public string chaveNome = "npc.nome";

        public bool Aberto { get; private set; }

        GameObject caixa;
        TextMeshProUGUI txtNome, txtFala, txtDica;
        RectTransform seta;
        readonly List<string> chaves = new List<string>();
        int linha;
        float progresso;
        string textoAtual = "";
        Action aoTerminar;
        int frameAbertura;
        AudioSource voz;
        float proximoBlip;

        void Start()
        {
            var r = hud.Raiz;
            var fundo = hud.Imagem("Dialogo", r, HudCentro.Painel, true);
            caixa = fundo.gameObject;
            var rt = fundo.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 40);
            rt.sizeDelta = new Vector2(1180, 210);
            var borda = hud.Imagem("Borda", rt, HudCentro.Ciano, true);
            HudCentro.Esticar(borda.rectTransform);
            borda.rectTransform.offsetMin = new Vector2(0, 202); borda.rectTransform.offsetMax = new Vector2(0, 0);

            var tag = hud.Imagem("Nome", rt, HudCentro.Amarelo, true);
            tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = tag.rectTransform.pivot = new Vector2(0, 1);
            tag.rectTransform.anchoredPosition = new Vector2(36, 26);
            tag.rectTransform.sizeDelta = new Vector2(330, 50);
            txtNome = hud.Texto("Texto", tag.rectTransform, 26, new Color(0.11f, 0.1f, 0.24f), TextAlignmentOptions.Center, hud.fonteTitulo);
            HudCentro.Esticar(txtNome.rectTransform);

            txtFala = hud.Texto("Fala", rt, 30, Color.white, TextAlignmentOptions.TopLeft);
            txtFala.rectTransform.anchorMin = Vector2.zero; txtFala.rectTransform.anchorMax = Vector2.one;
            txtFala.rectTransform.offsetMin = new Vector2(40, 46); txtFala.rectTransform.offsetMax = new Vector2(-40, -40);
            txtFala.lineSpacing = 8;

            txtDica = hud.Texto("Dica", rt, 20, HudCentro.Ciano, TextAlignmentOptions.BottomRight);
            txtDica.rectTransform.anchorMin = new Vector2(1, 0); txtDica.rectTransform.anchorMax = new Vector2(1, 0);
            txtDica.rectTransform.pivot = new Vector2(1, 0);
            txtDica.rectTransform.anchoredPosition = new Vector2(-70, 14);
            txtDica.rectTransform.sizeDelta = new Vector2(400, 30);

            var s = hud.Imagem("Seta", rt, HudCentro.Ciano);
            seta = s.rectTransform;
            seta.anchorMin = seta.anchorMax = new Vector2(1, 0);
            seta.sizeDelta = new Vector2(18, 18);
            seta.localRotation = Quaternion.Euler(0, 0, 45);
            seta.anchoredPosition = new Vector2(-42, 30);

            voz = gameObject.AddComponent<AudioSource>();
            voz.playOnAwake = false;
            voz.spatialBlend = 0;
            voz.clip = CriarBlip();
            caixa.SetActive(false);
            Loc.OnLanguageChanged += AoTrocarIdioma;
        }

        void OnDestroy() => Loc.OnLanguageChanged -= AoTrocarIdioma;

        // ------------------------------------------------------------------ API
        // prefixo "npc.est2" -> npc.est2.1, npc.est2.2, ... enquanto existirem no arquivo de idioma
        public static List<string> Linhas(string prefixo)
        {
            var l = new List<string>();
            for (int i = 1; i < 30 && Loc.Has($"{prefixo}.{i}"); i++) l.Add($"{prefixo}.{i}");
            if (l.Count == 0) l.Add(prefixo);
            return l;
        }

        public void Mostrar(string prefixo, Action fim = null) => Mostrar(Linhas(prefixo), fim);

        public void Mostrar(IList<string> chavesFalas, Action fim = null)
        {
            chaves.Clear();
            chaves.AddRange(chavesFalas);
            aoTerminar = fim;
            linha = 0;
            Aberto = true;
            frameAbertura = Time.frameCount;
            caixa.SetActive(true);
            txtNome.text = Loc.Get(chaveNome);
            txtDica.text = Loc.Get("ct.continuar");
            IniciarLinha();
        }

        public void Fechar()
        {
            Aberto = false;
            caixa.SetActive(false);
            var f = aoTerminar; aoTerminar = null;
            f?.Invoke();
        }

        // ------------------------------------------------------------------ lógica
        void IniciarLinha()
        {
            textoAtual = Loc.Get(chaves[linha]);
            progresso = 0;
            txtFala.text = textoAtual;
            txtFala.maxVisibleCharacters = 0;
        }

        void AoTrocarIdioma()
        {
            if (!Aberto) return;
            txtNome.text = Loc.Get(chaveNome);
            txtDica.text = Loc.Get("ct.continuar");
            IniciarLinha();
        }

        void Update()
        {
            if (!Aberto) return;
            int total = textoAtual.Length;
            bool completa = progresso >= total;
            if (!completa)
            {
                int antes = (int)progresso;
                progresso = Mathf.Min(total, progresso + letrasPorSegundo * Time.deltaTime);
                txtFala.maxVisibleCharacters = (int)progresso;
                if ((int)progresso > antes && Time.time >= proximoBlip && antes < total && !char.IsWhiteSpace(textoAtual[antes]))
                {
                    voz.pitch = UnityEngine.Random.Range(0.92f, 1.12f);
                    voz.volume = 0.25f * GameSettings.SfxVolume;
                    voz.Play();
                    proximoBlip = Time.time + 0.07f;
                }
            }
            seta.gameObject.SetActive(completa);
            if (completa) seta.anchoredPosition = new Vector2(-42, 30 + Mathf.Sin(Time.time * 6f) * 4f);

            if (Time.frameCount == frameAbertura || !Avancou()) return;
            if (!completa) { progresso = total; txtFala.maxVisibleCharacters = total; return; }
            linha++;
            if (linha >= chaves.Count) Fechar();
            else IniciarLinha();
        }

        static bool Avancou()
        {
            var kb = Keyboard.current; var m = Mouse.current;
            return (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                || (m != null && m.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked);
        }

        static AudioClip CriarBlip()
        {
            const int hz = 22050; int n = (int)(hz * 0.045f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)hz;
                float env = Mathf.Sin(Mathf.PI * i / n);
                d[i] = (Mathf.Sin(2 * Mathf.PI * 520 * t) * 0.6f + Mathf.Sin(2 * Mathf.PI * 1040 * t) * 0.2f) * env * 0.5f;
            }
            var c = AudioClip.Create("BlipVoz", n, 1, hz, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
