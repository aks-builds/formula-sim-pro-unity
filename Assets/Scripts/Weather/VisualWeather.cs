using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FormulaSim.Core;

namespace FormulaSim.Weather
{
    // Drives all URP post-processing and shader uniforms based on weather state.
    public class VisualWeather : MonoBehaviour
    {
        [Header("URP Volume")]
        [SerializeField] Volume postProcessVolume;

        [Header("Weather Materials")]
        [SerializeField] Material wetTrackMaterial;
        [SerializeField] Material rainOverlayMaterial;

        [Header("Particle Systems")]
        [SerializeField] ParticleSystem rainParticles;
        [SerializeField] ParticleSystem heavyRainParticles;
        [SerializeField] ParticleSystem rainSplashParticles;

        [Header("Scene Lighting")]
        [SerializeField] Light2D sunLight;
        [SerializeField] Light2D ambientFill;

        [Header("Lightning Flash")]
        [SerializeField] Light2D lightningLight;
        [SerializeField] float   lightningIntensityPeak = 8f;

        // URP post-processing components
        Bloom               bloom;
        ChromaticAberration chromAb;
        ColorAdjustments    colorAdj;
        MotionBlur          motionBlur;
        Vignette            vignette;
        LensDistortion      lensDistort;

        WeatherSystem weather;
        bool          isNight;

        // Current interpolated values
        float curFogDensity, curRainOpacity, curPuddleCoverage, curReflectionStr;
        Color curSkyTint;
        float curAmbientMult, curTrackDarkening;

        static readonly int _PuddleCoverage  = Shader.PropertyToID("_PuddleCoverage");
        static readonly int _TrackDarkening  = Shader.PropertyToID("_TrackDarkening");
        static readonly int _ReflectionStr   = Shader.PropertyToID("_ReflectionStr");
        static readonly int _TrackWetness    = Shader.PropertyToID("_TrackWetness");
        static readonly int _SkyTint         = Shader.PropertyToID("_SkyTint");
        static readonly int _FlashIntensity  = Shader.PropertyToID("_FlashIntensity");
        static readonly int _RainOpacity     = Shader.PropertyToID("_RainOpacity");
        static readonly int _Time            = Shader.PropertyToID("_Time");

        void Awake()
        {
            weather = FindObjectOfType<WeatherSystem>();
            postProcessVolume.profile.TryGet(out bloom);
            postProcessVolume.profile.TryGet(out chromAb);
            postProcessVolume.profile.TryGet(out colorAdj);
            postProcessVolume.profile.TryGet(out motionBlur);
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out lensDistort);
        }

        public void Init(bool nightRace)
        {
            isNight = nightRace;
            SetNightMode(nightRace);
        }

        public void SetNightMode(bool night)
        {
            isNight = night;
            if (sunLight)  sunLight.intensity  = night ? 0.1f : 1.2f;
            if (ambientFill) ambientFill.intensity = night ? 0.3f : 0.8f;
            // Night: more bloom for headlights and track lights
            if (bloom != null) bloom.intensity.value = night ? 2.8f : 0.9f;
        }

        void Update()
        {
            var state     = weather.CurrentState;
            float rain    = weather.RainIntensity;
            float wetness = weather.TrackWetness;

            // Lerp visual parameters toward target
            var target  = GetVisualTarget(state);
            float lerpT = Time.deltaTime * 0.4f;

            curFogDensity    = Mathf.Lerp(curFogDensity,    target.fogDensity,    lerpT);
            curRainOpacity   = Mathf.Lerp(curRainOpacity,   rain,                 lerpT);
            curPuddleCoverage= Mathf.Lerp(curPuddleCoverage,target.puddleCoverage,lerpT);
            curReflectionStr = Mathf.Lerp(curReflectionStr, target.reflectionStr, lerpT);
            curAmbientMult   = Mathf.Lerp(curAmbientMult,   target.ambientMult,   lerpT);
            curTrackDarkening= Mathf.Lerp(curTrackDarkening,target.trackDarkening,lerpT);
            curSkyTint       = Color.Lerp(curSkyTint, target.skyTint, lerpT);

            _PushShaderUniforms(wetness);
            _UpdatePostProcessing(state);
            _UpdateParticles(rain);
            _UpdateLighting();
        }

        void _PushShaderUniforms(float wetness)
        {
            float t = Time.time;
            if (wetTrackMaterial)
            {
                wetTrackMaterial.SetFloat(_PuddleCoverage, curPuddleCoverage);
                wetTrackMaterial.SetFloat(_TrackDarkening, curTrackDarkening);
                wetTrackMaterial.SetFloat(_ReflectionStr,  curReflectionStr);
                wetTrackMaterial.SetFloat(_TrackWetness,   wetness);
                wetTrackMaterial.SetColor(_SkyTint,        curSkyTint);
                wetTrackMaterial.SetFloat(_FlashIntensity, 0f);
            }
            if (rainOverlayMaterial)
            {
                rainOverlayMaterial.SetFloat(_RainOpacity, curRainOpacity);
            }
        }

        void _UpdatePostProcessing(WeatherState state)
        {
            // Bloom: peaks in heavy rain (headlight scatter, spray glow)
            if (bloom != null)
            {
                float targetBloom = isNight ? 2.8f : 0.9f;
                if (state >= WeatherState.Heavy) targetBloom += 1.2f;
                bloom.intensity.value = Mathf.Lerp(bloom.intensity.value, targetBloom, Time.deltaTime * 0.5f);
            }

            // Chromatic aberration: peaks in extreme conditions (visual stress)
            if (chromAb != null)
                chromAb.intensity.value = Mathf.Lerp(0f, 0.35f,
                    Mathf.InverseLerp(0f, (float)WeatherState.Extreme, (float)state));

            // Color grading: desaturate and cool in rain
            if (colorAdj != null)
            {
                float satTarget = state >= WeatherState.Heavy ? -18f : 0f;
                float tempTarget= state >= WeatherState.Drizzle ? -12f : 0f;
                colorAdj.saturation.value  = Mathf.Lerp(colorAdj.saturation.value, satTarget, Time.deltaTime * 0.3f);
                colorAdj.colorFilter.value = Color.Lerp(colorAdj.colorFilter.value, curSkyTint, Time.deltaTime * 0.2f);
            }

            // Vignette: tighter in extreme conditions
            if (vignette != null)
                vignette.intensity.value = Mathf.Lerp(0.25f, 0.55f,
                    Mathf.InverseLerp(0f, (float)WeatherState.Extreme, (float)state));

            // Lens distortion: subtle rain effect
            if (lensDistort != null)
                lensDistort.intensity.value = Mathf.Lerp(0f, -0.12f, curRainOpacity);
        }

        void _UpdateParticles(float rain)
        {
            SetParticleRate(rainParticles,      Mathf.Lerp(0f, 300f, rain * 0.7f));
            SetParticleRate(heavyRainParticles, Mathf.Lerp(0f, 600f, Mathf.Max(0f, rain - 0.6f) * 2.5f));
            SetParticleRate(rainSplashParticles,Mathf.Lerp(0f, 200f, weather.TrackWetness));
        }

        static void SetParticleRate(ParticleSystem ps, float rate)
        {
            if (!ps) return;
            var e = ps.emission;
            e.rateOverTime = rate;
            if (rate > 0 && !ps.isPlaying) ps.Play();
            else if (rate <= 0 && ps.isPlaying) ps.Stop();
        }

        void _UpdateLighting()
        {
            if (ambientFill)
                ambientFill.intensity = Mathf.Lerp(ambientFill.intensity,
                    (isNight ? 0.3f : 0.8f) * curAmbientMult, Time.deltaTime * 0.5f);
        }

        // ── Lightning flash ───────────────────────────────────────────────────

        public void TriggerLightningFlash()
        {
            StartCoroutine(_FlashCoroutine());
        }

        IEnumerator _FlashCoroutine()
        {
            if (lightningLight) lightningLight.intensity = lightningIntensityPeak;
            if (wetTrackMaterial) wetTrackMaterial.SetFloat(_FlashIntensity, 1f);
            if (bloom != null) bloom.intensity.value += 4f;
            yield return new WaitForSeconds(0.10f);

            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                float alpha = 1f - t / 0.25f;
                if (lightningLight) lightningLight.intensity = lightningIntensityPeak * alpha;
                if (wetTrackMaterial) wetTrackMaterial.SetFloat(_FlashIntensity, alpha);
                yield return null;
            }
            if (lightningLight) lightningLight.intensity = 0f;
        }

        // ── Visual parameter targets per state ────────────────────────────────

        struct VisualTarget
        {
            public float fogDensity, puddleCoverage, reflectionStr;
            public float ambientMult, trackDarkening, rainOpacity;
            public Color skyTint;
        }

        static VisualTarget GetVisualTarget(WeatherState s) => s switch
        {
            WeatherState.Dry      => new VisualTarget { fogDensity=0f,    puddleCoverage=0f,    reflectionStr=0f,    ambientMult=1.00f, trackDarkening=0f,    skyTint=new Color(0.55f,0.72f,1.00f) },
            WeatherState.Overcast => new VisualTarget { fogDensity=0.04f, puddleCoverage=0f,    reflectionStr=0.05f, ambientMult=0.90f, trackDarkening=0.02f, skyTint=new Color(0.62f,0.65f,0.70f) },
            WeatherState.Drizzle  => new VisualTarget { fogDensity=0.08f, puddleCoverage=0.10f, reflectionStr=0.18f, ambientMult=0.82f, trackDarkening=0.10f, skyTint=new Color(0.50f,0.55f,0.68f) },
            WeatherState.Light    => new VisualTarget { fogDensity=0.14f, puddleCoverage=0.30f, reflectionStr=0.40f, ambientMult=0.72f, trackDarkening=0.22f, skyTint=new Color(0.38f,0.44f,0.65f) },
            WeatherState.Heavy    => new VisualTarget { fogDensity=0.28f, puddleCoverage=0.65f, reflectionStr=0.72f, ambientMult=0.55f, trackDarkening=0.40f, skyTint=new Color(0.22f,0.26f,0.42f) },
            WeatherState.Extreme  => new VisualTarget { fogDensity=0.50f, puddleCoverage=1.00f, reflectionStr=0.95f, ambientMult=0.38f, trackDarkening=0.60f, skyTint=new Color(0.12f,0.14f,0.28f) },
            _                     => new VisualTarget { ambientMult=1f, skyTint=Color.white },
        };
    }
}
