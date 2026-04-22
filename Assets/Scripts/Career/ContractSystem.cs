using System;
using System.Collections.Generic;
using UnityEngine;

namespace FormulaSim.Career
{
    public static class ContractSystem
    {
        [Serializable]
        public class ContractOffer
        {
            public string teamId;
            public string teamName;
            public int    salaryCr;       // in-game credits
            public int    bonusPerWin;
            public int    durationSeasons;
            public string grade;          // "Development", "Competitive", "Factory", "Championship Seat"
            public float  teamPower;      // 0-100 (what the team can offer)
            public bool   isExpiring;     // only valid this season
        }

        static readonly Dictionary<string, Func<CareerData, bool>> UnlockCriteria = new()
        {
            ["apex"]     = _ => true,
            ["zenith"]   = _ => true,
            ["kinetic"]  = _ => true,
            ["phantom"]  = d => d.currentTier != "rookie" && d.totalWins >= 2,
            ["nova"]     = d => d.currentTier != "rookie" && d.totalWins >= 2,
            ["eclipse"]  = d => d.currentTier != "rookie" && d.totalWins >= 3,
            ["aurora"]   = d => d.currentTier == "pro"   || d.currentTier == "elite",
            ["titanium"] = d => (d.currentTier == "pro"  || d.currentTier == "elite") && d.totalWins >= 8 && d.totalChampionships >= 1,
            ["inferno"]  = d => d.currentTier == "elite" && d.totalWins >= 10 && d.totalChampionships >= 1,
            ["vortex"]   = d => d.currentTier == "elite" && d.totalWins >= 15 && d.totalChampionships >= 2,
        };

        static readonly Dictionary<string, (int salary, int bonus, string grade)> TeamOfferTemplate = new()
        {
            ["apex"]     = (50000,  2000,  "Development"),
            ["zenith"]   = (55000,  2500,  "Development"),
            ["kinetic"]  = (60000,  3000,  "Development"),
            ["phantom"]  = (85000,  5000,  "Competitive"),
            ["nova"]     = (90000,  5500,  "Competitive"),
            ["eclipse"]  = (95000,  6000,  "Competitive"),
            ["aurora"]   = (150000, 10000, "Factory"),
            ["titanium"] = (180000, 12000, "Factory"),
            ["inferno"]  = (250000, 18000, "Championship Seat"),
            ["vortex"]   = (300000, 22000, "Championship Seat"),
        };

        public static List<ContractOffer> GenerateOffers(CareerData data, int count = 3)
        {
            var unlocked = new List<string>();
            foreach (var kv in UnlockCriteria)
                if (kv.Value(data) && kv.Key != data.currentTeamId)
                    unlocked.Add(kv.Key);

            // Shuffle and pick up to `count`
            for (int i = unlocked.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (unlocked[i], unlocked[j]) = (unlocked[j], unlocked[i]);
            }

            var offers = new List<ContractOffer>();
            for (int i = 0; i < Mathf.Min(count, unlocked.Count); i++)
            {
                string id = unlocked[i];
                if (!TeamOfferTemplate.TryGetValue(id, out var tmpl)) continue;
                offers.Add(new ContractOffer
                {
                    teamId          = id,
                    salaryCr        = tmpl.salary,
                    bonusPerWin     = tmpl.bonus,
                    durationSeasons = 1,
                    grade           = tmpl.grade,
                    isExpiring      = UnityEngine.Random.value < 0.3f,
                });
            }

            // Always include current team as renewal option
            if (TeamOfferTemplate.TryGetValue(data.currentTeamId, out var cur))
                offers.Insert(0, new ContractOffer
                {
                    teamId          = data.currentTeamId,
                    salaryCr        = cur.salary,
                    bonusPerWin     = cur.bonus,
                    durationSeasons = 2,
                    grade           = cur.grade,
                });

            return offers;
        }
    }
}
