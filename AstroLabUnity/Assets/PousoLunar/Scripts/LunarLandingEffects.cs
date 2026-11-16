using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.PousoLunar
{
    /// <summary>Adapter opcional: controla emissores existentes, áudio e eventos de emissão personalizados.</summary>
    public sealed class LunarLandingEffects : MonoBehaviour
    {
        public ParticleSystem engine;
        public ParticleSystem dust;
        public ParticleSystem touchdownBurst;
        public AudioSource engineAudio;
        [Min(0)] public float engineRate = 45;
        [Min(0)] public float dustRate = 25;
        [Range(0,1)] public float engineVolume = .6f;
        public LandingIntensityEvent engineIntensity = new LandingIntensityEvent();
        public LandingIntensityEvent dustIntensity = new LandingIntensityEvent();
        public UnityEvent onTouchdown = new UnityEvent();
        Vector3 groundPosition;
        Quaternion groundRotation;
        bool hasGround;

        public void SetGround(Vector3 position, Quaternion rotation)
        {
            groundPosition = position; groundRotation = rotation; hasGround = true;
        }

        public void SetIntensity(float thrust, float groundDust)
        {
            thrust = Mathf.Clamp01(thrust); groundDust = Mathf.Clamp01(groundDust);
            if (dust && hasGround) dust.transform.SetPositionAndRotation(groundPosition,groundRotation);
            SetParticle(engine,thrust,engineRate); SetParticle(dust,groundDust,dustRate);
            if (engineAudio)
            {
                engineAudio.volume = thrust * engineVolume;
                if (thrust > 0 && !engineAudio.isPlaying) engineAudio.Play();
                else if (thrust == 0 && engineAudio.isPlaying) engineAudio.Stop();
            }
            engineIntensity.Invoke(thrust); dustIntensity.Invoke(groundDust);
        }

        static void SetParticle(ParticleSystem ps, float intensity, float rate)
        {
            if (!ps) return;
            var emission = ps.emission;
            emission.rateOverTime = intensity * rate;
            if (intensity > 0 && !ps.isPlaying) ps.Play(true);
            else if (intensity == 0 && ps.isPlaying) ps.Stop(true,ParticleSystemStopBehavior.StopEmitting);
        }

        public void Touchdown()
        {
            if (touchdownBurst)
            {
                if (hasGround) touchdownBurst.transform.SetPositionAndRotation(groundPosition,groundRotation);
                touchdownBurst.Play(true);
            }
            onTouchdown.Invoke();
        }
        public void ResetEffects()
        {
            SetIntensity(0,0);
            if (engine) engine.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if (dust) dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if (touchdownBurst) touchdownBurst.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDisable() => ResetEffects();
    }
}
