using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FormulaSim.Race;
using FormulaSim.Core;

namespace FormulaSim.UI
{
    /// <summary>
    /// Q1 / Q2 / Q3 overlay: session timer, best times tower, elimination band.
    /// Hooks into QualifyingSession events for live updates.
    /// </summary>
    public class QualifyingHUD : MonoBehaviour
    {
        [Header("Session Info")]
        [SerializeField] TMP_Text sessionLabel;     // "Q1" / "Q2" / "Q3"
        [SerializeField] TMP_Text timerLabel;       // "14:37"
        [SerializeField] TMP_Text gapLabel;         // player gap to P1 best

        [Header("Player Best")]
        [SerializeField] TMP_Text playerBestLabel;
        [SerializeField] TMP_Text playerSector1Label;
        [SerializeField] TMP_Text playerSector2Label;
        [SerializeField] TMP_Text playerSector3Label;

        [Header("Timing Tower")]
        [SerializeField] Transform      towerRoot;
        [SerializeField] GameObject     towerRowPrefab;  // has Position + Name + Time + Delta labels

        [Header("Elimination Band")]
        [SerializeField] GameObject eliminationBand;   // red line / panel
        [SerializeField] TMP_Text   eliminationLabel;  // "P16 — ELIMINATION ZONE"

        [Header("Flags")]
        [SerializeField] Image      flagImage;
        [SerializeField] Sprite     yellowFlagSprite;
        [SerializeField] Sprite     checkeredSprite;

        QualifyingSession session;
        List<(TMP_Text pos, TMP_Text name, TMP_Text time, TMP_Text delta)> towerRows = new();

        void Start()
        {
            session = FindObjectOfType<QualifyingSession>();
            if (session == null) return;

            session.OnStageChanged    += _OnStageChanged;
            session.OnTimingUpdate    += _OnTimingUpdate;
            session.OnSessionEnd      += _OnSessionEnd;

            _BuildTower(20);
            _OnStageChanged(session.CurrentStageInt);
        }

        void OnDestroy()
        {
            if (session == null) return;
            session.OnStageChanged -= _OnStageChanged;
            session.OnTimingUpdate -= _OnTimingUpdate;
            session.OnSessionEnd   -= _OnSessionEnd;
        }

        void Update()
        {
            if (session == null || !session.IsRunning) return;

            float remaining = session.TimeRemaining;
            int   mins      = Mathf.FloorToInt(remaining / 60f);
            int   secs      = Mathf.FloorToInt(remaining % 60f);
            if (timerLabel) timerLabel.text = $"{mins:D2}:{secs:D2}";

            // Pulse timer red in final 90s
            if (timerLabel)
                timerLabel.color = remaining < 90f ? Color.Lerp(Color.white, Color.red,
                    Mathf.PingPong(Time.time * 2f, 1f)) : Color.white;
        }

        void _BuildTower(int rows)
        {
            foreach (Transform child in towerRoot) Destroy(child.gameObject);
            towerRows.Clear();

            for (int i = 0; i < rows; i++)
            {
                var row = Instantiate(towerRowPrefab, towerRoot);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                // Convention: texts[0]=pos, texts[1]=name, texts[2]=time, texts[3]=delta
                towerRows.Add((texts[0], texts[1], texts[2], texts.Length > 3 ? texts[3] : null));
            }
        }

        void _OnStageChanged(int stage)
        {
            if (sessionLabel) sessionLabel.text = $"Q{stage}";

            int eliminationPos = stage switch { 1 => 16, 2 => 11, _ => 0 };
            if (eliminationBand)  eliminationBand.SetActive(eliminationPos > 0);
            if (eliminationLabel) eliminationLabel.text = eliminationPos > 0
                ? $"P{eliminationPos} — ELIMINATION ZONE" : string.Empty;
        }

        void _OnTimingUpdate(List<QualifyingSession.DriverTiming> timings)
        {
            for (int i = 0; i < towerRows.Count; i++)
            {
                if (i >= timings.Count)
                {
                    towerRows[i].pos.transform.parent.gameObject.SetActive(false);
                    continue;
                }

                towerRows[i].pos.transform.parent.gameObject.SetActive(true);
                var t = timings[i];
                towerRows[i].pos.text  = $"P{i + 1}";
                towerRows[i].name.text = t.driverName;
                towerRows[i].time.text = t.bestLap > 0f ? _FormatLapTime(t.bestLap) : "--:--.---";
                if (towerRows[i].delta != null)
                    towerRows[i].delta.text = (i == 0 || t.bestLap <= 0f) ? string.Empty
                        : $"+{(t.bestLap - timings[0].bestLap):F3}";

                // Colour: player row = yellow, eliminated zone = red tint
                bool isPlayer = t.isPlayer;
                towerRows[i].name.color = isPlayer ? Color.yellow : Color.white;
            }
        }

        void _OnSessionEnd()
        {
            if (flagImage && checkeredSprite) flagImage.sprite = checkeredSprite;
        }

        static string _FormatLapTime(float t)
        {
            int   m  = Mathf.FloorToInt(t / 60f);
            float s  = t - m * 60f;
            return $"{m}:{s:00.000}";
        }
    }
}
