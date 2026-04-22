using System;
using UnityEngine;

namespace FormulaSim.Cars
{
    /// <summary>
    /// Simulates F1 ERS: MGU-K (kinetic harvest/deploy) + MGU-H (heat harvest from turbo).
    /// Battery capacity: 4 MJ/lap (FIA regulation).
    /// Deploy limit: 120 kW peak, 160 kW burst.
    /// Harvest under braking (MGU-K) and under turbo load (MGU-H).
    /// </summary>
    public class ERSSystem : MonoBehaviour
    {
        // ── FIA-spec constants ────────────────────────────────────────────────
        const float BATTERY_CAPACITY_KJ  = 4000f;   // 4 MJ = 4000 kJ per lap
        const float MGUK_PEAK_DEPLOY_KW  = 120f;    // kW deployment limit
        const float MGUK_BURST_KW        = 160f;    // kW burst (< 3s)
        const float MGUK_HARVEST_KW      = 120f;    // kW harvest under braking
        const float MGUH_HARVEST_KW      = 45f;     // kW from turbo/exhaust heat
        const float BURST_DURATION_MAX   = 3.0f;    // seconds of burst mode
        const float REGEN_EFFICIENCY     = 0.82f;   // harvesting efficiency
        const float DEPLOY_EFFICIENCY    = 0.90f;   // deployment efficiency

        // ── Deploy modes ─────────────────────────────────────────────────────
        public enum DeployMode
        {
            None       = 0,   // full harvesting, no deployment
            Medium     = 1,   // balanced: harvest > deploy
            Overtake   = 2,   // max deploy, minimal harvest
            Hotlap     = 3,   // full deploy all lap, no harvest
        }

        [Header("Settings")]
        [SerializeField] DeployMode startMode = DeployMode.Medium;
        [SerializeField] bool       autoHarvest = true;

        // ── Public telemetry ─────────────────────────────────────────────────
        public float BatteryKJ       { get; private set; }     // current charge
        public float BatteryPercent  => BatteryKJ / BATTERY_CAPACITY_KJ;
        public float DeployKW        { get; private set; }     // current deploy rate
        public float HarvestKW       { get; private set; }     // current harvest rate
        public float ExtraForceN     { get; private set; }     // force to add to car
        public DeployMode CurrentMode{ get; private set; }
        public bool  IsBursting      { get; private set; }
        public float BurstRemaining  { get; private set; }

        public event Action<DeployMode> OnModeChanged;
        public event Action             OnBatteryEmpty;
        public event Action             OnBatteryFull;

        // ── Private ───────────────────────────────────────────────────────────
        F1CarController car;
        float burstTimer;
        bool  prevBraking;

        void Awake()
        {
            car          = GetComponent<F1CarController>();
            CurrentMode  = startMode;
            BatteryKJ    = BATTERY_CAPACITY_KJ * 0.75f;   // start at 75%
        }

        void FixedUpdate()
        {
            float dt        = Time.fixedDeltaTime;
            float throttle  = car.ThrottleInput;
            float brake     = car.BrakeInput;
            float speedMs   = car.SpeedMs;
            float rpm       = car.RPM;

            // ── MGU-H harvest: always on above idle RPM ────────────────────
            float mguH = 0f;
            if (rpm > 7000f)
                mguH = Mathf.Lerp(0f, MGUH_HARVEST_KW, (rpm - 7000f) / 8000f)
                     * (1f - throttle * 0.4f);   // less harvest under full throttle (turbo used)

            // ── MGU-K harvest: under braking ──────────────────────────────
            float mguK_harvest = 0f;
            if (autoHarvest && brake > 0.15f && speedMs > 10f)
            {
                mguK_harvest = MGUK_HARVEST_KW * brake
                             * Mathf.Clamp01(speedMs / 40f)
                             * REGEN_EFFICIENCY;
            }

            HarvestKW = mguH + mguK_harvest;

            // ── Deploy ─────────────────────────────────────────────────────
            float deployTarget = 0f;
            bool  canDeploy    = BatteryKJ > 10f && throttle > 0.4f && speedMs > 5f;

            if (canDeploy)
            {
                deployTarget = CurrentMode switch
                {
                    DeployMode.None     => 0f,
                    DeployMode.Medium   => MGUK_PEAK_DEPLOY_KW * 0.6f * throttle,
                    DeployMode.Overtake => MGUK_PEAK_DEPLOY_KW * throttle,
                    DeployMode.Hotlap   => MGUK_BURST_KW * throttle,
                    _                   => 0f,
                };
            }

            // Burst mode: short-duration peak
            if (IsBursting)
            {
                burstTimer       -= dt;
                BurstRemaining    = burstTimer;
                if (burstTimer <= 0f) IsBursting = false;
                else deployTarget = MGUK_BURST_KW;
            }

            DeployKW = Mathf.Lerp(DeployKW, deployTarget, dt * 8f);

            // ── Energy accounting ──────────────────────────────────────────
            float netKJ = (HarvestKW - DeployKW / DEPLOY_EFFICIENCY) * dt;
            BatteryKJ   = Mathf.Clamp(BatteryKJ + netKJ, 0f, BATTERY_CAPACITY_KJ);

            if (BatteryKJ <= 0f && DeployKW > 0f) { DeployKW = 0f; OnBatteryEmpty?.Invoke(); }
            if (BatteryKJ >= BATTERY_CAPACITY_KJ - 1f && HarvestKW > 0f) OnBatteryFull?.Invoke();

            // ── Convert deploy power to force ─────────────────────────────
            // Power (kW) → Force (N): F = P / v   (at 0 speed, cap to avoid infinity)
            float v = Mathf.Max(speedMs, 5f);
            ExtraForceN = (DeployKW * 1000f) / v;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetMode(DeployMode mode)
        {
            if (CurrentMode == mode) return;
            CurrentMode = mode;
            OnModeChanged?.Invoke(mode);
        }

        public void CycleMode()
        {
            int next = ((int)CurrentMode + 1) % 4;
            SetMode((DeployMode)next);
        }

        /// <summary>Trigger a 3-second burst (overtake button).</summary>
        public void ActivateBurst()
        {
            if (BatteryKJ < 50f) return;
            IsBursting   = true;
            burstTimer   = BURST_DURATION_MAX;
            BurstRemaining = BURST_DURATION_MAX;
        }

        /// <summary>Called from F1CarController to get additional drive force.</summary>
        public float GetExtraForce() => ExtraForceN;
    }
}
