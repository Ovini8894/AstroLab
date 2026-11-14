using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = System.Random;

// Estilo cartoon da Base Lunar: texturas desenhadas por código, materiais no shader AstroLab/Toon,
// céu, pós-processamento, contorno (renderer feature) e iluminação da cena.
// Menu: Missão Lunar > Estilo Cartoon. Tudo sai da PaletaLua (Assets/BaseLunar/Scripts/PaletaLua.cs).
public static class EstiloCartoon
{
    const string Root = "Assets/BaseLunar";
    const string TexDir = Root + "/Texturas/Cartoon";
    const string MatDirs = Root + "/Materiais";
    const string MatConsertada = "Assets/BaseConsertada/Materiais";
    const string ContornoMat = Root + "/Shaders/ContornoCartoon.mat";
    const int S = 512;

    // ================================================================== menu
    [MenuItem("Missão Lunar/Estilo Cartoon/Aplicar tudo (texturas + materiais + céu + luz + contorno)")]
    public static void AplicarTudo()
    {
        GerarTexturas();
        ConfigurarTodosMateriais();
        MaterialCeu();
        ConfigurarPosProcessamento(AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{Root}/PosProcessamento_Lua.asset"));
        InstalarContorno();
        AplicarIluminacaoNaCena();
        Debug.Log("[Estilo Cartoon] Aplicado. Salve a cena para manter a iluminação.");
    }

    [MenuItem("Missão Lunar/Estilo Cartoon/Só regerar texturas e materiais")]
    public static void SoMateriais()
    {
        GerarTexturas();
        ConfigurarTodosMateriais();
    }

    // ================================================================== materiais
    public static void ConfigurarTodosMateriais()
    {
        int n = 0;
        foreach (var dir in new[] { MatDirs, MatConsertada })
        {
            if (!AssetDatabase.IsValidFolder(dir)) continue;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.name == "Ceu_Estrelas") continue;
                ConfigurarMaterial(m, m.name);
                AssetDatabase.SaveAssetIfDirty(m);
                n++;
            }
        }
        Debug.Log($"[Estilo Cartoon] {n} materiais no shader AstroLab/Toon.");
    }

    static Texture2D T(string nome) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{nome}.png");

    // Cor de emissão HDR: SetColor converte de gama para linear, então passamos (linear × intensidade) em gama.
    static Color Hdr(Color srgb, float intensidade) => (srgb.linear * intensidade).gamma;

    public static void ConfigurarMaterial(Material m, string nome)
    {
        if (nome == "M_Terra_Atmosfera")                                   // halo da Terra: shader próprio
        {
            m.shader = Shader.Find("AstroLab/Atmosfera");
            m.SetColor("_Cor", Hdr(Hx("#7FD4FF"), 1.6f));
            m.SetFloat("_Potencia", 2.5f); m.SetFloat("_Suavidade", 0.3f); m.SetFloat("_Forca", 1.2f);
            EditorUtility.SetDirty(m);
            return;
        }
        var toon = Shader.Find("AstroLab/Toon");
        if (toon == null) { Debug.LogError("[Estilo Cartoon] Shader AstroLab/Toon não encontrado."); return; }
        m.shader = toon;
        m.SetTexture("_BaseMap", null);
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetColor("_BaseColor", Color.white);
        m.SetColor("_EmissionColor", Color.black);
        m.SetFloat("_EmissaoUsaTextura", 0f);
        m.SetFloat("_SemNevoa", nome.StartsWith("M_Terra") && nome != "M_Terra_Vaso" ? 1f : 0f);   // a Terra no céu não some na neblina
        // chão da Lua: o chão plano (sol a 32°, N·L≈0,53) fica na luz, mas paredes de cratera inclinadas
        // ~10° para longe do sol já caem no meio-tom — o relevo aparece mesmo visto de perto
        m.SetFloat("_FaixaDesloc", nome == "M_Regolito" ? 0.1f : 0f);
        m.SetFloat("_RimForca", 0.3f);
        m.SetFloat("_RimLargura", 0.25f);
        m.SetFloat("_BrilhoTamanho", 0f);
        m.SetFloat("_BrilhoForca", 0.6f);
        Opaco(m);

        void Tex(string t, Color c) { m.SetTexture("_BaseMap", T(t)); m.SetColor("_BaseColor", c); }
        void Cor(Color c) => m.SetColor("_BaseColor", c);
        void Brilho(float tam, float forca) { m.SetFloat("_BrilhoTamanho", tam); m.SetFloat("_BrilhoForca", forca); }
        void Luz(Color c, float i) { Cor(c); m.SetColor("_EmissionColor", Hdr(c, i)); m.SetFloat("_RimForca", 0f); }

        switch (nome)
        {
            case "M_Casco": Tex("T_Casco_Toon", PaletaLua.Casco); Brilho(0.035f, 0.35f); break;
            case "M_Casco_Escuro": Tex("T_CascoEscuro_Toon", PaletaLua.CascoEscuro); break;
            case "M_Casco_Queimado": Tex("T_CascoQueimado_Toon", Color.white); break;
            case "M_Piso": Tex("T_Piso_Toon", PaletaLua.Piso); break;
            case "M_Metal": Tex("T_Metal_Toon", PaletaLua.Metal); Brilho(0.05f, 0.5f); break;
            case "M_Metal_Enferrujado": Tex("T_MetalEnferrujado_Toon", Color.white); break;
            case "M_Amarelo": Tex("T_Amarelo_Toon", PaletaLua.Amarelo); break;
            case "M_Faixa_Azul": Tex("T_Faixa_Toon", PaletaLua.Faixa); break;
            case "M_Regolito":
                Tex("T_Regolito_Toon", PaletaLua.Regolito); m.SetFloat("_RimForca", 0.12f);
                m.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));     // manchas largas (chão tem ~6 m por repetição)
                break;
            case "M_Rocha": Tex("T_Rocha_Toon", PaletaLua.Rocha); m.SetFloat("_RimForca", 0.2f); break;
            case "M_Tecido": Tex("T_Tecido_Toon", PaletaLua.Tecido); break;
            case "M_Painel_Solar": Tex("T_PainelSolar_Toon", Color.white); Brilho(0.025f, 0.35f); break;
            case "M_Painel_Quebrado": Tex("T_PainelQuebrado_Toon", Color.white); break;
            case "M_Terra_Vaso": Cor(PaletaLua.TerraVaso); break;
            case "M_Planta_Morta": Cor(PaletaLua.PlantaMorta); break;
            case "M_Planta_Viva": Cor(PaletaLua.PlantaViva); break;
            case "M_Vermelho": Cor(PaletaLua.Vermelho); Brilho(0.04f, 0.4f); break;
            case "M_Branco": Cor(PaletaLua.Branco); break;
            case "M_Tela": Tex("T_Tela_Toon", Color.white); m.SetColor("_EmissionColor", Hdr(Color.white, 1.6f)); m.SetFloat("_EmissaoUsaTextura", 1f); break;
            case "M_Tela_Quebrada": Tex("T_TelaQuebrada_Toon", Color.white); break;
            case "M_Luz_Laranja": Luz(PaletaLua.LuzLaranja, 4f); break;
            case "M_Luz_Ambar": Luz(PaletaLua.LuzAmbar, 2.5f); break;
            case "M_Luz_Verde": Luz(PaletaLua.LuzVerde, 4f); break;
            case "M_Luz_Branca": Luz(PaletaLua.LuzBranca, 3.5f); break;
            case "M_Luz_Apagada": Cor(new Color(0.42f, 0.4f, 0.47f)); break;
            case "M_Terra":                                                  // textura pintada no Blender (crateras_e_terra.py)
                if (T("T_Terra_Toon") != null)
                {
                    Tex("T_Terra_Toon", Color.white);
                    m.SetColor("_EmissionColor", Hdr(Color.white, 0.35f)); m.SetFloat("_EmissaoUsaTextura", 1f);
                }
                else { Cor(PaletaLua.Planeta); m.SetColor("_EmissionColor", Hdr(PaletaLua.Planeta, 0.9f)); }
                m.SetFloat("_RimForca", 0.6f); m.SetFloat("_RimLargura", 0.3f); break;
            case "M_Terra_Nuvens":
                Tex("T_TerraNuvens_Toon", Color.white);
                m.SetColor("_EmissionColor", Hdr(Color.white, 0.25f)); m.SetFloat("_RimForca", 0f);
                Transparente(m); m.SetFloat("_Cull", (float)CullMode.Back); break;
            case "M_Vidro":
                Cor(new Color(PaletaLua.Vidro.r, PaletaLua.Vidro.g, PaletaLua.Vidro.b, 0.25f));
                Brilho(0.08f, 1.2f); m.SetFloat("_RimForca", 0.6f); Transparente(m); break;
            case "M_Vidro_Rachado":
                Tex("T_VidroRachado_Toon", PaletaLua.Vidro); m.SetFloat("_RimForca", 0.5f); Transparente(m); break;
            case "M_Agua":
                Cor(new Color(PaletaLua.Agua.r, PaletaLua.Agua.g, PaletaLua.Agua.b, 0.75f));
                m.SetColor("_EmissionColor", Hdr(PaletaLua.Agua, 0.35f)); Brilho(0.08f, 1f); Transparente(m); break;
            default:
                Debug.LogWarning($"[Estilo Cartoon] Material {nome} sem receita: ficou branco no Toon.");
                break;
        }
        EditorUtility.SetDirty(m);
    }

    static void Opaco(Material m)
    {
        m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero);
        m.SetFloat("_ZWrite", 1); m.SetFloat("_Cull", (float)CullMode.Back);
        m.SetOverrideTag("RenderType", "Opaque");
        m.renderQueue = (int)RenderQueue.Geometry;
    }

    static void Transparente(Material m)
    {
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", (float)CullMode.Off);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    // ================================================================== céu
    public static Material MaterialCeu()
    {
        // 1º: céu renderizado no Blender (Blender~/ceu_estrelas.py) como cubemap — estrelas redondas, Via Láctea, nebulosas
        string ceuBlender = $"{TexDir}/Ceu_Estrelas_Blender.png";
        if (File.Exists(ceuBlender))
        {
            var ci = (TextureImporter)AssetImporter.GetAtPath(ceuBlender);
            if (ci != null)
            {
                ci.textureShape = TextureImporterShape.TextureCube;
                ci.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
                ci.maxTextureSize = 2048;                                     // por face do cubo
                ci.mipmapEnabled = false;
                ci.textureCompression = TextureImporterCompression.CompressedHQ;
                ci.SaveAndReimport();
            }
            string matCeu = $"{MatDirs}/Ceu_Estrelas.mat";
            var mc = AssetDatabase.LoadAssetAtPath<Material>(matCeu);
            if (mc == null) { mc = new Material(Shader.Find("Skybox/Cubemap")); AssetDatabase.CreateAsset(mc, matCeu); }
            mc.shader = Shader.Find("Skybox/Cubemap");
            mc.SetTexture("_Tex", AssetDatabase.LoadAssetAtPath<Cubemap>(ceuBlender));
            mc.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            mc.SetFloat("_Exposure", 1.25f);                                  // as estrelas mais fortes passam do limiar do bloom
            mc.SetFloat("_Rotation", 40f);
            EditorUtility.SetDirty(mc);
            AssetDatabase.SaveAssetIfDirty(mc);
            return mc;
        }

        // 2º (reserva): céu desenhado por código
        string texPath = $"{TexDir}/Ceu_Cartoon.png";
        int w = 2048, h = 1024;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);                                  // 0 = embaixo, 1 = em cima
            float t = Mathf.Clamp01((v - 0.5f) / 0.35f);
            var c = Color.Lerp(PaletaLua.CeuHorizonte, PaletaLua.CeuTopo, Mathf.SmoothStep(0, 1, t));
            for (int x = 0; x < w; x++) px[y * w + x] = c;
        }
        var rnd = new Random(11);
        void Ponto(int cx, int cy, float r, Color c)
        {
            for (int dy = -(int)r - 1; dy <= r + 1; dy++)
            for (int dx = -(int)r - 1; dx <= r + 1; dx++)
            {
                int x = (cx + dx + w) % w, y = cy + dy;
                if (y < 0 || y >= h) continue;
                float a = Mathf.Clamp01(r + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * w + x] = Color.Lerp(px[y * w + x], c, a);
            }
        }
        for (int i = 0; i < 2200; i++)
        {
            int x = rnd.Next(w), y = h / 2 + rnd.Next(h / 2);
            float b = (float)rnd.NextDouble();
            Ponto(x, y, b > 0.92f ? 1.4f : 0.7f, Color.Lerp(PaletaLua.CeuHorizonte, Color.white, 0.45f + 0.55f * b));
        }
        for (int i = 0; i < 70; i++)                                       // estrelas "de desenho" (cruz de 4 pontas)
        {
            int x = rnd.Next(w), y = h / 2 + 20 + rnd.Next(h / 2 - 40);
            var c = i % 5 == 0 ? new Color(1f, 0.9f, 0.6f) : new Color(0.85f, 0.92f, 1f);
            int r = 4 + rnd.Next(5);
            for (int k = -r; k <= r; k++)
            {
                float a = 1f - Mathf.Abs(k) / (float)(r + 1);
                Ponto(x + k, y, 0.5f * a + 0.2f, c);
                Ponto(x, y + k, 0.5f * a + 0.2f, c);
            }
            Ponto(x, y, 1.6f, Color.white);
        }
        Directory.CreateDirectory(TexDir);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.SetPixels(px); File.WriteAllBytes(texPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
        ti.maxTextureSize = 4096; ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp;
        ti.SaveAndReimport();

        string matPath = $"{MatDirs}/Ceu_Estrelas.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null) { m = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(m, matPath); }
        m.shader = Shader.Find("Skybox/Panoramic");
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
        m.SetFloat("_Exposure", 1.0f);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssetIfDirty(m);
        return m;
    }

    // ================================================================== pós-processamento
    public static void ConfigurarPosProcessamento(VolumeProfile p)
    {
        if (p == null) return;
        T Get<T>() where T : VolumeComponent { if (!p.TryGet<T>(out var c)) { c = p.Add<T>(true); AssetDatabase.AddObjectToAsset(c, p); } c.active = true; return c; }
        var tone = Get<Tonemapping>(); tone.mode.Override(TonemappingMode.Neutral);
        var bloom = Get<Bloom>(); bloom.threshold.Override(1.0f); bloom.intensity.Override(0.7f); bloom.scatter.Override(0.65f); bloom.tint.Override(Color.white);
        var ca = Get<ColorAdjustments>(); ca.postExposure.Override(0f); ca.contrast.Override(10f); ca.saturation.Override(15f); ca.colorFilter.Override(Color.white);
        var vig = Get<Vignette>(); vig.intensity.Override(0.2f); vig.color.Override(PaletaLua.Contorno); vig.smoothness.Override(0.4f);
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssetIfDirty(p);
    }

    // ================================================================== contorno (renderer feature) + sombras
    public static void InstalarContorno()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ContornoMat);
        if (mat == null) { mat = new Material(Shader.Find("AstroLab/ContornoCartoon")); AssetDatabase.CreateAsset(mat, ContornoMat); }
        mat.shader = Shader.Find("AstroLab/ContornoCartoon");
        mat.SetColor("_Cor", PaletaLua.Contorno);
        AssetDatabase.SaveAssetIfDirty(mat);

        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            var f = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(x => x.name == "ContornoCartoon");
            if (f == null)
            {
                f = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                f.name = "ContornoCartoon";
                AssetDatabase.AddObjectToAsset(f, data);
                data.rendererFeatures.Add(f);
            }
            f.passMaterial = mat;
            f.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            f.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            f.fetchColorBuffer = true;
            f.SetActive(true);
            EditorUtility.SetDirty(f);
            data.SetDirty();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }

        // sombra do sol cobrindo a base inteira (o padrão de 50 m cortava as sombras)
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (rp.shadowDistance < 120f) { rp.shadowDistance = 120f; EditorUtility.SetDirty(rp); AssetDatabase.SaveAssetIfDirty(rp); }
        }
    }

    // ================================================================== iluminação da cena
    public static void AplicarIluminacaoNaCena()
    {
        Light L(string n) { var go = GameObject.Find(n); return go ? go.GetComponent<Light>() : null; }
        Light Ponto(string n, Vector3 pos)
        {
            var l = L(n);
            if (l == null) { l = new GameObject(n).AddComponent<Light>(); l.type = LightType.Point; l.transform.position = pos; l.shadows = LightShadows.Soft; }
            return l;
        }

        var sol = L("Sol");
        if (sol)
        {
            sol.color = PaletaLua.Sol; sol.intensity = 1.15f;
            sol.transform.rotation = Quaternion.Euler(32f, 122f, 0f);       // mais alto que antes: sombras longas, mas sem engolir tudo
            sol.shadows = LightShadows.Soft; sol.shadowStrength = 1f;
            RenderSettings.sun = sol;
        }
        var terra = L("Luz_da_Terra");
        if (terra) { terra.color = PaletaLua.LuzTerra; terra.intensity = 0.35f; terra.shadows = LightShadows.None; }

        // sombra só nas lâmpadas principais dos cômodos (menos mapas de sombra disputando o atlas)
        void Ajusta(Light l, Color c, float i, float r, bool sombra) { if (l) { l.color = c; l.intensity = i; l.range = r; l.shadows = sombra ? LightShadows.Soft : LightShadows.None; } }
        Ajusta(L("Luz_Hub_Emergencia"), PaletaLua.LuzAmbar, 3f, 14f, true);
        Ajusta(L("Luz_Hub_Controle"), PaletaLua.Tela, 2.2f, 8f, false);
        Ajusta(L("Luz_Eclusa"), PaletaLua.LuzAmbar, 2.5f, 8f, true);
        Ajusta(L("Luz_Abrigo"), PaletaLua.LuzAmbar, 2.5f, 10f, true);
        Ajusta(L("Luz_SuporteVida"), PaletaLua.LuzAmbar, 3f, 11f, true);
        Ajusta(L("Luz_Farol"), PaletaLua.LuzLaranja, 5f, 16f, false);
        Ajusta(Ponto("Luz_Estufa", BaseLunarSetup.BlenderToUnity(new Vector3(-18.5f, 0f, 3.4f))), PaletaLua.PlantaViva, 2.2f, 11f, false);

        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MatDirs}/Ceu_Estrelas.mat");
        RenderSettings.ambientMode = AmbientMode.Trilight;                 // para qualquer objeto que não use o Toon
        RenderSettings.ambientSkyColor = PaletaLua.AmbienteCeu;
        RenderSettings.ambientEquatorColor = Color.Lerp(PaletaLua.AmbienteCeu, PaletaLua.AmbienteChao, 0.5f);
        RenderSettings.ambientGroundColor = PaletaLua.AmbienteChao;
        RenderSettings.fog = true;                                          // profundidade: o horizonte some no violeta do céu
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.Lerp(PaletaLua.CeuHorizonte, PaletaLua.Regolito, 0.25f);
        RenderSettings.fogStartDistance = 140f;
        RenderSettings.fogEndDistance = 900f;

        var ilum = UnityEngine.Object.FindAnyObjectByType<IluminacaoCartoon>();
        if (ilum == null) ilum = new GameObject("IluminacaoCartoon").AddComponent<IluminacaoCartoon>();
        ilum.Aplicar();

        foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            var d = cam.GetUniversalAdditionalCameraData();
            d.renderPostProcessing = true;
            d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            EditorUtility.SetDirty(d);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // ================================================================== texturas
    // Pintor simples com formas anti-serrilhadas que "dão a volta" (texturas repetem sem emenda).
    sealed class Tela
    {
        public readonly Color[] px = new Color[S * S];
        public Tela(Color fundo) { for (int i = 0; i < px.Length; i++) px[i] = fundo; }

        void Mistura(int x, int y, Color c, float a)
        {
            if (a <= 0) return;
            x = ((x % S) + S) % S; y = ((y % S) + S) % S;
            int i = y * S + x;
            px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(a) * c.a);
        }

        public void Alfa(int x, int y, float a) { x = ((x % S) + S) % S; y = ((y % S) + S) % S; px[y * S + x].a = Mathf.Max(px[y * S + x].a, a); }

        public void Forma(float x0, float y0, float x1, float y1, Func<float, float, float> sdf, Color c, float a = 1f, bool soAlfa = false)
        {
            for (int y = Mathf.FloorToInt(y0) - 2; y <= Mathf.CeilToInt(y1) + 2; y++)
            for (int x = Mathf.FloorToInt(x0) - 2; x <= Mathf.CeilToInt(x1) + 2; x++)
            {
                float cov = Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f)) * a;
                if (cov <= 0) continue;
                if (soAlfa) Alfa(x, y, cov); else Mistura(x, y, c, cov);
            }
        }

        public void Ret(float x0, float y0, float x1, float y1, Color c, float a = 1f, float raio = 0f)
        {
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, hx = (x1 - x0) / 2 - raio, hy = (y1 - y0) / 2 - raio;
            Forma(x0, y0, x1, y1, (x, y) =>
            {
                float qx = Mathf.Abs(x - cx) - hx, qy = Mathf.Abs(y - cy) - hy;
                return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - raio;
            }, c, a);
        }

        public void Circ(float cx, float cy, float r, Color c, float a = 1f) =>
            Forma(cx - r, cy - r, cx + r, cy + r, (x, y) => new Vector2(x - cx, y - cy).magnitude - r, c, a);

        public void Arco(float cx, float cy, float r, float w, float ang0, float ang1, Color c, float a = 1f) =>
            Forma(cx - r - w, cy - r - w, cx + r + w, cy + r + w, (x, y) =>
            {
                float ang = Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg; if (ang < 0) ang += 360;
                bool dentro = ang0 <= ang1 ? (ang >= ang0 && ang <= ang1) : (ang >= ang0 || ang <= ang1);
                return dentro ? Mathf.Abs(new Vector2(x - cx, y - cy).magnitude - r) - w / 2 : 99f;
            }, c, a);

        public void Linha(float x0, float y0, float x1, float y1, float w, Color c, float a = 1f, bool soAlfa = false)
        {
            var p0 = new Vector2(x0, y0); var d = new Vector2(x1 - x0, y1 - y0); float l2 = Mathf.Max(d.sqrMagnitude, 1e-4f);
            Forma(Mathf.Min(x0, x1) - w, Mathf.Min(y0, y1) - w, Mathf.Max(x0, x1) + w, Mathf.Max(y0, y1) + w, (x, y) =>
            {
                var p = new Vector2(x, y) - p0; float t = Mathf.Clamp01(Vector2.Dot(p, d) / l2);
                return (p - d * t).magnitude - w / 2;
            }, c, a, soAlfa);
        }

        public void Rebite(float x, float y, float r, float escuro)
        {
            Circ(x + 1, y - 1, r, Cinza(escuro - 0.12f));
            Circ(x, y, r, Cinza(escuro));
            Circ(x - r * 0.3f, y + r * 0.3f, r * 0.35f, Cinza(1f));
        }

        public void Rachadura(Random rnd, float x, float y, int ramos, float comp, float w, Color c, bool soAlfa = false)
        {
            for (int b = 0; b < ramos; b++)
            {
                float ang = (float)(rnd.NextDouble() * Math.PI * 2), px0 = x, py0 = y;
                int seg = 3 + rnd.Next(3);
                for (int s = 0; s < seg; s++)
                {
                    ang += (float)(rnd.NextDouble() - 0.5) * 0.9f;
                    float l = comp / seg * (0.6f + (float)rnd.NextDouble() * 0.8f);
                    float px1 = px0 + Mathf.Cos(ang) * l, py1 = py0 + Mathf.Sin(ang) * l;
                    Linha(px0, py0, px1, py1, Mathf.Max(1f, w * (1f - s / (float)seg)), c, 1f, soAlfa);
                    px0 = px1; py0 = py1;
                }
            }
        }

        public void Salvar(string nome, bool alfa = false)
        {
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
            t.SetPixels(px); var path = $"{TexDir}/{nome}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.alphaIsTransparency = alfa; ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 4; ti.maxTextureSize = 512;
            ti.SaveAndReimport();
        }
    }

    static Color Cinza(float v, float a = 1f) => new Color(v, v, v, a);
    static Color Hx(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

    // Placas com emenda, chanfro de luz/sombra e rebites (multiplica a cor do material).
    static void Placas(Tela t, int nx, int ny, float emenda, float rebite)
    {
        float w = S / (float)nx, h = S / (float)ny;
        for (int j = 0; j < ny; j++)
        for (int i = 0; i < nx; i++)
        {
            float x0 = i * w, y0 = j * h, x1 = x0 + w, y1 = y0 + h;
            t.Ret(x0 + 8, y1 - 12, x1 - 8, y1 - 8, Cinza(1f));            // chanfro claro em cima
            t.Ret(x0 + 8, y0 + 8, x1 - 8, y0 + 12, Cinza(0.86f));          // chanfro escuro embaixo
            t.Ret(x1 - 12, y0 + 8, x1 - 8, y1 - 8, Cinza(0.88f));
            float m = 16;
            foreach (var (rx, ry) in new[] { (x0 + m, y0 + m), (x1 - m, y0 + m), (x0 + m, y1 - m), (x1 - m, y1 - m) })
                t.Rebite(rx, ry, rebite, 0.8f);
        }
        for (int i = 0; i <= nx; i++) t.Ret(i * w - emenda / 2, -2, i * w + emenda / 2, S + 2, Cinza(0.6f));
        for (int j = 0; j <= ny; j++) t.Ret(-2, j * h - emenda / 2, S + 2, j * h + emenda / 2, Cinza(0.6f));
    }

    public static void GerarTexturas()
    {
        Directory.CreateDirectory(TexDir);

        // ---- casco creme: 2x2 placas com rebites e uma tampinha de inspeção
        var t = new Tela(Cinza(0.97f));
        Placas(t, 2, 2, 5, 5);
        t.Ret(300, 300, 380, 360, Cinza(0.82f), 1, 6); t.Ret(304, 304, 376, 356, Cinza(0.93f), 1, 5);
        t.Salvar("T_Casco_Toon");

        // ---- casco escuro: placa grande + grade de ventilação
        t = new Tela(Cinza(1f));
        Placas(t, 1, 2, 6, 6);
        for (int k = 0; k < 5; k++) t.Ret(300, 60 + k * 26, 460, 74 + k * 26, Cinza(0.5f), 1, 7);
        t.Ret(60, 330, 200, 345, Cinza(0.75f), 1, 7);
        t.Salvar("T_CascoEscuro_Toon");

        // ---- piso: 4x4 ladrilhos com ranhura e antiderrapante
        t = new Tela(Cinza(1f));
        for (int j = 0; j < 4; j++)
        for (int i = 0; i < 4; i++)
        {
            float x0 = i * 128, y0 = j * 128;
            if ((i + j) % 2 == 0) t.Ret(x0, y0, x0 + 128, y0 + 128, Cinza(0.94f));
            for (int a = 0; a < 4; a++)
            for (int b = 0; b < 4; b++)
            {
                float cx = x0 + 20 + a * 29, cy = y0 + 20 + b * 29;
                if ((a + b) % 2 == 0) t.Linha(cx - 6, cy - 6, cx + 6, cy + 6, 4, Cinza(0.82f));
                else t.Linha(cx - 6, cy + 6, cx + 6, cy - 6, 4, Cinza(0.82f));
            }
        }
        for (int k = 0; k <= 4; k++) { t.Ret(k * 128 - 3, -2, k * 128 + 3, S + 2, Cinza(0.58f)); t.Ret(-2, k * 128 - 3, S + 2, k * 128 + 3, Cinza(0.58f)); }
        t.Salvar("T_Piso_Toon");

        // ---- metal: reflexos diagonais de desenho + parafusos
        t = new Tela(Cinza(0.93f));
        for (int k = -1; k <= 1; k++)
        {
            t.Linha(-100 + k * S, -100, 412 + k * S, 612, 46, Cinza(1f));
            t.Linha(40 + k * S, -100, 552 + k * S, 612, 14, Cinza(1f));
        }
        t.Ret(-2, S - 10, S + 2, S - 4, Cinza(0.7f));
        foreach (var x in new[] { 40f, 216f, 392f }) t.Rebite(x, S - 30, 6, 0.75f);
        t.Salvar("T_Metal_Toon");

        // ---- amarelo: listras de sinalização (repetem sem emenda)
        t = new Tela(Cinza(1f));
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float v = ((x + y) % 128) / 128f;                               // 4 pares de listras por repetição
            float cov = Mathf.Clamp01((Mathf.Abs(v - 0.5f) - 0.25f) * 128f / 1.4f + 0.5f);
            t.px[y * S + x] = Color.Lerp(Cinza(1f), Cinza(0.42f), cov);
        }
        t.Ret(-2, -2, S + 2, 10, Cinza(0.75f)); t.Ret(-2, S - 10, S + 2, S + 2, Cinza(0.75f));
        t.Salvar("T_Amarelo_Toon");

        // ---- faixa turquesa: setas de orientação
        t = new Tela(Cinza(1f));
        for (int k = 0; k < 4; k++)
        {
            float cx = 64 + k * 128;
            t.Linha(cx - 22, 196, cx + 14, 256, 18, Cinza(0.72f)); t.Linha(cx + 14, 256, cx - 22, 316, 18, Cinza(0.72f));
        }
        t.Ret(-2, 30, S + 2, 40, Cinza(1f)); t.Ret(-2, 472, S + 2, 482, Cinza(1f));
        t.Salvar("T_Faixa_Toon");

        // ---- regolito: as crateras agora são relevo de verdade (Blender~/crateras_e_terra.py);
        //      a textura só tem manchas suaves de tom e pedrinhas
        var rnd = new Random(3);
        t = new Tela(Cinza(1f));
        for (int k = 0; k < 170; k++) t.Circ(rnd.Next(S), rnd.Next(S), 1.5f + (float)rnd.NextDouble() * 2.5f, Cinza(0.86f + (float)rnd.NextDouble() * 0.07f));
        for (int k = 0; k < 40; k++) { float x = rnd.Next(S), y = rnd.Next(S); t.Circ(x + 1, y - 1, 3.5f, Cinza(0.8f)); t.Circ(x, y, 3f, Cinza(0.96f)); }
        t.Salvar("T_Regolito_Toon");

        // ---- rocha: manchas em 2 tons + rachaduras
        rnd = new Random(5);
        t = new Tela(Cinza(1f));
        for (int k = 0; k < 14; k++) t.Circ(rnd.Next(S), rnd.Next(S), 30 + rnd.Next(60), Cinza(0.86f));
        for (int k = 0; k < 10; k++) t.Circ(rnd.Next(S), rnd.Next(S), 10 + rnd.Next(25), Cinza(1f));
        for (int k = 0; k < 4; k++) t.Rachadura(rnd, rnd.Next(S), rnd.Next(S), 2, 110, 4, Cinza(0.62f));
        t.Salvar("T_Rocha_Toon");

        // ---- tecido: estofado em losango com costura
        t = new Tela(Cinza(1f));
        for (int k = -4; k <= 4; k++)
        {
            t.Linha(k * 128, 0, k * 128 + S, S, 4, Cinza(0.72f));
            t.Linha(k * 128, S, k * 128 + S, 0, 4, Cinza(0.72f));
        }
        for (int k = 0; k < 4; k++) for (int j = 0; j < 4; j++) t.Circ(64 + k * 128, 64 + j * 128, 5, Cinza(0.66f));
        t.Salvar("T_Tecido_Toon");

        // ---- painel solar (colorido): células azuis com grade clara e reflexo
        var azul = PaletaLua.PainelSolar; var azul2 = Color.Lerp(azul, Color.white, 0.18f); var grade = Hx("#D7E6FF");
        t = new Tela(azul);
        for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++)
        {
            float x0 = i * 256, y0 = j * 256;
            t.Ret(x0 + 6, y0 + 6, x0 + 250, y0 + 250, azul2, 1, 4);
            t.Ret(x0 + 6, y0 + 128, x0 + 250, y0 + 250, azul, 0.5f, 4);
            t.Linha(x0 + 40, y0 + 210, x0 + 110, y0 + 230, 10, Color.Lerp(azul2, Color.white, 0.6f));
        }
        for (int k = 0; k <= 2; k++) { t.Ret(k * 256 - 5, -2, k * 256 + 5, S + 2, grade); t.Ret(-2, k * 256 - 5, S + 2, k * 256 + 5, grade); }
        t.Ret(-2, 125, S + 2, 131, grade, 0.6f); t.Ret(125, -2, 131, S + 2, grade, 0.6f);
        t.Ret(381, -2, 387, S + 2, grade, 0.6f); t.Ret(-2, 381, S + 2, 387, grade, 0.6f);
        t.Salvar("T_PainelSolar_Toon");

        // ---- painel quebrado: células apagadas, uma faltando, rachaduras claras
        rnd = new Random(7);
        var apagado = Hx("#2C3A6E");
        t = new Tela(apagado);
        for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++) t.Ret(i * 256 + 6, j * 256 + 6, i * 256 + 250, j * 256 + 250, Color.Lerp(apagado, Color.white, 0.08f), 1, 4);
        t.Ret(262, 6, 506, 250, Hx("#151833"), 1, 4);
        for (int k = 0; k <= 2; k++) { t.Ret(k * 256 - 5, -2, k * 256 + 5, S + 2, Hx("#8C98B8")); t.Ret(-2, k * 256 - 5, S + 2, k * 256 + 5, Hx("#8C98B8")); }
        t.Rachadura(rnd, 140, 370, 6, 170, 4, Hx("#C9D8FF"));
        t.Rachadura(rnd, 100, 120, 4, 120, 3, Hx("#C9D8FF"));
        t.Salvar("T_PainelQuebrado_Toon");

        // ---- metal enferrujado (colorido)
        rnd = new Random(9);
        t = new Tela(PaletaLua.Metal);
        for (int k = 0; k < 12; k++)
        {
            float cx = rnd.Next(S), cy = rnd.Next(S), r = 20 + rnd.Next(50);
            t.Circ(cx, cy, r + 6, Hx("#8A4426"));
            t.Circ(cx, cy, r, PaletaLua.Enferrujado);
            t.Circ(cx - r * 0.3f, cy + r * 0.3f, r * 0.3f, Hx("#E08A4F"));
        }
        t.Salvar("T_MetalEnferrujado_Toon");

        // ---- casco queimado (colorido): fuligem e marcas
        rnd = new Random(13);
        t = new Tela(PaletaLua.Queimado);
        for (int k = 0; k < 10; k++) t.Circ(rnd.Next(S), rnd.Next(S), 30 + rnd.Next(60), Hx("#2B2433"));
        for (int k = 0; k < 6; k++) t.Rachadura(rnd, rnd.Next(S), rnd.Next(S), 3, 90, 5, Hx("#6A5A70"));
        t.Salvar("T_CascoQueimado_Toon");

        // ---- vidro rachado: transparente com rachaduras brancas opacas
        rnd = new Random(17);
        t = new Tela(new Color(1, 1, 1, 0.3f));
        t.Rachadura(rnd, 200, 260, 7, 230, 4, Color.white, true);
        t.Rachadura(rnd, 420, 90, 4, 120, 3, Color.white, true);
        t.Salvar("T_VidroRachado_Toon", true);

        // ---- tela ligada: gráfico e barras (emissivo)
        var fundoTela = Hx("#0B3A4A"); var ciano = PaletaLua.Tela;
        t = new Tela(fundoTela);
        for (int k = 0; k < 8; k++) t.Ret(-2, k * 64 + 30, S + 2, k * 64 + 32, Color.Lerp(fundoTela, ciano, 0.25f));
        float yAnt = 300;
        for (int x = 0; x <= S; x += 16) { float y = 300 + Mathf.Sin(x * 0.05f) * 50 + Mathf.Sin(x * 0.13f) * 20; t.Linha(x - 16, yAnt, x, y, 5, ciano); yAnt = y; }
        for (int k = 0; k < 6; k++) t.Ret(40 + k * 75, 60, 80 + k * 75, 90 + (k * 37 % 110), Color.Lerp(ciano, Color.white, 0.2f), 1, 3);
        t.Salvar("T_Tela_Toon");

        // ---- tela quebrada
        rnd = new Random(19);
        t = new Tela(Hx("#13151F"));
        t.Rachadura(rnd, 250, 250, 8, 260, 4, Hx("#5A6385"));
        t.Salvar("T_TelaQuebrada_Toon");

        AssetDatabase.Refresh();
        Debug.Log($"[Estilo Cartoon] Texturas geradas em {TexDir}.");
    }
}
