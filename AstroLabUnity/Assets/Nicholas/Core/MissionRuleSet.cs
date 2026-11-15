using System;
using System.Collections.Generic;

namespace AstroLab.Nicholas.Core
{
    [Serializable]
    public sealed class EquipmentDefinition
    {
        public EquipmentId Id;
        public string DisplayName;
        public int RepairParts;
        public int RepairTime;
        public int PowerDemand;
        public bool InitiallyRepaired;
        public bool NeedsEva;

        public EquipmentDefinition(EquipmentId id, string displayName, int parts, int time, int power,
            bool initiallyRepaired = false, bool needsEva = false)
        {
            Id = id; DisplayName = displayName; RepairParts = parts; RepairTime = time;
            PowerDemand = power; InitiallyRepaired = initiallyRepaired; NeedsEva = needsEva;
        }
    }

    /// <summary>Unidades abstratas de jogo, NÃO especificações da NASA.</summary>
    public sealed class MissionRuleSet
    {
        public const int MaximumTurns = 3;
        public const int ActionMinutesPerTurn = 6;
        public const int StartingSpareParts = 12;
        public const float SuitOxygenDrainPerSecond = 0.55f;
        public const float SuitBatteryDrainPerSecond = 0.50f;
        public const float EvaRepairBatteryCost = 18f;
        public const float EvaRepairOxygenCost = 12f;
        public const float EvaArtifactBatteryCost = 10f;
        public const float EvaArtifactOxygenCost = 8f;
        public const int InitialFood = 3;
        public const int InitialWater = 5;
        public const int InitialHabitatAir = 55;
        public const int MinimumEndFood = 2;
        public const int MinimumEndWater = 1;
        public const int MinimumEndHabitatAir = 30;
        public const int InitialPowerCapacity = 3;
        public const int SolarRepairPowerBonus = 5;

        // Total com todos os consumidores ligados: 4 + 3 + 2 + 1 = 10;
        // mesmo após reparar painéis, capacidade normal = 8. Há uma escolha real.
        public static readonly EquipmentDefinition[] Definitions =
        {
            new EquipmentDefinition(EquipmentId.SolarPanels, "Painéis solares", 2, 2, 0, false, true),
            new EquipmentDefinition(EquipmentId.LifeSupport, "Suporte de vida", 3, 2, 4),
            new EquipmentDefinition(EquipmentId.WaterRecycler, "Reciclador de água", 2, 2, 0),
            new EquipmentDefinition(EquipmentId.Greenhouse, "Estufa de alimentos", 2, 2, 3),
            new EquipmentDefinition(EquipmentId.RadiationShelter, "Abrigo de radiação", 3, 2, 0),
            new EquipmentDefinition(EquipmentId.SuitCharger, "Recarga do traje", 0, 0, 2, true),
            new EquipmentDefinition(EquipmentId.Communications, "Comunicações", 0, 0, 1, true)
        };

        public static EquipmentDefinition Find(EquipmentId id)
        {
            for (int i = 0; i < Definitions.Length; i++)
                if (Definitions[i].Id == id) return Definitions[i];
            throw new ArgumentOutOfRangeException(nameof(id), id, "Equipamento desconhecido");
        }
    }
}
