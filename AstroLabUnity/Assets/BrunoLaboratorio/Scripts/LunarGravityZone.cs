using System.Collections.Generic;
using UnityEngine;

namespace AstroLab.BrunoLaboratorio
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LunarGravityZone : MonoBehaviour
    {
        [SerializeField] private float gravity = -1.62f;
        private BoxCollider zone;
        private static readonly List<LunarGravityZone> ActiveZones = new();

        public float Gravity => gravity;

        private void Awake()
        {
            zone = GetComponent<BoxCollider>();
        }

        private void OnEnable()
        {
            if (!ActiveZones.Contains(this))
                ActiveZones.Add(this);
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
        }

        public static bool TryGetGravity(Vector3 worldPosition, out float value)
        {
            for (int i = ActiveZones.Count - 1; i >= 0; i--)
            {
                LunarGravityZone gravityZone = ActiveZones[i];
                if (gravityZone == null)
                {
                    ActiveZones.RemoveAt(i);
                    continue;
                }

                gravityZone.zone ??= gravityZone.GetComponent<BoxCollider>();
                if (gravityZone.zone.bounds.Contains(worldPosition))
                {
                    value = gravityZone.gravity;
                    return true;
                }
            }

            value = 0f;
            return false;
        }
    }
}
