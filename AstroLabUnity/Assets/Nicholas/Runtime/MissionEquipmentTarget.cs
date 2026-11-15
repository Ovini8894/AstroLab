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
        public string Label
        {
            get
            {
                var part = PecasDaBase.Encontrar(name);
                if (part != null && part.id == "Porta_SuporteVida") return "Porta do suporte de vida · Reparo do suporte de vida";
                if (part != null && part.id == "Porta_Eclusa_Externa")
                    return MissionController.Instance && MissionController.Instance.Engine.IsRepaired(equipment)
                        ? "Porta da eclusa · [L] ENTRAR/SAIR com o traje"
                        : "Porta da eclusa · Reparo da proteção da base";
                return MissionRuleSet.Find(equipment).DisplayName;
            }
        }
    }
}
