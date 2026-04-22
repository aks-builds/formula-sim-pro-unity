using System;
using System.Collections.Generic;
using UnityEngine;

namespace FormulaSim.Livery
{
    [Serializable]
    public class LiveryData
    {
        public string liveryType   = "base";      // base/anniversary/champion/fastest_lap
        public float  primaryHueOffset;           // degrees from team base
        public float  primarySaturation = 1f;
        public float  secondaryHueOffset;
        public int    accentStyle;
        public int    helmetStyle;
        public string numberColor = "white";
        public int    carNumber;
    }

    [Serializable]
    public class LiveryLimits
    {
        public float hueRange        = 15f;   // ° either side of team base hue
        public float satMin          = 0.5f;
        public float satMax          = 1.0f;
        public bool  allowSecondary  = true;
    }

    public static class LiverySystem
    {
        // Base hues (HSV H component, 0-360) per team
        static readonly Dictionary<string, float> BaseHue = new()
        {
            ["apex"]    = 210f, ["zenith"]  = 120f, ["kinetic"] = 15f,
            ["phantom"] = 270f, ["nova"]    = 180f, ["eclipse"] = 240f,
            ["aurora"]  = 30f,  ["titanium"]= 200f, ["inferno"] = 0f,
            ["vortex"]  = 290f,
        };

        static readonly Dictionary<string, LiveryLimits> Limits = new()
        {
            ["apex"]    = new() { hueRange = 25f },
            ["zenith"]  = new() { hueRange = 20f },
            ["kinetic"] = new() { hueRange = 18f },
            ["phantom"] = new() { hueRange = 15f },
            ["nova"]    = new() { hueRange = 15f },
            ["eclipse"] = new() { hueRange = 14f },
            ["aurora"]  = new() { hueRange = 12f },
            ["titanium"]= new() { hueRange = 10f },
            ["inferno"] = new() { hueRange = 10f },
            ["vortex"]  = new() { hueRange =  8f },
        };

        static readonly Dictionary<string, LiveryData> SavedLiveries = new();

        public static LiveryData GetDefault(string teamId) => new()
        {
            primaryHueOffset  = 0f,
            primarySaturation = 1f,
            secondaryHueOffset= 0f,
            accentStyle       = 0,
            helmetStyle       = 0,
            numberColor       = "white",
        };

        public static LiveryLimits GetLimits(string teamId)
            => Limits.TryGetValue(teamId, out var l) ? l : new LiveryLimits();

        public static Color GetPrimaryColor(string teamId, LiveryData livery)
        {
            float baseH = BaseHue.TryGetValue(teamId, out float h) ? h : 0f;
            float finalH = (baseH + livery.primaryHueOffset + 360f) % 360f;

            // Champion livery: shift to gold
            if (livery.liveryType == "champion")
                finalH = Mathf.Lerp(finalH, 43f, 0.7f);

            return Color.HSVToRGB(finalH / 360f, livery.primarySaturation, 0.85f);
        }

        public static Color GetSecondaryColor(string teamId, LiveryData livery)
        {
            float baseH  = BaseHue.TryGetValue(teamId, out float h) ? h : 0f;
            float finalH = (baseH + 180f + livery.secondaryHueOffset + 360f) % 360f;
            return Color.HSVToRGB(finalH / 360f, 0.6f, 0.9f);
        }

        public static void Save(string teamId, LiveryData livery)
        {
            SavedLiveries[teamId] = livery;
            PlayerPrefs.SetString($"livery_{teamId}", JsonUtility.ToJson(livery));
        }

        public static LiveryData Load(string teamId)
        {
            if (SavedLiveries.TryGetValue(teamId, out var cached)) return cached;
            string json = PlayerPrefs.GetString($"livery_{teamId}", "");
            if (string.IsNullOrEmpty(json)) return GetDefault(teamId);
            var data = JsonUtility.FromJson<LiveryData>(json);
            SavedLiveries[teamId] = data;
            return data;
        }
    }
}
