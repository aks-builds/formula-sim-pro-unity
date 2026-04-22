using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FormulaSim.Race;
using FormulaSim.Cars;

namespace FormulaSim.UI
{
    /// <summary>
    /// Overlay HUD for track limits warnings and time penalties.
    /// Shows a warning counter badge, animated penalty banner, and drive-through indicator.
    /// Subscribe to PenaltySystem events — no polling needed.
    /// </summary>
    public class PenaltyHUD : MonoBehaviour
    {
        [Header("Warning Badge")]
        [SerializeField] GameObject  warningBadge;
        [SerializeField] TMP_Text    warningCountText;     // e.g. "⚠ 2/3"
        [SerializeField] Image       warningBadgeBG;

        [Header("Penalty Banner")]
        [SerializeField] GameObject  penaltyBanner;
        [SerializeField] TMP_Text    penaltyBannerText;    // e.g. "+5s PENALTY"
        [SerializeField] CanvasGroup penaltyBannerGroup;

        [Header("Drive-Through Indicator")]
        [SerializeField] GameObject  driveThroughPanel;
        [SerializeField] TMP_Text    driveThroughLabel;

        [Header("Colors")]
        [SerializeField] Color warningColor  = new(1f, 0.75f, 0f, 1f);   // amber
        [SerializeField] Color penaltyColor  = new(0.9f, 0.15f, 0.15f, 1f);
        [SerializeField] Color safeColor     = new(0.2f, 0.9f, 0.4f, 1f);

        F1CarController _player;

        void Start()
        {
            _player = FindObjectOfType<F1CarController>();

            if (PenaltySystem.Instance)
                PenaltySystem.Instance.OnPenaltyIssued += _OnPenalty;

            if (warningBadge) warningBadge.SetActive(false);
            if (penaltyBanner) penaltyBanner.SetActive(false);
            if (driveThroughPanel) driveThroughPanel.SetActive(false);
        }

        void OnDestroy()
        {
            if (PenaltySystem.Instance)
                PenaltySystem.Instance.OnPenaltyIssued -= _OnPenalty;
        }

        void _OnPenalty(F1CarController ctrl, PenaltySystem.PenaltyRecord pen)
        {
            // Only show HUD for the player car
            if (_player == null || ctrl != _player) return;

            switch (pen.type)
            {
                case PenaltySystem.PenaltyType.TrackLimitsWarning:
                    _ShowWarning();
                    break;

                case PenaltySystem.PenaltyType.TrackLimitsPenalty:
                case PenaltySystem.PenaltyType.CollisionPenalty:
                case PenaltySystem.PenaltyType.PitSpeeding:
                    _ShowPenaltyBanner($"+{pen.seconds:F0}s PENALTY", penaltyColor);
                    _HideWarningBadge();
                    break;

                case PenaltySystem.PenaltyType.DriveThrough:
                    _ShowDriveThrough();
                    _ShowPenaltyBanner("DRIVE-THROUGH", penaltyColor);
                    break;
            }
        }

        void _ShowWarning()
        {
            if (_player == null || PenaltySystem.Instance == null) return;

            int warns = PenaltySystem.Instance.GetTrackLimitWarnings(_player);
            int max   = 3;

            if (warningBadge)
            {
                warningBadge.SetActive(true);
                if (warningCountText) warningCountText.text = $"⚠ {warns}/{max}";
                if (warningBadgeBG)
                    warningBadgeBG.color = warns >= max - 1 ? penaltyColor : warningColor;
            }

            // Flash banner for the warning itself
            _ShowPenaltyBanner($"TRACK LIMITS WARNING  {warns}/{max}", warningColor);
        }

        void _HideWarningBadge()
        {
            if (warningBadge) warningBadge.SetActive(false);
        }

        void _ShowPenaltyBanner(string message, Color color)
        {
            if (penaltyBanner == null) return;
            StopCoroutine("_FadeBanner");
            penaltyBanner.SetActive(true);
            if (penaltyBannerText) penaltyBannerText.text = message;
            if (penaltyBannerText) penaltyBannerText.color = color;
            if (penaltyBannerGroup) penaltyBannerGroup.alpha = 1f;
            StartCoroutine(_FadeBanner(3.5f));
        }

        void _ShowDriveThrough()
        {
            if (driveThroughPanel == null) return;
            driveThroughPanel.SetActive(true);
            if (driveThroughLabel) driveThroughLabel.text = "DRIVE-THROUGH — Serve when safe";
        }

        public void OnDriveThroughServed()
        {
            if (driveThroughPanel) driveThroughPanel.SetActive(false);
        }

        IEnumerator _FadeBanner(float holdSec)
        {
            yield return new WaitForSeconds(holdSec);

            if (penaltyBannerGroup == null)
            {
                penaltyBanner?.SetActive(false);
                yield break;
            }

            float t = 0f;
            const float fadeDur = 0.6f;
            while (t < fadeDur)
            {
                t += Time.unscaledDeltaTime;
                penaltyBannerGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDur);
                yield return null;
            }
            penaltyBanner.SetActive(false);
        }
    }
}
