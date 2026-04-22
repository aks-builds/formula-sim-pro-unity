using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FormulaSim.Audio;
using FormulaSim.Core;

namespace FormulaSim.UI
{
    public class RaceStartSequence : MonoBehaviour
    {
        [SerializeField] Image[]   lightPanels;       // 5 red light images
        [SerializeField] CanvasGroup lightsGroup;
        [SerializeField] TMP_Text  goText;
        [SerializeField] CanvasGroup goGroup;
        [SerializeField] Color      lightOnColor  = new(1f, 0.05f, 0.05f);
        [SerializeField] Color      lightOffColor = new(0.15f, 0.02f, 0.02f);
        [SerializeField] float      lightInterval = 1.0f;
        [SerializeField] ParticleSystem startFlare;

        AudioManager audio;

        void Start()
        {
            audio = FindObjectOfType<AudioManager>();
            foreach (var p in lightPanels) p.color = lightOffColor;
            goGroup.alpha = 0f;
        }

        public void StartSequence() => StartCoroutine(_LightsSequence());

        IEnumerator _LightsSequence()
        {
            lightsGroup.alpha = 1f;

            // Light up 5 lights one by one
            for (int i = 0; i < lightPanels.Length; i++)
            {
                yield return new WaitForSeconds(lightInterval);
                lightPanels[i].color = lightOnColor;
                audio?.PlayUI(UISound.CountdownBeep);
                _PulseLight(lightPanels[i]);
            }

            // Random hold: 0.2 – 3.0 seconds (anti-jump-start)
            float hold = 0.2f + Random.value * 2.8f;
            yield return new WaitForSeconds(hold);

            // LIGHTS OUT
            foreach (var p in lightPanels) p.color = lightOffColor;
            audio?.PlayUI(UISound.GoSignal);
            startFlare?.Play();

            // Flash GO text
            goGroup.alpha = 1f;
            goText.text   = "GO!";
            goText.color  = new Color(0.2f, 1f, 0.35f);
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                goText.transform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t / 0.6f);
                yield return null;
            }

            yield return new WaitForSeconds(1.5f);
            t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                goGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.5f);
                yield return null;
            }

            lightsGroup.alpha = 0f;
            GameManager.Instance.TransitionTo(GameState.Racing);
        }

        void _PulseLight(Image panel)
        {
            // Brief scale pop on each light
            StopCoroutine(_PulseCoroutine(panel));
            StartCoroutine(_PulseCoroutine(panel));
        }

        IEnumerator _PulseCoroutine(Image panel)
        {
            panel.transform.localScale = Vector3.one * 1.15f;
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                panel.transform.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one, t / 0.2f);
                yield return null;
            }
        }
    }
}
