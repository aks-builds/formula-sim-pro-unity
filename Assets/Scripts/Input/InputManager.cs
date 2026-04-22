using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FormulaSim.Input
{
    /// <summary>
    /// Unified input manager: aggregates touch buttons, tilt, and gamepad/keyboard.
    /// Exposes clean throttle/brake/steer values consumed by F1CarController.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Input Mode")]
        public InputMode mode = InputMode.Touch;
        [SerializeField] bool tiltEnabled = false;

        [Header("Touch Sensitivity")]
        [SerializeField] float steerDeadzone  = 0.05f;
        [SerializeField] float steerSensitivity = 1.0f;

        [Header("Tilt Steering")]
        [SerializeField] float tiltNeutralAngle = 0f;     // degrees (calibrate on session start)
        [SerializeField] float tiltMaxAngle     = 30f;
        [SerializeField] float tiltSmoothSpeed  = 8f;

        [Header("Gamepad")]
        [SerializeField] float gamepadSteerSensitivity = 1.0f;

        // ── Output (read by F1CarController) ──────────────────────────────────
        public float Throttle    { get; private set; }
        public float Brake       { get; private set; }
        public float Steer       { get; private set; }
        public bool  DrsPressed  { get; private set; }
        public bool  ERSBurst    { get; private set; }
        public bool  ERSCycle    { get; private set; }
        public bool  PausePressed{ get; private set; }
        public bool  CameraSwitch{ get; private set; }

        // Touch button states (set by MobileControlsUI)
        [HideInInspector] public float TouchThrottle;
        [HideInInspector] public float TouchBrake;
        [HideInInspector] public float TouchSteer;     // from touch joystick or swipe
        [HideInInspector] public bool  TouchDRS;
        [HideInInspector] public bool  TouchERSBurst;

        float tiltSteer;
        float smoothedSteer;
        Accelerometer accel;

        public event Action OnPause;
        public event Action OnCameraSwitch;

        public enum InputMode { Touch, Tilt, Gamepad, Keyboard }

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (tiltEnabled && SystemInfo.supportsAccelerometer)
            {
                accel = Accelerometer.current;
                if (accel != null) InputSystem.EnableDevice(accel);
            }
        }

        void Update()
        {
            _ReadGamepad();
            _ReadKeyboard();
            _ReadTilt();
            _Aggregate();
        }

        void _ReadGamepad()
        {
            var gp = Gamepad.current;
            if (gp == null) return;
            if (mode == InputMode.Gamepad)
            {
                TouchThrottle = gp.rightTrigger.ReadValue();
                TouchBrake    = gp.leftTrigger.ReadValue();
                TouchSteer    = gp.leftStick.x.ReadValue() * gamepadSteerSensitivity;
                TouchDRS      = gp.southButton.wasPressedThisFrame;
                TouchERSBurst = gp.westButton.wasPressedThisFrame;
            }
            PausePressed  = gp.startButton.wasPressedThisFrame;
            CameraSwitch  = gp.selectButton.wasPressedThisFrame;
        }

        void _ReadKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (mode == InputMode.Keyboard)
            {
                TouchThrottle = kb.upArrowKey.isPressed || kb.wKey.isPressed ? 1f : 0f;
                TouchBrake    = kb.downArrowKey.isPressed || kb.sKey.isPressed ? 1f : 0f;
                float rawSteer= (kb.leftArrowKey.isPressed || kb.aKey.isPressed ? -1f : 0f)
                              + (kb.rightArrowKey.isPressed || kb.dKey.isPressed ? 1f : 0f);
                TouchSteer    = rawSteer;
                TouchDRS      = kb.eKey.wasPressedThisFrame;
                TouchERSBurst = kb.qKey.wasPressedThisFrame;
            }
            PausePressed  |= kb.escapeKey.wasPressedThisFrame;
            CameraSwitch  |= kb.cKey.wasPressedThisFrame;
        }

        void _ReadTilt()
        {
            if (!tiltEnabled || accel == null || mode != InputMode.Tilt) return;
            Vector3 a       = accel.acceleration.ReadValue();
            float   angle   = Mathf.Atan2(a.x, -a.z) * Mathf.Rad2Deg;
            float   relative= angle - tiltNeutralAngle;
            tiltSteer       = Mathf.Clamp(relative / tiltMaxAngle, -1f, 1f);
            TouchSteer      = tiltSteer;
        }

        void _Aggregate()
        {
            float steerRaw = TouchSteer;
            if (Mathf.Abs(steerRaw) < steerDeadzone) steerRaw = 0f;
            else steerRaw = Mathf.Sign(steerRaw) * (Mathf.Abs(steerRaw) - steerDeadzone) / (1f - steerDeadzone);

            smoothedSteer = Mathf.Lerp(smoothedSteer, steerRaw * steerSensitivity, Time.deltaTime * tiltSmoothSpeed);

            Throttle    = Mathf.Clamp01(TouchThrottle);
            Brake       = Mathf.Clamp01(TouchBrake);
            Steer       = Mathf.Clamp(smoothedSteer, -1f, 1f);
            DrsPressed  = TouchDRS;
            ERSBurst    = TouchERSBurst;

            if (PausePressed)  OnPause?.Invoke();
            if (CameraSwitch)  OnCameraSwitch?.Invoke();
        }

        public void CalibrateTilt()
        {
            if (accel == null) return;
            var a = accel.acceleration.ReadValue();
            tiltNeutralAngle = Mathf.Atan2(a.x, -a.z) * Mathf.Rad2Deg;
        }

        public void SetMode(InputMode m) => mode = m;
    }
}
