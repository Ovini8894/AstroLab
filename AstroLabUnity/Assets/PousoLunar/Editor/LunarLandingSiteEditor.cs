using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstroLab.PousoLunar.Editor
{
    /// <summary>Ajusta somente o rig e o terreno de demonstração; cenas de gameplay são referências de leitura.</summary>
    public static class LunarLandingSiteEditor
    {
        public static readonly Vector3 ChosenSite = new Vector3(24.2f, 0f, 20.3f);
        const string TerrainAsset = "Assets/BaseLunar/Modelos/BaseLunar.fbx";
        const string TerrainName = "LandingTerrain_ExistingBlenderMesh";

        // Usado pelo gerador e por testes sintéticos, sem instanciar terreno ou depender de um FBX de foguete.
        public static void ConfigureNewRig(LunarLandingController rig)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainAsset);
            var filter = source ? source.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "Terreno_Lua") : null;
            if (!filter || !TrySurfaceHeight(filter, ChosenSite, out float height))
                throw new InvalidOperationException("Não foi encontrada superfície lunar no ponto escolhido.");
            MoveRig(rig, new Vector3(ChosenSite.x, height, ChosenSite.z), false);
            FrameFinalShotTowardBase(rig, filter.transform.position);
        }

        [MenuItem("AstroLab/Pouso Lunar/Aplicar ponto escolhido (24.2, 20.3)")]
        public static void ApplyChosenSite() => ApplySite(ChosenSite);

        [MenuItem("AstroLab/Pouso Lunar/Aplicar marcador selecionado como destino")]
        static void ApplySelectedMarker()
        {
            if (!Selection.activeTransform) throw new InvalidOperationException("Selecione um marcador de posição na Scene view.");
            ApplySite(Selection.activeTransform.position);
        }

        public static void ApplySite(Vector3 requested)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode antes de alterar o destino persistente.");
            Scene scene = SceneManager.GetSceneByPath(LunarLandingTransition.LandingScene);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(LunarLandingTransition.LandingScene, OpenSceneMode.Additive);
            var rigs = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LunarLandingController>(true)).ToArray();
            if (rigs.Length != 1) throw new InvalidOperationException("Esperado exatamente um rig na cena de pouso.");
            var terrain = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)).SingleOrDefault(f => f.name == TerrainName);
            if (!terrain) throw new InvalidOperationException("Terreno de demonstração não encontrado. Nenhum objeto será recriado.");
            var reference = FindLoadedGameplayTerrain();
            Scene preview = default;
            try
            {
                if (!reference)
                {
                    preview = EditorSceneManager.OpenPreviewScene(LunarLandingTransition.MoonScene);
                    reference = FindTerrain(preview);
                }
                if (!reference || reference.sharedMesh != terrain.sharedMesh)
                    throw new InvalidOperationException("Os terrenos não compartilham a mesma malha lunar. Alinhamento automático recusado.");
                if (!TrySurfaceHeight(reference, requested, out float height))
                    throw new InvalidOperationException("O ponto escolhido está fora da superfície lunar.");
                Vector3 destination = new Vector3(requested.x, height, requested.z);
                // Copia somente TRS da instância de referência. Não altera sua cena ou seu asset.
                Undo.RecordObject(terrain.transform, "Alinhar terreno do pouso");
                terrain.transform.SetPositionAndRotation(reference.transform.position, reference.transform.rotation);
                terrain.transform.localScale = reference.transform.lossyScale;
                MoveRig(rigs[0], destination, true);
                FrameFinalShotTowardBase(rigs[0], reference.transform.position);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, LunarLandingTransition.LandingScene))
                    throw new InvalidOperationException("Não foi possível salvar a cena de pouso.");

                // Edita o prefab existente sem recriá-lo nem aplicar alterações de outros objetos.
                var prefab = PrefabUtility.LoadPrefabContents(LunarLandingBuilder.PrefabPath);
                try
                {
                    var prefabRig = prefab.GetComponent<LunarLandingController>();
                    if (!prefabRig) throw new InvalidOperationException("Prefab sem LunarLandingController.");
                    MoveRig(prefabRig, destination, false);
                    FrameFinalShotTowardBase(prefabRig, reference.transform.position);
                    PrefabUtility.SaveAsPrefabAsset(prefab, LunarLandingBuilder.PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
                Debug.Log("Pouso lunar atualizado: world=" + destination.ToString("F6") +
                          "; Y calculado dos triângulos de " + reference.gameObject.scene.path +
                          ". Somente cena/prefab de PousoLunar foram salvos.");
            }
            finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
        }

        static MeshFilter FindLoadedGameplayTerrain()
        {
            // Prefere exatamente a referência utilizada na seleção aditiva do usuário.
            foreach (string path in new[] { "Assets/Nicholas/Cenas/MissaoNicholas.unity", LunarLandingTransition.MoonScene })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                var terrain = FindTerrain(scene);
                if (terrain) return terrain;
            }
            return null;
        }

        static MeshFilter FindTerrain(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true))
            .FirstOrDefault(f => f.name == "Terreno_Lua" && AssetDatabase.GetAssetPath(f.sharedMesh) == TerrainAsset);

        public static void MoveRig(LunarLandingController rig, Vector3 destination, bool undo)
        {
            if (!rig.ValidateConfiguration(out string error)) throw new InvalidOperationException(error);
            Vector3 delta = destination - rig.landingPoint.position;
            var transforms = new HashSet<Transform> { rig.approachPoint, rig.decelerationPoint, rig.descentPoint, rig.landingPoint };
            if (rig.cameraRig)
            {
                var camera = rig.cameraRig;
                foreach (var shot in new[] { camera.establishing, camera.approach, camera.descent, camera.final })
                    if (shot != null && shot.anchor) transforms.Add(shot.anchor);
            }
            // Ancestros se movem primeiro; os destinos capturados evitam aplicar o delta duas vezes.
            var targets = transforms.Where(t => t).ToDictionary(t => t, t => t.position + delta);
            if (undo) Undo.RecordObjects(targets.Keys.Cast<UnityEngine.Object>().Concat(new UnityEngine.Object[] { rig.motionRoot }).ToArray(), "Reposicionar pouso");
            foreach (var entry in targets.OrderBy(e => Depth(e.Key))) entry.Key.position = entry.Value;
            rig.landingPoint.rotation = Quaternion.identity;
            rig.ResetLanding();
            if (!rig.ValidateConfiguration(out error)) throw new InvalidOperationException(error);
            if (undo)
            {
                foreach (var item in targets.Keys) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rig.motionRoot);
                if (rig.cameraRig && rig.cameraRig.cinematicCamera)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rig.cameraRig.cinematicCamera.transform);
            }
        }

        static int Depth(Transform t) { int depth = 0; while (t.parent) { depth++; t = t.parent; } return depth; }

        static void FrameFinalShotTowardBase(LunarLandingController rig, Vector3 baseOrigin)
        {
            var shot = rig.cameraRig ? rig.cameraRig.final : null;
            if (shot == null || !shot.anchor) return;
            Vector3 site = rig.landingPoint.position;
            Vector3 outward = Vector3.ProjectOnPlane(site - baseOrigin, Vector3.up);
            if (outward.sqrMagnitude < .0001f) return;
            Vector3 offset = shot.anchor.position - site;
            float distance = Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;
            // Mantém altura/distância/FOV; filma do lado externo em direção à base, atrás do foguete.
            shot.anchor.position = site + outward.normalized * distance + Vector3.up * offset.y;
            rig.ResetLanding();
        }

        /// <summary>Interseção vertical em coordenadas mundiais com os triângulos reais. Sem collider ou offset.</summary>
        public static bool TrySurfaceHeight(MeshFilter filter, Vector3 worldPoint, out float height)
        {
            height = float.NegativeInfinity;
            if (!filter || !filter.sharedMesh) return false;
            var vertices = filter.sharedMesh.vertices;
            var triangles = filter.sharedMesh.triangles;
            var matrix = filter.transform.localToWorldMatrix;
            // Faz a transformação uma vez por vértice, em vez de três chamadas nativas por triângulo.
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < .0000001f) continue;
                float u = ((b.z - c.z) * (worldPoint.x - c.x) + (c.x - b.x) * (worldPoint.z - c.z)) / d;
                float v = ((c.z - a.z) * (worldPoint.x - c.x) + (a.x - c.x) * (worldPoint.z - c.z)) / d;
                if (u >= 0 && v >= 0 && u + v <= 1) height = Mathf.Max(height, u * a.y + v * b.y + (1 - u - v) * c.y);
            }
            return !float.IsNegativeInfinity(height);
        }
    }
}
