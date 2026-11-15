using System;
using System.Collections.Generic;

namespace AstroLab.Nicholas.Core
{
    /// <summary>
    /// Motor de gameplay determinístico. Sem UnityEngine, UI, I/O ou HTTP.
    /// A camada Unity chama comandos; nenhum botão calcula recursos diretamente.
    /// </summary>
    public sealed class MissionEngine
    {
        private readonly MissionSnapshot state;
        private bool oxygenWarningIssued;
        private bool batteryWarningIssued;

        public event Action<MissionLogEntry> EventRecorded;
        public event Action StateChanged;

        public MissionSnapshot State { get { return state.DeepCopy(); } }
        public int PowerCapacity { get { return MissionRuleSet.InitialPowerCapacity + (IsRepaired(EquipmentId.SolarPanels) ? MissionRuleSet.SolarRepairPowerBonus : 0); } }
        public int PowerUsed
        {
            get
            {
                int used = 0;
                for (int i = 0; i < state.Equipments.Count; i++)
                {
                    EquipmentProgress p = state.Equipments[i];
                    if (p.Repaired && p.Powered) used += MissionRuleSet.Find(p.Id).PowerDemand;
                }
                return used;
            }
        }

        public MissionEngine()
        {
            state = new MissionSnapshot();
            for (int i = 0; i < MissionRuleSet.Definitions.Length; i++)
            {
                EquipmentDefinition d = MissionRuleSet.Definitions[i];
                state.Equipments.Add(new EquipmentProgress { Id = d.Id, Repaired = d.InitiallyRepaired, Powered = false });
            }
            // Inicialmente, comunicação ligada; capacidade restante para decisões do jogador.
            Find(EquipmentId.Communications).Powered = true;
            Record(MissionEventType.Notice, "Restabeleça suporte de vida, energia, abrigo e alimentos antes da próxima tripulação.");
        }

        private MissionEngine(MissionSnapshot restored)
        {
            if (restored == null || restored.SchemaVersion != 1) throw new ArgumentException("Save incompatível");
            state = restored.DeepCopy();
            if (state.Equipments == null || state.History == null || state.ScannedArtifacts == null)
                throw new ArgumentException("Save incompleto");
            foreach (EquipmentDefinition definition in MissionRuleSet.Definitions) Find(definition.Id);
            if (state.Turn < 1 || state.Turn > MissionRuleSet.MaximumTurns ||
                state.RemainingActionTime < 0 || state.RemainingActionTime > MissionRuleSet.ActionMinutesPerTurn ||
                state.SpareParts < 0 || state.NextArtifactIndex < 0 || state.NextArtifactIndex > Apollo15Trail.Items.Length ||
                PowerUsed > PowerCapacity || state.HabitatAir < 0 || state.Food < 0 || state.Water < 0 ||
                state.SuitOxygen < 0 || state.SuitOxygen > 100 || state.SuitBattery < 0 || state.SuitBattery > 100)
                throw new ArgumentException("Save com valores inválidos");
        }

        public static MissionEngine Restore(MissionSnapshot saved) { return new MissionEngine(saved); }

        private EquipmentProgress Find(EquipmentId id)
        {
            for (int i = 0; i < state.Equipments.Count; i++) if (state.Equipments[i].Id == id) return state.Equipments[i];
            throw new InvalidOperationException("Equipamento não registrado: " + id);
        }

        public bool IsRepaired(EquipmentId id) { return Find(id).Repaired; }
        public bool IsPowered(EquipmentId id) { EquipmentProgress e = Find(id); return e.Repaired && e.Powered; }

        public OperationResult CanRepair(EquipmentId id)
        {
            if (state.Outcome != MissionOutcome.Running) return OperationResult.Fail("A missão terminou.");
            EquipmentDefinition d = MissionRuleSet.Find(id);
            EquipmentProgress p = Find(id);
            if (p.Repaired) return OperationResult.Fail("Equipamento já reparado.");
            if (d.NeedsEva != state.IsOutside)
                return OperationResult.Fail(d.NeedsEva ? "Saia pela eclusa para consertar lá fora." : "Volte para dentro da base.");
            if (state.SpareParts < d.RepairParts)
            {
                int missing = d.RepairParts - state.SpareParts;
                return OperationResult.Fail(missing == 1 ? "Falta 1 peça." : "Faltam " + missing + " peças.");
            }
            if (state.RemainingActionTime < d.RepairTime)
            {
                int missing = d.RepairTime - state.RemainingActionTime;
                return OperationResult.Fail((missing == 1 ? "Falta 1 tempo." : "Faltam " + missing + " tempos.") + " Termine o turno!");
            }
            if (d.NeedsEva)
            {
                if (state.SuitBattery < MissionRuleSet.EvaRepairBatteryCost)
                    return OperationResult.Fail("Bateria do traje insuficiente para reparo externo.");
                if (state.SuitOxygen < MissionRuleSet.EvaRepairOxygenCost)
                    return OperationResult.Fail("Oxigênio do traje insuficiente para reparo externo.");
            }
            return OperationResult.Ok("Pronto para reparar.");
        }

        public OperationResult Repair(EquipmentId id)
        {
            OperationResult check = CanRepair(id);
            if (!check.Success) return check;
            EquipmentDefinition d = MissionRuleSet.Find(id);
            EquipmentProgress p = Find(id);

            state.SpareParts -= d.RepairParts;
            state.RemainingActionTime -= d.RepairTime;
            if (d.NeedsEva)
            {
                state.SuitBattery = Math.Max(0f, state.SuitBattery - MissionRuleSet.EvaRepairBatteryCost);
                state.SuitOxygen = Math.Max(0f, state.SuitOxygen - MissionRuleSet.EvaRepairOxygenCost);
            }
            p.Repaired = true;
            Record(MissionEventType.Repair, d.DisplayName + " reparado (-" + d.RepairParts + " peças, -" + d.RepairTime + " tempo)." );
            NotifyChanged();
            return OperationResult.Ok("Reparo concluído. Ative o equipamento se ele consumir energia.");
        }

        public OperationResult SetPowered(EquipmentId id, bool enable)
        {
            if (state.Outcome != MissionOutcome.Running) return OperationResult.Fail("A missão terminou.");
            EquipmentProgress p = Find(id);
            EquipmentDefinition d = MissionRuleSet.Find(id);
            if (!p.Repaired) return OperationResult.Fail("Repare o equipamento primeiro.");
            if (d.PowerDemand == 0) return OperationResult.Fail("Este equipamento não possui chave de consumo elétrico.");
            if (p.Powered == enable) return OperationResult.Fail("Sistema já está " + (enable ? "ligado" : "desligado") + ".");
            if (enable && PowerUsed + d.PowerDemand > PowerCapacity)
                return OperationResult.Fail("Energia insuficiente: " + (PowerUsed + d.PowerDemand) + "/" + PowerCapacity + ". Desligue outro sistema.");
            p.Powered = enable;
            Record(MissionEventType.Power, d.DisplayName + (enable ? " ligado." : " desligado."));
            NotifyChanged();
            return OperationResult.Ok(d.DisplayName + (enable ? " energizado." : " desenergizado."));
        }

        public OperationResult StartEva()
        {
            if (state.Outcome != MissionOutcome.Running) return OperationResult.Fail("A missão terminou.");
            if (state.IsOutside) return OperationResult.Fail("Você já está fora da base.");
            if (state.SuitOxygen < 20f || state.SuitBattery < 20f)
                return OperationResult.Fail("Recarregue o traje antes de sair (mínimo 20%).");
            state.IsOutside = true;
            state.EvaSecondsRemaining = 300f;
            oxygenWarningIssued = state.SuitOxygen <= 25f;
            batteryWarningIssued = state.SuitBattery <= 25f;
            Record(MissionEventType.Notice, "EVA iniciada: oxigênio, bateria e tempo passam a diminuir.");
            NotifyChanged();
            return OperationResult.Ok("Saída iniciada. Retorne à eclusa antes de esgotar o traje.");
        }

        public OperationResult FinishEva()
        {
            if (!state.IsOutside) return OperationResult.Fail("Você já está dentro da base.");
            if (state.Outcome != MissionOutcome.Running) return OperationResult.Fail("A missão terminou.");
            state.IsOutside = false;
            state.ReturnReached = true;
            Record(MissionEventType.Notice, "Retorno à base concluído com segurança.");
            NotifyChanged();
            return OperationResult.Ok("Traje preservado. A recarga depende da energia do módulo.");
        }

        public OperationResult RechargeSuit()
        {
            if (state.Outcome != MissionOutcome.Running) return OperationResult.Fail("A missão terminou.");
            if (state.IsOutside) return OperationResult.Fail("Recarregue dentro da base.");
            if (!IsPowered(EquipmentId.SuitCharger) || !IsPowered(EquipmentId.LifeSupport))
                return OperationResult.Fail("Ative a recarga e o suporte de vida (consomem energia).");
            if (state.RemainingActionTime < 1) return OperationResult.Fail("Tempo de turno insuficiente.");
            state.RemainingActionTime--;
            state.SuitOxygen = 100f;
            state.SuitBattery = 100f;
            Record(MissionEventType.Power, "Traje recarregado (1 tempo de ação).");
            NotifyChanged();
            return OperationResult.Ok("Reservas do traje restauradas.");
        }

        /// <summary>Consumo por tempo REAL em segundos somente durante EVA; sem atalhos por frame.</summary>
        public void Tick(float deltaSeconds)
        {
            if (state.Outcome != MissionOutcome.Running || !state.IsOutside) return;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f) return;
            // Limita picos de frame (alt-tab e travamentos) e evita matar o jogador inesperadamente.
            float delta = Math.Min(deltaSeconds, 0.25f);
            state.SuitOxygen = Math.Max(0f, state.SuitOxygen - MissionRuleSet.SuitOxygenDrainPerSecond * delta);
            state.SuitBattery = Math.Max(0f, state.SuitBattery - MissionRuleSet.SuitBatteryDrainPerSecond * delta);
            state.EvaSecondsRemaining = Math.Max(0f, state.EvaSecondsRemaining - delta);
            if (state.SuitOxygen <= 25f && !oxygenWarningIssued)
            { oxygenWarningIssued = true; Record(MissionEventType.Warning, "Oxigênio do traje baixo: retorne à eclusa."); }
            if (state.SuitBattery <= 25f && !batteryWarningIssued)
            { batteryWarningIssued = true; Record(MissionEventType.Warning, "Bateria do traje baixa: retorne à eclusa."); }
            if (state.SuitOxygen <= 0f || state.SuitBattery <= 0f || state.EvaSecondsRemaining <= 0f)
                Fail("Recursos de EVA esgotados antes do retorno à base.");
            NotifyChanged();
        }

        /// <summary>O progresso da ciência não substitui os reparos obrigatórios.</summary>
        public OperationResult RegisterArtifact(ArtifactId id, string answer = null)
        {
            if (state.Outcome != MissionOutcome.Running) return OperationResult.Fail("Missão encerrada.");
            if (!state.IsOutside) return OperationResult.Fail("A investigação acontece durante uma saída externa.");
            if (state.NextArtifactIndex >= Apollo15Trail.Items.Length)
                return OperationResult.Fail("A coleção Apollo 15 já está completa.");
            Apollo15Trail.Artifact expected = Apollo15Trail.Items[state.NextArtifactIndex];
            if (id != expected.Id) return OperationResult.Fail("Siga a pista anterior: " + expected.Name + ".");
            if (id == ArtifactId.Feather && !string.Equals((answer ?? "").Trim(), "C", StringComparison.OrdinalIgnoreCase))
                return OperationResult.Fail("Qual objeto chegou primeiro? Pense na resistência do ar na Lua.");
            if (state.RemainingActionTime < 1) return OperationResult.Fail("Tempo de ação do turno insuficiente para registrar.");
            if (state.SuitBattery < MissionRuleSet.EvaArtifactBatteryCost)
                return OperationResult.Fail("Bateria do traje insuficiente para registrar o artefato.");
            if (state.SuitOxygen < MissionRuleSet.EvaArtifactOxygenCost)
                return OperationResult.Fail("Oxigênio do traje insuficiente para registrar o artefato.");
            state.RemainingActionTime--;
            state.SuitBattery = Math.Max(0f, state.SuitBattery - MissionRuleSet.EvaArtifactBatteryCost);
            state.SuitOxygen = Math.Max(0f, state.SuitOxygen - MissionRuleSet.EvaArtifactOxygenCost);
            state.ScannedArtifacts.Add(id.ToString());
            state.NextArtifactIndex++;
            Record(MissionEventType.Science, "Artefato documentado: " + expected.Name + ". " + expected.History);
            NotifyChanged();
            return OperationResult.Ok(expected.ClueToNext);
        }

        public OperationResult AdvanceTurn()
        {
            if (state.Outcome != MissionOutcome.Running) return OperationResult.Fail("Missão encerrada.");
            if (state.IsOutside) return OperationResult.Fail("Retorne à base para avançar o turno.");
            int current = state.Turn;

            // Abstrações para jogo: saldo é alterado apenas por esse motor.
            state.Food = Math.Max(0, state.Food - 1);
            state.Water = Math.Max(0, state.Water - 1);
            if (IsPowered(EquipmentId.Greenhouse))
            {
                state.Food = Math.Min(20, state.Food + 2);
                state.Water = Math.Max(0, state.Water - 1);
            }
            if (IsRepaired(EquipmentId.WaterRecycler)) state.Water = Math.Min(20, state.Water + 2);
            state.HabitatAir = Math.Max(0, Math.Min(100, state.HabitatAir + (IsPowered(EquipmentId.LifeSupport) ? 5 : -20)));

            if (current == 2)
            {
                if (IsRepaired(EquipmentId.RadiationShelter))
                    Record(MissionEventType.Radiation, "Alerta solar simulado: abrigo preparado, tripulação protegida.");
                else
                {
                    state.HabitatAir = Math.Max(0, state.HabitatAir - 10);
                    Record(MissionEventType.Radiation, "Alerta solar simulado: abrigo indisponível. Penalidade didática de 10 pontos na reserva.");
                }
            }
            Record(MissionEventType.Turn, "Turno " + current + " finalizado. Água " + state.Water +
                   ", alimentos " + state.Food + ", ar do habitat " + state.HabitatAir + ".");
            if (state.HabitatAir <= 0) { Fail("Suporte atmosférico da base esgotado."); return OperationResult.Fail(state.OutcomeReason); }
            if (state.Water <= 0) { Fail("Reserva de água esgotada."); return OperationResult.Fail(state.OutcomeReason); }
            if (state.Food <= 0) { Fail("Reserva de alimentos esgotada."); return OperationResult.Fail(state.OutcomeReason); }
            if (current == MissionRuleSet.MaximumTurns)
            {
                ResolveMission();
                NotifyChanged();
                return state.Outcome == MissionOutcome.Success ? OperationResult.Ok("Missão concluída! A segunda tripulação pode chegar.") : OperationResult.Fail(state.OutcomeReason);
            }
            state.Turn++;
            state.RemainingActionTime = MissionRuleSet.ActionMinutesPerTurn;
            NotifyChanged();
            return OperationResult.Ok("Turno " + state.Turn + " iniciado. Reavalie as prioridades.");
        }

        public VictoryCriterion[] GetVictoryChecklist()
        {
            return new[]
            {
                new VictoryCriterion("Painéis solares consertados", "painéis solares", IsRepaired(EquipmentId.SolarPanels)),
                new VictoryCriterion("Suporte de vida consertado e ligado", "suporte de vida energizado", IsPowered(EquipmentId.LifeSupport)),
                new VictoryCriterion("Abrigo contra radiação consertado", "abrigo de radiação", IsRepaired(EquipmentId.RadiationShelter)),
                new VictoryCriterion("Estufa de comida consertada e ligada", "estufa operante", IsPowered(EquipmentId.Greenhouse)),
                new VictoryCriterion("Pelo menos " + MissionRuleSet.MinimumEndFood + " comidas guardadas", "estoque de alimentos", state.Food >= MissionRuleSet.MinimumEndFood),
                new VictoryCriterion("Pelo menos " + MissionRuleSet.MinimumEndWater + " água guardada", "estoque de água", state.Water >= MissionRuleSet.MinimumEndWater),
                new VictoryCriterion("Ar da base em pelo menos " + MissionRuleSet.MinimumEndHabitatAir + "%", "reserva atmosférica", state.HabitatAir >= MissionRuleSet.MinimumEndHabitatAir)
            };
        }

        private void ResolveMission()
        {
            VictoryCriterion[] checklist = GetVictoryChecklist();
            List<string> pending = new List<string>();
            for (int i = 0; i < checklist.Length; i++)
                if (!checklist[i].Met) pending.Add(checklist[i].PendingName);
            if (pending.Count == 0)
            {
                state.Outcome = MissionOutcome.Success;
                state.OutcomeReason = "Base preparada para receber a próxima tripulação.";
                Record(MissionEventType.Success, state.OutcomeReason);
            }
            else Fail("Base incompleta: " + string.Join(", ", pending.ToArray()) + ".");
        }

        private void Fail(string reason)
        {
            state.Outcome = MissionOutcome.Failure;
            state.OutcomeReason = reason;
            Record(MissionEventType.Failure, reason);
        }

        private void Record(MissionEventType type, string message)
        {
            MissionLogEntry item = new MissionLogEntry { Turn = state.Turn, Type = type, Text = message };
            state.History.Add(item);
            EventRecorded?.Invoke(item);
        }

        private void NotifyChanged() { StateChanged?.Invoke(); }
    }
}
