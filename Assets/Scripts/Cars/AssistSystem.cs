using UnityEngine;

namespace FormulaSim.Cars
{
    /// <summary>
    /// Driver assist systems: Traction Control, ABS, Steering Assist.
    /// Each can be independently enabled. Designed so assists degrade gracefully —
    /// they never feel like invincibility, just a safety net.
    /// </summary>
    public class AssistSystem : MonoBehaviour
    {
        [Header("Traction Control")]
        [Tooltip("Clamps throttle when rear wheels exceed slip ratio threshold")]
        public bool  tcEnabled    = true;
        [Range(0f,1f)] public float tcSlipThreshold  = 0.20f;  // slip ratio that triggers TC
        [Range(0f,1f)] public float tcIntervention   = 0.70f;  // 0=light cut, 1=full cut

        [Header("ABS")]
        [Tooltip("Modulates brake pressure to prevent wheel lockup")]
        public bool  absEnabled   = true;
        [Range(0f,1f)] public float absLockThreshold = 0.25f;  // slip ratio that triggers ABS
        [Range(0f,1f)] public float absMinBrake      = 0.15f;  // minimum brake pressure during ABS

        [Header("Steering Assist")]
        [Tooltip("Adds corrective counter-steer to reduce oversteer")]
        public bool  steerAssistEnabled  = true;
        [Range(0f,1f)] public float steerAssistStrength = 0.45f;

        [Header("Stability Control")]
        public bool  stabilityEnabled = true;
        [Range(0f,1f)] public float stabilityThreshold = 0.5f; // yaw rate threshold

        // ── Output (applied in F1CarController) ──────────────────────────────
        public float ThrottleOut  { get; private set; }
        public float BrakeOut     { get; private set; }
        public float SteerOut     { get; private set; }

        // Feedback telemetry
        public bool  TCActive     { get; private set; }
        public bool  ABSActive    { get; private set; }
        public bool  SCActive     { get; private set; }

        // ── Internal ──────────────────────────────────────────────────────────
        Rigidbody2D rb;
        float       tcCutTimer;
        float       absModTimer;
        float       prevYawRate;

        void Awake() => rb = GetComponent<Rigidbody2D>();

        /// <summary>
        /// Call each FixedUpdate before applying inputs to Rigidbody.
        /// Returns corrected throttle, brake, steer.
        /// </summary>
        public void Process(
            float throttleIn, float brakeIn, float steerIn,
            float rearSlipRatio, float frontSlipRatio,
            float slipAngleRad, float speedMs, float dt)
        {
            float throttle = throttleIn;
            float brake    = brakeIn;
            float steer    = steerIn;

            // ── Traction Control ──────────────────────────────────────────────
            TCActive = false;
            if (tcEnabled && rearSlipRatio > tcSlipThreshold && speedMs > 2f)
            {
                TCActive = true;
                float excess    = (rearSlipRatio - tcSlipThreshold) / tcSlipThreshold;
                float cut       = Mathf.Clamp01(excess * tcIntervention);
                throttle        = Mathf.Lerp(throttle, throttle * (1f - cut), dt * 20f);
                tcCutTimer      = 0.12f;
            }
            else if (tcCutTimer > 0f)
            {
                tcCutTimer     -= dt;
                // Gradual throttle restore
                throttle        = Mathf.Min(throttleIn, throttle + dt * 3f);
            }

            // ── ABS ───────────────────────────────────────────────────────────
            ABSActive = false;
            if (absEnabled && frontSlipRatio > absLockThreshold && brakeIn > 0.1f && speedMs > 5f)
            {
                ABSActive = true;
                // Pulse brakes to maintain steering authority
                absModTimer += dt;
                float pulse  = Mathf.Sin(absModTimer * 25f) * 0.5f + 0.5f;   // 25 Hz ABS cycle
                brake        = Mathf.Lerp(absMinBrake, brakeIn, pulse);
            }
            else absModTimer = 0f;

            // ── Steering Assist ────────────────────────────────────────────────
            if (steerAssistEnabled && speedMs > 10f)
            {
                // Detect oversteer: large slip angle at rear
                float oversteering = Mathf.Clamp01(Mathf.Abs(slipAngleRad) / 0.35f - 0.5f);
                // Counter-steer toward slide direction
                float correction   = -Mathf.Sign(slipAngleRad) * oversteering * steerAssistStrength;
                steer              = Mathf.Clamp(steer + correction, -1f, 1f);
            }

            // ── Stability Control ──────────────────────────────────────────────
            SCActive = false;
            if (stabilityEnabled && speedMs > 15f)
            {
                float yawRate       = rb.angularVelocity * Mathf.Deg2Rad;
                float yawAccel      = (yawRate - prevYawRate) / dt;
                prevYawRate         = yawRate;
                float yawMagnitude  = Mathf.Abs(yawRate);
                if (yawMagnitude > stabilityThreshold && brakeIn < 0.1f)
                {
                    SCActive  = true;
                    // Reduce throttle proportionally to yaw excess
                    float excess = (yawMagnitude - stabilityThreshold) / stabilityThreshold;
                    throttle   *= Mathf.Clamp01(1f - excess * 0.6f);
                }
            }

            ThrottleOut = Mathf.Clamp01(throttle);
            BrakeOut    = Mathf.Clamp01(brake);
            SteerOut    = Mathf.Clamp(steer, -1f, 1f);
        }
    }
}
