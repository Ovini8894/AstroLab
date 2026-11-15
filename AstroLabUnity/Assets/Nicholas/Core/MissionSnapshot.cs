using System;
using System.Collections.Generic;

namespace AstroLab.Nicholas.Core
{
    [Serializable]
    public sealed class MissionSnapshot
    {
        public int SchemaVersion = 1;
        public int Turn = 1;
        public int RemainingActionTime = MissionRuleSet.ActionMinutesPerTurn;
        public int SpareParts = MissionRuleSet.StartingSpareParts;
        public int Food = MissionRuleSet.InitialFood;
        public int Water = MissionRuleSet.InitialWater;
        public int HabitatAir = MissionRuleSet.InitialHabitatAir;
        public float SuitOxygen = 100f;
        public float SuitBattery = 100f;
        public float EvaSecondsRemaining = 300f;
        public bool IsOutside;
        public bool ReturnReached;
        public bool HasPlayerPose;
        public float PlayerPosX;
        public float PlayerPosY;
        public float PlayerPosZ;
        public float PlayerRotY;
        public MissionOutcome Outcome = MissionOutcome.Running;
        public string OutcomeReason = string.Empty;
        public int NextArtifactIndex;
        public List<EquipmentProgress> Equipments = new List<EquipmentProgress>();
        public List<MissionLogEntry> History = new List<MissionLogEntry>();
        public List<string> ScannedArtifacts = new List<string>();

        public MissionSnapshot DeepCopy()
        {
            MissionSnapshot result = (MissionSnapshot)MemberwiseClone();
            result.Equipments = new List<EquipmentProgress>();
            for (int i = 0; i < Equipments.Count; i++)
            {
                EquipmentProgress p = Equipments[i];
                result.Equipments.Add(new EquipmentProgress { Id = p.Id, Repaired = p.Repaired, Powered = p.Powered });
            }
            result.History = new List<MissionLogEntry>();
            for (int i = 0; i < History.Count; i++)
            {
                MissionLogEntry h = History[i];
                result.History.Add(new MissionLogEntry { Turn = h.Turn, Type = h.Type, Text = h.Text });
            }
            result.ScannedArtifacts = new List<string>(ScannedArtifacts);
            return result;
        }
    }
}
