using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstroLab.Menu
{
    /// <summary>Realce das linhas das Opções (hover do mouse ou seleção por teclado/controle).</summary>
    public sealed class LinhaOpcaoVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] TemaMenu tema;
        [SerializeField] Image fundo, borda;
        bool dentro, selecionado;
        float t;

        public void Configurar(TemaMenu tema, Image fundo, Image borda) { this.tema = tema; this.fundo = fundo; this.borda = borda; }

        public void OnPointerEnter(PointerEventData e)
        {
            dentro = true;
            var sel = GetComponent<Selectable>();
            if (sel && EventSystem.current && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnPointerExit(PointerEventData e) => dentro = false;
        public void OnSelect(BaseEventData e) { if (!selecionado) SomMenu.Tocar(SomMenu.Som.Hover); selecionado = true; }
        public void OnDeselect(BaseEventData e) => selecionado = false;
        void OnDisable() { dentro = selecionado = false; t = 0f; Aplicar(); }

        void Update()
        {
            t = Mathf.MoveTowards(t, dentro || selecionado ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            Aplicar();
        }

        void Aplicar()
        {
            if (!tema || !fundo || !borda) return;
            var f0 = tema.fundo; f0.a *= 0.7f;
            fundo.color = Color.Lerp(f0, tema.fundoHover, t);
            var b0 = tema.bordaCor; b0.a = 0.35f;
            borda.color = Color.Lerp(b0, tema.bordaHover, t);
        }
    }
}
