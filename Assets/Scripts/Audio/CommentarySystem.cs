using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FormulaSim.Weather;

namespace FormulaSim.Audio
{
    public class CommentarySystem : MonoBehaviour
    {
        public static CommentarySystem Instance { get; private set; }

        [SerializeField] AudioManager audioManager;
        [SerializeField] bool         subtitlesEnabled = true;

        // ── Priority ──────────────────────────────────────────────────────────
        enum Priority { Filler=1, Info=2, Strategy=3, Excitement=4, Critical=5 }

        static readonly Dictionary<string, Priority> PRIORITIES = new()
        {
            ["WIN"]              = Priority.Critical,
            ["CRASH_HEAVY"]      = Priority.Critical,
            ["SAFETY_CAR"]       = Priority.Critical,
            ["CHAMPIONSHIP_FIGHT"]= Priority.Excitement,
            ["FASTEST_LAP"]      = Priority.Excitement,
            ["OVERTAKE"]         = Priority.Excitement,
            ["WEATHER_RAIN_START"]= Priority.Excitement,
            ["AQUAPLANING"]      = Priority.Excitement,
            ["PIT_FAST"]         = Priority.Excitement,
            ["WEATHER_DRYING"]   = Priority.Strategy,
            ["PIT_IN"]           = Priority.Strategy,
            ["TIRES_DEGRADING"]  = Priority.Strategy,
            ["LAP_COMPLETE"]     = Priority.Info,
            ["FILLER"]           = Priority.Filler,
        };

        static readonly Dictionary<string, float> COOLDOWNS = new()
        {
            ["LAP_COMPLETE"]  = 18f, ["OVERTAKE"] = 12f, ["FASTEST_LAP"] = 0f,
            ["AQUAPLANING"]   =  8f, ["CRASH_HEAVY"] = 0f, ["SAFETY_CAR"] = 0f,
            ["TIRES_DEGRADING"]= 45f,["CHAMPIONSHIP_FIGHT"] = 120f, ["FILLER"] = 35f,
            ["WIN"] = 0f, ["PIT_IN"] = 10f, ["WEATHER_RAIN_START"] = 0f,
        };

        // Context for line interpolation
        public string DriverName   = "the driver";
        public string Opponent     = "the rival";
        public string CircuitName  = "the circuit";
        public string CornerName   = "turn one";
        public string TeamName     = "the team";
        public int    Lap          = 1;
        public int    TotalLaps    = 50;
        public int    Position     = 1;
        public string GapStr       = "0.0";
        public string Compound     = "medium";
        public string LapTimeStr   = "1:30.000";
        public string StopTimeStr  = "2.4";

        struct QueuedLine { public string text; public Priority priority; }

        readonly List<QueuedLine>       queue       = new();
        readonly Dictionary<string, float> cooldowns = new();
        bool  playing;
        float playTimer;
        float fillerTimer;
        Priority currentPriority;

        UI.HUDController hud;

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            hud          = FindObjectOfType<UI.HUDController>();
            fillerTimer  = UnityEngine.Random.Range(20f, 40f);
        }

        void Update()
        {
            // Tick cooldowns
            var keys = new List<string>(cooldowns.Keys);
            foreach (var k in keys)
            {
                cooldowns[k] -= Time.deltaTime;
                if (cooldowns[k] <= 0) cooldowns.Remove(k);
            }

            // Playback timer
            if (playing)
            {
                playTimer -= Time.deltaTime;
                if (playTimer <= 0)
                {
                    playing          = false;
                    currentPriority  = 0;
                    audioManager?.DuckForCommentary(false);
                    _PlayNext();
                }
                return;
            }

            // Filler
            fillerTimer -= Time.deltaTime;
            if (fillerTimer <= 0)
            {
                fillerTimer = UnityEngine.Random.Range(30f, 60f);
                Trigger("FILLER");
            }
        }

        public void Trigger(string eventKey, Action<string> lineOverride = null)
        {
            if (cooldowns.ContainsKey(eventKey)) return;
            if (!PRIORITIES.TryGetValue(eventKey, out var pri)) pri = Priority.Info;

            cooldowns[eventKey] = COOLDOWNS.TryGetValue(eventKey, out float cd) ? cd : 20f;

            // Pull line from library
            var pool = CommentaryLines.Get(eventKey);
            if (pool == null || pool.Length == 0) return;
            string raw  = pool[UnityEngine.Random.Range(0, pool.Length)];
            string line = _Interpolate(raw);

            _Enqueue(line, pri);
        }

        void _Enqueue(string line, Priority pri)
        {
            // Interrupt if strictly higher priority
            if (playing && pri > currentPriority)
            {
                playing         = false;
                playTimer       = 0f;
                currentPriority = 0;
                audioManager?.DuckForCommentary(false);
                queue.Clear();
            }
            else if (playing && (int)pri < (int)Priority.Excitement) return;

            queue.Add(new QueuedLine { text = line, priority = pri });
            queue.Sort((a, b) => b.priority.CompareTo(a.priority));
            if (!playing) _PlayNext();
        }

        void _PlayNext()
        {
            if (queue.Count == 0) return;
            var entry    = queue[0]; queue.RemoveAt(0);
            playing      = true;
            currentPriority = entry.priority;
            playTimer    = Mathf.Max(2.5f, entry.text.Length / 13f);

            audioManager?.DuckForCommentary(true);
            hud?.ShowSubtitle(entry.text, playTimer);
        }

        string _Interpolate(string raw) => System.Text.RegularExpressions.Regex.Replace(raw,
            @"\{(\w+)\}", m => m.Groups[1].Value switch
            {
                "driver"    => DriverName,
                "opponent"  => Opponent,
                "circuit"   => CircuitName,
                "corner"    => CornerName,
                "team"      => TeamName,
                "lap"       => Lap.ToString(),
                "total"     => TotalLaps.ToString(),
                "pos"       => Position.ToString(),
                "gap"       => GapStr,
                "compound"  => Compound,
                "lap_time"  => LapTimeStr,
                "stop_time" => StopTimeStr,
                _           => m.Value,
            });
    }
}
