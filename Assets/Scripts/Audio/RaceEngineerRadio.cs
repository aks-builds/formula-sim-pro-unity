using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FormulaSim.Cars;
using FormulaSim.Race;
using FormulaSim.Weather;

namespace FormulaSim.Audio
{
    /// <summary>
    /// Context-aware race engineer. Delivers strategic radio calls to the player
    /// based on live telemetry. Separate channel from CommentarySystem — engineer
    /// is functional and strategic; commentary is entertainment.
    ///
    /// Wire up by placing this on the same GameObject as RaceManager.
    /// All lines are delivered as subtitles + optional audio clip.
    /// </summary>
    public class RaceEngineerRadio : MonoBehaviour
    {
        public static RaceEngineerRadio Instance { get; private set; }

        [Header("References")]
        [SerializeField] UI.HUDController hud;

        [Header("Timing")]
        [SerializeField] float minMessageInterval = 8f;   // don't spam the player
        [SerializeField] float pollInterval        = 3f;   // how often to check conditions

        // ── Priority queue ────────────────────────────────────────────────────
        class RadioMessage
        {
            public int    priority;   // 1 = info, 2 = strategy, 3 = urgent, 4 = critical
            public string text;
            public float  expiryTime; // discard if not played by this time
        }

        readonly Queue<RadioMessage> _queue = new();
        float _lastMessageTime;
        float _pollTimer;

        // Cooldowns per event type
        readonly Dictionary<string, float> _cooldowns = new();

        // ── Cached references ─────────────────────────────────────────────────
        F1CarController   _player;
        RaceManager       _rm;
        WeatherSystem     _weather;

        // ── Message templates ─────────────────────────────────────────────────

        static readonly Dictionary<string, string[]> _Lines = new()
        {
            ["drs_available"]       = new[] { "DRS available. Target P{pos_ahead}.", "DRS enabled — go for it.", "Use DRS, you're in range." },
            ["gap_closing"]         = new[] { "Gap to P{pos_ahead} is {gap}. DRS range next lap.", "You're closing on {pos_ahead} — {gap} gap.", "Keep pushing, gap is {gap}." },
            ["gap_opening"]         = new[] { "Gap to P{pos_behind} behind is {gap_behind}. Comfortable.", "Good gap — {gap_behind} to the car behind." },
            ["tire_deg_mild"]       = new[] { "Tyres are dropping off slightly. Box in {pit_laps} laps.", "Front left tyre showing wear. Consider pitting." },
            ["tire_deg_critical"]   = new[] { "Box this lap, tyres are critical!", "Box now! Tyres are gone.", "Immediate box, you're losing grip!" },
            ["tire_puncture"]       = new[] { "Puncture confirmed! Box this lap!", "You have a puncture. Box now!" },
            ["weather_rain"]        = new[] { "Rain expected in {rain_laps} laps. Watch the strategy.", "It's starting to rain. Prepare to box for Inters.", "Heavy rain incoming — box when safe." },
            ["weather_dry"]         = new[] { "Track is drying. We're watching the tyre call.", "Drying conditions — Slicks will be quicker soon." },
            ["safety_car"]          = new[] { "Safety car deployed. Box this lap for a free stop!", "Safety car — pit now for free tyres!", "VSC deployed. Stay out or box — your call." },
            ["safety_car_out"]      = new[] { "Safety car is in this lap. Get ready for the restart.", "Green flag next lap. Push hard on the out-lap." },
            ["fastest_lap"]         = new[] { "Purple sector! That's the fastest lap.", "Fastest lap of the race — great work!", "P-P-Purple! We've got the fastest lap." },
            ["position_gained"]     = new[] { "P{pos}, good move. Keep it up.", "You're up to P{pos}! Nice.", "Brilliant — P{pos}." },
            ["position_lost"]       = new[] { "We've dropped to P{pos}. Recover.", "P{pos} now. Stay calm, focus.", "That's P{pos}. We'll get it back." },
            ["low_fuel"]            = new[] { "Fuel is critical. Lift and coast through T1.", "Watch the fuel. Manage the final sector.", "We're short on fuel — back off 5%." },
            ["push_now"]            = new[] { "This is the lap — push!", "We need a fast lap. Everything you've got.", "Purple or nothing this lap. Go!" },
            ["sector_gap"]          = new[] { "Sector 2, you're down {sector_delta}. Nail the apex.", "Gaining in sector 3. Press through Sector 1.", },
            ["race_start"]          = new[] { "Good start! P{pos} into Turn 1.", "Off the line clean. We're P{pos}.", "Smooth start. P{pos}, let's race." },
            ["final_lap"]           = new[] { "Final lap. P{pos}. Bring it home.", "Last lap — stay out of trouble.", "Chequered next lap. P{pos}. Let's go." },
            ["rival_close"]         = new[] { "Your rival is right behind. Defend!", "Watch out — {rival} is looking." },
            ["rival_pitstop"]       = new[] { "{rival} has pitted. Net gain for you.", "Your rival is in the pits. Hold the pace." },
            ["front_wing_damage"]   = new[] { "Front wing damage. We'll assess — may need to box.", "Damaged front wing. Understeer will increase.", "Front wing is compromised. Box if it gets worse." },
            ["drive_through"]       = new[] { "Drive-through penalty. Serve it when you can.", "Stewards gave us a drive-through. Take it through the pits." },
            ["penalty_5s"]         = new[] { "Five-second penalty noted. We'll add it at the stop.", "Five seconds penalty. You'll serve it at the pit stop." },
            ["track_limits_warn"]   = new[] { "Track limits warning. Back inside the white lines.", "Keep within the lines. {warn_count} warnings.", },
            ["lap_record"]          = new[] { "That's a lap record! Incredible.", "Lap record! You're flying today." },
        };

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            _rm      = RaceManager.Instance;
            _weather = FindObjectOfType<WeatherSystem>();
            _player  = FindObjectOfType<F1CarController>();

            // Subscribe to event systems
            if (PenaltySystem.Instance)
                PenaltySystem.Instance.OnPenaltyIssued += _OnPenaltyIssued;

            if (RivalSystem.Instance)
                RivalSystem.Instance.OnRivalMessage += line => Transmit(line, 3);

            StartCoroutine(_PollCoroutine());
            StartCoroutine(_DequeueCoroutine());
        }

        void OnDestroy()
        {
            if (PenaltySystem.Instance)
                PenaltySystem.Instance.OnPenaltyIssued -= _OnPenaltyIssued;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Enqueue a raw message with priority.</summary>
        public void Transmit(string text, int priority = 2)
        {
            _queue.Enqueue(new RadioMessage
            {
                priority   = priority,
                text       = text,
                expiryTime = Time.time + 30f,
            });
        }

        /// <summary>Enqueue a line from the library with token substitution.</summary>
        public void TransmitEvent(string eventKey, int priority = 2)
        {
            if (_IsOnCooldown(eventKey)) return;
            if (!_Lines.TryGetValue(eventKey, out var lines)) return;

            string line = lines[Random.Range(0, lines.Length)];
            line = _Substitute(line);
            _SetCooldown(eventKey, _CooldownFor(eventKey));
            Transmit(line, priority);
        }

        // ── Context polling ───────────────────────────────────────────────────

        IEnumerator _PollCoroutine()
        {
            yield return new WaitForSeconds(3f); // let race start settle

            while (true)
            {
                yield return new WaitForSeconds(pollInterval);
                if (_player == null || _rm == null) continue;
                if (!_rm.RaceStarted || _rm.RaceFinished) continue;

                var pEntry = _rm.GetPlayerEntry();
                if (pEntry == null) continue;

                _PollGaps(pEntry);
                _PollTires(pEntry);
                _PollWeather(pEntry);
                _PollFuel(pEntry);
                _PollLapSpecial(pEntry);
            }
        }

        void _PollGaps(RaceManager.CarEntry entry)
        {
            float gap = entry.GapToAhead;
            if (gap > 0f && gap < 0.8f && _player.DrsActive)
                TransmitEvent("drs_available", 2);
            else if (gap > 0f && gap < 1.5f)
                TransmitEvent("gap_closing", 1);
            else if (entry.GapToBehind > 3f)
                TransmitEvent("gap_opening", 1);
        }

        void _PollTires(RaceManager.CarEntry entry)
        {
            var tires   = _player.Tires;
            var advice  = TireManager.GetPitAdvice(tires, _weather.CurrentState,
                          _rm.TotalLaps_ - entry.LapsComplete);

            if (advice == null) return;
            if (advice.Urgency == "CRITICAL")  TransmitEvent("tire_deg_critical", 4);
            else if (advice.Urgency == "HIGH") TransmitEvent("tire_deg_mild",     2);

            // Puncture check from DamageModel
            var dmg = _player.GetComponent<DamageModel>();
            if (dmg != null && dmg.HasPuncture) TransmitEvent("tire_puncture", 4);
        }

        void _PollWeather(RaceManager.CarEntry entry)
        {
            if (_weather.CurrentState == WeatherState.Drizzle ||
                _weather.CurrentState == WeatherState.Light)
                TransmitEvent("weather_rain", 3);
            else if (_weather.CurrentState == WeatherState.Dry)
                TransmitEvent("weather_dry", 2);
        }

        void _PollFuel(RaceManager.CarEntry entry)
        {
            float lapsRemaining = _rm.TotalLaps_ - entry.LapsComplete;
            if (lapsRemaining <= 5f) TransmitEvent("low_fuel", 3);
            if (entry.LapsComplete == _rm.TotalLaps_ - 1) TransmitEvent("final_lap", 3);
        }

        void _PollLapSpecial(RaceManager.CarEntry entry)
        {
            // Rival proximity
            if (RivalSystem.Instance != null && entry.GapToBehind < 0.5f)
                RivalSystem.Instance.OnRivalClose();
        }

        // ── Dequeue and display ───────────────────────────────────────────────

        IEnumerator _DequeueCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                if (_queue.Count == 0) continue;
                if (Time.time - _lastMessageTime < minMessageInterval) continue;

                // Find highest-priority non-expired message
                RadioMessage best = null;
                var temp = new List<RadioMessage>(_queue);
                _queue.Clear();

                foreach (var m in temp)
                {
                    if (Time.time > m.expiryTime) continue;
                    if (best == null || m.priority > best.priority) best = m;
                    else _queue.Enqueue(m);
                }

                if (best != null)
                {
                    _lastMessageTime = Time.time;
                    hud?.ShowSubtitle($"Engineer: {best.text}", 4f);
                    Debug.Log($"[Engineer] {best.text}");
                }
            }
        }

        // ── Penalty events ────────────────────────────────────────────────────

        void _OnPenaltyIssued(Cars.F1CarController ctrl, PenaltySystem.PenaltyRecord pen)
        {
            if (!ctrl.GetComponent<Cars.F1CarController>()?.CompareTag("Player") ?? true)
            {
                // Not the player car — only care if involves our car
                var entry = _rm?.GetPlayerEntry();
                if (entry?.Controller != ctrl) return;
            }

            string key = pen.type switch
            {
                PenaltySystem.PenaltyType.TrackLimitsWarning  => "track_limits_warn",
                PenaltySystem.PenaltyType.TrackLimitsPenalty  => "penalty_5s",
                PenaltySystem.PenaltyType.CollisionPenalty    => "penalty_5s",
                PenaltySystem.PenaltyType.PitSpeeding         => "penalty_5s",
                PenaltySystem.PenaltyType.DriveThrough        => "drive_through",
                _                                             => null,
            };
            if (key != null) TransmitEvent(key, 4);
        }

        // ── Token substitution ────────────────────────────────────────────────

        string _Substitute(string line)
        {
            var entry = _rm?.GetPlayerEntry();
            if (entry == null) return line;

            int    pos        = entry.RacePosition;
            int    posAhead   = Mathf.Max(1, pos - 1);
            float  gap        = entry.GapToAhead;
            float  gapBehind  = entry.GapToBehind;
            string rival      = RivalSystem.Instance?.RivalDriverName ?? "your rival";
            string warnCount  = PenaltySystem.Instance?.GetTrackLimitWarnings(entry.Controller).ToString() ?? "?";

            return line
                .Replace("{pos}",         pos.ToString())
                .Replace("{pos_ahead}",   $"P{posAhead}")
                .Replace("{gap}",         $"{gap:F1}s")
                .Replace("{gap_behind}",  $"{gapBehind:F1}s")
                .Replace("{rival}",       rival)
                .Replace("{warn_count}",  warnCount)
                .Replace("{pit_laps}",    "2")
                .Replace("{rain_laps}",   "3")
                .Replace("{sector_delta}","0.2s");
        }

        // ── Cooldown helpers ──────────────────────────────────────────────────

        bool  _IsOnCooldown(string key) =>
            _cooldowns.TryGetValue(key, out float t) && Time.time < t;

        void  _SetCooldown(string key, float seconds) =>
            _cooldowns[key] = Time.time + seconds;

        float _CooldownFor(string key) => key switch
        {
            "tire_deg_critical" => 20f,
            "tire_puncture"     => 15f,
            "safety_car"        => 60f,
            "fastest_lap"       => 120f,
            "low_fuel"          => 30f,
            "final_lap"         => 300f,
            "drs_available"     => 12f,
            _                   => 25f,
        };
    }
}
