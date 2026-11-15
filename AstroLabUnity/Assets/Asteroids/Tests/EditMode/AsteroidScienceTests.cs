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

        private static AsteroidRecord Record(string id, string date, bool history) => new AsteroidRecord {
            Id = id, Name = id, ApproachDate = date, Historical = history, MinMetres = 10, MaxMetres = 20
        };
    }
}
