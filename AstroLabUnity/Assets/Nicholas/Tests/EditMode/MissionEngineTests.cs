using AstroLab.Nicholas.Core;
using NUnit.Framework;

namespace AstroLab.Nicholas.Tests
{
    public sealed class MissionEngineTests
    {
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
            Assert.AreEqual(16, game.State.SpareParts);
            Assert.IsFalse(game.IsRepaired(EquipmentId.SolarPanels));
            Assert.IsTrue(game.StartEva().Success);
            Assert.IsTrue(game.Repair(EquipmentId.SolarPanels).Success);
            Assert.IsFalse(game.Repair(EquipmentId.SolarPanels).Success); // não cobra duas vezes
            Assert.AreEqual(14, game.State.SpareParts);
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
            Assert.AreEqual(100f - 0.18f * 0.2f, game.State.SuitOxygen, .00001f);
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
    }
}
