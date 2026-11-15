using System.Linq; using TMPro; using UnityEngine;
public static class Medir {
  public static string Run() {
    var sb = new System.Text.StringBuilder();
    foreach (var t in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include).Where(x => x.text=="NASA" || x.text.StartsWith("CENTRO ESPACIAL") || x.text.StartsWith("RECEP")).Take(4)) {
      t.ForceMeshUpdate(); var b = t.textBounds;
      sb.AppendLine($"{t.text} fontSize={t.fontSize:0.0} altura={b.size.y:0.00}m largura={b.size.x:0.00}m");
    }
    long tris = 0; foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include)) { var r = mf.GetComponent<MeshRenderer>(); if (r && r.enabled && mf.sharedMesh) tris += mf.sharedMesh.triangles.Length/3; }
    sb.AppendLine("tris visiveis=" + tris);
    return sb.ToString();
  }
}
