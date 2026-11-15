using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AstroLab.Centro;
using UnityEngine;

// Bot do P2 (plataforma -> elevador -> braço -> escotilha -> assento). Rode em Play Mode:
//   unity command script run --file AgentScripts/BotLancamento.cs --entry BotLancamento.Rodar --timeout 120
// Saída: ~15 linhas "[BOT] ...".
public static class BotLancamento
{
    static readonly List<string> log = new List<string>();
    static readonly List<string> erros = new List<string>();
    static float t0;

    static void L(string s) => log.Add($"[BOT] {Time.realtimeSinceStartup - t0,5:0.0}s {s}");

    static async Task<bool> Esperar(System.Func<bool> cond, float seg)
    {
        float fim = Time.realtimeSinceStartup + seg;
        while (Time.realtimeSinceStartup < fim) { if (cond()) return true; await Task.Delay(50); }
        return cond();
    }

    static void Teleportar(JogadorFPS j, Vector3 p)
    {
        var cc = j.GetComponent<CharacterController>(); cc.enabled = false;
        j.transform.position = p; cc.enabled = true;
    }

    public static async Task<string> Rodar()
    {
        log.Clear(); erros.Clear(); t0 = Time.realtimeSinceStartup;
        Application.LogCallback cb = (m, st, tipo) => { if (tipo == LogType.Error || tipo == LogType.Exception) erros.Add(m.Split('\n')[0]); };
        Application.logMessageReceived += cb;
        try
        {
            if (!Application.isPlaying) return "[BOT] precisa estar em Play Mode";
            Time.timeScale = 3f;
            var ctrl = Object.FindAnyObjectByType<ControleLancamento>();
            var elev = Object.FindAnyObjectByType<ElevadorTorre>();
            var j = Object.FindAnyObjectByType<JogadorFPS>();
            var lod = GameObject.Find("Onibus_LOD");
            L($"refs ctrl={(bool)ctrl} elev={(bool)elev} escotilha={(bool)ctrl?.escotilha} assento={(bool)ctrl?.assento} interior={(bool)ctrl?.interior} lod={(lod ? lod.GetComponent<LODGroup>().lodCount : 0)}");
            if (!ctrl || !elev || !j) return string.Join("\n", log);
            await Task.Delay(1500);

            typeof(ControleLancamento).GetProperty("Atual").SetValue(ctrl, ControleLancamento.Fase.IrParaPlataforma);
            Teleportar(j, new Vector3(-44f, 7.2f, -262f));
            bool ok = await Esperar(() => ctrl.Atual == ControleLancamento.Fase.Elevador, 3f);
            L($"deck -> fase {ctrl.Atual} {(ok ? "OK" : "FALHOU")} interior={ctrl.interior.activeSelf}");

            var baseE = GameObject.Find("WP_Elevador_Base").transform.position;
            Teleportar(j, baseE + Vector3.up * 1.0f);
            await Task.Delay(400);
            float y0 = j.transform.position.y;
            elev.Acionar();
            ok = await Esperar(() => elev.NoTopo && !elev.Viajando, 15f);
            await Task.Delay(300);
            L($"elevador topo={elev.NoTopo} jogador y {y0:0.0}->{j.transform.position.y:0.0} cc={j.GetComponent<CharacterController>().enabled} fps={j.enabled} fase {ctrl.Atual} {(ok && ctrl.Atual == ControleLancamento.Fase.Braco ? "OK" : "FALHOU")}");

            // anda pelo braço de verdade? aqui: teleporte para dentro da Sala Branca e confere o chão
            var esc = ctrl.escotilha.transform.position;
            Teleportar(j, new Vector3(esc.x + 1.2f, esc.y + 0.4f, esc.z));
            await Task.Delay(800);
            L($"sala branca: no chão={j.GetComponent<CharacterController>().isGrounded} y={j.transform.position.y:0.00} escotilha ativa={ctrl.escotilha.ativo} interior={ctrl.interior.activeSelf}");
            ctrl.escotilha.Interagir();
            ok = await Esperar(() => ctrl.Atual == ControleLancamento.Fase.Sentado && ctrl.assento.Sentado, 6f);
            await Task.Delay(600);
            var cam = j.GetComponentInChildren<Camera>().transform;
            var olho = ctrl.assento.olho.position;
            L($"sentado={ctrl.assento.Sentado} dist olho={Vector3.Distance(cam.position, olho):0.00} m frente={cam.forward:F2} cima={cam.up:F2} {(ok ? "OK" : "FALHOU")}");

            // olha o que a câmera "vê" à frente (painel/janelas) por raycast
            bool viu = Physics.Raycast(cam.position, cam.forward, out var hit, 4f);
            L($"raycast frente: {(viu ? hit.collider.name + " a " + hit.distance.ToString("0.0") + " m" : "nada em 4 m (normal: painel não tem colisor)")}");
            int telas = 0;
            foreach (var r in ctrl.interior.GetComponentsInChildren<Renderer>()) foreach (var m in r.sharedMaterials) if (m && m.name.StartsWith("OR_Tela")) telas++;
            L($"interior: renderers={ctrl.interior.GetComponentsInChildren<Renderer>().Length} materiais de tela={telas}");
        }
        finally
        {
            Application.logMessageReceived -= cb;
            Time.timeScale = 1f;
        }
        L($"erros/exceções: {erros.Count}" + (erros.Count > 0 ? " -> " + string.Join(" | ", erros.GetRange(0, Mathf.Min(4, erros.Count))) : ""));
        return string.Join("\n", log);
    }
}
