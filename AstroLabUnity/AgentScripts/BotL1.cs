using System.Collections.Generic;
using System.Threading.Tasks;
using AstroLab.Centro;
using UnityEngine;

public static class BotL1
{
    static readonly List<string> log = new List<string>();
    static float t0;
    static void L(string s) => log.Add($"[BOT] {Time.realtimeSinceStartup - t0,5:0.0}s {s}");
    static async Task<bool> Esperar(System.Func<bool> c, float seg, CaixaDialogo d)
    {
        float fim = Time.realtimeSinceStartup + seg;
        while (Time.realtimeSinceStartup < fim) { if (d && d.Aberto) d.Fechar(); if (c()) return true; await Task.Delay(50); }
        return c();
    }
    static void Tp(JogadorFPS j, Vector3 p) { var cc = j.GetComponent<CharacterController>(); cc.enabled = false; j.transform.position = p; cc.enabled = true; }

    public static async Task<string> Rodar()
    {
        log.Clear(); t0 = Time.realtimeSinceStartup;
        var erros = new List<string>();
        Application.LogCallback cb = (m, st, tp) => { if (tp == LogType.Error || tp == LogType.Exception) erros.Add(m.Split('\n')[0]); };
        Application.logMessageReceived += cb;
        try
        {
            Time.timeScale = 3f;
            var tut = Object.FindAnyObjectByType<TutorialCentro>();
            var dlg = Object.FindAnyObjectByType<CaixaDialogo>();
            var j = tut.jogador; var npc = tut.npc; var ps = tut.passagem;
            L($"refs passagem={(bool)ps} etapas={tut.etapas.Length}");
            await Task.Delay(800);
            Tp(j, npc.transform.position + npc.transform.forward * 2.5f + Vector3.up);
            bool ok = await Esperar(() => tut.EtapaAtual == 0, 8f, dlg); L($"intro -> etapa 0 {(ok ? "OK" : "FALHOU")}");
            var con = tut.etapas[0].console;
            ok = await Esperar(() => { if (!con.ativo) Tp(j, npc.transform.position - npc.transform.forward * 1.5f + Vector3.up); return con.ativo; }, 20f, dlg);
            L($"console recepção ativo {(ok ? "OK" : "FALHOU")}");
            con.Interagir();
            var painel0 = ps.painel.position;
            ok = await Esperar(() => { Tp(j, npc.transform.position - npc.transform.forward * 1.6f + Vector3.up); return (ps.painel.position - painel0).magnitude > 3f; }, 25f, dlg);
            L($"painel secreto abriu {(ok ? "OK" : "FALHOU")} (desl {(ps.painel.position - painel0).magnitude:0.0} m), bloqueio={ps.bloqueioPainel && ps.bloqueioPainel.enabled}");
            await Esperar(() => (npc.transform.position - ps.pontoNpcCabine.position).magnitude < 0.3f, 10f, dlg);
            Tp(j, ps.cabine.position + Vector3.up * 1.0f + Vector3.left * 0.4f);
            float yIni = j.transform.position.y;
            ok = await Esperar(() => ps.cabine.position.y < ps.fundo.position.y + 0.05f && j.enabled, 20f, dlg);
            await Task.Delay(500);
            L($"descida {(ok ? "OK" : "FALHOU")}: jogador y {yIni:0.0} -> {j.transform.position.y:0.0}, npc y {npc.transform.position.y:0.0}, cc={j.GetComponent<CharacterController>().enabled}, chão={j.GetComponent<CharacterController>().isGrounded}, mostrador='{ps.mostrador.text}'");
            await Esperar(() => false, 2f, dlg);
            L($"npc no vestíbulo={(npc.transform.position - ps.pontoSaida.position).magnitude < 1f}");
        }
        finally { Application.logMessageReceived -= cb; Time.timeScale = 1f; }
        L($"erros: {erros.Count}" + (erros.Count > 0 ? " -> " + string.Join(" | ", erros.GetRange(0, Mathf.Min(4, erros.Count))) : ""));
        return string.Join("\n", log);
    }
}
