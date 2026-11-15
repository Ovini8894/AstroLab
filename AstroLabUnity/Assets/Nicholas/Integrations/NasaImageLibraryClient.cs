using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AstroLab.Nicholas.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace AstroLab.Nicholas.Integrations
{
    [Serializable]
    public sealed class NasaMediaRecord
    {
        public string Artifact;
        public string NasaId;
        public string Title;
        public string Description;
        public string DateCreated;
        public string ThumbnailUrl;
        public string CatalogUrl;
        public bool LiveFromNasa;
    }

    [Serializable] internal sealed class NasaSearchResponse { public NasaCollection collection; }
    [Serializable] internal sealed class NasaCollection { public NasaItem[] items; }
    [Serializable] internal sealed class NasaItem { public NasaData[] data; public NasaLink[] links; }
    [Serializable] internal sealed class NasaData { public string nasa_id; public string title; public string description; public string date_created; }
    [Serializable] internal sealed class NasaLink { public string href; public string rel; }
    [Serializable] internal sealed class NasaCache { public List<NasaMediaRecord> entries = new List<NasaMediaRecord>(); }

    /// <summary>
    /// NASA Images API pública: descrições e metadados existentes; não inventa histórias.
    /// Registro histórico em português é curadoria local, não resposta da API.
    /// Sem rede, mantém ficha local e último resultado cacheado.
    /// </summary>
    public sealed class NasaImageLibraryClient : MonoBehaviour
    {
        private const string BaseUrl = "https://images-api.nasa.gov/search?q=";
        private readonly Dictionary<ArtifactId, NasaMediaRecord> records = new Dictionary<ArtifactId, NasaMediaRecord>();
        private readonly HashSet<ArtifactId> inFlight = new HashSet<ArtifactId>();
        public event Action<ArtifactId, NasaMediaRecord> RecordUpdated;
        public string CachePath { get { return Path.Combine(Application.persistentDataPath, "apollo15_media_cache.json"); } }

        private void Awake()
        {
            try
            {
                if (!File.Exists(CachePath)) return;
                NasaCache cache = JsonUtility.FromJson<NasaCache>(File.ReadAllText(CachePath));
                if (cache == null || cache.entries == null) return;
                foreach (NasaMediaRecord r in cache.entries)
                    if (Enum.TryParse(r.Artifact, out ArtifactId id) && Enum.IsDefined(typeof(ArtifactId), id)) records[id] = r;
            }
            catch (Exception error) { Debug.LogWarning("[NASA] Cache ignorado: " + error.Message); }
        }

        public NasaMediaRecord GetOrStart(ArtifactId id)
        {
            if (!records.ContainsKey(id))
            {
                Apollo15Trail.Artifact item = Apollo15Trail.Find(id);
                records[id] = new NasaMediaRecord
                {
                    Artifact = id.ToString(), Title = item.Name, Description = item.History,
                    CatalogUrl = item.OfficialSource, LiveFromNasa = false
                };
            }
            if (!inFlight.Contains(id) && !records[id].LiveFromNasa && Application.internetReachability != NetworkReachability.NotReachable)
                StartCoroutine(SearchNasa(id));
            return records[id];
        }

        private IEnumerator SearchNasa(ArtifactId id)
        {
            inFlight.Add(id);
            Apollo15Trail.Artifact item = Apollo15Trail.Find(id);
            string url = BaseUrl + UnityWebRequest.EscapeURL(item.NasaQuery) + "&media_type=image&page_size=5";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 12;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        NasaSearchResponse response = JsonUtility.FromJson<NasaSearchResponse>(request.downloadHandler.text);
                        if (response != null && response.collection != null && response.collection.items != null && response.collection.items.Length > 0)
                        {
                            NasaItem first = response.collection.items[0];
                            if (first.data != null && first.data.Length > 0)
                            {
                                NasaData data = first.data[0];
                                string thumb = (first.links != null && first.links.Length > 0) ? first.links[0].href : null;
                                records[id] = new NasaMediaRecord
                                {
                                    Artifact = id.ToString(), NasaId = data.nasa_id,
                                    Title = string.IsNullOrEmpty(data.title) ? item.Name : data.title,
                                    Description = string.IsNullOrEmpty(data.description) ? item.History : data.description,
                                    DateCreated = data.date_created,
                                    ThumbnailUrl = (thumb != null && thumb.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) ? thumb : string.Empty,
                                    CatalogUrl = "https://images.nasa.gov/search?q=" + UnityWebRequest.EscapeURL(item.NasaQuery),
                                    LiveFromNasa = true
                                };
                                Persist();
                                RecordUpdated?.Invoke(id, records[id]);
                            }
                        }
                    }
                    catch (Exception ex) { Debug.LogWarning("[NASA] JSON inválido: " + ex.Message); }
                }
                else Debug.LogWarning("[NASA] Consulta indisponível; usando catálogo offline: " + request.error);
            }
            inFlight.Remove(id);
        }

        public IEnumerator LoadThumbnail(NasaMediaRecord record, Action<Texture2D> callback)
        {
            if (record == null || string.IsNullOrEmpty(record.ThumbnailUrl)) yield break;
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(record.ThumbnailUrl, true))
            {
                request.timeout = 12;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                    callback?.Invoke(DownloadHandlerTexture.GetContent(request));
            }
        }

        private void Persist()
        {
            try
            {
                NasaCache cache = new NasaCache();
                foreach (NasaMediaRecord r in records.Values) cache.entries.Add(r);
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                File.WriteAllText(CachePath, JsonUtility.ToJson(cache, true));
            }
            catch (Exception ex) { Debug.LogWarning("[NASA] Não foi possível guardar o cache: " + ex.Message); }
        }
    }
}
