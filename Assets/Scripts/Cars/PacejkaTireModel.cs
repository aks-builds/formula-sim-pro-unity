using UnityEngine;

namespace FormulaSim.Cars
{
    /// <summary>
    /// Pacejka 'Magic Formula' tire model.
    /// Computes lateral (cornering) and longitudinal (traction/braking) forces
    /// from slip angle and slip ratio respectively, then combines via traction circle.
    ///
    /// Reference: Pacejka H.B., "Tire and Vehicle Dynamics", 3rd ed.
    /// Magic Formula: F = D * sin(C * atan(B*x - E*(B*x - atan(B*x))))
    /// where x = slip angle (lateral) or slip ratio (longitudinal)
    /// </summary>
    [CreateAssetMenu(menuName = "FormulaSim/Pacejka Tire Params", fileName = "PacejkaParams_Soft")]
    public class PacejkaTireModel : ScriptableObject
    {
        [Header("Lateral (Cornering Force)")]
        [Tooltip("Shape factor — controls peak sharpness")]
        public float Cy = 1.30f;
        [Tooltip("Peak factor — scales max lateral force")]
        public float Dy = 1.00f;
        [Tooltip("Stiffness factor — initial slope")]
        public float By = 10.0f;
        [Tooltip("Curvature — controls post-peak falloff")]
        public float Ey = -0.5f;

        [Header("Longitudinal (Drive/Brake Force)")]
        public float Cx = 1.65f;
        public float Dx = 1.10f;
        public float Bx = 11.0f;
        public float Ex = 0.30f;

        [Header("Load Sensitivity")]
        [Tooltip("Normal force at which Dy/Dx peak (N)")]
        public float nominalLoad = 3000f;
        [Tooltip("Friction reduction at high load (degrades with load squared)")]
        public float loadSensitivity = 0.0000015f;

        [Header("Temperature Sensitivity")]
        [Tooltip("Optimal temp °C — peak friction")]
        public float optimalTemp = 90f;
        [Tooltip("Width of temp window around optimal")]
        public float tempWindow  = 25f;

        // ── Magic Formula ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns normalized lateral force coefficient (0..1+) for a given slip angle (radians).
        /// Multiply result by normalForce to get force in Newtons.
        /// </summary>
        public float LateralForceCoeff(float slipAngleRad, float normalForceN, float tempC)
        {
            float x   = slipAngleRad;
            float mu  = LoadAdjustedFriction(Dy, normalForceN) * TempMultiplier(tempC);
            return mu * MagicFormula(Cy, By, Ey, x);
        }

        /// <summary>
        /// Returns normalized longitudinal force coefficient for a given slip ratio (0..1).
        /// </summary>
        public float LongitudinalForceCoeff(float slipRatio, float normalForceN, float tempC)
        {
            float x  = slipRatio;
            float mu = LoadAdjustedFriction(Dx, normalForceN) * TempMultiplier(tempC);
            return mu * MagicFormula(Cx, Bx, Ex, x);
        }

        /// <summary>
        /// Combined traction circle: longitudinal + lateral sharing traction budget.
        /// Returns (longCoeff, latCoeff) scaled so resultant ≤ peak friction.
        /// </summary>
        public (float longCoeff, float latCoeff) CombinedForce(
            float slipRatio, float slipAngleRad, float normalForceN, float tempC)
        {
            float fx = LongitudinalForceCoeff(slipRatio,   normalForceN, tempC);
            float fy = LateralForceCoeff(slipAngleRad,     normalForceN, tempC);
            float combined = Mathf.Sqrt(fx * fx + fy * fy);
            float mu       = LoadAdjustedFriction(Mathf.Max(Dx, Dy), normalForceN) * TempMultiplier(tempC);
            if (combined > mu)
            {
                float scale = mu / combined;
                fx *= scale;
                fy *= scale;
            }
            return (fx, fy);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static float MagicFormula(float C, float B, float E, float x)
        {
            float Bx  = B * x;
            return Mathf.Sin(C * Mathf.Atan(Bx - E * (Bx - Mathf.Atan(Bx))));
        }

        float LoadAdjustedFriction(float baseMu, float normalForceN)
        {
            // Friction coefficient degrades slightly at high vertical load
            float loadRatio = normalForceN / nominalLoad;
            return baseMu * (1f - loadSensitivity * normalForceN * Mathf.Max(0f, loadRatio - 1f));
        }

        float TempMultiplier(float tempC)
        {
            // Bell curve: peak at optimalTemp, degrades outside tempWindow
            float diff = tempC - optimalTemp;
            return Mathf.Clamp(Mathf.Exp(-0.5f * (diff / tempWindow) * (diff / tempWindow)), 0.35f, 1.0f);
        }
    }

    // ── Runtime slip calculator ───────────────────────────────────────────────

    public static class SlipCalculator
    {
        /// <summary>
        /// Compute slip angle (radians) for a tire given car velocity and tire heading.
        /// </summary>
        public static float SlipAngle(Vector2 wheelVelocity, Vector2 wheelForward)
        {
            if (wheelVelocity.magnitude < 0.5f) return 0f;
            float vx = Vector2.Dot(wheelVelocity, wheelForward);
            float vy = Vector2.Dot(wheelVelocity, new Vector2(-wheelForward.y, wheelForward.x));
            return Mathf.Atan2(vy, Mathf.Abs(vx));
        }

        /// <summary>
        /// Compute longitudinal slip ratio (0=free-rolling, 1=full spin/lockup).
        /// </summary>
        public static float SlipRatio(float wheelSpeedMs, float vehicleSpeedMs, bool isDriving)
        {
            float vRef = Mathf.Max(vehicleSpeedMs, 0.5f);
            if (isDriving)
                return Mathf.Clamp01((wheelSpeedMs - vRef) / vRef);
            else
                return Mathf.Clamp01((vRef - wheelSpeedMs) / vRef);
        }
    }
}
