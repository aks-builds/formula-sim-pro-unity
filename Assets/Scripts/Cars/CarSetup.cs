using System;
using UnityEngine;
using Newtonsoft.Json;

namespace FormulaSim.Cars
{
    public enum SuspensionSetting { Soft, Medium, Stiff }

    /// <summary>
    /// Pre-race car setup. Exposes sliders for wing, suspension, brake bias
    /// and differential. Computes physics multipliers applied at race start.
    /// Saved per-circuit to PlayerPrefs.
    /// </summary>
    [Serializable]
    public class CarSetupData
    {
        [Range(0, 11)] public int   frontWing    = 5;   // 0 = low drag, 11 = max downforce
        [Range(0, 11)] public int   rearWing     = 5;
        public SuspensionSetting    suspension   = SuspensionSetting.Medium;
        [Range(50, 70)] public float brakeBias   = 57f;  // % front braking
        [Range(0, 100)] public int   differential = 50;  // % on-throttle lock

        // ── Computed multipliers ──────────────────────────────────────────────

        /// <summary>Total aerodynamic downforce scale.</summary>
        public float DownforceMult => 1f + (frontWing + rearWing) * 0.022f;

        /// <summary>Aerodynamic drag scale (high wing = more drag).</summary>
        public float DragMult => 1f + (frontWing + rearWing) * 0.014f;

        /// <summary>Mechanical grip from suspension tuning.</summary>
        public float MechanicalGripMult => suspension switch
        {
            SuspensionSetting.Soft  => 1.06f,
            SuspensionSetting.Stiff => 0.96f,
            _                       => 1.00f,
        };

        /// <summary>Tire wear rate from suspension stiffness.</summary>
        public float TireWearMult => suspension switch
        {
            SuspensionSetting.Soft  => 1.14f,
            SuspensionSetting.Stiff => 0.86f,
            _                       => 1.00f,
        };

        /// <summary>
        /// ABS/lockup sensitivity from brake bias.
        /// High front bias = front locks first.
        /// </summary>
        public float FrontLockupBias => Mathf.Clamp01((brakeBias - 50f) / 20f);

        /// <summary>On-throttle traction from differential lock.</summary>
        public float TractionMult => 1f + (differential - 50) * 0.003f;

        /// <summary>Oversteer tendency from differential (open diff = rotate more).</summary>
        public float OversteerTendency => (50 - differential) * 0.008f;

        // ── Estimation helpers for UI display ─────────────────────────────────

        /// <summary>Estimated top speed relative to baseline (100 = standard).</summary>
        public int TopSpeedIndex => Mathf.RoundToInt(100 - (frontWing + rearWing) * 1.8f);

        /// <summary>Estimated cornering grip index (100 = standard).</summary>
        public int CorneringIndex => Mathf.RoundToInt(100 + (frontWing + rearWing) * 1.5f
            + (suspension == SuspensionSetting.Soft ? 6 : suspension == SuspensionSetting.Stiff ? -4 : 0));

        /// <summary>Estimated tire wear index (100 = standard).</summary>
        public int TireWearIndex => Mathf.RoundToInt(TireWearMult * 100f);

        /// <summary>Estimated traction index.</summary>
        public int TractionIndex => Mathf.RoundToInt(TractionMult * 100f);

        // ── Presets ────────────────────────────────────────────────────────────

        public static CarSetupData Balanced() => new();

        public static CarSetupData MonacoSetup() => new()
        {
            frontWing = 11, rearWing = 11, suspension = SuspensionSetting.Soft,
            brakeBias = 55f, differential = 65
        };

        public static CarSetupData MonzaSetup() => new()
        {
            frontWing = 1, rearWing = 1, suspension = SuspensionSetting.Stiff,
            brakeBias = 60f, differential = 40
        };

        public static CarSetupData WetSetup() => new()
        {
            frontWing = 9, rearWing = 9, suspension = SuspensionSetting.Soft,
            brakeBias = 54f, differential = 55
        };
    }

    /// <summary>
    /// MonoBehaviour that holds the active car setup and applies it to VehicleConfig.
    /// Attach alongside F1CarController.
    /// </summary>
    public class CarSetup : MonoBehaviour
    {
        [SerializeField] VehicleConfig config;

        public CarSetupData Setup { get; private set; } = new();

        const string PREF_PREFIX = "setup_";

        void Start()
        {
            string circuitId = PlayerPrefs.GetString("selected_circuit", "balanced");
            Load(circuitId);
            Apply();
        }

        public void Apply()
        {
            if (config == null) return;

            // Scale downforce and drag coefficients
            config.downforceCoeff = config.baseDownforceCoeff
                * Setup.DownforceMult
                * Setup.MechanicalGripMult;

            config.dragCoeff = config.baseDragCoeff * Setup.DragMult;
        }

        public void ApplySetup(CarSetupData data)
        {
            Setup = data;
            Apply();
        }

        public void Save(string circuitId)
        {
            PlayerPrefs.SetString(PREF_PREFIX + circuitId, JsonConvert.SerializeObject(Setup));
            PlayerPrefs.Save();
        }

        public void Load(string circuitId)
        {
            string json = PlayerPrefs.GetString(PREF_PREFIX + circuitId, null);
            Setup = string.IsNullOrEmpty(json)
                ? new CarSetupData()
                : JsonConvert.DeserializeObject<CarSetupData>(json) ?? new CarSetupData();
        }
    }
}
