using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FormulaSim.Core
{
    /// <summary>
    /// Dynamic performance scaler targeting 60 FPS on mid-range mobile.
    /// Monitors frame time over a rolling window and scales quality settings
    /// when performance drops. Never reduces below minimum quality floor.
    ///
    /// Scalable parameters:
    ///   - Shadow distance
    ///   - Particle max count
    ///   - Post-processing (bloom, motion blur toggle)
    ///   - Physics fixed timestep
    ///   - LOD bias
    /// </summary>
    public class PerformanceManager : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] int   targetFPS          = 60;
        [SerializeField] float degradeThreshold   = 0.80f;  // start degrading at 80% of target
        [SerializeField] float recoverThreshold   = 0.95f;  // recover when above 95%

        [Header("Sample Window")]
        [SerializeField] float sampleWindowSecs   = 2f;
        [SerializeField] int   sampleCount        = 30;

        [Header("Quality Levels")]
        [SerializeField] QualityLevel[] levels;

        [Header("References")]
        [SerializeField] Volume   postProcessVolume;
        [SerializeField] UniversalRenderPipelineAsset urpAsset;

        [System.Serializable]
        public class QualityLevel
        {
            public string label;
            public float  shadowDistance    = 30f;
            public int    maxParticles      = 500;
            public bool   bloomEnabled      = true;
            public bool   motionBlurEnabled = true;
            public float  fixedTimeStep     = 0.02f;   // 50 Hz physics
            public float  lodBias           = 1.0f;
        }

        int   currentLevel;
        float[] frameSamples;
        int   sampleIdx;
        float sampleTimer;
        Bloom       bloom;
        MotionBlur  motionBlur;

        static readonly QualityLevel[] DEFAULT_LEVELS = new[]
        {
            new QualityLevel { label="Ultra",   shadowDistance=50f, maxParticles=1000, bloomEnabled=true,  motionBlurEnabled=true,  fixedTimeStep=0.016f, lodBias=1.5f },
            new QualityLevel { label="High",    shadowDistance=35f, maxParticles=700,  bloomEnabled=true,  motionBlurEnabled=true,  fixedTimeStep=0.02f,  lodBias=1.2f },
            new QualityLevel { label="Medium",  shadowDistance=20f, maxParticles=400,  bloomEnabled=true,  motionBlurEnabled=false, fixedTimeStep=0.02f,  lodBias=1.0f },
            new QualityLevel { label="Low",     shadowDistance=10f, maxParticles=200,  bloomEnabled=false, motionBlurEnabled=false, fixedTimeStep=0.025f, lodBias=0.7f },
            new QualityLevel { label="Minimum", shadowDistance=5f,  maxParticles=80,   bloomEnabled=false, motionBlurEnabled=false, fixedTimeStep=0.033f, lodBias=0.5f },
        };

        void Awake()
        {
            if (levels == null || levels.Length == 0) levels = DEFAULT_LEVELS;
            frameSamples  = new float[sampleCount];
            currentLevel  = Mathf.Clamp(1, 0, levels.Length - 1);   // start at High

            Application.targetFrameRate = targetFPS;
            QualitySettings.vSyncCount  = 0;

            postProcessVolume?.profile.TryGet(out bloom);
            postProcessVolume?.profile.TryGet(out motionBlur);

            _ApplyLevel(currentLevel);
        }

        void Update()
        {
            frameSamples[sampleIdx % sampleCount] = Time.unscaledDeltaTime;
            sampleIdx++;

            sampleTimer += Time.unscaledDeltaTime;
            if (sampleTimer < sampleWindowSecs) return;
            sampleTimer = 0f;

            float avg    = _AverageFrameTime();
            float target = 1f / targetFPS;
            float ratio  = target / avg;   // > 1 = fast, < 1 = slow

            if (ratio < degradeThreshold && currentLevel < levels.Length - 1)
            {
                currentLevel++;
                _ApplyLevel(currentLevel);
                Debug.Log($"[Perf] Degraded to {levels[currentLevel].label} ({avg * 1000f:F1}ms avg)");
            }
            else if (ratio > recoverThreshold && currentLevel > 0)
            {
                currentLevel--;
                _ApplyLevel(currentLevel);
                Debug.Log($"[Perf] Upgraded to {levels[currentLevel].label}");
            }
        }

        void _ApplyLevel(int lvl)
        {
            var q = levels[lvl];

            // Shadow distance
            if (urpAsset) urpAsset.shadowDistance = q.shadowDistance;

            // Post-processing
            if (bloom      != null) bloom.active      = q.bloomEnabled;
            if (motionBlur != null) motionBlur.active = q.motionBlurEnabled;

            // Physics rate
            Time.fixedDeltaTime = q.fixedTimeStep;

            // LOD
            QualitySettings.lodBias = q.lodBias;

            // Particle max — notify particle systems
            ParticleSystem.MainModule main;
            foreach (var ps in FindObjectsOfType<ParticleSystem>())
            {
                main = ps.main;
                main.maxParticles = q.maxParticles;
            }
        }

        float _AverageFrameTime()
        {
            float sum = 0f;
            foreach (float s in frameSamples) sum += s;
            return sum / frameSamples.Length;
        }

        public string CurrentLevelName => levels[currentLevel].label;
    }
}
