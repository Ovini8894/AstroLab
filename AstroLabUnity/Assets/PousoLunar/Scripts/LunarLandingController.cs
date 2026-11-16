using System;
using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.PousoLunar
{
    /// <summary>Relógio único e trajetória analítica. O eixo Y de landingPoint é a normal do solo.</summary>
    [DisallowMultipleComponent]
    public sealed class LunarLandingController : MonoBehaviour
    {
        [Header("Rig (o pivô de motionRoot representa o contato com o solo)")]
        public Transform motionRoot;
        public Transform visualSocket;
        public Transform engineEffectsSocket;
        public Transform landingEffectsSocket;
        public Transform approachPoint;
        public Transform decelerationPoint;
        public Transform descentPoint;
        public Transform landingPoint;
        public LunarLandingCamera cameraRig;
        public LunarLandingEffects effects;

        [Header("Durações em segundos; relógio não escalado")]
        [Min(0.01f)] public float approachDuration = 8f;
        [Min(0.01f)] public float decelerationDuration = 7f;
        [Min(0.01f)] public float verticalDescentDuration = 8f;
        [Min(0.01f)] public float touchdownDuration = 1.2f;
        [Min(0.01f)] public float engineShutdownDuration = 2f;
        [Min(0.01f)] public float finalCinematicDuration = 4f;
        [Tooltip("Velocidade na chegada ao waypoint de desaceleração (m/s).")]
        [Min(0f)] public float approachSpeed = 7f;
        [Tooltip("Se ativo, calcula o tempo vertical a partir da velocidade inicial de descida.")]
        public bool useDescentVelocity;
        [Min(0.01f)] public float descentVelocity = 5f;
        [Min(0f)] public float settlingHeight = 0.05f;
        public Vector3 initialInclination = new Vector3(0f, 0f, -12f);
        public Vector3 finalRotationOffset;
        [Tooltip("Linear preserva as velocidades nas junções Hermite. Evite curvas não monotônicas.")]
        public AnimationCurve approachCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve decelerationCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve verticalCurve = new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, 0, 0));
        public bool playOnStart = true;

        [Header("Eventos de integração")]
        public LandingStateEvent stateChanged = new LandingStateEvent();
        public UnityEvent touchdown = new UnityEvent();
        public UnityEvent completed = new UnityEvent();
        public event Action<LunarLandingState> StateChanged;
        public event Action Completed;
        public LunarLandingState State { get; private set; } = LunarLandingState.Idle;
        public bool IsRunning => State >= LunarLandingState.TransitionIn && State < LunarLandingState.Completed;
        public double Elapsed { get; private set; }
        public float TotalDuration => approachDuration + decelerationDuration + (initialized ? descentTime : DescentDuration) + touchdownDuration + engineShutdownDuration + finalCinematicDuration;
        public float DescentDuration => useDescentVelocity && landingPoint && descentPoint
            ? Mathf.Max(0.01f, 2f * Vector3.Dot(descentPoint.position - landingPoint.position, landingPoint.up) / Mathf.Max(.01f, descentVelocity))
            : Mathf.Max(.01f, verticalDescentDuration);

        Vector3 start, brake, vertical, ground, up, incoming;
        Quaternion initialRotation, finalRotation;
        float descentTime;
        bool initialized;
        int runVersion;

        void Start() { if (playOnStart && State == LunarLandingState.Idle) BeginLanding(); }
        void Update() { if (IsRunning && State != LunarLandingState.TransitionIn) Advance(Time.unscaledDeltaTime); }
        void OnDisable() { if (IsRunning) ResetLanding(); }

        public bool ValidateConfiguration(out string error)
        {
            error = null;
            if (!motionRoot || !approachPoint || !decelerationPoint || !descentPoint || !landingPoint)
                error = "Configure motionRoot e os quatro waypoints.";
            else if (motionRoot == transform || transform.IsChildOf(motionRoot))
                error = "motionRoot deve ser um filho independente da raiz do sistema.";
            else if (approachPoint.IsChildOf(motionRoot) || decelerationPoint.IsChildOf(motionRoot) || descentPoint.IsChildOf(motionRoot) || landingPoint.IsChildOf(motionRoot))
                error = "Waypoints não podem ser filhos do objeto animado.";
            else if (approachDuration <= 0 || decelerationDuration <= 0 || verticalDescentDuration <= 0 || touchdownDuration <= 0 || engineShutdownDuration <= 0 || finalCinematicDuration <= 0)
                error = "Todas as durações precisam ser positivas.";
            else if (Vector3.Dot(descentPoint.position - landingPoint.position, landingPoint.up) <= 0)
                error = "DescentPoint precisa estar acima de LandingPoint.";
            else if (Vector3.ProjectOnPlane(descentPoint.position - landingPoint.position, landingPoint.up).sqrMagnitude > .0001f)
                error = "Alinhe DescentPoint na vertical local de LandingPoint.";
            else if (!ValidCurve(approachCurve) || !ValidCurve(decelerationCurve) || !ValidCurve(rotationCurve) || !ValidCurve(verticalCurve))
                error = "As curvas devem ir de (0,0) a (1,1), sem recuar ou sair desse intervalo.";
            return error == null;
        }

        static bool ValidCurve(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return true;
            if (Mathf.Abs(curve.Evaluate(0)) > .0001f || Mathf.Abs(curve.Evaluate(1)-1) > .0001f) return false;
            float previous = 0;
            for (int i=1;i<=128;i++)
            {
                float value = curve.Evaluate(i/128f);
                if (float.IsNaN(value) || float.IsInfinity(value) || value < previous-.00001f || value > 1.00001f) return false;
                previous = value;
            }
            return true;
        }

        public bool PrepareLanding()
        {
            if (IsRunning) return false;
            if (!ValidateConfiguration(out string error)) { Debug.LogError(error, this); return false; }
            start = approachPoint.position; brake = decelerationPoint.position;
            ground = landingPoint.position; up = landingPoint.up;
            vertical = descentPoint.position; descentTime = DescentDuration;
            incoming = (brake - start).normalized * Mathf.Max(0, approachSpeed);
            initialRotation = approachPoint.rotation * Quaternion.Euler(initialInclination);
            finalRotation = landingPoint.rotation * Quaternion.Euler(finalRotationOffset);
            Elapsed = 0; initialized = true; runVersion++;
            effects?.SetGround(ground,landingPoint.rotation);
            effects?.ResetEffects();
            ApplyPose(0);
            ChangeState(LunarLandingState.TransitionIn);
            return true;
        }

        public bool BeginLanding()
        {
            if (State != LunarLandingState.TransitionIn && !PrepareLanding()) return false;
            ChangeState(LunarLandingState.Approach);
            ApplyPose(0);
            return true;
        }

        public void RestartLanding() { ResetLanding(); BeginLanding(); }
        public void ResetLanding()
        {
            initialized = false; Elapsed = 0; runVersion++;
            ChangeState(LunarLandingState.Idle);
            effects?.ResetEffects();
            if (motionRoot && approachPoint)
                motionRoot.SetPositionAndRotation(approachPoint.position, approachPoint.rotation * Quaternion.Euler(initialInclination));
            if (cameraRig && motionRoot) cameraRig.Sample(0, 0, motionRoot);
        }

        /// <summary>Permite testes sem depender de quadros. Preserva todos os eventos mesmo com delta longo.</summary>
        public void Advance(double deltaSeconds)
        {
            if (!initialized || !IsRunning || State == LunarLandingState.TransitionIn || deltaSeconds < 0 || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds)) return;
            Elapsed = Math.Min(Elapsed + deltaSeconds, TotalDuration);
            int version = runVersion;
            while (State < LunarLandingState.Completed && Elapsed >= EndOf(State))
            {
                ApplyPose(EndOf(State));
                ChangeState(State + 1);
                if (!initialized || !IsRunning || version != runVersion) return;
            }
            ApplyPose(Elapsed);
        }

        double EndOf(LunarLandingState state)
        {
            double t = approachDuration;
            if (state == LunarLandingState.Approach) return t;
            t += decelerationDuration; if (state == LunarLandingState.Deceleration) return t;
            t += descentTime; if (state == LunarLandingState.VerticalDescent) return t;
            t += touchdownDuration; if (state == LunarLandingState.Touchdown) return t;
            return TotalDuration;
        }

        void ChangeState(LunarLandingState next)
        {
            if (State == next) return;
            State = next;
            stateChanged.Invoke(next); StateChanged?.Invoke(next);
            if (next == LunarLandingState.Touchdown) { effects?.Touchdown(); touchdown.Invoke(); }
            if (next == LunarLandingState.Completed)
            {
                motionRoot.SetPositionAndRotation(ground, finalRotation);
                effects?.SetIntensity(0, 0); completed.Invoke(); Completed?.Invoke();
            }
        }

        static float Curve(AnimationCurve curve, float t)
        {
            if (t <= 0) return 0; if (t >= 1) return 1;
            return Mathf.Clamp01(curve == null || curve.length == 0 ? t : curve.Evaluate(t));
        }
        static Vector3 Hermite(Vector3 a, Vector3 b, Vector3 va, Vector3 vb, float duration, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return (2*t3-3*t2+1)*a + (t3-2*t2+t)*duration*va + (-2*t3+3*t2)*b + (t3-t2)*duration*vb;
        }

        void ApplyPose(double time)
        {
            double t = time; Vector3 position; Quaternion rotation; float engine = 0, dust = 0;
            int shot; float shotTime;
            Vector3 descentSpeed = -up * (2f * Vector3.Distance(vertical, ground) / descentTime);
            if (t < approachDuration)
            {
                float p = (float)t / approachDuration;
                position = Hermite(start, brake, incoming, incoming, approachDuration, Curve(approachCurve, p));
                rotation = initialRotation; shot = 0; shotTime = (float)t;
            }
            else if ((t -= approachDuration) < decelerationDuration)
            {
                float p = (float)t / decelerationDuration;
                position = Hermite(brake, vertical, incoming, descentSpeed, decelerationDuration, Curve(decelerationCurve, p));
                rotation = Quaternion.Slerp(initialRotation, finalRotation, Curve(rotationCurve, p));
                engine = Mathf.SmoothStep(0, .8f, p); shot = 1; shotTime = (float)t;
            }
            else if ((t -= decelerationDuration) < descentTime)
            {
                float p = (float)t / descentTime;
                position = Vector3.Lerp(vertical, ground, Curve(verticalCurve, p)); rotation = finalRotation;
                engine = Mathf.Lerp(.8f, 1f, p); dust = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1, p));
                shot = 2; shotTime = (float)t;
            }
            else if ((t -= descentTime) < touchdownDuration)
            {
                float p = (float)t / touchdownDuration;
                // Micro rebote somente acima do plano de contato; termina exatamente no waypoint.
                position = ground + up * (Mathf.Max(0, settlingHeight) * Mathf.Sin(p*Mathf.PI) * (1-p));
                rotation = finalRotation; engine = Mathf.Lerp(1, .5f, p); dust = 1;
                shot = 3; shotTime = (float)t;
            }
            else
            {
                t -= touchdownDuration;
                float p = Mathf.Clamp01((float)t / Mathf.Max(.01f, engineShutdownDuration));
                position = ground; rotation = finalRotation;
                engine = .5f * (1-Mathf.SmoothStep(0,1,p)); dust = 1-Mathf.SmoothStep(0,1,p);
                shot = 3; shotTime = touchdownDuration + (float)t;
            }
            // Nunca atravessa o plano de contato definido pela referência; sem raycasts ou física.
            float height = Vector3.Dot(position-ground, up);
            if (height < 0) position -= up * height;
            motionRoot.SetPositionAndRotation(position, rotation);
            effects?.SetIntensity(engine, dust);
            float shotDuration = shot == 1 ? decelerationDuration : shot == 2 ? descentTime : touchdownDuration + engineShutdownDuration + finalCinematicDuration;
            cameraRig?.Sample(shot, shotTime, motionRoot, shotDuration);
        }

        public void ConfigureVisual(Transform model, Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
        {
            if (!visualSocket || !model) return;
            model.SetParent(visualSocket, false); model.localPosition = localPosition;
            model.localRotation = Quaternion.Euler(localEuler); model.localScale = localScale;
        }

        void OnDrawGizmosSelected()
        {
            if (!approachPoint || !decelerationPoint || !descentPoint || !landingPoint) return;
            Gizmos.color = new Color(.17f,.78f,.75f);
            Vector3 velocity = (decelerationPoint.position-approachPoint.position).normalized*Mathf.Max(0,approachSpeed);
            Vector3 down = -landingPoint.up*(2*Vector3.Distance(descentPoint.position,landingPoint.position)/DescentDuration);
            Vector3 previous = approachPoint.position;
            for (int segment=0;segment<2;segment++)
                for (int i=1;i<=32;i++)
                {
                    float p = i/32f;
                    Vector3 next = segment == 0
                        ? Hermite(approachPoint.position,decelerationPoint.position,velocity,velocity,approachDuration,Curve(approachCurve,p))
                        : Hermite(decelerationPoint.position,descentPoint.position,velocity,down,decelerationDuration,Curve(decelerationCurve,p));
                    float height = Vector3.Dot(next-landingPoint.position,landingPoint.up);
                    if (height<0) next-=landingPoint.up*height;
                    Gizmos.DrawLine(previous,next); previous=next;
                }
            Gizmos.DrawLine(descentPoint.position, landingPoint.position);
            Gizmos.DrawWireSphere(approachPoint.position,.6f); Gizmos.DrawWireSphere(decelerationPoint.position,.6f);
            Gizmos.DrawWireSphere(descentPoint.position,.6f);
            Gizmos.color = new Color(1,.76f,.23f);
            Gizmos.DrawWireSphere(landingPoint.position,.8f);
            Gizmos.DrawRay(landingPoint.position, landingPoint.up*3);
        }
    }
}
