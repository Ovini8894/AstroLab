using System.Text;
using UnityEngine;
public static class Teste
{
    static Vector3 B(float x, float y, float z) => new Vector3(-x, z, -y);
    // coloca o jogador num ponto (coordenadas do Blender) olhando para outro
    public static string Ir(float x, float y, float z, float ax, float ay, float az)
    {
        var j = GameObject.Find("Jogador");
        var cc = j.GetComponent<CharacterController>(); cc.enabled = false;
        var p = B(x, y, z) + Vector3.up * 1.0f; j.transform.position = p;
        var d = B(ax, ay, az) - (p + Vector3.up * 0.8f);
        j.transform.rotation = Quaternion.Euler(0, Quaternion.LookRotation(new Vector3(d.x, 0, d.z)).eulerAngles.y, 0);
        var cam = j.GetComponentInChildren<Camera>().transform;
        cam.localEulerAngles = new Vector3(-Mathf.Asin(d.normalized.y) * Mathf.Rad2Deg, 0, 0);
        cc.enabled = true;
        return "ok " + p;
    }
    public static string Estado()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Jogador " + GameObject.Find("Jogador").transform.position.ToString("F2"));
        var n = GameObject.Find("Instrutora_Lima"); sb.AppendLine("NPC " + n.transform.position.ToString("F2") + " scale " + n.transform.lossyScale);
        var t = Object.FindAnyObjectByType<AstroLab.Centro.TutorialCentro>(); sb.AppendLine("Etapa " + t.EtapaAtual);
        return sb.ToString();
    }
}
