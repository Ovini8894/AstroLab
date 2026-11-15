using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AstroLab.BrunoLaboratorio.Editor
{
    /// <summary>
    /// Gera uma sala de laboratorio espacial usando somente primitivas e recursos nativos.
    /// Nada e executado automaticamente: use o menu AstroLab > Bruno > Gerar LaboratorioBruno.
    /// </summary>
    public static class LaboratorioBrunoGenerator
    {
        private const string RootFolder = "Assets/BrunoLaboratorio";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string SceneFolder = RootFolder + "/Scenes";
        private const string ScenePath = SceneFolder + "/LaboratorioBruno.unity";

        private static readonly Color FloorColor = Html("#2D3545");
        private static readonly Color WallColor = Html("#424D62");
        private static readonly Color AlloyColor = Html("#B9D2E8");
        private static readonly Color SlateColor = Html("#5D738A");
        private static readonly Color PropColor = Html("#20304A");
        private static readonly Color ShadowColor = Html("#161D2B");
        private static readonly Color CyanColor = Html("#37D5D0");
        private static readonly Color AmberColor = Html("#FFB45C");

        private static readonly Dictionary<string, Material> Materials = new();

        [MenuItem("AstroLab/Bruno/Gerar LaboratorioBruno", priority = 10)]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog(
                    "LaboratorioBruno ja existe",
                    "Deseja reconstruir somente a cena e os materiais da pasta Assets/BrunoLaboratorio?",
                    "Reconstruir",
                    "Cancelar"))
            {
                return;
            }

            BuildAndSaveScene(true);
        }

        /// <summary>Entrada segura para automacao: recusa substituir uma cena com alteracoes nao salvas.</summary>
        public static void GenerateForAutomation()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("A cena ativa possui alteracoes nao salvas.");

            BuildAndSaveScene(false);
        }

        private static void BuildAndSaveScene(bool showCompletionDialog)
        {
            EnsureFolders();
            CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = Group("LaboratorioBruno");
            BuildArchitecture(root.transform);
            BuildCorridor(root.transform);
            BuildWindow(root.transform);
            BuildControlStations(root.transform);
            BuildCentralFeature(root.transform);
            BuildLighting(root.transform);
            Transform playerCameraTarget = BuildPlayer(root.transform);
            BuildCamera(root.transform, playerCameraTarget);
            BuildPostProcessing(root.transform);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Nao foi possivel salvar a cena em " + ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            if (showCompletionDialog)
            {
                EditorUtility.DisplayDialog(
                    "LaboratorioBruno criado",
                    "A cena foi salva em:\n" + ScenePath +
                    "\n\nUse o botao Play para visualizar pela camera ou navegue pela Scene View.",
                    "OK");
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "BrunoLaboratorio");
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder, "Profiles");
            EnsureFolder(RootFolder, "Scenes");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(fullPath))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void CreateMaterials()
        {
            Materials.Clear();
            CreateOrUpdateMaterial("Floor", FloorColor, 0.78f, 0.34f);
            CreateOrUpdateMaterial("Wall", WallColor, 0.72f, 0.3f);
            CreateOrUpdateMaterial("Alloy", AlloyColor, 0.9f, 0.65f);
            CreateOrUpdateMaterial("Slate", SlateColor, 0.76f, 0.38f);
            CreateOrUpdateMaterial("Prop", PropColor, 0.82f, 0.28f);
            CreateOrUpdateMaterial("Shadow", ShadowColor, 0.7f, 0.25f);
            CreateOrUpdateMaterial("CyanEmission", CyanColor, 0.55f, 0.45f, CyanColor * 5.5f);
            CreateOrUpdateMaterial("AmberEmission", AmberColor, 0.5f, 0.42f, AmberColor * 4.5f);
            CreateOrUpdateMaterial("Space", new Color(0.003f, 0.006f, 0.018f), 0f, 0f);
            CreateOrUpdateMaterial("Star", Color.white, 0f, 0f, new Color(5f, 6f, 8f));
        }

        private static void CreateOrUpdateMaterial(
            string name,
            Color color,
            float smoothness,
            float metallic,
            Color? emission = null)
        {
            string path = MaterialFolder + "/MAT_" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
                throw new InvalidOperationException("Shader URP/Lit nao encontrado. Confirme que o projeto esta usando URP.");

            if (material == null)
            {
                material = new Material(shader) { name = "MAT_" + name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            Materials[name] = material;
        }

        private static void BuildArchitecture(Transform parent)
        {
            Transform architecture = Group("01_Arquitetura", parent).transform;

            // Sala principal: 24 m x 18 m, altura util de 5 m.
            CreateBox("Piso_Base", new Vector3(0f, -0.25f, 0f), new Vector3(24f, 0.5f, 18f), "Floor", architecture);
            CreateBox("Teto", new Vector3(0f, 5.25f, 0f), new Vector3(24f, 0.5f, 18f), "Shadow", architecture);

            // Placas de piso deixam uma area central livre de 12 x 8 m.
            for (int x = -5; x <= 5; x++)
            {
                for (int z = -3; z <= 3; z++)
                {
                    CreateBox(
                        $"PlacaPiso_{x + 6:00}_{z + 4:00}",
                        new Vector3(x * 2f, 0.015f, z * 2f),
                        new Vector3(1.92f, 0.03f, 1.92f),
                        (x + z) % 2 == 0 ? "Floor" : "Prop",
                        architecture,
                        false);
                }
            }

            // Paredes laterais segmentadas, com pilares e rodapes.
            for (int z = -7; z <= 7; z += 2)
            {
                CreateBox($"Parede_Oeste_{z:+00;-00;00}", new Vector3(-12f, 2.5f, z), new Vector3(0.45f, 5f, 1.82f), "Wall", architecture);
                CreateBox($"Parede_Leste_{z:+00;-00;00}", new Vector3(12f, 2.5f, z), new Vector3(0.45f, 5f, 1.82f), "Wall", architecture);
            }

            for (int x = -10; x <= 10; x += 2)
            {
                if (Mathf.Abs(x) > 2)
                    CreateBox($"Parede_Sul_{x:+00;-00;00}", new Vector3(x, 2.5f, -9f), new Vector3(1.82f, 5f, 0.45f), "Wall", architecture);
            }

            // Parede norte contorna a janela panoramica central.
            CreateBox("ParedeNorte_Esquerda", new Vector3(-8.5f, 2.5f, 9f), new Vector3(7f, 5f, 0.45f), "Wall", architecture);
            CreateBox("ParedeNorte_Direita", new Vector3(8.5f, 2.5f, 9f), new Vector3(7f, 5f, 0.45f), "Wall", architecture);
            CreateBox("ParedeNorte_Baixo", new Vector3(0f, 0.65f, 9f), new Vector3(10f, 1.3f, 0.45f), "Wall", architecture);
            CreateBox("ParedeNorte_Cima", new Vector3(0f, 4.65f, 9f), new Vector3(10f, 0.7f, 0.45f), "Wall", architecture);

            CreateBox("Rodape_Oeste", new Vector3(-11.7f, 0.3f, 0f), new Vector3(0.25f, 0.6f, 17.5f), "Slate", architecture);
            CreateBox("Rodape_Leste", new Vector3(11.7f, 0.3f, 0f), new Vector3(0.25f, 0.6f, 17.5f), "Slate", architecture);

            // Vigas do teto reforcam a escala industrial.
            for (int z = -6; z <= 6; z += 3)
                CreateBox($"VigaTeto_{z:+00;-00;00}", new Vector3(0f, 4.88f, z), new Vector3(23.5f, 0.22f, 0.28f), "Alloy", architecture, false);

            CreateLightStripRow(architecture, -11.55f, 4.35f, true);
            CreateLightStripRow(architecture, 11.55f, 4.35f, true);
        }

        private static void BuildCorridor(Transform parent)
        {
            Transform corridor = Group("02_EntradaECorredor", parent).transform;
            const float corridorZ = -14f;

            CreateBox("Piso_Corredor", new Vector3(0f, -0.2f, corridorZ), new Vector3(4.6f, 0.4f, 10f), "Floor", corridor);
            CreateBox("Parede_Corredor_E", new Vector3(-2.3f, 2.4f, corridorZ), new Vector3(0.35f, 4.8f, 10f), "Wall", corridor);
            CreateBox("Parede_Corredor_D", new Vector3(2.3f, 2.4f, corridorZ), new Vector3(0.35f, 4.8f, 10f), "Wall", corridor);
            CreateBox("Teto_Corredor", new Vector3(0f, 4.8f, corridorZ), new Vector3(4.6f, 0.3f, 10f), "Shadow", corridor);

            CreateBox("Portal_Esquerdo", new Vector3(-2.1f, 2.4f, -8.75f), new Vector3(0.55f, 4.8f, 0.8f), "Alloy", corridor);
            CreateBox("Portal_Direito", new Vector3(2.1f, 2.4f, -8.75f), new Vector3(0.55f, 4.8f, 0.8f), "Alloy", corridor);
            CreateBox("Portal_Superior", new Vector3(0f, 4.35f, -8.75f), new Vector3(4.6f, 0.8f, 0.8f), "Alloy", corridor);
            CreateBox("LuzPortal", new Vector3(0f, 4.12f, -8.3f), new Vector3(3.4f, 0.12f, 0.08f), "CyanEmission", corridor, false);

            for (int z = -18; z <= -10; z += 2)
                CreateBox($"LuzCorredor_{z}", new Vector3(0f, 4.58f, z), new Vector3(1.6f, 0.08f, 0.22f), "CyanEmission", corridor, false);
        }

        private static void BuildWindow(Transform parent)
        {
            Transform window = Group("03_JanelaPanoramica", parent).transform;

            CreateBox("Espaco_Exterior", new Vector3(0f, 2.8f, 9.45f), new Vector3(9.6f, 3.5f, 0.08f), "Space", window, false);
            CreateBox("Moldura_E", new Vector3(-5.05f, 2.8f, 8.73f), new Vector3(0.35f, 3.8f, 0.45f), "Alloy", window);
            CreateBox("Moldura_D", new Vector3(5.05f, 2.8f, 8.73f), new Vector3(0.35f, 3.8f, 0.45f), "Alloy", window);
            CreateBox("Moldura_Cima", new Vector3(0f, 4.65f, 8.73f), new Vector3(10.4f, 0.3f, 0.45f), "Alloy", window);
            CreateBox("Moldura_Baixo", new Vector3(0f, 0.95f, 8.73f), new Vector3(10.4f, 0.3f, 0.45f), "Alloy", window);
            CreateBox("Divisoria_E", new Vector3(-1.7f, 2.8f, 8.68f), new Vector3(0.12f, 3.5f, 0.25f), "Slate", window, false);
            CreateBox("Divisoria_D", new Vector3(1.7f, 2.8f, 8.68f), new Vector3(0.12f, 3.5f, 0.25f), "Slate", window, false);

            // Campo de estrelas deterministico, sem texturas externas.
            var random = new System.Random(4187);
            for (int i = 0; i < 42; i++)
            {
                float x = Mathf.Lerp(-4.6f, 4.6f, (float)random.NextDouble());
                float y = Mathf.Lerp(1.2f, 4.35f, (float)random.NextDouble());
                float size = Mathf.Lerp(0.018f, 0.065f, (float)random.NextDouble());
                CreateSphere($"Estrela_{i:00}", new Vector3(x, y, 9.36f), Vector3.one * size, "Star", window, false);
            }

            // Planeta distante estilizado para dar profundidade a vista.
            CreateSphere("Planeta_Distante", new Vector3(3.5f, 1.65f, 9.25f), Vector3.one * 1.15f, "Slate", window, false);
            CreateSphere("Planeta_Sombra", new Vector3(3.85f, 1.85f, 9.12f), Vector3.one * 1.05f, "Shadow", window, false);
        }

        private static void BuildControlStations(Transform parent)
        {
            Transform controls = Group("04_PaineisDeControle", parent).transform;

            CreateConsoleBank("Console_Oeste", new Vector3(-9.8f, 0f, 1.5f), Quaternion.Euler(0f, 90f, 0f), controls, false);
            CreateConsoleBank("Console_Leste", new Vector3(9.8f, 0f, 1.5f), Quaternion.Euler(0f, -90f, 0f), controls, true);
            CreateConsoleBank("Console_Janela_E", new Vector3(-3.2f, 0f, 6.8f), Quaternion.identity, controls, false);
            CreateConsoleBank("Console_Janela_D", new Vector3(3.2f, 0f, 6.8f), Quaternion.identity, controls, true);

            // Gabinetes e computadores nas laterais, fora da circulacao central.
            for (int i = 0; i < 3; i++)
            {
                float z = -5f + i * 3.2f;
                CreateServerRack($"Servidor_{i + 1}", new Vector3(-10.7f, 0f, z), controls, i == 1);
            }
        }

        private static void CreateConsoleBank(string name, Vector3 position, Quaternion rotation, Transform parent, bool amber)
        {
            GameObject bank = Group(name, parent);
            bank.transform.SetPositionAndRotation(position, rotation);

            CreateBox("Base", new Vector3(0f, 0.55f, 0f), new Vector3(3.2f, 1.1f, 1.15f), "Prop", bank.transform);
            CreateBox("Tampo", new Vector3(0f, 1.18f, -0.12f), new Vector3(3.25f, 0.12f, 1.35f), "Alloy", bank.transform);
            CreateBox("PainelInclinado", new Vector3(0f, 1.62f, 0.28f), new Vector3(2.85f, 0.85f, 0.12f), "Shadow", bank.transform, false, Quaternion.Euler(-18f, 0f, 0f));

            for (int i = -2; i <= 2; i++)
            {
                string material = amber && i == 2 ? "AmberEmission" : "CyanEmission";
                CreateBox($"Indicador_{i + 3}", new Vector3(i * 0.48f, 1.63f, 0.205f), new Vector3(0.3f, 0.28f, 0.035f), material, bank.transform, false, Quaternion.Euler(-18f, 0f, 0f));
            }
        }

        private static void CreateServerRack(string name, Vector3 position, Transform parent, bool amber)
        {
            GameObject rack = Group(name, parent);
            rack.transform.position = position;
            CreateBox("Gabinete", new Vector3(0f, 1.35f, 0f), new Vector3(1.25f, 2.7f, 1.1f), "Prop", rack.transform);
            CreateBox("Moldura", new Vector3(0f, 1.35f, 0.58f), new Vector3(1.05f, 2.35f, 0.08f), "Shadow", rack.transform, false);
            for (int i = 0; i < 6; i++)
                CreateBox($"Modulo_{i:00}", new Vector3(0f, 0.42f + i * 0.36f, 0.63f), new Vector3(0.82f, 0.11f, 0.04f), amber && i == 4 ? "AmberEmission" : "CyanEmission", rack.transform, false);
        }

        private static void BuildCentralFeature(Transform parent)
        {
            Transform central = Group("05_NucleoCentral", parent).transform;
            CreateCylinder("Plataforma", new Vector3(0f, 0.14f, 0f), new Vector3(2.4f, 0.14f, 2.4f), "Slate", central);
            CreateCylinder("AnelLuminoso", new Vector3(0f, 0.3f, 0f), new Vector3(1.85f, 0.04f, 1.85f), "CyanEmission", central, false);
            CreateCylinder("MesaHolografica", new Vector3(0f, 0.82f, 0f), new Vector3(0.9f, 0.52f, 0.9f), "Prop", central);
            CreateCylinder("Projetor", new Vector3(0f, 1.4f, 0f), new Vector3(0.5f, 0.08f, 0.5f), "Alloy", central);

            GameObject hologram = CreateSphere("Holograma", new Vector3(0f, 2.15f, 0f), new Vector3(0.95f, 0.95f, 0.95f), "CyanEmission", central, false);
            hologram.transform.localScale = new Vector3(0.95f, 0.55f, 0.95f);

            // Marcadores discretos delimitam a area livre para caminhada.
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreateBox(
                        $"Marcador_{x}_{z}",
                        new Vector3(x * 5.5f, 0.04f, z * 3.8f),
                        new Vector3(0.7f, 0.04f, 0.12f),
                        "AmberEmission",
                        central,
                        false,
                        Quaternion.Euler(0f, x * z > 0 ? 45f : -45f, 0f));
                }
            }
        }

        private static void BuildLighting(Transform parent)
        {
            Transform lighting = Group("06_Iluminacao", parent).transform;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.15f, 0.22f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.14f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.035f, 0.055f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.025f, 0.055f, 0.085f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;

            CreateLight("LuzPrincipalFria", LightType.Directional, new Vector3(0f, 4.5f, -2f), Quaternion.Euler(48f, -32f, 0f), new Color(0.68f, 0.82f, 1f), 0.72f, 0f, lighting);
            CreateLight("LuzCentralCiano", LightType.Point, new Vector3(0f, 3.8f, 0f), Quaternion.identity, CyanColor, 8.5f, 13f, lighting);
            CreateLight("LuzJanelaAzul", LightType.Point, new Vector3(0f, 3f, 6.8f), Quaternion.identity, new Color(0.25f, 0.55f, 1f), 5.5f, 11f, lighting);
            CreateLight("LuzEntradaAmbar", LightType.Point, new Vector3(0f, 2.8f, -7f), Quaternion.identity, AmberColor, 3.2f, 7f, lighting);

            for (int z = -5; z <= 5; z += 5)
            {
                CreateLight($"LuzTeto_O_{z}", LightType.Point, new Vector3(-7.5f, 4.3f, z), Quaternion.identity, new Color(0.3f, 0.8f, 1f), 3.2f, 7f, lighting);
                CreateLight($"LuzTeto_L_{z}", LightType.Point, new Vector3(7.5f, 4.3f, z), Quaternion.identity, new Color(0.3f, 0.8f, 1f), 3.2f, 7f, lighting);
            }
        }

        private static Transform BuildPlayer(Transform parent)
        {
            Transform gameplay = Group("08_Jogabilidade", parent).transform;
            GameObject player = Group("Jogador", gameplay);
            player.transform.position = new Vector3(0f, 0.02f, -4.8f);
            player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            player.tag = "Player";

            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.36f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.stepOffset = 0.3f;
            characterController.slopeLimit = 48f;
            characterController.skinWidth = 0.04f;
            characterController.minMoveDistance = 0f;

            PlayerInput playerInput = player.AddComponent<PlayerInput>();
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (inputActions == null)
                throw new InvalidOperationException("O Input System existente nao foi encontrado em Assets/InputSystem_Actions.inputactions.");
            playerInput.actions = inputActions;
            playerInput.defaultActionMap = "Player";
            playerInput.neverAutoSwitchControlSchemes = false;

            GameObject visual = Group("Visual_Capsula", player.transform);
            visual.transform.localPosition = Vector3.zero;

            GameObject body = CreateCapsule("Corpo", new Vector3(0f, 0.9f, 0f), new Vector3(0.72f, 0.9f, 0.72f), "Alloy", visual.transform, false);
            body.transform.localRotation = Quaternion.identity;
            CreateBox("Visor", new Vector3(0f, 1.18f, 0.325f), new Vector3(0.42f, 0.22f, 0.06f), "CyanEmission", visual.transform, false);
            CreateBox("Mochila", new Vector3(0f, 0.95f, -0.37f), new Vector3(0.48f, 0.72f, 0.22f), "Prop", visual.transform, false);
            CreateBox("LuzMochila", new Vector3(0f, 1.12f, -0.495f), new Vector3(0.22f, 0.12f, 0.03f), "AmberEmission", visual.transform, false);

            GameObject cameraTarget = Group("AlvoCamera", player.transform);
            cameraTarget.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            cameraTarget.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            var controller = player.AddComponent<AstroLab.BrunoLaboratorio.LaboratorioPlayerController>();
            controller.Configure(cameraTarget.transform);
            var motion = player.AddComponent<AstroLab.BrunoLaboratorio.LaboratorioPlayerMotion>();
            motion.Configure(controller, visual.transform);

            CreateBox("PontoInicial", new Vector3(0f, 0.025f, -4.8f), new Vector3(1.3f, 0.025f, 1.3f), "CyanEmission", gameplay, false);
            return cameraTarget.transform;
        }

        private static void BuildCamera(Transform parent, Transform playerCameraTarget)
        {
            GameObject cameraObject = new("Camera_LaboratorioBruno");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(0f, 4.4f, -13.8f);
            cameraObject.transform.rotation = LookAtRotation(cameraObject.transform.position, new Vector3(0f, 1.8f, 1.5f));
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.allowHDR = true;
            camera.backgroundColor = ShadowColor;
            camera.clearFlags = CameraClearFlags.SolidColor;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CinemachineBrain>();

            GameObject virtualCameraObject = new("CinemachineCamera_3aPessoa");
            virtualCameraObject.transform.SetParent(parent);
            CinemachineCamera virtualCamera = virtualCameraObject.AddComponent<CinemachineCamera>();
            virtualCamera.Follow = playerCameraTarget;
            virtualCamera.Lens.FieldOfView = 58f;

            CinemachineThirdPersonFollow follow = virtualCameraObject.AddComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset = new Vector3(0.55f, 0.25f, 0f);
            follow.VerticalArmLength = 0.55f;
            follow.CameraSide = 0.72f;
            follow.CameraDistance = 4.4f;
            follow.Damping = new Vector3(0.35f, 0.55f, 0.45f);
        }

        private static void BuildPostProcessing(Transform parent)
        {
            string profilePath = ProfileFolder + "/LaboratorioBrunoVolume.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "LaboratorioBrunoVolume";
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            else
            {
                foreach (VolumeComponent component in new List<VolumeComponent>(profile.components))
                    profile.Remove(component.GetType());
            }

            Bloom bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.intensity.Override(0.36f);
            bloom.threshold.Override(1.05f);

            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            Vignette vignette = profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.42f);

            ColorAdjustments adjustments = profile.Add<ColorAdjustments>();
            adjustments.active = true;
            adjustments.saturation.Override(8f);
            adjustments.contrast.Override(6f);

            EditorUtility.SetDirty(profile);

            GameObject volumeObject = Group("07_PosProcessamento", parent);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
        }

        private static void CreateLightStripRow(Transform parent, float x, float y, bool cyan)
        {
            for (int z = -6; z <= 6; z += 3)
                CreateBox($"FaixaLuz_{x}_{z}", new Vector3(x, y, z), new Vector3(0.08f, 0.16f, 1.8f), cyan ? "CyanEmission" : "AmberEmission", parent, false);
        }

        private static Light CreateLight(
            string name,
            LightType type,
            Vector3 position,
            Quaternion rotation,
            Color color,
            float intensity,
            float range,
            Transform parent)
        {
            GameObject gameObject = new(name);
            gameObject.transform.SetParent(parent);
            gameObject.transform.SetPositionAndRotation(position, rotation);
            Light light = gameObject.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = type == LightType.Directional ? LightShadows.Soft : LightShadows.None;
            return light;
        }

        private static GameObject CreateBox(
            string name,
            Vector3 position,
            Vector3 scale,
            string material,
            Transform parent,
            bool collider = true,
            Quaternion? rotation = null)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, position, scale, material, parent, collider, rotation ?? Quaternion.identity);
        }

        private static GameObject CreateSphere(
            string name,
            Vector3 position,
            Vector3 scale,
            string material,
            Transform parent,
            bool collider = true)
        {
            return CreatePrimitive(PrimitiveType.Sphere, name, position, scale, material, parent, collider, Quaternion.identity);
        }

        private static GameObject CreateCylinder(
            string name,
            Vector3 position,
            Vector3 scale,
            string material,
            Transform parent,
            bool collider = true)
        {
            return CreatePrimitive(PrimitiveType.Cylinder, name, position, scale, material, parent, collider, Quaternion.identity);
        }

        private static GameObject CreateCapsule(
            string name,
            Vector3 position,
            Vector3 scale,
            string material,
            Transform parent,
            bool collider = true)
        {
            return CreatePrimitive(PrimitiveType.Capsule, name, position, scale, material, parent, collider, Quaternion.identity);
        }

        private static GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            string material,
            Transform parent,
            bool collider,
            Quaternion localRotation)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localRotation = localRotation;
            gameObject.transform.localScale = localScale;

            Renderer renderer = gameObject.GetComponent<Renderer>();
            renderer.sharedMaterial = Materials[material];

            if (!collider)
            {
                Collider existingCollider = gameObject.GetComponent<Collider>();
                if (existingCollider != null)
                    UnityEngine.Object.DestroyImmediate(existingCollider);
            }

            return gameObject;
        }

        private static GameObject Group(string name, Transform parent = null)
        {
            GameObject gameObject = new(name);
            if (parent != null)
                gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Quaternion LookAtRotation(Vector3 origin, Vector3 target)
        {
            return Quaternion.LookRotation((target - origin).normalized, Vector3.up);
        }

        private static Color Html(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out Color color))
                throw new ArgumentException("Cor HTML invalida: " + value);
            return color;
        }
    }
}
