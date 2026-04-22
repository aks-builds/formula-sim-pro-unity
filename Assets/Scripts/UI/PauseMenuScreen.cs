using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FormulaSim.Core;

namespace FormulaSim.UI
{
    public class PauseMenuScreen : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] Button resumeBtn;
        [SerializeField] Button restartBtn;
        [SerializeField] Button settingsBtn;
        [SerializeField] Button mainMenuBtn;

        [Header("Sliders (in-race quick settings)")]
        [SerializeField] Slider masterVolumeSlider;
        [SerializeField] Slider sfxVolumeSlider;
        [SerializeField] Toggle tcToggle;
        [SerializeField] Toggle absToggle;

        [Header("Stats Panel")]
        [SerializeField] TMP_Text lapLabel;
        [SerializeField] TMP_Text positionLabel;
        [SerializeField] TMP_Text tyreLabel;
        [SerializeField] TMP_Text ersLabel;

        [Header("Settings Panel")]
        [SerializeField] GameObject settingsPanel;

        Race.RaceManager raceManager;

        void Awake()
        {
            resumeBtn   .onClick.AddListener(_Resume);
            restartBtn  .onClick.AddListener(_Restart);
            settingsBtn .onClick.AddListener(_ToggleSettings);
            mainMenuBtn .onClick.AddListener(_GoMainMenu);

            if (settingsPanel) settingsPanel.SetActive(false);

            masterVolumeSlider?.onValueChanged.AddListener(v =>
            {
                AudioListener.volume = v;
                PlayerPrefs.SetFloat("vol_master", v);
            });

            sfxVolumeSlider?.onValueChanged.AddListener(v => PlayerPrefs.SetFloat("vol_sfx", v));

            tcToggle?.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetInt("tc", v ? 1 : 0);
                _ApplyAssists();
            });

            absToggle?.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetInt("abs", v ? 1 : 0);
                _ApplyAssists();
            });
        }

        void OnEnable()
        {
            raceManager = FindObjectOfType<Race.RaceManager>();
            _RefreshStats();

            if (masterVolumeSlider) masterVolumeSlider.value = PlayerPrefs.GetFloat("vol_master", 1f);
            if (sfxVolumeSlider)    sfxVolumeSlider.value    = PlayerPrefs.GetFloat("vol_sfx", 1f);
            if (tcToggle)           tcToggle.isOn            = PlayerPrefs.GetInt("tc",  1) == 1;
            if (absToggle)          absToggle.isOn           = PlayerPrefs.GetInt("abs", 1) == 1;
        }

        void _Resume()
        {
            GameManager.Instance.TogglePause();
        }

        void _Restart()
        {
            GameManager.Instance.TransitionTo(GameState.Formation);
        }

        void _GoMainMenu()
        {
            Time.timeScale = 1f;
            GameManager.Instance.TransitionTo(GameState.MainMenu);
        }

        void _ToggleSettings()
        {
            if (settingsPanel) settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        void _RefreshStats()
        {
            if (raceManager == null) return;

            var entry = raceManager.GetPlayerEntry();
            if (entry == null) return;

            if (lapLabel)      lapLabel.text      = $"Lap {entry.LapsComplete + 1}";
            if (positionLabel) positionLabel.text = $"P{entry.RacePosition}";
        }

        void _ApplyAssists()
        {
            var assists = FindObjectOfType<Cars.AssistSystem>();
            if (assists == null) return;
            assists.tcEnabled  = PlayerPrefs.GetInt("tc",  1) == 1;
            assists.absEnabled = PlayerPrefs.GetInt("abs", 1) == 1;
        }
    }
}
