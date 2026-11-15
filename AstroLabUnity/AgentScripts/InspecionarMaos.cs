using System.Text;
using UnityEditor;
using UnityEngine;
public static class InspecionarMaos
{
    public static string Run()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Personagem/PrimeiraPessoa/Prefabs/MaosEScanner.prefab");
        var sb = new StringBuilder();
        void Dump(Transform t, int d)
        {
            sb.Append(new string(' ', d * 2)).Append(t.name).Append("  p=").Append(t.localPosition.ToString("F3")).Append(" s=").Append(t.localScale.ToString("F3"));
            foreach (var c in t.GetComponents<Component>())
            {
                if (c is Transform) continue;
                sb.Append(" [").Append(c.GetType().Name);
                if (c is MeshRenderer r) sb.Append(":").Append(string.Join(",", System.Array.ConvertAll(r.sharedMaterials, m => m ? m.name : "null")));
                if (c is MeshFilter f) sb.Append(":").Append(f.sharedMesh ? f.sharedMesh.name : "null");
                sb.Append("]");
            }
            sb.AppendLine();
            foreach (Transform c in t) Dump(c, d + 1);
        }
        Dump(go.transform, 0);
        return sb.ToString();
    }
}
