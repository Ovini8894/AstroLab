using System.Text;
using UnityEngine;
public static class Diag
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var j = GameObject.Find("Jogador"); sb.AppendLine("Jogador " + j.transform.position.ToString("F2"));
        var sp = GameObject.Find("SPAWN_Player"); sb.AppendLine("SPAWN " + (sp ? sp.transform.position.ToString("F2") : "null"));
        var n = GameObject.Find("Instrutora_Lima");
        foreach (var t in n.GetComponentsInChildren<Transform>()) sb.AppendLine($"{t.name} pos={t.position:F2} lossy={t.lossyScale:F3} local={t.localScale:F3}");
        var c = GameObject.Find("CentroTreinamento"); sb.AppendLine("Centro root rot=" + c.transform.eulerAngles + " scale=" + c.transform.localScale);
        return sb.ToString();
    }
}
