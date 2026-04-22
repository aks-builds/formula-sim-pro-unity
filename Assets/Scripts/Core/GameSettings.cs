using UnityEngine;

namespace FormulaSim.Core
{
    /// <summary>
    /// Global game settings ScriptableObject.
    /// Assign one instance to GameManager. All systems read from GameSettings.Instance.
    /// </summary>
    [CreateAssetMenu(menuName = "FormulaSim/Game Settings", fileName = "GameSettings")]
    public class GameSettings : ScriptableObject
    {
        public static GameSettings Instance { get; private set; }

        [Header("AI Difficulty")]
        [Tooltip("0 = Novice (0.70), 1 = Legend (1.00)")]
        [Range(0f, 1f)] public float aiDifficulty = 0.5f;

        [Header("Assist Defaults")]
        public bool defaultTC            = true;
        public bool defaultABS           = true;
        public bool defaultSteerAssist   = true;
        public bool defaultStability     = true;

        [Header("Audio")]
        [Range(0f, 1f)] public float masterVolume   = 1f;
        [Range(0f, 1f)] public float sfxVolume      = 1f;
        [Range(0f, 1f)] public float musicVolume    = 0.75f;
        [Range(0f, 1f)] public float commentaryVolume = 0.85f;

        [Header("Graphics")]
        public bool motionBlurEnabled = true;
        public bool bloomEnabled      = true;

        // Difficulty as driver skill value (0.70 Novice → 1.00 Legend)
        public float AIDifficultySkill => Mathf.Lerp(0.70f, 1.00f, aiDifficulty);

        // Personality noise (inverted from skill: harder AI makes fewer errors)
        public float AIConsistency     => Mathf.Lerp(0.50f, 1.00f, aiDifficulty);
        public float AIAggression      => Mathf.Lerp(0.40f, 0.85f, aiDifficulty);

        public void Register() => Instance = this;

        public void Save()
        {
            PlayerPrefs.SetFloat("gs_aiDiff",    aiDifficulty);
            PlayerPrefs.SetFloat("gs_master",    masterVolume);
            PlayerPrefs.SetFloat("gs_sfx",       sfxVolume);
            PlayerPrefs.SetFloat("gs_music",     musicVolume);
            PlayerPrefs.SetInt  ("gs_mb",        motionBlurEnabled ? 1 : 0);
            PlayerPrefs.SetInt  ("gs_bloom",     bloomEnabled ? 1 : 0);
            PlayerPrefs.SetInt  ("gs_tc",        defaultTC ? 1 : 0);
            PlayerPrefs.SetInt  ("gs_abs",       defaultABS ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            aiDifficulty        = PlayerPrefs.GetFloat("gs_aiDiff", 0.5f);
            masterVolume        = PlayerPrefs.GetFloat("gs_master",  1f);
            sfxVolume           = PlayerPrefs.GetFloat("gs_sfx",     1f);
            musicVolume         = PlayerPrefs.GetFloat("gs_music",   0.75f);
            motionBlurEnabled   = PlayerPrefs.GetInt  ("gs_mb",      1) == 1;
            bloomEnabled        = PlayerPrefs.GetInt  ("gs_bloom",   1) == 1;
            defaultTC           = PlayerPrefs.GetInt  ("gs_tc",      1) == 1;
            defaultABS          = PlayerPrefs.GetInt  ("gs_abs",     1) == 1;
        }
    }
}
