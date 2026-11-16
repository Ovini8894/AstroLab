using System;
using System.Collections.Generic;
using System.Globalization;

namespace AstroLab.Asteroids
{
    public enum AsteroidQuestionKind { ModuleScale, AstronautScale, Uncertainty, TravelInOneMinute, SpeedConversion, LunarDistance }

    public sealed class AsteroidQuestion
    {
        public AsteroidQuestionKind Kind { get; }
        public string Prompt { get; }
        public string[] Options { get; }
        public int CorrectIndex { get; }
        public string Hint { get; }
        public string Explanation { get; }
        internal AsteroidQuestion(AsteroidQuestionKind kind, string prompt, double correct, double wrongA, double wrongB,
            string unit, string hint, string explanation, Random random)
        {
            Kind = kind; Prompt = prompt; Hint = hint; Explanation = explanation;
            var values = new[] { correct, wrongA, wrongB };
            var order = new[] { 0, 1, 2 };
            for (int i = 2; i > 0; i--) { int j = random.Next(i + 1); int value = order[i]; order[i] = order[j]; order[j] = value; }
            Options = new string[3];
            for (int i = 0; i < 3; i++)
            {
                Options[i] = Format(values[order[i]]) + " " + unit;
                if (order[i] == 0) CorrectIndex = i;
            }
        }
        internal static string Format(double value) => value.ToString(value < 1 ? "N3" : "N1", CultureInfo.GetCultureInfo("pt-BR"));
    }

    /// <summary>Two-stage activity. A diameter range is accepted, never treated as an exact measured size.</summary>
    public sealed class AsteroidAnalysis
    {
        public AsteroidRecord Record { get; }
        public bool SizeAccepted { get; private set; }
        public bool Completed { get; private set; }
        public double GuessMetres { get; private set; }
        public AsteroidQuestion Question { get; private set; }
        private readonly int round;

        public AsteroidAnalysis(AsteroidRecord record, int round)
        {
            if (!AsteroidScience.CanAnalyse(record)) throw new ArgumentException("Diâmetro estimado indisponível", nameof(record));
            Record = record; this.round = Math.Max(0, round);
        }

        // Used only to draw the didactic comparison; it is explicitly not a NASA shape/size measurement.
        public double RepresentativeMetres => Math.Sqrt(Record.MinMetres * Record.MaxMetres);
        public double ScaleMaximum
        {
            get
            {
                double desired = Math.Max(12, Record.MaxMetres * 1.5);
                double power = Math.Pow(10, Math.Floor(Math.Log10(desired)));
                return Math.Ceiling(desired / power) * power;
            }
        }

        public bool SubmitEstimate(double metres)
        {
            if (Completed || SizeAccepted || double.IsNaN(metres) || double.IsInfinity(metres) || metres <= 0) return false;
            GuessMetres = metres;
            // Half of the UI's 0.1 m display precision, rather than a fictitious scientific tolerance.
            if (metres < Record.MinMetres - .05 || metres > Record.MaxMetres + .05) return false;
            SizeAccepted = true;
            Question = CreateQuestion(Record, round);
            return true;
        }

        public bool Answer(int option)
        {
            if (!SizeAccepted || Completed || Question == null || option != Question.CorrectIndex) return false;
            Completed = true;
            return true;
        }

        public static AsteroidQuestion CreateQuestion(AsteroidRecord r, int round)
        {
            if (!AsteroidScience.CanAnalyse(r)) throw new ArgumentException("Diâmetro estimado indisponível", nameof(r));
            var kinds = new List<AsteroidQuestionKind> { AsteroidQuestionKind.ModuleScale, AsteroidQuestionKind.AstronautScale };
            if (r.MaxMetres > r.MinMetres * 1.01) kinds.Add(AsteroidQuestionKind.Uncertainty);
            if (FinitePositive(r.SpeedKmS)) { kinds.Add(AsteroidQuestionKind.TravelInOneMinute); kinds.Add(AsteroidQuestionKind.SpeedConversion); }
            if (FinitePositive(r.DistanceKm)) kinds.Add(AsteroidQuestionKind.LunarDistance);
            int seed = StableSeed(r.Id);
            var random = new Random(unchecked(seed + Math.Max(0, round) * 7919) & int.MaxValue);
            var kind = kinds[(seed % kinds.Count + Math.Max(0, round) % kinds.Count) % kinds.Count];
            double value;
            switch (kind)
            {
                case AsteroidQuestionKind.ModuleScale:
                    value = r.MinMetres / 12;
                    return new AsteroidQuestion(kind, "Usando o MENOR diâmetro estimado, quantos módulos de 12 m cabem lado a lado nessa largura?", value, value * 2, value / 2, "módulos",
                        "Divida a estimativa mínima por 12. Arredonde para uma casa decimal.",
                        "Menor diâmetro ÷ 12 m = aproximadamente " + AsteroidQuestion.Format(value) + " módulos.", random);
                case AsteroidQuestionKind.AstronautScale:
                    value = r.MaxMetres / 1.8;
                    return new AsteroidQuestion(kind, "No MAIOR diâmetro estimado, quantos astronautas de 1,8 m caberiam lado a lado?", value, value * 1.8, value / 1.8, "astronautas",
                        "Divida a estimativa máxima pela altura de 1,8 m.",
                        "Maior diâmetro ÷ 1,8 m = aproximadamente " + AsteroidQuestion.Format(value) + " astronautas.", random);
                case AsteroidQuestionKind.Uncertainty:
                    value = r.MaxMetres - r.MinMetres;
                    return new AsteroidQuestion(kind, "Qual é a largura da faixa de incerteza do diâmetro estimado?", value, r.MaxMetres + r.MinMetres, value / 2, "m",
                        "Subtraia a estimativa mínima da máxima. A faixa não é uma medida exata única.",
                        "Máximo − mínimo = " + AsteroidQuestion.Format(value) + " m de intervalo estimado.", random);
                case AsteroidQuestionKind.TravelInOneMinute:
                    value = r.SpeedKmS * 60;
                    return new AsteroidQuestion(kind, "Mantendo a velocidade informada na aproximação, que distância ele percorreria em 1 minuto?", value, r.SpeedKmS, value * 60, "km",
                        "A velocidade está em km/s. Um minuto tem 60 segundos.",
                        "Velocidade × 60 s = aproximadamente " + AsteroidQuestion.Format(value) + " km; hipótese de velocidade constante.", random);
                case AsteroidQuestionKind.SpeedConversion:
                    value = r.SpeedKmS * 3600;
                    return new AsteroidQuestion(kind, "Como fica a velocidade da ficha quando convertida de km/s para km/h?", value, r.SpeedKmS * 60, r.SpeedKmS * 3.6, "km/h",
                        "Uma hora tem 3.600 segundos. Multiplique a velocidade por esse número.",
                        "Velocidade em km/s × 3.600 = aproximadamente " + AsteroidQuestion.Format(value) + " km/h.", random);
                default:
                    value = r.DistanceKm / AsteroidScience.MeanLunarDistanceKm;
                    return new AsteroidQuestion(kind, "A distância de aproximação À TERRA equivale a quantas distâncias médias Terra–Lua (384.400 km)?", value, value * 10, value / 10, "distâncias Terra–Lua",
                        "Divida a distância da ficha por 384.400 km. Isso não é distância até a base lunar.",
                        "Distância à Terra ÷ 384.400 km = aproximadamente " + AsteroidQuestion.Format(value) + " distâncias médias Terra–Lua.", random);
            }
        }
        private static bool FinitePositive(double n) => n > 0 && !double.IsNaN(n) && !double.IsInfinity(n);
        private static int StableSeed(string id)
        {
            uint hash = 2166136261;
            foreach (char c in id ?? "") hash = unchecked((hash ^ c) * 16777619);
            return (int)(hash & int.MaxValue);
        }
    }
}
