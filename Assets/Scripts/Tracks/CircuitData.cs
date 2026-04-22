using UnityEngine;

namespace FormulaSim.Tracks
{
    [CreateAssetMenu(menuName = "FormulaSim/Circuit Data", fileName = "Circuit_New")]
    public class CircuitData : ScriptableObject
    {
        [Header("Identity")]
        public string circuitId;
        public string displayName;
        public string country;
        public string city;

        [Header("Race Config")]
        public int   raceLaps        = 58;
        public float lapLengthKm     = 5.3f;
        public bool  isNightRace     = false;
        public bool  isStreetCircuit = false;
        public bool  isAntiClockwise = false;
        public bool  weatherVariable = false;   // Spa: higher rain probability
        public bool  hasTunnel       = false;
        public bool  hasOverpass     = false;

        [Header("DRS Zones")]
        public DrsZone[] drsZones;

        [Header("Waypoints (world positions)")]
        public Vector2[] waypoints;

        [Header("Tunnel Waypoint Range")]
        public Vector2Int tunnelRange;     // waypoint index range where tunnel is active
        public Vector2Int overpassUnder;
        public Vector2Int overpassOver;

        [Header("Named Corners")]
        public CornerInfo[] corners;

        [Header("Sector Split Indices")]
        public int sector1End;
        public int sector2End;

        [Header("Track Metrics")]
        [Range(0,1)] public float averageSpeedRating;   // 0=Monaco, 1=Monza
        [Range(0,1)] public float downtownDensity;      // street circuit walls
        public string circuitCharacter;                 // "High-Speed", "Technical", "Street"
    }

    [System.Serializable]
    public class DrsZone
    {
        public int   activationWaypointIdx;
        public int   endWaypointIdx;
        public float detectionLineOffset;  // metres before activation
    }

    [System.Serializable]
    public class CornerInfo
    {
        public string name;
        [Range(0,1)] public float apexSpeed;  // fraction of max speed
        public int waypointIdx;
        public bool isHighSpeed;
    }
}
