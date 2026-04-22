using System;
using System.Collections.Generic;
using UnityEngine;

namespace FormulaSim.Championship
{
    [Serializable]
    public class RaceResult
    {
        public string circuitId;
        public List<FinishEntry> finishOrder = new();
        public string fastestLapDriver;
    }

    [Serializable]
    public class FinishEntry
    {
        public string driverId;
        public string teamId;
        public int    position;
        public bool   finishedRace;
        public float  lapTime;
    }

    public class SeasonManager : MonoBehaviour
    {
        public static readonly int[] POINTS = { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 };
        const int FASTEST_LAP_BONUS = 1;

        public  int         Season        { get; private set; } = 1;
        public  int         CurrentRound  { get; private set; }
        public  RaceResult  LastRaceResult{ get; private set; }

        public Dictionary<string, int> DriverPoints      { get; } = new();
        public Dictionary<string, int> ConstructorPoints { get; } = new();

        string[] calendar;

        public void InitSeason(int season, string[] raceCalendar)
        {
            Season       = season;
            calendar     = raceCalendar;
            CurrentRound = 0;
            DriverPoints.Clear();
            ConstructorPoints.Clear();
        }

        public void RecordRaceResult(RaceResult result)
        {
            LastRaceResult = result;
            CurrentRound++;

            // Award points
            for (int i = 0; i < result.finishOrder.Count && i < POINTS.Length; i++)
            {
                var entry = result.finishOrder[i];
                if (!entry.finishedRace) continue;
                int pts = POINTS[i];
                if (entry.driverId == result.fastestLapDriver && i < 10)
                    pts += FASTEST_LAP_BONUS;

                AddPoints(DriverPoints,      entry.driverId, pts);
                AddPoints(ConstructorPoints, entry.teamId,   pts);
            }
        }

        static void AddPoints(Dictionary<string, int> dict, string key, int pts)
        {
            if (!dict.ContainsKey(key)) dict[key] = 0;
            dict[key] += pts;
        }

        public List<(string id, int pts)> GetDriverStandings()
            => Sorted(DriverPoints);

        public List<(string id, int pts)> GetConstructorStandings()
            => Sorted(ConstructorPoints);

        static List<(string, int)> Sorted(Dictionary<string, int> d)
        {
            var list = new List<(string, int)>();
            foreach (var kv in d) list.Add((kv.Key, kv.Value));
            list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return list;
        }

        public int RoundsRemaining => (calendar?.Length ?? 0) - CurrentRound;

        public bool IsTitleMathematicallyAlive(string driverId)
        {
            var standings = GetDriverStandings();
            if (standings.Count == 0) return true;
            int leader = standings[0].pts;
            int maxPossible = (DriverPoints.ContainsKey(driverId) ? DriverPoints[driverId] : 0)
                            + RoundsRemaining * (POINTS[0] + FASTEST_LAP_BONUS);
            return maxPossible >= leader;
        }
    }
}
