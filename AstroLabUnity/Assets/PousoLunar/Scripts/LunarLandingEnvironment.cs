using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstroLab.PousoLunar
{
    /// <summary>Base visual por referências de mesh/material; nunca instancia scripts nem carrega gameplay.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LunarLandingEnvironment : MonoBehaviour
    {
        public string sourceScenePath = "Assets/BaseLunar/Cenas/Lua.unity";
        public string sourceModelPath = "Assets/BaseLunar/Modelos/BaseLunar.fbx";
        [Tooltip("Raiz GameObject real do FBX; apenas malhas e transforms são copiados, nenhum script é instanciado.")]
        public GameObject sourceVisualModel;
        [Min(0)] public float nearbyRockRadius = 70;
        public int visualMeshCount;
        public int solarPanelCount;
        public int nearbyRockCount;
        public Bounds structureBounds;
        public Mesh sourceTerrainMesh;
        public Vector3 sourceTerrainPosition;
        public Quaternion sourceTerrainRotation = Quaternion.identity;
        public Vector3 sourceTerrainScale = Vector3.one;
        public Transform GeneratedRoot { get; private set; }

        void OnEnable() => EnsureBuilt();
        void OnDisable() => ClearGenerated();

        [ContextMenu("Montar ambiente visual")]
        public void EnsureBuilt()
        {
            if (GeneratedRoot || !sourceVisualModel) return;
            // Um prefab extraído pelo Editor já contém as malhas persistentes; não gera uma segunda cópia.
            if (GetComponentsInChildren<MeshFilter>(true).Length > 0) return;
            var generated = new GameObject("GeneratedLunarBaseVisuals").transform;
            generated.SetParent(transform, false);
            GeneratedRoot = generated;
            var map = new Dictionary<Transform, Transform>();
            Transform CloneTransform(Transform source)
            {
                if (map.TryGetValue(source, out var found)) return found;
                var target = new GameObject(source.name).transform;
                target.SetParent(source == sourceVisualModel.transform ? generated : CloneTransform(source.parent), false);
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
                map.Add(source, target);
                return target;
            }
            visualMeshCount = solarPanelCount = nearbyRockCount = 0;
            bool hasBounds = false;
            foreach (var filter in sourceVisualModel.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!filter.sharedMesh || !renderer || !renderer.enabled || !filter.gameObject.activeSelf) continue;
                if (filter.name == "Terreno_Lua")
                {
                    sourceTerrainMesh = filter.sharedMesh;
                    sourceTerrainPosition = filter.transform.position;
                    sourceTerrainRotation = filter.transform.rotation;
                    sourceTerrainScale = filter.transform.lossyScale;
                    continue;
                }
                if (IsHelper(filter.name)) continue;
                bool rock = filter.name.StartsWith("Rocha_", StringComparison.Ordinal);
                Bounds originalBounds = WorldBounds(filter);
                if (rock && Vector3.ProjectOnPlane(originalBounds.center - new Vector3(24.2f, -.071624f, 20.3f), Vector3.up).sqrMagnitude
                    > nearbyRockRadius * nearbyRockRadius) continue;
                var target = CloneTransform(filter.transform).gameObject;
                var mesh = target.AddComponent<MeshFilter>(); mesh.sharedMesh = filter.sharedMesh;
                var copy = target.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = renderer.shadowCastingMode;
                copy.receiveShadows = renderer.receiveShadows;
                copy.lightProbeUsage = renderer.lightProbeUsage;
                copy.reflectionProbeUsage = renderer.reflectionProbeUsage;
                copy.renderingLayerMask = renderer.renderingLayerMask;
                visualMeshCount++;
                if (rock) nearbyRockCount++;
                if (filter.name.StartsWith("Painel_R", StringComparison.Ordinal)) solarPanelCount++;
                if (!rock)
                {
                    Bounds bounds = WorldBounds(mesh);
                    if (!hasBounds) { structureBounds = bounds; hasBounds = true; }
                    else structureBounds.Encapsulate(bounds);
                }
            }
            foreach (var child in generated.GetComponentsInChildren<Transform>(true))
                child.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            if (!ValidateVisualOnly(out string error)) Debug.LogError("Pouso: " + error, this);
        }

        static Bounds WorldBounds(MeshFilter filter)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            Bounds world = new Bounds(filter.transform.TransformPoint(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++) world.Encapsulate(filter.transform.TransformPoint(bounds.center
                + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return world;
        }

        static bool IsHelper(string name) => name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("SPAWN_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("WP_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Ceu", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Estrela", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Terra_", StringComparison.OrdinalIgnoreCase) && !name.Contains("Vaso");

        [ContextMenu("Limpar ambiente gerado")]
        public void ClearGenerated()
        {
            if (GeneratedRoot) DestroyImmediate(GeneratedRoot.gameObject);
            GeneratedRoot = null;
        }

        public bool ValidateVisualOnly(out string error)
        {
            error = null;
            foreach (var component in GetComponentsInChildren<Component>(true))
            {
                if (component is Transform || component is MeshFilter || component is MeshRenderer || component == this) continue;
                error = "Componente não visual no ambiente: " + (component ? component.GetType().Name : "script ausente");
                return false;
            }
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
                if (filter.name == "Terreno_Lua" || filter.sharedMesh == sourceTerrainMesh)
                { error = "Terreno duplicado no snapshot visual."; return false; }
            if (visualMeshCount <= 0 || solarPanelCount <= 0)
            { error = "Snapshot sem estruturas ou painéis solares."; return false; }
            return true;
        }

        public bool MatchesTerrain(MeshFilter terrain)
        {
            return Vector3.Distance(transform.position, Vector3.zero) < .0001f
                && Quaternion.Angle(transform.rotation, Quaternion.identity) < .001f
                && Vector3.Distance(transform.lossyScale, Vector3.one) < .0001f
                && terrain && terrain.sharedMesh == sourceTerrainMesh
                && Vector3.Distance(terrain.transform.position, sourceTerrainPosition) < .0001f
                && Quaternion.Angle(terrain.transform.rotation, sourceTerrainRotation) < .001f
                && Vector3.Distance(terrain.transform.lossyScale, sourceTerrainScale) < .0001f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.17f, .78f, .75f, .8f);
            Gizmos.DrawWireCube(structureBounds.center, structureBounds.size);
        }
    }
}
