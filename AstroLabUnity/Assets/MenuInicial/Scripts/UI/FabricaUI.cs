using AstroLab.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroLab.Menu
{
    /// <summary>Monta os elementos de interface do menu no estilo do <see cref="TemaMenu"/> (usado no Editor e em runtime).</summary>
    public static class FabricaUI
    {
        public static RectTransform Rect(string nome, Transform pai)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(pai, false);
            return rt;
        }

        public static void Esticar(RectTransform rt, float margem = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margem, margem); rt.offsetMax = new Vector2(-margem, -margem);
        }

        public static Image Imagem(string nome, Transform pai, Sprite sprite, Color cor, bool fatiada = true)
        {
            var rt = Rect(nome, pai);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = fatiada && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = cor;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Texto(string nome, Transform pai, TemaMenu tema, string chave, float tamanho,
                                            TextAlignmentOptions alinhamento = TextAlignmentOptions.Center, bool maiusculas = false, bool titulo = false)
        {
            var rt = Rect(nome, pai);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = titulo ? tema.fonteTitulo : tema.fonteTexto;
            t.fontSize = tamanho;
            t.alignment = alinhamento;
            t.color = tema.texto;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            if (!string.IsNullOrEmpty(chave))
            {
                var lt = rt.gameObject.AddComponent<LocalizedText>();
                lt.UpperCase = maiusculas;
                lt.Key = chave;
            }
            return t;
        }

        /// <summary>Painel translúcido com borda (fundo de Opções/Créditos).</summary>
        public static RectTransform Painel(string nome, Transform pai, TemaMenu tema, Vector2 tamanho)
        {
            var rt = Rect(nome, pai);
            rt.sizeDelta = tamanho;
            var halo = Imagem("Halo", rt, tema.brilho, new Color(tema.destaque.r, tema.destaque.g, tema.destaque.b, 0.10f));
            Esticar(halo.rectTransform, -28f);
            var fundo = Imagem("Fundo", rt, tema.arredondado, tema.painel);
            Esticar(fundo.rectTransform);
            fundo.raycastTarget = true;                                 // bloqueia cliques no que está atrás
            var borda = Imagem("Borda", rt, tema.borda, tema.painelBorda);
            Esticar(borda.rectTransform);
            return rt;
        }

        /// <summary>Botão azul translúcido com borda destacada e animação de hover (<see cref="BotaoMenu"/>).</summary>
        public static BotaoMenu Botao(string nome, Transform pai, TemaMenu tema, string chave, Vector2 tamanho, float fonte = 30f)
        {
            var rt = Rect(nome, pai);
            rt.sizeDelta = tamanho;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = tamanho.x; le.preferredHeight = tamanho.y;

            var visual = Rect("Visual", rt); Esticar(visual);
            var halo = Imagem("Halo", visual, tema.brilho, new Color(1, 1, 1, 0));
            Esticar(halo.rectTransform, -22f);
            var fundo = Imagem("Fundo", visual, tema.arredondado, tema.fundo);
            Esticar(fundo.rectTransform);
            fundo.raycastTarget = true;

            var mascara = Rect("Mascara", visual); Esticar(mascara, 2f);
            mascara.gameObject.AddComponent<RectMask2D>();
            var faixa = Imagem("FaixaBrilho", mascara, tema.faixaBrilho, new Color(1, 1, 1, 0), false);
            faixa.rectTransform.anchorMin = new Vector2(0, 0); faixa.rectTransform.anchorMax = new Vector2(0, 1);
            faixa.rectTransform.sizeDelta = new Vector2(tamanho.y * 1.2f, 30f);
            faixa.rectTransform.localRotation = Quaternion.Euler(0, 0, -18f);

            var marcadorE = Imagem("MarcadorEsq", visual, tema.seta, new Color(1, 1, 1, 0), false);
            marcadorE.rectTransform.anchorMin = marcadorE.rectTransform.anchorMax = new Vector2(0, 0.5f);
            marcadorE.rectTransform.sizeDelta = new Vector2(14, 18);
            marcadorE.rectTransform.anchoredPosition = new Vector2(26, 0);
            var marcadorD = Imagem("MarcadorDir", visual, tema.seta, new Color(1, 1, 1, 0), false);
            marcadorD.rectTransform.anchorMin = marcadorD.rectTransform.anchorMax = new Vector2(1, 0.5f);
            marcadorD.rectTransform.sizeDelta = new Vector2(14, 18);
            marcadorD.rectTransform.anchoredPosition = new Vector2(-26, 0);
            marcadorD.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);

            var borda = Imagem("Borda", visual, tema.borda, tema.bordaCor);
            Esticar(borda.rectTransform);

            var texto = Texto("Texto", visual, tema, chave, fonte, TextAlignmentOptions.Center, true);
            Esticar(texto.rectTransform);
            texto.characterSpacing = 8f;
            texto.fontStyle = FontStyles.Bold;

            var botao = rt.gameObject.AddComponent<Button>();
            botao.transition = Selectable.Transition.None;
            botao.targetGraphic = fundo;

            var bm = rt.gameObject.AddComponent<BotaoMenu>();
            bm.Configurar(tema, visual, fundo, borda, halo, faixa, texto, marcadorE, marcadorD);
            return bm;
        }

        /// <summary>Linha "rótulo .......... ◀ valor ▶" das Opções.</summary>
        public static SeletorOpcao Seletor(Transform pai, TemaMenu tema, string chaveRotulo, float altura = 58f)
        {
            var linha = Linha("Linha_" + chaveRotulo, pai, tema, chaveRotulo, altura, out var fundo, out var borda);
            var sel = linha.gameObject.AddComponent<SeletorOpcao>();

            var area = Rect("Controle", linha);
            area.anchorMin = new Vector2(0.52f, 0); area.anchorMax = new Vector2(1, 1);
            area.offsetMin = new Vector2(0, 6); area.offsetMax = new Vector2(-14, -6);

            var esq = BotaoSeta("Anterior", area, tema, true);
            var dir = BotaoSeta("Proximo", area, tema, false);
            var valor = Texto("Valor", area, tema, null, 24f);
            valor.rectTransform.anchorMin = Vector2.zero; valor.rectTransform.anchorMax = Vector2.one;
            valor.rectTransform.offsetMin = new Vector2(44, 0); valor.rectTransform.offsetMax = new Vector2(-44, 0);
            valor.color = tema.textoHover;
            valor.overflowMode = TextOverflowModes.Ellipsis;

            sel.Configurar(tema, fundo, borda, valor, esq, dir);
            return sel;
        }

        /// <summary>Linha "rótulo ===o======= 75" das Opções.</summary>
        public static SliderOpcao Slider(Transform pai, TemaMenu tema, string chaveRotulo, float min, float max, bool inteiro, float altura = 58f)
        {
            var linha = Linha("Linha_" + chaveRotulo, pai, tema, chaveRotulo, altura, out var fundo, out var borda);

            var area = Rect("Controle", linha);
            area.anchorMin = new Vector2(0.52f, 0); area.anchorMax = new Vector2(1, 1);
            area.offsetMin = new Vector2(8, 0); area.offsetMax = new Vector2(-96, 0);

            var trilho = Imagem("Trilho", area, tema.arredondado, new Color(0.6f, 0.8f, 1f, 0.22f));
            trilho.rectTransform.anchorMin = new Vector2(0, 0.5f); trilho.rectTransform.anchorMax = new Vector2(1, 0.5f);
            trilho.rectTransform.sizeDelta = new Vector2(0, 8);

            var preenchArea = Rect("AreaPreenchimento", area);
            preenchArea.anchorMin = new Vector2(0, 0.5f); preenchArea.anchorMax = new Vector2(1, 0.5f);
            preenchArea.sizeDelta = new Vector2(0, 8);
            var preench = Imagem("Preenchimento", preenchArea, tema.arredondado, tema.destaque);
            preench.rectTransform.sizeDelta = Vector2.zero;

            var alcaArea = Rect("AreaAlca", area); Esticar(alcaArea);
            alcaArea.offsetMin = new Vector2(10, 0); alcaArea.offsetMax = new Vector2(-10, 0);
            var alca = Imagem("Alca", alcaArea, tema.arredondado, Color.white);
            alca.rectTransform.sizeDelta = new Vector2(20, 30);
            alca.raycastTarget = true;
            var alcaBorda = Imagem("Borda", alca.rectTransform, tema.borda, tema.destaque);
            Esticar(alcaBorda.rectTransform);

            var s = linha.gameObject.AddComponent<UnityEngine.UI.Slider>();
            s.fillRect = preench.rectTransform;
            s.handleRect = alca.rectTransform;
            s.targetGraphic = alca;
            s.transition = Selectable.Transition.None;
            s.minValue = min; s.maxValue = max; s.wholeNumbers = inteiro;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;

            var valor = Texto("Valor", linha, tema, null, 24f, TextAlignmentOptions.Right);
            valor.rectTransform.anchorMin = new Vector2(1, 0); valor.rectTransform.anchorMax = new Vector2(1, 1);
            valor.rectTransform.pivot = new Vector2(1, 0.5f);
            valor.rectTransform.sizeDelta = new Vector2(80, 0);
            valor.rectTransform.anchoredPosition = new Vector2(-14, 0);
            valor.color = tema.textoHover;

            var so = linha.gameObject.AddComponent<SliderOpcao>();
            so.Configurar(tema, s, fundo, borda, valor);
            return so;
        }

        /// <summary>Linha só de leitura: "Mover ........ W A S D".</summary>
        public static void LinhaInfo(Transform pai, TemaMenu tema, string chaveRotulo, string valor, float altura = 46f)
        {
            var linha = Linha("Info_" + chaveRotulo, pai, tema, chaveRotulo, altura, out var fundo, out var borda);
            fundo.color = new Color(tema.fundo.r, tema.fundo.g, tema.fundo.b, tema.fundo.a * 0.4f);
            borda.color = new Color(tema.bordaCor.r, tema.bordaCor.g, tema.bordaCor.b, 0.18f);
            var t = Texto("Tecla", linha, tema, null, 22f, TextAlignmentOptions.Right);
            t.rectTransform.anchorMin = new Vector2(0.5f, 0); t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.offsetMin = Vector2.zero; t.rectTransform.offsetMax = new Vector2(-22, 0);
            t.color = tema.destaque;
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 4f;
            if (valor != null && valor.StartsWith("@"))
            {
                var lt = t.gameObject.AddComponent<LocalizedText>();
                lt.UpperCase = true;
                lt.Key = valor.Substring(1);
            }
            else t.text = valor;
        }

        static RectTransform Linha(string nome, Transform pai, TemaMenu tema, string chaveRotulo, float altura, out Image fundo, out Image borda)
        {
            var linha = Rect(nome, pai);
            linha.sizeDelta = new Vector2(0, altura);
            var le = linha.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = altura; le.minHeight = altura;
            fundo = Imagem("Fundo", linha, tema.arredondado, new Color(tema.fundo.r, tema.fundo.g, tema.fundo.b, tema.fundo.a * 0.7f));
            Esticar(fundo.rectTransform);
            fundo.raycastTarget = true;
            borda = Imagem("Borda", linha, tema.borda, new Color(tema.bordaCor.r, tema.bordaCor.g, tema.bordaCor.b, 0.35f));
            Esticar(borda.rectTransform);
            var rotulo = Texto("Rotulo", linha, tema, chaveRotulo, 24f, TextAlignmentOptions.Left);
            rotulo.rectTransform.anchorMin = new Vector2(0, 0); rotulo.rectTransform.anchorMax = new Vector2(0.52f, 1);
            rotulo.rectTransform.offsetMin = new Vector2(22, 0); rotulo.rectTransform.offsetMax = Vector2.zero;
            rotulo.color = tema.texto;
            rotulo.overflowMode = TextOverflowModes.Ellipsis;
            return linha;
        }

        static Button BotaoSeta(string nome, Transform pai, TemaMenu tema, bool esquerda)
        {
            var rt = Rect(nome, pai);
            rt.anchorMin = rt.anchorMax = new Vector2(esquerda ? 0 : 1, 0.5f);
            rt.pivot = new Vector2(esquerda ? 0 : 1, 0.5f);
            rt.sizeDelta = new Vector2(40, 40);
            var fundo = Imagem("Fundo", rt, tema.arredondado, new Color(tema.fundo.r, tema.fundo.g, tema.fundo.b, 0.35f));
            Esticar(fundo.rectTransform);
            fundo.raycastTarget = true;
            var borda = Imagem("Borda", rt, tema.borda, tema.bordaCor);
            Esticar(borda.rectTransform);
            var seta = Imagem("Seta", rt, tema.seta, tema.destaque, false);
            seta.rectTransform.sizeDelta = new Vector2(12, 16);
            if (esquerda) seta.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fundo;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var cores = b.colors;
            cores.normalColor = Color.white; cores.highlightedColor = new Color(1.6f, 1.8f, 2f, 1f);
            cores.pressedColor = new Color(2f, 2f, 2f, 1f); cores.selectedColor = Color.white;
            cores.colorMultiplier = 2f; cores.fadeDuration = 0.08f;
            b.colors = cores;
            return b;
        }

        public static VerticalLayoutGroup ColunaVertical(RectTransform rt, float espaco, RectOffset margem = null)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = espaco;
            v.padding = margem ?? new RectOffset();
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }
    }
}
