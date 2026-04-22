using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FormulaSim.Cars;

namespace FormulaSim.Race
{
    /// <summary>
    /// Records a rolling 90-second frame buffer of all car positions.
    /// Extracts highlight moments (overtakes, crashes, battles) automatically.
    /// Supports playback mode: freezes physics and replays car transforms
    /// while a free-roaming replay camera films the action.
    /// </summary>
    public class ReplaySystem : MonoBehaviour
    {
        public static ReplaySystem Instance { get; private set; }

        [Header("Recording")]
        [SerializeField] float recordHz     = 20f;    // frames per second to record
        [SerializeField] float maxBufferSec = 90f;    // seconds of rolling buffer

        [Header("Playback")]
        [SerializeField] Camera        replayCam;
        [SerializeField] float         replayCamSpeed = 18f;
        [SerializeField] Transform     replayCamTarget;

        // ── Data types ────────────────────────────────────────────────────────

        struct CarState
        {
            public Vector3 position;
            public float   rotation;
            public float   speed;
            public int     gear;
        }

        struct ReplayFrame
        {
            public float    timestamp;
            public CarState[] cars;
        }

        public class Highlight
        {
            public string eventType;   // "Overtake", "Crash", "Battle", "FastestLap"
            public float  timestamp;
            public int    primaryCarIdx;
            public int    secondaryCarIdx;
            public string label;
        }

        // ── Internal state ────────────────────────────────────────────────────
        ReplayFrame[]  _buffer;
        int            _bufferHead;
        int            _bufferCount;
        int            _maxFrames;
        float          _recordInterval;
        float          _recordTimer;
        bool           _isRecording;
        bool           _isPlayingBack;

        List<F1CarController> _cars = new();
        int[]                 _lastPositions;
        List<Highlight>       _highlights = new();

        public bool         IsPlayingBack => _isPlayingBack;
        public List<Highlight> Highlights => _highlights;

        // ── Events ────────────────────────────────────────────────────────────
        public event System.Action<Highlight>  OnHighlightRecorded;
        public event System.Action             OnPlaybackStarted;
        public event System.Action             OnPlaybackEnded;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            _recordInterval = 1f / recordHz;
            _maxFrames      = Mathf.RoundToInt(maxBufferSec * recordHz);
            _buffer         = new ReplayFrame[_maxFrames];

            if (replayCam) replayCam.gameObject.SetActive(false);
        }

        public void StartRecording(List<F1CarController> cars)
        {
            _cars         = new List<F1CarController>(cars);
            _lastPositions = new int[_cars.Count];
            _isRecording  = true;
        }

        public void StopRecording() => _isRecording = false;

        void Update()
        {
            if (!_isRecording || _isPlayingBack) return;

            _recordTimer += Time.deltaTime;
            if (_recordTimer < _recordInterval) return;
            _recordTimer = 0f;

            _RecordFrame();
        }

        // ── Recording ─────────────────────────────────────────────────────────

        void _RecordFrame()
        {
            var frame = new ReplayFrame
            {
                timestamp = Time.time,
                cars      = new CarState[_cars.Count],
            };

            for (int i = 0; i < _cars.Count; i++)
            {
                if (_cars[i] == null) continue;
                frame.cars[i] = new CarState
                {
                    position = _cars[i].transform.position,
                    rotation = _cars[i].transform.eulerAngles.z,
                    speed    = _cars[i].SpeedMs,
                    gear     = _cars[i].Gear,
                };
            }

            _buffer[_bufferHead] = frame;
            _bufferHead          = (_bufferHead + 1) % _maxFrames;
            if (_bufferCount < _maxFrames) _bufferCount++;

            _CheckForHighlights(frame);
        }

        void _CheckForHighlights(ReplayFrame frame)
        {
            var rm = RaceManager.Instance;
            if (rm == null) return;

            // Detect position changes (overtakes)
            for (int i = 0; i < _cars.Count; i++)
            {
                if (_cars[i] == null) continue;
                var entry = rm.Cars.Find(e => e.Controller == _cars[i]);
                if (entry == null) continue;

                int prevPos = _lastPositions[i];
                int curPos  = entry.RacePosition;

                if (prevPos > 0 && curPos < prevPos)   // position improved = overtake made
                {
                    var h = new Highlight
                    {
                        eventType      = "Overtake",
                        timestamp      = frame.timestamp,
                        primaryCarIdx  = i,
                        label          = $"P{prevPos} → P{curPos}",
                    };
                    _AddHighlight(h);
                }
                _lastPositions[i] = curPos;
            }

            // Detect crashes (sudden speed drop from >20 m/s to <5 m/s in 1 frame)
            for (int i = 0; i < _cars.Count; i++)
            {
                if (i >= frame.cars.Length) break;
                if (_bufferCount < 2) break;

                int prev = (_bufferHead - 2 + _maxFrames) % _maxFrames;
                if (prev >= _buffer.Length || _buffer[prev].cars == null) continue;
                if (i >= _buffer[prev].cars.Length) continue;

                float prevSpeed = _buffer[prev].cars[i].speed;
                float curSpeed  = frame.cars[i].speed;
                if (prevSpeed > 20f && curSpeed < 5f)
                {
                    var h = new Highlight
                    {
                        eventType     = "Crash",
                        timestamp     = frame.timestamp,
                        primaryCarIdx = i,
                        label         = "Heavy impact",
                    };
                    _AddHighlight(h);
                }
            }
        }

        void _AddHighlight(Highlight h)
        {
            _highlights.Add(h);
            if (_highlights.Count > 50) _highlights.RemoveAt(0);   // cap list
            OnHighlightRecorded?.Invoke(h);
        }

        // ── Playback ──────────────────────────────────────────────────────────

        public void PlayHighlight(Highlight h)
        {
            if (_isPlayingBack) return;
            float preTime  = h.timestamp - 10f;   // 10s before the event
            float postTime = h.timestamp + 5f;    // 5s after
            StartCoroutine(_PlaybackCoroutine(preTime, postTime, h.primaryCarIdx));
        }

        public void PlayLastNSeconds(float seconds)
        {
            if (_isPlayingBack) return;
            float endTime   = Time.time;
            float startTime = endTime - seconds;
            StartCoroutine(_PlaybackCoroutine(startTime, endTime, 0));
        }

        IEnumerator _PlaybackCoroutine(float startTime, float endTime, int focusCar)
        {
            _isPlayingBack = true;
            Time.timeScale = 0f;
            if (replayCam) replayCam.gameObject.SetActive(true);
            OnPlaybackStarted?.Invoke();

            // Collect frames in time range
            var frames = new List<ReplayFrame>();
            for (int i = 0; i < _bufferCount; i++)
            {
                int idx = (_bufferHead - _bufferCount + i + _maxFrames) % _maxFrames;
                var f   = _buffer[idx];
                if (f.timestamp >= startTime && f.timestamp <= endTime)
                    frames.Add(f);
            }

            // Replay them
            foreach (var frame in frames)
            {
                for (int i = 0; i < _cars.Count && i < frame.cars.Length; i++)
                {
                    if (_cars[i] == null) continue;
                    _cars[i].transform.SetPositionAndRotation(
                        frame.cars[i].position,
                        Quaternion.Euler(0, 0, frame.cars[i].rotation));
                }

                // Move replay cam toward focus car
                if (replayCam && focusCar < _cars.Count && _cars[focusCar] != null)
                {
                    Vector3 target = _cars[focusCar].transform.position + Vector3.back * 15f + Vector3.up * 8f;
                    replayCam.transform.position = Vector3.MoveTowards(
                        replayCam.transform.position, target, replayCamSpeed * Time.unscaledDeltaTime);
                    replayCam.transform.LookAt(_cars[focusCar].transform);
                }

                yield return new WaitForSecondsRealtime(_recordInterval);
            }

            // End playback
            Time.timeScale = 1f;
            if (replayCam) replayCam.gameObject.SetActive(false);
            _isPlayingBack = false;
            OnPlaybackEnded?.Invoke();
        }

        public void StopPlayback()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            if (replayCam) replayCam.gameObject.SetActive(false);
            _isPlayingBack = false;
            OnPlaybackEnded?.Invoke();
        }
    }
}
