using System;
using System.Collections;
using UnityEngine;
using FormulaSim.Audio;
using FormulaSim.Cars;

namespace FormulaSim.Gameplay
{
    public class PitStopGame : MonoBehaviour
    {
        public static PitStopGame Instance { get; private set; }

        public enum StepId { JackUp, WheelFL, WheelFR, WheelRL, WheelRR, JackDown, Lollipop }

        [Serializable]
        public struct StepDef
        {
            public StepId  id;
            public string  label;
            public float   duration;
            public float   idealPoint;   // 0-1 fraction of duration for perfect tap
            public float   perfectWindow;
            public float   goodWindow;
        }

        public static readonly StepDef[] Steps =
        {
            new() { id=StepId.JackUp,   label="JACK UP",      duration=0.6f, idealPoint=0.65f, perfectWindow=0.08f, goodWindow=0.18f },
            new() { id=StepId.WheelFL,  label="FRONT LEFT",   duration=0.8f, idealPoint=0.65f, perfectWindow=0.09f, goodWindow=0.18f },
            new() { id=StepId.WheelFR,  label="FRONT RIGHT",  duration=0.8f, idealPoint=0.65f, perfectWindow=0.09f, goodWindow=0.18f },
            new() { id=StepId.WheelRL,  label="REAR LEFT",    duration=0.8f, idealPoint=0.65f, perfectWindow=0.09f, goodWindow=0.18f },
            new() { id=StepId.WheelRR,  label="REAR RIGHT",   duration=0.8f, idealPoint=0.65f, perfectWindow=0.09f, goodWindow=0.18f },
            new() { id=StepId.JackDown, label="JACK DOWN",    duration=0.5f, idealPoint=0.65f, perfectWindow=0.07f, goodWindow=0.14f },
            new() { id=StepId.Lollipop, label="GO!",          duration=0.4f, idealPoint=0.70f, perfectWindow=0.06f, goodWindow=0.12f },
        };

        const float BASE_TIME      = 4.5f;
        const float PERFECT_SAVE   = 0.35f;
        const float GOOD_SAVE      = 0.15f;
        const float MISS_PENALTY   = 0.50f;

        public bool  IsActive        { get; private set; }
        public int   CurrentStepIdx  { get; private set; }
        public float StepProgress    { get; private set; }  // 0-1

        public event Action<StepId, string> OnStepBegin;    // (id, label)
        public event Action<string>         OnStepResult;   // "perfect"/"good"/"early"/"miss"
        public event Action<float, string>  OnComplete;     // (stopTimeSecs, gradeName)

        float stopTime;
        float stepTimer;
        bool  hitThisStep;
        AudioManager audio;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            audio = FindObjectOfType<AudioManager>();
        }

        public void StartStop(Action<float, string> onDone = null)
        {
            if (IsActive) return;
            IsActive       = true;
            CurrentStepIdx = 0;
            stopTime       = BASE_TIME;
            stepTimer      = 0f;
            hitThisStep    = false;
            if (onDone != null) OnComplete += onDone;
            StartCoroutine(_RunSequence());
        }

        IEnumerator _RunSequence()
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                CurrentStepIdx = i;
                stepTimer      = 0f;
                hitThisStep    = false;

                var step = Steps[i];
                OnStepBegin?.Invoke(step.id, step.label);
                _PlayStepAudio(step.id, false);

                while (stepTimer < step.duration)
                {
                    stepTimer   += Time.deltaTime;
                    StepProgress = stepTimer / step.duration;
                    yield return null;
                }

                if (!hitThisStep)
                {
                    stopTime += MISS_PENALTY;
                    OnStepResult?.Invoke("miss");
                }
            }

            _Finish();
        }

        public void OnTap()
        {
            if (!IsActive || hitThisStep) return;
            var step  = Steps[CurrentStepIdx];
            float prog = StepProgress;
            float dev  = Mathf.Abs(prog - step.idealPoint);

            string result;
            if      (dev <= step.perfectWindow * 0.5f) { result = "perfect"; stopTime -= PERFECT_SAVE; }
            else if (dev <= step.goodWindow)            { result = "good";    stopTime -= GOOD_SAVE; }
            else                                        { result = "early";   stopTime -= GOOD_SAVE * 0.4f; }

            hitThisStep = true;
            _PlayStepAudio(Steps[CurrentStepIdx].id, true);
            OnStepResult?.Invoke(result);
        }

        void _Finish()
        {
            IsActive  = false;
            stopTime  = Mathf.Max(2.0f, stopTime);
            string grade = stopTime <= 2.30f ? "LIGHTNING"
                         : stopTime <= 2.80f ? "CLEAN STOP"
                         : stopTime <= 3.40f ? "SOLID"
                         : stopTime <= 4.10f ? "MESSY"
                         : "POOR STOP";

            if (stopTime <= 2.45f) audio?.PlayPitEvent("crew_go");
            else if (stopTime > 4f) audio?.PlayPitEvent("nut_error");

            OnComplete?.Invoke(stopTime, grade);
            OnComplete = null;
        }

        void _PlayStepAudio(StepId step, bool done)
        {
            string evt = step switch
            {
                StepId.JackUp   => done ? "jack_raise"      : "jack_raise",
                StepId.WheelFL  or StepId.WheelFR or StepId.WheelRL or StepId.WheelRR
                                => done ? "wheel_gun_done"  : "wheel_gun_spin",
                StepId.JackDown => done ? "jack_lower"      : "jack_lower",
                StepId.Lollipop => done ? "lollipop_up"     : "lollipop_up",
                _               => null,
            };
            if (evt != null) audio?.PlayPitEvent(evt);
        }
    }
}
