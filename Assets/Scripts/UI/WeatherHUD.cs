using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FormulaSim.Weather;
using FormulaSim.Cars;

namespace FormulaSim.UI
{
    public class WeatherHUD : MonoBehaviour
    {
        [Header("Weather State")]
        [SerializeField] Image     weatherIcon;
        [SerializeField] TMP_Text  weatherLabel;
        [SerializeField] Sprite[]  weatherIcons;   // indexed by WeatherState enum

        [Header("Rain Bar")]
        [SerializeField] Image     rainBar;
        [SerializeField] Image     rainBarFill;
        [SerializeField] float     rainBarMaxW = 120f;

        [Header("Optimal Tire")]
        [SerializeField] Image     tireIndicator;
        [SerializeField] TMP_Text  tireLabel;

        [Header("Strategy Banner")]
        [SerializeField] RectTransform strategyBanner;
        [SerializeField] Image         strategyBg;
        [SerializeField] TMP_Text      strategyText;
        [SerializeField] CanvasGroup   strategyGroup;

        [Header("Aquaplane Alert")]
        [SerializeField] CanvasGroup  aquaplaneGroup;
        [SerializeField] TMP_Text     aquaplaneLabel;

        [Header("Track Wetness")]
        [SerializeField] TMP_Text  pitWindowLabel;
        [SerializeField] Image     visibilityBar;

        WeatherSystem weather;
        WeatherState  lastState;
        float         bannerTimer;
        float         aqFlashTimer;
        bool          bannerVisible;

        static readonly Color[] WEATHER_COLORS =
        {
            new(1.00f, 0.95f, 0.50f),   // Dry
            new(0.75f, 0.80f, 0.85f),   // Overcast
            new(0.65f, 0.80f, 1.00f),   // Drizzle
            new(0.40f, 0.65f, 1.00f),   // Light
            new(0.25f, 0.45f, 0.90f),   // Heavy
            new(0.30f, 0.20f, 0.80f),   // Extreme
        };

        static readonly Color[] COMPOUND_COLORS =
        {
            new(1.0f, 0.2f, 0.2f),  // Soft
            new(1.0f, 0.9f, 0.1f),  // Medium
            new(0.9f, 0.9f, 0.9f),  // Hard
            new(0.2f, 0.8f, 0.3f),  // Inter
            new(0.3f, 0.5f, 1.0f),  // Wet
        };

        void Start()
        {
            weather = FindObjectOfType<WeatherSystem>();
            if (weather)
            {
                weather.OnWeatherChanged += OnWeatherChanged;
                weather.OnWeatherWarning += OnWeatherWarning;
            }
            aquaplaneGroup.alpha = 0f;
            strategyGroup.alpha  = 0f;
        }

        void Update()
        {
            if (!weather) return;

            // Rain intensity bar
            float targetW = weather.RainIntensity * rainBarMaxW;
            var sz = rainBarFill.rectTransform.sizeDelta;
            rainBarFill.rectTransform.sizeDelta = new Vector2(
                Mathf.Lerp(sz.x, targetW, Time.deltaTime * 4f), sz.y);

            // Banner auto-dismiss
            if (bannerVisible && bannerTimer > 0)
            {
                bannerTimer -= Time.deltaTime;
                if (bannerTimer <= 0) StartCoroutine(_DismissBanner());
            }

            // Aquaplane flash
            if (aqFlashTimer > 0)
            {
                aqFlashTimer -= Time.deltaTime;
                float pulse = Mathf.Abs(Mathf.Sin(aqFlashTimer * 12f));
                aquaplaneGroup.alpha = pulse;
                if (aqFlashTimer <= 0) aquaplaneGroup.alpha = 0f;
            }

            // Track wetness label
            float wet = weather.TrackWetness;
            pitWindowLabel.text = wet > 0.05f
                ? $"TRACK WET {Mathf.FloorToInt(wet * 100)}%"
                : "TRACK DRY";
        }

        void OnWeatherChanged(WeatherState state)
        {
            int idx = (int)state;
            Color col = WEATHER_COLORS[Mathf.Clamp(idx, 0, WEATHER_COLORS.Length - 1)];

            weatherLabel.text  = WeatherSystem.WeatherLabel(state);
            weatherLabel.color = col;
            rainBarFill.color  = col;
            if (weatherIcons != null && idx < weatherIcons.Length)
                weatherIcon.sprite = weatherIcons[idx];
            weatherIcon.color = col;

            // Shake icon on heavy rain
            if (state >= WeatherState.Heavy) StartCoroutine(_ShakeIcon());

            // Optimal tire
            TireCompound optimal = weather.OptimalCompound();
            int ci = (int)optimal;
            tireIndicator.color = COMPOUND_COLORS[Mathf.Clamp(ci, 0, COMPOUND_COLORS.Length - 1)];
            tireLabel.text      = optimal.ToString()[0].ToString();

            lastState = state;
        }

        void OnWeatherWarning(string message)
        {
            ShowStrategyBanner(message, "FORECAST", new Color(0.3f, 0.6f, 1f, 0.9f), 6f);
        }

        public void ShowStrategyBanner(string message, string urgency, Color bgColor, float duration)
        {
            strategyText.text = message;
            strategyBg.color  = bgColor;
            bannerTimer       = duration;
            StartCoroutine(_ShowBanner());
        }

        IEnumerator _ShowBanner()
        {
            bannerVisible = true;
            // Slide in from below
            Vector2 hiddenPos = new(strategyBanner.anchoredPosition.x,
                strategyBanner.anchoredPosition.y - 80f);
            Vector2 shownPos  = new(strategyBanner.anchoredPosition.x,
                strategyBanner.anchoredPosition.y);
            strategyGroup.alpha = 0f;
            strategyBanner.anchoredPosition = hiddenPos;

            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float ease = 1f - Mathf.Pow(1f - t / 0.4f, 3f);   // ease out cubic
                strategyBanner.anchoredPosition = Vector2.Lerp(hiddenPos, shownPos, ease);
                strategyGroup.alpha = Mathf.Lerp(0f, 1f, t / 0.4f);
                yield return null;
            }
        }

        IEnumerator _DismissBanner()
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                strategyGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.35f);
                yield return null;
            }
            bannerVisible = false;
        }

        public void TriggerAquaplaneAlert(bool active)
        {
            if (active)
            {
                aquaplaneLabel.text = "⚠ AQUAPLANING";
                aqFlashTimer = 2.5f;
            }
            else
            {
                aqFlashTimer = 0f;
                aquaplaneGroup.alpha = 0f;
            }
        }

        IEnumerator _ShakeIcon()
        {
            Vector3 orig = weatherIcon.transform.localPosition;
            int steps = 8; float dur = 0.6f; float amp = 6f;
            for (int i = 0; i <= steps; i++)
            {
                float decay = 1f - (float)i / steps;
                float dx = (i % 2 == 0 ? amp : -amp) * decay;
                float dy = Random.Range(-amp, amp) * decay;
                weatherIcon.transform.localPosition = orig + new Vector3(dx, dy, 0);
                yield return new WaitForSeconds(dur / steps);
            }
            weatherIcon.transform.localPosition = orig;
        }
    }
}
