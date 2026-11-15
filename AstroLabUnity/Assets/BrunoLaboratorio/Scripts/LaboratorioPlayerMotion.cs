using UnityEngine;

namespace AstroLab.BrunoLaboratorio
{
    public sealed class LaboratorioPlayerMotion : MonoBehaviour
    {
        [SerializeField] private LaboratorioPlayerController controller;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float bobAmplitude = 0.045f;
        [SerializeField] private float fullSpeedStepFrequency = 2.5f;
        [SerializeField] private float idleBreathAmplitude = 0.012f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;

        public void Configure(LaboratorioPlayerController playerController, Transform root)
        {
            controller = playerController;
            visualRoot = root;
            CaptureBasePose();
        }

        private void Awake()
        {
            CaptureBasePose();
        }

        private void LateUpdate()
        {
            if (controller == null || visualRoot == null)
                return;

            float speed01 = Mathf.Clamp01(controller.CurrentSpeed / Mathf.Max(0.01f, controller.MaximumSpeed));
            float gait = Mathf.Sin(Time.time * fullSpeedStepFrequency * Mathf.PI * 2f);
            float breathe = Mathf.Sin(Time.time * 1.4f) * idleBreathAmplitude * (1f - speed01);
            float bob = Mathf.Abs(gait) * bobAmplitude * speed01;
            float roll = gait * 2f * speed01;

            visualRoot.localPosition = baseLocalPosition + Vector3.up * (breathe + bob);
            visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, roll);
        }

        private void CaptureBasePose()
        {
            if (visualRoot == null)
                return;

            baseLocalPosition = visualRoot.localPosition;
            baseLocalRotation = visualRoot.localRotation;
        }
    }
}
