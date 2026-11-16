using UnityEngine;

namespace AstroLab.PousoLunar
{
    /// <summary>Grãos balísticos; a emissão depende da altura real e do empuxo, sem fumaça atmosférica.</summary>
    [DisallowMultipleComponent]
    public sealed class LunarLandingDust : MonoBehaviour
    {
        public LunarLandingController controller;
        public LunarLandingMainEngine mainEngine;
        public ParticleSystem grains;
        public ParticleSystem touchdownGrains;
        [Min(.1f)] public float interactionHeight = 5;
        [Min(0)] public float particlesPerSecond = 180;
        [Min(0)] public float outwardVelocity = 4;
        [Min(.01f)] public float lifetime = .8f;
        [Range(0, 1)] public float upwardSpread = .12f;
        [Min(0)] public int touchdownCount = 65;
        public AnimationCurve heightEmission = AnimationCurve.EaseInOut(0, 1, 1, 0);
        public float CurrentEmission { get; private set; }
        Vector3 ground;
        Quaternion groundRotation = Quaternion.identity;
        float remainder, previewRemaining;
        int sequence;

        public void SetGround(Vector3 point, Quaternion rotation) { ground = point; groundRotation = rotation; }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (previewRemaining > 0) previewRemaining = Mathf.Max(0, previewRemaining - dt);
            bool active = controller && controller.State >= LunarLandingState.Deceleration
                && controller.State <= LunarLandingState.EngineShutdown;
            float thrust = mainEngine ? mainEngine.CurrentThrust : 0;
            JetFootprint(out float height);
            CurrentEmission = active ? particlesPerSecond * thrust * Mathf.Clamp01(heightEmission.Evaluate(height / interactionHeight)) : 0;
            if (previewRemaining > 0) CurrentEmission = particlesPerSecond * .75f;
            remainder += CurrentEmission * dt;
            int count = Mathf.FloorToInt(remainder);
            remainder -= count;
            if (count > 0) Emit(grains, Mathf.Min(count, 128), 1);
            if (CurrentEmission == 0 && grains && grains.particleCount == 0 && grains.isPlaying)
                grains.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (touchdownGrains && touchdownGrains.particleCount == 0 && touchdownGrains.isPlaying)
                touchdownGrains.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        void Emit(ParticleSystem system, int count, float multiplier)
        {
            if (!system) return;
            if (!system.isPlaying) system.Play(false);
            Vector3 up = groundRotation * Vector3.up;
            Vector3 center = previewRemaining > 0 ? ground : JetFootprint(out _);
            // O vetor de força é mundial: não depende de Physics.gravity do gameplay.
            var force = system.forceOverLifetime;
            force.enabled = true; force.space = ParticleSystemSimulationSpace.World;
            force.x = -1.62f * up.x; force.y = -1.62f * up.y; force.z = -1.62f * up.z;
            for (int i = 0; i < count; i++)
            {
                float angle = sequence++ * 2.39996323f; // distribuição determinística uniforme
                Vector3 radial = groundRotation * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                float variation = .7f + .3f * Mathf.Abs(Mathf.Sin(sequence * 1.713f));
                var particle = new ParticleSystem.EmitParams
                {
                    position = center + up * .025f + radial * (.25f + .3f * variation),
                    velocity = (radial + up * upwardSpread) * (outwardVelocity * variation * multiplier),
                    startLifetime = lifetime * variation,
                    startSize = .025f + .04f * variation
                };
                system.Emit(particle, 1);
            }
        }

        Vector3 JetFootprint(out float height)
        {
            Vector3 source=Vector3.zero; int count=0;
            if (mainEngine && mainEngine.nozzles != null) foreach (var nozzle in mainEngine.nozzles)
                if (nozzle != null && nozzle.core) { source+=nozzle.core.transform.position; count++; }
            source = count > 0 ? source/count : controller && controller.motionRoot ? controller.motionRoot.position : ground;
            Vector3 up=groundRotation*Vector3.up;
            height=Mathf.Max(0,Vector3.Dot(source-ground,up));
            return source-up*Vector3.Dot(source-ground,up);
        }

        public void Touchdown() => Emit(touchdownGrains, touchdownCount, 1.25f);

        [ContextMenu("Testar poeira (2 segundos em Play)")]
        public void TestDust()
        {
            if (controller && controller.landingPoint) SetGround(controller.landingPoint.position, controller.landingPoint.rotation);
            previewRemaining = 2;
            Emit(grains, 40, 1);
        }

        [ContextMenu("Limpar poeira")]
        public void ResetEffects()
        {
            CurrentEmission = remainder = previewRemaining = 0; sequence = 0;
            if (grains) grains.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (touchdownGrains) touchdownGrains.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDisable() => ResetEffects();
    }
}
