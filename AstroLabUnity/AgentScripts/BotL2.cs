using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AstroLab.Centro;
using UnityEngine;

public static class BotL2
{
    public static async Task<string> Rodar()
    {
        var log = new List<string>();
        var j = Object.FindAnyObjectByType<JogadorFPS>();
        var cc = j.GetComponent<CharacterController>();
        var mods = GameObject.Find("Corredor").transform.Cast<Transform>().OrderBy(t => t.name).ToList();
        int semChao = 0, bloqueados = 0; var ruins = new List<string>();
        for (int k = 0; k < mods.Count - 1; k++)
        {
            var a = mods[k].position; var b = mods[k + 1].position;
            var meio = (a + b) / 2f;
            cc.enabled = false; j.transform.position = meio + Vector3.up * 1.0f; cc.enabled = true;
            await Task.Delay(120);
            bool chao = Physics.Raycast(j.transform.position, Vector3.down, out var h, 2.5f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (!chao) { semChao++; ruins.Add($"{mods[k].name}:sem chão"); }
            var p0 = meio + Vector3.up * 1.2f; var p1 = (b + mods[Mathf.Min(k + 2, mods.Count - 1)].position) / 2f + Vector3.up * 1.2f;
            if (Physics.Linecast(p0, p1, out var hit, ~(1 << 2), QueryTriggerInteraction.Ignore)) { bloqueados++; ruins.Add($"{mods[k].name}->{hit.collider.name}"); }
        }
        log.Add($"[BOT] módulos={mods.Count} sem chão={semChao} caminho bloqueado={bloqueados} {(ruins.Count > 0 ? string.Join(", ", ruins.Take(6)) : "")}");
        var porta = Object.FindObjectsByType<PortaHex>(FindObjectsInactive.Include).FirstOrDefault();
        if (porta)
        {
            porta.Abrir(); await Task.Delay(1800);
            log.Add($"[BOT] porta {porta.name}: aberta={porta.Aberta} bloqueio ativo={porta.bloqueio.enabled} placa='{porta.placa.text}'");
        }
        // ida do vestíbulo ao primeiro módulo: há passagem?
        var vest = GameObject.Find("WP_Subsolo_Vestibulo").transform.position + Vector3.up * 1.2f;
        bool livre = !Physics.Linecast(vest, mods[0].position + Vector3.up * 1.2f + (mods[1].position - mods[0].position).normalized * 2f, out var hv, ~(1 << 2), QueryTriggerInteraction.Ignore);
        log.Add($"[BOT] vestíbulo -> corredor livre={livre}{(livre ? "" : " (" + hv.collider.name + ")")}");
        return string.Join("\n", log);
    }
}
