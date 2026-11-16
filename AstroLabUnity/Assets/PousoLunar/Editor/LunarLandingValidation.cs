using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstroLab.PousoLunar.Editor
{
    /// <summary>Harness isolado: sem framework adicional nem alterações nos testes dos colegas.</summary>
    public static class LunarLandingValidation
    {
        [MenuItem("AstroLab/Pouso Lunar/Validar trajetória e eventos")]
        public static void BuildAndValidate()
        {
            LunarLandingBuilder.CreateDemo();
            var rig = LunarLandingBuilder.CreateRig();
            rig.playOnStart = false;
            try
            {
                Check(rig.ValidateConfiguration(out _),"configuração do rig vazio");
                Check(rig.visualSocket.childCount == 0,"modelo visual ausente");
                Check(!rig.effects.engine && !rig.effects.dust,"efeitos ausentes");
                int count=0, touch=0;
                var states = new List<LunarLandingState>();
                rig.Completed += () => count++;
                rig.touchdown.AddListener(() => touch++);
                rig.StateChanged += state => states.Add(state);
                Check(rig.BeginLanding(),"início público");
                Check(!rig.BeginLanding(),"bloqueio de execução simultânea");
                rig.Advance(1000);
                Check(rig.State == LunarLandingState.Completed && count == 1 && touch == 1,"delta longo e eventos únicos");
                Check(states.Count == 7,"todas as transições incluindo TransitionIn e Completed");
                for (int i=0;i<states.Count;i++) Check(states[i] == (LunarLandingState)(i+1),"ordem dos estados",false);
                Check(Vector3.Distance(rig.motionRoot.position,rig.landingPoint.position)<.00001f,"posição final exata");
                rig.Advance(1); Check(count==1,"conclusão sem repetição");
                rig.ResetLanding(); rig.BeginLanding();
                rig.Advance(12.345); Vector3 oneStep = rig.motionRoot.position;
                Quaternion oneRotation = rig.motionRoot.rotation;
                Vector3 oneCamera = rig.cameraRig.cinematicCamera.transform.position;
                rig.ResetLanding(); rig.BeginLanding();
                for (int i=0;i<12345;i++) rig.Advance(.001);
                Check(Vector3.Distance(oneStep,rig.motionRoot.position)<.0001f,"independência de frame rate (posição)");
                Check(Quaternion.Angle(oneRotation,rig.motionRoot.rotation)<.01f,"independência de frame rate (rotação)");
                Check(Vector3.Distance(oneCamera,rig.cameraRig.cinematicCamera.transform.position)<.0001f,"câmera determinística");
                rig.ResetLanding(); rig.BeginLanding();
                Vector3 previous = rig.motionRoot.position;
                for (int i=0;i<5000 && rig.IsRunning;i++)
                {
                    rig.Advance(.01);
                    Check(Vector3.Dot(rig.motionRoot.position-rig.landingPoint.position,rig.landingPoint.up)>=-.0001f,"não atravessa o plano do solo",false);
                    Check(Vector3.Distance(previous,rig.motionRoot.position)<.5f,"continuidade da trajetória",false);
                    previous=rig.motionRoot.position;
                }
                Check(rig.State == LunarLandingState.Completed,"sequência completa em passos de 10 ms");
                rig.ResetLanding(); Check(rig.State==LunarLandingState.Idle,"reset");
                rig.RestartLanding(); Check(rig.State==LunarLandingState.Approach,"reinício");
                ValidateAssets();
                Debug.Log("LANDING_VALIDATION_PASS: trajetória, estados, eventos, reset, câmera e ausência de modelo/efeitos.");
            }
            finally { UnityEngine.Object.DestroyImmediate(rig.gameObject); }
        }

        static void Check(bool condition,string message,bool log=true)
        {
            if (!condition) throw new InvalidOperationException("LANDING_VALIDATION_FAIL: " + message);
            if (log) Debug.Log("LANDING_PASS: "+message);
        }

        static void ValidateAssets()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(LunarLandingTransition.LandingScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(LunarLandingTransition.LandingScene,OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var component in root.GetComponentsInChildren<Transform>(true))
                        Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(component.gameObject)==0,"sem scripts ausentes",false);
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                            Check(material && material.name!="Default-Material" && material.shader && material.shader.name=="AstroLab/Toon","material Toon: "+renderer.name,false);
                }
                Debug.Log("LANDING_PASS: referências e materiais da demonstração");
                Debug.Log("LANDING_PIPELINE: " + (GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "Built-in (verificar QualitySettings)"));
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene,true); }
        }

        const string PlayKey = "AstroLab.PousoLunar.PlayValidation";
        static LunarLandingController playing;
        static double deadline;
        static bool sawCompleted;
        static int errors;

        public static void StartPlayValidation()
        {
            EditorSceneManager.OpenScene(LunarLandingTransition.LandingScene);
            SessionState.SetBool(PlayKey,true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Subscribe()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        static void OnPlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PlayKey,false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                playing = UnityEngine.Object.FindAnyObjectByType<LunarLandingController>();
                deadline = EditorApplication.timeSinceStartup + 75;
                sawCompleted = false; errors = 0;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += MonitorPlay;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(PlayKey,false);
                if (Application.isBatchMode) EditorApplication.Exit(sawCompleted && errors==0 ? 0 : 1);
            }
        }

        static void OnLog(string text,string stack,LogType type)
        {
            if (type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors++;
        }

        static void MonitorPlay()
        {
            if (playing && playing.State==LunarLandingState.Completed)
            {
                sawCompleted = Vector3.Distance(playing.motionRoot.position,playing.landingPoint.position)<.0001f;
                Debug.Log("LANDING_PLAY_RESULT: completed="+sawCompleted+", errors="+errors);
            }
            else if (EditorApplication.timeSinceStartup<deadline) return;
            else Debug.LogError("LANDING_PLAY_TIMEOUT");
            EditorApplication.update -= MonitorPlay;
            Application.logMessageReceived -= OnLog;
            EditorApplication.isPlaying = false;
        }
    }
}
