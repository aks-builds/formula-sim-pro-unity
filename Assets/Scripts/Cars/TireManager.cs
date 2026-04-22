using System;
using UnityEngine;
using FormulaSim.Weather;

namespace FormulaSim.Cars
{
    public static class TireManager
    {
        public class TireCornner
        {
            public TireCompound Compound;
            public float Temp;
            public float Pressure;
            public float Wear;           // 0=new, 1=destroyed
            public bool  FlatSpot;
        }

        public class TireSet
        {
            public TireCornner FL, FR, RL, RR;
            public TireCompound Compound;
            public float LapsOn;

            public TireCornner[] All() => new[] { FL, FR, RL, RR };
        }

        public struct CornerLoad
        {
            public float throttlePct;
            public float brakePct;
            public float lateralG;
        }

        // Optimal operating windows (°C)
        static readonly (float min, float ideal, float max)[] OptTemp =
        {
            (80, 95, 110),   // Soft
            (75, 90, 108),   // Medium
            (70, 85, 105),   // Hard
            (40, 55, 75),    // Inter
            (30, 45, 65),    // Wet
        };

        static readonly float[] WearRate   = { 0.000045f, 0.000028f, 0.000018f, 0.000012f, 0.000010f };
        static readonly float[] GripAtFull = { 0.40f,     0.52f,     0.60f,     0.55f,     0.58f };

        const float PressureNominal  = 22f;
        const float PressurePerDeg   = 0.065f;
        const float TempGripColdSlope = 0.008f;
        const float TempGripHotSlope  = 0.012f;
        const float FlatSpotProbPerMs = 0.0008f;
        const float WetSlickWearMult  = 1.8f;

        public static TireSet NewSet(TireCompound c)
        {
            var idx = (int)c;
            var (min, _, _) = OptTemp[idx];
            float startTemp = min + 5f;

            TireCornner Make() => new TireCornner
            {
                Compound = c,
                Temp     = startTemp,
                Pressure = PressureNominal + (startTemp - 20f) * PressurePerDeg,
                Wear     = 0f,
                FlatSpot = false,
            };
            return new TireSet { FL = Make(), FR = Make(), RL = Make(), RR = Make(), Compound = c };
        }

        public static void Update(TireSet tires, float dt, float speedMs,
                                  CornerLoad load, WeatherState weather, bool isLockUp)
        {
            bool isWet    = weather == WeatherState.Heavy || weather == WeatherState.Extreme;
            bool isSlick  = tires.Compound <= TireCompound.Hard;
            int  idx      = (int)tires.Compound;
            var  (min, _, max) = OptTemp[idx];

            foreach (var t in tires.All())
            {
                // Heat
                float heat   = load.throttlePct * 0.35f + load.brakePct * 0.55f
                             + Mathf.Abs(load.lateralG) * 0.40f + 0.05f;
                float coolMult = isWet ? 1.6f : 1.0f;
                float cooling  = (2.8f + speedMs * 0.06f) * coolMult;
                t.Temp         = Mathf.Max(20f, t.Temp + (heat - cooling) * dt);

                // Pressure
                t.Pressure = PressureNominal + (t.Temp - 20f) * PressurePerDeg;

                // Wear
                float loadFactor  = load.throttlePct + load.brakePct + Mathf.Abs(load.lateralG) * 0.5f;
                float effectWear  = WearRate[idx] * loadFactor;
                if (isWet && isSlick) effectWear *= WetSlickWearMult;
                t.Wear = Mathf.Min(1f, t.Wear + effectWear * dt);

                // Flat spot
                if (isLockUp && !t.FlatSpot)
                {
                    float prob = speedMs * FlatSpotProbPerMs * dt;
                    if (UnityEngine.Random.value < prob)
                        t.FlatSpot = true;
                }
            }

            tires.LapsOn += dt / 90f;
        }

        public static float GetGripMult(TireCornner t)
        {
            int idx = (int)t.Compound;
            var (min, _, max) = OptTemp[idx];

            float wearMult  = Mathf.Lerp(GripAtFull[idx], 1f, 1f - t.Wear);
            float tempMult  = 1f;
            if (t.Temp < min)
                tempMult = Mathf.Max(0.4f, 1f - (min - t.Temp) * TempGripColdSlope);
            else if (t.Temp > max)
                tempMult = Mathf.Max(0.3f, 1f - (t.Temp - max) * TempGripHotSlope);

            float flatMult = t.FlatSpot ? 0.94f : 1f;
            return wearMult * tempMult * flatMult;
        }

        public static float GetAvgGrip(TireSet tires)
        {
            float sum = 0;
            foreach (var t in tires.All()) sum += GetGripMult(t);
            return sum / 4f;
        }

        public static PitAdvice GetPitAdvice(TireSet tires, WeatherState weather, int lapsRemaining)
        {
            float avg = 0;
            foreach (var t in tires.All()) avg += t.Wear;
            avg /= 4f;

            bool isWet   = weather == WeatherState.Heavy || weather == WeatherState.Extreme;
            bool isSlick = tires.Compound <= TireCompound.Hard;

            if (isWet && isSlick)
                return new PitAdvice { Urgency = "CRITICAL", Message = "PIT NOW — SLICKS IN HEAVY RAIN", SuggestedCompound = TireCompound.Wet };
            if (avg > 0.88f)
                return new PitAdvice { Urgency = "CRITICAL", Message = "TYRES GONE — BOX THIS LAP" };
            if (avg > 0.72f && lapsRemaining > 3)
                return new PitAdvice { Urgency = "HIGH",     Message = "CONSIDER PITTING — HIGH TYRE WEAR" };
            if (avg > 0.55f && lapsRemaining > 8)
                return new PitAdvice { Urgency = "MEDIUM",   Message = "TYRE MANAGEMENT ADVISED" };
            return null;
        }

        public class PitAdvice
        {
            public string      Urgency;
            public string      Message;
            public TireCompound? SuggestedCompound;
        }
    }
}
