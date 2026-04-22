using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace FormulaSim.Backend
{
    /// <summary>
    /// REST client with:
    ///  - Exponential-backoff retry (up to MAX_RETRIES attempts)
    ///  - PlayerPrefs offline cache for GET endpoints
    ///  - Graceful degradation: on network failure, serves cached data if available
    /// </summary>
    public class APIClient : MonoBehaviour
    {
        const string BASE        = "https://api.formulasimpro.com/v1";
        const int    TIMEOUT     = 10;
        const int    MAX_RETRIES = 3;
        const string CACHE_PREFIX = "api_cache_";
        const int    CACHE_TTL_SECS = 3600;   // 1 hour

        public static APIClient Instance { get; private set; }

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Leaderboard ───────────────────────────────────────────────────────

        public void GetLeaderboard(string circuitId, Action<LeaderboardResponse> onDone, Action<string> onError = null)
            => StartCoroutine(_GetWithCache($"/leaderboards/{circuitId}", onDone, onError));

        public void SubmitLap(LapSubmission lap, Action<ApiResult> onDone = null, Action<string> onError = null)
            => StartCoroutine(_PostWithRetry("/laps", lap, onDone, onError));

        // ── Race Results ──────────────────────────────────────────────────────

        public void SubmitRaceResult(RaceResultPayload result, Action<ApiResult> onDone = null, Action<string> onError = null)
            => StartCoroutine(_PostWithRetry("/races", result, onDone, onError));

        public void GetStandings(string season, Action<StandingsResponse> onDone, Action<string> onError = null)
            => StartCoroutine(_GetWithCache($"/standings/{season}", onDone, onError));

        // ── Player Profile ────────────────────────────────────────────────────

        public void GetProfile(string playerId, Action<PlayerProfile> onDone, Action<string> onError = null)
            => StartCoroutine(_GetWithCache($"/players/{playerId}", onDone, onError));

        public void UpdateProfile(PlayerProfile profile, Action<ApiResult> onDone = null, Action<string> onError = null)
            => StartCoroutine(_PostWithRetry("/players", profile, onDone, onError));

        // ── GET with cache ────────────────────────────────────────────────────

        IEnumerator _GetWithCache<T>(string path, Action<T> onDone, Action<string> onError)
        {
            string cacheKey = CACHE_PREFIX + path.Replace("/", "_");
            bool   success  = false;

            for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
            {
                if (attempt > 0)
                    yield return new WaitForSeconds(Mathf.Pow(2f, attempt - 1));  // 1s, 2s, 4s

                using var req = UnityWebRequest.Get(BASE + path);
                req.timeout   = TIMEOUT;
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string json = req.downloadHandler.text;
                    _WriteCache(cacheKey, json);
                    try
                    {
                        onDone?.Invoke(JsonConvert.DeserializeObject<T>(json));
                        success = true;
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Parse error: {e.Message}");
                        success = true;  // don't retry parse errors
                    }
                    break;
                }

                Debug.LogWarning($"[API] {path} attempt {attempt + 1} failed: {req.error}");
            }

            if (!success)
            {
                // Serve cached data if available
                string cached = _ReadCache(cacheKey);
                if (!string.IsNullOrEmpty(cached))
                {
                    Debug.Log($"[API] Serving cached data for {path}");
                    try { onDone?.Invoke(JsonConvert.DeserializeObject<T>(cached)); }
                    catch { onError?.Invoke("Offline — cached data unavailable"); }
                }
                else
                {
                    onError?.Invoke("Network unavailable and no cached data");
                }
            }
        }

        // ── POST with retry ───────────────────────────────────────────────────

        IEnumerator _PostWithRetry<TReq, TRes>(string path, TReq body, Action<TRes> onDone, Action<string> onError)
        {
            string json    = JsonConvert.SerializeObject(body);
            bool   success = false;

            for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
            {
                if (attempt > 0)
                    yield return new WaitForSeconds(Mathf.Pow(2f, attempt - 1));

                using var req = new UnityWebRequest(BASE + path, "POST");
                req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout         = TIMEOUT;
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        onDone?.Invoke(JsonConvert.DeserializeObject<TRes>(req.downloadHandler.text));
                        success = true;
                    }
                    catch (Exception e) { onError?.Invoke($"Parse error: {e.Message}"); success = true; }
                    break;
                }

                Debug.LogWarning($"[API] POST {path} attempt {attempt + 1} failed: {req.error}");
            }

            if (!success)
                onError?.Invoke($"POST {path} failed after {MAX_RETRIES} attempts");
        }

        // ── Cache helpers (PlayerPrefs, TTL-stamped) ──────────────────────────

        static void _WriteCache(string key, string json)
        {
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.SetInt(key + "_ts", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            PlayerPrefs.Save();
        }

        static string _ReadCache(string key)
        {
            int ts = PlayerPrefs.GetInt(key + "_ts", 0);
            int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now - ts > CACHE_TTL_SECS) return null;  // expired
            return PlayerPrefs.GetString(key, null);
        }

        // ── DTOs ──────────────────────────────────────────────────────────────

        [Serializable] public class LapSubmission      { public string circuitId; public string driverId; public float lapTime; public string compound; }
        [Serializable] public class RaceResultPayload  { public string circuitId; public string season; public object[] results; }
        [Serializable] public class LeaderboardResponse{ public LapEntry[] entries; }
        [Serializable] public class LapEntry           { public string driverName; public float lapTime; public int rank; }
        [Serializable] public class StandingsResponse  { public object[] drivers; public object[] constructors; }
        [Serializable] public class PlayerProfile      { public string id; public string name; public int totalWins; public float rating; }
        [Serializable] public class ApiResult          { public bool success; public string message; }
    }
}
