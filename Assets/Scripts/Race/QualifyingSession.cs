using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FormulaSim.Cars;

namespace FormulaSim.Race
{
    /// <summary>
    /// F1-style qualifying: Q1 (20 min, eliminates P16-20), Q2 (15 min, eliminates P11-15), Q3 (12 min, top 10).
    /// Player and AI set flying laps; best time determines grid position.
    /// </summary>
    public class QualifyingSession : MonoBehaviour
    {
        public enum QualStage { Q1, Q2, Q3 }

        [SerializeField] float[] stageDurationMins = { 20f, 15f, 12f };
        [SerializeField] int[]   eliminationCounts = {  5,   5,   0  };   // cars out per stage

        public QualStage CurrentStage    { get; private set; } = QualStage.Q1;
        public int       CurrentStageInt => (int)CurrentStage + 1;   // 1/2/3
        public float     TimeRemaining   { get; private set; }
        public bool      SessionActive   { get; private set; }
        public bool      IsRunning       => SessionActive;
        public List<QualEntry> Results   { get; } = new();

        // ── HUD-facing DTO ────────────────────────────────────────────────────
        public class DriverTiming
        {
            public string driverName;
            public float  bestLap;
            public bool   isPlayer;
        }

        public class QualEntry
        {
            public string  DriverId;
            public string  TeamId;
            public bool    IsPlayer;
            public float   BestLap = float.MaxValue;
            public bool    Eliminated;
            public int     GridPosition;
            public F1CarController Controller;
        }

        public event System.Action<QualStage>              OnStageBegin;
        public event System.Action<List<QualEntry>>        OnStageEnd;
        public event System.Action<List<QualEntry>>        OnQualifyingComplete;
        // QualifyingHUD-facing events
        public event System.Action<int>                    OnStageChanged;
        public event System.Action<List<DriverTiming>>     OnTimingUpdate;
        public event System.Action                         OnSessionEnd;

        public void RegisterCar(F1CarController ctrl, string driverId, string teamId, bool isPlayer)
        {
            Results.Add(new QualEntry
            {
                Controller = ctrl,
                DriverId   = driverId,
                TeamId     = teamId,
                IsPlayer   = isPlayer,
            });
        }

        public void StartQualifying() => StartCoroutine(_RunQualifying());

        IEnumerator _RunQualifying()
        {
            foreach (QualStage stage in new[] { QualStage.Q1, QualStage.Q2, QualStage.Q3 })
            {
                CurrentStage   = stage;
                TimeRemaining  = stageDurationMins[(int)stage] * 60f;
                SessionActive  = true;
                OnStageBegin?.Invoke(stage);
                OnStageChanged?.Invoke(CurrentStageInt);

                // Hook all cars to report laps during this stage
                foreach (var e in Results.Where(x => !x.Eliminated))
                    e.Controller.OnLapComplete += t => _OnLapSet(e, t);

                while (TimeRemaining > 0f)
                {
                    TimeRemaining -= Time.deltaTime;
                    OnTimingUpdate?.Invoke(_BuildTimings());
                    yield return null;
                }

                SessionActive = false;
                _Eliminate(stage);
                OnStageEnd?.Invoke(Results);

                // Brief break between stages
                if (stage < QualStage.Q3)
                    yield return new WaitForSeconds(8f);
            }

            // Assign final grid positions
            var active = Results.Where(e => !e.Eliminated)
                                .OrderBy(e => e.BestLap).ToList();
            var eliminated = Results.Where(e => e.Eliminated)
                                    .OrderBy(e => e.BestLap).ToList();
            var final = active.Concat(eliminated).ToList();
            for (int i = 0; i < final.Count; i++)
                final[i].GridPosition = i + 1;

            OnQualifyingComplete?.Invoke(final);
            OnSessionEnd?.Invoke();
        }

        List<DriverTiming> _BuildTimings()
        {
            return Results
                .Where(e => !e.Eliminated)
                .OrderBy(e => e.BestLap == float.MaxValue ? float.MaxValue : e.BestLap)
                .Select(e => new DriverTiming
                {
                    driverName = e.DriverId,
                    bestLap    = e.BestLap == float.MaxValue ? 0f : e.BestLap,
                    isPlayer   = e.IsPlayer,
                })
                .ToList();
        }

        void _Eliminate(QualStage stage)
        {
            int count = eliminationCounts[(int)stage];
            if (count == 0) return;

            var ranked = Results
                .Where(e => !e.Eliminated)
                .OrderByDescending(e => e.BestLap == float.MaxValue ? float.MaxValue : e.BestLap)
                .Take(count)
                .ToList();
            foreach (var e in ranked) e.Eliminated = true;
        }

        void _OnLapSet(QualEntry entry, float lapTime)
        {
            if (!SessionActive) return;
            if (lapTime < entry.BestLap) entry.BestLap = lapTime;
        }
    }
}
