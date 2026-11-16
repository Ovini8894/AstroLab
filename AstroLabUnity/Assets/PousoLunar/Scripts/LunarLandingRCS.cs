using System;
using UnityEngine;

namespace AstroLab.PousoLunar
{
    /// <summary>Jatos de atitude derivados da rotação real do rig, sem alterar sua trajetória.</summary>
    [DisallowMultipleComponent]
    public sealed class LunarLandingRCS : MonoBehaviour
    {
        [Serializable]
        public sealed class Thruster
        {
            public string label;
            [Tooltip("Eixo +Z aponta para fora do bocal, na direção do gás expelido.")]
            public Transform socket;
            public ParticleSystem jet;
            [Min(0)] public float emissionRate = 85;
            [Range(0, 2)] public float intensity = 1;
            [NonSerialized] internal float remaining;
            [NonSerialized] internal float strength;
            [NonSerialized] internal float cooldown;
        }

        public LunarLandingController controller;
        [Tooltip("Centro de massa aproximado, em coordenadas locais de motionRoot.")]
        public Vector3 localCenterOfMass = new Vector3(0, 3, 0);
        public Thruster[] thrusters = Array.Empty<Thruster>();
        [Min(.02f)] public float burstDuration = .11f;
        [Min(.05f)] public float repeatInterval = .28f;
        [Tooltip("Graus/s²: evita pulsos causados por ruído numérico.")]
        [Min(.001f)] public float accelerationDeadZone = .12f;
        [Min(.01f)] public float fullThrustAcceleration = 4f;
        [Range(0, 1)] public float stabilizationIntensity = .35f;
        public AnimationCurve emissionCurve = new AnimationCurve(
            new Keyframe(0, .35f), new Keyframe(.18f, 1), new Keyframe(1, 0));
        [Header("Teste individual pelo menu de contexto ou ferramenta de pouso")]
        [Min(0)] public int manualThrusterIndex;
        [Range(0, 1)] public float manualIntensity = .7f;

        public int BurstCount { get; private set; }
        public Vector3 MeasuredAngularVelocity { get; private set; }
        public Vector3 MeasuredAngularAcceleration { get; private set; }

        LunarLandingController subscribedController;
        Quaternion previousRotation;
        Vector3 previousVelocity;
        bool hasSample;

        void OnEnable() { Subscribe(); hasSample = false; }

        void Subscribe()
        {
            if (subscribedController == controller) return;
            if (subscribedController) subscribedController.StateChanged -= OnStateChanged;
            subscribedController = controller;
            if (subscribedController) subscribedController.StateChanged += OnStateChanged;
        }

        void OnStateChanged(LunarLandingState state)
        {
            // A primeira amostra de uma fase não interpreta um reset/teleporte como torque.
            if (state == LunarLandingState.Idle || state == LunarLandingState.TransitionIn)
            {
                ResetEffects();
                hasSample = false;
            }
            else if (state >= LunarLandingState.Touchdown) StopBursts();
        }

        void LateUpdate()
        {
            Subscribe();
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0) return;
            if (controller && controller.motionRoot)
            {
                Transform root = controller.motionRoot;
                Quaternion rotation = root.rotation;
                if (hasSample && dt < .25f)
                {
                    Quaternion delta = rotation * Quaternion.Inverse(previousRotation);
                    // Quaternion q e -q são a mesma rotação; usa sempre o menor arco.
                    if (delta.w < 0) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
                    delta.ToAngleAxis(out float angle, out Vector3 axis);
                    Vector3 velocity = angle > .00001f && IsFinite(axis) ? axis * (angle / dt) : Vector3.zero;
                    Vector3 acceleration = (velocity - previousVelocity) / dt;
                    MeasuredAngularVelocity = velocity;
                    MeasuredAngularAcceleration = acceleration;
                    previousVelocity = velocity;
                    LunarLandingState state = controller.State;
                    if (state >= LunarLandingState.Approach && state <= LunarLandingState.VerticalDescent
                        && acceleration.magnitude >= accelerationDeadZone)
                    {
                        float activity = state == LunarLandingState.VerticalDescent ? stabilizationIntensity : 1f;
                        SelectTorquePair(root, acceleration.normalized,
                            Mathf.Clamp01(acceleration.magnitude / Mathf.Max(.01f, fullThrustAcceleration)) * activity);
                    }
                }
                else previousVelocity = Vector3.zero;
                previousRotation = rotation;
                hasSample = true;
            }
            UpdateBursts(dt);
        }

        static bool IsFinite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        Vector3 Torque(Thruster thruster, Vector3 center)
        {
            if (thruster == null || !thruster.socket || !thruster.jet) return Vector3.zero;
            return Vector3.Cross(thruster.socket.position - center, -thruster.socket.forward).normalized;
        }

        void SelectTorquePair(Transform root, Vector3 desiredTorque, float strength)
        {
            if (thrusters == null || strength <= 0) return;
            // Um par por vez: forças opostas reduzem translação enquanto somam torque.
            for (int i = 0; i < thrusters.Length; i++)
                if (thrusters[i] != null && thrusters[i].remaining > 0) return;
            Vector3 center = root.TransformPoint(localCenterOfMass);
            int first = -1;
            float best = .65f;
            for (int i = 0; i < thrusters.Length; i++)
            {
                Thruster t = thrusters[i];
                if (t == null || t.cooldown > 0) continue;
                float score = Vector3.Dot(Torque(t, center), desiredTorque);
                if (score > best) { best = score; first = i; }
            }
            if (first < 0) return;
            int second = -1;
            best = .65f;
            for (int i = 0; i < thrusters.Length; i++)
            {
                Thruster t = thrusters[i];
                if (i == first || t == null || !t.socket || !t.jet || t.cooldown > 0) continue;
                if (Vector3.Dot(t.socket.forward, thrusters[first].socket.forward) > -.5f) continue;
                float score = Vector3.Dot(Torque(t, center), desiredTorque);
                if (score > best) { best = score; second = i; }
            }
            StartBurst(first, strength);
            if (second >= 0) StartBurst(second, strength);
        }

        void StartBurst(int index, float strength)
        {
            Thruster t = thrusters[index];
            if (t == null || !t.socket || !t.jet) return;
            t.remaining = Mathf.Max(.02f, burstDuration);
            t.strength = Mathf.Clamp01(strength);
            t.cooldown = Mathf.Max(repeatInterval, burstDuration);
            BurstCount++;
        }

        void UpdateBursts(float dt)
        {
            if (thrusters == null) return;
            foreach (Thruster t in thrusters)
            {
                if (t == null || !t.jet) continue;
                t.cooldown = Mathf.Max(0, t.cooldown - dt);
                float intensity = 0;
                if (t.remaining > 0)
                {
                    float p = 1f - t.remaining / Mathf.Max(.02f, burstDuration);
                    intensity = t.strength * t.intensity * Mathf.Max(0, emissionCurve == null ? 1 : emissionCurve.Evaluate(p));
                    t.remaining = Mathf.Max(0, t.remaining - dt);
                }
                var emission = t.jet.emission;
                emission.rateOverTime = intensity * t.emissionRate;
                if (intensity > 0 && !t.jet.isPlaying) t.jet.Play(false);
                else if (intensity <= 0 && t.jet.isPlaying) t.jet.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>Teste determinístico de um único bocal. Em edição, emite uma amostra imediata.</summary>
        public void TestThruster(int index, float intensity = .7f)
        {
            if (thrusters == null || index < 0 || index >= thrusters.Length) return;
            Thruster t = thrusters[index];
            if (t == null || !t.jet || !t.socket) return;
            StopBursts();
            if (Application.isPlaying) StartBurst(index, intensity);
            else t.jet.Emit(Mathf.Max(1, Mathf.CeilToInt(t.emissionRate * burstDuration * Mathf.Clamp01(intensity) * t.intensity)));
        }

        [ContextMenu("Testar bocal selecionado")]
        void TestSelectedThruster() => TestThruster(manualThrusterIndex, manualIntensity);

        public void StopBursts()
        {
            if (thrusters == null) return;
            foreach (Thruster t in thrusters)
            {
                if (t == null) continue;
                t.remaining = 0;
                t.cooldown = 0;
                if (!t.jet) continue;
                var emission = t.jet.emission;
                emission.rateOverTime = 0;
                t.jet.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        [ContextMenu("Limpar RCS")]
        public void ResetEffects()
        {
            StopBursts();
            if (thrusters != null) foreach (Thruster t in thrusters) if (t != null && t.jet) t.jet.Clear(false);
            MeasuredAngularVelocity = MeasuredAngularAcceleration = previousVelocity = Vector3.zero;
            hasSample = false;
            BurstCount = 0;
        }

        void OnDisable()
        {
            if (subscribedController) subscribedController.StateChanged -= OnStateChanged;
            subscribedController = null;
            ResetEffects();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.72f, .82f, .91f, .8f);
            if (controller && controller.motionRoot) Gizmos.DrawWireSphere(controller.motionRoot.TransformPoint(localCenterOfMass), .15f);
            if (thrusters == null) return;
            foreach (Thruster t in thrusters)
                if (t != null && t.socket) { Gizmos.DrawWireSphere(t.socket.position, .06f); Gizmos.DrawRay(t.socket.position, t.socket.forward * .7f); }
        }
    }
}
