using UnityEngine;
using FormulaSim.Tracks;

/// <summary>
/// Static circuit waypoint library for all 7 non-Silverstone GP circuits.
/// Each circuit has 40–52 waypoints (top-down 2-D, 1 unit = 1 metre).
/// Run Assets > Create > FormulaSim > Circuits > Create All Circuits to generate
/// all CircuitData ScriptableObjects into Assets/Resources/Circuits/.
/// </summary>
public static class CircuitLibrary
{
    // ── MONACO — 3.337 km, 78 laps, street circuit, tunnel ───────────────────
    static readonly Vector2[] _Monaco = {
        new(  0,   0), // 0  Start / Finish
        new( 60,  -4), // 1
        new(120, -10), // 2  Sainte-Dévote entry
        new(155, -42), // 3  Sainte-Dévote apex
        new(144, -80), // 4
        new(122,-122), // 5  Uphill to Casino
        new( 98,-168), // 6
        new( 70,-214), // 7  Casino Square entry
        new( 38,-250), // 8  Casino
        new(  8,-272), // 9
        new(-26,-288), // 10 Mirabeau entry
        new(-58,-296), // 11 Mirabeau
        new(-84,-288), // 12
        new(-105,-268),// 13 Grand Hotel / Loews entry
        new(-118,-244),// 14 Loews apex (super-hairpin)
        new(-122,-218),// 15
        new(-106,-194),// 16 Portier entry
        new(-80, -172),// 17 Portier apex
        new(-52, -156),// 18
        new(-18, -150),// 19 Tunnel entry
        new( 40, -148),// 20 In tunnel
        new(102, -146),// 21 In tunnel
        new(168, -142),// 22 Tunnel exit
        new(212, -126),// 23
        new(240, -104),// 24 Nouvelle Chicane entry
        new(252,  -76),// 25 Chicane left
        new(242,  -50),// 26 Chicane right
        new(220,  -32),// 27 Post-chicane / Tabac
        new(202,  -10),// 28 Tabac apex
        new(192,   16),// 29 Swimming Pool 1
        new(204,   40),// 30 Swimming Pool 2
        new(194,   62),// 31 Swimming Pool exit
        new(170,   74),// 32 Rascasse entry
        new(142,   78),// 33 Rascasse apex
        new(115,   70),// 34 Anthony Noghès entry
        new( 92,   52),// 35 Anthony Noghès apex
        new( 76,   32),// 36
        new( 56,   14),// 37
        new( 30,    4),// 38
        new(  8,    0),// 39
        // loops to 0
    };

    // ── MONZA — 5.793 km, 53 laps, Italy, high-speed ─────────────────────────
    static readonly Vector2[] _Monza = {
        new(   0,   0), // 0  Start / Finish
        new( 150,   0), // 1  Pit straight
        new( 310,   0), // 2
        new( 460,   0), // 3  Brake zone — Rettifilo
        new( 492, -22), // 4  Rettifilo right
        new( 476, -58), // 5  Rettifilo left
        new( 492, -92), // 6  Rettifilo right exit
        new( 508,-108), // 7
        new( 492,-148), // 8  Curva Grande entry
        new( 454,-188), // 9
        new( 406,-210), // 10
        new( 352,-218), // 11
        new( 296,-208), // 12
        new( 244,-186), // 13
        new( 204,-150), // 14
        new( 190,-110), // 15
        new( 186, -70), // 16 Roggia chicane entry
        new( 198, -38), // 17 Roggia right   ← SECTOR 1 END
        new( 208,  -8), // 18 Roggia left
        new( 196,  22), // 19 Roggia exit
        new( 178,  52), // 20 Lesmo 1 entry
        new( 148,  78), // 21
        new( 108,  96), // 22 Lesmo 1 apex
        new(  68, 112), // 23
        new(  44, 152), // 24 Lesmo 2 entry
        new(  52, 196), // 25 Lesmo 2 apex
        new(  78, 228), // 26
        new( 116, 248), // 27
        new( 158, 258), // 28 Ascari approach
        new( 198, 246), // 29 Ascari right
        new( 218, 222), // 30
        new( 214, 196), // 31 Ascari left     ← SECTOR 2 END
        new( 218, 170), // 32 Ascari right exit
        new( 238, 146), // 33
        new( 274, 122), // 34 Parabolica entry
        new( 322,  98), // 35
        new( 380,  78), // 36 Parabolica apex
        new( 442,  68), // 37
        new( 494,  74), // 38
        new( 524,  94), // 39
        new( 516, 136), // 40
        new( 484, 172), // 41
        new( 428, 196), // 42
        new( 352, 208), // 43
        new( 264, 198), // 44
        new( 174, 170), // 45
        new(  98, 130), // 46
        new(  42,  84), // 47
        new(   8,  38), // 48
        new(   0,   0), // 49 loops
    };

    // ── SPA-FRANCORCHAMPS — 7.004 km, 44 laps, Belgium, weatherVariable ───────
    static readonly Vector2[] _Spa = {
        new(   0,   0), // 0  Start / Finish
        new( 130,  -8), // 1  Kemmel approach
        new( 260, -16), // 2
        new( 390, -12), // 3
        new( 450,  10), // 4  La Source hairpin entry
        new( 458,  50), // 5  La Source apex
        new( 426,  80), // 6
        new( 380,  96), // 7  Eau Rouge / Raidillon entry
        new( 330, 116), // 8  Raidillon left
        new( 282, 152), // 9  Raidillon right (uphill)
        new( 250, 196), // 10 Top of hill
        new( 248, 240), // 11 Kemmel straight start
        new( 250, 300), // 12
        new( 252, 370), // 13
        new( 254, 430), // 14
        new( 256, 490), // 15
        new( 270, 540), // 16 Les Combes entry
        new( 292, 568), // 17 Les Combes right ← SECTOR 1 END
        new( 274, 592), // 18 Les Combes left
        new( 248, 608), // 19
        new( 214, 618), // 20 Malmedy / Rivage entry
        new( 188, 640), // 21 Rivage apex
        new( 186, 666), // 22 Pouhon entry
        new( 180, 708), // 23 Pouhon left
        new( 196, 742), // 24 Pouhon right
        new( 228, 758), // 25
        new( 268, 762), // 26 Campus / Fagnes
        new( 308, 750), // 27
        new( 344, 728), // 28 Stavelot entry
        new( 372, 700), // 29 Stavelot apex
        new( 398, 674), // 30
        new( 428, 666), // 31 Paul Frère / Blanchimont entry
        new( 464, 660), // 32 Blanchimont (flat-out)
        new( 504, 642), // 33 ← SECTOR 2 END
        new( 536, 616), // 34
        new( 556, 582), // 35 Bus Stop chicane entry
        new( 562, 548), // 36 Bus Stop right
        new( 548, 518), // 37 Bus Stop left
        new( 518, 502), // 38
        new( 480, 500), // 39
        new( 432, 490), // 40 Return to pit straight
        new( 368, 452), // 41
        new( 288, 380), // 42
        new( 188, 278), // 43
        new(  80, 148), // 44
        new(   0,   0), // 45 loops
    };

    // ── SUZUKA — 5.807 km, 53 laps, Japan, figure-8 (hasOverpass) ────────────
    static readonly Vector2[] _Suzuka = {
        new(   0,   0), // 0  Start / Finish
        new( 110,   0), // 1  Pit straight
        new( 220,   0), // 2  Turn 1 entry
        new( 268, -36), // 3  Turn 1
        new( 272, -86), // 4  Turn 2
        new( 252,-128), // 5  S-Curves entry
        new( 218,-160), // 6  S-Curve left
        new( 186,-192), // 7  S-Curve right
        new( 154,-224), // 8  S-Curve left
        new( 128,-262), // 9  Dunlop entry
        new( 110,-304), // 10 Dunlop
        new( 100,-348), // 11 Degner 1 entry
        new(  96,-390), // 12 Degner 1 apex
        new( 110,-428), // 13 Degner 2
        new( 132,-458), // 14
        new( 168,-474), // 15 Hairpin entry ← SECTOR 1 END
        new( 210,-480), // 16 Hairpin turn
        new( 252,-470), // 17 Hairpin exit
        new( 290,-448), // 18 Spoon approach
        new( 320,-416), // 19
        new( 342,-378), // 20 Spoon entry
        new( 344,-336), // 21 Spoon apex
        new( 328,-300), // 22 Spoon exit
        new( 296,-272), // 23
        new( 260,-256), // 24 Back straight
        new( 218,-250), // 25
        new( 172,-252), // 26
        new( 128,-264), // 27 Overpass approach (goes over S-curves)
        new(  88,-280), // 28
        new(  50,-302), // 29 130R entry
        new(  24,-334), // 30 130R (flat)
        new(   8,-374), // 31 130R apex
        new(  12,-414), // 32 130R exit ← SECTOR 2 END
        new(  34,-446), // 33
        new(  64,-468), // 34 Chicane entry
        new(  88,-486), // 35 Chicane right
        new(  70,-504), // 36 Chicane left
        new(  42,-514), // 37 Chicane exit
        new(   8,-520), // 38 Final complex
        new( -28,-508), // 39
        new( -56,-482), // 40
        new( -68,-444), // 41
        new( -58,-400), // 42
        new( -32,-354), // 43
        new(   8,-306), // 44
        new(  42,-256), // 45
        new(  56,-196), // 46
        new(  48,-136), // 47
        new(  26, -72), // 48
        new(   6, -20), // 49
        // loops to 0
    };

    // ── ABU DHABI — 5.281 km, 58 laps, UAE, night race ───────────────────────
    static readonly Vector2[] _AbuDhabi = {
        new(   0,   0), // 0  Start / Finish
        new( 140,  -5), // 1  Straight
        new( 280,  -5), // 2
        new( 380, -10), // 3  Turn 1 entry
        new( 414, -44), // 4  Turn 1
        new( 416, -88), // 5  Turn 2
        new( 404,-124), // 6  Hotel section entry
        new( 380,-156), // 7  Hotel hairpin
        new( 344,-172), // 8
        new( 296,-176), // 9  Hotel exit
        new( 248,-168), // 10
        new( 204,-152), // 11
        new( 166,-128), // 12 Turn 5 area
        new( 144, -96), // 13
        new( 136, -58), // 14 ← SECTOR 1 END
        new( 140, -18), // 15
        new( 160,  16), // 16 Turn 8 entry
        new( 192,  44), // 17 Turn 9 (sequence)
        new( 228,  60), // 18
        new( 270,  66), // 19 Marina complex
        new( 314,  54), // 20 Inner loop turn
        new( 348,  24), // 21
        new( 366, -10), // 22
        new( 370, -48), // 23
        new( 354, -82), // 24 Inner hairpin
        new( 324,-104), // 25
        new( 288,-108), // 26
        new( 252, -94), // 27
        new( 226, -64), // 28 ← SECTOR 2 END
        new( 216, -28), // 29
        new( 224,  10), // 30
        new( 246,  42), // 31 Yas Marina hotel underpass
        new( 276,  64), // 32
        new( 308,  70), // 33
        new( 340,  54), // 34
        new( 364,  18), // 35
        new( 370, -28), // 36
        new( 348, -68), // 37
        new( 306, -88), // 38
        new( 256, -84), // 39
        new( 200, -60), // 40
        new( 150, -20), // 41
        new(  96,  10), // 42
        new(  44,  10), // 43
        new(   4,   4), // 44
        // loops to 0
    };

    // ── BAHRAIN — 5.412 km, 57 laps, Bahrain ─────────────────────────────────
    static readonly Vector2[] _Bahrain = {
        new(   0,   0), // 0  Start / Finish
        new( 140,   0), // 1
        new( 280,   0), // 2  Turn 1 entry
        new( 326, -38), // 3  Turn 1 apex
        new( 326, -82), // 4  Turn 2
        new( 310,-122), // 5  Turn 3 entry
        new( 274,-148), // 6  Turn 3 apex
        new( 232,-148), // 7
        new( 192,-140), // 8  Turn 4 entry
        new( 162,-114), // 9  Turn 4 apex
        new( 150, -78), // 10
        new( 150, -38), // 11 Turn 5 entry
        new( 158,  -2), // 12 Turn 5 area
        new( 182,  26), // 13
        new( 218,  44), // 14 Back section
        new( 262,  50), // 15 ← SECTOR 1 END
        new( 310,  44), // 16
        new( 352,  20), // 17 Turn 8 hairpin entry
        new( 370, -18), // 18 Turn 8 apex
        new( 358, -58), // 19
        new( 322, -72), // 20
        new( 284, -64), // 21 Turn 10 entry
        new( 262, -34), // 22
        new( 260,   2), // 23
        new( 278,  36), // 24 Turn 12
        new( 306,  56), // 25
        new( 336,  52), // 26 Turn 13
        new( 354,  24), // 27
        new( 360, -16), // 28 Turn 14 ← SECTOR 2 END
        new( 348, -54), // 29
        new( 318, -70), // 30
        new( 278, -62), // 31 Turn 15 area
        new( 250, -32), // 32
        new( 246,   6), // 33
        new( 258,  42), // 34
        new( 282,  64), // 35
        new( 310,  72), // 36
        new( 330,  58), // 37
        new( 340,  24), // 38
        new( 320, -14), // 39
        new( 274, -24), // 40
        new( 218,  -8), // 41
        new( 152,  18), // 42
        new(  80,  22), // 43
        new(  28,   8), // 44
        // loops to 0
    };

    // ── CANADA (Gilles Villeneuve) — 4.361 km, 70 laps, island circuit ────────
    static readonly Vector2[] _Canada = {
        new(   0,   0), // 0  Start / Finish
        new( 120,   0), // 1  Pit straight
        new( 240,   0), // 2
        new( 340,   0), // 3  Turn 1 entry
        new( 380, -32), // 4  Turn 1
        new( 384, -72), // 5  Turn 2
        new( 368,-108), // 6
        new( 338,-130), // 7  Turn 3
        new( 296,-134), // 8
        new( 252,-124), // 9  Turn 4 entry
        new( 218, -98), // 10 Turn 4 apex (Island hairpin)
        new( 200, -60), // 11
        new( 200, -18), // 12 Turn 5 entry
        new( 222,  16), // 13 Turn 5
        new( 258,  32), // 14
        new( 298,  32), // 15 ← SECTOR 1 END
        new( 334,  16), // 16 Turn 6
        new( 352, -18), // 17
        new( 340, -52), // 18 L'Épingle (hairpin) entry
        new( 310, -68), // 19 L'Épingle apex
        new( 274, -60), // 20
        new( 248, -28), // 21
        new( 246,  10), // 22 Casino (chicane) entry
        new( 260,  46), // 23 Casino left
        new( 252,  82), // 24 Casino right
        new( 232, 106), // 25
        new( 196, 116), // 26 ← SECTOR 2 END
        new( 154, 112), // 27
        new( 116,  94), // 28 Back straight
        new(  80,  62), // 29
        new(  54,  22), // 30 Wall of Champions chicane entry
        new(  44, -18), // 31 Chicane right
        new(  28, -50), // 32 Chicane left
        new(   8, -62), // 33
        new( -18, -58), // 34
        new( -38, -32), // 35
        new( -38,   8), // 36
        new( -18,  30), // 37
        new(  12,  28), // 38
        new(  42,  18), // 39
        new(  50,  -8), // 40
        new(  46, -38), // 41
        new(  22, -52), // 42
        new( -12, -44), // 43
        new( -22,   0), // 44
        new( -12,  18), // 45 Final turn
        new(   0,  10), // 46
        // loops to 0
    };

    // ── Circuit metadata ──────────────────────────────────────────────────────

    public struct CircuitDef
    {
        public string    id;
        public string    displayName;
        public string    country;
        public string    city;
        public int       raceLaps;
        public float     lapLengthKm;
        public bool      isNightRace;
        public bool      isStreetCircuit;
        public bool      isAntiClockwise;
        public bool      weatherVariable;
        public bool      hasTunnel;
        public bool      hasOverpass;
        public float     speedRating;
        public string    character;
        public Vector2[] waypoints;
        public int       sector1End;
        public int       sector2End;
        public DrsZone[] drsZones;
    }

    public static CircuitDef[] All => new[]
    {
        new CircuitDef
        {
            id="monaco", displayName="Monaco Grand Prix", country="Monaco", city="Monte Carlo",
            raceLaps=78, lapLengthKm=3.337f, isStreetCircuit=true, isAntiClockwise=false,
            hasTunnel=true, speedRating=0.18f, character="Street",
            waypoints=_Monaco, sector1End=17, sector2End=27,
            drsZones = new[] { new DrsZone { activationWaypointIdx=0, endWaypointIdx=2, detectionLineOffset=40f } },
        },
        new CircuitDef
        {
            id="monza", displayName="Monza Grand Prix", country="Italy", city="Monza",
            raceLaps=53, lapLengthKm=5.793f, speedRating=0.96f, character="High-Speed",
            waypoints=_Monza, sector1End=17, sector2End=31,
            drsZones = new[]
            {
                new DrsZone { activationWaypointIdx=0, endWaypointIdx=3, detectionLineOffset=60f },
                new DrsZone { activationWaypointIdx=28, endWaypointIdx=33, detectionLineOffset=60f },
            },
        },
        new CircuitDef
        {
            id="spa", displayName="Spa-Francorchamps", country="Belgium", city="Stavelot",
            raceLaps=44, lapLengthKm=7.004f, weatherVariable=true, speedRating=0.78f, character="High-Speed",
            waypoints=_Spa, sector1End=17, sector2End=33,
            drsZones = new[]
            {
                new DrsZone { activationWaypointIdx=0,  endWaypointIdx=4,  detectionLineOffset=50f },
                new DrsZone { activationWaypointIdx=11, endWaypointIdx=17, detectionLineOffset=50f },
            },
        },
        new CircuitDef
        {
            id="suzuka", displayName="Suzuka Circuit", country="Japan", city="Suzuka",
            raceLaps=53, lapLengthKm=5.807f, hasOverpass=true, speedRating=0.70f, character="Technical",
            waypoints=_Suzuka, sector1End=15, sector2End=32,
            drsZones = new[]
            {
                new DrsZone { activationWaypointIdx=0,  endWaypointIdx=2,  detectionLineOffset=50f },
                new DrsZone { activationWaypointIdx=22, endWaypointIdx=28, detectionLineOffset=50f },
            },
        },
        new CircuitDef
        {
            id="abudhabi", displayName="Yas Marina Circuit", country="United Arab Emirates", city="Abu Dhabi",
            raceLaps=58, lapLengthKm=5.281f, isNightRace=true, speedRating=0.68f, character="Technical",
            waypoints=_AbuDhabi, sector1End=14, sector2End=28,
            drsZones = new[]
            {
                new DrsZone { activationWaypointIdx=0,  endWaypointIdx=3,  detectionLineOffset=50f },
                new DrsZone { activationWaypointIdx=29, endWaypointIdx=33, detectionLineOffset=50f },
            },
        },
        new CircuitDef
        {
            id="bahrain", displayName="Bahrain International Circuit", country="Bahrain", city="Sakhir",
            raceLaps=57, lapLengthKm=5.412f, speedRating=0.62f, character="Technical",
            waypoints=_Bahrain, sector1End=15, sector2End=28,
            drsZones = new[]
            {
                new DrsZone { activationWaypointIdx=0,  endWaypointIdx=3,  detectionLineOffset=50f },
                new DrsZone { activationWaypointIdx=14, endWaypointIdx=20, detectionLineOffset=50f },
            },
        },
        new CircuitDef
        {
            id="canada", displayName="Circuit Gilles Villeneuve", country="Canada", city="Montreal",
            raceLaps=70, lapLengthKm=4.361f, speedRating=0.65f, character="Street",
            waypoints=_Canada, sector1End=15, sector2End=26,
            drsZones = new[]
            {
                new DrsZone { activationWaypointIdx=0,  endWaypointIdx=3,  detectionLineOffset=50f },
                new DrsZone { activationWaypointIdx=12, endWaypointIdx=16, detectionLineOffset=50f },
            },
        },
    };

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/FormulaSim/Circuits/Create All Circuits")]
    public static void CreateAllAssets()
    {
        _EnsureFolder();
        int created = 0;
        foreach (var def in All)
        {
            string path = $"Assets/Resources/Circuits/{_Capitalise(def.id)}.asset";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<CircuitData>(path) != null)
            {
                Debug.Log($"[CircuitLibrary] {def.displayName} already exists, skipping.");
                continue;
            }

            var data               = ScriptableObject.CreateInstance<CircuitData>();
            data.circuitId         = def.id;
            data.displayName       = def.displayName;
            data.country           = def.country;
            data.city              = def.city;
            data.raceLaps          = def.raceLaps;
            data.lapLengthKm       = def.lapLengthKm;
            data.isNightRace       = def.isNightRace;
            data.isStreetCircuit   = def.isStreetCircuit;
            data.isAntiClockwise   = def.isAntiClockwise;
            data.weatherVariable   = def.weatherVariable;
            data.hasTunnel         = def.hasTunnel;
            data.hasOverpass       = def.hasOverpass;
            data.waypoints         = def.waypoints;
            data.sector1End        = def.sector1End;
            data.sector2End        = def.sector2End;
            data.averageSpeedRating = def.speedRating;
            data.circuitCharacter  = def.character;
            data.drsZones          = def.drsZones;

            UnityEditor.AssetDatabase.CreateAsset(data, path);
            created++;
            Debug.Log($"[CircuitLibrary] Created {def.displayName} at {path}");
        }

        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[CircuitLibrary] Done — {created} circuit(s) created.");
    }

    static void _EnsureFolder()
    {
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
            UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Circuits"))
            UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "Circuits");
    }

    static string _Capitalise(string s)
        => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];
#endif
}
