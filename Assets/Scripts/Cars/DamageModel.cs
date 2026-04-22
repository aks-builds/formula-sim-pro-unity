using UnityEngine;
using System.Linq;

namespace FormulaSim.Cars
{
    /// <summary>
    /// Per-component damage model. Tracks front wing, rear wing, suspension
    /// and tire punctures. Provides physics multipliers consumed by F1CarController.
    /// Attach alongside F1CarController — it reads OnCollisionEnter2D from that component.
    /// </summary>
    public class DamageModel : MonoBehaviour
    {
        [Header("Thresholds")]
        [SerializeField] float minImpactForce     = 4f;    // ignore minor contact
        [SerializeField] float punctureThreshold  = 22f;   // impact force to risk puncture
        [SerializeField] float punctureChancePerG = 0.015f;

        // ── Per-component damage levels (0 = perfect, 1 = destroyed) ──────────
        public float FrontWingDamage   { get; private set; }
        public float RearWingDamage    { get; private set; }
        public float SuspensionDamage  { get; private set; }
        public bool[] TirePunctured    { get; private set; } = new bool[4]; // FL FR RL RR

        // ── Visual state ───────────────────────────────────────────────────────
        public bool FrontWingLost  => FrontWingDamage >= 0.85f;
        public bool HasPuncture    => TirePunctured.Any(p => p);

        // ── Physics multipliers (read by F1CarController every FixedUpdate) ────
        /// <summary>Combined downforce multiplier. Max 45% loss from front wing.</summary>
        public float DownforceMultiplier =>
            Mathf.Clamp01((1f - FrontWingDamage * 0.45f) * (1f - RearWingDamage * 0.25f));

        /// <summary>Grip multiplier. Punctured tire = near-undriveable.</summary>
        public float GripMultiplier =>
            HasPuncture ? 0.18f : Mathf.Clamp01(1f - SuspensionDamage * 0.15f);

        /// <summary>Drag multiplier. Front wing debris increases drag.</summary>
        public float DragMultiplier => 1f + (FrontWingLost ? 0.18f : 0f);

        // ── Events ─────────────────────────────────────────────────────────────
        public event System.Action<int>   OnTirePunctured;   // tire index (0-3)
        public event System.Action<float> OnFrontWingDamaged;
        public event System.Action        OnFrontWingLost;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by F1CarController.OnCollisionEnter2D.</summary>
        public void ApplyCollision(float impactForce, Vector2 collisionNormal)
        {
            if (impactForce < minImpactForce) return;

            Vector2 carForward = transform.up;
            float   frontDot   = Vector2.Dot(collisionNormal, -carForward);   // +1 = hit from front
            float   rearDot    = Vector2.Dot(collisionNormal,  carForward);   // +1 = hit from rear
            float   sideDot    = 1f - Mathf.Abs(frontDot) - Mathf.Abs(rearDot);

            float normalized = impactForce / 60f;   // normalise around typical crash speed

            if (frontDot > 0.5f)
            {
                float prev = FrontWingDamage;
                FrontWingDamage = Mathf.Clamp01(FrontWingDamage + normalized * 0.55f);
                OnFrontWingDamaged?.Invoke(FrontWingDamage);
                if (prev < 0.85f && FrontWingDamage >= 0.85f) OnFrontWingLost?.Invoke();
            }
            else if (rearDot > 0.5f)
            {
                RearWingDamage = Mathf.Clamp01(RearWingDamage + normalized * 0.35f);
            }
            else
            {
                SuspensionDamage = Mathf.Clamp01(SuspensionDamage + normalized * 0.25f);
            }

            // Tire puncture check
            if (impactForce > punctureThreshold)
            {
                float chance = (impactForce - punctureThreshold) * punctureChancePerG;
                // Side impact: affects tires on impact side
                bool leftSide = Vector2.Dot(collisionNormal, transform.right) > 0;
                int[] candidates = sideDot > 0.4f
                    ? (leftSide ? new[] { 0, 2 } : new[] { 1, 3 })   // FL/RL or FR/RR
                    : new[] { 0, 1, 2, 3 };

                foreach (int idx in candidates)
                {
                    if (!TirePunctured[idx] && Random.value < chance * 0.3f)
                    {
                        TirePunctured[idx] = true;
                        OnTirePunctured?.Invoke(idx);
                    }
                }
            }
        }

        public void RepairAll()
        {
            FrontWingDamage  = 0f;
            RearWingDamage   = 0f;
            SuspensionDamage = 0f;
            for (int i = 0; i < 4; i++) TirePunctured[i] = false;
        }

        // String labels for HUD
        public string FrontWingStatus =>
            FrontWingLost ? "LOST" :
            FrontWingDamage > 0.5f ? "DAMAGED" :
            FrontWingDamage > 0.15f ? "MINOR" : "OK";

        public string GetTireStatus(int idx) =>
            idx < 0 || idx > 3 ? "?" :
            TirePunctured[idx] ? "PUNCTURE" : "OK";
    }
}
