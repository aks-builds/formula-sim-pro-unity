using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FormulaSim.Audio;
using FormulaSim.Weather;
using FormulaSim.Career;
using FormulaSim.Championship;
using FormulaSim.Network;
using FormulaSim.Notifications;

namespace FormulaSim.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("System References")]
        [SerializeField] AudioManager     audioManager;
        [SerializeField] WeatherSystem    weatherSystem;
        [SerializeField] VisualWeather    visualWeather;
        [SerializeField] CareerManager    careerManager;
        [SerializeField] SeasonManager    seasonManager;
        [SerializeField] ConnectivityManager connectivity;
        [SerializeField] GameSettings     gameSettings;

        public GameState State { get; private set; } = GameState.MainMenu;
        public RaceFlag  CurrentFlag { get; private set; } = RaceFlag.Green;

        public event Action<GameState> OnStateChanged;
        public event Action<RaceFlag>  OnFlagChanged;
        public event Action<int>       OnLapCompleted;   // lap number
        public event Action            OnRaceStarted;
        public event Action            OnPitWindowOpen;

        // Race session state
        public int   CurrentLap    { get; private set; }
        public int   TotalLaps     { get; private set; }
        public float RaceTime      { get; private set; }
        public bool  IsNightRace   { get; private set; }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (gameSettings != null) { gameSettings.Load(); gameSettings.Register(); }
        }

        IEnumerator Start()
        {
            yield return connectivity.CheckAsync();
            PushSystem.Init();
            audioManager.Init();
            TransitionTo(GameState.MainMenu);
        }

        void Update()
        {
            if (State == GameState.Racing || State == GameState.SafetyCar)
                RaceTime += Time.deltaTime;
        }

        // ── State machine ───────────────────────────────────────────────────

        public void TransitionTo(GameState next)
        {
            State = next;
            OnStateChanged?.Invoke(next);

            switch (next)
            {
                case GameState.MainMenu:
                    audioManager.PlayMusicTrack(MusicTrack.MenuTheme);
                    break;

                case GameState.Formation:
                    audioManager.PlayMusicTrack(MusicTrack.RaceBuild);
                    weatherSystem.ResetForRace();
                    visualWeather.Init(IsNightRace);
                    break;

                case GameState.Racing:
                    OnRaceStarted?.Invoke();
                    SetFlag(RaceFlag.Green);
                    break;

                case GameState.Results:
                    audioManager.PlayMusicTrack(MusicTrack.PodiumFanfare);
                    careerManager.OnRaceComplete(seasonManager.LastRaceResult);
                    break;
            }
        }

        // ── Race events ──────────────────────────────────────────────────────

        public void NotifyLapComplete(int lap)
        {
            CurrentLap = lap;
            OnLapCompleted?.Invoke(lap);
            weatherSystem.AdvanceLap();

            // Pit window opens from lap 20% into race
            if (lap == Mathf.FloorToInt(TotalLaps * 0.20f))
                OnPitWindowOpen?.Invoke();
        }

        public void SetFlag(RaceFlag flag)
        {
            CurrentFlag = flag;
            OnFlagChanged?.Invoke(flag);

            if (flag == RaceFlag.SafetyCar || flag == RaceFlag.VirtualSafetyCar)
                TransitionTo(GameState.SafetyCar);
            else if (flag == RaceFlag.Green && State == GameState.SafetyCar)
                TransitionTo(GameState.Racing);
        }

        public void StartRaceSession(int totalLaps, bool nightRace)
        {
            TotalLaps   = totalLaps;
            CurrentLap  = 0;
            RaceTime    = 0f;
            IsNightRace = nightRace;
            visualWeather.SetNightMode(nightRace);
            audioManager.SetNightMode(nightRace);
            TransitionTo(GameState.Formation);
        }

        public void TogglePause()
        {
            if (State == GameState.Racing)
            {
                Time.timeScale = 0f;
                TransitionTo(GameState.Paused);
            }
            else if (State == GameState.Paused)
            {
                Time.timeScale = 1f;
                TransitionTo(GameState.Racing);
            }
        }

        // ── Lightning relay (from VisualWeather) ─────────────────────────────

        public void OnLightningFlash(float audioDelaySecs)
        {
            StartCoroutine(DelayedThunder(audioDelaySecs));
        }

        IEnumerator DelayedThunder(float delay)
        {
            yield return new WaitForSeconds(delay);
            audioManager.PlayThunder(weatherSystem.CurrentState);
        }
    }
}
