using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AstroLab.Asteroids
{
    public sealed class NeoWsClient : MonoBehaviour
    {
        public List<AsteroidRecord> Records { get; private set; } = new List<AsteroidRecord>();
        public string Status { get; private set; } = "Carregando acervo científico…";
        public bool Busy { get; private set; }
        public event Action Changed;
        private List<AsteroidRecord> archive;
        private float nextRefresh;
        private string CachePath => Path.Combine(Application.persistentDataPath, "astrolab_asteroids_cache.json");

        private void Awake()
        {
            TextAsset data = Resources.Load<TextAsset>("asteroid_archive");
            try { archive = data ? AsteroidScience.ParseFeed(data.text, true) : new List<AsteroidRecord>(); }
            catch { archive = new List<AsteroidRecord>(); }
            Records = AsteroidScience.Merge(archive, new List<AsteroidRecord>());
            try
            {
                if (File.Exists(CachePath))
                {
                    // Old cache remains available as historical material, never advertised as today's forecast.
                    Records = AsteroidScience.Merge(Records, AsteroidScience.ParseFeed(File.ReadAllText(CachePath), true));
                    Status = "Acervo e última consulta salva · " + File.GetLastWriteTime(CachePath).ToString("dd/MM/yyyy");
                }
                else Status = "Acervo histórico NASA · disponível sem conexão";
            }
            catch { Status = "Acervo histórico NASA · cache indisponível"; }
            Debug.Log("[AstroLab][Asteroids] Acervo disponível: " + Records.Count + " objetos distintos.");
        }

        private void Start() { Refresh(); }
        public void Refresh()
        {
            if (Busy || Time.unscaledTime < nextRefresh) return;
            string key = ReadLocalKey();
            if (string.IsNullOrWhiteSpace(key)) { Status = "Acervo histórico NASA · consulta recente não configurada"; Changed?.Invoke(); return; }
            StartCoroutine(Fetch(key));
        }

        private static string ReadLocalKey()
        {
            string key = Environment.GetEnvironmentVariable("ASTROLAB_NASA_API_KEY");
            if (!string.IsNullOrWhiteSpace(key)) return key.Trim();
            string path = Path.Combine(Application.persistentDataPath, "nasa_api_key.txt");
            try { if (File.Exists(path)) return File.ReadAllText(path).Trim(); } catch { }
#if UNITY_EDITOR
            try
            {
                path = Path.Combine(Application.dataPath, "../UserSettings/NasaApiKey.txt");
                if (File.Exists(path)) return File.ReadAllText(path).Trim();
            }
            catch { }
#endif
            return null;
        }

        private IEnumerator Fetch(string key)
        {
            Busy = true; nextRefresh = Time.unscaledTime + 60;
            Status = "Consultando aproximações dos próximos sete dias…"; Changed?.Invoke();
            DateTime today = DateTime.UtcNow.Date;
            string url = "https://api.nasa.gov/neo/rest/v1/feed?start_date=" + today.ToString("yyyy-MM-dd") +
                         "&end_date=" + today.AddDays(6).ToString("yyyy-MM-dd") + "&api_key=" + UnityWebRequest.EscapeURL(key);
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var recent = AsteroidScience.ParseFeed(request.downloadHandler.text, false);
                        // Keep all previously available objects, including when a feed is empty.
                        foreach (var old in Records) old.Historical = true;
                        Records = AsteroidScience.Merge(Records, recent);
                        // Feed contains credential-bearing pagination URLs. Persist scientific records only.
                        var clean = Newtonsoft.Json.Linq.JObject.Parse(request.downloadHandler.text);
                        clean.Remove("links");
                        foreach (var date in ((Newtonsoft.Json.Linq.JObject)clean["near_earth_objects"]).Properties())
                            foreach (var item in date.Value) ((Newtonsoft.Json.Linq.JObject)item).Remove("links");
                        try { File.WriteAllText(CachePath, clean.ToString()); } catch { }
                        Status = recent.Count > 0 ? "NASA atualizada · " + today.ToString("dd/MM/yyyy") + " · aproximações à Terra" :
                            "Nenhum alvo neste período · explore o acervo histórico";
                        Debug.Log("[AstroLab][Asteroids] Consulta concluída; coleção mantém " + Records.Count + " objetos distintos.");
                    }
                    catch { Status = "Resposta indisponível · acervo preservado"; }
                }
                else
                {
                    Status = request.responseCode == 429 ? "Limite de consultas atingido · explore o acervo" : "Sem atualização · explore o acervo salvo";
                    // Never log request.error or URL: a URL may contain the private API key.
                    Debug.LogWarning("[AstroLab][Asteroids] Atualização indisponível; acervo preservado. HTTP " + request.responseCode);
                }
            }
            Busy = false; Changed?.Invoke();
        }
    }
}
