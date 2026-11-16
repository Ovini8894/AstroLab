using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstroLab.PousoLunar.Editor
{
    public static class LunarLandingSiteValidation
    {
        [MenuItem("AstroLab/Pouso Lunar/Validar destino e ambiente carregado")]
        public static void Run() => Debug.Log(Validate());

        public static string Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Validação de Editor: saia do Play Mode.");
            var scene = SceneManager.GetSceneByPath(LunarLandingTransition.LandingScene);
            var rig = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LunarLandingController>(true)).Single();
            var terrain = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true))
                .Single(f => f.name == "LandingTerrain_ExistingBlenderMesh");
            var site = rig.landingPoint.position;
            Require(Mathf.Abs(site.x - 24.2f) < .0001f && Mathf.Abs(site.z - 20.3f) < .0001f, "coordenadas XZ selecionadas");
            Require(LunarLandingSiteEditor.TrySurfaceHeight(terrain, site, out float surface) && Mathf.Abs(surface - site.y) < .0001f, "contato no terreno real");
            var lines = new List<string> { "PASS destino=" + site.ToString("F6"), "PASS terreno da demonstração=" + surface.ToString("F6") };
            var lua = EditorSceneManager.OpenPreviewScene(LunarLandingTransition.MoonScene);
            try
            {
                var luaTerrain = lua.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)).Single(f => f.name == "Terreno_Lua");
                Require(LunarLandingSiteEditor.TrySurfaceHeight(luaTerrain, site, out float luaHeight), "superfície na Lua");
                Require(Mathf.Abs(luaHeight - site.y) < .0001f, "altura compartilhada com Lua");
                Require(Vector3.Distance(terrain.transform.position, luaTerrain.transform.position) < .0001f &&
                        Quaternion.Angle(terrain.transform.rotation, luaTerrain.transform.rotation) < .001f &&
                        Vector3.Distance(terrain.transform.lossyScale, luaTerrain.transform.lossyScale) < .0001f, "TRS compartilhado com Lua");
                lines.Add("PASS Lua: mesma malha/TRS e Y=" + luaHeight.ToString("F6"));
            }
            finally { EditorSceneManager.ClosePreviewScene(lua); }

            var previous = SceneManager.GetActiveScene();
            var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(testScene);
            try
            {
                var clone = UnityEngine.Object.Instantiate(rig.gameObject).GetComponent<LunarLandingController>();
                clone.playOnStart = false;
                var obstacles = SceneManager.GetSceneByPath("Assets/Nicholas/Cenas/MissaoNicholas.unity");
                var obstacleBounds = obstacles.IsValid() && obstacles.isLoaded
                    ? obstacles.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>()).Where(r =>
                        r.enabled && r.name != "Terreno_Lua" && !r.name.StartsWith("Ceu", StringComparison.OrdinalIgnoreCase) &&
                        !r.name.StartsWith("Estrela", StringComparison.OrdinalIgnoreCase) && !r.name.StartsWith("Terra", StringComparison.OrdinalIgnoreCase))
                        .Select(r => (r.name, r.bounds)).ToArray()
                    : Array.Empty<(string name, Bounds bounds)>();
                var visual = clone.visualSocket.GetComponentsInChildren<Renderer>();
                var hits = new HashSet<string>();
                int completionCount = 0;
                clone.Completed += () => completionCount++;
                clone.ResetLanding(); Require(clone.BeginLanding(), "início da cópia isolada");
                Vector3 last = clone.motionRoot.position, cameraLast = clone.cameraRig.cinematicCamera.transform.position;
                float maxStep = 0, maxCameraStep = 0;
                int sample = 0;
                while (clone.IsRunning && sample < 2000)
                {
                    clone.Advance(.05); sample++;
                    maxStep = Mathf.Max(maxStep, Vector3.Distance(last, clone.motionRoot.position));
                    maxCameraStep = Mathf.Max(maxCameraStep, Vector3.Distance(cameraLast, clone.cameraRig.cinematicCamera.transform.position));
                    last = clone.motionRoot.position; cameraLast = clone.cameraRig.cinematicCamera.transform.position;
                    foreach (var renderer in visual)
                        foreach (var obstacle in obstacleBounds)
                            if (renderer.bounds.Intersects(obstacle.bounds)) hits.Add(obstacle.name);
                    if (sample % 20 == 0 && LunarLandingSiteEditor.TrySurfaceHeight(terrain, last, out float ground))
                        Require(last.y >= ground - .0001f, "trajetória acima da superfície");
                }
                Require(clone.State == LunarLandingState.Completed && completionCount == 1, "estados/conclusão única");
                Require(Vector3.Distance(clone.motionRoot.position, site) < .0001f, "posição final exata");
                var structures = hits.Where(n => !n.StartsWith("Rocha_", StringComparison.OrdinalIgnoreCase)).ToArray();
                Require(structures.Length == 0, "AABB do preview versus estruturas: " + string.Join(", ", structures));
                lines.Add("PASS trajetória completa: " + sample + " amostras, passo=0.05 s, conclusão única");
                lines.Add("PASS terreno ao longo da trajetória (amostras a cada 1 s)");
                lines.Add("PASS AABB preview/estruturas: " + obstacleBounds.Length + " renderers consultados, sem interseções com prédios/painéis");
                if (hits.Count > 0) lines.Add("AVISO volumes conservadores próximos a rochas: " + string.Join(", ", hits) + "; conferir geometria/apoios com o FBX final, sem elevar o pivô artificialmente.");
                lines.Add("INFO maior deslocamento por amostra: foguete=" + maxStep.ToString("F4") + " m; câmera=" + maxCameraStep.ToString("F4") + " m");

                // Prova de coordenadas mundiais com offsets nos pais e sem visual/efeitos.
                clone.ResetLanding();
                while (clone.visualSocket.childCount > 0) UnityEngine.Object.DestroyImmediate(clone.visualSocket.GetChild(0).gameObject);
                clone.effects = null;
                clone.transform.position += new Vector3(7, 3, -11);
                LunarLandingSiteEditor.MoveRig(clone, site, false);
                Require(Vector3.Distance(clone.landingPoint.position, site) < .0001f, "offset dos pais");
                Require(clone.BeginLanding(), "execução sem modelo/efeitos"); clone.Advance(1000);
                Require(clone.State == LunarLandingState.Completed && Vector3.Distance(clone.motionRoot.position, site) < .0001f, "pouso sem modelo/efeitos");
                lines.Add("PASS pais deslocados, visual vazio e efeitos ausentes");
                LunarLandingValidation.BuildAndValidate();
                lines.Add("PASS harness original de estados/eventos/determinismo/reinício/materiais");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(testScene, true);
            }
            return string.Join("\n", lines);
        }

        static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("LANDING_SITE_FAIL: " + description);
        }
    }
}
