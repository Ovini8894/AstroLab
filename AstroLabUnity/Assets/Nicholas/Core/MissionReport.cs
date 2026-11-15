using System;
using System.Text;

namespace AstroLab.Nicholas.Core
{
    public static class MissionReport
    {
        public static string Build(MissionEngine engine)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            MissionSnapshot state = engine.State;
            StringBuilder text = new StringBuilder();
            text.AppendLine("ASTROLAB — RELATÓRIO DA MISSÃO LUNAR");
            text.AppendLine("Resultado: " + state.Outcome);
            text.AppendLine("Explicação: " + state.OutcomeReason);
            text.AppendLine("Turnos: " + state.Turn + "/" + MissionRuleSet.MaximumTurns);
            text.AppendLine("Recursos: peças " + state.SpareParts + ", ar " + state.HabitatAir +
                            ", água " + state.Water + ", alimentos " + state.Food);
            text.AppendLine("Potência: " + engine.PowerUsed + "/" + engine.PowerCapacity +
                            " (pontos fictícios por turno)");
            text.AppendLine("Coleção Apollo 15: " + state.NextArtifactIndex + "/" + Apollo15Trail.Items.Length);
            text.AppendLine();
            text.AppendLine("REGISTRO DE DECISÕES");
            for (int i = 0; i < state.History.Count; i++)
                text.AppendLine("[T" + state.History[i].Turn + "][" + state.History[i].Type + "] " + state.History[i].Text);
            text.AppendLine();
            text.AppendLine("LIÇÃO: geração de energia e armazenamento são limitados. Reparo não implica consumo zero.");
            text.AppendLine("Os valores numéricos são abstratos para fins educativos, não dados de engenharia lunar.");
            return text.ToString();
        }
    }
}
