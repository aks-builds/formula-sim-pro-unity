using System.Collections.Generic;
using UnityEngine;

namespace FormulaSim.Race
{
    /// <summary>
    /// Driver rivalry engine.
    /// Tracks head-to-head incidents and position battles, builds rivalry heat,
    /// and drives engineer radio updates and AI aggression modifiers.
    /// Persists across career sessions via PlayerPrefs.
    /// </summary>
    public class RivalSystem : MonoBehaviour
    {
        public static RivalSystem Instance { get; private set; }

        // ── Active rival ──────────────────────────────────────────────────────
        public string RivalDriverId   { get; private set; }
        public string RivalDriverName { get; private set; } = "Unknown";
        public int    RivalryHeat     { get; private set; }   // 0-100

        // ── Events ────────────────────────────────────────────────────────────
        public event System.Action<string> OnRivalMessage;      // radio line for engineer
        public event System.Action<string, int> OnHeatChanged;  // (rivalId, newHeat)

        // Rival AI aggression boost when heat is high
        public float RivalAggressionBoost => Mathf.Lerp(0f, 0.25f, RivalryHeat / 100f);

        // ── Battle tracker ────────────────────────────────────────────────────
        readonly Dictionary<string, int> battleLapCount = new();   // driverId → laps battling
        float _heatDecayTimer;

        const string PREF_RIVAL_ID   = "rival_id";
        const string PREF_RIVAL_NAME = "rival_name";
        const string PREF_HEAT       = "rival_heat";

        // ── Radio lines ───────────────────────────────────────────────────────
        static readonly string[] _CloseLines = {
            "Your rival {rival} is right behind you — defend!",
            "Watch out, {rival} is looking for a gap.",
            "{rival} on your tail. Don't let them through.",
        };
        static readonly string[] _OvertakenLines = {
            "Your rival {rival} just passed you. We need that position back!",
            "{rival} is ahead. Stay within DRS range.",
        };
        static readonly string[] _OvertakeLines = {
            "You're past {rival}! Great move.",
            "Brilliant. {rival} is behind — gap is growing.",
        };
        static readonly string[] _IncidentLines = {
            "Stewards are looking at the incident with {rival}.",
            "That was aggressive from {rival} — we'll note it.",
        };
        static readonly string[] _NaturalRivalLines = {
            "{rival} is your closest championship rival. Keep the pressure on.",
            "Every point against {rival} matters today.",
        };

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
            _Load();
        }

        void Update()
        {
            // Rivalry heat decays over time (1 point per 2 seconds if no incidents)
            _heatDecayTimer += Time.deltaTime;
            if (_heatDecayTimer >= 2f && RivalryHeat > 0)
            {
                _heatDecayTimer = 0f;
                RivalryHeat     = Mathf.Max(0, RivalryHeat - 1);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetRival(string driverId, string driverName)
        {
            RivalDriverId   = driverId;
            RivalDriverName = driverName;
            _Save();
        }

        public void AutoDetectRival(List<RaceManager.CarEntry> standings, string playerDriverId)
        {
            // Rival = the driver nearest in championship standing to the player
            if (standings == null || standings.Count < 2) return;
            int playerPos = standings.FindIndex(e => e.IsPlayer);
            if (playerPos < 0) return;

            int rivalPos = playerPos > 0 ? playerPos - 1 : playerPos + 1;
            if (rivalPos >= standings.Count) rivalPos = playerPos - 1;
            if (rivalPos < 0) return;

            var rival = standings[rivalPos];
            if (rival.DriverId != RivalDriverId)
                SetRival(rival.DriverId, rival.DriverId);
        }

        /// <summary>Call when player and rival exchange positions.</summary>
        public void OnPlayerOvertakesRival()
        {
            _AddHeat(12);
            OnRivalMessage?.Invoke(_Pick(_OvertakeLines));
        }

        /// <summary>Call when rival overtakes player.</summary>
        public void OnRivalOvertakesPlayer()
        {
            _AddHeat(15);
            OnRivalMessage?.Invoke(_Pick(_OvertakenLines));
        }

        /// <summary>Call when rival is within DRS range of player.</summary>
        public void OnRivalClose()
        {
            if (RivalryHeat < 30) return;
            OnRivalMessage?.Invoke(_Pick(_CloseLines));
        }

        /// <summary>Call when a racing incident involves the rival.</summary>
        public void OnRacingIncident(string instigatorId, float severity)
        {
            if (instigatorId != RivalDriverId && instigatorId != "player") return;
            _AddHeat(Mathf.RoundToInt(severity * 0.8f));
            OnRivalMessage?.Invoke(_Pick(_IncidentLines));
        }

        /// <summary>Called by engineer radio periodically during championship battles.</summary>
        public void OnChampionshipUpdate()
        {
            if (!string.IsNullOrEmpty(RivalDriverId))
                OnRivalMessage?.Invoke(_Pick(_NaturalRivalLines));
        }

        /// <summary>Per-lap: track how long player and rival are battling (within 1s).</summary>
        public void UpdateBattle(string driverId, float gapSeconds)
        {
            if (driverId != RivalDriverId) return;

            if (gapSeconds < 1.0f)
            {
                battleLapCount.TryGetValue(driverId, out int count);
                battleLapCount[driverId] = count + 1;
                if (count + 1 >= 3) _AddHeat(5);
            }
            else
            {
                battleLapCount[driverId] = 0;
            }
        }

        // ── Internal ──────────────────────────────────────────────────────────

        void _AddHeat(int amount)
        {
            RivalryHeat = Mathf.Clamp(RivalryHeat + amount, 0, 100);
            OnHeatChanged?.Invoke(RivalDriverId, RivalryHeat);
            _Save();
        }

        string _Pick(string[] lines)
        {
            string line = lines[Random.Range(0, lines.Length)];
            return line.Replace("{rival}", RivalDriverName ?? RivalDriverId ?? "your rival");
        }

        void _Save()
        {
            PlayerPrefs.SetString(PREF_RIVAL_ID,   RivalDriverId   ?? "");
            PlayerPrefs.SetString(PREF_RIVAL_NAME, RivalDriverName ?? "");
            PlayerPrefs.SetInt   (PREF_HEAT,       RivalryHeat);
            PlayerPrefs.Save();
        }

        void _Load()
        {
            RivalDriverId   = PlayerPrefs.GetString(PREF_RIVAL_ID,   "");
            RivalDriverName = PlayerPrefs.GetString(PREF_RIVAL_NAME, "");
            RivalryHeat     = PlayerPrefs.GetInt   (PREF_HEAT,       0);
        }
    }
}
