using UnityEngine;

namespace FormulaSim.Cars
{
    [CreateAssetMenu(menuName = "FormulaSim/Vehicle Config", fileName = "VehicleConfig")]
    public class VehicleConfig : ScriptableObject
    {
        [Header("Engine")]
        public float maxTorque      = 450f;     // Nm
        public float maxRPM         = 15000f;
        public float idleRPM        = 3500f;
        public float maxDriveForce  = 28000f;   // N
        public float maxBrakeForce  = 36000f;   // N
        public float engineBraking  = 0.35f;    // fraction of brake force when off throttle

        [Header("Gearbox")]
        public float[] gearRatios   = { 3.40f, 2.25f, 1.72f, 1.38f, 1.14f, 0.96f, 0.82f, 0.70f };
        public float   finalDrive   = 3.07f;
        public float   shiftUpRPM   = 13800f;
        public float   shiftDownRPM = 8500f;

        [Header("DRS")]
        public float drsDragMultiplier = 0.80f;
        public float drsMinSpeed       = 50f;   // m/s (~180 km/h)

        [Header("Aerodynamics")]
        public float downforceCoeff     = 0.0035f;   // lateral grip += speed² * coeff
        public float dragCoeff          = 0.24f;
        // Base values preserved so CarSetup can scale from the original figures
        public float baseDownforceCoeff = 0.0035f;
        public float baseDragCoeff      = 0.24f;

        [Header("Steering")]
        public float maxSteerAngle         = 28f;   // degrees
        public float steerSpeedSensitivity = 0.70f; // reduces steer at high speed

        [Header("Suspension / Damping")]
        public float linearDamping  = 0.6f;
        public float angularDamping = 3.0f;
        public float mass           = 740f;

        [Header("Tire Compounds")]
        public TireCompoundData soft   = new TireCompoundData { grip = 1.15f, wear = 1.60f, heatRate = 1.40f };
        public TireCompoundData medium = new TireCompoundData { grip = 1.00f, wear = 1.00f, heatRate = 1.00f };
        public TireCompoundData hard   = new TireCompoundData { grip = 0.88f, wear = 0.65f, heatRate = 0.75f };
        public TireCompoundData inter  = new TireCompoundData { grip = 0.80f, wear = 0.55f, heatRate = 0.60f };
        public TireCompoundData wet    = new TireCompoundData { grip = 0.72f, wear = 0.40f, heatRate = 0.45f };

        [Header("AI Difficulty Multipliers")]
        public float aiNovice     = 0.70f;
        public float aiAmateur    = 0.82f;
        public float aiPro        = 0.91f;
        public float aiElite      = 0.97f;
        public float aiLegendary  = 1.00f;

        public TireCompoundData GetCompound(TireCompound c) => c switch
        {
            TireCompound.Soft   => soft,
            TireCompound.Medium => medium,
            TireCompound.Hard   => hard,
            TireCompound.Inter  => inter,
            TireCompound.Wet    => wet,
            _                   => medium,
        };
    }

    [System.Serializable]
    public class TireCompoundData
    {
        public float grip;
        public float wear;
        public float heatRate;
    }

    public enum TireCompound { Soft, Medium, Hard, Inter, Wet }
}
