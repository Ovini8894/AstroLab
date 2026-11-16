using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroLab.Menu
{
    /// <summary>Linha com slider e valor formatado (ex.: "80%", "1.0x", "75°").</summary>
    public sealed class SliderOpcao : MonoBehaviour
    {
        [SerializeField] Slider slider;
        [SerializeField] TMP_Text valor;

        public Func<float, string> Formatar = v => Mathf.RoundToInt(v).ToString();
        public event Action<float> AoMudar;
        public Slider Slider => slider;

        float ultimoSom;

        public void Configurar(TemaMenu tema, Slider slider, Image fundo, Image borda, TMP_Text valor)
        {
            this.slider = slider; this.valor = valor;
            gameObject.AddComponent<LinhaOpcaoVisual>().Configurar(tema, fundo, borda);
            Ligar();
        }

        void Awake() => Ligar();

        bool ligado;

        void Ligar()
        {
            if (ligado || slider == null) return;
            ligado = true;
            slider.onValueChanged.AddListener(v =>
            {
                Atualizar();
                if (Time.unscaledTime - ultimoSom > 0.06f) { SomMenu.Tocar(SomMenu.Som.Tique); ultimoSom = Time.unscaledTime; }
                AoMudar?.Invoke(v);
            });
        }

        /// <summary>Define o valor sem disparar <see cref="AoMudar"/>.</summary>
        public void DefinirValor(float v)
        {
            slider.SetValueWithoutNotify(v);
            Atualizar();
        }

        void Atualizar() { if (valor) valor.text = Formatar(slider.value); }
    }
}
