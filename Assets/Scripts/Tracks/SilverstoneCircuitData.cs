using UnityEngine;

namespace FormulaSim.Tracks
{
    /// <summary>
    /// Silverstone GP circuit — 50 waypoints, 5.891 km.
    /// Run Assets > Create > FormulaSim > Circuits > Create Silverstone to generate the asset.
    /// </summary>
    public static class SilverstoneCircuitData
    {
        // Bug fix: CircuitData.waypoints is Vector2[]. Values use (X, Z) from the 3-D layout
        // mapped onto the top-down 2-D plane.
        public static readonly Vector2[] Waypoints = new Vector2[]
        {
            // Start / finish straight
            new(   0f,    0f),  // 0  – Start line
            new( 200f,    0f),  // 1  – Mid straight
            new( 400f,    0f),  // 2  – Copse entry
            // Copse (fast right-hander)
            new( 480f,  -60f),  // 3
            new( 510f, -140f),  // 4  – Copse apex
            new( 490f, -220f),  // 5
            // Maggotts–Becketts–Chapel complex
            new( 440f, -280f),  // 6  – Maggotts entry
            new( 360f, -320f),  // 7  – Maggotts apex
            new( 280f, -290f),  // 8  – Becketts 1 entry
            new( 220f, -340f),  // 9  – Becketts 1 apex
            new( 200f, -410f),  // 10 – Becketts 2 entry
            new( 240f, -470f),  // 11 – Becketts 2 apex
            new( 310f, -490f),  // 12 – Chapel entry
            new( 370f, -520f),  // 13 – Chapel apex
            // Hangar straight
            new( 440f, -540f),  // 14
            new( 600f, -545f),  // 15
            new( 760f, -545f),  // 16
            new( 920f, -545f),  // 17 – Mid Hangar  ← SECTOR 1 END
            new(1080f, -545f),  // 18
            // Stowe
            new(1180f, -530f),  // 19 – Stowe entry
            new(1230f, -470f),  // 20 – Stowe apex
            new(1210f, -400f),  // 21
            // Vale
            new(1180f, -340f),  // 22 – Vale entry
            new(1150f, -280f),  // 23 – Vale apex
            new(1170f, -220f),  // 24
            // Club
            new(1200f, -160f),  // 25 – Club entry
            new(1180f,  -90f),  // 26 – Club apex
            new(1130f,  -40f),  // 27
            // National straight
            new(1060f,    0f),  // 28
            new( 980f,    0f),  // 29 – Abbey entry
            // Abbey
            new( 920f,  -30f),  // 30
            new( 880f,  -80f),  // 31 – Abbey apex
            new( 900f, -140f),  // 32
            // Farm / Village                       ← SECTOR 2 END at index 34
            new( 860f, -190f),  // 33 – Farm entry
            new( 800f, -230f),  // 34 – Farm apex
            new( 740f, -220f),  // 35
            new( 700f, -260f),  // 36 – Village entry
            new( 680f, -320f),  // 37 – Village apex
            new( 700f, -380f),  // 38
            // The Loop
            new( 720f, -430f),  // 39 – Loop entry
            new( 760f, -480f),  // 40
            new( 820f, -500f),  // 41 – Loop apex
            new( 880f, -480f),  // 42
            new( 900f, -430f),  // 43
            // Wellington
            new( 880f, -370f),  // 44
            new( 840f, -310f),  // 45 – Wellington apex
            // Luffield / Woodcote
            new( 780f, -250f),  // 46 – Luffield entry
            new( 700f, -190f),  // 47 – Luffield apex
            new( 580f, -100f),  // 48 – Woodcote exit
            new( 340f,  -30f),  // 49 – Return to pit straight
            // loops back to waypoint 0
        };

        public static readonly int  Sector1End     = 17;
        public static readonly int  Sector2End     = 34;
        public static readonly string CircuitId    = "silverstone";
        public static readonly string DisplayName  = "Silverstone Grand Prix";
        public static readonly string Country      = "United Kingdom";
        public static readonly string City         = "Silverstone";
        public static readonly int    RaceLaps     = 52;
        public static readonly float  LapLengthKm  = 5.891f;
        public static readonly float  SpeedRating  = 0.72f;
        public static readonly string Character    = "High-Speed";

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Assets/Create/FormulaSim/Circuits/Create Silverstone")]
        public static void CreateAsset()
        {
            var data               = ScriptableObject.CreateInstance<CircuitData>();
            data.circuitId         = CircuitId;
            data.displayName       = DisplayName;
            data.country           = Country;
            data.city              = City;
            data.raceLaps          = RaceLaps;
            data.lapLengthKm       = LapLengthKm;
            data.isNightRace       = false;
            data.isStreetCircuit   = false;
            data.weatherVariable   = false;
            data.waypoints         = Waypoints;
            data.sector1End        = Sector1End;
            data.sector2End        = Sector2End;
            data.averageSpeedRating = SpeedRating;
            data.circuitCharacter  = Character;
            data.drsZones = new[]
            {
                new DrsZone { activationWaypointIdx = 0,  endWaypointIdx = 2,  detectionLineOffset = 50f },
                new DrsZone { activationWaypointIdx = 14, endWaypointIdx = 18, detectionLineOffset = 50f },
            };

            _EnsureFolder();
            string path = "Assets/Resources/Circuits/Silverstone.asset";
            UnityEditor.AssetDatabase.CreateAsset(data, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.Selection.activeObject = data;
            Debug.Log($"[CircuitData] Created {path}");
        }

        static void _EnsureFolder()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Circuits"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "Circuits");
        }
#endif
    }
}
