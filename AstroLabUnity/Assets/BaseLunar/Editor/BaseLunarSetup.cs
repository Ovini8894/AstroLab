using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Menu "Missão Lunar": cria materiais URP a partir das texturas e monta a cena da Lua.
// Pode ser rodado de novo sempre que o FBX for reexportado do Blender.
public static class BaseLunarSetup
{
    const string Root = "Assets/BaseLunar";
    const string Fbx = Root + "/Modelos/BaseLunar.fbx";
    const string MatDir = Root + "/Materiais";
    const string TexDir = Root + "/Texturas";
    const string ScenePath = Root + "/Cenas/Lua.unity";

    // Os materiais seguem o estilo cartoon (shader AstroLab/Toon): receitas em EstiloCartoon.ConfigurarMaterial,
    // cores em PaletaLua. As texturas realistas antigas (T_*_Cor / T_*_Normal) não são mais usadas.
    [MenuItem("Missão Lunar/1. Criar materiais URP")]
    public static void CreateMaterials()
    {
        Directory.CreateDirectory(MatDir);
        EstiloCartoon.GerarTexturas();
        var toon = Shader.Find("AstroLab/Toon");
        var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        var names = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Material>().Select(m => m.name)
            .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name))
            .Distinct().ToList();
        foreach (var name in names)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(toon); AssetDatabase.CreateAsset(m, path); }
            EstiloCartoon.ConfigurarMaterial(m, name);
            AssetDatabase.SaveAssetIfDirty(m);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), m);
        }
        importer.SaveAndReimport();
        Debug.Log($"[Missão Lunar] {names.Count} materiais cartoon criados em {MatDir}");
    }

    // ---------------------------------------------------------------- cena

    static bool NeedsCollider(string n, Bounds b)
    {
        if (n == "Terreno_Lua") return true;
        if (n.StartsWith("Rocha_")) return b.size.magnitude > 1.5f;
        if (n.StartsWith("Detrito_") || n.StartsWith("Usina_Cabos") || n.StartsWith("Rampa_Corrimao")) return false;
        if (n.Contains("Rotulo") || n.StartsWith("Terra_") || n == "Hub_Torre_Vidros" || n.StartsWith("Antena_Pedaco")) return false;
        return true;   // cascos, corredores, móveis, painéis, REPAIR_*, portas...
    }

    // Depois de reexportar o FBX com objetos NOVOS: põe colisor/flags só neles, sem recriar a cena.
    [MenuItem("Missão Lunar/3. Atualizar colisores da cena aberta")]
    public static void AtualizarColisores()
    {
        var baseGo = GameObject.Find("BaseLunar");
        if (baseGo == null) { Debug.LogWarning("[Missão Lunar] Abra a cena com o objeto BaseLunar."); return; }
        int novos = 0;
        foreach (var mf in baseGo.GetComponentsInChildren<MeshFilter>(true))
        {
            var go = mf.gameObject;
            var r = go.GetComponent<Renderer>();
            if (go.GetComponent<MeshCollider>() == null && NeedsCollider(go.name, r ? r.bounds : new Bounds()))
            {
                go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                novos++;
            }
            if (r && go.name.StartsWith("Terra_")) r.shadowCastingMode = ShadowCastingMode.Off;
        }
        EditorSceneManager.MarkSceneDirty(baseGo.scene);
        Debug.Log($"[Missão Lunar] {novos} colisores novos.");
    }

    [MenuItem("Missão Lunar/2. Montar cena da Lua")]
    public static void BuildScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var baseGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        baseGo.name = "BaseLunar";
        int cols = 0;
        foreach (var mf in baseGo.GetComponentsInChildren<MeshFilter>(true))
        {
            var go = mf.gameObject;
            var r = go.GetComponent<Renderer>();
            if (NeedsCollider(go.name, r ? r.bounds : new Bounds()))
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                cols++;
            }
            bool dynamic = go.name.StartsWith("REPAIR_") || go.name.StartsWith("Porta_");
            GameObjectUtility.SetStaticEditorFlags(go, dynamic ? 0 : (StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic));
            if (r && go.name.StartsWith("Terra_")) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Sol baixo (sombras longas) + luz fraca da Terra
        var sun = new GameObject("Sol").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 2.2f; sun.color = new Color(1f, 0.97f, 0.93f);
        sun.shadows = LightShadows.Soft; sun.shadowStrength = 1f;
        sun.transform.rotation = Quaternion.Euler(12f, 122f, 0f);   // mesmo ângulo do sol no Blender
        var earthLight = new GameObject("Luz_da_Terra").AddComponent<Light>();
        earthLight.type = LightType.Directional; earthLight.intensity = 0.08f; earthLight.color = new Color(0.6f, 0.75f, 1f);
        earthLight.shadows = LightShadows.None; earthLight.transform.rotation = Quaternion.Euler(20f, -20f, 0f);

        // luzes internas de emergência (posições do Blender: x, y, z -> Unity: -x, z, -y)
        void Point(string n, Vector3 blender, Color c, float intensity, float range)
        {
            var l = new GameObject(n).AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.Soft;
            l.transform.position = BlenderToUnity(blender);
        }
        Point("Luz_Hub_Emergencia", new Vector3(0, 0, 4.3f), new Color(1f, 0.6f, 0.25f), 4f, 12f);
        Point("Luz_Hub_Controle", new Vector3(0, 6.5f, 3.8f), new Color(0.4f, 0.8f, 1f), 1.5f, 6f);
        Point("Luz_Eclusa", new Vector3(0, -14.5f, 3.6f), new Color(1f, 0.55f, 0.2f), 2.5f, 6f);
        Point("Luz_Abrigo", new Vector3(-16.85f, 14.14f, 3.5f), new Color(1f, 0.6f, 0.25f), 2f, 8f);
        Point("Luz_SuporteVida", new Vector3(16f, 0f, 3.7f), new Color(1f, 0.6f, 0.25f), 2.5f, 9f);
        Point("Luz_Farol", new Vector3(-3.2f, -3.5f, 8.4f), new Color(1f, 0.45f, 0.1f), 6f, 15f);

        // Céu estrelado + ambiente escuro
        RenderSettings.skybox = StarSkybox();
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.055f);
        RenderSettings.sun = sun;
        RenderSettings.fog = false;

        // Pós-processamento (bloom nas luzes, tonemapping, vinheta)
        var vol = new GameObject("PosProcessamento").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = VolumeProfileAsset();

        // Jogador 1,80 m no SPAWN_Player
        var spawn = baseGo.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SPAWN_Player");
        var player = new GameObject("Jogador");
        player.transform.position = (spawn ? spawn.position : Vector3.zero) + Vector3.up * 1.0f;
        player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.3f; cc.center = Vector3.zero; cc.slopeLimit = 40f; cc.stepOffset = 0.35f;
        player.AddComponent<JogadorFPS>();
        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        cam.transform.SetParent(player.transform, false);
        cam.transform.localPosition = new Vector3(0f, 0.8f, 0f);   // olhos a ~1,70 m do chão
        var camera = cam.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f; camera.farClipPlane = 6000f; camera.fieldOfView = 70f;
        cam.AddComponent<AudioListener>();
        var data = cam.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;

        // estilo cartoon: céu, pós, contorno e luzes (sobrescreve os valores acima)
        EstiloCartoon.MaterialCeu();
        EstiloCartoon.ConfigurarPosProcessamento(vol.sharedProfile);
        EstiloCartoon.InstalarContorno();
        EstiloCartoon.AplicarIluminacaoNaCena();

        EditorSceneManager.SaveScene(scene, ScenePath);
        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == ScenePath)) { list.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
        Debug.Log($"[Missão Lunar] Cena criada em {ScenePath} com {cols} colisores.");
    }

    // Blender (Z para cima) -> Unity (Y para cima), igual ao export FBX usado (-Z forward, Y up)
    public static Vector3 BlenderToUnity(Vector3 b) => new Vector3(-b.x, b.z, -b.y);

    static Material StarSkybox()
    {
        string texPath = $"{TexDir}/Ceu_Estrelas.png";
        if (!File.Exists(texPath))
        {
            int w = 4096, h = 2048;
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color32[w * h];
            var rnd = new System.Random(7);
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(1, 1, 2, 255);
            for (int s = 0; s < 9000; s++)
            {
                int x = rnd.Next(w), y = rnd.Next(h);
                float b = (float)System.Math.Pow(rnd.NextDouble(), 3);
                byte v = (byte)(60 + 195 * b);
                byte blue = (byte)System.Math.Min(255, v + 15);
                px[y * w + x] = new Color32(v, v, blue, 255);
                if (b > 0.7f && x + 1 < w) px[y * w + x + 1] = new Color32((byte)(v / 2), (byte)(v / 2), (byte)(blue / 2), 255);
            }
            t.SetPixels32(px); File.WriteAllBytes(texPath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath);
        }
        string matPath = $"{MatDir}/Ceu_Estrelas.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null) { m = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(m, matPath); }
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        m.SetFloat("_Exposure", 1.2f);
        return m;
    }

    static VolumeProfile VolumeProfileAsset()
    {
        string path = $"{Root}/PosProcessamento_Lua.asset";
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p != null) return p;
        p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);
        var bloom = p.Add<Bloom>(true); bloom.threshold.Override(1f); bloom.intensity.Override(0.8f); bloom.scatter.Override(0.6f);
        var tone = p.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
        var vig = p.Add<Vignette>(true); vig.intensity.Override(0.25f);
        var ca = p.Add<ColorAdjustments>(true); ca.contrast.Override(12f); ca.postExposure.Override(0.3f);
        foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
        AssetDatabase.SaveAssets();
        return p;
    }

    [MenuItem("Missão Lunar/Fazer tudo (materiais + cena)")]
    public static void All() { CreateMaterials(); BuildScene(); }
}
