using AstroLab.Nicholas.Core;

namespace AstroLab.Gameplay
{
    /// <summary>One next action shared by the visor and management screen.</summary>
    public static class MissionGuidance
    {
        public static EquipmentId? Focus(MissionEngine engine)
        {
            if (!engine.IsRepaired(EquipmentId.SolarPanels)) return EquipmentId.SolarPanels;
            if (!engine.IsPowered(EquipmentId.LifeSupport)) return EquipmentId.LifeSupport;
            if (!engine.IsPowered(EquipmentId.Greenhouse)) return EquipmentId.Greenhouse;
            if (!engine.IsRepaired(EquipmentId.RadiationShelter)) return EquipmentId.RadiationShelter;
            if (!engine.IsRepaired(EquipmentId.WaterRecycler)) return EquipmentId.WaterRecycler;
            return null;
        }

        public static string Next(MissionEngine e)
        {
            var s = e.State;
            if (s.Outcome != MissionOutcome.Running) return s.OutcomeReason + "\n[Tab] Ver resultado ou reiniciar missão.";
            if (s.IsOutside && (s.SuitOxygen <= 30 || s.SuitBattery <= 30))
                return "RETORNE À ECLUSA\nSiga a baliza ECLUSA. [L] junto à entrada. Recarregue o traje antes de sair novamente.";
            var focus = Focus(e);
            if (focus.HasValue)
            {
                var d = MissionRuleSet.Find(focus.Value);
                if (s.IsOutside && !d.NeedsEva) return "VOLTE PARA A BASE\nUse [L] junto à eclusa. Próximo sistema: " + d.DisplayName + ".";
                if (!e.IsRepaired(d.Id))
                {
                    if (s.RemainingActionTime < d.RepairTime)
                        return "PLANEJE O PRÓXIMO TURNO\n[Tab] Confira a previsão e avance o turno dentro da base. O tempo de ação será renovado.";
                    if (d.NeedsEva && !s.IsOutside)
                        return "1 · RECUPERE A ENERGIA\nVá à eclusa, use [L] para sair e siga o marcador dos painéis solares.";
                    string place = d.Id == EquipmentId.LifeSupport ? "módulo de suporte de vida, à esquerda do hub" :
                        d.Id == EquipmentId.Greenhouse ? "estufa, à direita do hub" :
                        d.Id == EquipmentId.RadiationShelter ? "painel do abrigo, no corredor diagonal" :
                        d.Id == EquipmentId.WaterRecycler ? "reciclador no suporte de vida" : "conjunto de painéis no exterior";
                    return "REPARE: " + d.DisplayName.ToUpperInvariant() + "\nVá ao " + place + ". Mire no equipamento e use [R] ou [E]. Custo: " + d.RepairParts + " peças + " + d.RepairTime + " ações.";
                }
                return "LIGUE: " + d.DisplayName.ToUpperInvariant() + "\n[Tab] Selecione o sistema e pressione Ligar. Se faltar energia, desligue comunicações ou recarga do traje.";
            }
            if (s.IsOutside) return "RETORNE À BASE\n[L] junto à eclusa. Depois confira o estoque e finalize o turno em [Tab].";
            return "MANTENHA A BASE OPERACIONAL\n[Tab] Confira água, alimentos e ar na previsão. Avance até o fim do terceiro turno. Apollo 15 é uma missão extra.";
        }
    }
}
