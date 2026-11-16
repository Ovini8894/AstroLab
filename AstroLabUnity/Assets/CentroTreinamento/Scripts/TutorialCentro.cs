using System;
using System.Collections;
using AstroLab.Localization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Centro
{
    [Serializable]
    public class EtapaTreino
    {
        public string id = "est0";               // chaves: npc.<id>.N, npc.<id>.fim.N, treino.<id>.nome, treino.<id>.acao
        public Transform pontoNpc;
        public Interagivel console;
        public Vector3[] rota;                   // caminho da instrutora até pontoNpc (último ponto = pontoNpc)
    }

    // Roteiro do tutorial: a instrutora leva o jogador por todas as estações de treino; em cada uma ela explica,
    // o jogador usa o painel ([E]), a atividade acontece e ela fecha a etapa. No fim a porta norte abre para a plataforma.
    public class TutorialCentro : MonoBehaviour
    {
        [Header("Cena")]
        public NpcInstrutor npc;
        public Transform npcInicio;
        public JogadorFPS jogador;
        public HudCentro hud;
        public CaixaDialogo dialogo;
        public EstadoVestimentaJogador vestimenta;
        public MarcadorObjetivo marcador;

        [Header("Etapas")]
        public EtapaTreino[] etapas;
        public Vector3[] rotaSaida;
        public Transform alvoFinal;

        [Header("Estações")]
        public Centrifuga centrifuga;
        public Deslizador plataformaNbl;
        public Deslizador portaSaida;
        public ZonaGravidade zonaLunar;
        public Renderer[] telasVr;

        [Header("Passagem secreta (depois da recepção)")]
        public PassagemSecreta passagem;
        public int etapaAntesDoElevador = 0;

        [Header("Teste")]
        [Tooltip("Começa direto nesta etapa (0 = do início). Só para testar.")]
        public int etapaInicial = 0;

        public static bool TreinamentoConcluido { get; private set; }
        public event Action AoConcluirTreinamento;
        public int EtapaAtual { get; private set; } = -1;

        Transform cam;
        Vector3 camBase;

        IEnumerator Start()
        {
            TreinamentoConcluido = false;
            cam = jogador.GetComponentInChildren<Camera>().transform;
            camBase = cam.localPosition;
            PerfilGravidade.Terra.AplicarEm(jogador);
            vestimenta.Definir(Vestimenta.MaosNuas, true);
            marcador.gameObject.SetActive(false);
            foreach (var e in etapas) if (e.console) e.console.ativo = false;
            hud.FadeAlpha = 1f;
            hud.Progresso("");

            if (etapaInicial > 0 && etapaInicial < etapas.Length)
            {
                var anterior = etapas[etapaInicial - 1].pontoNpc;
                npc.Teleportar(anterior.position, 0);
                jogador.GetComponent<CharacterController>().enabled = false;
                jogador.transform.position = anterior.position + anterior.forward * -2f + Vector3.up * 0.1f;
                jogador.GetComponent<CharacterController>().enabled = true;
            }
            else
            {
                var olhar = jogador.transform.position - npcInicio.position; olhar.y = 0;
                npc.Teleportar(npcInicio.position, olhar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(olhar).eulerAngles.y : 0f);
            }
            yield return hud.Fade(0f, 1.4f);

            if (etapaInicial <= 0)
            {
                hud.Objetivo(Loc.Get("ct.obj.falar"));
                marcador.Colocar(npc.transform.position + Vector3.up * 3.6f);   // acima do letreiro, visível do spawn
                yield return new WaitUntil(() => Distancia(npc.transform) < 4.5f);
                marcador.gameObject.SetActive(false);
                yield return Falar("npc.intro");
            }

            for (int i = Mathf.Max(0, etapaInicial); i < etapas.Length; i++)
            {
                yield return RodarEtapa(i);
                if (passagem && i == etapaAntesDoElevador) yield return passagem.Rodar(this);
            }
            if (passagem && etapas.Length <= etapaAntesDoElevador) yield return passagem.Rodar(this);
            if (etapas.Length <= 1) yield break;              // (L1: o corredor e as salas chegam nos próximos lotes)

            // ---------------------------------------------------------- fim do treinamento
            TreinamentoConcluido = true;
            EtapaAtual = etapas.Length;
            hud.Progresso(Loc.Get("ct.progresso.fim"));
            if (portaSaida) portaSaida.Abrir();
            npc.IrPara(rotaSaida);
            hud.Objetivo(Loc.Get("ct.obj.plataforma"));
            if (alvoFinal) marcador.Colocar(alvoFinal.position + Vector3.up * 3f);
            AoConcluirTreinamento?.Invoke();
        }

        IEnumerator RodarEtapa(int i)
        {
            var e = etapas[i];
            EtapaAtual = i;
            hud.Progresso(Loc.Get("ct.progresso", i + 1, etapas.Length));
            string nome = Loc.Get($"treino.{e.id}.nome");
            hud.Objetivo(Loc.Get("ct.obj.seguir", nome));
            if (e.console) marcador.Colocar(e.console.transform.position + Vector3.up * 1.3f);

            bool chegou = false;
            npc.IrPara(e.rota, () => chegou = true);
            yield return new WaitUntil(() => chegou && Distancia(npc.transform) < 6.5f);
            yield return Falar($"npc.{e.id}");

            if (e.console)
            {
                hud.Objetivo(Loc.Get("ct.obj.interagir", Loc.Get($"treino.{e.id}.acao")));
                bool usou = false;
                void Usou(Interagivel _) => usou = true;
                e.console.Interagiu += Usou;
                e.console.ativo = true;
                yield return new WaitUntil(() => usou);
                e.console.Interagiu -= Usou;
                e.console.ativo = false;
            }
            marcador.gameObject.SetActive(false);

            hud.Ocupado = true;
            yield return Atividade(e.id);
            hud.Ocupado = false;
            yield return Falar($"npc.{e.id}.fim");
            if (vestimenta.Atual == Vestimenta.TrajeTreino) vestimenta.Definir(Vestimenta.MaosNuas);
        }

        // Durante a fala o jogador para (Espaço/E avançam o texto, não pulam) e o corpo vira para a instrutora.
        // Só o yaw do corpo muda: o pitch da câmera é do JogadorFPS e fica como estava.
        public IEnumerator Falar(string prefixo)
        {
            bool fim = false;
            npc.Falando = true;
            jogador.enabled = false;
            dialogo.Mostrar(prefixo, () => fim = true);
            while (!fim)
            {
                var d = npc.transform.position - jogador.transform.position; d.y = 0;
                if (d.sqrMagnitude > 0.25f && d.sqrMagnitude < 100f)
                {
                    var alvo = Quaternion.LookRotation(d.normalized, Vector3.up);
                    jogador.transform.rotation = Quaternion.Slerp(jogador.transform.rotation, alvo, 1f - Mathf.Exp(-4f * Time.deltaTime));
                }
                yield return null;
            }
            jogador.enabled = true;
            npc.Falando = false;
        }

        float Distancia(Transform t)
        {
            var a = t.position - jogador.transform.position;
            a.y = 0;
            return a.magnitude;
        }

        // ================================================================== atividades das estações
        IEnumerator Atividade(string id)
        {
            switch (id)
            {
                case "est0":
                    yield return MostrarEfeito(Loc.Get("treino.est0.efeito"), Loc.Get("treino.est0.efeito.sub"), 2.2f);
                    break;

                case "est1":
                    for (float t = 0; t < 3.2f; t += Time.deltaTime)
                    {
                        int bpm = 66 + Mathf.RoundToInt(Mathf.PerlinNoise(Time.time * 2f, 0f) * 6f);
                        hud.Efeito(Loc.Get("treino.est1.bpm", bpm), Loc.Get("treino.est1.exame"), Mathf.Clamp01(t * 3f));
                        yield return null;
                    }
                    yield return MostrarEfeito(Loc.Get("treino.est1.resultado"), Loc.Get("treino.est1.resultado.sub"), 2f);
                    break;

                case "est2":
                    if (centrifuga) centrifuga.Ligar(centrifuga.rpmMaximo);
                    for (float t = 0; t < 9f || (centrifuga && centrifuga.Rpm > 0.3f); t += Time.deltaTime)
                    {
                        if (t >= 9f && centrifuga) centrifuga.Desligar();
                        float g = centrifuga ? centrifuga.ForcaG : 1f;
                        float k = Mathf.Clamp01((g - 1f) / 3f);
                        hud.Efeito(Loc.Get("treino.forcag", g.ToString("0.0")), Loc.Get("treino.est2.dica"), 1f);
                        hud.Vinheta(new Color(0.25f, 0.02f, 0.05f, k * 0.85f));
                        cam.localPosition = camBase + UnityEngine.Random.insideUnitSphere * (0.012f * k);
                        if (t > 25f) break;
                        yield return null;
                    }
                    cam.localPosition = camBase;
                    hud.Vinheta(Color.clear);
                    hud.Efeito("", "", 0);
                    break;

                case "est3":
                    yield return hud.Fade(1f, 0.6f);
                    hud.Overlay(new Color(0.05f, 0.25f, 0.75f, 0.35f));
                    foreach (var r in telasVr)
                        if (r) foreach (var m in r.materials) if (m.name.StartsWith("CT_Tela_VR")) m.SetColor("_EmissionColor", Color.white * 2.5f);
                    yield return hud.Fade(0f, 0.6f);
                    for (float t = 0; t < 4.5f; t += Time.deltaTime)
                    {
                        hud.Efeito(Loc.Get("treino.est3.efeito"), Loc.Get("treino.est3.efeito.sub"), Mathf.Clamp01(t * 2f));
                        hud.Overlay(new Color(0.05f, 0.25f, 0.75f, 0.28f + Mathf.Sin(t * 9f) * 0.04f));
                        yield return null;
                    }
                    yield return hud.Fade(1f, 0.5f);
                    hud.Overlay(Color.clear); hud.Efeito("", "", 0);
                    yield return hud.Fade(0f, 0.6f);
                    break;

                case "est4":
                    vestimenta.Definir(Vestimenta.TrajeTreino);
                    yield return new WaitForSeconds(1.0f);
                    if (plataformaNbl) plataformaNbl.Abrir();
                    yield return hud.Fade(1f, 0.8f);
                    hud.Overlay(new Color(0.03f, 0.3f, 0.55f, 0.55f));
                    yield return hud.Fade(0f, 0.8f);
                    for (float t = 0; t < 6f; t += Time.deltaTime)
                    {
                        float prof = Mathf.Lerp(0f, 12f, Mathf.SmoothStep(0, 1, t / 5f));
                        hud.Efeito(Loc.Get("treino.est4.profundidade", prof.ToString("0.0")), Loc.Get("treino.est4.efeito.sub"), 1f);
                        cam.localPosition = camBase + new Vector3(Mathf.Sin(t * 0.9f) * 0.02f, Mathf.Sin(t * 1.3f) * 0.03f, 0);
                        yield return null;
                    }
                    yield return hud.Fade(1f, 0.8f);
                    cam.localPosition = camBase;
                    hud.Overlay(Color.clear); hud.Efeito("", "", 0);
                    if (plataformaNbl) plataformaNbl.Fechar();
                    yield return hud.Fade(0f, 0.8f);
                    break;

                case "est5":
                    vestimenta.Definir(Vestimenta.TrajeTreino);          // a troca anima sozinha; os saltos já contam
                    // conta POUSOS (chão -> ar dentro do campo, depois ar -> chão): independe da ordem dos Update
                    // e o número sobe depois da queda lenta em 1,62 m/s², que é o que a estação quer mostrar
                    int saltos = 0;
                    var cc = jogador.GetComponent<CharacterController>();
                    bool noChaoAntes = cc.isGrounded, saiuNaZona = false;
                    if (zonaLunar) marcador.Colocar(zonaLunar.transform.position + Vector3.up * 1.5f);
                    while (saltos < 3)
                    {
                        hud.Objetivo(Loc.Get("ct.obj.saltos", saltos));
                        bool noChao = cc.isGrounded;
                        bool naZona = zonaLunar && zonaLunar.JogadorDentro;
                        if (naZona) marcador.gameObject.SetActive(false);
                        if (noChaoAntes && !noChao && naZona) saiuNaZona = true;
                        if (!noChaoAntes && noChao && saiuNaZona) { saltos++; saiuNaZona = false; }
                        noChaoAntes = noChao;
                        yield return null;
                    }
                    hud.Objetivo(Loc.Get("ct.obj.saltos", 3));
                    yield return new WaitForSeconds(1.5f);
                    break;

                case "est6":
                    yield return hud.Fade(1f, 0.8f);
                    vestimenta.Definir(Vestimenta.TrajeCompleto, true);
                    hud.Efeito(Loc.Get("treino.est6.efeito"), Loc.Get("treino.est6.efeito.sub"), 1f);
                    yield return new WaitForSeconds(1.6f);
                    yield return hud.Fade(0f, 0.8f);
                    yield return new WaitForSeconds(1.2f);
                    hud.Efeito("", "", 0);
                    break;
            }
        }

        IEnumerator MostrarEfeito(string valor, string sub, float tempo)
        {
            for (float t = 0; t < tempo; t += Time.deltaTime)
            {
                float a = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((tempo - t) * 3f);
                hud.Efeito(valor, sub, a);
                yield return null;
            }
            hud.Efeito("", "", 0);
        }
    }
}
