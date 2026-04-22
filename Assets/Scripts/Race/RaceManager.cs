using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FormulaSim.Cars;
using FormulaSim.Audio;
using FormulaSim.Championship;

namespace FormulaSim.Race
{
    /// <summary>
    /// Central race orchestrator. Manages:
    ///  - Grid formation and rolling start
    ///  - Real-time lap leader tracking
    ///  - Gap to car ahead/behind for all cars
    ///  - Race order by laps completed + distance progress
    ///  - Flag deployment (yellow zones, SC, VSC)
    ///  - Results collection and passing to SeasonManager
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [Header("Race Config")]
        [SerializeField] int   totalLaps    = 58;
        [SerializeField] float gridSpacing  = 15f;    // metres between grid slots
        [SerializeField] Transform gridOrigin;

        [Header("References")]
        [SerializeField] AudioManager     audio;
        [SerializeField] FlagSystem       flagSystem;

        // ── All cars in the race ──────────────────────────────────────────────
        public List<CarEntry> Cars { get; } = new();

        public class CarEntry
        {
            public F1CarController Controller;
            public string          DriverId;
            public string          TeamId;
            public bool            IsPlayer;

            // Race progress
            public int   LapsComplete;
            public float DistanceOnTrack;   // 0..1 fraction of one lap
            public float TotalProgress;     // LapsComplete + DistanceOnTrack
            public int   RacePosition;
            public float GapToAhead;        // seconds
            public float GapToBehind;       // seconds (for car behind)
            public bool  HasRetired;
            public bool  InPit;
            public int   PitStopCount;

            // Timing
            public float BestLapTime = float.MaxValue;
            public float LastLapTime;
            public float FastestSectorTimes = float.MaxValue;
        }

        // ── Race state ────────────────────────────────────────────────────────
        public bool   RaceStarted   { get; private set; }
        public bool   RaceFinished  { get; private set; }
        public float  RaceElapsed   { get; private set; }
        public int    TotalLaps_    => totalLaps;
        public CarEntry Leader      => Cars.Count > 0 ? Cars[0] : null;

        public event Action<CarEntry>          OnOvertake;
        public event Action<CarEntry, int>     OnLapCompleted;  // entry, lap
        public event Action<List<CarEntry>>    OnRaceFinished;

        // ── Waypoint progress tracking ────────────────────────────────────────
        // Each car's position is projected onto the track spine to compute TotalProgress.
        // TrackSpline is a list of world-space waypoints normalized 0..totalLength.
        Tracks.CircuitData circuit;
        float[]             waypointDistances;    // cumulative distance per waypoint
        float               totalTrackLength;

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Init ──────────────────────────────────────────────────────────────

        public void Init(Tracks.CircuitData circuitData)
        {
            circuit = circuitData;
            _BuildWaypointDistances();
            PenaltySystem.Instance?.Init(circuitData);
        }

        public void RegisterCar(F1CarController ctrl, string driverId, string teamId, bool isPlayer)
        {
            Cars.Add(new CarEntry
            {
                Controller = ctrl,
                DriverId   = driverId,
                TeamId     = teamId,
                IsPlayer   = isPlayer,
            });
            PenaltySystem.Instance?.RegisterCar(ctrl);
        }

        // ── Formation / Grid ──────────────────────────────────────────────────

        public void PlaceOnGrid()
        {
            // Cars are assumed pre-sorted by qualifying time (index 0 = pole)
            for (int i = 0; i < Cars.Count; i++)
            {
                int   row      = i / 2;
                bool  leftSide = (i % 2 == 0);
                float offset   = leftSide ? -3.5f : 3.5f;
                Vector3 pos    = gridOrigin.position
                               + gridOrigin.forward * -(row * gridSpacing)
                               + gridOrigin.right   * offset;
                Cars[i].Controller.transform.SetPositionAndRotation(pos, gridOrigin.rotation);
            }
        }

        public IEnumerator StartSequence()
        {
            // Formation lap (simplified): all cars crawl along track
            // Real formation lap handled by AI in follow-leader mode
            yield return new WaitForSeconds(2f);
            // Lights sequence handled by RaceStartSequence.cs
            // We just wait for the GO signal
            yield return new WaitUntil(() => RaceStarted);
        }

        public void StartRace()
        {
            RaceStarted  = true;
            RaceFinished = false;
            RaceElapsed  = 0f;
            foreach (var c in Cars) { c.LapsComplete = 0; c.TotalProgress = 0f; }

            // Start replay recording
            var carCtrls = new List<F1CarController>(Cars.ConvertAll(e => e.Controller));
            ReplaySystem.Instance?.StartRecording(carCtrls);
        }

        // ── Update ────────────────────────────────────────────────────────────

        void FixedUpdate()
        {
            if (!RaceStarted || RaceFinished) return;
            RaceElapsed += Time.fixedDeltaTime;
            _UpdateProgress();
            _SortByProgress();
            _UpdateGaps();
            _AssignPositions();
        }

        void _UpdateProgress()
        {
            foreach (var e in Cars)
            {
                if (e.HasRetired || e.InPit) continue;
                float dist = _ProjectOntoTrack(e.Controller.transform.position);
                e.DistanceOnTrack = dist;
                e.TotalProgress   = e.LapsComplete + dist;
            }
        }

        void _SortByProgress()
        {
            Cars.Sort((a, b) => b.TotalProgress.CompareTo(a.TotalProgress));
        }

        void _UpdateGaps()
        {
            // Gap in seconds = distance gap / average speed
            for (int i = 0; i < Cars.Count; i++)
            {
                if (i == 0) { Cars[0].GapToAhead = 0f; }
                else
                {
                    float progressGap = Cars[i - 1].TotalProgress - Cars[i].TotalProgress;
                    float distGap     = progressGap * totalTrackLength;
                    float avgSpeed    = Mathf.Max(1f, Cars[i].Controller.SpeedMs);
                    Cars[i].GapToAhead = distGap / avgSpeed;
                }
                if (i < Cars.Count - 1)
                    Cars[i].GapToBehind = Cars[i + 1].GapToAhead;
                else
                    Cars[i].GapToBehind = 0f;
            }
        }

        void _AssignPositions()
        {
            for (int i = 0; i < Cars.Count; i++)
            {
                int oldPos = Cars[i].RacePosition;
                Cars[i].RacePosition = i + 1;
                if (oldPos != 0 && oldPos > i + 1)
                    OnOvertake?.Invoke(Cars[i]);   // moved up a position
            }
        }

        // ── Lap complete notification ─────────────────────────────────────────

        public void NotifyLapComplete(F1CarController ctrl, float lapTime)
        {
            var entry = Cars.Find(e => e.Controller == ctrl);
            if (entry == null) return;

            entry.LapsComplete++;
            entry.LastLapTime = lapTime;
            if (lapTime < entry.BestLapTime) entry.BestLapTime = lapTime;

            OnLapCompleted?.Invoke(entry, entry.LapsComplete);

            // Check if this car finished the race
            if (entry.LapsComplete >= totalLaps && !RaceFinished)
            {
                if (entry.IsPlayer || entry.RacePosition == 1)
                    StartCoroutine(_FinishRace());
            }
        }

        IEnumerator _FinishRace()
        {
            yield return new WaitForSeconds(30f);
            RaceFinished = true;
            ReplaySystem.Instance?.StopRecording();

            // Build result
            var result = new RaceResult
            {
                circuitId         = circuit ? circuit.circuitId : "unknown",
                fastestLapDriver  = _FindFastestLap(),
            };
            foreach (var e in Cars)
            {
                result.finishOrder.Add(new FinishEntry
                {
                    driverId    = e.DriverId,
                    teamId      = e.TeamId,
                    position    = e.RacePosition,
                    finishedRace= !e.HasRetired,
                    lapTime     = e.BestLapTime,
                });
            }

            OnRaceFinished?.Invoke(Cars);
            Core.GameManager.Instance.TransitionTo(Core.GameState.Results);
        }

        string _FindFastestLap()
        {
            CarEntry fastest = null;
            foreach (var e in Cars)
                if (fastest == null || e.BestLapTime < fastest.BestLapTime)
                    fastest = e;
            return fastest?.DriverId ?? "";
        }

        // ── Track projection ──────────────────────────────────────────────────

        void _BuildWaypointDistances()
        {
            if (circuit == null || circuit.waypoints == null) return;
            var wps = circuit.waypoints;
            waypointDistances = new float[wps.Length];
            float total = 0f;
            waypointDistances[0] = 0f;
            for (int i = 1; i < wps.Length; i++)
            {
                total += Vector2.Distance(wps[i], wps[i - 1]);
                waypointDistances[i] = total;
            }
            totalTrackLength = total + Vector2.Distance(wps[wps.Length - 1], wps[0]);
        }

        /// <summary>Returns 0..1 fraction of lap progress for a world position.</summary>
        float _ProjectOntoTrack(Vector3 worldPos)
        {
            if (circuit == null || waypointDistances == null) return 0f;
            var wps  = circuit.waypoints;
            float bestDist = float.MaxValue;
            float bestT    = 0f;

            for (int i = 0; i < wps.Length; i++)
            {
                int   next = (i + 1) % wps.Length;
                Vector2 a  = wps[i];
                Vector2 b  = wps[next];
                Vector2 p  = new(worldPos.x, worldPos.y);
                Vector2 ab = b - a;
                float   t  = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                Vector2 closest = a + t * ab;
                float   d  = Vector2.Distance(p, closest);
                if (d < bestDist)
                {
                    bestDist = d;
                    float segDist = waypointDistances[i] + t * Vector2.Distance(a, b);
                    bestT    = segDist / totalTrackLength;
                }
            }
            return bestT;
        }

        // ── Public helpers for UI ─────────────────────────────────────────────

        public CarEntry GetPlayerEntry()    => Cars.Find(e => e.IsPlayer);
        public int      GetPlayerPosition() => GetPlayerEntry()?.RacePosition ?? 0;
        public float    GetGapToAhead()     => GetPlayerEntry()?.GapToAhead ?? 0f;
        public CarEntry GetCarAhead()
        {
            var p = GetPlayerEntry();
            if (p == null || p.RacePosition <= 1) return null;
            return Cars.Find(e => e.RacePosition == p.RacePosition - 1);
        }

        /// <summary>
        /// Called by F1CarController when a car accumulates fatal damage.
        /// Marks the entry as retired so it is excluded from progress sorting.
        /// </summary>
        public void RetireCar(Cars.F1CarController ctrl)
        {
            var entry = Cars.Find(e => e.Controller == ctrl);
            if (entry == null || entry.HasRetired) return;
            entry.HasRetired = true;
            audio?.PlayCrowdEvent(Audio.CrowdEvent.Gasp);
            Debug.Log($"[RaceManager] {entry.DriverId} retired from position {entry.RacePosition}");
        }
    }
}
