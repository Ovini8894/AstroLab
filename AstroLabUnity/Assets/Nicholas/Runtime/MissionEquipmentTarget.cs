using AstroLab.Nicholas.Core;
using UnityEngine;

namespace AstroLab.Nicholas
{
    /// <summary>Adicione a uma instância de objeto REPAIR_* apenas na cena Nicholas.</summary>
    public sealed class MissionEquipmentTarget : MonoBehaviour
    {
        [SerializeField] private EquipmentId equipment;
        public EquipmentId Equipment { get { return equipment; } }
        public void Configure(EquipmentId id) { equipment = id; }
        public string Label { get { return MissionRuleSet.Find(equipment).DisplayName; } }
    }
}
