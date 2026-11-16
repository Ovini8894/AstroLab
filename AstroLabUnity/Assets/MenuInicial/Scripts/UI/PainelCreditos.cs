using System;
using AstroLab.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstroLab.Menu
{
    /// <summary>
    /// Créditos lidos de Resources/AstroLabMenu/creditos.json (fácil de editar sem abrir o Unity).
    /// "titulo" e "funcao" podem ser chaves de idioma (Loc) ou texto puro.
    /// </summary>
    public sealed class PainelCreditos : MonoBehaviour
    {
        public const string ArquivoCreditos = "AstroLabMenu/creditos";

        [SerializeField] TemaMenu tema;
        [SerializeField] RectTransform lista;
        [SerializeField] BotaoMenu botaoVoltar;

        public event Action AoVoltar;

        [Serializable] class Pessoa { public string nome; public string funcao; }
        [Serializable] class Secao { public string titulo; public Pessoa[] pessoas; }
        [Serializable] class Arquivo { public Secao[] secoes; }

        bool montado;

        public void Configurar(TemaMenu tema, RectTransform lista, BotaoMenu voltar)
        {
            this.tema = tema; this.lista = lista; botaoVoltar = voltar;
        }

        void Awake()
        {
            botaoVoltar.Botao.onClick.AddListener(() => AoVoltar?.Invoke());
            Montar();
        }

        public void Focar()
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(botaoVoltar.gameObject);
        }

        void Montar()
        {
            if (montado) return;
            montado = true;
            var ta = Resources.Load<TextAsset>(ArquivoCreditos);
            if (ta == null) { Debug.LogWarning($"[Menu] Créditos não encontrados em Resources/{ArquivoCreditos}.json"); return; }
            Arquivo arq;
            try { arq = JsonUtility.FromJson<Arquivo>(ta.text); }
            catch (Exception e) { Debug.LogError($"[Menu] creditos.json inválido: {e.Message}"); return; }
            if (arq?.secoes == null) return;

            bool primeira = true;
            foreach (var s in arq.secoes)
            {
                if (!primeira) Espaco(18f);
                primeira = false;
                var cab = Texto("Secao", s.titulo, 22f, tema.destaque, true);
                cab.characterSpacing = 10f;
                cab.fontStyle = FontStyles.Bold;
                Separador();
                if (s.pessoas == null) continue;
                foreach (var p in s.pessoas)
                {
                    var nome = Texto("Nome", null, 34f, tema.textoHover, false);
                    nome.text = p.nome;
                    nome.fontStyle = FontStyles.Bold;
                    var f = Texto("Funcao", p.funcao, 21f, tema.textoSecundario, false);
                    f.margin = new Vector4(0, -4, 0, 6);
                }
            }
        }

        TextMeshProUGUI Texto(string nome, string chaveOuTexto, float tamanho, Color cor, bool maiusculas)
        {
            bool ehChave = !string.IsNullOrEmpty(chaveOuTexto) && Loc.Has(chaveOuTexto);
            var t = FabricaUI.Texto(nome, lista, tema, ehChave ? chaveOuTexto : null, tamanho, TextAlignmentOptions.Center, maiusculas);
            if (!ehChave && chaveOuTexto != null) t.text = maiusculas ? chaveOuTexto.ToUpperInvariant() : chaveOuTexto;
            t.color = cor;
            t.textWrappingMode = TextWrappingModes.Normal;
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = tamanho * 1.25f;
            return t;
        }

        void Separador()
        {
            var img = FabricaUI.Imagem("Separador", lista, tema.faixaBrilho, new Color(tema.destaque.r, tema.destaque.g, tema.destaque.b, 0.6f), false);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 2f;
            Espaco(6f);
        }

        void Espaco(float h)
        {
            var rt = FabricaUI.Rect("Espaco", lista);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
        }
    }
}
