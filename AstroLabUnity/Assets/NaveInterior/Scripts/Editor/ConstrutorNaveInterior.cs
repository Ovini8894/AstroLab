using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstroLab.Capacete;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AstroLab.Nave.Editor
{
    // Monta o interior do onibus espacial a partir do que o Blender exporta (Assets/NaveInterior/Blender~/gerar_interior.py):
    //   1) Importar: regras de import dos FBX/PNG + materiais AstroLab/Toon (NaveInterior/Materiais)
    //   2) Montar cena: NaveInterior.unity (aberta como cena ADITIVA, salva e fechada: nao troca a cena de quem estiver no Editor)
    // Nunca chama AssetDatabase.SaveAssets (so SaveAssetIfDirty nos assets criados aqui e SaveScene na cena).
    public static class ConstrutorNaveInterior
    {
        const string R = "Assets/NaveInterior";
        const string FbxInt = R + "/Modelos/NI_Interior.fbx";
        const string FbxKit = R + "/Modelos/NI_Kit.fbx";
        const string Json = R + "/Modelos/NI_Instancias.json";
        const string MatDir = R + "/Materiais";
        public const string CenaPath = R + "/Cenas/NaveInterior.unity";
        const string MaosPrefab = "Assets/CentroTreinamento/Prefabs/Maos_LuvaTraje.prefab";   // so referencia
        const string Fonte = "Assets/MenuInicial/Fontes/Exo2 SDF.asset";

        static Color Hx(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
        static Color Hdr(Color srgb, float i) => (srgb.linear * i).gamma;

        static readonly Dictionary<string, string> Cores = new Dictionary<string, string>
        {
            { "NI_Painel", "#56627D" }, { "NI_Moldura", "#2F3954" }, { "NI_Escuro", "#2A2C3D" }, { "NI_Tecla", "#4A5068" },
            { "NI_Metal", "#C3CEDF" }, { "NI_Branco", "#F4F2EC" }, { "NI_Linha", "#E8E6E0" }, { "NI_Parede", "#DAD6CC" },
            { "NI_Parede_Escura", "#A9AEBE" }, { "NI_Estrutura", "#7C8CB8" }, { "NI_Piso", "#6E7A9A" }, { "NI_Tecido", "#4F6496" },
            { "NI_Cinto", "#C9B48A" }, { "NI_Amarelo", "#FFC23A" }, { "NI_Vermelho", "#E03C2A" }, { "NI_Medidor", "#E9E2C8" },
            { "NI_Lente_Apagada", "#3E4C78" }, { "NI_Lente_Verde", "#59FF86" }, { "NI_Lente_Ambar", "#FFC23A" },
            { "NI_Lente_Vermelha", "#FF4A4A" }, { "NI_Lente_Branca", "#FFF4DE" }, { "NI_Luz", "#FFF4DE" }, { "NI_Vidro", "#9CC8F0" },
            { "NI_Etiquetas", "#FFFFFF" }, { "NI_Legendas", "#FFFFFF" }, { "NI_Legendas_Acesas", "#FFFFFF" }, { "NI_Tela", "#FFFFFF" },
        };

        // ================================================================== 1. importacao + materiais
        [MenuItem("AstroLab/Nave Interior/1. Importar modelos e materiais")]
        public static void ImportarMenu() => Debug.Log("[Nave] " + Importar());

        public static string Importar()
        {
            var log = new List<string>();
            foreach (var t in new[] { R + "/Texturas/T_NI_Etiquetas.png", R + "/Texturas/T_NI_MEDS.png" })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(t);
                if (ti == null) { log.Add("sem textura " + t); continue; }
                ti.maxTextureSize = 2048; ti.anisoLevel = 8; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.sRGBTexture = true;
                ti.SaveAndReimport();
            }
            foreach (var f in new[] { FbxInt, FbxKit })
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(f);
                if (mi == null) { log.Add("sem fbx " + f); continue; }
                // o FBX ja vem com eixos e metros embutidos (bake_space_transform no Blender)
                mi.globalScale = 1f; mi.useFileScale = true; mi.bakeAxisConversion = false;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.None;
                mi.importCameras = false; mi.importLights = false; mi.importAnimation = false; mi.importBlendShapes = false;
                mi.animationType = ModelImporterAnimationType.None; mi.generateSecondaryUV = false;
                mi.isReadable = true;                    // StaticBatchingUtility.Combine em runtime (a nave gira conforme a fase)
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.SaveAndReimport();
                var nomes = AssetDatabase.LoadAllAssetsAtPath(f).OfType<Material>().Select(x => x.name)
                    .Concat(mi.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name)).Distinct().ToList();
                foreach (var n in nomes) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), Material(n));
                mi.SaveAndReimport();
                log.Add($"{Path.GetFileName(f)}: {nomes.Count} materiais");
            }
            return string.Join("\n", log);
        }

        static Material Material(string nome)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("AstroLab/Toon")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("AstroLab/Toon");
            var cor = Cores.TryGetValue(nome, out var h) ? Hx(h) : Color.magenta;
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", cor);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_EmissaoUsaTextura", 0f);
            m.SetFloat("_SemNevoa", 0f);
            m.SetFloat("_FaixaDesloc", 0f);
            m.SetFloat("_RimForca", 0.18f);
            m.SetFloat("_RimLargura", 0.25f);
            m.SetFloat("_BrilhoTamanho", 0f);
            m.SetFloat("_BrilhoForca", 0.6f);
            Opaco(m);
            var etq = AssetDatabase.LoadAssetAtPath<Texture2D>(R + "/Texturas/T_NI_Etiquetas.png");
            var meds = AssetDatabase.LoadAssetAtPath<Texture2D>(R + "/Texturas/T_NI_MEDS.png");
            void Tex(Texture2D t, float emis)
            {
                m.SetTexture("_BaseMap", t); m.SetColor("_BaseColor", Color.white); m.SetFloat("_RimForca", 0f);
                if (emis > 0) { m.SetColor("_EmissionColor", Hdr(Color.white, emis)); m.SetFloat("_EmissaoUsaTextura", 1f); }
            }
            switch (nome)
            {
                case "NI_Etiquetas": Tex(etq, 0.12f); break;
                case "NI_Legendas": Tex(etq, 0.6f); break;
                case "NI_Legendas_Acesas": Tex(etq, 2.2f); break;
                case "NI_Tela": Tex(meds, 1.3f); break;
                case "NI_Lente_Verde": case "NI_Lente_Ambar": case "NI_Lente_Vermelha": case "NI_Lente_Branca":
                    m.SetColor("_EmissionColor", Hdr(cor, 1.8f)); m.SetFloat("_RimForca", 0f); break;
                case "NI_Luz": m.SetColor("_EmissionColor", Hdr(cor, 2.6f)); m.SetFloat("_RimForca", 0f); break;
                case "NI_Metal": m.SetFloat("_BrilhoTamanho", 0.08f); m.SetFloat("_BrilhoForca", 0.8f); break;
                case "NI_Vidro":
                    m.SetColor("_BaseColor", new Color(cor.r, cor.g, cor.b, 0.14f));
                    m.SetFloat("_BrilhoTamanho", 0.08f); m.SetFloat("_BrilhoForca", 1.2f); m.SetFloat("_RimForca", 0.4f); Transparente(m); break;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        static void Opaco(Material m)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1); m.SetFloat("_Cull", (float)CullMode.Back);
            m.SetOverrideTag("RenderType", "Opaque"); m.renderQueue = (int)RenderQueue.Geometry;
        }

        static void Transparente(Material m)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
        }

        // ================================================================== 2. cena
        [Serializable] class Tipo { public string tipo; public float[] m; public int n; }
        [Serializable] class Parte { public string tipo; public float[] m; }
        [Serializable] class Inter { public string id, tipo, loc, painel, cor; public int pos; public float[] m; public Parte[] partes; }
        [Serializable] class Luz { public float[] p; public string cor; public float alcance, intens; }
        [Serializable] class Marca { public string nome; public float[] p; }
        [Serializable] class NomeMalha { public string tipo, nome; }
        [Serializable] class Dados { public Tipo[] tipos; public Inter[] interativos; public Luz[] luzes; public Marca[] marcas; public NomeMalha[] malhas; }

        static Matrix4x4 Mx(float[] a, int o = 0)
        {
            var m = new Matrix4x4();
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) m[i, j] = a[o + i * 4 + j];
            return m;
        }

        static void Por(Transform t, Matrix4x4 m)
        {
            t.localPosition = m.GetColumn(3);
            t.localRotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
        }

        static readonly Dictionary<string, string> CorBotao = new Dictionary<string, string>
            { { "verde", "#59FF86" }, { "ambar", "#FFC23A" }, { "verm", "#FF4A4A" }, { "branca", "#FFF4DE" }, { "apag", "#47E6FF" } };

        [MenuItem("AstroLab/Nave Interior/2. Montar cena (orbita)")]
        public static void MontarOrbita() => Debug.Log("[Nave] " + Montar("Orbita"));

        [MenuItem("AstroLab/Nave Interior/2. Montar cena (plataforma)")]
        public static void MontarPlataforma() => Debug.Log("[Nave] " + Montar("Plataforma"));

        public static string Montar(string fase)
        {
            var log = new List<string>();
            var dados = Newtonsoft.Json.JsonConvert.DeserializeObject<Dados>(File.ReadAllText(Json));
            var ativa = SceneManager.GetActiveScene();
            Directory.CreateDirectory(Path.GetDirectoryName(CenaPath));
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(cena);
            try
            {
                var raiz = new GameObject("NaveInterior").transform;
                var nave = new GameObject("Nave").transform; nave.SetParent(raiz, false);       // gira conforme a fase
                // ---------------------------------------------------------- interior estatico
                var inter = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FbxInt), cena);
                inter.name = "Interior";
                inter.transform.SetParent(nave, false);
                PrefabUtility.UnpackPrefabInstance(inter, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                var animados = new GameObject("Animados").transform; animados.SetParent(nave, false);
                foreach (var r in inter.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name.StartsWith("ANIM_")) { r.transform.SetParent(animados, true); continue; }
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                    if (r.name is "NI_Vidros" or "NI_Luzes" or "NI_Telas") r.shadowCastingMode = ShadowCastingMode.Off;
                }
                // ---------------------------------------------------------- kit
                var kitGo = AssetDatabase.LoadAssetAtPath<GameObject>(FbxKit);
                var porNome = dados.malhas.ToDictionary(x => x.tipo, x => x.nome);
                (Mesh, Material[]) Kit(string tipo)
                {
                    var t = kitGo.transform.Find(porNome[tipo]);
                    return (t.GetComponent<MeshFilter>().sharedMesh, t.GetComponent<MeshRenderer>().sharedMaterials);
                }
                Renderer Peca(string tipo, Transform pai, string nome)
                {
                    var (malha, mats) = Kit(tipo);
                    var o = new GameObject(nome, typeof(MeshFilter), typeof(MeshRenderer));
                    o.transform.SetParent(pai, false);
                    o.GetComponent<MeshFilter>().sharedMesh = malha;
                    var mr = o.GetComponent<MeshRenderer>();
                    mr.sharedMaterials = mats; mr.shadowCastingMode = ShadowCastingMode.Off;
                    return mr;
                }
                var kitRaiz = new GameObject("Kit").transform; kitRaiz.SetParent(nave, false);
                int n = 0;
                foreach (var tp in dados.tipos)
                {
                    var g = new GameObject("Kit_" + tp.tipo).transform; g.SetParent(kitRaiz, false);
                    for (int k = 0; k < tp.n; k++, n++)
                    {
                        var r = Peca(tp.tipo, g, tp.tipo);
                        Por(r.transform, Mx(tp.m, k * 16));
                        GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.OccludeeStatic);
                    }
                }
                log.Add($"kit: {n} instancias em {dados.tipos.Length} tipos");
                // ---------------------------------------------------------- controles interativos
                var intRaiz = new GameObject("Interativos").transform; intRaiz.SetParent(nave, false);
                foreach (var it in dados.interativos)
                {
                    var o = new GameObject("ANIM_" + it.id).transform; o.SetParent(intRaiz, false);
                    Por(o, Mx(it.m));
                    var rs = new List<Renderer>();
                    for (int k = 0; k < it.partes.Length; k++)
                    {
                        var r = Peca(it.partes[k].tipo, o, k == 0 ? "Base" : "Movel");
                        Por(r.transform, Mx(it.partes[k].m));
                        rs.Add(r);
                    }
                    var c = o.gameObject.AddComponent<ControleNave>();
                    c.id = it.id; c.chaveLoc = it.loc; c.movel = rs[1].transform;
                    if (it.tipo == "chave")
                    {
                        c.tipo = TipoControle.Chave;
                        // lampada de estado ao lado da chave (capa verde do kit, em escala reduzida)
                        var luz = Peca("capa_verde", o, "LuzEstado");
                        luz.transform.localPosition = new Vector3(-0.0165f, 0f, -0.012f);    // panel (u +0.0165, v +0.012)
                        luz.transform.localScale = Vector3.one * 0.45f;
                        c.luzEstado = luz; rs.Add(luz);
                        c.corEstado = Hx("#59FF86");
                    }
                    else
                    {
                        c.tipo = TipoControle.Botao;
                        c.luzEstado = rs[1];
                        c.corEstado = Hx(CorBotao.TryGetValue(it.cor ?? "apag", out var hc) ? hc : "#59FF86");
                    }
                    c.pecas = rs.ToArray();
                    var bc = o.gameObject.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0, 0.012f, 0); bc.size = new Vector3(0.034f, 0.03f, 0.034f);
                }
                log.Add($"interativos: {dados.interativos.Length}");
                // ---------------------------------------------------------- luzes
                var luzes = new GameObject("Luzes").transform; luzes.SetParent(nave, false);
                var painel = new List<Light>(); var cabine = new List<Light>();
                for (int i = 0; i < dados.luzes.Length; i++)
                {
                    var l = dados.luzes[i];
                    var o = new GameObject($"Luz_{i:00}", typeof(Light)); o.transform.SetParent(luzes, false);
                    o.transform.localPosition = new Vector3(l.p[0], l.p[1], l.p[2]);
                    var L = o.GetComponent<Light>(); L.type = LightType.Point; L.color = Hx(l.cor); L.range = l.alcance; L.intensity = l.intens * 1.6f;
                    L.shadows = LightShadows.None;
                    (i < 2 ? painel : cabine).Add(L);             // as 2 primeiras sao as de baixo do glareshield
                }
                // ---------------------------------------------------------- marcas
                var marcas = new GameObject("Marcas").transform; marcas.SetParent(nave, false);
                foreach (var mc in dados.marcas)
                {
                    var o = new GameObject(mc.nome).transform; o.SetParent(marcas, false);
                    o.localPosition = new Vector3(mc.p[0], mc.p[1], mc.p[2]);
                    // frente do orbitador = -X do Unity; na estacao de re o tripulante fica de pe olhando para tras
                    o.localRotation = Quaternion.LookRotation(mc.nome == "ESTACAO_RE" ? Vector3.right : Vector3.left, Vector3.up);
                }
                // ---------------------------------------------------------- ambiente
                var terra = Ambiente(raiz, log);
                // ---------------------------------------------------------- jogador: rig no olho do CDR, camera, maos, HUD
                var olho = marcas.Find("OLHO_CDR");
                var rig = new GameObject("Rig_CDR").transform;
                rig.SetParent(nave, false); rig.localPosition = olho.localPosition; rig.localRotation = olho.localRotation;
                var cam = new GameObject("Camera_Jogador", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                cam.transform.SetParent(rig, false);
                cam.nearClipPlane = 0.02f; cam.farClipPlane = 3000f; cam.fieldOfView = 75f; cam.clearFlags = CameraClearFlags.Skybox;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                cam.tag = "MainCamera";
                var ca = cam.gameObject.AddComponent<CameraAssento>();
                ca.postos = new[] { marcas.Find("OLHO_CDR"), marcas.Find("OLHO_PLT"), marcas.Find("ESTACAO_RE") };
                ca.chavesPosto = new[] { "nave.posto.cdr", "nave.posto.plt", "nave.posto.re" };
                ca.postos = ca.postos.Where(t => t).ToArray();
                var maosRaiz = new GameObject("Maos").transform; maosRaiz.SetParent(cam.transform, false);
                maosRaiz.localPosition = new Vector3(0f, -0.05f, -0.03f);           // maos mais baixas: nao cobrem os paineis de baixo
                var maos = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MaosPrefab), maosRaiz);
                var mm = maos ? maos.GetComponent<MovimentoMaos>() : null;
                if (mm) mm.jogador = rig;
                log.Add("maos: " + (maos ? maos.name : "NAO ACHADO"));
                var mira = cam.gameObject.AddComponent<MiraNave>();
                mira.alcance = 2.5f;
                mira.maoDireita = maos ? maos.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MaoDireita") : null;
                var (txtMira, txtCheck, txtLeg, imgMira) = Hud(raiz);
                mira.rotulo = txtMira; mira.mira = imgMira;
                // ---------------------------------------------------------- sistemas
                var sis = new GameObject("Sistemas").AddComponent<SistemasNave>();
                sis.transform.SetParent(raiz, false);
                sis.fase = fase == "Plataforma" ? FaseNave.Plataforma : FaseNave.Orbita;
                sis.raizNave = nave;
                sis.combinarEstaticos = new[] { inter, kitRaiz.gameObject };
                sis.telas = inter.transform.Find("NI_Telas")?.GetComponent<Renderer>();
                sis.luzesTeto = inter.transform.Find("NI_Luzes")?.GetComponent<Renderer>();
                sis.luzesPainel = painel.ToArray(); sis.luzesCabine = cabine.ToArray();
                sis.terra = terra; sis.txtChecklist = txtCheck; sis.txtLegenda = txtLeg;
                EditorSceneManager.SaveScene(cena, CenaPath);
                log.Add($"cena salva ({fase}): {CenaPath}");
            }
            finally
            {
                if (ativa.IsValid()) SceneManager.SetActiveScene(ativa);
                EditorSceneManager.CloseScene(cena, true);
            }
            return string.Join("\n", log);
        }

        static GameObject Ambiente(Transform raiz, List<string> log)
        {
            var ceu = AssetDatabase.LoadAssetAtPath<Material>("Assets/BaseLunar/Materiais/Ceu_Estrelas.mat");   // so referencia
            RenderSettings.skybox = ceu;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hx("#5C6A96");
            RenderSettings.ambientEquatorColor = Hx("#6E6A78");
            RenderSettings.ambientGroundColor = Hx("#3A3040");
            RenderSettings.fog = false;
            var sol = new GameObject("Sol", typeof(Light)).GetComponent<Light>();
            sol.transform.SetParent(raiz, false);
            sol.type = LightType.Directional; sol.color = Hx("#FFF2DC"); sol.intensity = 1.2f; sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(42f, -60f, 0f);
            // Terra pela janela (malhas e materiais do menu, so referencia): em orbita a baia de carga fica voltada para ela
            var lua = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MenuInicial/Modelos/MenuLua.fbx");
            var terra = new GameObject("Terra").transform; terra.SetParent(raiz, false);
            terra.position = new Vector3(-120f, 2300f, 300f);
            var ref0 = lua ? lua.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "Terra") : null;
            float esc = ref0 ? 2000f / ref0.sharedMesh.bounds.extents.x : 1f;
            foreach (var (nome, mat) in new[] { ("Terra", "M_Menu_Terra"), ("Terra_Nuvens", "M_Menu_Terra_Nuvens"), ("Terra_Atmosfera", "M_Menu_Terra_Atmosfera") })
            {
                var src = lua ? lua.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == nome) : null;
                if (src == null) { log.Add("Terra: sem malha " + nome); continue; }
                var o = new GameObject(nome, typeof(MeshFilter), typeof(MeshRenderer));
                o.transform.SetParent(terra, false);
                o.transform.localScale = Vector3.one * esc;
                o.GetComponent<MeshFilter>().sharedMesh = src.sharedMesh;
                var mr = o.GetComponent<MeshRenderer>();
                mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"Assets/MenuInicial/Materiais/{mat}.mat");
                mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            }
            var estilo = new GameObject("IluminacaoCartoon").AddComponent<IluminacaoCartoon>();
            estilo.transform.SetParent(raiz, false);
            estilo.sombra = Hx("#4A4F7C"); estilo.ambienteCeu = Hx("#5C6A96"); estilo.ambienteChao = Hx("#3A3040");
            var vol = new GameObject("Pos", typeof(Volume)).GetComponent<Volume>();
            vol.transform.SetParent(raiz, false);
            vol.isGlobal = true;
            vol.sharedProfile = Pos();
            return terra.gameObject;
        }

        static VolumeProfile Pos()
        {
            string path = R + "/PosProcessamento_Nave.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
            T Get<T>() where T : VolumeComponent { if (!p.TryGet<T>(out var c)) { c = p.Add<T>(true); AssetDatabase.AddObjectToAsset(c, p); } c.active = true; return c; }
            Get<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var b = Get<Bloom>(); b.threshold.Override(1.0f); b.intensity.Override(0.5f); b.scatter.Override(0.55f);
            var ca = Get<ColorAdjustments>(); ca.contrast.Override(10f); ca.saturation.Override(8f); ca.postExposure.Override(0.1f);
            var v = Get<Vignette>(); v.intensity.Override(0.2f); v.color.Override(Hx("#1C1A3D")); v.smoothness.Override(0.45f);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssetIfDirty(p);
            return p;
        }

        static (TMP_Text mira, TMP_Text check, TMP_Text legenda, Image ponto) Hud(Transform raiz)
        {
            var fonte = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonte);
            var cv = new GameObject("HUD_Nave", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            cv.transform.SetParent(raiz, false);
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = cv.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            RectTransform Ret(string nome, Vector2 anc, Vector2 piv, Vector2 pos, Vector2 tam)
            {
                var rt = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>();
                rt.SetParent(cv.transform, false);
                rt.anchorMin = rt.anchorMax = anc; rt.pivot = piv; rt.anchoredPosition = pos; rt.sizeDelta = tam;
                return rt;
            }
            TMP_Text Txt(RectTransform rt, float tam, TextAlignmentOptions al)
            {
                var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
                if (fonte) t.font = fonte;
                t.fontSize = tam; t.alignment = al; t.color = Color.white; t.raycastTarget = false;
                t.outlineWidth = 0.18f; t.outlineColor = new Color32(28, 26, 61, 255);
                return t;
            }
            var ponto = Ret("Mira", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7)).gameObject.AddComponent<Image>();
            ponto.raycastTarget = false;
            var mira = Txt(Ret("Rotulo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(900, 120)), 30, TextAlignmentOptions.Top);
            var check = Txt(Ret("Checklist", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40, -40), new Vector2(620, 520)), 26, TextAlignmentOptions.TopLeft);
            var leg = Txt(Ret("Legenda", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(1400, 140)), 30, TextAlignmentOptions.Bottom);
            mira.text = check.text = leg.text = "";
            return (mira, check, leg, ponto);
        }

        // ================================================================== 3. captura isolada (preview scene: nao mexe na cena aberta)
        public static string Captura(string arq, float inclina, float giro, float fov, int w, int h, string mirarId = null, string posto = null)
        {
            var cena = EditorSceneManager.OpenPreviewScene(CenaPath);
            try
            {
                var raiz = cena.GetRootGameObjects().First(g => g.name == "NaveInterior").transform;
                var cam = raiz.GetComponentInChildren<Camera>(true);
                if (!string.IsNullOrEmpty(posto))
                {
                    var mk = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == posto);
                    if (mk) cam.transform.parent.SetPositionAndRotation(mk.position, mk.rotation);
                }
                cam.transform.localRotation = Quaternion.Euler(-inclina, giro, 0f);
                if (!string.IsNullOrEmpty(mirarId))
                {
                    var alvo = raiz.GetComponentsInChildren<ControleNave>(true).FirstOrDefault(c => c.id == mirarId);
                    if (alvo)
                    {
                        cam.transform.rotation = Quaternion.LookRotation(alvo.transform.position - cam.transform.position, cam.transform.parent.up);
                        alvo.Realcar(true);
                    }
                }
                cam.fieldOfView = fov;
                cam.scene = cena;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                Directory.CreateDirectory(Path.GetDirectoryName(arq));
                File.WriteAllBytes(arq, tex.EncodeToPNG());
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
                return "ok " + arq;
            }
            finally { EditorSceneManager.ClosePreviewScene(cena); }
        }
    }
}
