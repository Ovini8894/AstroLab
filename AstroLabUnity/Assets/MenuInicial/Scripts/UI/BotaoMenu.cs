using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstroLab.Menu
{
    /// <summary>
    /// Animação do botão do menu: no hover/seleção o fundo azul acende, a borda fica ciano e brilhante,
    /// aparece um halo, as setinhas laterais entram, uma faixa de luz atravessa o botão e ele cresce um pouco.
    /// Mouse e teclado/controle usam o mesmo estado (passar o mouse seleciona o botão).
    /// Tudo em tempo não escalado (funciona com o jogo pausado).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class BotaoMenu : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
                                    IPointerDownHandler, IPointerUpHandler, ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] TemaMenu tema;
        [SerializeField] RectTransform visual;
        [SerializeField] Image fundo, borda, halo, faixa, marcadorEsq, marcadorDir;
        [SerializeField] TMP_Text texto;
        [SerializeField] float escalaHover = 1.045f;

        Button botao;
        bool mouseDentro, selecionado, pressionado;
        float t;              // 0 = repouso, 1 = hover
        float tPress;
        float faixaT = 2f;    // progresso da faixa de luz (>1 = parada)
        float espacoBase;

        public Button Botao => botao != null ? botao : botao = GetComponent<Button>();

        public void Configurar(TemaMenu tema, RectTransform visual, Image fundo, Image borda, Image halo, Image faixa,
                               TMP_Text texto, Image marcadorEsq, Image marcadorDir)
        {
            this.tema = tema; this.visual = visual; this.fundo = fundo; this.borda = borda; this.halo = halo;
            this.faixa = faixa; this.texto = texto; this.marcadorEsq = marcadorEsq; this.marcadorDir = marcadorDir;
        }

        void Awake()
        {
            botao = GetComponent<Button>();
            espacoBase = texto ? texto.characterSpacing : 0f;
        }

        void OnDisable()
        {
            mouseDentro = selecionado = pressionado = false;
            t = tPress = 0f; faixaT = 2f;
            Aplicar();
        }

        bool Ativo => mouseDentro || selecionado;

        public void OnPointerEnter(PointerEventData e)
        {
            if (!Botao.interactable) return;
            mouseDentro = true;
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
            else Entrou();
        }

        public void OnPointerExit(PointerEventData e) { mouseDentro = false; pressionado = false; }

        public void OnSelect(BaseEventData e)
        {
            if (!selecionado) { selecionado = true; Entrou(); }
        }

        public void OnDeselect(BaseEventData e) => selecionado = false;
        public void OnPointerDown(PointerEventData e) { if (Botao.interactable) pressionado = true; }
        public void OnPointerUp(PointerEventData e) => pressionado = false;
        public void OnPointerClick(PointerEventData e) { if (Botao.interactable) Clicou(); }
        public void OnSubmit(BaseEventData e) { if (Botao.interactable) { tPress = 1f; Clicou(); } }

        void Entrou()
        {
            faixaT = 0f;
            SomMenu.Tocar(SomMenu.Som.Hover);
        }

        void Clicou()
        {
            tPress = 1f;
            SomMenu.Tocar(SomMenu.Som.Clique);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t = Mathf.MoveTowards(t, Ativo && Botao.interactable ? 1f : 0f, dt * 7f);
            tPress = Mathf.MoveTowards(tPress, pressionado ? 1f : 0f, dt * (pressionado ? 20f : 5f));
            if (faixaT <= 1.2f) faixaT += dt * 1.9f;
            Aplicar();
        }

        void Aplicar()
        {
            if (tema == null || visual == null) return;
            float e = 1f - (1f - t) * (1f - t);                       // ease-out
            float pulso = Ativo ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f) : 0f;

            float escala = Mathf.Lerp(1f, escalaHover, e) * Mathf.Lerp(1f, 0.97f, tPress);
            visual.localScale = new Vector3(escala, escala, 1f);

            var corFundo = Color.Lerp(tema.fundo, tema.fundoHover, e);
            fundo.color = Color.Lerp(corFundo, tema.fundoPressionado, tPress);
            var corBorda = Color.Lerp(tema.bordaCor, tema.bordaHover, e);
            borda.color = Color.Lerp(corBorda, Color.white, tPress * 0.6f + pulso * 0.15f * e);
            var h = tema.brilhoHover; h.a *= e * (0.75f + 0.25f * pulso) + tPress * 0.3f;
            halo.color = h;

            if (texto)
            {
                texto.color = Color.Lerp(tema.texto, tema.textoHover, e);
                texto.characterSpacing = espacoBase + 4f * e;
            }

            var cm = tema.destaque; cm.a = e;
            if (marcadorEsq)
            {
                marcadorEsq.color = cm;
                marcadorEsq.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(8f, 26f, e) + pulso * 3f * e, 0f);
            }
            if (marcadorDir)
            {
                marcadorDir.color = cm;
                marcadorDir.rectTransform.anchoredPosition = new Vector2(-Mathf.Lerp(8f, 26f, e) - pulso * 3f * e, 0f);
            }

            if (faixa)
            {
                float w = ((RectTransform)faixa.rectTransform.parent).rect.width;
                float p = Mathf.Clamp01(faixaT);
                faixa.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-80f, w + 80f, p), 0f);
                float a = faixaT <= 1f ? Mathf.Sin(p * Mathf.PI) * 0.55f : 0f;
                faixa.color = new Color(1f, 1f, 1f, a);
            }
        }
    }
}
