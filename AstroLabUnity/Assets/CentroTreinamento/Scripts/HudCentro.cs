using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroLab.Centro
{
    // HUD do Centro de Treinamento, montado por código (estilo do menu: azul-noite translúcido, contorno ciano, fonte Exo 2).
    // Objetivo (alto, à esquerda), progresso, prompt de interação, overlays de efeito (força G, água, VR) e fade.
    [DefaultExecutionOrder(-50)]
    public class HudCentro : MonoBehaviour
    {
        public TMP_FontAsset fonte;
        public TMP_FontAsset fonteTitulo;
        public Sprite spriteArredondado;

        public static readonly Color Painel = new Color(0.06f, 0.07f, 0.18f, 0.82f);
        public static readonly Color Ciano = new Color(0.28f, 0.9f, 1f, 1f);
        public static readonly Color Amarelo = new Color(1f, 0.76f, 0.23f, 1f);

        public RectTransform Raiz { get; private set; }
        public bool Ocupado { get; set; }            // durante animações/fades a interação fica bloqueada

        TextMeshProUGUI txtObjetivo, txtProgresso, txtPrompt, txtEfeito, txtEfeitoSub;
        GameObject caixaObjetivo, caixaPrompt;
        Image fade, overlay, vinheta;
        CanvasGroup grupoEfeito;

        void Awake()
        {
            var canvasGo = new GameObject("HUD_Centro", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Raiz = (RectTransform)canvasGo.transform;

            // overlays de efeito (atrás do texto)
            overlay = Imagem("Overlay", Raiz, new Color(0, 0, 0, 0));
            Esticar(overlay.rectTransform);
            overlay.raycastTarget = false;
            vinheta = Imagem("Vinheta", Raiz, new Color(0, 0, 0, 0));
            Esticar(vinheta.rectTransform);
            vinheta.sprite = CriarVinheta();
            vinheta.raycastTarget = false;

            // objetivo
            var obj = Imagem("Objetivo", Raiz, Painel, true);
            caixaObjetivo = obj.gameObject;
            var rt = obj.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(36, -32);
            rt.sizeDelta = new Vector2(620, 104);
            var faixa = Imagem("Faixa", rt, Amarelo, true);
            faixa.rectTransform.anchorMin = new Vector2(0, 0); faixa.rectTransform.anchorMax = new Vector2(0, 1);
            faixa.rectTransform.pivot = new Vector2(0, 0.5f);
            faixa.rectTransform.sizeDelta = new Vector2(8, -18); faixa.rectTransform.anchoredPosition = new Vector2(10, 0);
            txtProgresso = Texto("Progresso", rt, 20, Ciano, TextAlignmentOptions.TopLeft, fonteTitulo ? fonteTitulo : fonte);
            Posicionar(txtProgresso.rectTransform, new Vector2(30, -12), new Vector2(570, 28));
            txtObjetivo = Texto("Texto", rt, 27, Color.white, TextAlignmentOptions.TopLeft);
            Posicionar(txtObjetivo.rectTransform, new Vector2(30, -42), new Vector2(570, 58));
            caixaObjetivo.SetActive(false);

            // prompt de interação
            var pr = Imagem("Prompt", Raiz, Painel, true);
            caixaPrompt = pr.gameObject;
            pr.rectTransform.anchorMin = pr.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            pr.rectTransform.anchoredPosition = new Vector2(0, -140);
            pr.rectTransform.sizeDelta = new Vector2(520, 60);
            txtPrompt = Texto("Texto", pr.rectTransform, 26, Color.white, TextAlignmentOptions.Center);
            Esticar(txtPrompt.rectTransform);
            caixaPrompt.SetActive(false);

            // texto grande de efeito (força G, profundidade...)
            var ge = new GameObject("Efeito", typeof(RectTransform), typeof(CanvasGroup));
            ge.transform.SetParent(Raiz, false);
            grupoEfeito = ge.GetComponent<CanvasGroup>();
            grupoEfeito.alpha = 0;
            var ger = (RectTransform)ge.transform;
            ger.anchorMin = ger.anchorMax = new Vector2(0.5f, 0.5f);
            ger.anchoredPosition = new Vector2(0, 230);
            ger.sizeDelta = new Vector2(1200, 200);
            txtEfeito = Texto("Valor", ger, 64, Amarelo, TextAlignmentOptions.Center, fonteTitulo ? fonteTitulo : fonte);
            Posicionar(txtEfeito.rectTransform, new Vector2(0, 0), new Vector2(1200, 90), centro: true);
            txtEfeitoSub = Texto("Sub", ger, 26, Color.white, TextAlignmentOptions.Center);
            Posicionar(txtEfeitoSub.rectTransform, new Vector2(0, -70), new Vector2(1200, 40), centro: true);

            // fade (por cima de tudo)
            fade = Imagem("Fade", Raiz, new Color(0.04f, 0.04f, 0.1f, 1f));
            Esticar(fade.rectTransform);
            fade.raycastTarget = false;
        }

        // ------------------------------------------------------------------ API
        public void Objetivo(string texto)
        {
            caixaObjetivo.SetActive(!string.IsNullOrEmpty(texto));
            txtObjetivo.text = texto ?? "";
        }

        public void Progresso(string texto) => txtProgresso.text = texto ?? "";

        public void Prompt(string texto)
        {
            bool mostrar = !string.IsNullOrEmpty(texto);
            if (caixaPrompt.activeSelf != mostrar) caixaPrompt.SetActive(mostrar);
            if (mostrar && txtPrompt.text != texto) txtPrompt.text = texto;
        }

        public void Efeito(string valor, string sub, float alpha)
        {
            txtEfeito.text = valor ?? "";
            txtEfeitoSub.text = sub ?? "";
            grupoEfeito.alpha = alpha;
        }

        public void Overlay(Color cor) => overlay.color = cor;
        public void Vinheta(Color cor) => vinheta.color = cor;

        public float FadeAlpha { get => fade.color.a; set { var c = fade.color; c.a = value; fade.color = c; } }

        public IEnumerator Fade(float alvo, float duracao)
        {
            float ini = FadeAlpha, t = 0;
            while (t < duracao)
            {
                t += Time.unscaledDeltaTime;
                FadeAlpha = Mathf.Lerp(ini, alvo, Mathf.SmoothStep(0, 1, t / duracao));
                yield return null;
            }
            FadeAlpha = alvo;
        }

        // ------------------------------------------------------------------ construção
        public Image Imagem(string nome, Transform pai, Color cor, bool arredondada = false)
        {
            var go = new GameObject(nome, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(pai, false);
            var img = go.GetComponent<Image>();
            img.color = cor;
            if (arredondada && spriteArredondado)
            {
                img.sprite = spriteArredondado;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 0.5f;
            }
            img.raycastTarget = false;
            return img;
        }

        public TextMeshProUGUI Texto(string nome, Transform pai, float tamanho, Color cor, TextAlignmentOptions alin, TMP_FontAsset f = null)
        {
            var go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(pai, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (f ?? fonte) t.font = f ?? fonte;
            t.fontSize = tamanho;
            t.color = cor;
            t.alignment = alin;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        public static void Esticar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Posicionar(RectTransform rt, Vector2 pos, Vector2 tam, bool centro = false)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = centro ? new Vector2(0.5f, 0.5f) : new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = tam;
        }

        static Sprite CriarVinheta()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                float a = Mathf.SmoothStep(0.35f, 1.15f, Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
