using System;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstroLab.PousoLunar.Editor
{
    public static class LunarLandingPresentationEditor
    {
        public static void Configure(LunarLandingController rig)
        {
            var camera=rig.cameraRig;
            Set(camera.establishing,28,32,35,46);
            Set(camera.approach,22,30,55,44);
            Set(camera.descent,19,34,40,46);
            Set(camera.final,23,30,45,48);
            // O prefab pode usar SmartUpdate; os dois modos automáticos são compatíveis.
            var brain=rig.GetComponentInChildren<CinemachineBrain>(true);
            if(brain && brain.UpdateMethod==CinemachineBrain.UpdateMethods.ManualUpdate)
                throw new InvalidOperationException("Brain ainda manual: execute a validação isolada antes de alterar seu modo.");
            rig.cameraRig.Sample(0,0,rig.motionRoot);
        }
        static void Set(LunarLandingCamera.Shot shot,float distance,float elevation,float orbit,float fov)
        {
            shot.followTarget=true; shot.distance=distance; shot.elevation=elevation;
            shot.orbitAngle=orbit; shot.fieldOfView=fov; shot.targetOffset=new Vector3(0,3.8f,0);
            shot.blendDuration=2; shot.smoothing=1;
        }
        [MenuItem("AstroLab/Pouso Lunar/Configurar apresentação e base visual")]
        public static void Setup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia do Play antes de configurar.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Há cena com trabalho pendente. Salve antes de configurar a apresentação.");
            var scene=EditorSceneManager.OpenScene(LunarLandingTransition.LandingScene,OpenSceneMode.Single);
            var rig=UnityEngine.Object.FindAnyObjectByType<LunarLandingController>();
            if(!rig) throw new InvalidOperationException("Controlador de pouso ausente.");
            var site=rig.landingPoint.position;
            Configure(rig);
            LunarLandingModelResolver.Resolve(rig.GetComponent<LunarLandingSpacecraft>(),false);
            LunarLandingEnvironmentBuilder.InstallInLandingScene(scene,false);
            if(Vector3.Distance(site,rig.landingPoint.position)>.000001f) throw new InvalidOperationException("Destino mudou durante configuração.");
            var spacecraft=rig.GetComponent<LunarLandingSpacecraft>();
            spacecraft.ClearGenerated();
            try
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Falha ao salvar somente a cena de pouso.");
            }
            finally { spacecraft.EnsureBuilt(); }
            var prefab=PrefabUtility.LoadPrefabContents(LunarLandingBuilder.PrefabPath);
            try
            {
                var prefabRig=prefab.GetComponent<LunarLandingController>(); Configure(prefabRig);
                var visual=prefab.GetComponent<LunarLandingSpacecraft>(); visual.ClearGenerated();
                visual.sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>(LunarLandingModelResolver.ModelPath);
                PrefabUtility.SaveAsPrefabAsset(prefab,LunarLandingBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            Debug.Log("LANDING_PRESENTATION_CONFIGURED: quatro planos elevados; base visual; destino preservado.");
        }
    }
}
