using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstroLab.Localization
{
    /// <summary>Um idioma disponível no jogo.</summary>
    public readonly struct LanguageInfo
    {
        public readonly string Code;        // "pt-BR", "en", "es"
        public readonly string NativeName;  // "Português (Brasil)", "English", "Español"
        public LanguageInfo(string code, string nativeName) { Code = code; NativeName = nativeName; }
    }

    /// <summary>
    /// Sistema de idiomas leve (sem o pacote Unity Localization).
    /// Textos ficam em JSON dentro de qualquer pasta Resources, no caminho <c>Resources/AstroLabLoc/&lt;idioma&gt;/*.json</c>.
    /// Cada equipe pode ter o próprio arquivo (ex.: Assets/CentroTreinamento/Resources/AstroLabLoc/pt-BR/npc.json):
    /// todos os arquivos do idioma são juntados. Formato:
    /// <code>{ "itens": [ { "chave": "menu.iniciar", "texto": "Iniciar" } ] }</code>
    /// Chave ausente no idioma atual cai para pt-BR e, se também faltar, devolve a própria chave.
    /// </summary>
    public static class Loc
    {
        public const string DefaultLanguage = "pt-BR";
        public const string ResourcesFolder = "AstroLabLoc";
        const string PrefKey = "astrolab.language";

        /// <summary>Idiomas oferecidos no menu (sem chinês, por decisão do time).</summary>
        public static readonly LanguageInfo[] Languages =
        {
            new LanguageInfo("pt-BR", "Português (Brasil)"),
            new LanguageInfo("en", "English"),
            new LanguageInfo("es", "Español"),
        };

        /// <summary>Disparado depois que o idioma muda (os textos já estão recarregados).</summary>
        public static event Action OnLanguageChanged;

        static string idioma;
        static Dictionary<string, string> atual;
        static Dictionary<string, string> padrao;

        /// <summary>Código do idioma atual. Ao alterar, salva em PlayerPrefs e dispara <see cref="OnLanguageChanged"/>.</summary>
        public static string Language
        {
            get
            {
                if (idioma == null)
                {
                    idioma = PlayerPrefs.GetString(PrefKey, DefaultLanguage);
                    if (IndexOf(idioma) < 0) idioma = DefaultLanguage;
                }
                return idioma;
            }
            set
            {
                if (IndexOf(value) < 0) { Debug.LogWarning($"[Loc] Idioma desconhecido: {value}"); return; }
                if (value == Language) return;
                idioma = value;
                PlayerPrefs.SetString(PrefKey, value);
                PlayerPrefs.Save();
                atual = null;
                OnLanguageChanged?.Invoke();
            }
        }

        /// <summary>Posição do idioma em <see cref="Languages"/> (-1 se não existir).</summary>
        public static int IndexOf(string code)
        {
            for (int i = 0; i < Languages.Length; i++)
                if (string.Equals(Languages[i].Code, code, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>Texto da chave no idioma atual.</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            Garantir();
            if (atual.TryGetValue(key, out var t)) return t;
            if (padrao.TryGetValue(key, out t)) return t;
            return key;
        }

        /// <summary>Texto com parâmetros no estilo string.Format: "Faltam {0} peças".</summary>
        public static string Get(string key, params object[] args)
        {
            string t = Get(key);
            try { return args == null || args.Length == 0 ? t : string.Format(t, args); }
            catch (FormatException) { return t; }
        }

        /// <summary>A chave existe no idioma atual ou no padrão?</summary>
        public static bool Has(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            Garantir();
            return atual.ContainsKey(key) || padrao.ContainsKey(key);
        }

        /// <summary>Relê os JSON (útil depois de editar os arquivos com o jogo rodando).</summary>
        public static void Reload()
        {
            atual = null; padrao = null;
            OnLanguageChanged?.Invoke();
        }

        static void Garantir()
        {
            padrao ??= Carregar(DefaultLanguage);
            atual ??= Language == DefaultLanguage ? padrao : Carregar(Language);
        }

        [Serializable] class Item { public string chave; public string texto; }
        [Serializable] class Arquivo { public Item[] itens; }

        static Dictionary<string, string> Carregar(string code)
        {
            var d = new Dictionary<string, string>(256);
            foreach (var ta in Resources.LoadAll<TextAsset>($"{ResourcesFolder}/{code}"))
            {
                Arquivo arq;
                try { arq = JsonUtility.FromJson<Arquivo>(ta.text); }
                catch (Exception e) { Debug.LogError($"[Loc] JSON inválido em {code}/{ta.name}: {e.Message}"); continue; }
                if (arq?.itens == null) continue;
                foreach (var it in arq.itens)
                {
                    if (string.IsNullOrEmpty(it.chave)) continue;
                    if (d.ContainsKey(it.chave)) Debug.LogWarning($"[Loc] Chave repetida '{it.chave}' em {code}/{ta.name} (vale a última).");
                    d[it.chave] = it.texto ?? string.Empty;
                }
            }
            return d;
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void LimparAoEntrarNoPlay() { idioma = null; atual = null; padrao = null; OnLanguageChanged = null; }
#endif
    }
}
