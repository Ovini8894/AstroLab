using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AstroLab.Nicholas
{
    /// <summary>
    /// Efeito curto (~1,5 s) ao consertar um equipamento: faíscas/partículas procedurais,
    /// pulso de luz ciano, leve "pop" de escala no objeto OK_* e som existente do projeto (somConcluido).
    /// Nunca roda durante carga inicial ou LoadGame e executa apenas 1x para o grupo dos painéis solares.
    /// </summary>
    public sealed class MissionRepairEffects : MonoBehaviour
    {
        private const float EffectDuration = 1.5f;
        private const float PopDuration = 0.48f;

        private sealed class ActiveScalePop
        {
            public Transform Target;
            public Vector3 OriginalScale;
        }

        private Texture2D sparkTexture;
        private Material sparkMaterial;
        private AudioClip completionClip;
        private bool clipSearched;
        private AudioSource audioSource;

        private readonly List<Coroutine> activeCoroutines = new List<Coroutine>();
        private readonly List<GameObject> transientObjects = new List<GameObject>();
        private readonly List<ActiveScalePop> activeScalePops = new List<ActiveScalePop>();

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            BuildSparkResources();
        }

        private void OnDestroy()
        {
            StopAllEffects();
            if (sparkMaterial) Destroy(sparkMaterial);
            if (sparkTexture) Destroy(sparkTexture);
        }

        public void StopAllEffects()
        {
            for (int i = 0; i < activeCoroutines.Count; i++)
                if (activeCoroutines[i] != null) StopCoroutine(activeCoroutines[i]);
            activeCoroutines.Clear();

            for (int i = 0; i < activeScalePops.Count; i++)
                if (activeScalePops[i].Target) activeScalePops[i].Target.localScale = activeScalePops[i].OriginalScale;
            activeScalePops.Clear();

            for (int i = 0; i < transientObjects.Count; i++)
                if (transientObjects[i]) Destroy(transientObjects[i]);
            transientObjects.Clear();
        }

        public void PlayAtTarget(Transform target, float burstRadius = 0.45f)
        {
            if (!target) return;
            Vector3 center = ComputeCenter(target);
            Transform[] scaleTargets = { target };
            PlayEffect(center, scaleTargets, burstRadius);
        }

        public void PlayForGroup(Vector3 center, Transform[] scaleTargets, float burstRadius = 1.6f)
        {
            PlayEffect(center, scaleTargets, burstRadius);
        }

        private void PlayEffect(Vector3 center, Transform[] scaleTargets, float burstRadius)
        {
            PlayCompletionAudio();

            GameObject effectGo = new GameObject("Nicholas_Efeito_Conserto");
            effectGo.transform.position = center;
            transientObjects.Add(effectGo);

            Light pulseLight = CreateCyanLight(effectGo.transform);
            CreateSparkParticles(effectGo, burstRadius);

            List<ActiveScalePop> pops = new List<ActiveScalePop>();
            if (scaleTargets != null)
            {
                for (int i = 0; i < scaleTargets.Length; i++)
                {
                    Transform t = scaleTargets[i];
                    if (!t) continue;
                    ActiveScalePop existing = FindScalePop(t);
                    if (existing != null)
                    {
                        pops.Add(existing);
                    }
                    else
                    {
                        ActiveScalePop pop = new ActiveScalePop { Target = t, OriginalScale = t.localScale };
                        activeScalePops.Add(pop);
                        pops.Add(pop);
                    }
                }
            }

            Coroutine routine = null;
            routine = StartCoroutine(AnimateRepairRoutine(effectGo, pulseLight, pops, () =>
            {
                activeCoroutines.Remove(routine);
            }));
            activeCoroutines.Add(routine);
        }

        private IEnumerator AnimateRepairRoutine(GameObject effectGo, Light pulseLight, List<ActiveScalePop> pops, Action onDone)
        {
            float elapsed = 0f;
            try
            {
                while (elapsed < EffectDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / EffectDuration);

                    if (pulseLight)
                    {
                        float attack = Mathf.Clamp01(elapsed / 0.16f);
                        float decay = Mathf.Pow(1f - t, 1.6f);
                        float shimmer = 0.85f + 0.15f * Mathf.Sin(elapsed * 18f);
                        pulseLight.intensity = 5.2f * attack * decay * shimmer;
                    }

                    float popFactor = 1f;
                    if (elapsed < PopDuration)
                    {
                        float p = elapsed / PopDuration;
                        popFactor = 1f + 0.09f * Mathf.Sin(p * Mathf.PI) * (1f - p * 0.35f);
                    }

                    for (int i = 0; i < pops.Count; i++)
                        if (pops[i].Target) pops[i].Target.localScale = pops[i].OriginalScale * popFactor;

                    yield return null;
                }
            }
            finally
            {
                for (int i = 0; i < pops.Count; i++)
                {
                    if (pops[i].Target) pops[i].Target.localScale = pops[i].OriginalScale;
                    activeScalePops.Remove(pops[i]);
                }
                transientObjects.Remove(effectGo);
                if (effectGo) Destroy(effectGo);
                onDone?.Invoke();
            }
        }

        private void PlayCompletionAudio()
        {
            AudioClip clip = ResolveCompletionClip();
            if (clip && audioSource)
                audioSource.PlayOneShot(clip, 0.75f);
        }

        private AudioClip ResolveCompletionClip()
        {
            if (clipSearched) return completionClip;
            clipSearched = true;
            foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!mb || mb.GetType().Name != "ScannerPrimeiraPessoa") continue;
                FieldInfo clipField = mb.GetType().GetField("somConcluido", BindingFlags.Instance | BindingFlags.Public);
                if (clipField != null)
                {
                    completionClip = clipField.GetValue(mb) as AudioClip;
                    if (completionClip) break;
                }
            }
            return completionClip;
        }

        private static Light CreateCyanLight(Transform parent)
        {
            GameObject lightGo = new GameObject("Pulso_Luz_Ciano");
            lightGo.transform.SetParent(parent, false);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.20f, 0.93f, 1.00f);
            light.range = 7.5f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            return light;
        }

        private void CreateSparkParticles(GameObject host, float burstRadius)
        {
            ParticleSystem ps = host.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = host.GetComponent<ParticleSystemRenderer>();
            if (psr && sparkMaterial)
            {
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.sharedMaterial = sparkMaterial;
                psr.sortingOrder = 20;
            }

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.20f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.gravityModifier = 0.35f;
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.35f, 0.96f, 1.00f, 1f),
                new Color(1.00f, 0.96f, 0.72f, 1f));

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = burstRadius;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.35f, 0.92f, 1f), 0.55f),
                    new GradientColorKey(new Color(0.20f, 0.75f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = fade;

            ps.Play();
        }

        private void BuildSparkResources()
        {
            const int size = 32;
            sparkTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
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
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            sparkTexture.SetPixels32(pixels);
            sparkTexture.Apply();

            Shader shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader)
            {
                sparkMaterial = new Material(shader);
                sparkMaterial.mainTexture = sparkTexture;
            }
        }

        private ActiveScalePop FindScalePop(Transform target)
        {
            for (int i = 0; i < activeScalePops.Count; i++)
                if (activeScalePops[i].Target == target) return activeScalePops[i];
            return null;
        }

        private static Vector3 ComputeCenter(Transform tr)
        {
            Renderer[] renderers = tr.GetComponentsInChildren<Renderer>(false);
            if (renderers != null && renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return bounds.center + Vector3.up * 0.2f;
            }
            return tr.position + Vector3.up * 0.6f;
        }
    }
}
