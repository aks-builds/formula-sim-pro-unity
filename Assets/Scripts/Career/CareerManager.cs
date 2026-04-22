using System;
using System.Collections.Generic;
using UnityEngine;
using FormulaSim.Championship;

namespace FormulaSim.Career
{
    [Serializable]
    public class CareerData
    {
        public string playerName;
        public string currentTier    = "rookie";
        public string currentTeamId;
        public int    totalWins;
        public int    totalPodiums;
        public int    totalFastestLaps;
        public int    totalChampionships;
        public int    currentSeason   = 1;
        public float  rating          = 50f;   // 0-100 Elo-like
        public List<string> contractHistory = new();
    }

    [Serializable]
    public class TierDefinition
    {
        public string   id;
        public string[] eligibleTeams;
        public string   calendarKey;
        public int      carPowerCap;   // 0-100
    }

    public class CareerManager : MonoBehaviour
    {
        public static CareerManager Instance { get; private set; }

        public CareerData Data  { get; private set; } = new();
        public bool IsNewSeason { get; private set; }

        static readonly TierDefinition[] Tiers =
        {
            new() { id="rookie", eligibleTeams=new[]{"apex","zenith","kinetic"}, calendarKey="rookie", carPowerCap=85 },
            new() { id="junior", eligibleTeams=new[]{"apex","zenith","kinetic","phantom","nova","eclipse"}, calendarKey="junior", carPowerCap=94 },
            new() { id="pro",    eligibleTeams=new[]{"apex","zenith","kinetic","phantom","nova","eclipse","aurora","titanium"}, calendarKey="pro", carPowerCap=97 },
            new() { id="elite",  eligibleTeams=new[]{"apex","zenith","kinetic","phantom","nova","eclipse","aurora","titanium","inferno","vortex"}, calendarKey="pro", carPowerCap=100 },
        };

        const string SAVE_KEY = "career_v1";

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            _Load();
        }

        public void StartNewCareer(string playerName, string teamId)
        {
            Data = new CareerData { playerName = playerName, currentTeamId = teamId };
            Data.contractHistory.Add(teamId);
            _Save();
        }

        public void OnRaceComplete(RaceResult result)
        {
            if (result == null) return;
            foreach (var entry in result.finishOrder)
            {
                if (entry.driverId != "player") continue;
                if (entry.position == 1) Data.totalWins++;
                if (entry.position <= 3) Data.totalPodiums++;
                _UpdateRating(entry.position, result.finishOrder.Count);
            }
            if (result.fastestLapDriver == "player") Data.totalFastestLaps++;
            _EvaluateTierPromotion();
            _Save();
        }

        void _UpdateRating(int position, int field)
        {
            float expected = 0.5f;
            float actual   = 1f - ((float)(position - 1) / (field - 1));
            Data.rating = Mathf.Clamp(Data.rating + 8f * (actual - expected), 0f, 100f);
        }

        void _EvaluateTierPromotion()
        {
            string next = Data.currentTier switch
            {
                "rookie" when Data.totalWins >= 2 && Data.totalPodiums >= 5   => "junior",
                "junior" when Data.totalWins >= 8 || Data.totalChampionships >= 1 => "pro",
                "pro"    when Data.totalWins >= 15 || Data.totalChampionships >= 2 => "elite",
                _ => null,
            };
            if (next != null)
            {
                Data.currentTier = next;
                IsNewSeason      = true;
            }
        }

        public void AcceptContract(string teamId)
        {
            Data.currentTeamId = teamId;
            if (!Data.contractHistory.Contains(teamId))
                Data.contractHistory.Add(teamId);
            _Save();
        }

        public TierDefinition GetCurrentTier()
        {
            foreach (var t in Tiers) if (t.id == Data.currentTier) return t;
            return Tiers[0];
        }

        void _Save() => PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(Data));
        void _Load()
        {
            string json = PlayerPrefs.GetString(SAVE_KEY, "");
            if (!string.IsNullOrEmpty(json))
                Data = JsonUtility.FromJson<CareerData>(json);
        }
    }
}
