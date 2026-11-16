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

namespace AstroLab.Centro.Editor
{
    // Menu "Centro de Treinamento": materiais, mãos em 1ª pessoa e montagem da cena CentroTreinamento.unity.
    // Pode rodar de novo sempre que os FBX forem reexportados do Blender~ (a cena é recriada do zero).
    public static class CentroTreinamentoSetup
    {
        const string Raiz = EstiloCentro.Raiz;
        public const string CenaPath = Raiz + "/Cenas/CentroTreinamento.unity";
        const string FbxCentro = Raiz + "/Modelos/CentroTreinamento.fbx";
        const string FbxNpc = Raiz + "/Modelos/NPC_Instrutora.fbx";
        const string FbxPlataforma = Raiz + "/Modelos/PlataformaLancamento.fbx";
        const string FbxOnibus = Raiz + "/Modelos/OnibusEspacial.fbx";
        const string FbxCorredor = Raiz + "/Modelos/Corredor_Kit.fbx";
        const string JsonCorredor = Raiz + "/Dados/Corredor_Caminho.json";
        const string PrefabDir = Raiz + "/Prefabs";
        const string MaosOrig = "Assets/Personagem/PrimeiraPessoa/Prefabs/MaosEScanner.prefab";
        const string ShaderViewmodel = "AstroLab/MaosCentro";   // visual do shader do time + mãos sempre por cima

        // Blender (Z para cima) -> Unity (Y para cima), igual ao export FBX (-Z forward, Y up)
        static Vector3 B(float x, float y, float z) => new Vector3(-x, z, -y);

        // ================================================================== 1. materiais
        [MenuItem("Centro de Treinamento/1. Texturas e materiais")]
        public static void Materiais()
        {
            EstiloCentro.GerarTexturas();
            EstiloCentro.MateriaisDoModelo(FbxCentro);
            EstiloCentro.MateriaisDoModelo(FbxNpc);
            if (File.Exists(FbxPlataforma)) EstiloCentro.MateriaisDoModelo(FbxPlataforma);
            if (File.Exists(FbxOnibus)) EstiloCentro.MateriaisDoModelo(FbxOnibus);
            if (File.Exists(FbxCorredor)) EstiloCentro.MateriaisDoModelo(FbxCorredor);
            EstiloCentro.Ceu();
            EstiloCentro.Pos();
        }

        // ================================================================== 2. mãos (cópias do MaosEScanner do Pedro Scalon, sem editar o original)
        // Versão aprovada pelo Vinicius: blocos do MaosEScanner, mesma posição/pose. (Maos_Centro.fbx do Blender foi reprovado: não usar.)
        [MenuItem("Centro de Treinamento/2. Mãos em primeira pessoa")]
        public static void Maos()
        {
            Directory.CreateDirectory(PrefabDir);
            var pele = MatViewmodel("MV_Pele", "#D9A07A");
            var manga = MatViewmodel("MV_Manga_Macacao", "#2F5BB7");
            var punho = MatViewmodel("MV_Punho_Macacao", "#22428A");
            var luvaBranca = MatViewmodel("MV_Luva_Traje", "#F1F0EA");
            var luvaJunta = MatViewmodel("MV_Luva_Traje_Junta", "#3E4C78");
            var luvaProt = MatViewmodel("MV_Luva_Traje_Protecao", "#C9D3E0");
            var luvaFaixa = MatViewmodel("MV_Luva_Traje_Faixa", "#2456B8");

            // mãos nuas: tira as peças da luva, dedos mais finos, pele + manga do macacão azul com punho
            CriarMaos("Maos_Nuas", raiz =>
            {
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (t == null) continue;
                    string n = t.name;
                    if (n.StartsWith("Protecao") || n.StartsWith("Junta") || n is "PlacaDorso" or "CosturaDorso" or "AnelPunho" or "CosturaManga" or "FaixaCiano")
                    { Object.DestroyImmediate(t.gameObject); continue; }
                    var r = t.GetComponent<MeshRenderer>();
                    if (r == null) continue;
                    if (n == "Manga") r.sharedMaterial = manga;
                    else r.sharedMaterial = pele;
                    if (n.StartsWith("Luva")) t.localScale = Vector3.Scale(t.localScale, new Vector3(0.8f, 0.78f, 1f));
                    if (n == "Palma") t.localScale = Vector3.Scale(t.localScale, new Vector3(0.94f, 0.82f, 1f));
                    if (n == "Punho") t.localScale = Vector3.Scale(t.localScale, new Vector3(0.78f, 0.78f, 1f));
                    if (n is "BasePolegar" or "PontaPolegar") t.localScale = Vector3.Scale(t.localScale, new Vector3(0.72f, 0.72f, 0.85f));
                }
                // polegar encostado na palma (nu ele fica fino e "solto" na posição da luva)
                foreach (var pol in raiz.GetComponentsInChildren<Transform>(true).Where(x => x.name == "Polegar").ToArray())
                    pol.localPosition = new Vector3(pol.localPosition.x * 0.78f, pol.localPosition.y, pol.localPosition.z - 0.012f);
                foreach (var m in raiz.GetComponentsInChildren<Transform>(true).Where(x => x.name == "Manga").ToArray())
                {
                    var c = Object.Instantiate(m.gameObject, m.parent);
                    c.name = "PunhoManga";
                    c.transform.localPosition = m.localPosition + new Vector3(0, 0, 0.095f);
                    c.transform.localScale = new Vector3(m.localScale.x * 1.06f, m.localScale.y * 1.08f, 0.035f);
                    c.GetComponent<MeshRenderer>().sharedMaterial = punho;
                }
            });
            // luvas de treino: as luvas do time (mesmo visual da Lua), sem o scanner
            CriarMaos("Maos_LuvaTreino", raiz => { });
            // luvas do traje completo: brancas com juntas índigo e faixa azul (combinam com o traje da missão)
            CriarMaos("Maos_LuvaTraje", raiz =>
            {
                foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = r.name;
                    if (n.StartsWith("Junta") || n is "CosturaDorso" or "AnelPunho") r.sharedMaterial = luvaJunta;
                    else if (n.StartsWith("Protecao") || n is "PlacaDorso" or "CosturaManga") r.sharedMaterial = luvaProt;
                    else if (n == "FaixaCiano") r.sharedMaterial = luvaFaixa;
                    else r.sharedMaterial = luvaBranca;
                }
            });
            AssetDatabase.Refresh();
            Debug.Log("[Centro] Prefabs de mãos (derivados do MaosEScanner) criados em " + PrefabDir);
        }

        static Material MatViewmodel(string nome, string cor)
        {
            Directory.CreateDirectory(EstiloCentro.MatDir);
            string path = $"{EstiloCentro.MatDir}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(ShaderViewmodel)); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find(ShaderViewmodel);
            m.SetColor("_BaseColor", EstiloCentro.Hx(cor));
            m.SetColor("_EmissionColor", nome.Contains("Faixa") ? EstiloCentro.Hx(cor) * 0.6f : Color.black);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        static void CriarMaos(string nome, System.Action<GameObject> ajustar)
        {
            var raiz = PrefabUtility.LoadPrefabContents(MaosOrig);
            try
            {
                raiz.name = nome;
                var scanner = raiz.transform.Find("ScannerPortatil");
                if (scanner) Object.DestroyImmediate(scanner.gameObject);
                var mm = raiz.GetComponent<MovimentoMaos>();
                if (mm) mm.scanner = null;
                ajustar(raiz);
                PrefabUtility.SaveAsPrefabAsset(raiz, $"{PrefabDir}/{nome}.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        // ================================================================== 3. cena
        [MenuItem("Centro de Treinamento/3. Montar cena")]
        public static void MontarCena()
        {
            var ativa = EditorSceneManager.GetActiveScene();
            if (ativa.isDirty) { Debug.LogError($"[Centro] A cena aberta ({ativa.path}) tem alterações não salvas. Salve ou descarte antes de montar."); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(CenaPath));
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------------------------------------------------- modelo
            // desempacota: os FBX não levam hierarquia (empties pais quebram a escala), então os vidros das peças
            // animadas (ANIM_*_Transp) e do interior do ônibus são presos à peça aqui. Reexportou? Rode "Montar cena" de novo.
            var todos = new List<Transform>();
            GameObject Carregar(string fbx, string nome)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (asset == null) { Debug.LogWarning("[Centro] Modelo ausente: " + fbx); return null; }
                var g = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                g.name = nome;
                PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                todos.AddRange(g.GetComponentsInChildren<Transform>(true));
                return g;
            }
            var centro = Carregar(FbxCentro, "CentroTreinamento");
            Carregar(FbxPlataforma, "PlataformaLancamento");
            Carregar(FbxOnibus, "OnibusEspacial");
            Transform A(string n) => todos.FirstOrDefault(t => t.name == n);
            foreach (var t in todos.Where(t => (t.name.StartsWith("ANIM_") || t.name.StartsWith("Onibus_Interior")) && t.name.EndsWith("_Transp")).ToList())
            {
                var pai = A(t.name.Substring(0, t.name.Length - "_Transp".Length));
                if (pai) t.SetParent(pai, true);
            }
            int cols = 0;
            foreach (var t in todos)
            {
                var go = t.gameObject; string n = go.name;
                var r = go.GetComponent<MeshRenderer>();
                if (n.StartsWith("COL_") || n.StartsWith("ZONA_"))
                {
                    if (r) r.enabled = false;
                    var bc = go.AddComponent<BoxCollider>();
                    bc.isTrigger = n.StartsWith("ZONA_");
                    GameObjectUtility.SetStaticEditorFlags(go, 0);
                    cols++;
                    continue;
                }
                if (r == null) continue;
                bool anim = t.GetComponentsInParent<Transform>(true).Any(p => p.name.StartsWith("ANIM_"));
                GameObjectUtility.SetStaticEditorFlags(go, anim ? 0 : StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                bool transp = r.sharedMaterials.All(EstiloCentro.EhTransparente);
                if (transp || n == "Terreno") r.shadowCastingMode = ShadowCastingMode.Off;
                if (transp) GameObjectUtility.SetStaticEditorFlags(go, anim ? 0 : StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }

            // ---------------------------------------------------------- luz, céu, pós e estilo cartoon (dia)
            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional; sol.color = EstiloCentro.Hx("#FFF2DC"); sol.intensity = 1.2f;
            sol.shadows = LightShadows.Soft; sol.shadowStrength = 0.85f;
            sol.transform.rotation = Quaternion.Euler(42f, 205f, 0f);
            RenderSettings.sun = sol;
            RenderSettings.skybox = EstiloCentro.Ceu();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = EstiloCentro.Hx("#9DB8E6");       // gradiente: céu frio,
            RenderSettings.ambientEquatorColor = EstiloCentro.Hx("#C8CCD6");   // equador neutro,
            RenderSettings.ambientGroundColor = EstiloCentro.Hx("#6B5E52");    // chão quente escuro
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = EstiloCentro.Hx("#CFE2F2");
            RenderSettings.fogStartDistance = 140f; RenderSettings.fogEndDistance = 700f;
            var vol = new GameObject("PosProcessamento").AddComponent<Volume>();
            vol.isGlobal = true; vol.sharedProfile = EstiloCentro.Pos();
            var ilum = new GameObject("IluminacaoCartoon").AddComponent<IluminacaoCartoon>();
            ilum.sombra = EstiloCentro.Hx("#8F92CC"); ilum.meioTom = EstiloCentro.Hx("#D4D7F0");
            ilum.ambienteCeu = EstiloCentro.Hx("#9DB8E6"); ilum.ambienteChao = EstiloCentro.Hx("#7D8670");
            ilum.bordaIluminada = Color.white; ilum.corContorno = EstiloCentro.Hx("#1C1A3D");
            ilum.limiteMeioTom = 0.35f; ilum.limiteSombra = 0.0f;

            var luzes = new GameObject("Luzes_Hangar").transform;
            void Ponto(string n, Vector3 p, Color c, float i, float alcance)
            {
                var l = new GameObject(n).AddComponent<Light>();
                l.transform.SetParent(luzes); l.transform.position = p;
                l.type = LightType.Point; l.color = c; l.intensity = i; l.range = alcance; l.shadows = LightShadows.None;
            }
            var quente = EstiloCentro.Hx("#FFF0D8");
            // recepção (L1): poucas luzes, o resto é emissivo (luminárias do forro)
            Ponto("Luz_Recepcao_O", B(-7, 9, 6.5f), quente, 1.8f, 13f);
            Ponto("Luz_Recepcao_L", B(7, 9, 6.5f), quente, 1.8f, 13f);
            Ponto("Luz_Recepcao_Fundo", B(0, 16, 5.5f), quente, 1.4f, 10f);
            var spot = new GameObject("Luz_Destaque_Letreiro").AddComponent<Light>();                    // destaque quente no letreiro
            spot.type = LightType.Spot; spot.color = EstiloCentro.Hx("#FFE2B8"); spot.intensity = 6f; spot.range = 14f; spot.spotAngle = 70f;
            spot.transform.position = B(0, 11, 7.3f); spot.transform.rotation = Quaternion.LookRotation(B(0, 19.5f, 3.4f) - B(0, 11, 7.3f));
            Ponto("Luz_Subsolo_Vestibulo", B(10.8f, 16, -16.6f), EstiloCentro.Hx("#BFE6FF"), 2.2f, 10f);

            // ---------------------------------------------------------- jogador
            var spawn = A("SPAWN_Player");
            var jogador = new GameObject("Jogador");
            jogador.transform.SetPositionAndRotation((spawn ? spawn.position : Vector3.zero) + Vector3.up * 1.0f, Quaternion.Euler(0, 180, 0));
            var cc = jogador.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = Vector3.zero; cc.slopeLimit = 40f; cc.stepOffset = 0.35f;
            var pegadas = jogador.AddComponent<AstroLab.BaseLunar.PegadasLunar>();
            pegadas.enabled = false;                                             // sem pegadas lunares na Terra
            var fps = jogador.AddComponent<JogadorFPS>();
            PerfilGravidade.Terra.AplicarEm(fps);
            jogador.AddComponent<AplicadorConfiguracoesJogador>();
            var interacao = jogador.AddComponent<InteracaoJogador>();
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(jogador.transform, false);
            camGo.transform.localPosition = new Vector3(0, 0.8f, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f; cam.farClipPlane = 1500f; cam.fieldOfView = 75f;
            camGo.AddComponent<AudioListener>();
            var cd = camGo.AddComponent<UniversalAdditionalCameraData>();
            cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.None;      // MSAA 4x no asset (traços finos nítidos)
            cam.allowMSAA = true;
            // mãos por cima do cenário: shader AstroLab/MaosCentro (cópia do do time + profundidade comprimida).
            // Câmera Overlay não serve aqui: o contorno do toon é um FullScreenPass do único renderer e roda também
            // na Overlay (que só tem a profundidade das mãos), pintando a tela inteira.
            var raizMaos = new GameObject("Maos").transform;
            raizMaos.SetParent(camGo.transform, false);

            GameObject InstMaos(string n)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{n}.prefab");
                var g = (GameObject)PrefabUtility.InstantiatePrefab(p, raizMaos);
                var mm = g.GetComponent<MovimentoMaos>(); if (mm) mm.jogador = jogador.transform;
                return g;
            }
            var vest = jogador.AddComponent<EstadoVestimentaJogador>();
            vest.raizMaos = raizMaos;
            vest.maosNuas = InstMaos("Maos_Nuas");
            vest.luvasTreino = InstMaos("Maos_LuvaTreino");
            vest.luvasTraje = InstMaos("Maos_LuvaTraje");
            vest.luvasTreino.SetActive(false); vest.luvasTraje.SetActive(false);

            // ---------------------------------------------------------- instrutora
            var npcGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FbxNpc));
            npcGo.name = "Instrutora_Lima";
            npcGo.transform.localScale = Vector3.one * 1.06f;                  // 1,82 m: presença ao lado do jogador
            var npcInicio = A("NPC_Inicio");
            npcGo.transform.position = npcInicio ? npcInicio.position : Vector3.zero;
            foreach (var t in npcGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;   // Ignore Raycast
            var caps = npcGo.AddComponent<CapsuleCollider>();
            caps.height = 1.75f; caps.radius = 0.28f; caps.center = new Vector3(0, 0.88f, 0);
            var npc = npcGo.AddComponent<NpcInstrutor>();
            npc.jogador = jogador.transform;
            npc.chao = ~(1 << 2);

            // ---------------------------------------------------------- UI
            var ui = new GameObject("UI_Centro");
            var hud = ui.AddComponent<HudCentro>();
            hud.fonte = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MenuInicial/Fontes/Exo2 SDF.asset");
            hud.fonteTitulo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MenuInicial/Fontes/Orbitron SDF.asset");
            hud.spriteArredondado = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var dialogo = ui.AddComponent<CaixaDialogo>();
            dialogo.hud = hud;
            interacao.hud = hud; interacao.dialogo = dialogo;

            // ---------------------------------------------------------- estações (consoles, peças animadas)
            var consoles = new Interagivel[7];
            for (int i = 0; i < 7; i++)
            {
                var c = todos.FirstOrDefault(t => t.name.StartsWith($"EST_{i}_") && t.name.EndsWith("_Console"));
                if (c == null) { Debug.LogWarning($"[Centro] console da estação {i} não encontrado"); continue; }
                consoles[i] = c.gameObject.AddComponent<Interagivel>();
                consoles[i].chaveAcao = $"treino.est{i}.acao";
                consoles[i].ativo = false;
            }
            var centrifuga = A("ANIM_Centrifuga_Braco")?.gameObject.AddComponent<Centrifuga>();
            var plataforma = A("ANIM_NBL_Plataforma")?.gameObject.AddComponent<Deslizador>();
            if (plataforma) { plataforma.deslocamento = new Vector3(0, -5.6f, 0); plataforma.duracao = 4f; }
            var portaSaidaT = A("ANIM_Porta_Saida");
            Deslizador portaSaida = null;
            if (portaSaidaT)
            {
                portaSaida = portaSaidaT.gameObject.AddComponent<Deslizador>();
                portaSaida.deslocamento = portaSaidaT.parent.InverseTransformVector(Vector3.up * 6.3f);
                portaSaida.duracao = 3.5f;
                portaSaidaT.gameObject.AddComponent<BoxCollider>();
            }
            var zonaT = A("ZONA_GravidadeLunar");
            ZonaGravidade zona = null;
            if (zonaT)
            {
                zona = zonaT.gameObject.AddComponent<ZonaGravidade>();
                var rb = zonaT.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            }
            var ponte = A("ANIM_ARGOS_Ponte")?.gameObject.AddComponent<PonteArgos>();
            if (ponte) { ponte.jogador = jogador.transform; ponte.zona = zona; }

            // portas de vidro da entrada
            var folhaO = A("ANIM_Porta_Entrada_O"); var folhaL = A("ANIM_Porta_Entrada_L");
            if (folhaO && folhaL)
            {
                var porta = new GameObject("PortaEntrada");
                porta.transform.position = B(0, -0.2f, 1.6f);
                var pa = porta.AddComponent<PortaAutomatica>();
                pa.folhas = new[] { folhaO, folhaL };
                pa.aberturas = new[] { folhaO.parent.InverseTransformVector(B(-2.5f, 0, 0)), folhaL.parent.InverseTransformVector(B(2.5f, 0, 0)) };
                pa.quemAbre = new[] { jogador.transform, npcGo.transform };
                var bl = porta.AddComponent<BoxCollider>();
                bl.size = new Vector3(5.3f, 3.2f, 0.3f);
                pa.bloqueio = bl;
            }

            // marcador de objetivo (losango âmbar)
            var marc = new GameObject("MarcadorObjetivo");
            marc.AddComponent<MeshFilter>().sharedMesh = Losango();
            var mr = marc.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{EstiloCentro.MatDir}/CT_Luz_Ambar.mat");
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var marcador = marc.AddComponent<MarcadorObjetivo>();
            marc.SetActive(false);

            // ---------------------------------------------------------- tutorial
            var tut = new GameObject("Tutorial").AddComponent<TutorialCentro>();
            tut.npc = npc; tut.npcInicio = npcInicio; tut.jogador = fps; tut.hud = hud; tut.dialogo = dialogo;
            tut.vestimenta = vest; tut.marcador = marcador;
            Vector3 W(int k) => A($"WP_NPC_{k:00}") ? A($"WP_NPC_{k:00}").position : Vector3.zero;
            var faixas = new (int a, int b)[] { (1, 2), (3, 3), (3, 3), (3, 3), (3, 3), (3, 3), (3, 3) };
            var ids = new[] { "Recepcao", "Medico", "Centrifuga", "RealidadeVirtual", "NBL", "ARGOS", "Trajes" };
            var etapas = new List<EtapaTreino>();
            for (int i = 0; i < 7; i++)
            {
                var ponto = A($"EST_{i}_{ids[i]}_NPC");
                if (ponto == null || i > 0) continue;                          // estações 1..6: rotas pelo corredor (Salas)
                var rota = new List<Vector3>();
                for (int k = faixas[i].a; k <= faixas[i].b; k++) rota.Add(W(k));
                if (ponto) rota.Add(ponto.position);
                etapas.Add(new EtapaTreino { id = $"est{i}", pontoNpc = ponto, console = consoles[i], rota = rota.ToArray() });
            }
            tut.etapas = etapas.ToArray();
            tut.rotaSaida = new[] { W(17), W(18) };
            tut.alvoFinal = A("WP_Estrada_Fim");
            tut.centrifuga = centrifuga; tut.plataformaNbl = plataforma; tut.portaSaida = portaSaida; tut.zonaLunar = zona;
            var vr = A("Est3_RealidadeVirtual");
            tut.telasVr = vr ? new[] { vr.GetComponent<Renderer>() } : new Renderer[0];

            // ---------------------------------------------------------- passagem secreta (L1)
            var cab = A("ANIM_Elevador_Secreto");
            if (cab)
            {
                var ps = new GameObject("PassagemSecreta").AddComponent<PassagemSecreta>();
                ps.painel = A("ANIM_Painel_Secreto");
                ps.bloqueioPainel = A("COL_Painel_Secreto") ? A("COL_Painel_Secreto").GetComponent<Collider>() : null;
                ps.cabine = cab;
                ps.portaE = A("ANIM_Elevador_Secreto_Porta_E"); ps.portaD = A("ANIM_Elevador_Secreto_Porta_D");
                foreach (var n in new[] { "ANIM_Elevador_Secreto_Porta_E", "ANIM_Elevador_Secreto_Porta_D", "ZONA_Elevador_Secreto", "COL_Elevador_Secreto_Piso", "INT_Elevador_Secreto" })
                    if (A(n)) A(n).SetParent(cab, true);
                foreach (var n in new[] { "ANIM_Elevador_Secreto_Porta_E", "ANIM_Elevador_Secreto_Porta_D" })
                    if (A(n)) A(n).gameObject.AddComponent<BoxCollider>();
                ps.zonaCabine = A("ZONA_Elevador_Secreto") ? A("ZONA_Elevador_Secreto").GetComponent<Collider>() : null;
                ps.fundo = A("WP_Elevador_Secreto_Fundo");
                ps.pontoNpcPainel = A("WP_Painel_Secreto"); ps.pontoNpcCabine = A("NPC_Elevador_Secreto"); ps.pontoSaida = A("WP_Subsolo_Vestibulo");
                var lp = new GameObject("Luz_Poco").AddComponent<Light>();
                lp.transform.position = B(10.8f, 21.5f, 2.6f); lp.type = LightType.Point; lp.color = EstiloCentro.Hx("#9FDcFF"); lp.intensity = 2.5f; lp.range = 7f;
                ps.luzPoco = lp;
                var lc = new GameObject("Luz_Cabine").AddComponent<Light>();
                lc.transform.SetParent(cab, false); lc.transform.position = B(10.8f, 22f, 2.9f);
                lc.type = LightType.Point; lc.color = quente; lc.intensity = 1.2f; lc.range = 4.5f;
                var mo = new GameObject("Mostrador_Andar").AddComponent<TextMeshPro>();
                mo.transform.SetParent(cab, false);
                mo.transform.SetPositionAndRotation(B(10.8f, 23.36f, 2.75f), Quaternion.LookRotation(Vector3.back, Vector3.up));
                mo.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MenuInicial/Fontes/Orbitron SDF.asset");
                mo.fontSize = 2.0f; mo.alignment = TextAlignmentOptions.Center; mo.color = EstiloCentro.Hx("#FFB45C");
                mo.rectTransform.sizeDelta = new Vector2(0.95f, 0.28f); mo.textWrappingMode = TextWrappingModes.NoWrap;
                ps.mostrador = mo;
                tut.passagem = ps; tut.etapaAntesDoElevador = 0;
            }

            // ---------------------------------------------------------- corredor super secreto (L2): kit instanciado no caminho
            var portas = Corredor(quente);

            // ---------------------------------------------------------- letreiros (TextMeshPro 3D: nítido a qualquer distância)
            Letreiros(Raiz + "/Dados/Letreiros_Centro.json");
            // ---------------------------------------------------------- salas (L3): agrupa cada sala e encaixa na sua porta
            Salas(portas, tut, A);

            // ---------------------------------------------------------- plataforma de lançamento e ônibus espacial
            var elevT = A("ANIM_Elevador");
            ElevadorTorre elev = null;
            if (elevT)
            {
                elev = elevT.gameObject.AddComponent<ElevadorTorre>();
                elev.baseNivel = A("WP_Elevador_Base"); elev.topoNivel = A("WP_Elevador_Topo"); elev.jogador = fps;
                var intE = A("INT_Elevador");
                if (intE)
                {
                    intE.SetParent(elevT, true);
                    var it = intE.gameObject.AddComponent<Interagivel>();
                    it.chaveAcao = "treino.elevador.acao"; it.alcance = 1.9f;
                    elev.botoeira = it;
                }
                var zE = A("ZONA_Elevador"); if (zE) { zE.SetParent(elevT, true); elev.zonaCabine = zE.GetComponent<Collider>(); }
                var piso = A("COL_Elevador_Piso"); if (piso) piso.SetParent(elevT, true);
            }
            var escT = A("INT_Escotilha");
            Interagivel escotilha = null;
            if (escT)
            {
                escotilha = escT.gameObject.AddComponent<Interagivel>();
                escotilha.chaveAcao = "ct.escotilha.acao"; escotilha.alcance = 2.4f; escotilha.ativo = false;
            }
            var assento = new GameObject("AssentoComandante").AddComponent<AssentoCabine>();
            assento.olho = A("SEAT_CDR_Olho");
            var interior = A("Onibus_Interior");
            if (interior && assento.olho)
            {
                var lc = new GameObject("Luz_Cabine").AddComponent<Light>();
                lc.transform.SetParent(interior, false);
                lc.transform.position = assento.olho.position + Vector3.forward * 0.3f + Vector3.up * 0.4f;
                lc.type = LightType.Point; lc.color = EstiloCentro.Hx("#FFF0D8"); lc.intensity = 1.6f; lc.range = 5f; lc.shadows = LightShadows.None;
                foreach (var r in interior.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            // LOD do exterior do ônibus: LOD0 (completo) / LOD1 (~40%) / LOD2 (silhueta)
            var pecasLod = new[] { "Onibus_Orbitador", "Onibus_TanqueExterno", "Onibus_Boosters" };
            var orb = A("Onibus_Orbitador");
            if (orb)
            {
                var lodGo = new GameObject("Onibus_LOD");
                lodGo.transform.position = A("Onibus_TanqueExterno") ? A("Onibus_TanqueExterno").GetComponent<Renderer>().bounds.center : orb.position;
                lodGo.transform.SetParent(orb.parent, true);
                Renderer[] Nivel(string suf) => pecasLod.Select(n => A(n + suf)).Where(t => t).Select(t => { t.SetParent(lodGo.transform, true); return t.GetComponent<Renderer>(); })
                    .Concat(suf == "" && A("Onibus_Orbitador_Transp") ? new[] { A("Onibus_Orbitador_Transp").GetComponent<Renderer>() } : new Renderer[0]).ToArray();
                var lg = lodGo.AddComponent<LODGroup>();
                lg.SetLODs(new[] { new LOD(0.22f, Nivel("")), new LOD(0.07f, Nivel("_L1")), new LOD(0.008f, Nivel("_L2")) });
                lg.RecalculateBounds();
                if (A("Onibus_Orbitador_Transp")) A("Onibus_Orbitador_Transp").SetParent(lodGo.transform, true);
            }
            var ctrl = new GameObject("ControleLancamento").AddComponent<ControleLancamento>();
            ctrl.tutorial = tut; ctrl.jogador = fps; ctrl.hud = hud; ctrl.marcador = marcador;
            ctrl.elevador = elev; ctrl.escotilha = escotilha; ctrl.assento = assento;
            ctrl.zonaPlataforma = A("ZONA_Plataforma") ? A("ZONA_Plataforma").GetComponent<Collider>() : null;
            ctrl.alvoElevador = A("INT_Elevador");
            ctrl.interior = interior ? interior.gameObject : null;

            AntiSerrilhado();
            EditorSceneManager.SaveScene(cena, CenaPath);
            VarrerShaders();
            Debug.Log($"[Centro] Cena salva em {CenaPath}: {cols} colisores/zonas, {todos.Count} objetos do modelo.");
        }

        // ================================================================== qualidade
        public const string LayerMaos = "PrimeiraPessoa";
        public const float TmpEscala = 10f;          // fontSize do TextMeshPro 3D por metro de altura de letra (calibrado)

        // cria a layer no TagManager se não existir (autorizado pelo coordenador; avisar o chat do menu)
        public static int GarantirLayer(string nome)
        {
            int existente = LayerMask.NameToLayer(nome);
            if (existente >= 0) return existente;
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue)) { el.stringValue = nome; tm.ApplyModifiedPropertiesWithoutUndo(); Debug.Log($"[Centro] Layer {nome} = {i}"); return i; }
            }
            Debug.LogError("[Centro] Sem layer livre para " + nome);
            return 0;
        }

        // MSAA 4x no asset URP de PC (FXAA/SMAA borram os traços finos do contorno)
        static void AntiSerrilhado()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("PC")) continue;
                var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (rp.msaaSampleCount != 4) { rp.msaaSampleCount = 4; EditorUtility.SetDirty(rp); AssetDatabase.SaveAssetIfDirty(rp); Debug.Log("[Centro] MSAA 4x em " + path); }
            }
        }

        static Material PresetTmp(TMP_FontAsset f, string nome, float contorno)
        {
            string path = $"{EstiloCentro.MatDir}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(f.material); AssetDatabase.CreateAsset(m, path); }
            m.shader = f.material.shader;
            m.SetTexture("_MainTex", f.material.GetTexture("_MainTex"));
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", contorno);
            m.SetColor("_OutlineColor", EstiloCentro.Hx("#1C1A3D"));
            m.SetFloat("_FaceDilate", contorno > 0 ? 0.1f : 0f);
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        [System.Serializable] class Letreiro { public string nome, texto, cor, alinhar; public float[] pos, direita, cima, normal; public float tam; }
        [System.Serializable] class ListaLetreiros { public Letreiro[] letreiros; }

        static void Letreiros(string json)
        {
            if (!File.Exists(json)) { Debug.LogWarning("[Centro] Sem " + json); return; }
            var lista = JsonUtility.FromJson<ListaLetreiros>(File.ReadAllText(json));
            var exo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MenuInicial/Fontes/Exo2 SDF.asset");
            var orb = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MenuInicial/Fontes/Orbitron SDF.asset") ?? exo;
            var claroExo = PresetTmp(exo, "TMP_Letreiro_Exo2_Contorno", 0.15f);
            var escuroExo = PresetTmp(exo, "TMP_Letreiro_Exo2", 0.0f);
            var claroOrb = PresetTmp(orb, "TMP_Letreiro_Orbitron_Contorno", 0.15f);
            var escuroOrb = PresetTmp(orb, "TMP_Letreiro_Orbitron", 0.0f);
            var pai = new GameObject("Letreiros").transform;
            int n = 0, espelhados = 0;
            foreach (var L in lista.letreiros)
            {
                var go = new GameObject(L.nome);
                go.transform.SetParent(pai, false);
                var tmp = go.AddComponent<TextMeshPro>();
                bool titulo = L.tam >= 1.0f;
                var cor = EstiloCentro.Cores.TryGetValue(L.cor, out var h) ? EstiloCentro.Hx(h) : Color.white;
                bool claro = cor.grayscale > 0.55f;
                tmp.font = titulo ? orb : exo;
                tmp.fontSharedMaterial = titulo ? (claro ? claroOrb : escuroOrb) : (claro ? claroExo : escuroExo);
                tmp.text = L.texto;
                tmp.color = cor;
                tmp.fontSize = L.tam * TmpEscala;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.alignment = L.alinhar == "LEFT" ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
                tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, L.texto.Length * L.tam * 0.75f + L.tam), L.tam * 1.6f);
                var n_ = new Vector3(L.normal[0], L.normal[1], L.normal[2]);
                var up = new Vector3(L.cima[0], L.cima[1], L.cima[2]);
                var dir = new Vector3(L.direita[0], L.direita[1], L.direita[2]);
                // texto do TMP é lido de frente olhando para +Z local: frente local = -normal
                var rot = Quaternion.LookRotation(-n_, up);
                go.transform.SetPositionAndRotation(new Vector3(L.pos[0], L.pos[1], L.pos[2]) + n_ * 0.012f, rot);
                if (Vector3.Dot(go.transform.right, dir) < 0) { go.transform.rotation = Quaternion.LookRotation(n_, up); espelhados++; }
                n++;
            }
            Debug.Log($"[Centro] {n} letreiros TMP ({espelhados} corrigidos de lado).");
        }

        // todo renderer da cena num shader da família toon (ou viewmodel/TMP/céu)
        public static string VarrerShaders()
        {
            var ruins = new List<string>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                foreach (var m in r.sharedMaterials)
                {
                    string sh = m ? m.shader.name : "null";
                    if (!(sh.StartsWith("AstroLab/") || sh.StartsWith("TextMeshPro/") || sh.StartsWith("Skybox/")))
                        ruins.Add($"{r.name}:{(m ? m.name : "null")}({sh})");
                }
            string res = ruins.Count == 0 ? "0 materiais fora do toon" : $"{ruins.Count} fora do toon: " + string.Join(", ", ruins.Take(12));
            Debug.Log("[Centro] Varredura de shaders: " + res);
            return res;
        }

        [System.Serializable] class Modulo { public int i, lado; public string tipo, sala; public float[] pos; public float rumoBlender; }
        [System.Serializable] class Caminho { public Modulo[] modulos; public float comprimento; }

        // Monta o corredor: cada módulo é uma cópia das peças do kit (mesma malha = static batching barato).
        // Portas à esquerda usam o mesmo módulo girado 180° (o tubo é simétrico). Devolve as portas por nome de sala.
        public static Dictionary<string, PortaHex> Corredor(Color luzCor)
        {
            var portas = new Dictionary<string, PortaHex>();
            var kit = AssetDatabase.LoadAssetAtPath<GameObject>(FbxCorredor);
            if (kit == null || !File.Exists(JsonCorredor)) { Debug.LogWarning("[Centro] Kit do corredor ausente."); return portas; }
            var cam = JsonUtility.FromJson<Caminho>(File.ReadAllText(JsonCorredor));
            float L = cam.comprimento;
            var pai = new GameObject("Corredor").transform;
            ModulosCorredor.Clear();
            var orb = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MenuInicial/Fontes/Orbitron SDF.asset");
            int nLuz = 0;
            foreach (var md in cam.modulos)
            {
                float r = md.rumoBlender * Mathf.Deg2Rad;
                var dir = new Vector3(-Mathf.Cos(r), 0, -Mathf.Sin(r));                    // frente do módulo no Unity
                var rot = Quaternion.LookRotation(-dir, Vector3.up);                         // o +Y do Blender vira -Z local
                var pos = new Vector3(md.pos[0], md.pos[1], md.pos[2]);
                if (md.lado < 0 && (md.tipo == "Seg_Porta" || md.tipo == "Seg_Janela")) { rot *= Quaternion.Euler(0, 180, 0); pos += dir * L; }
                var go = new GameObject($"Mod_{md.i:00}_{md.tipo}{(string.IsNullOrEmpty(md.sala) ? "" : "_" + md.sala)}");
                go.transform.SetParent(pai, false);
                go.transform.SetPositionAndRotation(pos, rot);
                ModulosCorredor.Add(go.transform);
                GameObject Copiar(string nome, bool estatico = true)
                {
                    var src = kit.transform.Find(nome);
                    if (src == null) return null;
                    var c = Object.Instantiate(src.gameObject, go.transform);
                    c.name = nome;
                    GameObjectUtility.SetStaticEditorFlags(c, estatico ? StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic : 0);
                    var rr = c.GetComponent<MeshRenderer>();
                    if (rr && rr.sharedMaterials.All(EstiloCentro.EhTransparente)) { rr.shadowCastingMode = ShadowCastingMode.Off; GameObjectUtility.SetStaticEditorFlags(c, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic); }
                    return c;
                }
                Copiar(md.tipo);
                foreach (Transform t in kit.transform)
                    if (t.name.StartsWith($"COL_{md.tipo}_") || t.name == $"COL_{md.tipo}")
                    {
                        var c = Copiar(t.name);
                        c.GetComponent<MeshRenderer>().enabled = false;
                        c.AddComponent<BoxCollider>();
                    }
                if (md.tipo == "Seg_Janela") { Copiar("Seg_Janela_Vidro"); Copiar("Seg_Janela_CasaMaquinas"); }
                if (md.tipo == "Seg_Porta")
                {
                    Copiar("Porta_Hex_Moldura");
                    var anel = Copiar("Porta_Hex_Anel", false);
                    var ph = go.AddComponent<PortaHex>();
                    ph.laminas = Enumerable.Range(0, 6).Select(k => Copiar($"Porta_Hex_Lamina_{k}", false)?.transform).Where(x => x).ToArray();
                    ph.anel = anel ? anel.GetComponent<Renderer>() : null;
                    var bl = new GameObject("Bloqueio").AddComponent<BoxCollider>();
                    bl.transform.SetParent(go.transform, false);
                    bl.transform.localPosition = new Vector3(-2.6f, 1.85f, -L / 2); bl.size = new Vector3(0.25f, 3.6f, 3.6f);
                    ph.bloqueio = bl;
                    var placa = new GameObject("Placa_Sala").AddComponent<TextMeshPro>();
                    placa.transform.SetParent(go.transform, false);
                    placa.transform.localPosition = new Vector3(-2.40f, 4.3f, -L / 2);
                    placa.transform.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
                    placa.font = orb; placa.fontSize = 2.6f; placa.color = Color.white; placa.alignment = TextAlignmentOptions.Center;
                    placa.rectTransform.sizeDelta = new Vector2(2.5f, 0.45f); placa.textWrappingMode = TextWrappingModes.NoWrap;
                    placa.gameObject.AddComponent<AstroLab.Localization.LocalizedText>().Key = $"ct.sala.{md.sala}";
                    ph.placa = placa;
                    portas[md.sala] = ph;
                }
                if (md.i % 4 == 2 && md.tipo != "Seg_Fim")
                {
                    var l = new GameObject($"Luz_Corredor_{nLuz++:00}").AddComponent<Light>();
                    l.transform.SetParent(go.transform, false);
                    l.transform.localPosition = new Vector3(0, 3.6f, -L / 2);
                    l.type = LightType.Point; l.color = luzCor; l.intensity = 1.6f; l.range = 11f; l.shadows = LightShadows.None;
                }
            }
            Debug.Log($"[Centro] Corredor: {cam.modulos.Length} módulos, {portas.Count} portas hexagonais, {nLuz} luzes.");
            return portas;
        }

        public static readonly List<Transform> ModulosCorredor = new List<Transform>();

        [System.Serializable] class SalaDef { public string nome; public float[] min, max, entrada, dentro; public float altura; }
        [System.Serializable] class ListaSalas { public SalaDef[] salas; }

        static readonly string[] NaoMover = { "Poco", "Elevador", "Painel_Secreto", "Subsolo", "Recepcao", "Chao", "NPC_", "WP_", "SPAWN" };

        static void Salas(Dictionary<string, PortaHex> portas, TutorialCentro tut, System.Func<string, Transform> A)
        {
            string json = Raiz + "/Dados/Salas.json";
            if (!File.Exists(json) || ModulosCorredor.Count == 0) { Debug.LogWarning("[Centro] Salas: falta Salas.json ou corredor."); return; }
            var defs = JsonUtility.FromJson<ListaSalas>(File.ReadAllText(json)).salas;
            float L = 4f;
            // candidatos: tudo da cena que veio do modelo + letreiros, menos recepção/elevador/terreno
            var candidatos = new List<Transform>();
            foreach (var raizNome in new[] { "CentroTreinamento", "Letreiros" })
            {
                var r0 = GameObject.Find(raizNome);
                if (r0) foreach (Transform t in r0.transform) candidatos.Add(t);
            }
            Vector3 Centro(Transform t)
            {
                var r = t.GetComponent<Renderer>();
                return r && r.enabled ? r.bounds.center : (r ? r.bounds.center : t.position);
            }
            var raizes = new Dictionary<string, Transform>();
            foreach (var d in defs)
            {
                var mn = new Vector3(Mathf.Min(d.min[0], d.max[0]), -7f, Mathf.Min(d.min[2], d.max[2])) - new Vector3(0.6f, 0, 0.6f);
                var mx = new Vector3(Mathf.Max(d.min[0], d.max[0]), d.altura + 1f, Mathf.Max(d.min[2], d.max[2])) + new Vector3(0.6f, 0, 0.6f);
                var b = new Bounds(); b.SetMinMax(mn, mx);
                var raiz = new GameObject("Sala_" + d.nome).transform;
                raiz.SetPositionAndRotation(new Vector3(d.entrada[0], d.entrada[1], d.entrada[2]), Quaternion.LookRotation(new Vector3(d.dentro[0], d.dentro[1], d.dentro[2]), Vector3.up));
                int n = 0;
                foreach (var t in candidatos.ToList())
                {
                    if (t == null || t.parent == null || NaoMover.Any(k => t.name.Contains(k))) continue;
                    if (t.name.StartsWith("EST_0") || t.name.StartsWith("ZONA_EST_0")) continue;
                    if (!b.Contains(Centro(t))) continue;
                    t.SetParent(raiz, true); candidatos.Remove(t); n++;
                }
                raizes[d.nome] = raiz;
                // encaixe: entrada da sala = fim do portal da porta hexagonal (frente = para dentro da sala)
                if (portas.TryGetValue(d.nome, out var porta))
                {
                    var mod = porta.transform;
                    raiz.SetPositionAndRotation(mod.TransformPoint(new Vector3(-4.6f, 0f, -L / 2)), mod.rotation * Quaternion.LookRotation(Vector3.left, Vector3.up));
                    raiz.SetParent(mod.parent, true);
                }
                Debug.Log($"[Centro] Sala {d.nome}: {n} objetos encaixados.");
            }
            // conflitos: sala atravessando outra sala ou o corredor
            var bs = raizes.ToDictionary(kv => kv.Key, kv => { var rs = kv.Value.GetComponentsInChildren<Renderer>(); var bb = rs.Length > 0 ? rs[0].bounds : new Bounds(kv.Value.position, Vector3.one); foreach (var r in rs) if (!r.name.StartsWith("COL_")) bb.Encapsulate(r.bounds); return bb; });
            foreach (var a in bs) foreach (var c in bs) if (string.CompareOrdinal(a.Key, c.Key) < 0 && a.Value.Intersects(c.Value))
                        Debug.LogWarning($"[Centro] Salas {a.Key} e {c.Key} se sobrepõem (bounds).");
            foreach (var a in bs)
                foreach (var m in ModulosCorredor)
                {
                    var p = m.TransformPoint(new Vector3(0, 2f, -L / 2));
                    var inner = a.Value; inner.Expand(-1.5f);
                    if (inner.Contains(p)) { Debug.LogWarning($"[Centro] Sala {a.Key} cruza o corredor em {m.name}."); break; }
                }
            // rotas da instrutora no subsolo: vestíbulo -> módulos do corredor -> portal -> entrada da sala -> ponto da estação
            if (tut == null) return;
            int Indice(Transform mod) => int.Parse(mod.name.Substring(4, 2));
            int anterior = 0;
            Vector3 Centro2(Transform mod) => mod.TransformPoint(new Vector3(0, 0, -L / 2));
            var lista = tut.etapas.ToList();
            var ids = new[] { "Recepcao", "Medico", "Centrifuga", "RealidadeVirtual", "NBL", "ARGOS", "Trajes" };
            for (int i = 1; i < 7; i++)
            {
                var ponto = A($"EST_{i}_{ids[i]}_NPC");
                if (ponto == null || !portas.TryGetValue(ids[i], out var porta)) continue;
                var mod = porta.transform; int idx = Indice(mod);
                var rota = new List<Vector3>();
                for (int k = anterior; k <= idx; k++) rota.Add(Centro2(ModulosCorredor[k]));
                rota.Add(mod.TransformPoint(new Vector3(-3.6f, 0, -L / 2)));
                rota.Add(raizes[ids[i]].position + raizes[ids[i]].forward * 2.5f);
                rota.Add(ponto.position);
                var con = ponto.parent ? ponto.parent.GetComponentsInChildren<Interagivel>(true).FirstOrDefault(x => x.name.StartsWith($"EST_{i}_")) : null;
                lista.Add(new EtapaTreino { id = $"est{i}", pontoNpc = ponto, console = con, rota = rota.ToArray(), porta = porta });
                anterior = idx;
            }
            tut.etapas = lista.ToArray();
            Debug.Log($"[Centro] Tutorial: {tut.etapas.Length} etapas com rotas pelo corredor.");
        }

        static Mesh Losango()
        {
            string path = $"{Raiz}/Modelos/Marcador_Losango.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m != null) return m;
            m = new Mesh { name = "Marcador_Losango" };
            float r = 0.22f, h = 0.34f;
            var v = new List<Vector3>(); var tri = new List<int>();
            Vector3[] eq = { new Vector3(r, 0, 0), new Vector3(0, 0, r), new Vector3(-r, 0, 0), new Vector3(0, 0, -r) };
            for (int i = 0; i < 4; i++)
            {
                var a = eq[i]; var b = eq[(i + 1) % 4];
                foreach (var tip in new[] { new Vector3(0, h, 0), new Vector3(0, -h, 0) })
                {
                    int o = v.Count; v.Add(tip); v.Add(a); v.Add(b);
                    if (tip.y > 0) tri.AddRange(new[] { o, o + 2, o + 1 }); else tri.AddRange(new[] { o, o + 1, o + 2 });
                }
            }
            m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ================================================================== tudo
        [MenuItem("Centro de Treinamento/Fazer tudo (materiais + mãos + cena)")]
        public static void Tudo() { Materiais(); Maos(); MontarCena(); }

        // Build Settings é arquivo compartilhado com o chat do menu: só por menu explícito, nunca automático.
        [MenuItem("Centro de Treinamento/Adicionar cena ao Build (depois do menu)")]
        public static void AdicionarAoBuild()
        {
            var lista = EditorBuildSettings.scenes.ToList();
            if (lista.Any(s => s.path == CenaPath)) { Debug.Log("[Centro] Cena já está no Build."); return; }
            int idx = Mathf.Min(1, lista.Count);
            lista.Insert(idx, new EditorBuildSettingsScene(CenaPath, true));
            EditorBuildSettings.scenes = lista.ToArray();
            Debug.Log($"[Centro] Cena adicionada ao Build no índice {idx}.");
        }
    }
}
