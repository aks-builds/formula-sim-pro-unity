using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FormulaSim.Cars;
using FormulaSim.Audio;
using System.Collections;

namespace FormulaSim.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Lap / Time")]
        [SerializeField] TMP_Text lapCounterLabel;
        [SerializeField] TMP_Text lapTimeLabel;
        [SerializeField] TMP_Text lastLapLabel;
        [SerializeField] TMP_Text bestLapLabel;

        [Header("Speed / RPM")]
        [SerializeField] TMP_Text  speedLabel;
        [SerializeField] TMP_Text  gearLabel;
        [SerializeField] Image     rpmBar;
        [SerializeField] Image     rpmBarFill;

        [Header("DRS")]
        [SerializeField] CanvasGroup drsIndicator;
        [SerializeField] Image       drsGlow;

        [Header("Tire Corners")]
        [SerializeField] TireCornerUI tireFL, tireFR, tireRL, tireRR;

        [Header("Position")]
        [SerializeField] TMP_Text positionLabel;
        [SerializeField] TMP_Text gapLabel;

        [Header("Flags")]
        [SerializeField] CanvasGroup scBoard;
        [SerializeField] CanvasGroup vscBoard;

        [Header("Commentary Subtitle")]
        [SerializeField] TMP_Text   subtitleLabel;
        [SerializeField] CanvasGroup subtitleGroup;

        F1CarController   playerCar;
        AudioManager      audio;
        TireManager.TireSet tires;
        float subtitleTimer;

        static readonly Color RPM_NORMAL  = new(0.2f, 0.8f, 0.3f);
        static readonly Color RPM_HIGH    = new(1.0f, 0.8f, 0.1f);
        static readonly Color RPM_REDLINE = new(1.0f, 0.15f, 0.1f);

        void Start()
        {
            playerCar = FindObjectOfType<F1CarController>();
            audio     = FindObjectOfType<AudioManager>();
            if (playerCar)
            {
                playerCar.OnLapComplete += OnLapComplete;
                playerCar.OnGearChange  += _ => _FlashGear();
            }
            subtitleGroup.alpha = 0f;
        }

        void Update()
        {
            if (!playerCar) return;

            float rpm       = playerCar.RPM;
            float maxRpm    = 15000f;
            float rpmFrac   = rpm / maxRpm;

            // Speed
            speedLabel.text = $"{playerCar.SpeedKph:0}";
            gearLabel.text  = playerCar.Gear == 0 ? "N" : playerCar.Gear.ToString();

            // RPM bar
            rpmBarFill.fillAmount = rpmFrac;
            rpmBarFill.color      = rpmFrac > 0.92f ? RPM_REDLINE
                                  : rpmFrac > 0.75f ? RPM_HIGH
                                  : RPM_NORMAL;

            // Lap time
            lapTimeLabel.text = _FormatTime(playerCar.LapTime);

            // DRS
            bool drs = playerCar.DrsActive;
            drsIndicator.alpha = Mathf.Lerp(drsIndicator.alpha, drs ? 1f : 0.25f, Time.deltaTime * 6f);
            if (drs) drsGlow.color = Color.Lerp(drsGlow.color,
                new Color(0.3f, 0.9f, 1f, 0.9f + Mathf.Sin(Time.time * 8f) * 0.1f), Time.deltaTime * 8f);

            // Tire corners
            tires = playerCar.Tires;
            if (tires != null)
            {
                UpdateTireUI(tireFL, tires.FL);
                UpdateTireUI(tireFR, tires.FR);
                UpdateTireUI(tireRL, tires.RL);
                UpdateTireUI(tireRR, tires.RR);
            }

            // Subtitle decay
            if (subtitleTimer > 0)
            {
                subtitleTimer -= Time.deltaTime;
                subtitleGroup.alpha = Mathf.Clamp01(subtitleTimer);
            }
        }

        void UpdateTireUI(TireCornerUI ui, TireManager.TireCornner t)
        {
            if (ui == null || t == null) return;
            float wear = t.Wear;
            ui.wearBar.fillAmount = 1f - wear;
            ui.wearBar.color = wear < 0.4f ? new Color(0.2f, 0.9f, 0.3f)
                             : wear < 0.7f ? new Color(1f, 0.75f, 0.1f)
                             : new Color(1f, 0.2f, 0.1f);

            // Temperature ring colour
            var opt = t.Compound switch
            {
                TireCompound.Soft   => (80f, 95f, 110f),
                TireCompound.Medium => (75f, 90f, 108f),
                TireCompound.Hard   => (70f, 85f, 105f),
                TireCompound.Inter  => (40f, 55f, 75f),
                TireCompound.Wet    => (30f, 45f, 65f),
                _                   => (75f, 90f, 108f),
            };
            float norm = Mathf.InverseLerp(opt.Item1, opt.Item3, t.Temp);
            ui.tempRing.color = norm < 0.4f
                ? Color.Lerp(Color.blue,  Color.green, norm / 0.4f)
                : Color.Lerp(Color.green, Color.red, (norm - 0.4f) / 0.6f);

            if (t.FlatSpot)
            {
                ui.flatSpotIcon.SetActive(true);
                ui.flatSpotIcon.transform.localScale = Vector3.one
                    * (1f + Mathf.Sin(Time.time * 12f) * 0.1f);
            }
            else ui.flatSpotIcon.SetActive(false);
        }

        void OnLapComplete(float lapTime)
        {
            lastLapLabel.text = _FormatTime(lapTime);
            bool best = lapTime < playerCar.BestLapTime;
            bestLapLabel.text  = _FormatTime(playerCar.BestLapTime);
            bestLapLabel.color = best ? new Color(0.6f, 0.2f, 1f) : Color.white;
            if (best) audio?.PlayUI(UISound.NewBestLap);
            StartCoroutine(_PulseLapTime(best));
        }

        IEnumerator _PulseLapTime(bool isPurple)
        {
            Color target = isPurple ? new Color(0.6f, 0.2f, 1f) : new Color(0.2f, 0.9f, 0.3f);
            lastLapLabel.color = target;
            lastLapLabel.transform.localScale = Vector3.one * 1.3f;
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                lastLapLabel.transform.localScale = Vector3.Lerp(Vector3.one * 1.3f, Vector3.one, t / 0.5f);
                yield return null;
            }
            yield return new WaitForSeconds(3f);
            lastLapLabel.color = Color.white;
        }

        void _FlashGear()
        {
            StopCoroutine(nameof(_GearFlash));
            StartCoroutine(_GearFlash());
        }

        IEnumerator _GearFlash()
        {
            gearLabel.color = Color.white;
            float t = 0f;
            while (t < 0.15f) { t += Time.deltaTime; yield return null; }
            gearLabel.color = new Color(1, 1, 1, 0.7f);
        }

        public void ShowSubtitle(string text, float duration = 4f)
        {
            subtitleLabel.text = text;
            subtitleTimer      = duration;
            subtitleGroup.alpha = 1f;
        }

        public void SetPosition(int pos, float gapSecs)
        {
            positionLabel.text = $"P{pos}";
            gapLabel.text      = pos == 1 ? "LEADER" : $"+{gapSecs:0.000}";
        }

        public void SetSafetyCarBoards(bool sc, bool vsc)
        {
            scBoard.alpha  = sc  ? 1f : 0f;
            vscBoard.alpha = vsc ? 1f : 0f;
        }

        static string _FormatTime(float t)
        {
            int  m  = Mathf.FloorToInt(t / 60f);
            float s = t - m * 60f;
            return $"{m}:{s:00.000}";
        }
    }

    [System.Serializable]
    public class TireCornerUI
    {
        public Image  wearBar;
        public Image  tempRing;
        public GameObject flatSpotIcon;
    }
}
