using System;
using System.Reflection;
using AstroLab.Nicholas.Core;
using UnityEngine;

namespace AstroLab.Nicholas
{
    /// <summary>HUD opcional do colega: métodos públicos chamados sem dependência de compilação.</summary>
    public sealed class MissionHelmetBridge : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour helmet;
        private MissionController mission;
        private MethodInfo setOxygen, setBattery, setMission, setAirlockGuide;
        private void Start()
        {
            mission = MissionController.Instance;
            if (!helmet) helmet = OptionalTeamComponents.FindByName("HudCapacete");
            if (helmet)
            {
                Type type = helmet.GetType();
                setOxygen = type.GetMethod("DefinirOxigenio", new[] { typeof(float) });
                setBattery = type.GetMethod("DefinirEnergia", new[] { typeof(float) });
                setMission = type.GetMethod("DefinirMissao", new[] { typeof(string), typeof(string), typeof(int), typeof(int) });
                setAirlockGuide = type.GetMethod("DefinirGuiaEclusa", new[] { typeof(bool), typeof(Transform) });
            }
            if (mission != null) { mission.Updated += Refresh; Refresh(); }
        }
        private void OnDestroy() { if (mission != null) mission.Updated -= Refresh; }
        private void Refresh()
        {
            if (!helmet || mission == null || setOxygen == null || setBattery == null || setMission == null) return;
            MissionSnapshot state = mission.Engine.State;
            setOxygen.Invoke(helmet, new object[] { state.SuitOxygen });
            setBattery.Invoke(helmet, new object[] { state.SuitBattery }); // bateria do TRAJE, não capacidade da base
            setAirlockGuide?.Invoke(helmet, new object[] { state.IsOutside, mission.OuterAirlock });
            int complete = 0;
            if (mission.Engine.IsRepaired(EquipmentId.SolarPanels)) complete++;
            if (mission.Engine.IsPowered(EquipmentId.LifeSupport)) complete++;
            if (mission.Engine.IsRepaired(EquipmentId.RadiationShelter)) complete++;
            if (mission.Engine.IsPowered(EquipmentId.Greenhouse)) complete++;
            string objective = AstroLab.Gameplay.MissionGuidance.Next(mission.Engine);
            if (mission.TargetMarkers) mission.TargetMarkers.SetTutorialTarget(AstroLab.Gameplay.MissionGuidance.Focus(mission.Engine));
            setMission.Invoke(helmet, new object[] { "OBJETIVO · TURNO " + state.Turn + "/3", objective, complete, 4 });
        }
    }
}
