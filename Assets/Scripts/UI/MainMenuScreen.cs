using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FormulaSim.Core;

namespace FormulaSim.UI
{
    public class MainMenuScreen : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] Button quickRaceBtn;
        [SerializeField] Button careerBtn;
        [SerializeField] Button championshipBtn;
        [SerializeField] Button liveryBtn;
        [SerializeField] Button settingsBtn;
        [SerializeField] Button quitBtn;

        [Header("Version")]
        [SerializeField] TMP_Text versionLabel;

        [Header("Panels")]
        [SerializeField] GameObject settingsPanel;
        [SerializeField] Slider     masterVolumeSlider;
        [SerializeField] Slider     sfxVolumeSlider;
        [SerializeField] Slider     aiDifficultySlider;
        [SerializeField] TMP_Text   aiDifficultyLabel;
        [SerializeField] Toggle     motionBlurToggle;
        [SerializeField] Toggle     bloomToggle;
        [SerializeField] TMP_Dropdown qualityDropdown;

        void Start()
        {
            if (versionLabel) versionLabel.text = $"v{Application.version}";

            quickRaceBtn      .onClick.AddListener(OnQuickRace);
            careerBtn         .onClick.AddListener(OnCareer);
            championshipBtn   .onClick.AddListener(OnChampionship);
            liveryBtn         .onClick.AddListener(OnLivery);
            settingsBtn       .onClick.AddListener(ToggleSettings);
            quitBtn           .onClick.AddListener(OnQuit);

            if (settingsPanel) settingsPanel.SetActive(false);
            _LoadSettings();
        }

        void OnQuickRace()
        {
            GameManager.Instance.TransitionTo(GameState.RaceWeekend);
            // RaceSelectionScreen will load as part of the RaceWeekend scene
        }

        void OnCareer()       => GameManager.Instance.TransitionTo(GameState.Career);
        void OnChampionship() => GameManager.Instance.TransitionTo(GameState.Championship);
        void OnLivery()       => GameManager.Instance.TransitionTo(GameState.LiveryEditor);

        void ToggleSettings()
        {
            if (!settingsPanel) return;
            bool next = !settingsPanel.activeSelf;
            settingsPanel.SetActive(next);
            if (next) _LoadSettings();
        }

        void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Settings ──────────────────────────────────────────────────────────

        void _LoadSettings()
        {
            var gs = Core.GameSettings.Instance;
            if (gs != null) gs.Load();

            if (masterVolumeSlider) masterVolumeSlider.value = gs?.masterVolume  ?? PlayerPrefs.GetFloat("vol_master", 1f);
            if (sfxVolumeSlider)    sfxVolumeSlider.value    = gs?.sfxVolume     ?? PlayerPrefs.GetFloat("vol_sfx",    1f);
            if (motionBlurToggle)   motionBlurToggle.isOn    = gs?.motionBlurEnabled ?? (PlayerPrefs.GetInt("mb", 1) == 1);
            if (bloomToggle)        bloomToggle.isOn         = gs?.bloomEnabled   ?? (PlayerPrefs.GetInt("bloom", 1) == 1);
            if (aiDifficultySlider) aiDifficultySlider.value = gs?.aiDifficulty   ?? 0.5f;
            if (qualityDropdown)    qualityDropdown.value    = QualitySettings.GetQualityLevel();

            _UpdateDifficultyLabel(aiDifficultySlider != null ? aiDifficultySlider.value : 0.5f);

            masterVolumeSlider?.onValueChanged.AddListener(v =>
            {
                AudioListener.volume = v;
                if (gs != null) { gs.masterVolume = v; gs.Save(); }
                else PlayerPrefs.SetFloat("vol_master", v);
            });
            sfxVolumeSlider?.onValueChanged.AddListener(v =>
            {
                if (gs != null) { gs.sfxVolume = v; gs.Save(); }
                else PlayerPrefs.SetFloat("vol_sfx", v);
            });
            aiDifficultySlider?.onValueChanged.AddListener(v =>
            {
                if (gs != null) { gs.aiDifficulty = v; gs.Save(); }
                _UpdateDifficultyLabel(v);
            });
            motionBlurToggle?.onValueChanged.AddListener(v =>
            {
                if (gs != null) { gs.motionBlurEnabled = v; gs.Save(); }
                else PlayerPrefs.SetInt("mb", v ? 1 : 0);
            });
            bloomToggle?.onValueChanged.AddListener(v =>
            {
                if (gs != null) { gs.bloomEnabled = v; gs.Save(); }
                else PlayerPrefs.SetInt("bloom", v ? 1 : 0);
            });
            qualityDropdown?.onValueChanged.AddListener(v => QualitySettings.SetQualityLevel(v));
        }

        void _UpdateDifficultyLabel(float value)
        {
            if (!aiDifficultyLabel) return;
            string label = value switch
            {
                < 0.2f => "NOVICE",
                < 0.4f => "BEGINNER",
                < 0.6f => "INTERMEDIATE",
                < 0.8f => "EXPERT",
                _      => "LEGENDARY",
            };
            aiDifficultyLabel.text = label;
        }
    }
}
