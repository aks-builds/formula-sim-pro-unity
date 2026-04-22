using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FormulaSim.Race;

namespace FormulaSim.UI
{
    /// <summary>
    /// Post-race highlight reel screen.
    /// Shows a scrollable list of recorded highlights and lets the player
    /// watch any clip via ReplaySystem. Accessible from the post-race results screen.
    /// </summary>
    public class ReplayScreen : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] Transform   highlightListParent;
        [SerializeField] GameObject  highlightRowPrefab;   // needs TMP_Text label + Button
        [SerializeField] TMP_Text    emptyLabel;

        [Header("Playback Controls")]
        [SerializeField] Button      playLastBtn;           // "Play last 30s"
        [SerializeField] Button      stopBtn;
        [SerializeField] TMP_Text    nowPlayingText;

        [Header("Navigation")]
        [SerializeField] Button      closeBtn;
        [SerializeField] Button      backToResultsBtn;

        [Header("Playback Overlay")]
        [SerializeField] GameObject  playbackOverlay;       // dim + stop button shown during replay

        readonly List<GameObject> _rows = new();

        void Awake()
        {
            closeBtn?        .onClick.AddListener(_Close);
            backToResultsBtn?.onClick.AddListener(_Close);
            stopBtn?         .onClick.AddListener(_StopPlayback);
            playLastBtn?     .onClick.AddListener(() => _PlayLast(30f));

            if (playbackOverlay) playbackOverlay.SetActive(false);
        }

        void OnEnable()
        {
            _PopulateList();

            if (ReplaySystem.Instance)
            {
                ReplaySystem.Instance.OnPlaybackStarted += _OnPlaybackStarted;
                ReplaySystem.Instance.OnPlaybackEnded   += _OnPlaybackEnded;
                ReplaySystem.Instance.OnHighlightRecorded += _ => _PopulateList();
            }
        }

        void OnDisable()
        {
            if (ReplaySystem.Instance)
            {
                ReplaySystem.Instance.OnPlaybackStarted   -= _OnPlaybackStarted;
                ReplaySystem.Instance.OnPlaybackEnded     -= _OnPlaybackEnded;
                ReplaySystem.Instance.OnHighlightRecorded -= _ => _PopulateList();
            }
        }

        void _PopulateList()
        {
            // Clear old rows
            foreach (var r in _rows) Destroy(r);
            _rows.Clear();

            var highlights = ReplaySystem.Instance?.Highlights;
            bool empty = highlights == null || highlights.Count == 0;

            if (emptyLabel) emptyLabel.gameObject.SetActive(empty);
            if (empty) return;

            foreach (var h in highlights)
            {
                var row = _SpawnRow(h);
                _rows.Add(row);
            }
        }

        GameObject _SpawnRow(ReplaySystem.Highlight h)
        {
            var row = highlightRowPrefab != null
                ? Instantiate(highlightRowPrefab, highlightListParent)
                : new GameObject("Row", typeof(RectTransform));

            row.transform.SetParent(highlightListParent, false);

            // Label
            var label = row.GetComponentInChildren<TMP_Text>();
            if (label)
            {
                string icon = h.eventType switch
                {
                    "Overtake"    => "⟳",
                    "Crash"       => "💥",
                    "Battle"      => "⚔",
                    "FastestLap"  => "⚡",
                    _             => "•",
                };
                label.text = $"{icon}  {h.eventType}  —  {h.label}";
            }

            // Play button
            var btn = row.GetComponentInChildren<Button>();
            if (btn)
            {
                var capture = h;
                btn.onClick.AddListener(() => _PlayHighlight(capture));
            }

            return row;
        }

        void _PlayHighlight(ReplaySystem.Highlight h)
        {
            if (ReplaySystem.Instance == null) return;
            if (nowPlayingText) nowPlayingText.text = $"▶  {h.eventType}: {h.label}";
            ReplaySystem.Instance.PlayHighlight(h);
        }

        void _PlayLast(float seconds)
        {
            if (ReplaySystem.Instance == null) return;
            if (nowPlayingText) nowPlayingText.text = $"▶  Last {seconds:F0}s";
            ReplaySystem.Instance.PlayLastNSeconds(seconds);
        }

        void _StopPlayback()
        {
            ReplaySystem.Instance?.StopPlayback();
        }

        void _OnPlaybackStarted()
        {
            if (playbackOverlay) playbackOverlay.SetActive(true);
        }

        void _OnPlaybackEnded()
        {
            if (playbackOverlay) playbackOverlay.SetActive(false);
            if (nowPlayingText)  nowPlayingText.text = "";
        }

        void _Close()
        {
            ReplaySystem.Instance?.StopPlayback();
            gameObject.SetActive(false);
        }
    }
}
