using AstroLab.Nicholas.Core;
using NUnit.Framework;

namespace AstroLab.Nicholas.Tests
{
    public sealed class MissionEngineTests
    {
        [Test] public void PrevisaoDoTurnoIncluiEventoSolarSemAlterarPartida()
        {
            MissionEngine game = new MissionEngine();
            Assert.IsTrue(game.AdvanceTurn().Success);
            int originalAir = game.State.HabitatAir;
            int eventsBefore = game.State.History.Count;
            MissionSnapshot preview = game.PreviewNextTurn();
            Assert.AreEqual(originalAir - 30, preview.HabitatAir);
            Assert.AreEqual(originalAir, game.State.HabitatAir);
            Assert.AreEqual(eventsBefore, game.State.History.Count);
            Assert.IsTrue(game.AdvanceTurn().Success);
            Assert.AreEqual(preview.HabitatAir, game.State.HabitatAir);
            Assert.AreEqual(preview.Food, game.State.Food);
            Assert.AreEqual(preview.Water, game.State.Water);
        }

        [Test] public void PrevisaoRespeitaLimitesDeEstoqueEConsumoReal()
        {
            MissionEngine game = new MissionEngine();
            game.StartEva(); game.Repair(EquipmentId.SolarPanels); game.FinishEva();
            game.Repair(EquipmentId.LifeSupport); game.Repair(EquipmentId.Greenhouse);
            game.SetPowered(EquipmentId.LifeSupport, true); game.SetPowered(EquipmentId.Greenhouse, true);
            MissionSnapshot state = game.State; state.Food = 20; state.HabitatAir = 99;
            game = MissionEngine.Restore(state);
            var preview = game.PreviewNextTurn();
            Assert.AreEqual(20, preview.Food);
            Assert.AreEqual(100, preview.HabitatAir);
            Assert.AreEqual(99, game.State.HabitatAir);
        }

        [Test] public void ComecaComTresPontosDeEnergiaEComunicacaoLigada()
        {
            MissionEngine game = new MissionEngine();
            Assert.AreEqual(3, game.PowerCapacity);
            Assert.AreEqual(1, game.PowerUsed);
            Assert.AreEqual(1, game.State.Turn);
            Assert.AreEqual(3, MissionRuleSet.MaximumTurns);
        }

        [Test] public void ReparoEAtomicoSemPecasOuTempo()
        {
            MissionEngine game = new MissionEngine();
            Assert.IsFalse(game.Repair(EquipmentId.SolarPanels).Success); // exige EVA
            Assert.AreEqual(12, game.State.SpareParts);
            Assert.IsFalse(game.IsRepaired(EquipmentId.SolarPanels));
            Assert.IsTrue(game.StartEva().Success);
            Assert.IsTrue(game.Repair(EquipmentId.SolarPanels).Success);
            Assert.IsFalse(game.Repair(EquipmentId.SolarPanels).Success); // não cobra duas vezes
            Assert.AreEqual(10, game.State.SpareParts);
        }

        [Test] public void NaoPermiteExcederACapacidadeDeEnergia()
        {
            MissionEngine game = new MissionEngine();
            game.StartEva(); game.Repair(EquipmentId.SolarPanels); game.FinishEva();
            game.Repair(EquipmentId.LifeSupport);
            game.Repair(EquipmentId.Greenhouse);
            Assert.IsTrue(game.SetPowered(EquipmentId.LifeSupport, true).Success);
            Assert.IsTrue(game.SetPowered(EquipmentId.Greenhouse, true).Success); // 1+4+3=8
            Assert.IsFalse(game.SetPowered(EquipmentId.SuitCharger, true).Success); // 10/8
            Assert.AreEqual(8, game.PowerUsed);
        }

        [Test] public void TurnoNaoAvancaDuranteEva()
        {
            MissionEngine game = new MissionEngine(); game.StartEva();
            Assert.IsFalse(game.AdvanceTurn().Success);
            Assert.AreEqual(1, game.State.Turn);
        }

        [Test] public void TrilhasApollo15ExigemSequenciaERespostaCorreta()
        {
            MissionEngine game = new MissionEngine(); game.StartEva();
            Assert.IsFalse(game.RegisterArtifact(ArtifactId.Rover).Success);
            Assert.IsTrue(game.RegisterArtifact(ArtifactId.Hammer).Success);
            Assert.IsFalse(game.RegisterArtifact(ArtifactId.Feather, "A").Success);
            Assert.IsTrue(game.RegisterArtifact(ArtifactId.Feather, "C").Success);
            Assert.AreEqual(2, game.State.NextArtifactIndex);
        }

        [Test] public void EvaConsomeTrajePorTempoENaoPorFrame()
        {
            MissionEngine game = new MissionEngine(); game.StartEva();
            game.Tick(0.2f);
            Assert.Less(game.State.SuitOxygen, 100f);
            Assert.Less(game.State.SuitBattery, 100f);
            Assert.AreEqual(100f - MissionRuleSet.SuitOxygenDrainPerSecond * 0.2f, game.State.SuitOxygen, .00001f);
            Assert.AreEqual(100f - MissionRuleSet.SuitBatteryDrainPerSecond * 0.2f, game.State.SuitBattery, .00001f);
        }

        [Test] public void CaminhoCompletoVenceAoFimDoTerceiroTurno()
        {
            MissionEngine game = new MissionEngine();
            game.StartEva(); Assert.IsTrue(game.Repair(EquipmentId.SolarPanels).Success); game.FinishEva();
            Assert.IsTrue(game.Repair(EquipmentId.LifeSupport).Success);
            Assert.IsTrue(game.Repair(EquipmentId.Greenhouse).Success);
            Assert.IsTrue(game.SetPowered(EquipmentId.LifeSupport, true).Success);
            Assert.IsTrue(game.SetPowered(EquipmentId.Greenhouse, true).Success);
            Assert.IsTrue(game.AdvanceTurn().Success);
            Assert.IsTrue(game.Repair(EquipmentId.RadiationShelter).Success);
            Assert.IsTrue(game.Repair(EquipmentId.WaterRecycler).Success);
            Assert.IsTrue(game.AdvanceTurn().Success);
            Assert.IsTrue(game.AdvanceTurn().Success);
            Assert.AreEqual(MissionOutcome.Success, game.State.Outcome);
            Assert.GreaterOrEqual(game.State.Food, 2);
        }

        [Test] public void NaoReparaAbrigoSeEstiverNaRua()
        {
            MissionEngine game = new MissionEngine(); game.StartEva();
            Assert.IsFalse(game.Repair(EquipmentId.RadiationShelter).Success);
        }

        [Test] public void SaveSnapshotNaoPermiteMutarEstadoOriginal()
        {
            MissionEngine game = new MissionEngine();
            MissionSnapshot snapshot = game.State;
            snapshot.Food = -500;
            Assert.AreEqual(3, game.State.Food);
            MissionEngine restored = MissionEngine.Restore(game.State);
            Assert.AreEqual(game.State.Food, restored.State.Food);
        }

        [Test] public void ReparoEmEvaConsomeTrajeEEhAtomicoQuandoInsuficiente()
        {
            MissionEngine game = new MissionEngine();
            Assert.IsTrue(game.StartEva().Success);
            // Drena bateria abaixo do custo de reparo (18%) mantendo acima de 0%
            for (int i = 0; i < 700; i++) game.Tick(0.25f);
            // 700 * 0.25s = 175s => bateria = 100 - 175 * 0.50 = 12.5%, O2 = 100 - 175 * 0.55 = 3.75%
            float batAntes = game.State.SuitBattery;
            float oxAntes = game.State.SuitOxygen;
            int pecasAntes = game.State.SpareParts;
            int tempoAntes = game.State.RemainingActionTime;

            OperationResult res = game.Repair(EquipmentId.SolarPanels);
            Assert.IsFalse(res.Success);
            Assert.IsFalse(game.IsRepaired(EquipmentId.SolarPanels));
            Assert.AreEqual(batAntes, game.State.SuitBattery, .0001f);
            Assert.AreEqual(oxAntes, game.State.SuitOxygen, .0001f);
            Assert.AreEqual(pecasAntes, game.State.SpareParts);
            Assert.AreEqual(tempoAntes, game.State.RemainingActionTime);
        }

        [Test] public void RegistroDeArtefatoConsomeTrajeEEhAtomicoQuandoInsuficiente()
        {
            MissionEngine game = new MissionEngine();
            Assert.IsTrue(game.StartEva().Success);
            // Drena bateria para ~7.5% (abaixo de 10%) usando 185s: 100 - 185*0.50 = 7.5% (mas O2 iria a 0 em 181.8s).
            // Vamos drenar 170s: O2 = 6.5% (< 8%), bateria = 15% (> 10%). Testa atomicidade por O2 insuficiente.
            for (int i = 0; i < 680; i++) game.Tick(0.25f);
            float batAntes = game.State.SuitBattery;
            float oxAntes = game.State.SuitOxygen;
            int tempoAntes = game.State.RemainingActionTime;

            OperationResult res = game.RegisterArtifact(ArtifactId.Hammer);
            Assert.IsFalse(res.Success);
            Assert.AreEqual(0, game.State.NextArtifactIndex);
            Assert.AreEqual(batAntes, game.State.SuitBattery, .0001f);
            Assert.AreEqual(oxAntes, game.State.SuitOxygen, .0001f);
            Assert.AreEqual(tempoAntes, game.State.RemainingActionTime);
        }

        [Test] public void RecargaRestauraTrajeQuandoSuporteDeVidaERecargaEstaoLigados()
        {
            MissionEngine game = new MissionEngine();
            Assert.IsTrue(game.StartEva().Success);
            Assert.IsTrue(game.Repair(EquipmentId.SolarPanels).Success);
            Assert.AreEqual(100f - MissionRuleSet.EvaRepairBatteryCost, game.State.SuitBattery, .0001f);
            Assert.AreEqual(100f - MissionRuleSet.EvaRepairOxygenCost, game.State.SuitOxygen, .0001f);
            Assert.IsTrue(game.FinishEva().Success);

            Assert.IsFalse(game.RechargeSuit().Success); // Suporte de vida e recarga ainda desligados
            Assert.IsTrue(game.Repair(EquipmentId.LifeSupport).Success);
            Assert.IsTrue(game.SetPowered(EquipmentId.LifeSupport, true).Success);
            Assert.IsTrue(game.SetPowered(EquipmentId.SuitCharger, true).Success); // 1 + 4 + 2 = 7/8
            Assert.IsTrue(game.RechargeSuit().Success);
            Assert.AreEqual(100f, game.State.SuitBattery, .0001f);
            Assert.AreEqual(100f, game.State.SuitOxygen, .0001f);
        }

        [Test] public void ChecklistDeVitoriaRefleteEstadoAtualECoincideComResolveMission()
        {
            MissionEngine game = new MissionEngine();
            VictoryCriterion[] inicio = game.GetVictoryChecklist();
            Assert.AreEqual(7, inicio.Length);
            Assert.IsFalse(inicio[0].Met); // Painéis solares
            Assert.IsFalse(inicio[1].Met); // Suporte de vida ligado
            Assert.IsFalse(inicio[2].Met); // Abrigo
            Assert.IsFalse(inicio[3].Met); // Estufa ligada
            Assert.IsTrue(inicio[4].Met);  // Comida inicial 3 >= 2
            Assert.IsTrue(inicio[5].Met);  // Água inicial 5 >= 1
            Assert.IsTrue(inicio[6].Met);  // Ar inicial 55 >= 30

            game.StartEva(); Assert.IsTrue(game.Repair(EquipmentId.SolarPanels).Success); game.FinishEva();
            Assert.IsTrue(game.Repair(EquipmentId.LifeSupport).Success);
            Assert.IsTrue(game.Repair(EquipmentId.Greenhouse).Success);
            Assert.IsTrue(game.SetPowered(EquipmentId.LifeSupport, true).Success);
            Assert.IsTrue(game.SetPowered(EquipmentId.Greenhouse, true).Success);
            Assert.IsTrue(game.AdvanceTurn().Success);
            Assert.IsTrue(game.Repair(EquipmentId.RadiationShelter).Success);
            Assert.IsTrue(game.Repair(EquipmentId.WaterRecycler).Success);
            Assert.IsTrue(game.AdvanceTurn().Success);
            Assert.IsTrue(game.AdvanceTurn().Success);

            Assert.AreEqual(MissionOutcome.Success, game.State.Outcome);
            VictoryCriterion[] fim = game.GetVictoryChecklist();
            Assert.AreEqual(7, fim.Length);
            for (int i = 0; i < fim.Length; i++) Assert.IsTrue(fim[i].Met);
        }

        [Test] public void SnapshotPreservaPoseEmEvaERestauraSaveAntigoSemPose()
        {
            MissionEngine game = new MissionEngine();
            game.StartEva();
            MissionSnapshot comPose = game.State;
            comPose.HasPlayerPose = true;
            comPose.PlayerPosX = 12.5f;
            comPose.PlayerPosY = 1.2f;
            comPose.PlayerPosZ = -8.4f;
            comPose.PlayerRotY = 145f;

            MissionEngine restaurado = MissionEngine.Restore(comPose);
            MissionSnapshot lido = restaurado.State;
            Assert.IsTrue(lido.IsOutside);
            Assert.IsTrue(lido.HasPlayerPose);
            Assert.AreEqual(12.5f, lido.PlayerPosX, .0001f);
            Assert.AreEqual(1.2f, lido.PlayerPosY, .0001f);
            Assert.AreEqual(-8.4f, lido.PlayerPosZ, .0001f);
            Assert.AreEqual(145f, lido.PlayerRotY, .0001f);

            MissionSnapshot saveAntigo = new MissionEngine().State;
            saveAntigo.HasPlayerPose = false;
            MissionEngine antigoRestaurado = MissionEngine.Restore(saveAntigo);
            Assert.IsFalse(antigoRestaurado.State.HasPlayerPose);
        }

        [Test] public void CanRepairDevolveMotivoCurtoQuandoTempoOuLocalImpedem()
        {
            MissionEngine game = new MissionEngine();
            OperationResult foraBase = game.CanRepair(EquipmentId.SolarPanels);
            Assert.IsFalse(foraBase.Success);
            Assert.IsTrue(foraBase.Message.Contains("eclusa"));

            game.StartEva();
            OperationResult dentroBase = game.CanRepair(EquipmentId.LifeSupport);
            Assert.IsFalse(dentroBase.Success);
            Assert.AreEqual("Volte para dentro da base.", dentroBase.Message);
            game.FinishEva();

            Assert.IsTrue(game.Repair(EquipmentId.LifeSupport).Success);      // 4 restantes
            Assert.IsTrue(game.Repair(EquipmentId.Greenhouse).Success);       // 2 restantes
            Assert.IsTrue(game.Repair(EquipmentId.WaterRecycler).Success);    // 0 restantes

            OperationResult semTempo = game.CanRepair(EquipmentId.RadiationShelter);
            Assert.IsFalse(semTempo.Success);
            Assert.AreEqual("Faltam 2 tempos. Termine o turno!", semTempo.Message);
            Assert.AreEqual(semTempo.Message, game.Repair(EquipmentId.RadiationShelter).Message);
        }
        [Test] public void RecargaPrioritariaFuncionaComProducaoLigadaSemAlterarDistribuicao()
        {
            var game=new MissionEngine();game.StartEva();game.Repair(EquipmentId.SolarPanels);game.FinishEva();
            game.Repair(EquipmentId.LifeSupport);game.Repair(EquipmentId.Greenhouse);
            game.SetPowered(EquipmentId.LifeSupport,true);game.SetPowered(EquipmentId.Greenhouse,true);game.AdvanceTurn();
            int before=game.State.RemainingActionTime,power=game.PowerUsed;
            Assert.IsTrue(game.RechargeWithPriority().Success);
            Assert.AreEqual(100,game.State.SuitOxygen);Assert.AreEqual(100,game.State.SuitBattery);
            Assert.AreEqual(before-1,game.State.RemainingActionTime);Assert.AreEqual(power,game.PowerUsed);
            Assert.IsTrue(game.IsPowered(EquipmentId.Greenhouse));Assert.IsTrue(game.IsPowered(EquipmentId.LifeSupport));
            Assert.IsFalse(game.IsPowered(EquipmentId.SuitCharger));
        }
        [Test] public void RecargaPrioritariaNaoCobraTrajeCheioNemFuncionaForaDaBase()
        {
            var game=new MissionEngine();game.StartEva();game.Repair(EquipmentId.SolarPanels);game.FinishEva();
            game.Repair(EquipmentId.LifeSupport);game.SetPowered(EquipmentId.LifeSupport,true);
            Assert.IsTrue(game.RechargeWithPriority().Success);
            int before=game.State.RemainingActionTime;Assert.IsFalse(game.RechargeWithPriority().Success);
            Assert.AreEqual(before,game.State.RemainingActionTime);
            game.StartEva();game.Tick(2);Assert.IsFalse(game.RechargeWithPriority().Success);
            Assert.AreEqual(before,game.State.RemainingActionTime);
        }
        [Test] public void RecargaSemSuporteDeVidaMantemRecursos()
        {
            var game=new MissionEngine();var before=game.State;
            Assert.IsFalse(game.RechargeWithPriority().Success);
            Assert.AreEqual(before.RemainingActionTime,game.State.RemainingActionTime);
            Assert.AreEqual(before.SuitOxygen,game.State.SuitOxygen);Assert.AreEqual(before.SuitBattery,game.State.SuitBattery);
        }
        [Test] public void SavePreservaReferenciaDoMundoEsfericoEOrientacaoCompleta()
        {
            var game=new MissionEngine();var snapshot=game.State;snapshot.WorldLayoutVersion=2;snapshot.HasPlayerQuaternion=true;
            snapshot.PlayerQX=.5f;snapshot.PlayerQY=.5f;snapshot.PlayerQZ=.5f;snapshot.PlayerQW=.5f;
            var restored=MissionEngine.Restore(snapshot).State;
            Assert.AreEqual(2,restored.WorldLayoutVersion);Assert.IsTrue(restored.HasPlayerQuaternion);
            Assert.AreEqual(.5f,restored.PlayerQX);Assert.AreEqual(.5f,restored.PlayerQW);
        }
    }
}
