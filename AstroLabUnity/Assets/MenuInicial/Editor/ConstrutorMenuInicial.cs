using System;
using System.IO;
using System.Linq;
using AstroLab.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AstroLab.Menu.EditorTools
{
    /// <summary>
    /// Monta a cena do menu inicial do zero: Assets/MenuInicial/Cenas/MenuInicial.unity.
    /// Menu: AstroLab > Menu Inicial > Montar cena. Não mexe em nada fora de Assets/MenuInicial
    /// (só LÊ shaders/texturas/céu da BaseLunar). Nunca chama AssetDatabase.SaveAssets().
    /// </summary>
    public static class ConstrutorMenuInicial
    {
        const string Raiz = "Assets/MenuInicial";
        const string CenaPath = Raiz + "/Cenas/MenuInicial.unity";
        const string FbxPath = Raiz + "/Modelos/MenuLua.fbx";
        const string FogPath = Raiz + "/Modelos/Foguete.fbx";
        const string MatDir = Raiz + "/Materiais";
        const string UiDir = Raiz + "/UI";
        const string TexDir = Raiz + "/Texturas";
        const string FontDir = Raiz + "/Fontes";
        const string PrefabDir = Raiz + "/Prefabs";
        const string BaseLunarTex = "Assets/BaseLunar/Texturas/Cartoon";
        const string CeuPath = "Assets/BaseLunar/Materiais/Ceu_Estrelas.mat";

        // ---------------------------------------------------------------- composição (ajuste fino aqui)
        static readonly Vector2 TerraViewport = new Vector2(0.82f, 0.62f);    // centro da Terra na tela: terço superior direito
        const float TerraDistancia = 650f;
        const float TerraRaio = 27f;
        const float CameraFov = 42f;
        const float InclinacaoMundo = 40f;  // gira o cenário inteiro para a câmera olhar para a parte estrelada do céu
        const float SolElevacao = 15f;      // graus acima do horizonte: sombras longas nas crateras
        const float SolAzimute = -78f;      // em relação à frente da câmera (negativo = vem da esquerda)

        static Color Hx(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
        static Color Hdr(Color srgb, float i) => (srgb.linear * i).gamma;

        [MenuItem("AstroLab/Menu Inicial/Montar cena")]
        public static void MontarMenu() => Montar();

        public static string Montar()
        {
            var ativa = SceneManager.GetActiveScene();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return $"ABORTADO: a cena aberta '{SceneManager.GetSceneAt(i).name}' tem alterações não salvas.";

            foreach (var d in new[] { "Cenas", "Materiais", "UI", "Texturas", "Fontes", "Prefabs" })
                if (!AssetDatabase.IsValidFolder($"{Raiz}/{d}")) AssetDatabase.CreateFolder(Raiz, d);

            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            GerarSprites();
            var tema = CriarTema();
            var mats = CriarMateriais();
            var perfil = CriarPosProcessamento();

            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------------------------------------------------------- cenário 3D
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var lua = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            PrefabUtility.UnpackPrefabInstance(lua, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            lua.name = "Cenario_Lua";
            foreach (var r in lua.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = r.name switch
                {
                    "Lua_Superficie" => mats.regolito,
                    "Lua_Rochas" => mats.rocha,
                    "Bandeira_Mastro" => mats.mastro,
                    "Bandeira_Tecido" => mats.bandeira,
                    "Terra" => mats.terra,
                    "Terra_Nuvens" => mats.nuvens,
                    "Terra_Atmosfera" => mats.atmosfera,
                    _ => mats.regolito,
                };
                bool ceu = r.name.StartsWith("Terra");
                r.shadowCastingMode = ceu ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = !ceu;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            Transform Achar(string n) => lua.GetComponentsInChildren<Transform>(true).First(t => t.name == n);

            // câmera
            var camGo = new GameObject("Camera_Menu", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            camGo.transform.position = Achar("CAM_Menu").position;
            camGo.transform.rotation = Quaternion.LookRotation(Achar("CAM_Alvo").position - camGo.transform.position, Vector3.up);
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 2500f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var dadosCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            dadosCam.renderPostProcessing = true;
            dadosCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            dadosCam.renderShadows = true;
            var rig = camGo.AddComponent<CameraMenu>();

            // Terra: tira as esferas do modelo e põe num pivô próprio, à direita, acima do horizonte
            var terraRoot = new GameObject("Terra").transform;
            var ancora = terraRoot.gameObject.AddComponent<AncoraViewport>();
            ancora.cameraMenu = cam; ancora.viewport = TerraViewport; ancora.distancia = TerraDistancia;
            ancora.Posicionar();
            // orientação: Brasil de frente para a câmera, um pouco acima e à esquerda do centro do disco; eixo
            // inclinado de forma natural. A Terra fica (quase) parada e só as nuvens giram.
            var malhaTerra = Achar("Terra").GetComponent<MeshFilter>().sharedMesh;
            Vector3 localBrasil = DirecaoDaUV(malhaTerra, new Vector2((-50f + 180f) / 360f, (-11f + 90f) / 180f));
            Vector3 localNorte = DirecaoDaUV(malhaTerra, new Vector2(0.5f, 1f));
            Vector3 paraCam = (camGo.transform.position - terraRoot.position).normalized;
            Vector3 camUp = camGo.transform.up, camRight = camGo.transform.right;
            Vector3 alvoBrasil = (paraCam + camUp * 0.30f - camRight * 0.26f).normalized;
            Vector3 alvoNorte = Quaternion.AngleAxis(16f, paraCam) * camUp;
            var rotTerra = Quaternion.LookRotation(alvoBrasil, alvoNorte) * Quaternion.Inverse(Quaternion.LookRotation(localBrasil, localNorte));
            var eixo = new GameObject("Eixo_Terra").transform;
            eixo.SetParent(terraRoot, false);
            var osc = eixo.gameObject.AddComponent<OscilacaoLenta>();
            osc.eixoLocal = Vector3.up; osc.amplitudeGraus = 6f; osc.periodo = 48f;
            foreach (var n in new[] { "Terra", "Terra_Nuvens", "Terra_Atmosfera" })
            {
                var t = Achar(n);
                t.SetParent(eixo, false);
                t.localPosition = Vector3.zero;
                t.localScale = Vector3.one * TerraRaio * 100f;            // FBX do Blender: malha em cm, escala 100
                t.rotation = rotTerra;
                if (n == "Terra_Nuvens")
                {
                    var rl = t.gameObject.AddComponent<RotacaoLenta>();
                    rl.eixoLocal = localNorte;
                    rl.grausPorSegundo = 0.9f;
                }
            }
            eixo.localRotation = Quaternion.identity;
            // OscilacaoLenta gira em volta do eixo "up" do pivô: alinha esse eixo com o norte da Terra
            var norteMundo = rotTerra * localNorte;
            eixo.rotation = Quaternion.FromToRotation(Vector3.up, norteMundo);
            foreach (Transform t in eixo) t.rotation = rotTerra;

            // bandeira (modelada no Blender: mastro + haste Apollo + tecido/emblema); o Unity só anima o tecido
            var band = Achar("Bandeira_Tecido").gameObject;
            var br = band.GetComponent<MeshRenderer>();
            br.sharedMaterials = new[] { mats.bandeira, mats.emblema };
            br.shadowCastingMode = ShadowCastingMode.Off;                                    // a sombra do tecido virava uma mancha no chão
            br.lightProbeUsage = LightProbeUsage.Off;
            var bo = band.AddComponent<BandeiraOndulante>();

            // luzes (mesma linguagem da Lua.unity: sol quente e baixo + preenchimento frio da Terra)
            var fwd = camGo.transform.forward; fwd.y = 0f; fwd.Normalize();
            var dirSol = Quaternion.AngleAxis(SolAzimute, Vector3.up) * fwd;                 // de onde o sol vem (horizontal)
            var sol = new GameObject("Sol", typeof(Light)).GetComponent<Light>();
            sol.type = LightType.Directional;
            sol.color = Hx("#FFF1D6");
            sol.intensity = 1.25f;
            sol.shadows = LightShadows.Soft;
            sol.shadowStrength = 1f;
            sol.shadowBias = 0.03f; sol.shadowNormalBias = 0.3f;
            var paraOnde = -dirSol;
            float el = SolElevacao * Mathf.Deg2Rad;
            sol.transform.rotation = Quaternion.LookRotation((paraOnde * Mathf.Cos(el) + Vector3.down * Mathf.Sin(el)).normalized);
            RenderSettings.sun = sol;

            var luzTerra = new GameObject("Luz_da_Terra", typeof(Light)).GetComponent<Light>();
            luzTerra.type = LightType.Directional;
            luzTerra.color = Hx("#9DB8FF");
            luzTerra.intensity = 0.3f;
            luzTerra.shadows = LightShadows.None;
            luzTerra.transform.rotation = Quaternion.LookRotation(camGo.transform.position - terraRoot.position);

            // estilo cartoon da BaseLunar (componente IluminacaoCartoon, no Assembly-CSharp → por reflexão)
            var tipoIlum = Type.GetType("IluminacaoCartoon, Assembly-CSharp");
            var ilum = new GameObject("Iluminacao_Cartoon");
            if (tipoIlum != null)
            {
                // sombras mais escuras e frias só nesta cena (o componente é da cena do menu; a BaseLunar não muda)
                var comp = ilum.AddComponent(tipoIlum);
                void Campo(string n, object v) { var f = tipoIlum.GetField(n); if (f != null) f.SetValue(comp, v); }
                Campo("sombra", Hx("#3B3672"));
                Campo("meioTom", Hx("#A7A3D4"));
                Campo("limiteSombra", 0.07f);
                Campo("limiteMeioTom", 0.42f);
            }
            else Debug.LogWarning("[Menu] IluminacaoCartoon não encontrado: o Toon vai usar as cores padrão e sem contorno.");

            RenderSettings.skybox = CeuMenu();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hx("#4A4F8C");
            RenderSettings.ambientEquatorColor = Color.Lerp(Hx("#4A4F8C"), Hx("#3A3040"), 0.5f);
            RenderSettings.ambientGroundColor = Hx("#3A3040");
            RenderSettings.fog = false;

            var volGo = new GameObject("PosProcessamento_Menu", typeof(Volume));
            var vol = volGo.GetComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = perfil;

            // cometa (traço de luz do botão Iniciar)
            var cometa = new GameObject("Cometa");
            var rastro = cometa.AddComponent<TrailRenderer>();
            rastro.sharedMaterial = mats.rastro;
            rastro.time = 0.8f;
            rastro.minVertexDistance = 0.4f;
            rastro.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.25f, 0.55f), new Keyframe(1f, 0f));
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Hx("#FFD08A"), 0.2f), new GradientColorKey(Hx("#9FB4FF"), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
            rastro.colorGradient = grad;
            rastro.alignment = LineAlignment.View;
            rastro.shadowCastingMode = ShadowCastingMode.Off;
            rastro.receiveShadows = false;
            rastro.emitting = false;
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var cabeca = Billboard("Cabeca", cometa.transform, quad, mats.cometa);
            var luzCometa = new GameObject("Luz", typeof(Light)).GetComponent<Light>();
            luzCometa.transform.SetParent(cometa.transform, false);
            luzCometa.type = LightType.Point; luzCometa.color = Hx("#FFC27A"); luzCometa.intensity = 0f; luzCometa.range = 20f;
            luzCometa.shadows = LightShadows.None;
            var clarao = Billboard("Clarao_Terra", null, quad, mats.cometa);

            // foguete (Blender: foguete_menu.py). Pivô no pé dos motores, nariz no +Z do pivô, comprimento 1.
            var fogPivo = new GameObject("Foguete").transform;
            var fogModelo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FogPath));
            PrefabUtility.UnpackPrefabInstance(fogModelo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            fogModelo.transform.SetParent(fogPivo, false);
            Transform AcharF(string n) => fogModelo.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
            Vector3 fBase = AcharF("FOGUETE_Base").position, fPonta = AcharF("FOGUETE_Ponta").position;
            var eixoF = fPonta - fBase; float compF = eixoF.magnitude;
            var qF = Quaternion.FromToRotation(eixoF.normalized, Vector3.forward);
            fogModelo.transform.rotation = qF * fogModelo.transform.rotation;
            fogModelo.transform.position = qF * (fogModelo.transform.position - fBase);
            fogModelo.transform.localScale /= compF;
            fogModelo.transform.localPosition /= compF;
            foreach (var r in fogModelo.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterials = new[] { mats.fogLaranja, mats.fogBranco, mats.fogEscuro };
                r.shadowCastingMode = ShadowCastingMode.Off; r.lightProbeUsage = LightProbeUsage.Off;
            }
            var saida = new GameObject("Saida_Foguete").transform;
            var terraT = terraRoot.GetComponentsInChildren<Transform>(true).First(t => t.name == "Terra");
            saida.SetParent(terraT, false);
            saida.localPosition = localBrasil.normalized * malhaTerra.bounds.extents.x * 1.01f;

            var lanc = new GameObject("Lancamento").AddComponent<LancamentoMenu>();
            lanc.modo = ModoLancamento.Foguete;
            lanc.foguete = fogPivo;
            lanc.pontoDeSaida = saida;
            lanc.terra = terraRoot; lanc.raioTerra = TerraRaio; lanc.cameraMenu = cam; lanc.cameraRig = rig;
            lanc.bandeira = bo;
            lanc.cometa = cometa.transform; lanc.rastro = rastro; lanc.cabeca = cabeca; lanc.luz = luzCometa; lanc.claraoTerra = clarao;

            new GameObject("Som_Menu").AddComponent<SomMenu>();

            // céu: rotação lenta + meteoro ocasional
            var meteoroGo = new GameObject("Meteoro");
            var met = meteoroGo.AddComponent<TrailRenderer>();
            met.sharedMaterial = mats.rastro;
            met.time = 0.4f; met.minVertexDistance = 2f; met.emitting = false;
            met.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var gm = new Gradient();
            gm.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Hx("#9FD8FF"), 1f) },
                       new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            met.colorGradient = gm;
            met.shadowCastingMode = ShadowCastingMode.Off; met.receiveShadows = false;
            var ceuMenu = new GameObject("Ceu_Menu").AddComponent<CeuMenu>();
            ceuMenu.cameraMenu = cam; ceuMenu.meteoro = met;

            // inclina o mundo todo em volta da câmera: ela passa a olhar para cima, para a parte estrelada do céu
            // (o skybox não gira junto). A composição na tela continua igual.
            var mundo = new GameObject("Mundo").transform;
            mundo.position = camGo.transform.position;
            foreach (var t in new Transform[] { lua.transform, terraRoot, sol.transform, luzTerra.transform, camGo.transform, cometa.transform, clarao })
                t.SetParent(mundo, true);
            mundo.rotation = Quaternion.AngleAxis(-InclinacaoMundo, camGo.transform.right) * mundo.rotation;
            ancora.Posicionar();

            // ---------------------------------------------------------------- interface
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var canvasGo = new GameObject("Canvas_Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var raiz = (RectTransform)canvasGo.transform;

            var principal = Grupo("Principal", raiz);
            MontarPrincipal(principal, tema, out var bIniciar, out var bOpcoes, out var bCreditos, out var bSair);

            var opcoesGrupo = Grupo("Opcoes", raiz);
            var painelOpcoes = MontarOpcoes(opcoesGrupo, tema);

            var creditosGrupo = Grupo("Creditos", raiz);
            var painelCreditos = MontarCreditos(creditosGrupo, tema);

            var aviso = FabricaUI.Texto("Aviso", raiz, tema, null, 26f);
            aviso.rectTransform.anchorMin = aviso.rectTransform.anchorMax = new Vector2(0.5f, 0.12f);
            aviso.rectTransform.sizeDelta = new Vector2(1200, 50);
            aviso.color = Hx("#FFC23A");
            aviso.alpha = 0f;

            var flash = FabricaUI.Imagem("Flash", raiz, null, new Color(1, 1, 1, 0));
            FabricaUI.Esticar(flash.rectTransform);
            lanc.flash = flash;
            var fade = FabricaUI.Imagem("Fade", raiz, null, Hx("#07061A"));
            FabricaUI.Esticar(fade.rectTransform);

            var menu = canvasGo.AddComponent<MenuInicial>();
            var so = new SerializedObject(menu);
            void Ref(string campo, Object valor) => so.FindProperty(campo).objectReferenceValue = valor;
            Ref("grupoPrincipal", principal.GetComponent<CanvasGroup>());
            Ref("grupoOpcoes", opcoesGrupo.GetComponent<CanvasGroup>());
            Ref("grupoCreditos", creditosGrupo.GetComponent<CanvasGroup>());
            Ref("fadePreto", fade);
            Ref("aviso", aviso);
            Ref("botaoIniciar", bIniciar); Ref("botaoOpcoes", bOpcoes); Ref("botaoCreditos", bCreditos); Ref("botaoSair", bSair);
            Ref("painelOpcoes", painelOpcoes); Ref("painelCreditos", painelCreditos); Ref("lancamento", lanc);
            so.ApplyModifiedPropertiesWithoutUndo();

            // painel de opções também como prefab (para o menu de pausa do jogo)
            var copia = Object.Instantiate(painelOpcoes.gameObject);
            copia.name = "PainelOpcoes";
            copia.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(copia, $"{PrefabDir}/PainelOpcoes.prefab");
            Object.DestroyImmediate(copia);

            opcoesGrupo.gameObject.SetActive(false);
            creditosGrupo.gameObject.SetActive(false);
            fade.color = new Color(fade.color.r, fade.color.g, fade.color.b, 0f);   // no Editor a cena aparece; o fade inicial é feito em runtime

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena, CenaPath);
            string msg = $"[Menu] Cena montada: {CenaPath} (Terra em {terraRoot.position}, câmera em {camGo.transform.position})";
            Debug.Log(msg);
            return msg;
        }


        // ---------------------------------------------------------------- céu: o da BaseLunar, mais escuro + estrelas piscando
        static Material CeuMenu()
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>(CeuPath);
            if (original == null) return null;
            string path = $"{MatDir}/M_Menu_Ceu.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(original); AssetDatabase.CreateAsset(m, path); }
            m.shader = original.shader;
            m.CopyPropertiesFromMaterial(original);
            if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", original.GetFloat("_Exposure") * 0.5f);
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", new Color(0.36f, 0.36f, 0.48f, 0.5f));
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        /// <summary>Direção (no espaço local da malha) do vértice cuja UV é a mais próxima da pedida.</summary>
        static Vector3 DirecaoDaUV(Mesh m, Vector2 uv)
        {
            var v = m.vertices; var u = m.uv;
            int melhor = 0; float dMin = float.MaxValue;
            for (int i = 0; i < u.Length; i++)
            {
                float du = Mathf.Abs(u[i].x - uv.x); du = Mathf.Min(du, 1f - du);
                float d = du * du + (u[i].y - uv.y) * (u[i].y - uv.y);
                if (d < dMin) { dMin = d; melhor = i; }
            }
            return v[melhor].normalized;
        }

        static Transform Billboard(string nome, Transform pai, Mesh quad, Material mat)
        {
            var go = new GameObject(nome, typeof(MeshFilter), typeof(MeshRenderer));
            if (pai) go.transform.SetParent(pai, false);
            go.GetComponent<MeshFilter>().sharedMesh = quad;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            return go.transform;
        }

        static RectTransform Grupo(string nome, RectTransform pai)
        {
            var rt = FabricaUI.Rect(nome, pai);
            FabricaUI.Esticar(rt);
            rt.gameObject.AddComponent<CanvasGroup>();
            return rt;
        }

        // ---------------------------------------------------------------- tela principal
        static void MontarPrincipal(RectTransform p, TemaMenu tema, out BotaoMenu iniciar, out BotaoMenu opcoes, out BotaoMenu creditos, out BotaoMenu sair)
        {
            // título
            var titulo = FabricaUI.Texto("Titulo", p, tema, null, 132f, TextAlignmentOptions.Center, false, true);
            titulo.text = "ASTROLAB";
            titulo.characterSpacing = 14f;
            titulo.fontStyle = FontStyles.Bold;
            titulo.color = Color.white;
            titulo.fontSharedMaterial = MaterialTitulo(tema.fonteTitulo);
            titulo.rectTransform.anchorMin = titulo.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            titulo.rectTransform.sizeDelta = new Vector2(1100, 170);
            titulo.rectTransform.anchoredPosition = new Vector2(0, -190);

            var linha = FabricaUI.Imagem("Linha", p, tema.faixaBrilho, tema.destaque, false);
            linha.rectTransform.anchorMin = linha.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            linha.rectTransform.sizeDelta = new Vector2(520, 3);
            linha.rectTransform.anchoredPosition = new Vector2(0, -282);

            var sub = FabricaUI.Texto("Subtitulo", p, tema, "menu.subtitulo", 26f, TextAlignmentOptions.Center, true);
            sub.characterSpacing = 22f;
            sub.color = tema.textoSecundario;
            sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            sub.rectTransform.sizeDelta = new Vector2(900, 40);
            sub.rectTransform.anchoredPosition = new Vector2(0, -312);

            // botões (Créditos por último)
            var col = FabricaUI.Rect("Botoes", p);
            col.anchorMin = col.anchorMax = new Vector2(0.5f, 0.5f);
            col.pivot = new Vector2(0.5f, 1f);
            col.sizeDelta = new Vector2(420, 260);
            col.anchoredPosition = new Vector2(0, 40);
            FabricaUI.ColunaVertical(col, 22f);
            var tam = new Vector2(420, 66);
            iniciar = FabricaUI.Botao("Botao_Iniciar", col, tema, "menu.iniciar", tam, 32f);
            opcoes = FabricaUI.Botao("Botao_Opcoes", col, tema, "menu.opcoes", tam, 30f);
            creditos = FabricaUI.Botao("Botao_Creditos", col, tema, "menu.creditos", tam, 30f);
            NavVertical(iniciar.Botao, opcoes.Botao, creditos.Botao);

            // sair: discreto no canto inferior direito
            sair = FabricaUI.Botao("Botao_Sair", p, tema, "menu.sair", new Vector2(170, 48), 20f);
            var rs = (RectTransform)sair.transform;
            rs.anchorMin = rs.anchorMax = rs.pivot = new Vector2(1f, 0f);
            rs.anchoredPosition = new Vector2(-44, 40);
            var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = creditos.Botao, selectOnLeft = creditos.Botao };
            sair.Botao.navigation = nav;
            var nc = creditos.Botao.navigation; nc.selectOnDown = sair.Botao; creditos.Botao.navigation = nc;

            var versao = FabricaUI.Texto("Versao", p, tema, null, 18f, TextAlignmentOptions.BottomLeft);
            versao.text = $"v{Application.version}  ·  Uniube";
            versao.color = new Color(1f, 1f, 1f, 0.8f);
            versao.fontStyle = FontStyles.Bold;
            versao.rectTransform.anchorMin = versao.rectTransform.anchorMax = versao.rectTransform.pivot = new Vector2(0f, 0f);
            versao.rectTransform.sizeDelta = new Vector2(500, 30);
            versao.rectTransform.anchoredPosition = new Vector2(40, 36);
        }

        static void NavVertical(params Selectable[] s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                var n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnUp = s[(i + s.Length - 1) % s.Length];
                n.selectOnDown = s[(i + 1) % s.Length];
                s[i].navigation = n;
            }
        }

        // ---------------------------------------------------------------- opções
        static PainelOpcoes MontarOpcoes(RectTransform grupo, TemaMenu tema)
        {
            var painel = FabricaUI.Painel("Painel", grupo, tema, new Vector2(920, 800));
            painel.anchorMin = painel.anchorMax = new Vector2(0.5f, 0.5f);
            painel.anchoredPosition = new Vector2(-90, -10);

            var titulo = FabricaUI.Texto("Titulo", painel, tema, "opcoes.titulo", 40f, TextAlignmentOptions.Center, true);
            titulo.characterSpacing = 16f; titulo.fontStyle = FontStyles.Bold; titulo.color = Color.white;
            titulo.rectTransform.anchorMin = new Vector2(0, 1); titulo.rectTransform.anchorMax = new Vector2(1, 1);
            titulo.rectTransform.sizeDelta = new Vector2(0, 60); titulo.rectTransform.anchoredPosition = new Vector2(0, -50);

            var abasRt = FabricaUI.Rect("Abas", painel);
            abasRt.anchorMin = new Vector2(0, 1); abasRt.anchorMax = new Vector2(1, 1);
            abasRt.sizeDelta = new Vector2(-60, 54); abasRt.anchoredPosition = new Vector2(0, -122);
            var h = abasRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            string[] chaves = { "opcoes.aba.graficos", "opcoes.aba.audio", "opcoes.aba.controles", "opcoes.aba.jogo" };
            var abas = chaves.Select(c => FabricaUI.Botao("Aba_" + c.Split('.').Last(), abasRt, tema, c, new Vector2(200, 54), 21f)).ToArray();

            var conteudo = FabricaUI.Rect("Conteudo", painel);
            conteudo.anchorMin = new Vector2(0, 0); conteudo.anchorMax = new Vector2(1, 1);
            conteudo.offsetMin = new Vector2(30, 112); conteudo.offsetMax = new Vector2(-30, -172);
            var paginas = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var pg = FabricaUI.Rect("Pagina_" + chaves[i].Split('.').Last(), conteudo);
                FabricaUI.Esticar(pg);
                FabricaUI.ColunaVertical(pg, 10f);
                paginas[i] = pg;
            }

            var restaurar = FabricaUI.Botao("Botao_Restaurar", painel, tema, "opcoes.restaurar", new Vector2(300, 56), 20f);
            var rr = (RectTransform)restaurar.transform;
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0, 0); rr.anchoredPosition = new Vector2(30, 32);
            var voltar = FabricaUI.Botao("Botao_Voltar", painel, tema, "menu.voltar", new Vector2(240, 56), 22f);
            var rv = (RectTransform)voltar.transform;
            rv.anchorMin = rv.anchorMax = rv.pivot = new Vector2(1, 0); rv.anchoredPosition = new Vector2(-30, 32);
            var dica = FabricaUI.Texto("Dica", painel, tema, "menu.dica_voltar", 18f);
            dica.color = new Color(tema.textoSecundario.r, tema.textoSecundario.g, tema.textoSecundario.b, 0.6f);
            dica.rectTransform.anchorMin = dica.rectTransform.anchorMax = new Vector2(0.5f, 0); dica.rectTransform.anchoredPosition = new Vector2(30, 60);
            dica.rectTransform.sizeDelta = new Vector2(240, 30);

            var po = grupo.gameObject.AddComponent<PainelOpcoes>();
            po.Configurar(tema, abas, paginas, restaurar, voltar);
            return po;
        }

        // ---------------------------------------------------------------- créditos
        static PainelCreditos MontarCreditos(RectTransform grupo, TemaMenu tema)
        {
            var painel = FabricaUI.Painel("Painel", grupo, tema, new Vector2(800, 850));
            painel.anchorMin = painel.anchorMax = new Vector2(0.5f, 0.5f);
            painel.anchoredPosition = new Vector2(-90, -10);

            var titulo = FabricaUI.Texto("Titulo", painel, tema, "creditos.titulo", 40f, TextAlignmentOptions.Center, true);
            titulo.characterSpacing = 16f; titulo.fontStyle = FontStyles.Bold; titulo.color = Color.white;
            titulo.rectTransform.anchorMin = new Vector2(0, 1); titulo.rectTransform.anchorMax = new Vector2(1, 1);
            titulo.rectTransform.sizeDelta = new Vector2(0, 60); titulo.rectTransform.anchoredPosition = new Vector2(0, -50);

            var lista = FabricaUI.Rect("Lista", painel);
            lista.anchorMin = new Vector2(0, 0); lista.anchorMax = new Vector2(1, 1);
            lista.offsetMin = new Vector2(60, 110); lista.offsetMax = new Vector2(-60, -100);
            var v = FabricaUI.ColunaVertical(lista, 0f);
            v.childAlignment = TextAnchor.MiddleCenter;

            var voltar = FabricaUI.Botao("Botao_Voltar", painel, tema, "menu.voltar", new Vector2(240, 56), 22f);
            var rv = (RectTransform)voltar.transform;
            rv.anchorMin = rv.anchorMax = rv.pivot = new Vector2(0.5f, 0); rv.anchoredPosition = new Vector2(0, 32);
            voltar.Botao.navigation = new Navigation { mode = Navigation.Mode.None };

            var pc = grupo.gameObject.AddComponent<PainelCreditos>();
            pc.Configurar(tema, lista, voltar);
            return pc;
        }

        // ---------------------------------------------------------------- assets
        struct Mats { public Material regolito, rocha, mastro, bandeira, emblema, terra, nuvens, atmosfera, cometa, rastro, fogLaranja, fogBranco, fogEscuro; }

        static Mats CriarMateriais()
        {
            Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{BaseLunarTex}/{n}.png");
            var m = new Mats();
            m.regolito = Toon("M_Menu_Regolito", T("T_Regolito_Toon"), Hx("#A9A5C0"), x =>
            {
                x.SetTextureScale("_BaseMap", new Vector2(0.8f, 0.8f));
                x.SetFloat("_FaixaDesloc", 0.04f); x.SetFloat("_RimForca", 0.12f);
            });
            m.rocha = Toon("M_Menu_Rocha", T("T_Rocha_Toon"), Hx("#7F7A9E"), x => x.SetFloat("_RimForca", 0.2f));
            m.mastro = Toon("M_Menu_Mastro", T("T_Metal_Toon"), Hx("#B4C2D9"), x => { x.SetFloat("_BrilhoTamanho", 0.05f); x.SetFloat("_BrilhoForca", 0.5f); });
            m.bandeira = Toon("M_Menu_Bandeira_Tecido", null, Hx("#2152C4"), x => { x.SetFloat("_Cull", (float)CullMode.Off); x.SetFloat("_RimForca", 0.25f); });
            m.emblema = Toon("M_Menu_Bandeira_Emblema", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_Bandeira_Emblema.png"), Color.white, x =>
            {
                Transparente(x); x.SetFloat("_Cull", (float)CullMode.Back); x.SetFloat("_RimForca", 0.15f);
            });
            m.terra = Toon("M_Menu_Terra", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_Menu_Terra_Toon.png"), Color.white, x =>
            {
                x.SetColor("_EmissionColor", Hdr(Color.white, 0.35f)); x.SetFloat("_EmissaoUsaTextura", 1f);
                x.SetFloat("_RimForca", 0.6f); x.SetFloat("_RimLargura", 0.3f); x.SetFloat("_SemNevoa", 1f);
            });
            m.nuvens = Toon("M_Menu_Terra_Nuvens", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_Menu_TerraNuvens.png"), Color.white, x =>
            {
                x.SetColor("_EmissionColor", Hdr(Color.white, 0.25f)); x.SetFloat("_RimForca", 0f); x.SetFloat("_SemNevoa", 1f);
                Transparente(x); x.SetFloat("_Cull", (float)CullMode.Back);
            });
            m.atmosfera = Mat("M_Menu_Terra_Atmosfera", "AstroLab/Atmosfera", x =>
            {
                x.SetColor("_Cor", Hdr(Hx("#7FD4FF"), 1.6f)); x.SetFloat("_Potencia", 2.5f); x.SetFloat("_Suavidade", 0.3f); x.SetFloat("_Forca", 1.2f);
            });
            m.cometa = Mat("M_Menu_Cometa", "AstroLab/Menu/Aditivo", x =>
            {
                x.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/FX_Brilho.png"));
                x.SetColor("_Cor", Hdr(Hx("#FFE2B0"), 6f));
            });
            m.rastro = Mat("M_Menu_Rastro", "AstroLab/Menu/Aditivo", x =>
            {
                x.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/FX_Rastro.png"));
                x.SetColor("_Cor", Hdr(Color.white, 4f));
            });
            m.fogLaranja = Toon("M_Menu_Foguete_Laranja", null, Hx("#F07A1E"), x => x.SetFloat("_RimForca", 0.35f));
            m.fogBranco = Toon("M_Menu_Foguete_Branco", null, Hx("#F4F1EA"), x => { x.SetFloat("_RimForca", 0.35f); x.SetFloat("_BrilhoTamanho", 0.04f); });
            m.fogEscuro = Toon("M_Menu_Foguete_Escuro", null, Hx("#2B2A3D"), x => x.SetFloat("_RimForca", 0.2f));
            return m;
        }

        static Material Mat(string nome, string shader, Action<Material> cfg)
        {
            string path = $"{MatDir}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            cfg?.Invoke(m);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        static Material Toon(string nome, Texture2D tex, Color cor, Action<Material> cfg) => Mat(nome, "AstroLab/Toon", m =>
        {
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", cor);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_EmissaoUsaTextura", 0f);
            m.SetFloat("_SemNevoa", 0f);
            m.SetFloat("_FaixaDesloc", 0f);
            m.SetFloat("_RimForca", 0.3f);
            m.SetFloat("_RimLargura", 0.25f);
            m.SetFloat("_BrilhoTamanho", 0f);
            m.SetFloat("_BrilhoForca", 0.6f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1); m.SetFloat("_Cull", (float)CullMode.Back);
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = (int)RenderQueue.Geometry;
            cfg?.Invoke(m);
        });

        static void Transparente(Material m)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static VolumeProfile CriarPosProcessamento()
        {
            string path = $"{Raiz}/PosProcessamento_Menu.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
            T Get<T>() where T : VolumeComponent
            {
                if (!p.TryGet<T>(out var c)) { c = p.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, p); }
                c.active = true; return c;
            }
            Get<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var bloom = Get<Bloom>(); bloom.threshold.Override(1.0f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.7f);
            var ca = Get<ColorAdjustments>(); ca.postExposure.Override(0f); ca.contrast.Override(12f); ca.saturation.Override(12f);
            var vig = Get<Vignette>(); vig.intensity.Override(0.22f); vig.color.Override(Hx("#1C1A3D")); vig.smoothness.Override(0.45f);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssetIfDirty(p);
            return p;
        }

        // ---------------------------------------------------------------- tema, fontes e sprites
        static TemaMenu CriarTema()
        {
            string path = $"{UiDir}/TemaMenu.asset";
            var t = AssetDatabase.LoadAssetAtPath<TemaMenu>(path);
            if (t == null) { t = ScriptableObject.CreateInstance<TemaMenu>(); AssetDatabase.CreateAsset(t, path); }
            t.fonteTitulo = Fonte("Orbitron", null);
            t.fonteTexto = Fonte("Exo2", null);
            Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/{n}.png");
            t.arredondado = S("UI_Arredondado"); t.borda = S("UI_Borda"); t.brilho = S("UI_Brilho");
            t.seta = S("UI_Seta"); t.faixaBrilho = S("UI_Faixa");
            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssetIfDirty(t);
            return t;
        }

        static TMP_FontAsset Fonte(string nome, string caracteres)
        {
            string path = $"{FontDir}/{nome} SDF.asset";
            var existente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existente != null) return existente;
            var ttf = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{nome}.ttf");
            var fa = TMP_FontAsset.CreateFontAsset(ttf, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            fa.name = nome + " SDF";
            if (caracteres == null)
            {
                var sb = new System.Text.StringBuilder();
                for (int c = 32; c < 127; c++) sb.Append((char)c);
                for (int c = 160; c < 256; c++) sb.Append((char)c);
                sb.Append("–—‘’“”•…×°·←→");
                caracteres = sb.ToString();
            }
            fa.TryAddCharacters(caracteres, out string faltando);
            if (!string.IsNullOrEmpty(faltando)) Debug.Log($"[Menu] Fonte {nome}: sem glifo para \"{faltando}\" (usa fallback).");
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(fa, path);
            foreach (var tex in fa.atlasTextures) { tex.name = nome + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
            fa.material.name = nome + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssetIfDirty(fa);
            return fa;
        }

        static Material MaterialTitulo(TMP_FontAsset f)
        {
            string path = $"{FontDir}/Orbitron SDF - Titulo.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(f.material); AssetDatabase.CreateAsset(m, path); }
            m.CopyPropertiesFromMaterial(f.material);
            m.shaderKeywords = f.material.shaderKeywords;
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0.18f, 0.75f, 1f, 0.55f));
            m.SetFloat("_UnderlayOffsetX", 0f); m.SetFloat("_UnderlayOffsetY", 0f);
            m.SetFloat("_UnderlayDilate", 0.35f); m.SetFloat("_UnderlaySoftness", 0.85f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        static void GerarSprites()
        {
            Salvar($"{UiDir}/UI_Arredondado.png", 64, 64, (x, y) => Cobertura(-SdRet(x, y, 64, 64, 16f, 0f)));
            Salvar($"{UiDir}/UI_Borda.png", 64, 64, (x, y) =>
            {
                float d = SdRet(x, y, 64, 64, 16f, 0f);
                return Cobertura(-d) * Cobertura(d + 2.6f);
            });
            Salvar($"{UiDir}/UI_Brilho.png", 128, 128, (x, y) =>
            {
                float d = SdRet(x, y, 128, 128, 18f, 40f);
                return d <= 0f ? 1f : Mathf.Exp(-(d * d) / (2f * 13f * 13f));
            });
            Salvar($"{UiDir}/UI_Seta.png", 32, 32, (x, y) =>
            {
                // triângulo apontando para a direita
                float px = x + 0.5f, py = y + 0.5f;
                float d1 = px - 6f, d2 = (26f - px) - Mathf.Abs(py - 16f) * 1.25f;
                return Cobertura(Mathf.Min(d1, d2));
            });
            Salvar($"{UiDir}/UI_Faixa.png", 128, 8, (x, y) =>
            {
                float u = (x + 0.5f) / 128f;
                return Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.5f);
            });
            Salvar($"{TexDir}/FX_Brilho.png", 128, 128, (x, y) =>
            {
                float dx = (x + 0.5f) / 64f - 1f, dy = (y + 0.5f) / 64f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float nucleo = Mathf.Exp(-r * r * 60f);
                float halo = Mathf.Exp(-r * r * 7f) * 0.55f;
                float raios = Mathf.Exp(-Mathf.Abs(dy) * 40f) * Mathf.Exp(-Mathf.Abs(dx) * 2.5f) * 0.5f
                            + Mathf.Exp(-Mathf.Abs(dx) * 40f) * Mathf.Exp(-Mathf.Abs(dy) * 2.5f) * 0.5f;
                return Mathf.Clamp01((nucleo + halo + raios) * Mathf.Clamp01(1.2f - r));
            });
            Salvar($"{TexDir}/FX_Rastro.png", 64, 32, (x, y) =>
            {
                float v = (y + 0.5f) / 32f - 0.5f;
                return Mathf.Exp(-(v * v) / (2f * 0.13f * 0.13f));
            });
        }

        static float Cobertura(float d) => Mathf.Clamp01(d + 0.5f);

        /// <summary>Distância com sinal até um retângulo arredondado (inset px das bordas da imagem).</summary>
        static float SdRet(int x, int y, int w, int h, float r, float inset)
        {
            float px = x + 0.5f - w / 2f, py = y + 0.5f - h / 2f;
            float hx = w / 2f - inset - r, hy = h / 2f - inset - r;
            float qx = Mathf.Abs(px) - hx, qy = Mathf.Abs(py) - hy;
            float fora = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return fora + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static void Salvar(string path, int w, int h, Func<int, int, float> alfa)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alfa(x, y)) * 255f));
            tex.SetPixels32(px);
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            if (File.Exists(full) && File.ReadAllBytes(full).SequenceEqual(bytes)) return;
            File.WriteAllBytes(full, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        // ---------------------------------------------------------------- Build Settings
        const string CenaCentro = "Assets/CentroTreinamento/Cenas/CentroTreinamento.unity";

        [MenuItem("AstroLab/Menu Inicial/Colocar no Build Settings (Menu 0, Centro 1)")]
        public static string ColocarNoBuild()
        {
            var cenas = EditorBuildSettings.scenes.ToList();
            cenas.RemoveAll(c => c.path == CenaPath || c.path == CenaCentro);
            int k = 0;
            cenas.Insert(k++, new EditorBuildSettingsScene(CenaPath, true));
            if (File.Exists(CenaCentro)) cenas.Insert(k++, new EditorBuildSettingsScene(CenaCentro, true));
            EditorBuildSettings.scenes = cenas.ToArray();
            string msg = "[Menu] Build Settings: " + string.Join(" | ", cenas.Select((c, n) => n + ":" + Path.GetFileNameWithoutExtension(c.path)));
            Debug.Log(msg);
            return msg;
        }
    }
}
