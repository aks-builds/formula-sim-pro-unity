using System.Collections.Generic;
using UnityEngine;

namespace FormulaSim.Teams
{
    [System.Serializable]
    public class DriverData
    {
        public string id;
        public string fullName;
        public string abbreviation;    // 3-letter code (e.g. VRN)
        public int    carNumber;
        public string nationality;
        [Range(0,100)] public float skill;
        [Range(0,100)] public float aggression;
        [Range(0,100)] public float consistency;
        [Range(0,100)] public float wetWeatherAbility;
    }

    [System.Serializable]
    public class TeamData
    {
        public string id;
        public string name;
        public string shortName;
        public Color  primaryColor;
        public Color  secondaryColor;
        [Range(0,100)] public float power;
        [Range(0,100)] public float chassis;
        [Range(0,100)] public float reliability;
        [Range(0,100)] public float aero;
        public DriverData[] drivers;
        public string tier;           // "rookie","junior","pro","elite"
        public bool   isUnlocked;
    }

    [CreateAssetMenu(menuName = "FormulaSim/Team Registry", fileName = "TeamRegistry")]
    public class TeamRegistry : ScriptableObject
    {
        [SerializeField] TeamData[] teams;

        static readonly Dictionary<string, TeamData> Map = new();

        void OnEnable()
        {
            Map.Clear();
            foreach (var t in teams) if (t != null) Map[t.id] = t;
        }

        public TeamData Get(string id)
            => Map.TryGetValue(id, out var t) ? t : null;

        public TeamData[] AllTeams => teams;

        public TeamData[] GetByTier(string tier)
        {
            var list = new List<TeamData>();
            foreach (var t in teams) if (t.tier == tier) list.Add(t);
            return list.ToArray();
        }

        // Performance modifier 0.75-1.0 based on circuit character vs team strengths
        public float PerformanceAt(string teamId, Tracks.CircuitData circuit)
        {
            if (!Map.TryGetValue(teamId, out var team)) return 0.85f;
            float aeroWeight = circuit.averageSpeedRating;
            float chassisWeight = 1f - circuit.averageSpeedRating;
            float raw = (team.aero * aeroWeight + team.chassis * chassisWeight) / 100f;
            return Mathf.Lerp(0.75f, 1.0f, raw);
        }
    }
}
