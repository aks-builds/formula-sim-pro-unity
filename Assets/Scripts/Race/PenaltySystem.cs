using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FormulaSim.Cars;
using FormulaSim.Tracks;

namespace FormulaSim.Race
{
    /// <summary>
    /// FIA-style penalty system.
    ///  - Track limits: 3 violations → 5-second time penalty
    ///  - Causing collision: 5-second penalty
    ///  - Pit lane speeding: 5-second penalty
    ///  - Drive-through penalty for severe infringements
    ///
    /// Integrates with RaceManager for lap-time adjustments.
    /// UI surfaces penalties via PenaltyHUD.
    /// </summary>
    public class PenaltySystem : MonoBehaviour
    {
        public static PenaltySystem Instance { get; private set; }

        [Header("Track Limits")]
        [SerializeField] float trackHalfWidth    = 6.5f;    // metres from centerline to edge
        [SerializeField] float kerbAllowance     = 1.0f;    // metres of kerb allowed
        [SerializeField] int   warningsPerPenalty = 3;
        [SerializeField] float trackLimitCooldown = 2f;     // seconds between warnings

        [Header("Penalty Values")]
        [SerializeField] float timePenalty5s  = 5f;
        [SerializeField] float timePenalty10s = 10f;

        CircuitData circuit;

        class DriverPenaltyRecord
        {
            public F1CarController ctrl;
            public int    trackLimitWarnings;
            public float  accumulatedTimePenalty;
            public bool   hasDriveThrough;
            public float  lastWarningTime;
            public float  lastOffTrackTime;

            public List<PenaltyRecord> history = new();
        }

        public class PenaltyRecord
        {
            public PenaltyType type;
            public float       seconds;
            public int         lap;
            public string      description;
        }

        public enum PenaltyType { TrackLimitsWarning, TrackLimitsPenalty, CollisionPenalty, PitSpeeding, DriveThrough }

        readonly Dictionary<F1CarController, DriverPenaltyRecord> records = new();

        public event System.Action<F1CarController, PenaltyRecord> OnPenaltyIssued;

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Init(CircuitData circuitData) => circuit = circuitData;

        public void RegisterCar(F1CarController ctrl)
        {
            if (!records.ContainsKey(ctrl))
                records[ctrl] = new DriverPenaltyRecord { ctrl = ctrl };
        }

        void FixedUpdate()
        {
            if (circuit == null) return;
            foreach (var kvp in records)
                _CheckTrackLimits(kvp.Value);
        }

        // ── Track limits ──────────────────────────────────────────────────────

        void _CheckTrackLimits(DriverPenaltyRecord rec)
        {
            if (rec.ctrl == null) return;
            if (Time.time - rec.lastWarningTime < trackLimitCooldown) return;

            float lateralDist = _LateralDistanceFromTrack(rec.ctrl.transform.position);
            float limit       = trackHalfWidth + kerbAllowance;

            if (lateralDist > limit)
            {
                // Off track (beyond kerb)
                if (Time.time - rec.lastOffTrackTime > 0.5f)   // hysteresis
                {
                    rec.lastOffTrackTime = Time.time;
                    _IssueTrackLimitsWarning(rec);
                }
            }
        }

        void _IssueTrackLimitsWarning(DriverPenaltyRecord rec)
        {
            rec.trackLimitWarnings++;
            rec.lastWarningTime = Time.time;

            var entry = RaceManager.Instance?.Cars.Find(e => e.Controller == rec.ctrl);
            int lap   = entry?.LapsComplete + 1 ?? 0;

            if (rec.trackLimitWarnings >= warningsPerPenalty)
            {
                rec.trackLimitWarnings = 0;
                _IssuePenalty(rec, PenaltyType.TrackLimitsPenalty, timePenalty5s, lap,
                    "Track limits exceeded (3 violations)");
            }
            else
            {
                var warn = new PenaltyRecord
                {
                    type        = PenaltyType.TrackLimitsWarning,
                    seconds     = 0f,
                    lap         = lap,
                    description = $"Track limits warning ({rec.trackLimitWarnings}/{warningsPerPenalty})"
                };
                rec.history.Add(warn);
                OnPenaltyIssued?.Invoke(rec.ctrl, warn);
            }
        }

        // ── Collision penalty ─────────────────────────────────────────────────

        public void ReportCollision(F1CarController instigator, F1CarController victim, float severity)
        {
            if (!records.TryGetValue(instigator, out var rec)) return;
            var entry = RaceManager.Instance?.Cars.Find(e => e.Controller == instigator);

            if (severity > 15f)
            {
                float pen = severity > 35f ? timePenalty10s : timePenalty5s;
                _IssuePenalty(rec, PenaltyType.CollisionPenalty, pen, entry?.LapsComplete + 1 ?? 0,
                    $"Causing collision (severity {severity:F0} m/s)");
            }
        }

        // ── Pit lane speeding ─────────────────────────────────────────────────

        public void CheckPitLaneSpeeding(F1CarController ctrl, float speed)
        {
            const float PIT_LIMIT = 16.67f; // 60 km/h
            if (speed <= PIT_LIMIT + 2f) return;  // 2 m/s grace

            if (!records.TryGetValue(ctrl, out var rec)) return;
            var entry = RaceManager.Instance?.Cars.Find(e => e.Controller == ctrl);
            _IssuePenalty(rec, PenaltyType.PitSpeeding, timePenalty5s, entry?.LapsComplete + 1 ?? 0,
                $"Pit lane speeding ({speed * 3.6f:F0} km/h)");
        }

        // ── Drive-through ─────────────────────────────────────────────────────

        public void IssueDriveThrough(F1CarController ctrl, string reason)
        {
            if (!records.TryGetValue(ctrl, out var rec)) return;
            var entry = RaceManager.Instance?.Cars.Find(e => e.Controller == ctrl);
            rec.hasDriveThrough = true;
            var pen = new PenaltyRecord
            {
                type        = PenaltyType.DriveThrough,
                seconds     = 0f,
                lap         = entry?.LapsComplete + 1 ?? 0,
                description = reason
            };
            rec.history.Add(pen);
            OnPenaltyIssued?.Invoke(ctrl, pen);
        }

        // ── Query API ─────────────────────────────────────────────────────────

        public float GetTimePenalty(F1CarController ctrl)
            => records.TryGetValue(ctrl, out var r) ? r.accumulatedTimePenalty : 0f;

        public bool HasDriveThrough(F1CarController ctrl)
            => records.TryGetValue(ctrl, out var r) && r.hasDriveThrough;

        public void ClearDriveThrough(F1CarController ctrl)
        {
            if (records.TryGetValue(ctrl, out var r)) r.hasDriveThrough = false;
        }

        public int GetTrackLimitWarnings(F1CarController ctrl)
            => records.TryGetValue(ctrl, out var r) ? r.trackLimitWarnings : 0;

        public List<PenaltyRecord> GetHistory(F1CarController ctrl)
            => records.TryGetValue(ctrl, out var r) ? r.history : new List<PenaltyRecord>();

        // ── Internal ──────────────────────────────────────────────────────────

        void _IssuePenalty(DriverPenaltyRecord rec, PenaltyType type, float seconds, int lap, string desc)
        {
            rec.accumulatedTimePenalty += seconds;
            var pen = new PenaltyRecord { type = type, seconds = seconds, lap = lap, description = desc };
            rec.history.Add(pen);
            OnPenaltyIssued?.Invoke(rec.ctrl, pen);
            Debug.Log($"[Penalty] {rec.ctrl?.name ?? "?"}: {desc} (+{seconds}s)");
        }

        float _LateralDistanceFromTrack(Vector3 worldPos)
        {
            if (circuit == null || circuit.waypoints == null || circuit.waypoints.Length < 2)
                return 0f;

            var  pts     = circuit.waypoints;
            int  n       = pts.Length;
            float minDist = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % n];
                Vector2 p = new Vector2(worldPos.x, worldPos.z != 0 ? worldPos.z : worldPos.y);

                Vector2 ab  = b - a;
                float   t   = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                Vector2 proj = a + t * ab;
                float   d    = Vector2.Distance(p, proj);
                if (d < minDist) minDist = d;
            }
            return minDist;
        }
    }
}
