using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FormulaSim.Cars;

namespace FormulaSim.UI
{
    /// <summary>
    /// Pre-race car setup screen. Shows wing/suspension/brake/diff sliders and
    /// a live stat preview panel. Accessible from RaceSelectionScreen.
    /// </summary>
    public class CarSetupScreen : MonoBehaviour
    {
        [Header("Setup Sliders")]
        [SerializeField] Slider     frontWingSlider;
        [SerializeField] Slider     rearWingSlider;
        [SerializeField] Slider     brakeBiasSlider;
        [SerializeField] Slider     differentialSlider;

        [Header("Suspension")]
        [SerializeField] Button softSuspBtn;
        [SerializeField] Button medSuspBtn;
        [SerializeField] Button stiffSuspBtn;

        [Header("Slider Labels")]
        [SerializeField] TMP_Text frontWingLabel;
        [SerializeField] TMP_Text rearWingLabel;
        [SerializeField] TMP_Text brakeBiasLabel;
        [SerializeField] TMP_Text differentialLabel;

        [Header("Stat Preview")]
        [SerializeField] TMP_Text topSpeedText;
        [SerializeField] TMP_Text downforceText;
        [SerializeField] TMP_Text tireWearText;
        [SerializeField] TMP_Text tractionText;
        [SerializeField] Image    topSpeedBar;
        [SerializeField] Image    downforceBar;

        [Header("Presets")]
        [SerializeField] Button balancedBtn;
        [SerializeField] Button monacoBtn;
        [SerializeField] Button monzaBtn;
        [SerializeField] Button wetBtn;

        [Header("Navigation")]
        [SerializeField] Button saveBtn;
        [SerializeField] Button resetBtn;
        [SerializeField] Button closeBtn;

        CarSetupData _working;
        string       _circuitId;

        void Awake()
        {
            frontWingSlider?   .onValueChanged.AddListener(v => { _working.frontWing  = (int)v; _Refresh(); });
            rearWingSlider?    .onValueChanged.AddListener(v => { _working.rearWing   = (int)v; _Refresh(); });
            brakeBiasSlider?   .onValueChanged.AddListener(v => { _working.brakeBias  = v;      _Refresh(); });
            differentialSlider?.onValueChanged.AddListener(v => { _working.differential = (int)v; _Refresh(); });

            softSuspBtn? .onClick.AddListener(() => { _working.suspension = SuspensionSetting.Soft;   _HighlightSusp(); _Refresh(); });
            medSuspBtn?  .onClick.AddListener(() => { _working.suspension = SuspensionSetting.Medium; _HighlightSusp(); _Refresh(); });
            stiffSuspBtn?.onClick.AddListener(() => { _working.suspension = SuspensionSetting.Stiff;  _HighlightSusp(); _Refresh(); });

            balancedBtn?.onClick.AddListener(() => _LoadPreset(CarSetupData.Balanced()));
            monacoBtn?  .onClick.AddListener(() => _LoadPreset(CarSetupData.MonacoSetup()));
            monzaBtn?   .onClick.AddListener(() => _LoadPreset(CarSetupData.MonzaSetup()));
            wetBtn?     .onClick.AddListener(() => _LoadPreset(CarSetupData.WetSetup()));

            saveBtn? .onClick.AddListener(_Save);
            resetBtn?.onClick.AddListener(() => _LoadPreset(CarSetupData.Balanced()));
            closeBtn?.onClick.AddListener(() => gameObject.SetActive(false));
        }

        public void Open(string circuitId)
        {
            _circuitId = circuitId;
            _working   = new CarSetupData();

            var cs = FindObjectOfType<CarSetup>();
            if (cs != null)
            {
                cs.Load(circuitId);
                _working = cs.Setup;
            }

            gameObject.SetActive(true);
            _LoadToSliders();
            _Refresh();
        }

        void _LoadToSliders()
        {
            if (frontWingSlider)    { frontWingSlider.minValue = 0; frontWingSlider.maxValue = 11; frontWingSlider.value = _working.frontWing; }
            if (rearWingSlider)     { rearWingSlider.minValue  = 0; rearWingSlider.maxValue  = 11; rearWingSlider.value  = _working.rearWing; }
            if (brakeBiasSlider)    { brakeBiasSlider.minValue = 50; brakeBiasSlider.maxValue = 70; brakeBiasSlider.value = _working.brakeBias; }
            if (differentialSlider) { differentialSlider.minValue = 0; differentialSlider.maxValue = 100; differentialSlider.value = _working.differential; }
            _HighlightSusp();
        }

        void _LoadPreset(CarSetupData preset)
        {
            _working = preset;
            _LoadToSliders();
            _Refresh();
        }

        void _Refresh()
        {
            // Labels
            if (frontWingLabel)    frontWingLabel.text    = $"Front Wing: {_working.frontWing}";
            if (rearWingLabel)     rearWingLabel.text     = $"Rear Wing: {_working.rearWing}";
            if (brakeBiasLabel)    brakeBiasLabel.text    = $"Brake Bias: {_working.brakeBias:F0}% front";
            if (differentialLabel) differentialLabel.text = $"Differential: {_working.differential}%";

            // Stats
            int topSpeed   = _working.TopSpeedIndex;
            int cornering  = _working.CorneringIndex;
            int tireWear   = _working.TireWearIndex;
            int traction   = _working.TractionIndex;

            if (topSpeedText)  topSpeedText.text  = $"Top Speed:  {topSpeed}";
            if (downforceText) downforceText.text  = $"Cornering:  {cornering}";
            if (tireWearText)  tireWearText.text   = $"Tyre Wear:  {tireWear}";
            if (tractionText)  tractionText.text   = $"Traction:   {traction}";

            if (topSpeedBar)  topSpeedBar.fillAmount  = topSpeed  / 130f;
            if (downforceBar) downforceBar.fillAmount = cornering / 130f;
        }

        void _HighlightSusp()
        {
            Color active   = new(0.2f, 0.9f, 0.4f, 1f);
            Color inactive = new(0.4f, 0.4f, 0.4f, 1f);

            if (softSuspBtn)  softSuspBtn .image.color = _working.suspension == SuspensionSetting.Soft   ? active : inactive;
            if (medSuspBtn)   medSuspBtn  .image.color = _working.suspension == SuspensionSetting.Medium ? active : inactive;
            if (stiffSuspBtn) stiffSuspBtn.image.color = _working.suspension == SuspensionSetting.Stiff  ? active : inactive;
        }

        void _Save()
        {
            var cs = FindObjectOfType<CarSetup>();
            if (cs != null)
            {
                cs.ApplySetup(_working);
                cs.Save(_circuitId);
            }
            gameObject.SetActive(false);
        }
    }
}
