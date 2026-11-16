using System;
using System.Collections;
using AstroLab.Localization;
using UnityEngine;

namespace AstroLab.Centro
{
    // Depois do treinamento: estrada -> plataforma -> elevador da torre -> braço de acesso -> Sala Branca ->
    // escotilha [E] -> fade -> sentado na cadeira do comandante. Liga o interior do ônibus só quando o jogador
    // está perto (otimização) e dispara AoSentar para quem for cuidar do lançamento.
    public class ControleLancamento : MonoBehaviour
    {
        public TutorialCentro tutorial;
        public JogadorFPS jogador;
        public HudCentro hud;
        public MarcadorObjetivo marcador;
        public ElevadorTorre elevador;
        public Interagivel escotilha;
        public AssentoCabine assento;
        public Collider zonaPlataforma;
        public Transform alvoElevador;
        public GameObject interior;           // Onibus_Interior (+ _Transp, luz da cabine)
        public float distanciaInterior = 30f;
        public string CenaInterior = "NaveInterior";

        public static event Action AoSentar;
        public enum Fase { Treinando, IrParaPlataforma, Elevador, Braco, Sentado }
        public Fase Atual { get; private set; } = Fase.Treinando;

        void Start()
        {
            if (interior) interior.SetActive(false);
            if (escotilha) { escotilha.ativo = false; escotilha.Interagiu += _ => StartCoroutine(Embarcar()); }
            if (tutorial) tutorial.AoConcluirTreinamento += () => Atual = Fase.IrParaPlataforma;
            if (elevador) elevador.AoChegar += topo =>
            {
                if (Atual == Fase.Elevador && topo) IrPara(Fase.Braco);
                else if (Atual == Fase.Braco && !topo) IrPara(Fase.Elevador);
            };
        }

        void IrPara(Fase f)
        {
            Atual = f;
            switch (f)
            {
                case Fase.Elevador:
                    hud.Objetivo(Loc.Get("ct.obj.elevador"));
                    if (alvoElevador) marcador.Colocar(alvoElevador.position + Vector3.up * 1.2f);
                    break;
                case Fase.Braco:
                    hud.Objetivo(Loc.Get("ct.obj.braco"));
                    if (escotilha) { escotilha.ativo = true; marcador.Colocar(escotilha.transform.position + Vector3.up * 1.0f); }
                    break;
            }
        }

        void Update()
        {
            if (Atual == Fase.IrParaPlataforma && zonaPlataforma && zonaPlataforma.bounds.Contains(jogador.transform.position))
                IrPara(Fase.Elevador);
            if (interior && escotilha)
            {
                bool perto = Atual == Fase.Sentado || (jogador.transform.position - escotilha.transform.position).sqrMagnitude < distanciaInterior * distanciaInterior;
                if (interior.activeSelf != perto) interior.SetActive(perto);
            }
        }

        IEnumerator Embarcar()
        {
            if (Atual == Fase.Sentado) yield break;
            escotilha.ativo = false;
            marcador.gameObject.SetActive(false);
            hud.Ocupado = true;
            yield return hud.Fade(1f, 0.9f);
            Atual = Fase.Sentado;
            // pela escotilha da Sala Branca o jogador entra no interior do ônibus (cena da equipe da nave)
            if (Application.CanStreamedLevelBeLoaded(CenaInterior))
            {
                Debug.Log("[Centro] Escotilha: carregando " + CenaInterior);
                AoSentar?.Invoke();
                AstroLab.Nave.SistemasNave.FaseInicial = AstroLab.Nave.FaseNave.Plataforma;
                UnityEngine.SceneManagement.SceneManager.LoadScene(CenaInterior);
                yield break;
            }
            Debug.LogWarning("[Centro] Cena " + CenaInterior + " fora do Build Settings: usando o assento local.");
            if (interior) interior.SetActive(true);
            assento.Sentar(jogador);
            hud.Objetivo(Loc.Get("ct.obj.sentado"));
            hud.Progresso(Loc.Get("ct.progresso.missao"));
            yield return new WaitForSeconds(0.4f);
            yield return hud.Fade(0f, 1.2f);
            for (float t = 0; t < 4.5f; t += Time.deltaTime)
            {
                float a = Mathf.Clamp01(t * 2f) * Mathf.Clamp01((4.5f - t) * 1.5f);
                hud.Efeito(Loc.Get("ct.cabine.titulo"), Loc.Get("ct.cabine.sub"), a);
                yield return null;
            }
            hud.Efeito("", "", 0);
            hud.Ocupado = false;
            Debug.Log("[Centro] Jogador sentado na cadeira do comandante.");
            AoSentar?.Invoke();
        }
    }
}
