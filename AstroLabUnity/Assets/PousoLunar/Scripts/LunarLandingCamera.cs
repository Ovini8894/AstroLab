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
            public Vector3 targetOffset = new Vector3(0, 3.8f, 0);
            [Tooltip("Acompanha o alvo com o enquadramento orbital abaixo. Desative para usar a posição fixa da âncora.")]
            public bool followTarget = true;
            [Tooltip("Distância da câmera ao ponto observado, em metros.")]
            [Min(1)] public float distance = 24;
            [Tooltip("Ângulo de visão para baixo. 20–40 graus mantém o dorso e a lateral legíveis.")]
            [Range(5, 75)] public float elevation = 32;
            [Tooltip("Azimute mundial: 0 = lado +Z; 90 = lado +X. +Z mostra o dorso do orbitador.")]
            public float orbitAngle = 40;
            [Range(15, 100)] public float fieldOfView = 48;
            [Min(.01f)] public float blendDuration = 2;
            [Tooltip("Suavidade da transição: 0 linear, 1 acelera e desacelera suavemente. Não acumula atraso no acompanhamento.")]
            [Range(0, 1)] public float smoothing = 1;
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
            float progress = Mathf.Clamp01(seconds / Mathf.Max(.01f,Mathf.Min(to.blendDuration,phaseDuration)));
            float blend = index == 0 ? 1 : Mathf.Lerp(progress,Mathf.SmoothStep(0,1,progress),to.smoothing);
            Vector3 a = LookPoint(from,rocket), b = LookPoint(to,rocket);
            Vector3 position = Vector3.Lerp(ShotPosition(from,a),ShotPosition(to,b),blend);
            Vector3 lookPoint = Vector3.Lerp(a,b,blend);
            // Aponta a partir da posição interpolada. Interpolar duas rotações independentes
            // podia deslocar a nave para fora do quadro durante a passagem entre os planos.
            cinematicCamera.transform.SetPositionAndRotation(position,Aim(lookPoint-position,cinematicCamera.transform.rotation));
            var lens = cinematicCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(from.fieldOfView,to.fieldOfView,blend);
            cinematicCamera.Lens = lens;
        }

        static Vector3 LookPoint(Shot shot, Transform rocket)
            => (shot.target ? shot.target.position : rocket.position) + shot.targetOffset;

        static Vector3 ShotPosition(Shot shot, Vector3 lookPoint)
        {
            if (!shot.followTarget) return shot.anchor.position;
            float elevation = Mathf.Clamp(shot.elevation,5,75) * Mathf.Deg2Rad;
            float orbit = shot.orbitAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(orbit)*Mathf.Cos(elevation),Mathf.Sin(elevation),Mathf.Cos(orbit)*Mathf.Cos(elevation));
            Vector3 position = lookPoint + offset * Mathf.Max(1,shot.distance);
            // Reutiliza as quatro âncoras existentes como representação editável da tomada.
            // A receita polar é a fonte de verdade no modo de acompanhamento.
            shot.anchor.position = position;
            return position;
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
