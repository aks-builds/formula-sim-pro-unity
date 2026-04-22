using UnityEngine;
using FormulaSim.Cars;
using FormulaSim.Race;
using FormulaSim.Core;

namespace FormulaSim.AI
{
    /// <summary>
    /// High-level behavior state machine layered on top of AIDriver waypoint following.
    /// This is the single authority that calls car.SetAIInputs() each physics step.
    /// States: Racing → Attacking → Overtaking → Defending → Mistake → Recovering → Pitting.
    /// </summary>
    [RequireComponent(typeof(AIDriver))]
    [RequireComponent(typeof(F1CarController))]
    public class AIBehavior : MonoBehaviour
    {
        [Header("Personality (overridden by GameSettings difficulty if UseGlobalDifficulty)")]
        [Range(0f,1f)] public float aggression        = 0.6f;
        [Range(0f,1f)] public float consistency       = 0.8f;
        [Range(0f,1f)] public float racecraft         = 0.7f;
        [Range(0f,1f)] public float wetWeatherAbility = 0.7f;
        public bool useGlobalDifficulty = true;

        [Header("Overtake")]
        public float overtakeGapThreshold = 0.8f;
        public float drsOvertakeBoostGap  = 0.3f;
        public float overtakeAbortGap     = 2.0f;

        [Header("Mistakes")]
        public float mistakeProbPerLap = 0.05f;
        public float mistakeSeverity   = 0.3f;

        public enum State { Racing, Attacking, Overtaking, Defending, Mistake, Recovering, Pitting }
        public State CurrentState { get; private set; } = State.Racing;

        AIDriver        driver;
        F1CarController car;
        RaceManager     raceManager;

        float stateTimer;
        float defenseTimer;
        bool  movingLeft;

        float behaviorThrottleMod = 1f;
        float behaviorBrakeMod    = 0f;
        float behaviorSteerMod    = 0f;

        // Per-driver personality (applied at start based on global difficulty)
        float _consistency;
        float _aggression;

        void Awake()
        {
            driver      = GetComponent<AIDriver>();
            car         = GetComponent<F1CarController>();
            raceManager = FindObjectOfType<RaceManager>();
        }

        void Start()
        {
            // Apply global difficulty on top of per-driver personality
            if (useGlobalDifficulty && GameSettings.Instance != null)
            {
                _consistency = Mathf.Lerp(consistency,  GameSettings.Instance.AIConsistency, 0.6f);
                _aggression  = Mathf.Lerp(aggression,   GameSettings.Instance.AIAggression,  0.6f);
            }
            else
            {
                _consistency = consistency;
                _aggression  = aggression;
            }
        }

        void FixedUpdate()
        {
            stateTimer += Time.fixedDeltaTime;

            // 1. Get base driving inputs from the waypoint follower
            driver.ComputeFrame();

            // 2. Update high-level behavior state
            var entry = raceManager?.Cars.Find(e => e.Controller == car);
            if (entry != null)
            {
                float    gap  = entry.GapToAhead;
                bool     drs  = car.DrsActive;
                RaceFlag flag = GameManager.Instance?.CurrentFlag ?? RaceFlag.Green;
                _UpdateStateMachine(entry, gap, drs, flag);
            }

            // 3. Apply behavior mods on top of base inputs → send to car
            _ApplyBehaviorModifiers();
        }

        void _UpdateStateMachine(RaceManager.CarEntry entry, float gap, bool drs, RaceFlag flag)
        {
            switch (CurrentState)
            {
                case State.Racing:
                    behaviorThrottleMod = 1f;
                    behaviorBrakeMod    = 0f;
                    behaviorSteerMod    = 0f;

                    if (flag == RaceFlag.SafetyCar || flag == RaceFlag.VirtualSafetyCar)
                    {
                        behaviorThrottleMod = 0.45f;
                        break;
                    }
                    if (gap > 0f && gap < overtakeGapThreshold && entry.RacePosition > 1)
                    { _TransitionTo(State.Attacking); break; }

                    if (entry.GapToBehind < 0.5f && racecraft > 0.5f)
                    { _TransitionTo(State.Defending); break; }

                    if (_RollMistake()) _TransitionTo(State.Mistake);
                    break;

                case State.Attacking:
                    behaviorThrottleMod = Mathf.Lerp(1f, 1.06f, _aggression);
                    if (gap > overtakeAbortGap || stateTimer > 8f)
                    { _TransitionTo(State.Racing); break; }
                    if ((drs && gap < drsOvertakeBoostGap) || (!drs && gap < 0.3f && _aggression > 0.65f))
                    {
                        movingLeft = Random.value > 0.5f;
                        _TransitionTo(State.Overtaking);
                    }
                    break;

                case State.Overtaking:
                    behaviorSteerMod    = (movingLeft ? -1f : 1f) * 0.3f;
                    behaviorThrottleMod = 1.08f;
                    if (stateTimer > 3f || gap > 1.5f) _TransitionTo(State.Racing);
                    break;

                case State.Defending:
                    defenseTimer += Time.fixedDeltaTime;
                    behaviorSteerMod    = -behaviorSteerMod * 0.4f * racecraft;
                    behaviorThrottleMod = 1f;
                    if (entry.GapToBehind > 1.2f || defenseTimer > 5f)
                    { defenseTimer = 0f; _TransitionTo(State.Racing); }
                    break;

                case State.Mistake:
                    float sev           = mistakeSeverity * (1f - _consistency);
                    behaviorThrottleMod = Mathf.Lerp(1f, 0.1f, sev);
                    behaviorSteerMod    = Mathf.Sin(stateTimer * 15f) * sev * 0.5f;
                    if (stateTimer > Mathf.Lerp(0.5f, 2.5f, sev)) _TransitionTo(State.Recovering);
                    break;

                case State.Recovering:
                    behaviorThrottleMod = Mathf.Lerp(behaviorThrottleMod, 1f, Time.fixedDeltaTime * 3f);
                    behaviorSteerMod    = Mathf.Lerp(behaviorSteerMod,    0f, Time.fixedDeltaTime * 4f);
                    if (stateTimer > 3f) _TransitionTo(State.Racing);
                    break;

                case State.Pitting:
                    behaviorThrottleMod = 0.3f;
                    behaviorBrakeMod    = 0f;
                    behaviorSteerMod    = 0f;
                    break;
            }
        }

        void _ApplyBehaviorModifiers()
        {
            float noise = driver.NoiseAmount;

            // Base inputs from waypoint follower
            float t = driver.BaseThrottle;
            float b = driver.BaseBrake;
            float s = driver.BaseSteer;

            // Apply behavior multipliers
            t *= behaviorThrottleMod;
            b  = Mathf.Clamp01(b + behaviorBrakeMod);
            s  = Mathf.Clamp(s + behaviorSteerMod, -1f, 1f);

            // Add per-driver reaction noise
            t  = Mathf.Clamp01(t + Random.Range(-noise, noise));
            s  = Mathf.Clamp(s + Random.Range(-noise * 0.5f, noise * 0.5f), -1f, 1f);

            car.SetAIInputs(t, b, s);
        }

        void _TransitionTo(State next) { CurrentState = next; stateTimer = 0f; }

        bool _RollMistake()
        {
            float probPerFrame = mistakeProbPerLap / (90f / Time.fixedDeltaTime);
            float mod          = probPerFrame * (1f - _consistency) * (1f + car.SpeedMs / 80f * 0.3f);
            return Random.value < mod;
        }

        public bool ShouldPitNow(TireManager.TireSet tires, Weather.WeatherState weather, int lapsRemaining)
        {
            var advice = TireManager.GetPitAdvice(tires, weather, lapsRemaining);
            if (advice == null) return false;
            if (advice.Urgency == "CRITICAL") return true;
            if (advice.Urgency == "HIGH" && _aggression < 0.5f) return true;
            return false;
        }
    }
}
