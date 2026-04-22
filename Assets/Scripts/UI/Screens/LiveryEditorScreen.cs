using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FormulaSim.Livery;
using FormulaSim.Teams;

namespace FormulaSim.UI.Screens
{
    public class LiveryEditorScreen : MonoBehaviour
    {
        [Header("Preview")]
        [SerializeField] SpriteRenderer carPreview;
        [SerializeField] Material       carMaterial;    // CarBody.shader instance

        [Header("Sliders")]
        [SerializeField] Slider primaryHueSlider;
        [SerializeField] Slider primarySatSlider;
        [SerializeField] Slider secondaryHueSlider;

        [Header("Buttons")]
        [SerializeField] Button[]  accentBtns;
        [SerializeField] Button[]  helmetBtns;
        [SerializeField] Button    saveBtn;
        [SerializeField] Button    resetBtn;

        [Header("Info")]
        [SerializeField] TMP_Text  teamNameLabel;
        [SerializeField] TMP_Text  unlockNotice;

        string     teamId;
        LiveryData livery;
        TeamRegistry teams;

        static readonly int _PrimaryColor   = Shader.PropertyToID("_PrimaryColor");
        static readonly int _SecondaryColor = Shader.PropertyToID("_SecondaryColor");

        void Start()
        {
            teams = FindObjectOfType<TeamRegistry>() ?? Resources.Load<TeamRegistry>("TeamRegistry");
            saveBtn.onClick.AddListener(OnSave);
            resetBtn.onClick.AddListener(OnReset);
            primaryHueSlider.onValueChanged.AddListener(_ => ApplyPreview());
            primarySatSlider.onValueChanged.AddListener(_ => ApplyPreview());
            secondaryHueSlider.onValueChanged.AddListener(_ => ApplyPreview());

            for (int i = 0; i < accentBtns.Length; i++)
            {
                int idx = i;
                accentBtns[i].onClick.AddListener(() => SelectAccent(idx));
            }
            for (int i = 0; i < helmetBtns.Length; i++)
            {
                int idx = i;
                helmetBtns[i].onClick.AddListener(() => SelectHelmet(idx));
            }
        }

        public void Open(string teamIdIn)
        {
            teamId = teamIdIn;
            livery = LiverySystem.GetDefault(teamId);
            gameObject.SetActive(true);

            var team = teams?.Get(teamId);
            teamNameLabel.text = team?.name ?? teamId;

            var limits = LiverySystem.GetLimits(teamId);
            primaryHueSlider.minValue = -limits.hueRange;
            primaryHueSlider.maxValue =  limits.hueRange;
            primaryHueSlider.value    = livery.primaryHueOffset;
            primarySatSlider.value    = livery.primarySaturation;
            secondaryHueSlider.value  = livery.secondaryHueOffset;

            ApplyPreview();
        }

        void ApplyPreview()
        {
            livery.primaryHueOffset   = primaryHueSlider.value;
            livery.primarySaturation  = primarySatSlider.value;
            livery.secondaryHueOffset = secondaryHueSlider.value;

            Color primary   = LiverySystem.GetPrimaryColor(teamId, livery);
            Color secondary = LiverySystem.GetSecondaryColor(teamId, livery);

            carMaterial.SetColor(_PrimaryColor,   primary);
            carMaterial.SetColor(_SecondaryColor, secondary);

            // Tiny scale pulse on preview
            carPreview.transform.localScale = Vector3.one * 1.025f;
            LeanTween.scale(carPreview.gameObject, Vector3.one, 0.15f).setEaseOutSine();
        }

        void SelectAccent(int idx)
        {
            livery.accentStyle = idx;
            for (int i = 0; i < accentBtns.Length; i++)
                accentBtns[i].image.color = (i == idx)
                    ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.7f);
            ApplyPreview();
        }

        void SelectHelmet(int idx)
        {
            livery.helmetStyle = idx;
            for (int i = 0; i < helmetBtns.Length; i++)
                helmetBtns[i].image.color = (i == idx)
                    ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.7f);
        }

        void OnSave()
        {
            LiverySystem.Save(teamId, livery);
            var flash = saveBtn.image.color;
            LeanTween.color(saveBtn.gameObject, new Color(0.2f, 1f, 0.4f), 0.2f)
                .setOnComplete(() => LeanTween.color(saveBtn.gameObject, flash, 0.3f));
        }

        void OnReset()
        {
            livery = LiverySystem.GetDefault(teamId);
            Open(teamId);
        }
    }
}
