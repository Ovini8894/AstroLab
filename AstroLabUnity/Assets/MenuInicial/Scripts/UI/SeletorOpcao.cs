using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstroLab.Menu
{
    /// <summary>Seletor "◀ valor ▶": clique nas setas, ou setas esquerda/direita do teclado/controle com a linha selecionada.</summary>
    public sealed class SeletorOpcao : Selectable, ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] TMP_Text valor;
        [SerializeField] Button anterior, proximo;

        readonly List<string> opcoes = new List<string>();
        int indice;

        public event Action<int> AoMudar;
        public int Indice => indice;

        public void Configurar(TemaMenu tema, Image fundo, Image borda, TMP_Text valor, Button anterior, Button proximo)
        {
            this.valor = valor; this.anterior = anterior; this.proximo = proximo;
            transition = Transition.None;
            targetGraphic = fundo;
            gameObject.AddComponent<LinhaOpcaoVisual>().Configurar(tema, fundo, borda);
            Ligar();
        }

        bool ligado;

        protected override void Awake()
        {
            base.Awake();
            Ligar();
        }

        void Ligar()
        {
            if (ligado || anterior == null || proximo == null) return;
            ligado = true;
            anterior.onClick.AddListener(() => Passo(-1));
            proximo.onClick.AddListener(() => Passo(+1));
        }

        /// <summary>Troca a lista de opções sem disparar <see cref="AoMudar"/>.</summary>
        public void DefinirOpcoes(IList<string> lista, int selecionada)
        {
            opcoes.Clear();
            opcoes.AddRange(lista);
            indice = Mathf.Clamp(selecionada, 0, Mathf.Max(0, opcoes.Count - 1));
            Atualizar();
        }

        public void DefinirIndice(int i) { indice = Mathf.Clamp(i, 0, Mathf.Max(0, opcoes.Count - 1)); Atualizar(); }

        void Passo(int d)
        {
            if (opcoes.Count == 0) return;
            indice = (indice + d + opcoes.Count) % opcoes.Count;
            Atualizar();
            SomMenu.Tocar(SomMenu.Som.Clique);
            AoMudar?.Invoke(indice);
        }

        void Atualizar()
        {
            if (valor) valor.text = opcoes.Count > 0 ? opcoes[indice] : "-";
        }

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) { Passo(-1); e.Use(); return; }
            if (e.moveDir == MoveDirection.Right) { Passo(+1); e.Use(); return; }
            base.OnMove(e);
        }

        public void OnSubmit(BaseEventData e) => Passo(+1);

        public void OnPointerClick(PointerEventData e)
        {
            // clique no meio da linha (fora das setas, que têm o próprio botão) avança — prático para Ligado/Desligado
            if (e.button == PointerEventData.InputButton.Left) Passo(+1);
        }
    }
}
