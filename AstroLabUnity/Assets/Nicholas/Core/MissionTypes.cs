using System;

namespace AstroLab.Nicholas.Core
{
    /// <summary>Identificadores estáveis; não renomear sem migrar saves.</summary>
    public enum EquipmentId { SolarPanels, LifeSupport, WaterRecycler, Greenhouse, RadiationShelter, SuitCharger, Communications }
    public enum MissionOutcome { Running, Success, Failure }
    public enum MissionEventType { Notice, Repair, Power, Science, Radiation, Turn, Warning, Failure, Success }
    public enum ArtifactId { Hammer, Feather, Rover, Retroreflector, FalconDescentStage }

    [Serializable]
    public sealed class OperationResult
    {
        public bool Success;
        public string Message;
        public static OperationResult Ok(string message) { return new OperationResult { Success = true, Message = message }; }
        public static OperationResult Fail(string message) { return new OperationResult { Success = false, Message = message }; }
    }

    [Serializable]
    public sealed class MissionLogEntry
    {
        public int Turn;
        public MissionEventType Type;
        public string Text;
    }

    [Serializable]
    public sealed class EquipmentProgress
    {
        public EquipmentId Id;
        public bool Repaired;
        public bool Powered;
    }

    public readonly struct VictoryCriterion
    {
        public readonly string Label;
        public readonly string PendingName;
        public readonly bool Met;

        public VictoryCriterion(string label, string pendingName, bool met)
        {
            Label = label;
            PendingName = pendingName;
            Met = met;
        }
    }
}
