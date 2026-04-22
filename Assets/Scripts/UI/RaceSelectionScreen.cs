using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FormulaSim.Core;
using FormulaSim.Tracks;

namespace FormulaSim.UI
{
    /// <summary>
    /// Circuit picker + session type selector (FP1/FP2/FP3, Q, Race).
    /// Populate circuitList from Resources/Circuits or Addressables.
    /// </summary>
    public class RaceSelectionScreen : MonoBehaviour
    {
        [Header("Circuit List")]
        [SerializeField] Transform      circuitListRoot;
        [SerializeField] GameObject     circuitItemPrefab;   // has TMP_Text + Button
        [SerializeField] TMP_Text       selectedCircuitLabel;
        [SerializeField] Image          circuitPreviewImage;

        [Header("Session")]
        [SerializeField] Button         fp1Btn, fp2Btn, fp3Btn;
        [SerializeField] Button         qualiBtn, raceBtn;

        [Header("Car / Team")]
        [SerializeField] TMP_Dropdown   teamDropdown;
        [SerializeField] TMP_Dropdown   driverDropdown;

        [Header("Assists")]
        [SerializeField] Toggle tcToggle, absToggle, steerAssistToggle;

        [Header("Navigation")]
        [SerializeField] Button backBtn;
        [SerializeField] Button launchBtn;

        CircuitData    selectedCircuit;
        GameState      selectedSession = GameState.Racing;

        static readonly (string label, GameState state)[] SESSION_TYPES =
        {
            ("FP1", GameState.RaceWeekend),
            ("FP2", GameState.RaceWeekend),
            ("FP3", GameState.RaceWeekend),
            ("Qualifying", GameState.Qualifying),
            ("Race",       GameState.Racing),
        };

        void Start()
        {
            var circuits = Resources.LoadAll<CircuitData>("Circuits");
            _PopulateCircuits(circuits);

            fp1Btn  ?.onClick.AddListener(() => selectedSession = GameState.RaceWeekend);
            fp2Btn  ?.onClick.AddListener(() => selectedSession = GameState.RaceWeekend);
            fp3Btn  ?.onClick.AddListener(() => selectedSession = GameState.RaceWeekend);
            qualiBtn?.onClick.AddListener(() => selectedSession = GameState.Qualifying);
            raceBtn ?.onClick.AddListener(() => selectedSession = GameState.Racing);

            backBtn  ?.onClick.AddListener(() => GameManager.Instance.TransitionTo(GameState.MainMenu));
            launchBtn?.onClick.AddListener(_Launch);

            _LoadAssistToggles();
        }

        void _PopulateCircuits(IEnumerable<CircuitData> circuits)
        {
            foreach (Transform child in circuitListRoot) Destroy(child.gameObject);

            foreach (var c in circuits)
            {
                var item = Instantiate(circuitItemPrefab, circuitListRoot);
                item.GetComponentInChildren<TMP_Text>().text = c.displayName;   // fix: was c.circuitName
                item.GetComponent<Button>().onClick.AddListener(() => _SelectCircuit(c));
            }
        }

        void _SelectCircuit(CircuitData c)
        {
            selectedCircuit = c;
            if (selectedCircuitLabel) selectedCircuitLabel.text = c.displayName;  // fix: was c.circuitName
            // previewSprite removed — CircuitData has no such property; assign via inspector
        }

        void _Launch()
        {
            if (selectedCircuit == null)
            {
                Debug.LogWarning("[RaceSelection] No circuit selected.");
                return;
            }

            PlayerPrefs.SetString("selected_circuit", selectedCircuit.circuitId);
            PlayerPrefs.SetInt("tc",          tcToggle          ? (tcToggle.isOn          ? 1 : 0) : 1);
            PlayerPrefs.SetInt("abs",         absToggle         ? (absToggle.isOn         ? 1 : 0) : 1);
            PlayerPrefs.SetInt("steerAssist", steerAssistToggle ? (steerAssistToggle.isOn ? 1 : 0) : 1);
            PlayerPrefs.Save();

            GameManager.Instance.TransitionTo(selectedSession);
        }

        void _LoadAssistToggles()
        {
            if (tcToggle)          tcToggle.isOn          = PlayerPrefs.GetInt("tc",          1) == 1;
            if (absToggle)         absToggle.isOn         = PlayerPrefs.GetInt("abs",         1) == 1;
            if (steerAssistToggle) steerAssistToggle.isOn = PlayerPrefs.GetInt("steerAssist", 1) == 1;
        }
    }
}
