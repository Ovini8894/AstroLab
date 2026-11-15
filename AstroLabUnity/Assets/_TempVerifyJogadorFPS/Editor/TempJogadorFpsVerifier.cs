#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class TempJogadorFpsVerifier
{
    const string PrefActive = "TempVerifyJogadorFPS_Active";
    const string PrefDone = "TempVerifyJogadorFPS_Done";
    const string PrefExitCode = "TempVerifyJogadorFPS_ExitCode";
    const string ResultPath = @"C:\Users\pedro\.gemini\antigravity-cli\brain\41cbbc80-715e-48ef-b37e-a45672d9093f\scratch\verify_result.txt";

    static readonly string[] ScenesToCheck =
    {
        "Assets/BaseLunar/Cenas/Lua.unity",
        "Assets/Treinamentos/CapacetePedro.unity",
        "Assets/Nicholas/Cenas/MissaoNicholas.unity",
    };

    static readonly double StartWallTime = EditorApplication.timeSinceStartup;

    public static void Start()
    {
        SessionState.SetBool(PrefActive, false);
        SessionState.SetBool(PrefDone, false);
        SessionState.SetInt(PrefExitCode, 1);

        var sb = new StringBuilder();
        bool scenesOk = true;

        try
        {
            foreach (string scenePath in ScenesToCheck)
            {
                Scene s = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                JogadorFPS fps = UnityEngine.Object.FindFirstObjectByType<JogadorFPS>();
                if (!s.IsValid() || fps == null)
                {
                    sb.AppendLine($"FAIL SceneCheck {scenePath}: JogadorFPS not found or invalid scene");
                    scenesOk = false;
                    continue;
                }

                bool defaultsOk =
                    Mathf.Approximately(fps.gravidade, 1.62f) &&
                    Mathf.Approximately(fps.velocidade, 3.0f) &&
                    Mathf.Approximately(fps.velocidadeCorrida, 5.5f) &&
                    Mathf.Approximately(fps.alturaPulo, 1.2f) &&
                    Mathf.Approximately(fps.multiplicadorAlturaPulo, 1.85f) &&
                    Mathf.Approximately(fps.velocidadeMaximaQueda, 4.2f);

                sb.AppendLine(
                    $"{(defaultsOk ? "PASS" : "FAIL")} SceneCheck {scenePath}: " +
                    $"g={fps.gravidade}, walk={fps.velocidade}, run={fps.velocidadeCorrida}, " +
                    $"h={fps.alturaPulo}, mult={fps.multiplicadorAlturaPulo}, maxFall={fps.velocidadeMaximaQueda}");

                if (!defaultsOk) scenesOk = false;
            }

            // Reopen Lua.unity for PlayMode verification (read-only).
            EditorSceneManager.OpenScene("Assets/BaseLunar/Cenas/Lua.unity", OpenSceneMode.Single);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"FAIL Exception during EditMode scene check: {ex}");
            scenesOk = false;
        }

        WriteResult(sb.ToString(), append: false);

        if (!scenesOk)
        {
            EditorApplication.Exit(1);
            return;
        }

        SessionState.SetBool(PrefActive, true);
        SessionState.SetBool(PrefDone, false);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void RegisterExitWatcher()
    {
        EditorApplication.update += WatchCompletion;
    }

    static void WatchCompletion()
    {
        if (SessionState.GetBool(PrefDone, false) && !EditorApplication.isPlaying)
        {
            int code = SessionState.GetInt(PrefExitCode, 1);
            SessionState.SetBool(PrefDone, false);
            EditorApplication.Exit(code);
            return;
        }

        if (SessionState.GetBool(PrefActive, false) && EditorApplication.timeSinceStartup - StartWallTime > 120.0)
        {
            WriteResult("FAIL Timeout watchdog triggered\n", append: true);
            SessionState.SetBool(PrefActive, false);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(2);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnPlayModeStarted()
    {
        if (!SessionState.GetBool(PrefActive, false)) return;

        var go = new GameObject("TempVerifyRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<PlayModeRunner>();
    }

    static void WriteResult(string text, bool append)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
        if (append) File.AppendAllText(ResultPath, text, Encoding.UTF8);
        else File.WriteAllText(ResultPath, text, Encoding.UTF8);
    }

    sealed class PlayModeRunner : MonoBehaviour
    {
        readonly StringBuilder sb = new StringBuilder();
        readonly List<string> runtimeErrors = new List<string>();
        bool allPassed = true;

        void OnEnable()
        {
            Application.logMessageReceived += CaptureLog;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= CaptureLog;
        }

        void CaptureLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                runtimeErrors.Add($"{type}: {condition}");
            }
        }

        IEnumerator Start()
        {
            Time.captureDeltaTime = 0.02f; // 50 Hz deterministico

            // Fase 1: Verifica Lua.unity real por 10 frames sem erros de console.
            JogadorFPS luaPlayer = FindFirstObjectByType<JogadorFPS>();
            if (luaPlayer == null)
            {
                Fail("LuaPlayMode", "JogadorFPS nao encontrado em Lua.unity em PlayMode");
            }
            else
            {
                for (int i = 0; i < 10; i++) yield return null;
                Pass($"LuaPlayMode ativo sem erros (pos={luaPlayer.transform.position})");
            }

            // Fase 2: Cena limpa controlada para medir fisica exata do JogadorFPS.
            Scene testScene = SceneManager.CreateScene("TempPhysicsArena");
            SceneManager.SetActiveScene(testScene);
            Scene luaScene = SceneManager.GetSceneByName("Lua");
            if (luaScene.IsValid())
            {
                foreach (GameObject root in luaScene.GetRootGameObjects()) root.SetActive(false);
            }

            InputSettings.BackgroundBehavior prevBg = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            Keyboard kb = InputSystem.AddDevice<Keyboard>("VerifyKeyboard");
            Mouse mouse = InputSystem.AddDevice<Mouse>("VerifyMouse");
            InputSystem.EnableDevice(kb);
            InputSystem.EnableDevice(mouse);
            kb.MakeCurrent();
            mouse.MakeCurrent();

            Pass($"InputDevices bg={InputSystem.settings.backgroundBehavior}, kb={Keyboard.current?.name}, enabled={kb.enabled}, mouse={Mouse.current?.name}");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(200f, 1f, 200f);

            var playerGo = new GameObject("TestPlayer");
            playerGo.transform.position = new Vector3(0f, 1.0f, 0f);
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = Vector3.zero;
            cc.slopeLimit = 40f;
            cc.stepOffset = 0.35f;

            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(playerGo.transform, false);
            camGo.AddComponent<Camera>();

            var fps = playerGo.AddComponent<JogadorFPS>();

            // Deixa estabilizar no solo (chao topo y=0 -> centro capsule y=0.9 + skinWidth).
            QueueKeys(kb);
            for (int i = 0; i < 30; i++) yield return null;
            AssertCond("InitialGrounded", cc.isGrounded, $"isGrounded={cc.isGrounded}, y={playerGo.transform.position.y:F3}");

            // Teste 1: Aceleracao e desaceleracao suave ao caminhar no solo.
            Vector3 p0 = playerGo.transform.position;
            QueueKeys(kb, Key.W);
            yield return null;
            float speedFrame1 = HorizontalSpeed(p0, playerGo.transform.position, 0.02f);

            for (int i = 0; i < 29; i++)
            {
                p0 = playerGo.transform.position;
                QueueKeys(kb, Key.W);
                yield return null;
            }
            float walkSteadySpeed = HorizontalSpeed(p0, playerGo.transform.position, 0.02f);

            QueueKeys(kb); // Solta W
            yield return null;
            p0 = playerGo.transform.position;
            QueueKeys(kb);
            yield return null;
            float speedAfterRelease1 = HorizontalSpeed(p0, playerGo.transform.position, 0.02f);

            for (int i = 0; i < 25; i++)
            {
                p0 = playerGo.transform.position;
                QueueKeys(kb);
                yield return null;
            }
            float speedAfterStop = HorizontalSpeed(p0, playerGo.transform.position, 0.02f);

            AssertCond("SmoothGroundWalk",
                speedFrame1 > 0.05f && speedFrame1 < 1.5f &&
                Mathf.Abs(walkSteadySpeed - 3.0f) < 0.15f &&
                speedAfterRelease1 > 1.5f && speedAfterRelease1 < 2.95f &&
                speedAfterStop < 0.05f,
                $"f1={speedFrame1:F2}, steady={walkSteadySpeed:F2}, rel1={speedAfterRelease1:F2}, stop={speedAfterStop:F2}");

            // Teste 2: Corrida no solo chega a 5.5 m/s.
            for (int i = 0; i < 40; i++)
            {
                p0 = playerGo.transform.position;
                QueueKeys(kb, Key.W, Key.LeftShift);
                yield return null;
            }
            float sprintSpeed = HorizontalSpeed(p0, playerGo.transform.position, 0.02f);
            AssertCond("GroundSprint", Mathf.Abs(sprintSpeed - 5.5f) < 0.15f, $"sprintSpeed={sprintSpeed:F2}");

            // Para completamente antes do teste parado.
            for (int i = 0; i < 35; i++)
            {
                QueueKeys(kb);
                yield return null;
            }

            // Teste 3: Pulo parado + Shift+W no ar NAO permite correr no ar (fica <= velocidadeMinimaAjusteAereo).
            QueueKeys(kb, Key.Space);
            yield return null;
            float maxAirSpeedFromStandstill = 0f;
            float groundY = playerGo.transform.position.y;
            float maxJumpY = groundY;
            int airFrames = 0;

            for (int i = 0; i < 220; i++)
            {
                p0 = playerGo.transform.position;
                QueueKeys(kb, Key.W, Key.LeftShift);
                yield return null;
                float curSpeed = HorizontalSpeed(p0, playerGo.transform.position, 0.02f);
                if (playerGo.transform.position.y > maxJumpY) maxJumpY = playerGo.transform.position.y;
                if (!cc.isGrounded)
                {
                    airFrames++;
                    if (curSpeed > maxAirSpeedFromStandstill) maxAirSpeedFromStandstill = curSpeed;
                }
                else if (airFrames > 10)
                {
                    break;
                }
            }

            float jumpHeight = maxJumpY - groundY;
            float airTime = airFrames * 0.02f;
            AssertCond("NoSprintFromStandstillJump",
                airFrames > 10 && maxAirSpeedFromStandstill > 0.2f &&
                maxAirSpeedFromStandstill <= fps.velocidadeMinimaAjusteAereo + 0.05f,
                $"maxAirSpeed={maxAirSpeedFromStandstill:F2} (cap={fps.velocidadeMinimaAjusteAereo:F2}), airFrames={airFrames}");
            AssertCond("LunarJumpHeightAndDuration",
                jumpHeight >= 2.05f && jumpHeight <= 2.40f && airTime >= 3.0f && airTime <= 3.65f,
                $"jumpHeight={jumpHeight:F2}m, airTime={airTime:F2}s");

            // Teste 4: Pulo caminhando (3.0 m/s) + Shift no ar NAO ganha velocidade de corrida.
            for (int i = 0; i < 35; i++) { QueueKeys(kb); yield return null; }
            for (int i = 0; i < 30; i++) { QueueKeys(kb, Key.W); yield return null; }

            QueueKeys(kb, Key.W, Key.Space);
            yield return null;
            float maxAirWalkPlusShift = 0f;
            int airFrames4 = 0;
            for (int i = 0; i < 60; i++)
            {
                p0 = playerGo.transform.position;
                QueueKeys(kb, Key.W, Key.LeftShift);
                yield return null;
                float spd = HorizontalSpeed(p0, playerGo.transform.position, 0.02f);
                if (!cc.isGrounded)
                {
                    airFrames4++;
                    if (spd > maxAirWalkPlusShift) maxAirWalkPlusShift = spd;
                }
            }
            for (int i = 0; i < 200 && !cc.isGrounded; i++) { QueueKeys(kb); yield return null; }
            AssertCond("NoAirSprintBoostFromWalk",
                airFrames4 > 10 && maxAirWalkPlusShift >= 2.85f && maxAirWalkPlusShift <= 3.05f,
                $"maxAirWalkPlusShift={maxAirWalkPlusShift:F2}m/s, airFrames={airFrames4}");

            // Teste 5: Corrida no solo + pulo + solta teclas no ar -> conserva momento da decolagem.
            for (int i = 0; i < 40; i++) { QueueKeys(kb); yield return null; }
            for (int i = 0; i < 40; i++) { QueueKeys(kb, Key.W, Key.LeftShift); yield return null; }
            QueueKeys(kb, Key.W, Key.LeftShift, Key.Space);
            yield return null;

            float minMomentumInAir = float.MaxValue;
            int airFrames5 = 0;
            for (int i = 0; i < 50; i++)
            {
                p0 = playerGo.transform.position;
                QueueKeys(kb); // Solta tudo no vacuo lunar
                yield return null;
                float spd = HorizontalSpeed(p0, playerGo.transform.position, 0.02f);
                if (!cc.isGrounded)
                {
                    airFrames5++;
                    if (spd < minMomentumInAir) minMomentumInAir = spd;
                }
            }
            for (int i = 0; i < 200 && !cc.isGrounded; i++) { QueueKeys(kb); yield return null; }
            AssertCond("VacuumMomentumConserved",
                airFrames5 > 10 && minMomentumInAir >= 5.25f,
                $"minMomentumInAir={minMomentumInAir:F2}m/s, airFrames={airFrames5}");

            // Teste 6: Queda alta -> velocidade vertical limita em -velocidadeMaximaQueda (-4.2 m/s).
            QueueKeys(kb);
            cc.enabled = false;
            playerGo.transform.position = new Vector3(0f, 22f, 0f);
            cc.enabled = true;
            float minVelY = 0f;
            for (int i = 0; i < 600; i++)
            {
                float prevY = playerGo.transform.position.y;
                QueueKeys(kb);
                yield return null;
                if (!cc.isGrounded)
                {
                    float vy = (playerGo.transform.position.y - prevY) / 0.02f;
                    if (vy < minVelY) minVelY = vy;
                }
                else if (i > 10)
                {
                    break;
                }
            }
            AssertCond("TerminalFallVelocityCapped",
                minVelY <= -4.05f && minVelY >= -4.3f && cc.isGrounded,
                $"minVelY={minVelY:F2}m/s, grounded={cc.isGrounded}");

            // Teste 7: Colisao com teto evita flutuar colado e OnDisable limpa inercia.
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.position = new Vector3(playerGo.transform.position.x, playerGo.transform.position.y + 2.5f, playerGo.transform.position.z);
            ceiling.transform.localScale = new Vector3(10f, 0.5f, 10f);

            QueueKeys(kb, Key.Space);
            yield return null;
            int ceilingAirFrames = 0;
            for (int i = 0; i < 160; i++)
            {
                QueueKeys(kb);
                yield return null;
                if (!cc.isGrounded) ceilingAirFrames++;
                else if (ceilingAirFrames > 3) break;
            }
            Destroy(ceiling);
            AssertCond("CeilingCollisionDropsNaturally",
                ceilingAirFrames > 15 && ceilingAirFrames < 110 && cc.isGrounded,
                $"ceilingAirFrames={ceilingAirFrames} ({ceilingAirFrames * 0.02f:F2}s), grounded={cc.isGrounded}");

            for (int i = 0; i < 30; i++) { QueueKeys(kb, Key.W, Key.LeftShift); yield return null; }
            fps.enabled = false;
            yield return null;
            fps.enabled = true;
            p0 = playerGo.transform.position;
            QueueKeys(kb);
            yield return null;
            float postDisableSpeed = HorizontalSpeed(p0, playerGo.transform.position, 0.02f);
            AssertCond("OnDisableClearsInertia", postDisableSpeed < 0.05f, $"postDisableSpeed={postDisableSpeed:F2}");

            InputSystem.settings.backgroundBehavior = prevBg;
            InputSystem.RemoveDevice(kb);
            InputSystem.RemoveDevice(mouse);

            AssertCond("NoConsoleErrors", runtimeErrors.Count == 0,
                runtimeErrors.Count == 0 ? "0 errors" : string.Join(" | ", runtimeErrors));

            WriteResult(sb.ToString(), append: true);
            SessionState.SetInt(PrefExitCode, allPassed ? 0 : 1);
            SessionState.SetBool(PrefActive, false);
            SessionState.SetBool(PrefDone, true);
            EditorApplication.isPlaying = false;
        }

        static void QueueKeys(Keyboard kb, params Key[] keys)
        {
            InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
        }

        static float HorizontalSpeed(Vector3 a, Vector3 b, float dt)
        {
            Vector3 d = b - a;
            d.y = 0f;
            return d.magnitude / dt;
        }

        void Pass(string msg) => sb.AppendLine($"PASS {msg}");

        void Fail(string name, string detail)
        {
            allPassed = false;
            sb.AppendLine($"FAIL {name}: {detail}");
        }

        void AssertCond(string name, bool ok, string detail)
        {
            if (ok) sb.AppendLine($"PASS {name}: {detail}");
            else Fail(name, detail);
        }
    }
}
#endif
