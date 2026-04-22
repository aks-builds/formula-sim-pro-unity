using System.Collections;
using UnityEngine;
using Cinemachine;

namespace FormulaSim.FX
{
    // Controls Cinemachine virtual camera for F1 top-down perspective.
    // Camera sits at ~60° tilt to give pseudo-3D depth while remaining
    // functionally top-down for physics.
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    public class CameraRig : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] Transform target;
        [SerializeField] float followLag       = 0.10f;
        [SerializeField] float lookaheadDist   = 4f;    // world units ahead of car

        [Header("Zoom")]
        [SerializeField] float zoomMin         = 6f;    // ortho size (fast)
        [SerializeField] float zoomMax         = 10f;   // ortho size (slow / idle)
        [SerializeField] float zoomSpeed       = 2f;

        [Header("Shake")]
        [SerializeField] float shakeAmplitude  = 2.0f;
        [SerializeField] float shakeFrequency  = 8.0f;

        [Header("Speed Lines")]
        [SerializeField] MeshRenderer speedLinesQuad;

        CinemachineVirtualCamera vcam;
        CinemachineTransposer     transposer;
        CinemachineBasicMultiChannelPerlin noise;

        Cars.F1CarController playerCar;
        float shakeTimer;
        float currentShakeAmp = 0f;

        static readonly int _Intensity = Shader.PropertyToID("_Intensity");

        void Awake()
        {
            vcam       = GetComponent<CinemachineVirtualCamera>();
            transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
            noise      = vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            playerCar  = FindObjectOfType<Cars.F1CarController>();
            if (target == null && playerCar) target = playerCar.transform;
            vcam.Follow = target;
        }

        void Update()
        {
            if (!playerCar) return;
            float speed = playerCar.SpeedMs;

            // Zoom: tighter at high speed
            float targetOrtho = Mathf.Lerp(zoomMax, zoomMin, Mathf.Clamp01(speed / 80f));
            vcam.m_Lens.OrthographicSize = Mathf.Lerp(vcam.m_Lens.OrthographicSize, targetOrtho, Time.deltaTime * zoomSpeed);

            // Lookahead offset: camera pushes ahead of car
            if (transposer != null)
            {
                Vector3 lookahead = playerCar.transform.up * lookaheadDist * Mathf.Clamp01(speed / 40f);
                transposer.m_FollowOffset = Vector3.Lerp(transposer.m_FollowOffset,
                    new Vector3(lookahead.x, lookahead.y, transposer.m_FollowOffset.z),
                    Time.deltaTime * 4f);
            }

            // Shake decay
            if (shakeTimer > 0)
            {
                shakeTimer -= Time.deltaTime;
                currentShakeAmp = Mathf.Lerp(shakeAmplitude, 0f, 1f - shakeTimer);
            }
            else currentShakeAmp = 0f;

            if (noise != null)
            {
                noise.m_AmplitudeGain  = currentShakeAmp;
                noise.m_FrequencyGain  = shakeFrequency;
            }

            // Speed lines
            _UpdateSpeedLines(speed);
        }

        void _UpdateSpeedLines(float speed)
        {
            if (!speedLinesQuad) return;
            float intensity = Mathf.Clamp01((speed - 50f) / 30f);   // fade in above 50 m/s
            // DRS boost: stronger effect
            if (playerCar.DrsActive) intensity = Mathf.Min(1f, intensity + 0.3f);
            speedLinesQuad.material.SetFloat(_Intensity, intensity);
        }

        // Call on collision
        public void TriggerShake(float impactVelocity)
        {
            float normalized = Mathf.Clamp01(impactVelocity / 60f);
            shakeTimer = 0.3f + normalized * 0.8f;
            currentShakeAmp = shakeAmplitude * normalized;
        }

        // DRS tunnel-vision: slight FOV push + chromatic aberration spike
        public void SetDrsEffect(bool active)
        {
            StopCoroutine(nameof(_DrsTransition));
            StartCoroutine(_DrsTransition(active));
        }

        IEnumerator _DrsTransition(bool open)
        {
            float target = open ? zoomMin * 0.92f : zoomMin;
            float start  = vcam.m_Lens.OrthographicSize;
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                vcam.m_Lens.OrthographicSize = Mathf.Lerp(start, target, t / 0.3f);
                yield return null;
            }
        }

        // Night race: add very slight film grain via Cinemachine noise preset
        public void SetNightMode(bool night)
        {
            if (noise != null)
                noise.m_AmplitudeGain = night ? 0.05f : 0f;
        }
    }
}
