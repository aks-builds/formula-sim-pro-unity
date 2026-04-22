using System;
using System.Collections.Generic;
using UnityEngine;
using FormulaSim.Cars;

namespace FormulaSim.Weather
{
    public enum WeatherState { Dry, Overcast, Drizzle, Light, Heavy, Extreme }

    public class WeatherSystem : MonoBehaviour
    {
        public WeatherState CurrentState { get; private set; } = WeatherState.Dry;
        public float        RainIntensity { get; private set; }  // 0..1
        public float        TrackWetness  { get; private set; }

        public bool         IsWeatherVariable = false;   // set by circuit (e.g. Spa)

        public event Action<WeatherState> OnWeatherChanged;
        public event Action<string>       OnWeatherWarning;   // "incoming rain in 2 laps"

        // Markov transition matrix [from][to]
        static readonly Dictionary<WeatherState, Dictionary<WeatherState, float>> Transitions = new()
        {
            [WeatherState.Dry] = new()
            {
                [WeatherState.Dry]      = 0.80f, [WeatherState.Overcast] = 0.18f,
                [WeatherState.Drizzle]  = 0.02f,
            },
            [WeatherState.Overcast] = new()
            {
                [WeatherState.Dry]      = 0.22f, [WeatherState.Overcast] = 0.45f,
                [WeatherState.Drizzle]  = 0.28f, [WeatherState.Light]    = 0.05f,
            },
            [WeatherState.Drizzle] = new()
            {
                [WeatherState.Overcast] = 0.25f, [WeatherState.Drizzle]  = 0.45f,
                [WeatherState.Light]    = 0.28f, [WeatherState.Dry]      = 0.02f,
            },
            [WeatherState.Light] = new()
            {
                [WeatherState.Drizzle]  = 0.30f, [WeatherState.Light]    = 0.40f,
                [WeatherState.Heavy]    = 0.22f, [WeatherState.Overcast]  = 0.08f,
            },
            [WeatherState.Heavy] = new()
            {
                [WeatherState.Light]    = 0.25f, [WeatherState.Heavy]    = 0.50f,
                [WeatherState.Extreme]  = 0.20f, [WeatherState.Drizzle]  = 0.05f,
            },
            [WeatherState.Extreme] = new()
            {
                [WeatherState.Heavy]    = 0.55f, [WeatherState.Extreme]  = 0.45f,
            },
        };

        // Grip table [weather][compound]
        static readonly Dictionary<WeatherState, Dictionary<TireCompound, float>> GripTable = new()
        {
            [WeatherState.Dry]      = new() { [TireCompound.Soft]=1.00f,[TireCompound.Medium]=0.97f,[TireCompound.Hard]=0.93f,[TireCompound.Inter]=0.82f,[TireCompound.Wet]=0.72f },
            [WeatherState.Overcast] = new() { [TireCompound.Soft]=0.98f,[TireCompound.Medium]=0.95f,[TireCompound.Hard]=0.91f,[TireCompound.Inter]=0.84f,[TireCompound.Wet]=0.74f },
            [WeatherState.Drizzle]  = new() { [TireCompound.Soft]=0.78f,[TireCompound.Medium]=0.76f,[TireCompound.Hard]=0.73f,[TireCompound.Inter]=0.92f,[TireCompound.Wet]=0.88f },
            [WeatherState.Light]    = new() { [TireCompound.Soft]=0.55f,[TireCompound.Medium]=0.52f,[TireCompound.Hard]=0.50f,[TireCompound.Inter]=0.94f,[TireCompound.Wet]=0.92f },
            [WeatherState.Heavy]    = new() { [TireCompound.Soft]=0.28f,[TireCompound.Medium]=0.26f,[TireCompound.Hard]=0.25f,[TireCompound.Inter]=0.74f,[TireCompound.Wet]=0.97f },
            [WeatherState.Extreme]  = new() { [TireCompound.Soft]=0.14f,[TireCompound.Medium]=0.13f,[TireCompound.Hard]=0.12f,[TireCompound.Inter]=0.52f,[TireCompound.Wet]=0.88f },
        };

        static readonly Dictionary<WeatherState, float> DragTable = new()
        {
            [WeatherState.Dry]=1.00f, [WeatherState.Overcast]=1.01f, [WeatherState.Drizzle]=1.05f,
            [WeatherState.Light]=1.09f, [WeatherState.Heavy]=1.14f, [WeatherState.Extreme]=1.20f,
        };

        static readonly Dictionary<WeatherState, float> IntensityMap = new()
        {
            [WeatherState.Dry]=0f, [WeatherState.Overcast]=0f, [WeatherState.Drizzle]=0.15f,
            [WeatherState.Light]=0.40f, [WeatherState.Heavy]=0.75f, [WeatherState.Extreme]=1.0f,
        };

        // ── Aquaplane thresholds (m/s) ───────────────────────────────────────
        static readonly Dictionary<WeatherState, float> AquaplaneThreshold = new()
        {
            [WeatherState.Dry]=999f, [WeatherState.Overcast]=999f, [WeatherState.Drizzle]=999f,
            [WeatherState.Light]=65f, [WeatherState.Heavy]=42f, [WeatherState.Extreme]=28f,
        };

        public void ResetForRace()
        {
            CurrentState  = WeatherState.Dry;
            RainIntensity = 0f;
            TrackWetness  = 0f;
        }

        // Called once per lap by GameManager
        public void AdvanceLap()
        {
            var trans = Transitions[CurrentState];
            float roll = UnityEngine.Random.value;
            float cumulative = 0f;
            WeatherState next = CurrentState;
            foreach (var kv in trans)
            {
                cumulative += IsWeatherVariable ? kv.Value * 1.25f : kv.Value;
                if (roll <= cumulative) { next = kv.Key; break; }
            }

            if (next != CurrentState)
            {
                // Warn one lap ahead for rain arrival
                if (IsRainy(next) && !IsRainy(CurrentState))
                    OnWeatherWarning?.Invoke($"Rain forecast — {WeatherLabel(next)} expected next lap");

                CurrentState = next;
                RainIntensity = IntensityMap[next];
                OnWeatherChanged?.Invoke(next);
            }

            // Dry track slowly when not raining
            if (!IsRainy(CurrentState))
                TrackWetness = Mathf.Max(0f, TrackWetness - 0.08f);
            else
                TrackWetness = Mathf.Min(1f, TrackWetness + RainIntensity * 0.15f);
        }

        public struct PhysicsMods
        {
            public float gripMultiplier;
            public float dragMultiplier;
            public float aquaplaneRisk;    // 0..1
        }

        public PhysicsMods GetPhysicsMods(TireCompound compound)
        {
            float grip = GripTable[CurrentState][compound];
            float drag = DragTable[CurrentState];
            float apThresh = AquaplaneThreshold[CurrentState];
            float apRisk = 0f;
            if (compound <= TireCompound.Hard)
                apRisk = Mathf.Clamp01((80f - apThresh) / 60f);    // rough risk factor
            return new PhysicsMods { gripMultiplier=grip, dragMultiplier=drag, aquaplaneRisk=apRisk };
        }

        public static bool IsRainy(WeatherState s)
            => s >= WeatherState.Drizzle;

        public static string WeatherLabel(WeatherState s) => s switch
        {
            WeatherState.Dry      => "DRY",
            WeatherState.Overcast => "OVERCAST",
            WeatherState.Drizzle  => "DRIZZLE",
            WeatherState.Light    => "LIGHT RAIN",
            WeatherState.Heavy    => "HEAVY RAIN",
            WeatherState.Extreme  => "EXTREME",
            _                     => "DRY",
        };

        public TireCompound OptimalCompound()
        {
            return CurrentState switch
            {
                WeatherState.Dry      => TireCompound.Medium,
                WeatherState.Overcast => TireCompound.Medium,
                WeatherState.Drizzle  => TireCompound.Inter,
                WeatherState.Light    => TireCompound.Inter,
                WeatherState.Heavy    => TireCompound.Wet,
                WeatherState.Extreme  => TireCompound.Wet,
                _                     => TireCompound.Medium,
            };
        }
    }
}
