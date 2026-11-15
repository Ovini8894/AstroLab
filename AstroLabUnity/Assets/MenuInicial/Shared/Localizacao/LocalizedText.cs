using TMPro;
using UnityEngine;

namespace AstroLab.Localization
{
    /// <summary>
    /// Coloque num objeto com TMP_Text e preencha a chave: o texto acompanha o idioma escolhido.
    /// Para textos com parâmetros, chame <see cref="SetArgs"/>.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("AstroLab/Texto Localizado")]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] string key;
        [Tooltip("Deixar o texto em MAIÚSCULAS (útil para botões).")]
        [SerializeField] bool upperCase;

        object[] args;
        TMP_Text texto;

        public string Key
        {
            get => key;
            set { key = value; Refresh(); }
        }

        public bool UpperCase
        {
            get => upperCase;
            set { upperCase = value; Refresh(); }
        }

        public void SetArgs(params object[] valores) { args = valores; Refresh(); }

        void OnEnable()
        {
            Loc.OnLanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable() => Loc.OnLanguageChanged -= Refresh;

#if UNITY_EDITOR
        void OnValidate() { if (isActiveAndEnabled) Refresh(); }
#endif

        public void Refresh()
        {
            if (string.IsNullOrEmpty(key)) return;
            if (texto == null) texto = GetComponent<TMP_Text>();
            string t = Loc.Get(key, args);
            texto.text = upperCase ? t.ToUpperInvariant() : t;
        }
    }
}
