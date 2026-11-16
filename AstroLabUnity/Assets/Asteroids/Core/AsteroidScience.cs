using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AstroLab.Asteroids
{
    [Serializable]
    public sealed class AsteroidRecord
    {
        public string Id, Name, ApproachDate;
        public double MinMetres = -1, MaxMetres = -1, SpeedKmS = -1, DistanceKm = -1;
        public bool Historical;
    }

    public static class AsteroidScience
    {
        public const double MeanLunarDistanceKm = 384400;

        // Read only Earth encounters. An encounter date is not a visibility prediction from the Moon.
        public static List<AsteroidRecord> ParseFeed(string json, bool historical)
        {
            JObject root = JObject.Parse(json);
            JObject dates = root["near_earth_objects"] as JObject;
            var result = new List<AsteroidRecord>();
            if (dates == null) return result;
            foreach (var day in dates.Properties())
            foreach (JObject item in day.Value.OfType<JObject>())
            {
                string id = (string)item["id"];
                if (string.IsNullOrWhiteSpace(id)) continue;
                var encounters = item["close_approach_data"] as JArray;
                JObject approach = encounters?.OfType<JObject>().FirstOrDefault(a =>
                    (string)a["orbiting_body"] == "Earth" && (string)a["close_approach_date"] == day.Name);
                if (approach == null) continue;
                JToken diameter = item["estimated_diameter"]?["meters"];
                result.Add(new AsteroidRecord {
                    Id = id, Name = (string)item["name"] ?? id, ApproachDate = day.Name,
                    MinMetres = Number(diameter?["estimated_diameter_min"]),
                    MaxMetres = Number(diameter?["estimated_diameter_max"]),
                    SpeedKmS = Number(approach["relative_velocity"]?["kilometers_per_second"]),
                    DistanceKm = Number(approach["miss_distance"]?["kilometers"]), Historical = historical
                });
            }
            return result;
        }

        private static double Number(JToken token)
        {
            double n;
            string raw = token == null ? null : token.Type == JTokenType.Float || token.Type == JTokenType.Integer
                ? token.Value<double>().ToString("R", CultureInfo.InvariantCulture) : token.ToString();
            return raw != null && double.TryParse(raw, NumberStyles.Float,
                CultureInfo.InvariantCulture, out n) && !double.IsNaN(n) && !double.IsInfinity(n) && n >= 0 ? n : -1;
        }

        public static List<AsteroidRecord> Merge(IEnumerable<AsteroidRecord> archive, IEnumerable<AsteroidRecord> recent)
        {
            var records = new Dictionary<string, AsteroidRecord>();
            foreach (AsteroidRecord r in archive.Concat(recent))
                if (r != null && !string.IsNullOrWhiteSpace(r.Id))
                {
                    if (!records.TryGetValue(r.Id, out AsteroidRecord old) ||
                        (old.Historical && !r.Historical) ||
                        (old.Historical == r.Historical && string.CompareOrdinal(r.ApproachDate, old.ApproachDate) < 0))
                        records[r.Id] = r;
                }
            return records.Values.OrderBy(r => r.Historical).ThenBy(r => r.ApproachDate).ThenBy(r => r.Name).ToList();
        }

        // A complete scientific comparison is required for this activity; missing fields are never invented.
        public static bool CanAnalyse(AsteroidRecord r) => r != null && !string.IsNullOrWhiteSpace(r.Id) && r.MinMetres > 0 && r.MaxMetres >= r.MinMetres;
    }

    [Serializable]
    public sealed class AsteroidProgress
    {
        public List<string> AnalysedIds = new List<string>();
        public int Count => AnalysedIds == null ? 0 : AnalysedIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Count();
        public bool Contains(string id) => AnalysedIds != null && AnalysedIds.Contains(id);
        public bool Complete(AsteroidRecord record)
        {
            if (!AsteroidScience.CanAnalyse(record) || Contains(record.Id)) return false;
            if (AnalysedIds == null) AnalysedIds = new List<string>();
            AnalysedIds.Add(record.Id);
            return true;
        }
    }
}
