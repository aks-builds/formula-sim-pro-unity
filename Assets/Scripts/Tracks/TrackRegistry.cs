using System.Collections.Generic;
using UnityEngine;

namespace FormulaSim.Tracks
{
    [CreateAssetMenu(menuName = "FormulaSim/Track Registry", fileName = "TrackRegistry")]
    public class TrackRegistry : ScriptableObject
    {
        [SerializeField] CircuitData[] allCircuits;

        // Ordered race calendar for each tier (IDs must match CircuitData.circuitId)
        [Header("Career Calendars")]
        [SerializeField] string[] rookieCalendar = { "bahrain", "silverstone", "monza" };
        [SerializeField] string[] juniorCalendar = { "bahrain", "canada", "silverstone", "monza", "spa", "suzuka" };
        [SerializeField] string[] proCalendar    = { "bahrain", "monaco", "canada", "silverstone", "spa", "monza", "suzuka", "abudhabi" };

        Dictionary<string, CircuitData> _map;

        void OnEnable()
        {
            _map = new Dictionary<string, CircuitData>();
            foreach (var c in allCircuits)
                if (c) _map[c.circuitId] = c;
        }

        public CircuitData Get(string id)
            => _map.TryGetValue(id, out var c) ? c : null;

        public string[] GetCalendar(string tier) => tier switch
        {
            "rookie"  => rookieCalendar,
            "junior"  => juniorCalendar,
            _         => proCalendar,
        };

        public CircuitData GetOfflineCircuit() => Get("silverstone");

        public CircuitData[] AllCircuits => allCircuits;
    }
}
