using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using UnityEngine;

namespace AstroLab.Gameplay
{
    /// <summary>Physical room volumes prevent EVA state leaking into the habitat through walkable doors.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class BaseInteriorSafety : MonoBehaviour
    {
        [SerializeField] private BoxCollider[] interiorZones;
        private MissionController mission;
        private Vector3 lastIndoorPosition;
        private bool hasIndoorPosition;

        public void Configure(BoxCollider[] zones) { interiorZones = zones; }
        public bool Contains(Vector3 point)
        {
            if (interiorZones == null) return false;
            foreach (var zone in interiorZones)
            {
                if (!zone) continue;
                Vector3 p = zone.transform.InverseTransformPoint(point) - zone.center;
                Vector3 h = zone.size * .5f;
                if (Mathf.Abs(p.x) > h.x || Mathf.Abs(p.y) > h.y || Mathf.Abs(p.z) > h.z) continue;
                if ((zone.name == "SuporteVida" || zone.name == "Estufa" || zone.name == "Abrigo") &&
                    Mathf.Abs(p.x) / h.x + Mathf.Abs(p.z) / h.z > 1.42f) continue;
                return true;
            }
            return false;
        }

        public void Synchronize()
        {
            if (!mission) mission = MissionController.Instance;
            if (!mission || mission.Engine == null || !mission.PlayerTransform || mission.Engine.State.Outcome != MissionOutcome.Running) return;
            var player = mission.PlayerTransform;
            bool inside = Contains(player.position);
            if (inside)
            {
                lastIndoorPosition = player.position; hasIndoorPosition = true;
                if (mission.Engine.State.IsOutside)
                {
                    mission.Engine.FinishEva();
                    Debug.Log("[AstroLab] Entrada física no habitat: EVA encerrada e reparos internos liberados.");
                }
            }
            else if (!mission.Engine.State.IsOutside)
            {
                var result = mission.Engine.StartEva();
                if (!result.Success && hasIndoorPosition)
                {
                    var controller = player.GetComponent<CharacterController>();
                    bool controllerWasEnabled=controller&&controller.enabled;
                    if (controller) controller.enabled = false;
                    player.position = lastIndoorPosition;
                    if (controller) controller.enabled = controllerWasEnabled;
                    Debug.Log("[AstroLab] Saída bloqueada: recarregue o traje antes de explorar.");
                }
            }
        }
        private void Update() { Synchronize(); }
    }
}
