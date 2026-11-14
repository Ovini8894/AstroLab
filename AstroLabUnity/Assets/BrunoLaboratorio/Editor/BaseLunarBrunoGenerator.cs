using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AstroLab.BrunoLaboratorio.Editor
{
    public static class BaseLunarBrunoGenerator
    {
        private const string RootFolder = "Assets/BrunoLaboratorio";
        private const string LunarFolder = RootFolder + "/Lunar";
        private const string LunarMaterialFolder = LunarFolder + "/Materials";
        private const string LunarDataFolder = LunarFolder + "/Terrain";
        private const string SceneFolder = RootFolder + "/Scenes";
        private const string LunarScenePath = SceneFolder + "/BaseLunarBruno.unity";

        private static readonly Color LunarSoil = Html("#777B82");
        private static readonly Color LunarDark = Html("#30343B");
        private static readonly Color LunarRock = Html("#555A62");
        private static readonly Color EarthBlue = Html("#2A78D2");
        private static readonly Color EarthCloud = Html("#D8F1FF");
        private static readonly Color Cyan = Html("#37D5D0");
        private static readonly Dictionary<string, Material> Materials = new();

        [MenuItem("AstroLab/Bruno/Gerar Base Lunar Integrada", priority = 11)]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LunarScenePath) != null &&
                !EditorUtility.DisplayDialog(
                    "BaseLunarBruno ja existe",
                    "Deseja reconstruir somente a cena lunar e os recursos em Assets/BrunoLaboratorio?",
                    "Reconstruir",
                    "Cancelar"))
            {
                return;
            }

            BuildAndSave(true);
        }

        public static void GenerateForAutomation()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("A cena ativa possui alteracoes nao salvas.");

            BuildAndSave(false);
        }

        private static void BuildAndSave(bool showCompletionDialog)
        {
            // Reconstroi a origem conhecida para que a cena lunar sempre tenha um unico jogador e uma unica camera.
            LaboratorioBrunoGenerator.GenerateForAutomation();
            EnsureFolders();
            CreateMaterials();

            Scene scene = EditorSceneManager.GetActiveScene();
            GameObject laboratoryRoot = GameObject.Find("LaboratorioBruno");
            if (laboratoryRoot == null)
                throw new InvalidOperationException("O laboratorio base nao foi encontrado.");

            Transform lunarRoot = Group("BaseLunar_Exterior", laboratoryRoot.transform).transform;
            BuildTerrain(lunarRoot);
            BuildAirlockExit(lunarRoot);
            BuildRocksAndCraters(lunarRoot);
            BuildSkyAndEarth(lunarRoot);
            BuildLunarLighting(lunarRoot);
            BuildBoundaries(lunarRoot);
            BuildGravityZone(lunarRoot);

            if (!EditorSceneManager.SaveScene(scene, LunarScenePath))
                throw new InvalidOperationException("Nao foi possivel salvar a cena em " + LunarScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = lunarRoot.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();

            if (showCompletionDialog)
            {
                EditorUtility.DisplayDialog(
                    "Base lunar criada",
                    "A cena integrada foi salva em:\n" + LunarScenePath +
                    "\n\nPressione Play para explorar o laboratorio e a superficie lunar.",
                    "OK");
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder(RootFolder, "Lunar");
            EnsureFolder(LunarFolder, "Materials");
            EnsureFolder(LunarFolder, "Terrain");
            EnsureFolder(RootFolder, "Scenes");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void CreateMaterials()
        {
            Materials.Clear();
            Materials["Rock"] = CreateMaterial("MAT_LunarRock", LunarRock, 0.12f, 0.05f);
            Materials["Crater"] = CreateMaterial("MAT_CraterDark", LunarDark, 0.08f, 0.02f);
            Materials["Rail"] = CreateMaterial("MAT_BoundaryRail", LunarDark, 0.3f, 0.5f);
            Materials["Earth"] = CreateMaterial("MAT_Earth", EarthBlue, 0.55f, 0.1f, EarthBlue * 1.4f);
            Materials["EarthCloud"] = CreateMaterial("MAT_EarthCloud", EarthCloud, 0.35f, 0f, EarthCloud * 1.1f);
            Materials["Cyan"] = AssetDatabase.LoadAssetAtPath<Material>(RootFolder + "/Materials/MAT_CyanEmission.mat");
            Materials["Alloy"] = AssetDatabase.LoadAssetAtPath<Material>(RootFolder + "/Materials/MAT_Alloy.mat");
            Materials["Star"] = AssetDatabase.LoadAssetAtPath<Material>(RootFolder + "/Materials/MAT_Star.mat");
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, float metallic, Color? emission = null)
        {
            string path = LunarMaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Shader URP/Lit nao encontrado.");

            if (material == null)
            {
                material = new Material(shader) { name = name };
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
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildTerrain(Transform parent)
        {
            const int resolution = 129;
            const float width = 100f;
            const float length = 100f;
            const float heightScale = 4f;
            string dataPath = LunarDataFolder + "/BaseLunarTerrain.asset";

            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (terrainData == null)
            {
                terrainData = new TerrainData { name = "BaseLunarTerrain" };
                AssetDatabase.CreateAsset(terrainData, dataPath);
            }

            terrainData.heightmapResolution = resolution;
            terrainData.size = new Vector3(width, heightScale, length);
            float[,] heights = new float[resolution, resolution];

            var craters = new[]
            {
                new Vector3(-28f, -52f, 7f), new Vector3(21f, -68f, 9f),
                new Vector3(-12f, -82f, 5f), new Vector3(34f, -35f, 4f),
                new Vector3(-39f, -25f, 3.5f), new Vector3(9f, -43f, 3f)
            };

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = Mathf.Lerp(-50f, 50f, x / (resolution - 1f));
                    float worldZ = Mathf.Lerp(-100f, 0f, z / (resolution - 1f));
                    float elevation = (Mathf.PerlinNoise((worldX + 130f) * 0.032f, (worldZ + 170f) * 0.032f) - 0.5f) * 0.32f;
                    elevation += (Mathf.PerlinNoise((worldX + 19f) * 0.09f, (worldZ + 27f) * 0.09f) - 0.5f) * 0.08f;

                    foreach (Vector3 crater in craters)
                    {
                        float distance = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(crater.x, crater.y));
                        float radius = crater.z;
                        if (distance < radius)
                        {
                            float normalized = distance / radius;
                            float bowl = -(1f - normalized * normalized) * 0.55f;
                            float rim = Mathf.Exp(-Mathf.Pow((normalized - 0.88f) * 8f, 2f)) * 0.22f;
                            elevation += bowl + rim;
                        }
                    }

                    // A rota da eclusa fica plana e alinhada ao piso interno.
                    float routeBlend = Mathf.Clamp01((Mathf.Abs(worldX) - 3f) / 4f);
                    if (worldZ > -31f)
                        elevation = Mathf.Lerp(0f, elevation, routeBlend);

                    heights[z, x] = Mathf.Clamp01((1f + elevation) / heightScale);
                }
            }

            terrainData.SetHeights(0, 0, heights);
            terrainData.terrainLayers = new[] { CreateTerrainLayer() };
            EditorUtility.SetDirty(terrainData);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "Terreno_Lunar_100x100";
            terrainObject.transform.SetParent(parent);
            terrainObject.transform.position = new Vector3(-50f, -1f, -100f);
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 150f;
        }

        private static TerrainLayer CreateTerrainLayer()
        {
            string texturePath = LunarDataFolder + "/LunarSoilTexture.asset";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(8, 8, TextureFormat.RGBA32, true) { name = "LunarSoilTexture" };
                var random = new System.Random(721);
                Color[] pixels = new Color[64];
                for (int i = 0; i < pixels.Length; i++)
                {
                    float variation = Mathf.Lerp(0.86f, 1.08f, (float)random.NextDouble());
                    pixels[i] = LunarSoil * variation;
                    pixels[i].a = 1f;
                }
                texture.SetPixels(pixels);
                texture.Apply(true, false);
                AssetDatabase.CreateAsset(texture, texturePath);
            }

            string layerPath = LunarDataFolder + "/LunarSoilLayer.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer { name = "LunarSoilLayer" };
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(6f, 6f);
            layer.smoothness = 0.08f;
            layer.metallic = 0.02f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void BuildAirlockExit(Transform parent)
        {
            Transform exit = Group("Saida_Eclusa_Exterior", parent).transform;
            CreateBox("Passarela", new Vector3(0f, -0.05f, -24f), new Vector3(4.2f, 0.18f, 10f), "Alloy", exit);
            CreateBox("Corrimao_E", new Vector3(-2f, 0.55f, -24f), new Vector3(0.12f, 1.1f, 10f), "Rail", exit);
            CreateBox("Corrimao_D", new Vector3(2f, 0.55f, -24f), new Vector3(0.12f, 1.1f, 10f), "Rail", exit);
            for (int z = -20; z >= -28; z -= 2)
            {
                CreateBox($"Baliza_E_{z}", new Vector3(-1.8f, 0.18f, z), new Vector3(0.14f, 0.12f, 0.35f), "Cyan", exit, false);
                CreateBox($"Baliza_D_{z}", new Vector3(1.8f, 0.18f, z), new Vector3(0.14f, 0.12f, 0.35f), "Cyan", exit, false);
            }
        }

        private static void BuildRocksAndCraters(Transform parent)
        {
            Transform geology = Group("RochasECrateras", parent).transform;
            var random = new System.Random(9041);
            for (int i = 0; i < 48; i++)
            {
                float x;
                float z;
                do
                {
                    x = Mathf.Lerp(-46f, 46f, (float)random.NextDouble());
                    z = Mathf.Lerp(-95f, -24f, (float)random.NextDouble());
                } while (Mathf.Abs(x) < 5f && z > -34f);

                float size = Mathf.Lerp(0.25f, 1.25f, (float)random.NextDouble());
                GameObject rock = CreateSphere($"Rocha_{i:00}", new Vector3(x, size * 0.35f, z), new Vector3(size, size * 0.65f, size * 0.82f), "Rock", geology);
                rock.transform.rotation = Quaternion.Euler(
                    Mathf.Lerp(-12f, 12f, (float)random.NextDouble()),
                    Mathf.Lerp(0f, 360f, (float)random.NextDouble()),
                    Mathf.Lerp(-12f, 12f, (float)random.NextDouble()));
            }

            var craterMarkers = new[]
            {
                new Vector3(-28f, 0.08f, -52f), new Vector3(21f, 0.08f, -68f),
                new Vector3(-12f, 0.08f, -82f), new Vector3(34f, 0.08f, -35f)
            };
            for (int i = 0; i < craterMarkers.Length; i++)
                CreateCylinder($"CentroCratera_{i:00}", craterMarkers[i], new Vector3(1.1f, 0.025f, 1.1f), "Crater", geology, false);
        }

        private static void BuildSkyAndEarth(Transform parent)
        {
            Transform sky = Group("CeuEspacial", parent).transform;
            Material skybox = CreateSkybox();
            RenderSettings.skybox = skybox;
            RenderSettings.fog = false;

            var random = new System.Random(1969);
            for (int i = 0; i < 120; i++)
            {
                float angle = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble());
                float radius = Mathf.Lerp(62f, 88f, (float)random.NextDouble());
                float y = Mathf.Lerp(10f, 45f, (float)random.NextDouble());
                float size = Mathf.Lerp(0.035f, 0.16f, (float)random.NextDouble());
                CreateSphere($"EstrelaLunar_{i:000}", new Vector3(Mathf.Cos(angle) * radius, y, -48f + Mathf.Sin(angle) * radius), Vector3.one * size, "Star", sky, false);
            }

            GameObject earth = CreateSphere("Terra_No_Horizonte", new Vector3(36f, 15f, -92f), Vector3.one * 6.5f, "Earth", sky, false);
            CreateSphere("Nuvens_Da_Terra", earth.transform.position + new Vector3(-0.7f, 0.8f, -0.8f), new Vector3(5.9f, 2.1f, 0.5f), "EarthCloud", sky, false);
        }

        private static Material CreateSkybox()
        {
            string path = LunarMaterialFolder + "/MAT_LunarSkybox.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Skybox/Procedural");
            if (material == null)
            {
                material = new Material(shader) { name = "MAT_LunarSkybox" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_SkyTint", new Color(0.002f, 0.004f, 0.012f));
            material.SetColor("_GroundColor", new Color(0.002f, 0.003f, 0.008f));
            material.SetFloat("_AtmosphereThickness", 0f);
            material.SetFloat("_Exposure", 0.18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildLunarLighting(Transform parent)
        {
            Transform lights = Group("IluminacaoLunar", parent).transform;
            GameObject sunObject = Group("Sol_Lunar", lights);
            sunObject.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.82f, 0.9f, 1f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.92f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.055f, 0.065f, 0.09f);
            RenderSettings.ambientEquatorColor = new Color(0.025f, 0.03f, 0.045f);
            RenderSettings.ambientGroundColor = new Color(0.012f, 0.014f, 0.02f);
        }

        private static void BuildBoundaries(Transform parent)
        {
            Transform boundaries = Group("LimitesDoMapa", parent).transform;
            CreateBoundary("Limite_Oeste", new Vector3(-49.5f, 1.25f, -50f), new Vector3(1f, 2.5f, 100f), boundaries);
            CreateBoundary("Limite_Leste", new Vector3(49.5f, 1.25f, -50f), new Vector3(1f, 2.5f, 100f), boundaries);
            CreateBoundary("Limite_Sul", new Vector3(0f, 1.25f, -99.5f), new Vector3(100f, 2.5f, 1f), boundaries);
            CreateBoundary("Limite_Norte", new Vector3(0f, 1.25f, -0.5f), new Vector3(100f, 2.5f, 1f), boundaries);

            for (int x = -45; x <= 45; x += 10)
                CreateBox($"BalizaSul_{x}", new Vector3(x, 0.35f, -97.8f), new Vector3(0.12f, 0.7f, 0.12f), "Cyan", boundaries, false);
        }

        private static void CreateBoundary(string name, Vector3 position, Vector3 size, Transform parent)
        {
            GameObject boundary = Group(name, parent);
            boundary.transform.position = position;
            BoxCollider collider = boundary.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private static void BuildGravityZone(Transform parent)
        {
            GameObject zoneObject = Group("Zona_Gravidade_Lunar_1_62", parent);
            zoneObject.transform.position = new Vector3(0f, 20f, -59.5f);
            BoxCollider collider = zoneObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(98f, 50f, 81f);
            zoneObject.AddComponent<AstroLab.BrunoLaboratorio.LunarGravityZone>();
        }

        private static GameObject CreateBox(string name, Vector3 position, Vector3 scale, string material, Transform parent, bool collider = true)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, position, scale, material, parent, collider);
        }

        private static GameObject CreateSphere(string name, Vector3 position, Vector3 scale, string material, Transform parent, bool collider = true)
        {
            return CreatePrimitive(PrimitiveType.Sphere, name, position, scale, material, parent, collider);
        }

        private static GameObject CreateCylinder(string name, Vector3 position, Vector3 scale, string material, Transform parent, bool collider = true)
        {
            return CreatePrimitive(PrimitiveType.Cylinder, name, position, scale, material, parent, collider);
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, string material, Transform parent, bool collider)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = Materials[material];
            if (!collider)
            {
                Collider existing = gameObject.GetComponent<Collider>();
                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing);
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

        private static Color Html(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out Color color))
                throw new ArgumentException("Cor HTML invalida: " + value);
            return color;
        }
    }
}
