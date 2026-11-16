using UnityEngine;

namespace AstroLab.Gameplay
{
    /// <summary>Attached only to restored door instances. Mesh and collider move together, leaving the passage clear.</summary>
    public sealed class RestoredSlidingDoor : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float openingDistance = 3.2f;
        [SerializeField, Min(.1f)] private float closingDistance = 4.2f;
        [SerializeField, Min(.1f)] private float openingSpeed = 5f;
        private Vector3 closedLocal, slideLocal, closedWorld;
        private Transform viewer;
        private float fraction;
        private bool initialized, wasOpen;
        public bool IsOpen => fraction > .95f;

        private void Awake()
        {
            closedLocal = transform.localPosition;
            var renderer = GetComponent<Renderer>();
            if (!renderer) { enabled = false; return; }
            var bounds = renderer.bounds;
            closedWorld = bounds.center;
            Vector3 slideWorld = bounds.size.x < bounds.size.z
                ? Vector3.forward * (bounds.size.z + .25f)
                : Vector3.right * (bounds.size.x + .25f);
            slideLocal = transform.parent ? transform.parent.InverseTransformVector(slideWorld) : slideWorld;
            initialized = true;
        }
        private void OnEnable() { if (initialized) { fraction = 0; transform.localPosition = closedLocal; } }
        private void OnDisable() { if (initialized) { fraction = 0; transform.localPosition = closedLocal; wasOpen = false; } }
        private void Update()
        {
            if (!initialized || Time.deltaTime <= 0) return;
            if (!viewer && Camera.main) viewer = Camera.main.transform;
            if (!viewer) return;
            float distance = Vector3.Distance(viewer.position, closedWorld);
            bool shouldOpen = distance < (wasOpen ? closingDistance : openingDistance);
            if (shouldOpen != wasOpen)
            {
                wasOpen = shouldOpen;
                Debug.Log("[AstroLab][Door] " + name + (wasOpen ? " abrindo passagem." : " fechando passagem."));
            }
            fraction = Mathf.MoveTowards(fraction, shouldOpen ? 1 : 0, Time.deltaTime * openingSpeed);
            transform.localPosition = closedLocal + slideLocal * Mathf.SmoothStep(0, 1, fraction);
        }
    }
}
