using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AstroLab.Asteroids.Tests
{
    public sealed class AsteroidScienceTests
    {
        [Test]
        public void NumericJsonMeasurementsAreIndependentOfPortugueseLocale()
        {
            var before = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
                var asset = Resources.Load<TextAsset>("asteroid_archive");
                var records = AsteroidScience.ParseFeed(asset.text, true);
                Assert.That(records.Any(r => r.MinMetres > 0 && r.SpeedKmS > 0), Is.True);
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = before; }
        }

        [Test]
        public void ShippedArchiveAllowsTenDistinctAnalysesWithoutNetworkOrTodaysFeed()
        {
            var asset = Resources.Load<TextAsset>("asteroid_archive");
            Assert.That(asset, Is.Not.Null);
            var records = AsteroidScience.Merge(AsteroidScience.ParseFeed(asset.text, true), new List<AsteroidRecord>());
            Assert.That(records.Count(AsteroidScience.CanAnalyse), Is.GreaterThanOrEqualTo(10));
            var progress = new AsteroidProgress();
            foreach (var r in records.Where(AsteroidScience.CanAnalyse).Take(10)) Assert.That(progress.Complete(r), Is.True);
            Assert.That(progress.Count, Is.EqualTo(10));
        }

        [Test]
        public void SameObjectOnAnotherDateDoesNotAwardAnotherDiscovery()
        {
            var progress = new AsteroidProgress();
            Assert.That(progress.Complete(Record("1", "2025-01-01", true)), Is.True);
            Assert.That(progress.Complete(Record("1", "2026-11-15", false)), Is.False);
            var restored = JsonUtility.FromJson<AsteroidProgress>(JsonUtility.ToJson(progress));
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(restored.Complete(Record("1", "2026-11-16", false)), Is.False);
        }

        [Test]
        public void EmptyDailyFeedPreservesArchiveAndRecentDataReplacesHistoricalDuplicate()
        {
            var archive = new[] { Record("1", "2025-01-01", true), Record("2", "2025-01-01", true) };
            Assert.That(AsteroidScience.Merge(archive, AsteroidScience.ParseFeed("{\"near_earth_objects\":{}}", false)).Count, Is.EqualTo(2));
            var merged = AsteroidScience.Merge(archive, new[] { Record("1", "2026-11-15", false) });
            Assert.That(merged.Count, Is.EqualTo(2));
            Assert.That(merged[0].ApproachDate, Is.EqualTo("2026-11-15"));
        }

        [Test]
        public void MissingMeasurementsAreNotInventedOrEligibleForSizeChallenge()
        {
            const string json = "{\"near_earth_objects\":{\"2026-11-15\":[{\"id\":\"a\",\"name\":\"Test\",\"close_approach_data\":[{\"orbiting_body\":\"Earth\",\"close_approach_date\":\"2026-11-15\"}]}]}}";
            var record = AsteroidScience.ParseFeed(json, false).Single();
            Assert.That(record.MinMetres, Is.EqualTo(-1));
            Assert.That(record.DistanceKm, Is.EqualTo(-1));
            Assert.That(new AsteroidProgress().Complete(record), Is.False);
        }

        [Test]
        public void NonEarthEncountersAreNotLabelledAsEarthApproaches()
        {
            const string json = "{\"near_earth_objects\":{\"2026-11-15\":[{\"id\":\"a\",\"close_approach_data\":[{\"orbiting_body\":\"Mars\",\"close_approach_date\":\"2026-11-15\"}]}]}}";
            Assert.That(AsteroidScience.ParseFeed(json, false), Is.Empty);
        }

        [Test]
        public void AnalysisCannotSkipEstimateAndOnlyCorrectInterpretationCompletesIt()
        {
            var analysis = new AsteroidAnalysis(Record("1", "2026-11-15", false), 0);
            Assert.That(analysis.Question, Is.Null);
            Assert.That(analysis.Answer(0), Is.False);
            Assert.That(analysis.SubmitEstimate(0), Is.False);
            Assert.That(analysis.SubmitEstimate(4), Is.False);
            Assert.That(analysis.SubmitEstimate(30), Is.False);
            Assert.That(analysis.SubmitEstimate(15), Is.True);
            var question = analysis.Question;
            Assert.That(analysis.Answer((question.CorrectIndex + 1) % 3), Is.False);
            Assert.That(analysis.Completed, Is.False);
            Assert.That(analysis.Answer(question.CorrectIndex), Is.True);
            Assert.That(analysis.Completed, Is.True);
            Assert.That(analysis.Answer(question.CorrectIndex), Is.False);
        }

        [Test]
        public void EntireEstimatedRangeIsAcceptedWithoutRequiringAFictitiousExactDiameter()
        {
            var record = Record("1", "2026-11-15", false);
            foreach (double guess in new[] { record.MinMetres, 12.5, 17.5, record.MaxMetres })
                Assert.That(new AsteroidAnalysis(record, 0).SubmitEstimate(guess), Is.True);
            Assert.That(new AsteroidAnalysis(record, 0).SubmitEstimate(double.NaN), Is.False);
            Assert.That(new AsteroidAnalysis(record, 0).SubmitEstimate(double.PositiveInfinity), Is.False);
        }

        [Test]
        public void ComparisonScaleDoesNotExposeEitherEndOfTheNasaDiameterRange()
        {
            var analysis = new AsteroidAnalysis(Record("1", "2026-11-15", false), 0);
            Assert.That(analysis.ScaleMaximum, Is.GreaterThan(analysis.Record.MaxMetres));
            Assert.That(analysis.RepresentativeMetres, Is.InRange(analysis.Record.MinMetres, analysis.Record.MaxMetres));
            Assert.That(analysis.GuessMetres, Is.Zero);
            Assert.That(analysis.SizeAccepted, Is.False);
        }

        [Test]
        public void ReanalysingAnObjectCyclesAllAvailableTopicsAndChangesAnswerPositions()
        {
            var record = Record("1", "2026-11-15", false);
            record.SpeedKmS = 20; record.DistanceKm = AsteroidScience.MeanLunarDistanceKm * 2;
            var questions = Enumerable.Range(0, 12).Select(round => AsteroidAnalysis.CreateQuestion(record, round)).ToList();
            Assert.That(questions.Select(q => q.Kind).Distinct().Count(), Is.EqualTo(6));
            Assert.That(questions.Select(q => q.CorrectIndex).Distinct().Count(), Is.GreaterThan(1));
            Assert.That(questions.All(q => q.Options.Length == 3 && q.Options.Distinct().Count() == 3), Is.True);
        }

        [Test]
        public void MissingSpeedAndDistanceExcludeQuestionsAboutUnknownValues()
        {
            var record = Record("no-velocity", "2026-11-15", false);
            var questions = Enumerable.Range(0, 12).Select(round => AsteroidAnalysis.CreateQuestion(record, round)).ToList();
            Assert.That(questions.All(q => q.Kind == AsteroidQuestionKind.ModuleScale || q.Kind == AsteroidQuestionKind.AstronautScale || q.Kind == AsteroidQuestionKind.Uncertainty), Is.True);
            Assert.That(questions.Select(q => q.Kind).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void TravelQuestionUsesSecondsAndLunarDistanceQuestionKeepsEarthReference()
        {
            var record = Record("conversion", "2026-11-15", false);
            record.SpeedKmS = 20; record.DistanceKm = AsteroidScience.MeanLunarDistanceKm * 2;
            var questions = Enumerable.Range(0, 6).Select(round => AsteroidAnalysis.CreateQuestion(record, round)).ToList();
            var travel = questions.Single(q => q.Kind == AsteroidQuestionKind.TravelInOneMinute);
            Assert.That(travel.Options[travel.CorrectIndex], Is.EqualTo("1.200,0 km"));
            var lunar = questions.Single(q => q.Kind == AsteroidQuestionKind.LunarDistance);
            Assert.That(lunar.Options[lunar.CorrectIndex], Is.EqualTo("2,0 distâncias Terra–Lua"));
            Assert.That(lunar.Prompt, Does.Contain("À TERRA"));
        }

        [Test]
        public void EveryShippedArchiveObjectOffersDistinctOptionsAndAnAcceptableVisualEstimate()
        {
            var records = AsteroidScience.ParseFeed(Resources.Load<TextAsset>("asteroid_archive").text, true);
            foreach (var record in records.Where(AsteroidScience.CanAnalyse))
            {
                var analysis = new AsteroidAnalysis(record, 0);
                Assert.That(analysis.SubmitEstimate(analysis.RepresentativeMetres), Is.True, record.Id);
                foreach (int round in Enumerable.Range(0, 6))
                    Assert.That(AsteroidAnalysis.CreateQuestion(record, round).Options.Distinct().Count(), Is.EqualTo(3), record.Id);
            }
        }

        private static AsteroidRecord Record(string id, string date, bool history) => new AsteroidRecord {
            Id = id, Name = id, ApproachDate = date, Historical = history, MinMetres = 10, MaxMetres = 20
        };
    }
}
