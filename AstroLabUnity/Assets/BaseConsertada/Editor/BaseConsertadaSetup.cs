using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Menu "AstroLab > Base Consertada": cria os materiais novos, liga o FBX aos materiais da BaseLunar
// (só referencia, não altera nada em Assets/BaseLunar) e monta o prefab BaseConsertada.
// Rode de novo sempre que o BaseConsertada.fbx for regerado pelo script do Blender.
public static class BaseConsertadaSetup
{
    const string Root = "Assets/BaseConsertada";
    const string Fbx = Root + "/Modelos/BaseConsertada.fbx";
    const string MatDir = Root + "/Materiais";
    const string PrefabPath = Root + "/Prefabs/BaseConsertada.prefab";
    const string MatBaseLunar = "Assets/BaseLunar/Materiais";

    [MenuItem("AstroLab/Base Consertada/Atualizar materiais e prefab")]
    public static void Atualizar()
    {
        CriarMateriais();
        CriarPrefab();
    }

    static void CriarMateriais()
    {
        Directory.CreateDirectory(MatDir);
        var lit = Shader.Find("Universal Render Pipeline/Lit");

        Material Mat(string nome)
        {
            string path = $"{MatDir}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            return m;
        }

        void Emissivo(Material m, Color c, float intensidade)
        {
            m.SetColor("_BaseColor", c.gamma);
            m.SetFloat("_Smoothness", 0.5f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensidade);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
        }

        Emissivo(Mat("M_Luz_Verde"), new Color(0.1f, 1f, 0.25f), 4f);
        Emissivo(Mat("M_Luz_Branca"), new Color(1f, 0.97f, 0.9f), 5f);

        var planta = Mat("M_Planta_Viva");
        planta.SetColor("_BaseColor", new Color(0.12f, 0.45f, 0.08f).gamma);
        planta.SetFloat("_Smoothness", 0.3f);
        EditorUtility.SetDirty(planta);

        var agua = Mat("M_Agua");
        agua.SetColor("_BaseColor", new Color(0.1f, 0.45f, 0.8f, 0.75f));
        agua.SetFloat("_Smoothness", 0.95f);
        agua.SetFloat("_Surface", 1); agua.SetFloat("_Blend", 0); agua.SetOverrideTag("RenderType", "Transparent");
        agua.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); agua.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        agua.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); agua.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        agua.SetFloat("_ZWrite", 0);
        agua.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        agua.renderQueue = (int)RenderQueue.Transparent;
        agua.EnableKeyword("_EMISSION");
        agua.SetColor("_EmissionColor", new Color(0.05f, 0.3f, 0.6f) * 0.5f);
        EditorUtility.SetDirty(agua);
        // salva SÓ os materiais desta pasta (AssetDatabase.SaveAssets() gravaria também os da BaseLunar, que não podemos alterar)
        foreach (var nome in new[] { "M_Luz_Verde", "M_Luz_Branca", "M_Planta_Viva", "M_Agua" })
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{nome}.mat"));

        // liga cada material do FBX a um .mat existente: primeiro os da BaseLunar, depois os novos
        var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        // todos os materiais do FBX: os ainda embutidos + os que já foram ligados numa execução anterior
        var nomes = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Material>().Select(m => m.name)
            .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name))
            .Distinct().ToList();
        int ligados = 0;
        foreach (var nome in nomes)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatBaseLunar}/{nome}.mat")
                    ?? AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{nome}.mat");
            if (m == null) { Debug.LogWarning($"[Base Consertada] Material {nome} não encontrado."); continue; }
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), nome), m);
            ligados++;
        }
        importer.SaveAndReimport();
        Debug.Log($"[Base Consertada] {ligados}/{nomes.Count} materiais ligados.");
    }

    static void CriarPrefab()
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var root = new GameObject("BaseConsertada");
        var modelo = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        modelo.transform.SetParent(root.transform, false);

        int n = 0;
        foreach (var mf in modelo.GetComponentsInChildren<MeshFilter>(true))
        {
            var go = mf.gameObject;
            if (!go.name.StartsWith("OK_")) continue;
            if (go.name != "OK_Hub_Torre_Vidros")      // mesma regra de colisores da BaseLunar
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
            go.SetActive(false);                       // começa escondido; o BaseConsertada liga quando consertar
            n++;
        }
        root.AddComponent<BaseConsertada>();

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[Base Consertada] Prefab salvo em {PrefabPath} com {n} peças OK_.");
    }
}
