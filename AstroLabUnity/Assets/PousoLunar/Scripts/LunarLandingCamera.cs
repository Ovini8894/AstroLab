using System;
using UnityEngine;
using Unity.Cinemachine;

namespace AstroLab.PousoLunar
{
    /// <summary>Quatro enquadramentos dirigem uma câmera Cinemachine; o Brain controla a câmera de saída.</summary>
    public sealed class LunarLandingCamera : MonoBehaviour
    {
        [Serializable]
        public sealed class Shot
        {
            public Transform anchor;
            public Transform target;
            public Vector3 targetOffset = new Vector3(0, 3, 0);
            [Range(15, 100)] public float fieldOfView = 48;
            [Min(.01f)] public float blendDuration = 2;
        }

        public CinemachineCamera cinematicCamera;
        public Shot establishing = new Shot();
        public Shot approach = new Shot();
        public Shot descent = new Shot();
        public Shot final = new Shot();

        Shot GetShot(int index) => index == 0 ? establishing : index == 1 ? approach : index == 2 ? descent : final;

        public void Sample(int index, float seconds, Transform rocket, float phaseDuration = float.PositiveInfinity)
        {
            if (!cinematicCamera || !rocket) return;
            Shot to = GetShot(index), from = GetShot(Mathf.Max(0,index-1));
            if (to == null || !to.anchor) return;
            if (from == null || !from.anchor) from = to;
            float blend = index == 0 ? 1 : Mathf.SmoothStep(0,1,Mathf.Clamp01(seconds / Mathf.Max(.01f,Mathf.Min(to.blendDuration,phaseDuration))));
            Vector3 position = Vector3.Lerp(from.anchor.position, to.anchor.position, blend);
            Vector3 a = (from.target ? from.target.position : rocket.position) + from.targetOffset;
            Vector3 b = (to.target ? to.target.position : rocket.position) + to.targetOffset;
            Quaternion qa = Aim(a-from.anchor.position, from.anchor.rotation);
            Quaternion qb = Aim(b-to.anchor.position, to.anchor.rotation);
            cinematicCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(qa,qb,blend));
            var lens = cinematicCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(from.fieldOfView,to.fieldOfView,blend);
            cinematicCamera.Lens = lens;
        }

        static Quaternion Aim(Vector3 direction, Quaternion fallback)
        {
            if (direction.sqrMagnitude < .00001f) return fallback;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > .999f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(direction, up);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.28f,.9f,1f);
            for (int i=0;i<4;i++)
            {
                Shot shot = GetShot(i);
                if (shot == null || !shot.anchor) continue;
                Gizmos.DrawWireSphere(shot.anchor.position,.5f);
                if (shot.target) Gizmos.DrawLine(shot.anchor.position,shot.target.position + shot.targetOffset);
            }
        }
    }
}
