using System.Collections.Generic;
using System.Linq;
using AstroLab.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AstroLab.Nave
{
    public enum FaseNave { Plataforma, Orbita }

    // Funcao real dos controles e checklist da fase. Na PLATAFORMA a nave fica em pe (nariz para cima) e o
    // jogador energiza os sistemas para o lancamento; em ORBITA ela fica deitada com a baia voltada para a Terra.
    public class SistemasNave : MonoBehaviour
    {
        public FaseNave fase = FaseNave.Orbita;

        /// <summary>Quem carrega a cena (ex.: o mapa, na White Room) define a fase antes: SistemasNave.FaseInicial = FaseNave.Plataforma.</summary>
        public static FaseNave? FaseInicial;

        /// <summary>Cena para onde o jogador volta ao segurar M (o mapa pode trocar antes de carregar a nave).</summary>
        public static string CenaRetorno = "CentroTreinamento";
        float segurandoM;
        public Transform raizNave;              // gira conforme a fase (antes do static batching)
        public GameObject[] combinarEstaticos;  // Interior + Kit: combinados em runtime depois de posicionar
        public Renderer telas;                  // NI_Telas (MEDS)
        public Renderer luzesTeto;              // NI_Luzes (faixas emissivas)
        public Light[] luzesPainel, luzesCabine;
        public GameObject terra;
        public TMP_Text txtChecklist, txtLegenda;

        static readonly string[] PassosPlataforma = { "mn_a", "mn_b", "mn_c", "fuel_cells", "apu1", "apu2", "apu3", "meds", "comm" };
        static readonly string[] PassosOrbita = { "luz_flood", "meds", "portas_baia", "radiadores", "rms", "comm" };

        readonly Dictionary<string, ControleNave> ctl = new Dictionary<string, ControleNave>();
        string[] passos;
        float legendaAte;
        bool concluido;

        void Start()
        {
            if (FaseInicial.HasValue) { fase = FaseInicial.Value; FaseInicial = null; }
            if (raizNave) raizNave.localRotation = fase == FaseNave.Plataforma ? Quaternion.Euler(0f, 0f, -90f) : Quaternion.identity;
            foreach (var g in combinarEstaticos) if (g) StaticBatchingUtility.Combine(g);
            if (terra) terra.SetActive(fase == FaseNave.Orbita);
            foreach (var c in ControleNave.Todos) ctl[c.id] = c;
            passos = fase == FaseNave.Plataforma ? PassosPlataforma : PassosOrbita;
            // estado inicial: na plataforma tudo desligado; em orbita a energia ja esta ligada
            bool energia = fase == FaseNave.Orbita;
            foreach (var c in ctl.Values)
                c.Definir(energia && (c.id.StartsWith("mn_") || c.id == "fuel_cells" || c.id == "ventilacao" || c.id == "luz_dome"));
            AplicarTudo();
            ControleNave.OnAcionado += Acionado;
            Loc.OnLanguageChanged += Atualizar;
            Atualizar();
            Debug.Log($"[Nave] fase {fase}: {ctl.Count} controles, {passos.Length} passos");
        }

        void OnDestroy()
        {
            ControleNave.OnAcionado -= Acionado;
            Loc.OnLanguageChanged -= Atualizar;
        }

        bool On(string id) => ctl.TryGetValue(id, out var c) && c.ligado;
        bool Energia => On("mn_a") || On("mn_b") || On("mn_c");

        void Acionado(ControleNave c)
        {
            AplicarTudo();
            switch (c.id)
            {
                case "comm":
                    if (c.ligado && Energia) { SomNave.Bipe(c.transform.position); Legenda("nave.comm." + (fase == FaseNave.Plataforma ? "plataforma" : "orbita")); }
                    break;
                case "master_alarm":
                    SomNave.Alarme(c.transform.position);
                    Legenda("nave.alarme");
                    c.Definir(false);
                    break;
                case "meds":
                    if (c.ligado && !Energia) Legenda("nave.sem_energia");
                    break;
            }
            Debug.Log($"[Nave] {c.id} -> {(c.ligado ? "ON" : "OFF")}");
            Atualizar();
        }

        void AplicarTudo()
        {
            bool e = Energia;
            if (telas) telas.enabled = e && On("meds");
            foreach (var l in luzesPainel) if (l) l.enabled = e && On("luz_flood");
            foreach (var l in luzesCabine) if (l) l.enabled = !e || On("luz_dome");     // sem energia: luz de emergencia
            if (luzesTeto) luzesTeto.enabled = !e || On("luz_dome") || On("luz_flood");
        }

        public void Sair()
        {
            if (!Application.CanStreamedLevelBeLoaded(CenaRetorno))
            {
                Debug.LogWarning($"[Nave] cena de retorno '{CenaRetorno}' nao esta no Build Settings");
                Legenda("nave.sem_retorno");
                segurandoM = 0f;
                return;
            }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Debug.Log("[Nave] voltando para " + CenaRetorno);
            SceneManager.LoadScene(CenaRetorno);
        }

        public void Legenda(string chave)
        {
            if (!txtLegenda) return;
            txtLegenda.text = Loc.Get(chave);
            legendaAte = Time.time + 5f;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.isPressed)
            {
                segurandoM += Time.unscaledDeltaTime;
                if (txtLegenda) { txtLegenda.text = Loc.Get("nave.saindo"); legendaAte = Time.time + 0.5f; }
                if (segurandoM >= 1f) { segurandoM = -999f; Sair(); }
            }
            else if (segurandoM > 0f) segurandoM = 0f;
            if (txtLegenda && legendaAte > 0 && Time.time > legendaAte) { txtLegenda.text = ""; legendaAte = 0; }
        }

        void Atualizar()
        {
            if (!txtChecklist || passos == null) return;
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>").Append(Loc.Get(fase == FaseNave.Plataforma ? "nave.check.plataforma" : "nave.check.orbita")).Append("</b>\n");
            int feitos = 0;
            foreach (var p in passos)
            {
                bool ok = On(p);
                if (ok) feitos++;
                sb.Append(ok ? "<color=#59FF86>[OK]</color> " : "<color=#5D738A>[  ]</color> ").Append(Loc.Get("nave." + p)).Append('\n');
            }
            if (feitos == passos.Length)
            {
                sb.Append("\n<color=#FFC23A>").Append(Loc.Get("nave.check.ok")).Append("</color>");
                if (!concluido) { concluido = true; Debug.Log("[Nave] checklist concluido"); }
            }
            sb.Append("\n<size=75%><color=#B9D2E8>").Append(Loc.Get("nave.dica_tab")).Append("</color></size>");
            txtChecklist.text = sb.ToString();
        }
    }
}
