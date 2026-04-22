using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using FormulaSim.Cars;
using FormulaSim.Weather;

namespace FormulaSim.Audio
{
    public enum MusicTrack { MenuTheme, RaceBuild, RaceIntense, PodiumFanfare, NightRaceTheme, RainAmbient }

    public class AudioManager : MonoBehaviour
    {
        [Header("Mixer")]
        [SerializeField] AudioMixer mixer;

        [Header("Engine Sources (RPM layers)")]
        [SerializeField] AudioSource engineIdle, engineLow, engineMid, engineHigh, engineLimit;

        [Header("Transmission")]
        [SerializeField] AudioSource transmissionSource;
        [SerializeField] AudioClip   clipUpshift, clipDownshift, clipDrsOpen, clipDrsClose, clipRevLimit;

        [Header("Weather")]
        [SerializeField] AudioSource rainBodyLight, rainBodyHeavy, rainTrackLight, rainTrackHeavy;
        [SerializeField] AudioSource windSource;
        [SerializeField] AudioClip[] thunderDistant, thunderCrack;

        [Header("Crowd")]
        [SerializeField] AudioSource crowdAmbient;
        [SerializeField] AudioClip   cheerBig, cheerSmall, gasp, rainGroan;

        [Header("Pit")]
        [SerializeField] AudioSource pitSource;
        [SerializeField] AudioClip   jackRaise, jackLower, wheelGun, wheelGunDone, tyreBang;
        [SerializeField] AudioClip   lollipopUp, crewGo, crewWait, nutOk, nutError;
        [SerializeField] AudioClip   pitSpeedBeep;

        [Header("Crash")]
        [SerializeField] AudioSource crashSource;
        [SerializeField] AudioClip   wallLight, wallMedium, wallHeavy, carToCar;

        [Header("Music")]
        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioClip   menuTheme, raceBuild, raceIntense, podiumFanfare, nightTheme, rainAmbient;

        [Header("UI")]
        [SerializeField] AudioSource uiSource;
        [SerializeField] AudioClip   buttonTap, buttonConfirm, countdownBeep, goSignal;
        [SerializeField] AudioClip   sectorPurple, sectorGreen, sectorYellow, newBestLap, fastestLap;

        [Header("Commentary")]
        [SerializeField] AudioSource commentarySource;

        // Engine RPM crossfade — 5 layers: idle=3500, low=7000, mid=10500, high=13500, limit=14800
        static readonly float[] RPM_CENTERS     = { 3500f, 7000f, 10500f, 13500f, 14800f };
        const  float            CROSSFADE_WIDTH  = 3000f;
        const  float            DUCK_FACTOR      = 0.35f;
        const  float            DUCK_ATTACK      = 0.08f;
        const  float            DUCK_RELEASE     = 0.60f;

        float thunderTimer;
        const float THUNDER_MIN_INTERVAL = 8f;

        public void Init()
        {
            crowdAmbient.loop = true;
            crowdAmbient.Play();
            engineIdle.loop = engineLow.loop = engineMid.loop = engineHigh.loop = engineLimit.loop = true;
            engineIdle.volume = 0f; engineLow.volume = 0f; engineMid.volume = 0f;
            engineHigh.volume = 0f; engineLimit.volume = 0f;
            engineIdle.Play(); engineLow.Play(); engineMid.Play(); engineHigh.Play(); engineLimit.Play();
        }

        // ── Engine ────────────────────────────────────────────────────────────

        public void UpdateEngine(float rpm, float throttle, int gear)
        {
            var sources = new[] { engineIdle, engineLow, engineMid, engineHigh, engineLimit };
            for (int i = 0; i < sources.Length; i++)
            {
                float dist = Mathf.Abs(rpm - RPM_CENTERS[i]);
                float gain = Mathf.Max(0f, 1f - dist / CROSSFADE_WIDTH);
                // Soft-square the bell for sharper transitions
                gain = gain * gain;
                sources[i].volume = Mathf.Lerp(sources[i].volume, gain, Time.deltaTime * 8f);

                // Pitch shift: ±12% around center
                float pitchRange = 0.12f + (i * 0.04f);
                float pitchOffset = Mathf.Clamp((rpm - RPM_CENTERS[i]) / CROSSFADE_WIDTH, -1f, 1f) * pitchRange;
                sources[i].pitch  = 1f + pitchOffset;
            }
        }

        // ── Transmission ─────────────────────────────────────────────────────

        public void PlayTransmissionEvent(TransmissionEvent ev)
        {
            AudioClip clip = ev switch
            {
                TransmissionEvent.UpshiftCut       => clipUpshift,
                TransmissionEvent.DownshiftCrackle  => clipDownshift,
                TransmissionEvent.DrsOpen           => clipDrsOpen,
                TransmissionEvent.DrsClose          => clipDrsClose,
                _                                   => null,
            };
            if (clip) transmissionSource.PlayOneShot(clip);
        }

        // ── Weather ───────────────────────────────────────────────────────────

        public void UpdateWeather(WeatherState state, float rainIntensity, float dt)
        {
            float bodyL = Mathf.Clamp01(Mathf.InverseLerp(0.10f, 0.45f, rainIntensity));
            float bodyH = Mathf.Clamp01(Mathf.InverseLerp(0.55f, 1.00f, rainIntensity));
            float trackL= Mathf.Clamp01(Mathf.InverseLerp(0.05f, 0.40f, rainIntensity));
            float trackH= Mathf.Clamp01(Mathf.InverseLerp(0.60f, 1.00f, rainIntensity));

            SetVolSmooth(rainBodyLight,  bodyL);
            SetVolSmooth(rainBodyHeavy,  bodyH);
            SetVolSmooth(rainTrackLight, trackL);
            SetVolSmooth(rainTrackHeavy, trackH);

            // Low-pass on engine bus when rain_intensity > 0.4
            float cutoff = Mathf.Lerp(22000f, 8000f, Mathf.Clamp01((rainIntensity - 0.4f) / 0.6f));
            mixer.SetFloat("EngineLowpassCutoff", cutoff);
            mixer.SetFloat("CrowdLowpassCutoff",  Mathf.Lerp(22000f, 10000f, rainIntensity));

            // Wind
            SetVolSmooth(windSource, state == WeatherState.Extreme ? 0.7f : state == WeatherState.Heavy ? 0.4f : 0f);

            // Crowd rain groan on state entering rain
            _UpdateThunder(dt, state);
        }

        void _UpdateThunder(float dt, WeatherState state)
        {
            thunderTimer -= dt;
            if (thunderTimer > 0) return;

            float prob = state == WeatherState.Extreme ? 0.55f
                       : state == WeatherState.Heavy   ? 0.25f : 0f;
            if (prob > 0 && Random.value < prob)
                PlayThunder(state);

            thunderTimer = THUNDER_MIN_INTERVAL + Random.Range(0f, 12f);
        }

        public void PlayThunder(WeatherState state)
        {
            var pool = state == WeatherState.Extreme ? thunderCrack : thunderDistant;
            if (pool == null || pool.Length == 0) return;
            crashSource.PlayOneShot(pool[Random.Range(0, pool.Length)], 0.6f);
        }

        // ── Pit stop ─────────────────────────────────────────────────────────

        public void PlayPitEvent(string eventName)
        {
            AudioClip c = eventName switch
            {
                "jack_raise"       => jackRaise,
                "jack_lower"       => jackLower,
                "wheel_gun_spin"   => wheelGun,
                "wheel_gun_done"   => wheelGunDone,
                "tyre_bang"        => tyreBang,
                "lollipop_up"      => lollipopUp,
                "crew_go"          => crewGo,
                "crew_wait"        => crewWait,
                "nut_ok"           => nutOk,
                "nut_error"        => nutError,
                "speed_beep"       => pitSpeedBeep,
                _                  => null,
            };
            if (c) pitSource.PlayOneShot(c);
        }

        // ── Crash ─────────────────────────────────────────────────────────────

        public void PlayCrash(float impactVelocity)
        {
            AudioClip clip = impactVelocity > 60f ? wallHeavy
                           : impactVelocity > 30f ? wallMedium
                           : wallLight;
            crashSource.PlayOneShot(clip, Mathf.Clamp01(impactVelocity / 80f));
        }

        // ── Crowd ─────────────────────────────────────────────────────────────

        public void PlayCrowdEvent(CrowdEvent ev)
        {
            AudioClip c = ev switch
            {
                CrowdEvent.CheerBig   => cheerBig,
                CrowdEvent.CheerSmall => cheerSmall,
                CrowdEvent.Gasp       => gasp,
                CrowdEvent.RainGroan  => rainGroan,
                _                     => null,
            };
            if (c) crowdAmbient.PlayOneShot(c, 0.8f);
        }

        // ── Commentary ducking ────────────────────────────────────────────────

        public void DuckForCommentary(bool active)
        {
            float target = active ? DUCK_FACTOR : 1f;
            float speed  = active ? 1f / DUCK_ATTACK : 1f / DUCK_RELEASE;
            StartCoroutine(_FadeBusVolume("EngineVol",  target, speed));
            StartCoroutine(_FadeBusVolume("CrowdVol",   target, speed));
            StartCoroutine(_FadeBusVolume("WeatherVol", target, speed));
            StartCoroutine(_FadeBusVolume("MusicVol",   Mathf.Clamp(target, 0.15f, 1f), speed));
        }

        IEnumerator _FadeBusVolume(string param, float target, float speed)
        {
            mixer.GetFloat(param, out float cur);
            float linCur = Mathf.Pow(10f, cur / 20f);
            while (!Mathf.Approximately(linCur, target))
            {
                linCur = Mathf.MoveTowards(linCur, target, speed * Time.deltaTime);
                mixer.SetFloat(param, Mathf.Log10(Mathf.Max(0.001f, linCur)) * 20f);
                yield return null;
            }
        }

        // ── Music ─────────────────────────────────────────────────────────────

        public void PlayMusicTrack(MusicTrack track)
        {
            AudioClip c = track switch
            {
                MusicTrack.MenuTheme     => menuTheme,
                MusicTrack.RaceBuild     => raceBuild,
                MusicTrack.RaceIntense   => raceIntense,
                MusicTrack.PodiumFanfare => podiumFanfare,
                MusicTrack.NightRaceTheme=> nightTheme,
                MusicTrack.RainAmbient   => rainAmbient,
                _                        => menuTheme,
            };
            if (!c || musicSource.clip == c) return;
            StartCoroutine(_CrossfadeMusic(c));
        }

        IEnumerator _CrossfadeMusic(AudioClip next)
        {
            float t = 0f;
            float startVol = musicSource.volume;
            while (t < 1.5f) { t += Time.deltaTime; musicSource.volume = Mathf.Lerp(startVol, 0f, t / 1.5f); yield return null; }
            musicSource.clip = next;
            musicSource.Play();
            t = 0f;
            while (t < 1.5f) { t += Time.deltaTime; musicSource.volume = Mathf.Lerp(0f, 0.4f, t / 1.5f); yield return null; }
        }

        public void SetNightMode(bool night)
        {
            if (night) PlayMusicTrack(MusicTrack.NightRaceTheme);
        }

        // ── UI ────────────────────────────────────────────────────────────────

        public void PlayUI(UISound sound)
        {
            AudioClip c = sound switch
            {
                UISound.ButtonTap     => buttonTap,
                UISound.ButtonConfirm => buttonConfirm,
                UISound.CountdownBeep => countdownBeep,
                UISound.GoSignal      => goSignal,
                UISound.SectorPurple  => sectorPurple,
                UISound.SectorGreen   => sectorGreen,
                UISound.SectorYellow  => sectorYellow,
                UISound.NewBestLap    => newBestLap,
                UISound.FastestLap    => fastestLap,
                _                     => null,
            };
            if (c) uiSource.PlayOneShot(c);
        }

        static void SetVolSmooth(AudioSource src, float target)
        {
            if (!src) return;
            src.volume = Mathf.Lerp(src.volume, target, Time.deltaTime * 3f);
            if (target > 0.01f && !src.isPlaying) src.Play();
        }
    }

    public enum CrowdEvent { CheerBig, CheerSmall, Gasp, Boo, RainGroan }
    public enum UISound { ButtonTap, ButtonConfirm, CountdownBeep, GoSignal, SectorPurple, SectorGreen, SectorYellow, NewBestLap, FastestLap }
}
