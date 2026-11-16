using System.Collections;
using AstroLab.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AstroLab.Menu
{
    /// <summary>
    /// Controla o menu inicial: Principal (Iniciar / Opções / Créditos, Sair discreto no canto), Opções e Créditos.
    /// Toda troca de tela tem fade de 0,3 s. Esc / botão B voltam. Iniciar roda o <see cref="LancamentoMenu"/> e
    /// carrega a cena do jogo (<see cref="CenaDoJogo"/>).
    /// </summary>
    public sealed class MenuInicial : MonoBehaviour
    {
        /// <summary>Cena carregada pelo botão Iniciar (precisa estar no Build Settings).</summary>
        public const string CenaDoJogo = "CentroTreinamento";

        public enum Tela { Principal, Opcoes, Creditos, Lancando }

        [Header("Grupos")]
        [SerializeField] CanvasGroup grupoPrincipal;
        [SerializeField] CanvasGroup grupoOpcoes;
        [SerializeField] CanvasGroup grupoCreditos;
        [SerializeField] Image fadePreto;
        [SerializeField] TMP_Text aviso;

        [Header("Botões")]
        [SerializeField] BotaoMenu botaoIniciar;
        [SerializeField] BotaoMenu botaoOpcoes;
        [SerializeField] BotaoMenu botaoCreditos;
        [SerializeField] BotaoMenu botaoSair;

        [Header("Painéis")]
        [SerializeField] PainelOpcoes painelOpcoes;
        [SerializeField] PainelCreditos painelCreditos;
        [SerializeField] LancamentoMenu lancamento;

        public Tela TelaAtual { get; private set; } = Tela.Principal;

        Coroutine troca;
        GameObject ultimoBotaoPrincipal;

        void Awake()
        {
            botaoIniciar.Botao.onClick.AddListener(Iniciar);
            botaoOpcoes.Botao.onClick.AddListener(() => Abrir(Tela.Opcoes));
            botaoCreditos.Botao.onClick.AddListener(() => Abrir(Tela.Creditos));
            botaoSair.Botao.onClick.AddListener(Sair);
            painelOpcoes.AoVoltar += Voltar;
            painelCreditos.AoVoltar += Voltar;

            Mostrar(grupoPrincipal, true, 1f);
            Mostrar(grupoOpcoes, false, 0f);
            Mostrar(grupoCreditos, false, 0f);
            if (aviso) aviso.alpha = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
        }

        IEnumerator Start()
        {
            ultimoBotaoPrincipal = botaoIniciar.gameObject;
            fadePreto.color = new Color(fadePreto.color.r, fadePreto.color.g, fadePreto.color.b, 1f);
            fadePreto.raycastTarget = true;
            yield return null;
            yield return Fade(fadePreto, 1f, 0f, 1.2f);
            fadePreto.raycastTarget = false;
            Selecionar(botaoIniciar.gameObject);
        }

        void Update()
        {
            if (TelaAtual == Tela.Lancando) return;
            var kb = Keyboard.current; var gp = Gamepad.current;
            bool voltar = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame);
            if (voltar && TelaAtual != Tela.Principal) { Voltar(); return; }

            // sem nada selecionado (clicou no vazio): setas/Tab/controle voltam a selecionar o botão padrão
            var es = EventSystem.current;
            if (es && es.currentSelectedGameObject == null)
            {
                bool nav = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                                           kb.tabKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame))
                           || (gp != null && (gp.dpad.ReadValue() != Vector2.zero || gp.leftStick.ReadValue().sqrMagnitude > 0.25f));
                if (nav) FocarTelaAtual();
            }
            else if (es && TelaAtual == Tela.Principal && es.currentSelectedGameObject != null &&
                     es.currentSelectedGameObject.transform.IsChildOf(grupoPrincipal.transform))
                ultimoBotaoPrincipal = es.currentSelectedGameObject;
        }

        // ---------------------------------------------------------------- navegação
        public void Abrir(Tela t)
        {
            if (TelaAtual == Tela.Lancando || t == TelaAtual) return;
            var de = Grupo(TelaAtual); var para = Grupo(t);
            TelaAtual = t;
            if (troca != null) StopCoroutine(troca);
            troca = StartCoroutine(Trocar(de, para));
        }

        public void Voltar()
        {
            if (TelaAtual == Tela.Principal || TelaAtual == Tela.Lancando) return;
            SomMenu.Tocar(SomMenu.Som.Voltar);
            Abrir(Tela.Principal);
        }

        CanvasGroup Grupo(Tela t) => t switch
        {
            Tela.Opcoes => grupoOpcoes,
            Tela.Creditos => grupoCreditos,
            _ => grupoPrincipal,
        };

        IEnumerator Trocar(CanvasGroup de, CanvasGroup para)
        {
            de.interactable = false; de.blocksRaycasts = false;
            float a0 = de.alpha;
            for (float t = 0; t < 0.15f; t += Time.unscaledDeltaTime) { de.alpha = Mathf.Lerp(a0, 0f, t / 0.15f); yield return null; }
            Mostrar(de, false, 0f);
            para.gameObject.SetActive(true);
            para.alpha = 0f;
            var rt = (RectTransform)para.transform;
            for (float t = 0; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                float p = t / 0.18f;
                para.alpha = p;
                rt.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, 1f - (1f - p) * (1f - p));
                yield return null;
            }
            rt.localScale = Vector3.one;
            Mostrar(para, true, 1f);
            FocarTelaAtual();
            troca = null;
        }

        void FocarTelaAtual()
        {
            switch (TelaAtual)
            {
                case Tela.Principal: Selecionar(ultimoBotaoPrincipal ? ultimoBotaoPrincipal : botaoIniciar.gameObject); break;
                case Tela.Opcoes: painelOpcoes.Focar(); break;
                case Tela.Creditos: painelCreditos.Focar(); break;
            }
        }

        static void Selecionar(GameObject go)
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(go);
        }

        static void Mostrar(CanvasGroup g, bool visivel, float alpha)
        {
            g.alpha = alpha;
            g.interactable = visivel;
            g.blocksRaycasts = visivel;
            g.gameObject.SetActive(visivel);
        }

        // ---------------------------------------------------------------- ações
        void Iniciar()
        {
            if (TelaAtual != Tela.Principal) return;
            TelaAtual = Tela.Lancando;
            grupoPrincipal.interactable = false; grupoPrincipal.blocksRaycasts = false;
            Selecionar(null);
            StartCoroutine(Fade(grupoPrincipal, 1f, 0f, 0.45f));
            Debug.Log("[Menu] Iniciar: lançamento a caminho de " + CenaDoJogo);
            lancamento.Iniciar(() => StartCoroutine(CarregarJogo()));
        }

        IEnumerator CarregarJogo()
        {
            // fade PRETO total, segura um instante no preto e só então carrega
            fadePreto.raycastTarget = true;
            fadePreto.color = new Color(0f, 0f, 0f, fadePreto.color.a);
            yield return Fade(fadePreto, 0f, 1f, 0.9f);
            yield return new WaitForSecondsRealtime(0.3f);
            if (!Application.CanStreamedLevelBeLoaded(CenaDoJogo))
            {
                Debug.LogWarning($"[Menu] A cena \"{CenaDoJogo}\" não está no Build Settings (File > Build Profiles > Scenes). " +
                                 "O menu continua aberto.");
                yield return new WaitForSecondsRealtime(0.6f);                  // deixa o foguete terminar sob o preto
                lancamento.Restaurar();
                fadePreto.color = new Color(0.027f, 0.024f, 0.1f, 1f);
                Mostrar(grupoPrincipal, true, 0f);
                TelaAtual = Tela.Principal;
                yield return Fade(fadePreto, 1f, 0f, 0.6f);
                fadePreto.raycastTarget = false;
                yield return Fade(grupoPrincipal, 0f, 1f, 0.3f);
                Selecionar(botaoIniciar.gameObject);
                StartCoroutine(MostrarAviso("menu.cena_ausente"));
                yield break;
            }
            // a tela preta sobrevive à troca de cena e some sozinha quando a cena nova estiver pronta
            TransicaoPreta.Criar(1.0f);
            var op = SceneManager.LoadSceneAsync(CenaDoJogo);
            while (!op.isDone) yield return null;
        }

        IEnumerator MostrarAviso(string chave)
        {
            if (!aviso) yield break;
            aviso.text = Loc.Get(chave);
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) { aviso.alpha = t / 0.25f; yield return null; }
            aviso.alpha = 1f;
            yield return new WaitForSecondsRealtime(3.5f);
            for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime) { aviso.alpha = 1f - t / 0.6f; yield return null; }
            aviso.alpha = 0f;
        }

        void Sair()
        {
            Debug.Log("[Menu] Sair");
            StartCoroutine(SairRotina());
        }

        IEnumerator SairRotina()
        {
            TelaAtual = Tela.Lancando;
            fadePreto.raycastTarget = true;
            yield return Fade(fadePreto, 0f, 1f, 0.35f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- fades
        static IEnumerator Fade(Image img, float de, float para, float dur)
        {
            var c = img.color;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                c.a = Mathf.Lerp(de, para, Mathf.SmoothStep(0f, 1f, t / dur));
                img.color = c;
                yield return null;
            }
            c.a = para; img.color = c;
        }

        static IEnumerator Fade(CanvasGroup g, float de, float para, float dur)
        {
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                g.alpha = Mathf.Lerp(de, para, t / dur);
                yield return null;
            }
            g.alpha = para;
        }
    }
}
