using System;
using System.Collections.Generic;
using AstroLab.Nicholas.Core;
using UnityEngine;

namespace AstroLab.Nicholas
{
    /// <summary>
    /// Sinalização flutuante (billboard) no mundo para alvos interagíveis num raio de 12 m.
    /// Usa forma geométrica + ícone + texto + cor (acessibilidade sem depender só de cor),
    /// unifica os painéis solares em um único marcador de grupo e usa cache sem Find* por frame.
    /// </summary>
    public sealed class MissionTargetMarkers : MonoBehaviour
    {
        private enum MarkerKind
        {
            PendingRepair,
            Repaired,
            ArtifactPending,
            ArtifactScanned
        }

        private sealed class MarkerGroup
        {
            public bool IsEquipment;
            public EquipmentId Equipment;
            public ArtifactId Artifact;
            public string DisplayName;
            public Vector3 Position;
            public readonly List<Component> Members = new List<Component>();
            public GameObject Root;
            public SpriteRenderer IconRenderer;
            public TextMesh LabelMesh;
            public MarkerKind LastKind = (MarkerKind)(-1);
            public bool LastAimed;
            public bool LastTutorialFocus;
            public float Phase;
        }

        [SerializeField, Min(1f)] private float visibilityRadius = 12f;
        [SerializeField] private float pulseSpeed = 3.2f;
        [SerializeField] private float markerScale = 0.52f;

        private readonly List<MarkerGroup> groups = new List<MarkerGroup>();
        private Transform markersRoot;
        private Camera cachedCamera;
        private MissionController mission;
        private MissionPlayerInteraction interaction;
        private MissionOverlay overlay;
        private MonoBehaviour helmet;
        private EquipmentId? tutorialEquipmentTarget;

        private Sprite triangleSprite;
        private Sprite circleSprite;
        private Sprite starSprite;
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private readonly List<Sprite> generatedSprites = new List<Sprite>();

        private static readonly Color PendingColor = new Color(1.00f, 0.56f, 0.14f, 1f);
        private static readonly Color RepairedColor = new Color(0.28f, 0.90f, 0.54f, 1f);
        private static readonly Color ArtifactColor = new Color(0.22f, 0.85f, 1.00f, 1f);
        private static readonly Color ArtifactDoneColor = new Color(0.45f, 0.78f, 0.88f, 1f);
        private static readonly Color OutlineColor = new Color(0.08f, 0.11f, 0.17f, 0.96f);

        private void Awake()
        {
            mission = MissionController.Instance ?? FindFirstObjectByType<MissionController>();
            GameObject container = new GameObject("Nicholas_Marcadores_Alvos");
            container.transform.SetParent(transform, false);
            markersRoot = container.transform;
            BuildProceduralSprites();
        }

        /// <summary>Define ou limpa o equipamento destacado pelo tutorial do Turno 1.</summary>
        public void SetTutorialTarget(EquipmentId? equipment)
        {
            tutorialEquipmentTarget = equipment;
        }

        /// <summary>Obtém a posição em cache do marcador de grupo/estação do equipamento.</summary>
        public bool TryGetEquipmentMarkerPosition(EquipmentId equipment, out Vector3 worldPos)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                MarkerGroup g = groups[i];
                if (g.IsEquipment && g.Equipment == equipment && HasActiveMember(g))
                {
                    worldPos = g.Position;
                    return true;
                }
            }
            worldPos = Vector3.zero;
            return false;
        }

        private void Start()
        {
            mission = MissionController.Instance ?? FindFirstObjectByType<MissionController>();
            overlay = FindFirstObjectByType<MissionOverlay>();
            interaction = FindFirstObjectByType<MissionPlayerInteraction>();
            helmet = OptionalTeamComponents.FindByName("HudCapacete");
            RefreshTargets();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < generatedSprites.Count; i++)
                if (generatedSprites[i]) Destroy(generatedSprites[i]);
            for (int i = 0; i < generatedTextures.Count; i++)
                if (generatedTextures[i]) Destroy(generatedTextures[i]);
        }

        /// <summary>Reconstroi a lista em cache de marcadores (chamar ao iniciar ou após troca visual de reparos).</summary>
        public void RefreshTargets()
        {
            if (!mission) mission = MissionController.Instance ?? FindFirstObjectByType<MissionController>();
            ClearGroups();
            List<MissionEquipmentTarget> equipmentTargets = new List<MissionEquipmentTarget>();
            foreach (MissionEquipmentTarget t in FindObjectsByType<MissionEquipmentTarget>(FindObjectsSortMode.None))
            {
                if (t && t.isActiveAndEnabled && t.gameObject.activeInHierarchy)
                    equipmentTargets.Add(t);
            }

            BuildEquipmentGroups(equipmentTargets);

            foreach (MissionHistoricalTarget h in FindObjectsByType<MissionHistoricalTarget>(FindObjectsSortMode.None))
            {
                if (!h || !h.gameObject.activeInHierarchy) continue;
                MarkerGroup group = new MarkerGroup
                {
                    IsEquipment = false,
                    Artifact = h.Artifact,
                    DisplayName = Apollo15Trail.Find(h.Artifact).Name,
                    Phase = groups.Count * 0.7f
                };
                group.Members.Add(h);
                group.Position = ComputeTargetAnchorPosition(h.transform, false);
                CreateMarkerVisual(group);
                groups.Add(group);
            }
        }

        private void LateUpdate()
        {
            if (groups.Count == 0) return;
            if (!mission) mission = MissionController.Instance;
            if (!mission || mission.Engine == null) return;

            if (!cachedCamera || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
            Transform viewer = cachedCamera ? cachedCamera.transform : mission.PlayerTransform;
            if (!viewer) return;

            if (!overlay) overlay = FindFirstObjectByType<MissionOverlay>();
            if (!helmet) helmet = OptionalTeamComponents.FindByName("HudCapacete");
            if (!interaction && mission.PlayerTransform)
                interaction = mission.PlayerTransform.GetComponent<MissionPlayerInteraction>();

            bool hideAll = mission.Engine.State.Outcome != MissionOutcome.Running ||
                           (overlay && overlay.IsModalOpen) ||
                           OptionalTeamComponents.IsHelmetModalOpen(helmet);

            float maxSqrDist = visibilityRadius * visibilityRadius;
            Vector3 viewerPos = viewer.position;
            MissionEquipmentTarget aimedEq = interaction ? interaction.AimedEquipment : null;
            MissionHistoricalTarget aimedHist = interaction ? interaction.AimedHistory : null;
            float time = Time.unscaledTime;

            for (int i = 0; i < groups.Count; i++)
            {
                MarkerGroup g = groups[i];
                if (!g.Root) continue;
                if (hideAll || !HasActiveMember(g))
                {
                    if (g.Root.activeSelf) g.Root.SetActive(false);
                    continue;
                }

                bool isTutorialFocus = tutorialEquipmentTarget.HasValue && g.IsEquipment && g.Equipment == tutorialEquipmentTarget.Value;

                float sqrDist = (g.Position - viewerPos).sqrMagnitude;
                if (g.IsEquipment && g.Equipment == EquipmentId.SolarPanels && sqrDist > maxSqrDist && !isTutorialFocus)
                {
                    for (int m = 0; m < g.Members.Count; m++)
                    {
                        Component mem = g.Members[m];
                        if (!mem || !mem.gameObject.activeInHierarchy) continue;
                        float memberSqr = (mem.transform.position - viewerPos).sqrMagnitude;
                        if (memberSqr < sqrDist) sqrDist = memberSqr;
                    }
                }

                if (sqrDist > maxSqrDist && !isTutorialFocus)
                {
                    if (g.Root.activeSelf) g.Root.SetActive(false);
                    continue;
                }

                if (!g.Root.activeSelf) g.Root.SetActive(true);

                bool isAimed = IsGroupAimed(g, aimedEq, aimedHist);
                MarkerKind kind = ResolveKind(g);
                if (kind != g.LastKind || isAimed != g.LastAimed || isTutorialFocus != g.LastTutorialFocus)
                    ApplyMarkerAppearance(g, kind, isAimed, isTutorialFocus);

                Vector3 lookDir = g.Position - viewerPos;
                if (lookDir.sqrMagnitude > 0.0001f)
                    g.Root.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

                float speedMult = (isAimed || isTutorialFocus) ? 1.45f : 1f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * (pulseSpeed * speedMult) + g.Phase);
                float bob = (pulse - 0.5f) * ((isAimed || isTutorialFocus) ? 0.12f : 0.06f);
                g.Root.transform.position = g.Position + Vector3.up * bob;

                float dist = Mathf.Sqrt(sqrDist);
                float maxDistFactor = isTutorialFocus ? 3.8f : 1.18f;
                float distFactor = Mathf.Clamp(dist / 7f, 0.72f, maxDistFactor);
                float focusMult = isAimed ? 1.32f + pulse * 0.10f : (isTutorialFocus ? 1.18f + pulse * 0.14f : 0.94f + pulse * 0.08f);
                float scale = markerScale * distFactor * focusMult;
                g.Root.transform.localScale = Vector3.one * scale;
            }
        }

        private void BuildEquipmentGroups(List<MissionEquipmentTarget> targets)
        {
            List<MissionEquipmentTarget> solarMembers = new List<MissionEquipmentTarget>();
            Dictionary<EquipmentId, List<MissionEquipmentTarget>> byId = new Dictionary<EquipmentId, List<MissionEquipmentTarget>>();

            for (int i = 0; i < targets.Count; i++)
            {
                MissionEquipmentTarget t = targets[i];
                if (t.Equipment == EquipmentId.SolarPanels)
                {
                    solarMembers.Add(t);
                    continue;
                }
                if (!byId.TryGetValue(t.Equipment, out List<MissionEquipmentTarget> list))
                {
                    list = new List<MissionEquipmentTarget>();
                    byId[t.Equipment] = list;
                }
                list.Add(t);
            }

            if (solarMembers.Count > 0)
            {
                MarkerGroup solarGroup = new MarkerGroup
                {
                    IsEquipment = true,
                    Equipment = EquipmentId.SolarPanels,
                    DisplayName = MissionRuleSet.Find(EquipmentId.SolarPanels).DisplayName,
                    Phase = 0f
                };
                Vector3 sum = Vector3.zero;
                int panelCount = 0;
                float topY = float.MinValue;
                for (int i = 0; i < solarMembers.Count; i++)
                {
                    MissionEquipmentTarget t = solarMembers[i];
                    solarGroup.Members.Add(t);
                    if (t.gameObject.name.IndexOf("Painel_", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Vector3 p = ComputeTargetAnchorPosition(t.transform, true);
                        sum += new Vector3(p.x, 0f, p.z);
                        if (p.y > topY) topY = p.y;
                        panelCount++;
                    }
                }
                if (panelCount == 0)
                {
                    for (int i = 0; i < solarMembers.Count; i++)
                    {
                        Vector3 p = ComputeTargetAnchorPosition(solarMembers[i].transform, true);
                        sum += new Vector3(p.x, 0f, p.z);
                        if (p.y > topY) topY = p.y;
                        panelCount++;
                    }
                }
                solarGroup.Position = new Vector3(sum.x / panelCount, topY + 0.35f, sum.z / panelCount);
                CreateMarkerVisual(solarGroup);
                groups.Add(solarGroup);
            }

            foreach (KeyValuePair<EquipmentId, List<MissionEquipmentTarget>> entry in byId)
            {
                List<MissionEquipmentTarget> items = entry.Value;
                // Evita duplicar ícones em peças arquitetônicas da estufa se já existe o painel de controle ativo.
                if (entry.Key == EquipmentId.Greenhouse && items.Count > 1)
                {
                    MissionEquipmentTarget panelOnly = null;
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i].gameObject.name.IndexOf("Painel", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            panelOnly = items[i];
                            break;
                        }
                    }
                    if (panelOnly)
                    {
                        AddClusteredGroup(entry.Key, panelOnly, items);
                        continue;
                    }
                }

                // Agrupa peças próximas do mesmo equipamento (ex.: Gerador_O2 + Cano_Tanque_O2) para não empilhar ícones.
                List<MarkerGroup> createdForId = new List<MarkerGroup>();
                for (int i = 0; i < items.Count; i++)
                {
                    MissionEquipmentTarget t = items[i];
                    Vector3 anchor = ComputeTargetAnchorPosition(t.transform, false);
                    MarkerGroup mergeTarget = null;
                    for (int gIdx = 0; gIdx < createdForId.Count; gIdx++)
                    {
                        if ((createdForId[gIdx].Position - anchor).sqrMagnitude <= 64f)
                        {
                            mergeTarget = createdForId[gIdx];
                            break;
                        }
                    }
                    if (mergeTarget != null)
                    {
                        mergeTarget.Members.Add(t);
                        if (IsPreferredPrimaryPiece(t.gameObject.name))
                            mergeTarget.Position = anchor;
                    }
                    else
                    {
                        MarkerGroup group = new MarkerGroup
                        {
                            IsEquipment = true,
                            Equipment = entry.Key,
                            DisplayName = MissionRuleSet.Find(entry.Key).DisplayName,
                            Position = anchor,
                            Phase = groups.Count * 0.7f
                        };
                        group.Members.Add(t);
                        CreateMarkerVisual(group);
                        groups.Add(group);
                        createdForId.Add(group);
                    }
                }
            }
        }

        private void AddClusteredGroup(EquipmentId id, MissionEquipmentTarget primary, List<MissionEquipmentTarget> allMembers)
        {
            MarkerGroup group = new MarkerGroup
            {
                IsEquipment = true,
                Equipment = id,
                DisplayName = MissionRuleSet.Find(id).DisplayName,
                Position = ComputeTargetAnchorPosition(primary.transform, false),
                Phase = groups.Count * 0.7f
            };
            for (int i = 0; i < allMembers.Count; i++)
                group.Members.Add(allMembers[i]);
            CreateMarkerVisual(group);
            groups.Add(group);
        }

        private static bool IsPreferredPrimaryPiece(string objectName)
        {
            return objectName.IndexOf("Painel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("Gerador", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("Reciclador", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("Console", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Vector3 ComputeTargetAnchorPosition(Transform tr, bool isSolarPanel)
        {
            Renderer[] renderers = tr.GetComponentsInChildren<Renderer>(false);
            if (renderers != null && renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                float maxHeight = isSolarPanel ? 2.1f : 2.5f;
                float y = Mathf.Min(bounds.max.y + 0.52f, bounds.min.y + maxHeight);
                return new Vector3(bounds.center.x, y, bounds.center.z);
            }

            Collider col = tr.GetComponentInChildren<Collider>(false);
            if (col)
            {
                Bounds bounds = col.bounds;
                float y = Mathf.Min(bounds.max.y + 0.52f, bounds.min.y + 2.4f);
                return new Vector3(bounds.center.x, y, bounds.center.z);
            }

            return tr.position + Vector3.up * 1.6f;
        }

        private void CreateMarkerVisual(MarkerGroup group)
        {
            GameObject root = new GameObject("Marcador_" + (group.IsEquipment ? group.Equipment.ToString() : group.Artifact.ToString()));
            root.transform.SetParent(markersRoot, false);
            root.transform.position = group.Position;

            GameObject iconGo = new GameObject("Icone");
            iconGo.transform.SetParent(root.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            SpriteRenderer sr = iconGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 25;

            GameObject textGo = new GameObject("Texto");
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = new Vector3(0f, -0.32f, 0f);
            TextMesh tm = textGo.AddComponent<TextMesh>();
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.UpperCenter;
            tm.characterSize = 0.055f;
            tm.fontSize = 40;
            tm.fontStyle = FontStyle.Bold;

            group.Root = root;
            group.IconRenderer = sr;
            group.LabelMesh = tm;

            ApplyMarkerAppearance(group, ResolveKind(group), false, false);
            root.SetActive(false);
        }

        private MarkerKind ResolveKind(MarkerGroup g)
        {
            if (!mission) mission = MissionController.Instance;
            if (!mission || mission.Engine == null) return MarkerKind.PendingRepair;
            if (g.IsEquipment)
                return mission.Engine.IsRepaired(g.Equipment) ? MarkerKind.Repaired : MarkerKind.PendingRepair;

            bool scanned = mission.Engine.State.ScannedArtifacts.Contains(g.Artifact.ToString());
            return scanned ? MarkerKind.ArtifactScanned : MarkerKind.ArtifactPending;
        }

        private void ApplyMarkerAppearance(MarkerGroup g, MarkerKind kind, bool isAimed, bool isTutorialFocus)
        {
            g.LastKind = kind;
            g.LastAimed = isAimed;
            g.LastTutorialFocus = isTutorialFocus;

            Sprite sprite;
            Color color;
            string tag;
            switch (kind)
            {
                case MarkerKind.PendingRepair:
                    sprite = triangleSprite;
                    color = PendingColor;
                    tag = "! REPARAR";
                    break;
                case MarkerKind.Repaired:
                    sprite = circleSprite;
                    color = RepairedColor;
                    tag = "OK · VER";
                    break;
                case MarkerKind.ArtifactScanned:
                    sprite = starSprite;
                    color = ArtifactDoneColor;
                    tag = "APOLLO · OK";
                    break;
                default:
                    sprite = starSprite;
                    color = ArtifactColor;
                    tag = "APOLLO 15";
                    break;
            }

            if (g.IconRenderer)
            {
                g.IconRenderer.sprite = sprite;
                Color tinted = (isAimed || isTutorialFocus) ? Color.Lerp(color, Color.white, 0.28f) : color;
                tinted.a = (isAimed || isTutorialFocus) ? 1f : (kind == MarkerKind.ArtifactScanned ? 0.72f : 0.92f);
                g.IconRenderer.color = tinted;
            }

            if (g.LabelMesh)
            {
                if (isAimed || isTutorialFocus)
                {
                    g.LabelMesh.text = (isTutorialFocus && !isAimed ? "GUIA · " : string.Empty) + tag + "\n" + g.DisplayName;
                    g.LabelMesh.color = Color.Lerp(color, Color.white, 0.45f);
                }
                else
                {
                    g.LabelMesh.text = tag;
                    Color lc = Color.Lerp(color, Color.white, 0.25f);
                    lc.a = kind == MarkerKind.ArtifactScanned ? 0.78f : 0.95f;
                    g.LabelMesh.color = lc;
                }
            }
        }

        private static bool HasActiveMember(MarkerGroup g)
        {
            for (int i = 0; i < g.Members.Count; i++)
                if (g.Members[i] && g.Members[i].gameObject.activeInHierarchy) return true;
            return false;
        }

        private static bool IsGroupAimed(MarkerGroup g, MissionEquipmentTarget aimedEq, MissionHistoricalTarget aimedHist)
        {
            if (g.IsEquipment) return aimedEq && aimedEq.Equipment == g.Equipment;
            return aimedHist && aimedHist.Artifact == g.Artifact;
        }

        private void ClearGroups()
        {
            for (int i = 0; i < groups.Count; i++)
                if (groups[i].Root) Destroy(groups[i].Root);
            groups.Clear();
        }

        private void BuildProceduralSprites()
        {
            triangleSprite = CreateShapeSprite(MarkerKind.PendingRepair);
            circleSprite = CreateShapeSprite(MarkerKind.Repaired);
            starSprite = CreateShapeSprite(MarkerKind.ArtifactPending);
        }

        private Sprite CreateShapeSprite(MarkerKind kind)
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float dist = ShapeSignedDistance(kind, nx, ny);
                    Color px;
                    if (dist > 0.04f)
                    {
                        px = Color.clear;
                    }
                    else if (dist > -0.07f)
                    {
                        float aa = Mathf.Clamp01((0.04f - dist) / 0.04f);
                        px = OutlineColor;
                        px.a *= aa;
                    }
                    else if (dist > -0.15f)
                    {
                        px = Color.white;
                    }
                    else if (IsGlyphPixel(kind, nx, ny))
                    {
                        px = Color.white;
                    }
                    else
                    {
                        px = new Color(0.56f, 0.56f, 0.56f, 0.92f);
                    }
                    pixels[y * size + x] = px;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            generatedTextures.Add(tex);
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
            generatedSprites.Add(sp);
            return sp;
        }

        private static float ShapeSignedDistance(MarkerKind kind, float x, float y)
        {
            if (kind == MarkerKind.PendingRepair)
            {
                // Triângulo apontando para cima
                Vector2 p = new Vector2(Mathf.Abs(x), y + 0.14f);
                Vector2 apex = new Vector2(0f, 0.74f);
                Vector2 baseRight = new Vector2(0.70f, -0.48f);
                Vector2 edge = baseRight - apex;
                Vector2 outward = new Vector2(-edge.y, edge.x).normalized;
                float dSide = Vector2.Dot(p - apex, outward);
                float dBottom = baseRight.y - p.y;
                return Mathf.Max(dSide, dBottom) - 0.05f;
            }
            if (kind == MarkerKind.Repaired)
            {
                return Mathf.Sqrt(x * x + y * y) - 0.78f;
            }
            // Estrela de 5 pontas para artefatos Apollo
            float angle = Mathf.Atan2(y, x) - Mathf.PI * 0.5f;
            float sector = Mathf.PI * 2f / 5f;
            float halfSector = sector * 0.5f;
            float aNorm = Mathf.Abs(Mathf.Repeat(angle + halfSector, sector) - halfSector);
            float radius = Mathf.Sqrt(x * x + y * y);
            Vector2 q = new Vector2(radius * Mathf.Cos(aNorm), radius * Mathf.Sin(aNorm));
            Vector2 tip = new Vector2(0.86f, 0f);
            Vector2 valley = new Vector2(0.38f * Mathf.Cos(halfSector), 0.38f * Mathf.Sin(halfSector));
            Vector2 starEdge = valley - tip;
            Vector2 starOutward = new Vector2(starEdge.y, -starEdge.x).normalized;
            return Vector2.Dot(q - tip, starOutward) - 0.03f;
        }

        private static bool IsGlyphPixel(MarkerKind kind, float x, float y)
        {
            if (kind == MarkerKind.PendingRepair)
            {
                // Sinal "!" dentro do triângulo
                bool bar = Mathf.Abs(x) < 0.085f && y > -0.12f && y < 0.28f;
                bool dot = x * x + (y + 0.28f) * (y + 0.28f) < 0.085f * 0.085f;
                return bar || dot;
            }
            if (kind == MarkerKind.Repaired)
            {
                // Check "✓" dentro do círculo
                float d1 = SegmentDistance(new Vector2(x, y), new Vector2(-0.30f, -0.02f), new Vector2(-0.07f, -0.25f));
                float d2 = SegmentDistance(new Vector2(x, y), new Vector2(-0.07f, -0.25f), new Vector2(0.34f, 0.24f));
                return Mathf.Min(d1, d2) < 0.085f;
            }
            // Núcleo brilhante na estrela
            return x * x + y * y < 0.16f * 0.16f;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, Vector2.Dot(ab, ab)));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
