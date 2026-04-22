using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using FormulaSim.Cars;

namespace FormulaSim.Input
{
    /// <summary>
    /// Full mobile HUD control overlay.
    /// Layout: left = brake button | center = steering wheel joystick | right = throttle button
    /// Secondary buttons: DRS, ERS Burst, ERS Mode cycle, Camera switch.
    /// </summary>
    public class MobileControlsUI : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [Header("Main Controls")]
        [SerializeField] RectTransform throttleBtn;
        [SerializeField] RectTransform brakeBtn;
        [SerializeField] RectTransform steeringWheelZone;    // drag zone for steer
        [SerializeField] RectTransform steeringWheelKnob;    // visual indicator

        [Header("Secondary Buttons")]
        [SerializeField] Button drsButton;
        [SerializeField] Button ersBurstButton;
        [SerializeField] Button ersModeButton;
        [SerializeField] Button cameraButton;
        [SerializeField] Button pauseButton;

        [Header("ERS Display")]
        [SerializeField] Image      ersBatteryBar;
        [SerializeField] TMP_Text   ersModeLabel;
        [SerializeField] TMP_Text   drsLabel;

        [Header("Tilt Toggle")]
        [SerializeField] Toggle tiltToggle;

        // ── State ──────────────────────────────────────────────────────────────
        int     throttlePointerId = -1;
        int     brakePointerId    = -1;
        int     steerPointerId    = -1;
        Vector2 steerOrigin;
        float   steerZoneHalfW;

        InputManager   input;
        ERSSystem      ers;
        F1CarController car;

        static readonly Color DRS_ON_COLOR  = new(0.2f, 0.9f, 1f, 0.9f);
        static readonly Color DRS_OFF_COLOR = new(0.4f, 0.4f, 0.4f, 0.6f);
        static readonly Color ERS_FULL      = new(0.2f, 1f, 0.4f);
        static readonly Color ERS_LOW       = new(1f, 0.3f, 0.1f);

        void Start()
        {
            input  = InputManager.Instance;
            car    = FindObjectOfType<F1CarController>();
            ers    = car?.GetComponent<ERSSystem>();

            steerZoneHalfW = steeringWheelZone.rect.width * 0.5f;

            drsButton.onClick.AddListener(()    => input.TouchDRS      = true);
            ersBurstButton.onClick.AddListener(()=> input.TouchERSBurst = true);
            ersModeButton.onClick.AddListener(() => ers?.CycleMode());
            cameraButton.onClick.AddListener(()  => FindObjectOfType<FX.CameraSystem>()?.CycleCamera());
            pauseButton.onClick.AddListener(()   => Core.GameManager.Instance.TogglePause());

            if (tiltToggle)
            {
                tiltToggle.onValueChanged.AddListener(on =>
                {
                    input.SetMode(on ? InputManager.InputMode.Tilt : InputManager.InputMode.Touch);
                    if (on) input.CalibrateTilt();
                });
            }
        }

        void Update()
        {
            // Reset one-shot flags
            input.TouchDRS      = false;
            input.TouchERSBurst = false;

            // Update ERS display
            if (ers != null)
            {
                ersBatteryBar.fillAmount = ers.BatteryPercent;
                ersBatteryBar.color = Color.Lerp(ERS_LOW, ERS_FULL, ers.BatteryPercent);
                ersModeLabel.text   = ers.CurrentMode.ToString().ToUpper();
            }

            // DRS light
            if (car != null)
                drsButton.image.color = car.DrsActive ? DRS_ON_COLOR : DRS_OFF_COLOR;

            // Knob position feedback
            if (steeringWheelKnob)
            {
                float t = input.TouchSteer;
                var zonePos = steeringWheelZone.anchoredPosition;
                steeringWheelKnob.anchoredPosition = new Vector2(
                    zonePos.x + t * steerZoneHalfW * 0.85f,
                    steeringWheelKnob.anchoredPosition.y);
            }
        }

        // ── Touch handling ────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData e)
        {
            Vector2 pos = e.position;
            if (RectContains(throttleBtn, pos) && throttlePointerId < 0)
            {
                throttlePointerId  = e.pointerId;
                input.TouchThrottle = 1f;
            }
            else if (RectContains(brakeBtn, pos) && brakePointerId < 0)
            {
                brakePointerId  = e.pointerId;
                input.TouchBrake = 1f;
            }
            else if (RectContains(steeringWheelZone, pos) && steerPointerId < 0)
            {
                steerPointerId = e.pointerId;
                steerOrigin    = pos;
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == steerPointerId)
            {
                float delta = (e.position.x - steerOrigin.x) / steerZoneHalfW;
                input.TouchSteer = Mathf.Clamp(delta, -1f, 1f);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == throttlePointerId) { throttlePointerId = -1; input.TouchThrottle = 0f; }
            if (e.pointerId == brakePointerId)    { brakePointerId    = -1; input.TouchBrake    = 0f; }
            if (e.pointerId == steerPointerId)
            {
                steerPointerId   = -1;
                // Spring steer back to centre
                input.TouchSteer = 0f;
            }
        }

        static bool RectContains(RectTransform rt, Vector2 screenPos)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);
        }
    }
}
