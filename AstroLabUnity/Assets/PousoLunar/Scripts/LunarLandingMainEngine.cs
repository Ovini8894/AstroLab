using System;
using UnityEngine;

namespace AstroLab.PousoLunar
{
    /// <summary>Pluma de vácuo em camadas. Recebe o empuxo do controlador existente.</summary>
    [DisallowMultipleComponent]
    public sealed class LunarLandingMainEngine : MonoBehaviour
    {
        [Serializable]
        public sealed class Nozzle
        {
            public ParticleSystem core;
            public ParticleSystem outer;
            public ParticleSystem glow;
            public Light light;
        }

        public LunarLandingController controller;
        public Nozzle[] nozzles = Array.Empty<Nozzle>();
        [Min(.01f)] public float responseSpeed = 2.5f;
        [Tooltip("Comprimento nominal em metros. As proporções dos emissores são preservadas.")]
        [Min(.05f)] public float plumeLength = 3f;
        [Tooltip("Largura nominal em metros; ajusta partículas e abertura dos emissores.")]
        [Min(.01f)] public float plumeWidth = .65f;
        [Min(0)] public float coreEmissionRate = 150;
        [Min(0)] public float outerEmissionRate = 90;
        [Min(0)] public float glowEmissionRate = 35;
        [Range(0, .15f)] public float flickerAmount = .035f;
        [Header("Teste pelo menu de contexto")]
        [Range(0, 1)] public float previewIntensity = .65f;
        [Min(.1f)] public float previewDuration = 3f;

        public float CurrentThrust { get; private set; }
        public float TargetThrust { get; private set; }

        const float ReferenceLength = 3f;
        const float ReferenceWidth = .65f;

        sealed class ParticleBaseline
        {
            public ParticleSystem system;
            public float size, speed, lifetime, radius;
            public ParticleBaseline(ParticleSystem ps)
            {
                system = ps;
                if (!ps) return;
                var main = ps.main;
                size = main.startSizeMultiplier;
                speed = main.startSpeedMultiplier;
                lifetime = main.startLifetimeMultiplier;
                radius = ps.shape.radius;
            }
        }

        sealed class NozzleBaseline
        {
            public Nozzle source;
            public ParticleBaseline core, outer, glow;
            public float lightIntensity, lightRange;
        }

        NozzleBaseline[] baselines = Array.Empty<NozzleBaseline>();
        LunarLandingController subscribedController;
        float previewRemaining;
        double flickerTime;

        void OnEnable() => Subscribe();

        void Subscribe()
        {
            if (subscribedController == controller) return;
            if (subscribedController) subscribedController.StateChanged -= OnStateChanged;
            subscribedController = controller;
            if (subscribedController) subscribedController.StateChanged += OnStateChanged;
        }

        void OnStateChanged(LunarLandingState state)
        {
            if (state == LunarLandingState.Idle || state == LunarLandingState.TransitionIn || state == LunarLandingState.Completed)
                ResetEffects();
        }

        void EnsureBaselines()
        {
            int count = nozzles == null ? 0 : nozzles.Length;
            if (baselines.Length != count) baselines = new NozzleBaseline[count];
            for (int i = 0; i < count; i++)
            {
                Nozzle n = nozzles[i];
                NozzleBaseline b = baselines[i];
                if (b != null && b.source == n && n != null
                    && b.core.system == n.core && b.outer.system == n.outer && b.glow.system == n.glow) continue;
                baselines[i] = new NozzleBaseline
                {
                    source = n,
                    core = new ParticleBaseline(n == null ? null : n.core),
                    outer = new ParticleBaseline(n == null ? null : n.outer),
                    glow = new ParticleBaseline(n == null ? null : n.glow),
                    lightIntensity = n != null && n.light ? n.light.intensity : 0,
                    lightRange = n != null && n.light ? n.light.range : 0
                };
            }
        }

        public void SetThrust(float thrust)
        {
            TargetThrust = float.IsNaN(thrust) || float.IsInfinity(thrust) ? 0 : Mathf.Clamp01(thrust);
        }

        void LateUpdate()
        {
            Subscribe();
            float dt = Time.unscaledDeltaTime;
            if (previewRemaining > 0)
            {
                previewRemaining = Mathf.Max(0, previewRemaining - dt);
                TargetThrust = previewRemaining > 0 ? previewIntensity : 0;
            }
            CurrentThrust = Mathf.MoveTowards(CurrentThrust, TargetThrust, Mathf.Max(.01f, responseSpeed) * dt);
            flickerTime += dt;
            ApplyVisuals(CurrentThrust);
        }

        void ApplyVisuals(float intensity)
        {
            EnsureBaselines();
            // Sem Random: cintilação leve não altera o cronograma do empuxo.
            float flicker = 1 + flickerAmount * (.65f * Mathf.Sin((float)(flickerTime * 31))
                + .35f * Mathf.Sin((float)(flickerTime * 47)));
            float plume = Mathf.Lerp(.35f, 1, Mathf.Sqrt(intensity));
            float lengthScale = plumeLength / ReferenceLength;
            float widthScale = plumeWidth / ReferenceWidth;
            foreach (NozzleBaseline b in baselines)
            {
                ConfigureLayer(b.core, intensity * flicker, coreEmissionRate, plume, lengthScale, widthScale, false);
                ConfigureLayer(b.outer, intensity, outerEmissionRate, plume, lengthScale, widthScale, false);
                ConfigureLayer(b.glow, intensity * flicker, glowEmissionRate, 1, 1, widthScale, true);
                if (b.source == null || !b.source.light) continue;
                b.source.light.intensity = b.lightIntensity * intensity * flicker;
                b.source.light.range = b.lightRange * Mathf.Lerp(.65f, 1, intensity);
                b.source.light.enabled = intensity > .001f;
            }
        }

        static void ConfigureLayer(ParticleBaseline b, float intensity, float rate, float plume,
            float lengthScale, float widthScale, bool glow)
        {
            ParticleSystem ps = b.system;
            if (!ps) return;
            var main = ps.main;
            // Sempre deriva dos valores originais; nenhuma escala se acumula entre quadros.
            main.startSizeMultiplier = b.size * widthScale * (glow ? 1 : Mathf.Lerp(.5f, 1, plume));
            main.startSpeedMultiplier = b.speed * (glow ? 1 : Mathf.Sqrt(plume));
            main.startLifetimeMultiplier = b.lifetime * lengthScale * (glow ? 1 : Mathf.Sqrt(plume));
            var shape = ps.shape;
            if (shape.enabled) shape.radius = b.radius * widthScale;
            var emission = ps.emission;
            emission.rateOverTime = Mathf.Max(0, intensity) * rate;
            if (intensity > .001f && !ps.isPlaying) ps.Play(false);
            else if (intensity <= .001f && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        [ContextMenu("Testar motor (3 segundos por padrão)")]
        public void TestEngine()
        {
            if (Application.isPlaying)
            {
                previewRemaining = Mathf.Max(.1f, previewDuration);
                SetThrust(previewIntensity);
                return;
            }
            ApplyVisuals(previewIntensity);
            foreach (NozzleBaseline b in baselines)
            {
                EmitSample(b.core, coreEmissionRate);
                EmitSample(b.outer, outerEmissionRate);
                EmitSample(b.glow, glowEmissionRate);
                if (b.source != null && b.source.light) b.source.light.enabled = false;
            }
        }

        void EmitSample(ParticleBaseline b, float rate)
        {
            if (!b.system) return;
            b.system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            b.system.Emit(Mathf.Max(1, Mathf.CeilToInt(rate * .12f * previewIntensity)));
        }

        [ContextMenu("Limpar motor")]
        public void ResetEffects()
        {
            TargetThrust = CurrentThrust = previewRemaining = 0;
            flickerTime = 0;
            ApplyVisuals(0);
            foreach (NozzleBaseline b in baselines)
            {
                if (b.core.system) b.core.system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (b.outer.system) b.outer.system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (b.glow.system) b.glow.system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        void OnDisable()
        {
            if (subscribedController) subscribedController.StateChanged -= OnStateChanged;
            subscribedController = null;
            ResetEffects();
        }
    }
}
