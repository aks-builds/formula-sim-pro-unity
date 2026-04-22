using UnityEngine;
using FormulaSim.Core;

namespace FormulaSim.Cars
{
    /// <summary>
    /// Low-level waypoint follower. Computes throttle/brake/steer toward the
    /// next waypoint and exposes them as properties. AIBehavior reads these,
    /// applies high-level behavior modifiers, then calls car.SetAIInputs().
    /// </summary>
    public class AIDriver : MonoBehaviour
    {
        [SerializeField] float lookAheadDist = 200f;
        [SerializeField] float reactionNoise = 0.04f;

        F1CarController car;
        Vector2[]       waypoints;
        int             waypointIdx;

        // Base inputs computed this frame (read by AIBehavior)
        public float BaseThrottle { get; private set; }
        public float BaseBrake    { get; private set; }
        public float BaseSteer    { get; private set; }

        // Effective difficulty comes from global GameSettings
        float Difficulty => GameSettings.Instance != null
            ? GameSettings.Instance.AIDifficultySkill
            : 0.85f;

        public void Init(Vector2[] trackWaypoints)
        {
            waypoints   = trackWaypoints;
            waypointIdx = 0;
            car         = GetComponent<F1CarController>();
        }

        /// <summary>
        /// Compute base inputs for this physics step.
        /// Called by AIBehavior before it applies behavior mods.
        /// </summary>
        public void ComputeFrame()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            _AdvanceWaypoint();
            _ComputeInputs();
        }

        void _AdvanceWaypoint()
        {
            Vector2 pos    = transform.position;
            Vector2 target = waypoints[waypointIdx];
            if (Vector2.Distance(pos, target) < 30f)
                waypointIdx = (waypointIdx + 1) % waypoints.Length;
        }

        void _ComputeInputs()
        {
            Vector2 pos    = transform.position;
            Vector2 target = waypoints[waypointIdx];

            Vector2 toTarget = (target - pos).normalized;
            float   dot      = Vector2.Dot(transform.right, toTarget);
            BaseSteer        = Mathf.Clamp(dot * 2f, -1f, 1f);

            float sharpness  = _CornerSharpnessAhead(pos);
            float targetThrot = (1f - sharpness * 0.85f) * Difficulty;
            BaseThrottle     = Mathf.Lerp(BaseThrottle, targetThrot, Time.fixedDeltaTime * 4f);
            BaseBrake        = sharpness > 0.55f ? (sharpness - 0.55f) * 2f * Difficulty : 0f;
        }

        float _CornerSharpnessAhead(Vector2 fromPos)
        {
            int   lookahead = 5;
            float maxAngle  = 0f;
            int   start     = waypointIdx;
            for (int i = 0; i < lookahead && i < waypoints.Length - 1; i++)
            {
                int a = (start + i)     % waypoints.Length;
                int b = (start + i + 1) % waypoints.Length;
                int c = (start + i + 2) % waypoints.Length;

                Vector2 ab  = (waypoints[b] - waypoints[a]).normalized;
                Vector2 bc  = (waypoints[c] - waypoints[b]).normalized;
                float angle = Vector2.Angle(ab, bc);
                float dist  = Mathf.Lerp(1f, 0.3f, (float)i / lookahead);
                maxAngle    = Mathf.Max(maxAngle, angle * dist);
            }
            return Mathf.Clamp01(maxAngle / 90f);
        }

        // Expose noise for AIBehavior to add when calling car.SetAIInputs
        public float NoiseAmount => (1f - Difficulty) * reactionNoise;
    }
}
