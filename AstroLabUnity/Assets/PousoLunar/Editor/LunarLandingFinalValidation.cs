using System;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstroLab.PousoLunar.Editor
{
    /// <summary>Validação em quadros reais: saída do Brain, partículas e sequência completa na cena isolada.</summary>
    [InitializeOnLoad]
    public static class LunarLandingFinalValidation
    {
        const string Key = "AstroLab.PousoLunar.FinalValidation.";
        const string ReportPath = "Assets/PousoLunar/Documentacao/ULTIMO_TESTE_PLAY.md";
        static readonly Vector3 Destination = new Vector3(24.2f, -.071624f, 20.3f);
        static readonly float[] CaptureTimes = { 6, 11, 21, 23.5f };
        static readonly string[] CaptureNames = { "approach", "deceleration", "descent", "touchdown" };
        static LunarLandingController rig;
        static CinemachineCamera virtualCamera;
        static CinemachineBrain brain;
        static Camera output;
        static double deadline, probeStart, completedAt;
        static int lastFrame, errors, peakEngine, peakDust, peakRcs, alignedFrames;
        static bool probing, probeCaptured, manualReproduced, autoVerified, finished;
        static Vector3 probeVirtualPosition, probeOutputPosition, firstOutputPosition;
        static Quaternion probeVirtualRotation, probeOutputRotation, firstOutputRotation;
        static bool[] captures;
        static string failure;
        static Transform spacecraftVisual;
        static int framingFrames;
        static LunarLandingEnvironment environment;

        static LunarLandingFinalValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        [MenuItem("AstroLab/Pouso Lunar/Validar modelo final e VFX em Play isolado")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode antes de iniciar a validação.");
            // Abrir em Single não pode descartar trabalho aberto no Editor.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Há cena com alterações não salvas. Salve seu trabalho antes do teste isolado.");
            EditorSceneManager.OpenScene(LunarLandingTransition.LandingScene, OpenSceneMode.Single);
            var initial = UnityEngine.Object.FindAnyObjectByType<LunarLandingController>();
            if (!initial || !initial.cameraRig || !initial.cameraRig.cinematicCamera)
                throw new InvalidOperationException("Cena sem controlador/câmera de pouso configurados.");
            var initialBrain = initial.GetComponentInChildren<CinemachineBrain>(true);
            if (!initialBrain) throw new InvalidOperationException("LandingOutputCamera sem CinemachineBrain.");
            SessionState.SetInt(Key + "OriginalMode", (int)initialBrain.UpdateMethod);
            SessionState.SetBool(Key + "Active", true);
            SessionState.SetBool(Key + "Passed", false);
            SessionState.SetBool(Key + "PersistCamera", false);
            SessionState.SetBool(Key + "RunInBackground", Application.runInBackground);
            Debug.Log("LANDING_FINAL_PRECHECK: scene=" + LunarLandingTransition.LandingScene
                + "; original Brain=" + initialBrain.UpdateMethod
                + "; LunarLandingCamera.Sample não chama ManualUpdate. Nenhuma configuração foi alterada.");
            EditorApplication.isPlaying = true;
        }

        static void OnPlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) StartMonitor();
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                bool passed = SessionState.GetBool(Key + "Passed", false);
                try
                {
                    if (SessionState.GetBool(Key + "PersistCamera", false)) PersistConfirmedCameraFix();
                }
                catch (Exception exception) { passed = false; Debug.LogException(exception); }
                SessionState.SetBool(Key + "Active", false);
                SessionState.SetBool(Key + "PersistCamera", false);
                Application.runInBackground = SessionState.GetBool(Key + "RunInBackground", false);
                if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        static void StartMonitor()
        {
            errors = peakEngine = peakDust = peakRcs = alignedFrames = 0;
            completedAt = 0; probeCaptured = manualReproduced = autoVerified = finished = false;
            failure = null; lastFrame = -1; captures = new bool[CaptureTimes.Length];
            framingFrames=0;
            Application.runInBackground=true;
            Application.logMessageReceived += OnLog;
            try
            {
                Require(SceneManager.sceneCount == 1, "Somente PousoLunar deve estar carregada.");
                rig = UnityEngine.Object.FindAnyObjectByType<LunarLandingController>();
                Require(rig, "Controlador ausente.");
                var spacecraft = rig.GetComponent<LunarLandingSpacecraft>();
                Require(spacecraft, "LunarLandingSpacecraft ausente.");
                spacecraft.EnsureBuilt();
                spacecraftVisual=spacecraft.GeneratedModel;
                Require(spacecraft.GeneratedModel && spacecraft.GeneratedModel.IsChildOf(rig.visualSocket), "Modelo final fora do RocketVisualSocket.");
                Require(!rig.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("PREVIEW_", StringComparison.Ordinal)), "Preview temporário ainda presente.");
                foreach (Renderer renderer in spacecraft.GeneratedModel.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        Require(material && material.shader && material.shader.isSupported, "Material ausente/incompatível: " + renderer.name);
                Require(Vector3.Distance(rig.landingPoint.position, Destination) < .00001f, "Destino mundial foi alterado.");
                Require(rig.ValidateConfiguration(out string configurationError), configurationError);
                Require(rig.effects && rig.effects.mainEngine && rig.effects.rcs && rig.effects.lunarDust, "Referências de VFX ausentes.");
                virtualCamera = rig.cameraRig.cinematicCamera;
                brain = rig.GetComponentInChildren<CinemachineBrain>(true);
                output = brain ? brain.GetComponent<Camera>() : null;
                Require(output && output.enabled && output.targetDisplay == 0, "Camera de saída inválida.");
                Require(UnityEngine.Object.FindObjectsByType<Camera>().Count(c => c.enabled && c.gameObject.activeInHierarchy) == 1, "Mais de uma Camera ativa.");
                Require(UnityEngine.Object.FindObjectsByType<AudioListener>().Count(a => a.enabled && a.gameObject.activeInHierarchy) == 1, "AudioListeners duplicados/ausentes.");
                Require(UnityEngine.Object.FindObjectsByType<Canvas>().Length == 0, "HUD/Canvas inesperado na cena isolada.");
                Require(UnityEngine.Object.FindObjectsByType<Light>().Count(l=>l.type==LightType.Directional && l.enabled)==1,"Luz direcional duplicada/ausente.");
                environment=UnityEngine.Object.FindAnyObjectByType<LunarLandingEnvironment>();
                Require(environment,"Ambiente visual da base ausente.");
                environment.EnsureBuilt();
                Require(environment.ValidateVisualOnly(out string environmentError),environmentError);
                var terrains=UnityEngine.Object.FindObjectsByType<MeshFilter>().Where(f=>f.name=="LandingTerrain_ExistingBlenderMesh" || f.name=="Terreno_Lua").ToArray();
                Require(terrains.Length==1 && environment.MatchesTerrain(terrains[0]),"Terreno duplicado ou desalinhado com a base.");
                probing = brain.UpdateMethod == CinemachineBrain.UpdateMethods.ManualUpdate;
                probeStart = EditorApplication.timeSinceStartup;
                deadline = probeStart + Math.Max(75, rig.TotalDuration + 20);
                EditorApplication.update += Monitor;
            }
            catch (Exception exception) { Finish(false, exception.Message); }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert
                || message.IndexOf("AudioListener", StringComparison.OrdinalIgnoreCase) >= 0 && type == LogType.Warning)
                errors++;
        }

        static void Monitor()
        {
            if(Application.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (finished || !Application.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                Require(rig && output && brain && virtualCamera, "Referência destruída durante Play.");
                Require(EditorApplication.timeSinceStartup < deadline, "Tempo limite excedido.");
                if (!probeCaptured)
                {
                    if (rig.Elapsed < .15) return;
                    probeCaptured = true;
                    probeVirtualPosition = virtualCamera.transform.position; probeVirtualRotation = virtualCamera.transform.rotation;
                    probeOutputPosition = firstOutputPosition = output.transform.position;
                    probeOutputRotation = firstOutputRotation = output.transform.rotation;
                    probeStart = EditorApplication.timeSinceStartup;
                    return;
                }
                if (probing)
                {
                    if (EditorApplication.timeSinceStartup - probeStart < .8) return;
                    bool virtualMoved = Moved(probeVirtualPosition, probeVirtualRotation, virtualCamera.transform, .01f, .1f);
                    bool outputFrozen = !Moved(probeOutputPosition, probeOutputRotation, output.transform, .001f, .01f);
                    Require(virtualMoved && outputFrozen, "Modo manual não reproduziu saída congelada; nenhuma configuração será persistida.");
                    manualReproduced = true;
                    Debug.Log("LANDING_CAMERA_REPRODUCED: câmera virtual moveu, saída permaneceu parada em ManualUpdate, cena isolada.");
                    brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
                    brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
                    probing = false;
                    rig.RestartLanding();
                    firstOutputPosition = output.transform.position; firstOutputRotation = output.transform.rotation;
                    return;
                }

                // O Editor observa o resultado do quadro anterior; State e Camera devem corresponder.
                if (rig.Elapsed > .25 && ReferenceEquals(brain.ActiveVirtualCamera, virtualCamera)
                    && Vector3.Distance(output.transform.position, virtualCamera.State.GetFinalPosition()) < .01f
                    && Quaternion.Angle(output.transform.rotation, virtualCamera.State.GetFinalOrientation()) < .1f
                    && Mathf.Abs(output.fieldOfView - virtualCamera.State.Lens.FieldOfView) < .05f)
                    alignedFrames++;
                if (alignedFrames > 5 && Moved(firstOutputPosition, firstOutputRotation, output.transform, .1f, 1)) autoVerified = true;
                if (autoVerified && rig.Elapsed>.5f)
                {
                    foreach(var renderer in spacecraftVisual.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if(!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        Bounds box=renderer.bounds;
                        for(int corner=0;corner<8;corner++)
                        {
                            Vector3 point=box.center+Vector3.Scale(box.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                            Vector3 view=output.WorldToViewportPoint(point);
                            Require(view.z>output.nearClipPlane && view.x>0 && view.x<1 && view.y>0 && view.y<1,
                                "Nave fora do quadro: "+renderer.name+"; t="+rig.Elapsed.ToString("F2")+" viewport="+view);
                        }
                    }
                    Require(output.transform.position.y>rig.motionRoot.position.y+6,"Perspectiva baixa durante a sequência.");
                    framingFrames++;
                }

                var engine = rig.effects.mainEngine;
                int engineCount = 0;
                foreach (var nozzle in engine.nozzles)
                    if (nozzle != null) engineCount += Count(nozzle.core) + Count(nozzle.outer) + Count(nozzle.glow);
                peakEngine = Math.Max(peakEngine, engineCount);
                var dust = rig.effects.lunarDust;
                peakDust = Math.Max(peakDust, Count(dust.grains) + Count(dust.touchdownGrains));
                int rcsCount = 0;
                foreach (var thruster in rig.effects.rcs.thrusters) if (thruster != null) rcsCount += Count(thruster.jet);
                peakRcs = Math.Max(peakRcs, rcsCount);

                for (int i = 0; i < captures.Length; i++)
                    if (!captures[i] && rig.Elapsed >= CaptureTimes[i])
                    {
                        captures[i] = true;
                        string path = Path.GetFullPath("Assets/PousoLunar/Documentacao/play-" + CaptureNames[i] + ".png");
                        ScreenCapture.CaptureScreenshot(path);
                        Debug.Log("LANDING_CAPTURE_REQUESTED: " + path + "; elapsed=" + rig.Elapsed.ToString("F3")
                            + "; particles(engine/dust/RCS)=" + engineCount + "/" + Count(dust.grains) + "/" + rcsCount);
                    }

                if (rig.State != LunarLandingState.Completed) return;
                if (completedAt == 0) { completedAt = EditorApplication.timeSinceStartup; return; }
                if (EditorApplication.timeSinceStartup - completedAt < .4) return;
                Require(Vector3.Distance(rig.motionRoot.position, Destination) < .00001f, "Posição final incorreta.");
                Require(autoVerified, "Saída da câmera não acompanhou o estado da Cinemachine.");
                Require(framingFrames>30,"Poucos quadros reais para validar enquadramento.");
                var planes=GeometryUtility.CalculateFrustumPlanes(output);
                Require(GeometryUtility.TestPlanesAABB(planes,environment.structureBounds),"Base fora do enquadramento final.");
                Require(environment.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name.StartsWith("Hub_Casco",StringComparison.Ordinal)
                    && GeometryUtility.TestPlanesAABB(planes,r.bounds)),"Edifício principal da base fora do plano final.");
                Require(peakEngine > 0 && peakDust > 0 && peakRcs > 0 && rig.effects.rcs.BurstCount > 0, "Algum VFX não produziu partículas durante a sequência.");
                Require(engine.TargetThrust == 0 && engine.CurrentThrust < .001f && engineCount == 0, "Motor não desligou.");
                Require(rcsCount == 0 && dust.CurrentEmission == 0 && Count(dust.grains) == 0 && Count(dust.touchdownGrains) == 0, "Partículas/emissão permaneceram após Completed.");
                Require(errors == 0, "Console apresentou erros ou conflito de AudioListener.");
                Finish(true, null);
            }
            catch (Exception exception) { Finish(false, exception.Message); }
        }

        static int Count(ParticleSystem system) => system ? system.particleCount : 0;
        static bool Moved(Vector3 position, Quaternion rotation, Transform current, float distance, float degrees)
            => Vector3.Distance(position, current.position) > distance || Quaternion.Angle(rotation, current.rotation) > degrees;
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        static void Finish(bool passed, string reason)
        {
            if (finished) return;
            finished = true; failure = reason;
            EditorApplication.update -= Monitor;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(Key + "Passed", passed);
            SessionState.SetBool(Key + "PersistCamera", passed && manualReproduced && autoVerified);
            string report = "# Último teste automatizado em Play Mode\n\n"
                + "Executado em: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ".\n\n"
                + "Resultado: **" + (passed ? "PASS" : "FAIL") + "**. " + (failure ?? "Sequência completa e verificações aprovadas.") + "\n\n"
                + "- Cena aberta em Single: `PousoLunar.unity`.\n"
                + "- Brain original: " + (CinemachineBrain.UpdateMethods)SessionState.GetInt(Key + "OriginalMode", -1) + ".\n"
                + "- Saída congelada reproduzida em ManualUpdate: " + manualReproduced + ".\n"
                + "- Saída automática acompanhou a câmera virtual: " + autoVerified + ".\n"
                + "- Máximo de partículas motor/poeira/RCS: " + peakEngine + "/" + peakDust + "/" + peakRcs + ".\n"
                + "- Erros observados: " + errors + ".\n\n"
                + "- Quadros reais com nave inteira dentro do frustum: "+framingFrames+".\n"
                + "- Base: "+(environment?environment.visualMeshCount:0)+" malhas visuais, "+(environment?environment.solarPanelCount:0)+" painéis solares.\n"
                + "As capturas solicitadas em 6, 11, 21 e 23,5 s precisam de inspeção humana para aprovação visual. Este relatório não atribui aprovação artística às imagens.\n";
            try { File.WriteAllText(ReportPath, report); }
            catch (Exception exception) { Debug.LogException(exception); SessionState.SetBool(Key + "Passed", false); SessionState.SetBool(Key + "PersistCamera", false); }
            Debug.Log("LANDING_FINAL_RESULT: passed=" + passed + "; reason=" + (reason ?? "none")
                + "; maxParticles=" + peakEngine + "/" + peakDust + "/" + peakRcs + "; errors=" + errors);
            EditorApplication.isPlaying = false;
        }

        static void PersistConfirmedCameraFix()
        {
            Scene scene = SceneManager.GetSceneByPath(LunarLandingTransition.LandingScene);
            if (!scene.IsValid() || !scene.isLoaded || SceneManager.sceneCount != 1 || scene.isDirty)
                throw new InvalidOperationException("Correção confirmada, mas cena mudou/está suja; configuração não salva para preservar trabalho.");
            var sceneRig = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LunarLandingController>(true)).Single();
            var sceneBrain = sceneRig.GetComponentInChildren<CinemachineBrain>(true);
            var spacecraft = sceneRig.GetComponent<LunarLandingSpacecraft>();
            if (spacecraft) spacecraft.ClearGenerated();
            // Uma única propriedade persistente: sem tocar no controlador ou nos assets originais.
            var serialized = new SerializedObject(sceneBrain);
            serialized.FindProperty("UpdateMethod").intValue = (int)CinemachineBrain.UpdateMethods.LateUpdate;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            try
            {
                if (!EditorSceneManager.SaveScene(scene, LunarLandingTransition.LandingScene))
                    throw new IOException("Não foi possível salvar a configuração automática confirmada da câmera.");
            }
            finally { if (spacecraft) spacecraft.EnsureBuilt(); }
            Debug.Log("LANDING_CAMERA_FIX_SAVED: apenas Brain.UpdateMethod=LateUpdate salvo após reprodução e validação em Play.");
        }
    }
}
