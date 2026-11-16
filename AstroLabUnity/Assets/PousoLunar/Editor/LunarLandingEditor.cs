using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

namespace AstroLab.PousoLunar.Editor
{
    [CustomEditor(typeof(LunarLandingController))]
    public sealed class LunarLandingInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var rig = (LunarLandingController)target;
            EditorGUILayout.LabelField("Estado", rig.State.ToString());
            EditorGUILayout.LabelField("Tempo", rig.Elapsed.ToString("F2") + " s");
            if (!rig.ValidateConfiguration(out string error)) EditorGUILayout.HelpBox(error,MessageType.Error);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Iniciar pouso")) rig.BeginLanding();
                if (GUILayout.Button("Reiniciar pouso")) rig.RestartLanding();
            }
            if (GUILayout.Button("Resetar posição"))
            {
                if (!Application.isPlaying && rig.motionRoot) Undo.RecordObject(rig.motionRoot,"Resetar pouso");
                rig.ResetLanding(); SceneView.RepaintAll();
            }
            if (GUILayout.Button("Selecionar ponto de pouso") && rig.landingPoint) Selection.activeTransform = rig.landingPoint;
            if (Application.isPlaying) Repaint();
        }
    }

    public static class LunarLandingBuilder
    {
        public const string Root = "Assets/PousoLunar";
        public const string PrefabPath = Root + "/Prefabs/RocketLandingRoot.prefab";

        [MenuItem("AstroLab/Pouso Lunar/Criar demonstração isolada")]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(LunarLandingTransition.LandingScene) || File.Exists(PrefabPath))
            {
                Debug.LogWarning("Demonstração já existe. Abra a cena existente; o gerador não sobrescreve arquivos."); return;
            }
            foreach (string folder in new[]{"Scripts","Editor","Cenas","Prefabs","Blender~","Modelos","Documentacao"})
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var rig = CreateRig();
                PrefabUtility.SaveAsPrefabAsset(rig.gameObject,PrefabPath);
                AddEnvironment(rig);
                AddPreviewModel(rig);
                rig.ResetLanding();
                if (!EditorSceneManager.SaveScene(scene,LunarLandingTransition.LandingScene))
                    throw new IOException("Não foi possível salvar a cena de demonstração.");
                Selection.activeGameObject = rig.gameObject;
                Debug.Log("Pouso Lunar: prefab vazio e cena de demonstração criados. Build Settings não foram alterados.");
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        public static LunarLandingController CreateRig()
        {
            var root = new GameObject("RocketLandingRoot");
            var rig = root.AddComponent<LunarLandingController>();
            root.AddComponent<LunarLandingSceneLink>();
            rig.motionRoot = Child(root.transform,"RocketMotionRoot",Vector3.zero);
            rig.visualSocket = Child(rig.motionRoot,"RocketVisualSocket",Vector3.zero);
            rig.engineEffectsSocket = Child(rig.motionRoot,"EngineEffectsSocket",Vector3.zero);
            rig.landingEffectsSocket = Child(rig.motionRoot,"LandingEffectsSocket",Vector3.zero);
            rig.effects = root.AddComponent<LunarLandingEffects>();
            var waypoints = Child(root.transform,"LandingWaypoints",Vector3.zero);
            rig.approachPoint = Child(waypoints,"Approach",new Vector3(-60,65,0));
            rig.decelerationPoint = Child(waypoints,"Deceleration",new Vector3(-20,32,0));
            rig.descentPoint = Child(waypoints,"VerticalDescent",new Vector3(0,18,0));
            rig.landingPoint = Child(waypoints,"LandingPoint",Vector3.zero);
            var cameras = Child(root.transform,"LandingCameraRig",Vector3.zero);
            rig.cameraRig = cameras.gameObject.AddComponent<LunarLandingCamera>();
            var output = Child(cameras,"LandingOutputCamera",new Vector3(-80,40,-95)).gameObject;
            var camera = output.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = PaletaLua.CeuTopo;
            camera.nearClipPlane = .1f; camera.farClipPlane = 1500;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            output.AddComponent<AudioListener>();
            var brain = output.AddComponent<CinemachineBrain>();
            brain.IgnoreTimeScale = true;
            rig.cameraRig.cinematicCamera = Child(cameras,"LandingCinemachine",Vector3.zero).gameObject.AddComponent<CinemachineCamera>();
            rig.cameraRig.establishing.anchor = Child(cameras,"Shot1_Establishing",new Vector3(-65,36,-95));
            rig.cameraRig.approach.anchor = Child(cameras,"Shot2_Approach",new Vector3(-30,25,-48));
            rig.cameraRig.descent.anchor = Child(cameras,"Shot3_Descent",new Vector3(20,12,-28));
            rig.cameraRig.final.anchor = Child(cameras,"Shot4_Final",new Vector3(16,7,-20));
            rig.cameraRig.final.fieldOfView = 42;
            rig.cameraRig.establishing.target = rig.motionRoot;
            rig.cameraRig.approach.target = rig.motionRoot;
            rig.cameraRig.descent.target = rig.motionRoot;
            rig.cameraRig.final.target = rig.landingPoint;
            LunarLandingSiteEditor.ConfigureNewRig(rig);
            rig.ResetLanding();
            return rig;
        }

        static Transform Child(Transform parent,string name,Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent,false); child.localPosition = position; return child;
        }

        static void AddPreviewModel(LunarLandingController rig)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MenuInicial/Modelos/Foguete.fbx");
            if (!source) return;
            rig.motionRoot.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var preview = UnityEngine.Object.Instantiate(source,rig.visualSocket);
            preview.name = "PREVIEW_ExistingBlenderRocket_RemoveForFinalFBX";
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/BaseLunar/Materiais/M_Casco.mat");
            var renderers = preview.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { UnityEngine.Object.DestroyImmediate(preview); return; }
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                if (material)
                {
                    var materials = renderer.sharedMaterials;
                    for (int i=0;i<materials.Length;i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
            }
            float scale = 8f / Mathf.Max(.001f,bounds.size.y);
            preview.transform.localScale *= scale;
            preview.transform.localPosition -= new Vector3(bounds.center.x,bounds.min.y,bounds.center.z) * scale;
        }

        static void AddEnvironment(LunarLandingController rig)
        {
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/BaseLunar/Materiais/Ceu_Estrelas.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = PaletaLua.AmbienteCeu;
            RenderSettings.ambientEquatorColor = PaletaLua.MeioTom * .5f;
            RenderSettings.ambientGroundColor = PaletaLua.AmbienteChao;
            new GameObject("LandingCartoonStyle").AddComponent<IluminacaoCartoon>();
            var sun = new GameObject("LandingSun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = PaletaLua.Sol; sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(32,-35,0);
            RenderSettings.sun = sun;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BaseLunar/Modelos/BaseLunar.fbx");
            if (!source) { Debug.LogWarning("Sem cenário visual: BaseLunar.fbx não encontrado."); return; }
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name != "Terreno_Lua") continue;
                // Apenas instancia o mesh Blender existente; nenhum script/collider/gameplay da Base Lunar.
                var terrain = UnityEngine.Object.Instantiate(filter.gameObject);
                terrain.name = "LandingTerrain_ExistingBlenderMesh";
                terrain.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);
                terrain.transform.localScale = filter.transform.lossyScale;
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/BaseLunar/Materiais/M_Regolito.mat");
                var renderer = terrain.GetComponent<Renderer>();
                if (renderer && material) renderer.sharedMaterial = material;
                // Preserva o sistema de coordenadas original do FBX/gameplay.
                // O LandingPoint é que deve acompanhar a altura real da superfície escolhida.
                break;
            }
        }

    }
}
