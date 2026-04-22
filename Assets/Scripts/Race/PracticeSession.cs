using System.Collections;
using UnityEngine;
using FormulaSim.Cars;

namespace FormulaSim.Race
{
    /// <summary>
    /// Free practice session: player drives laps to learn circuit, test setups, manage tires.
    /// No time limit when offline (Silverstone free practice).
    /// Full practice (FP1/FP2/FP3) available in career mode.
    /// </summary>
    public class PracticeSession : MonoBehaviour
    {
        public enum PracticeType { FreePractice, FP1, FP2, FP3 }

        [SerializeField] PracticeType type            = PracticeType.FreePractice;
        [SerializeField] float        durationMinutes = 60f;
        [SerializeField] bool         unlimitedTime   = false;   // offline mode

        public float TimeRemaining  { get; private set; }
        public bool  SessionActive  { get; private set; }
        public float PersonalBest   { get; private set; } = float.MaxValue;
        public int   LapsCompleted  { get; private set; }
        public float[] SectorBests  { get; } = { float.MaxValue, float.MaxValue, float.MaxValue };

        F1CarController playerCar;

        public event System.Action<float>  OnPersonalBest;      // new pb lap time
        public event System.Action<int, float> OnSectorPB;      // sector index, time
        public event System.Action         OnSessionEnd;

        void Start()
        {
            playerCar = FindObjectOfType<F1CarController>();
            if (playerCar)
                playerCar.OnLapComplete += _OnLap;
        }

        public void StartSession()
        {
            TimeRemaining = durationMinutes * 60f;
            SessionActive = true;
            LapsCompleted = 0;
            StartCoroutine(_SessionTimer());
        }

        IEnumerator _SessionTimer()
        {
            while (SessionActive)
            {
                if (!unlimitedTime)
                {
                    TimeRemaining -= Time.deltaTime;
                    if (TimeRemaining <= 0f)
                    {
                        EndSession();
                        yield break;
                    }
                }
                yield return null;
            }
        }

        void _OnLap(float lapTime)
        {
            LapsCompleted++;
            if (lapTime < PersonalBest)
            {
                PersonalBest = lapTime;
                OnPersonalBest?.Invoke(lapTime);
            }
        }

        public void EndSession()
        {
            SessionActive = false;
            OnSessionEnd?.Invoke();
            Core.GameManager.Instance.TransitionTo(Core.GameState.RaceWeekend);
        }

        void OnDestroy()
        {
            if (playerCar) playerCar.OnLapComplete -= _OnLap;
        }
    }
}
