using System.Collections;
using Cinemachine;
using UnityEngine;

namespace FormulaSim.FX
{
    /// <summary>
    /// Multi-camera mode manager: Chase → Cockpit → TV Broadcast.
    /// Handles smooth Cinemachine priority blending between virtual cameras.
    /// </summary>
    public class CameraSystem : MonoBehaviour
    {
        public enum CameraMode { Chase, Cockpit, TVBroadcast }

        [Header("Virtual Cameras")]
        [SerializeField] CinemachineVirtualCamera chaseVCam;
        [SerializeField] CinemachineVirtualCamera cockpitVCam;
        [SerializeField] CinemachineVirtualCameraBase[] tvCams;   // 3-4 trackside cameras

        [Header("Chase Settings")]
        [SerializeField] float chaseDistance  = 12f;
        [SerializeField] float chaseHeight    = 6f;
        [SerializeField] float chaseDamping   = 0.15f;

        [Header("Cockpit Settings")]
        [SerializeField] Transform cockpitMount;    // child of car, positioned at driver's eye
        [SerializeField] float     cockpitFOV  = 80f;
        [SerializeField] float     cockpitSway = 0.8f;   // camera sway with G-forces

        [Header("TV Director")]
        [SerializeField] float tvCutMinInterval = 6f;   // minimum seconds between cuts
        [SerializeField] float tvCutMaxInterval = 18f;

        public CameraMode CurrentMode { get; private set; } = CameraMode.Chase;

        Cars.F1CarController playerCar;
        Rigidbody2D          playerRb;
        int                  currentTVCam;
        Coroutine            tvDirectorRoutine;
        float                cockpitSwayX, cockpitSwayY;

        void Start()
        {
            playerCar = FindObjectOfType<Cars.F1CarController>();
            if (playerCar) playerRb = playerCar.GetComponent<Rigidbody2D>();
            _SetMode(CameraMode.Chase);
        }

        void LateUpdate()
        {
            if (CurrentMode == CameraMode.Cockpit) _UpdateCockpitSway();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void CycleCamera()
        {
            CameraMode next = CurrentMode switch
            {
                CameraMode.Chase       => CameraMode.Cockpit,
                CameraMode.Cockpit     => CameraMode.TVBroadcast,
                CameraMode.TVBroadcast => CameraMode.Chase,
                _                      => CameraMode.Chase,
            };
            _SetMode(next);
        }

        public void SetMode(CameraMode mode) => _SetMode(mode);

        // ── Internal ──────────────────────────────────────────────────────────

        void _SetMode(CameraMode mode)
        {
            CurrentMode = mode;

            // Set Cinemachine priority: active = 15, inactive = 0
            SetPriority(chaseVCam,   mode == CameraMode.Chase        ? 15 : 0);
            SetPriority(cockpitVCam, mode == CameraMode.Cockpit      ? 15 : 0);

            if (tvCams != null)
                foreach (var tv in tvCams)
                    SetPriority(tv, 0);

            switch (mode)
            {
                case CameraMode.Chase:
                    _ConfigureChase();
                    if (tvDirectorRoutine != null) { StopCoroutine(tvDirectorRoutine); tvDirectorRoutine = null; }
                    break;
                case CameraMode.Cockpit:
                    _ConfigureCockpit();
                    if (tvDirectorRoutine != null) { StopCoroutine(tvDirectorRoutine); tvDirectorRoutine = null; }
                    break;
                case CameraMode.TVBroadcast:
                    tvDirectorRoutine = StartCoroutine(_TVDirector());
                    break;
            }
        }

        void _ConfigureChase()
        {
            if (!chaseVCam || !playerCar) return;
            chaseVCam.Follow = playerCar.transform;
            chaseVCam.LookAt = playerCar.transform;
            var transposer = chaseVCam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer)
            {
                transposer.m_FollowOffset = new Vector3(0, chaseHeight, -chaseDistance);
                transposer.m_XDamping     = chaseDamping;
                transposer.m_YDamping     = chaseDamping;
                transposer.m_ZDamping     = chaseDamping;
            }
            chaseVCam.m_Lens.FieldOfView = 65f;
        }

        void _ConfigureCockpit()
        {
            if (!cockpitVCam || !cockpitMount) return;
            cockpitVCam.Follow = cockpitMount;
            cockpitVCam.m_Lens.FieldOfView = cockpitFOV;
            // Minimal damping for cockpit — feels attached to the car
            var transposer = cockpitVCam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer)
            {
                transposer.m_FollowOffset = Vector3.zero;
                transposer.m_XDamping = 0.02f;
                transposer.m_YDamping = 0.02f;
                transposer.m_ZDamping = 0.02f;
            }
        }

        void _UpdateCockpitSway()
        {
            if (!playerRb || !cockpitVCam) return;

            // Lateral G-force → camera sway
            Vector2 lateralVel  = Vector2.Dot(playerRb.linearVelocity,
                                  playerCar.transform.right) * (Vector2)playerCar.transform.right;
            float   gForce      = lateralVel.magnitude / 9.81f;
            float   swayTarget  = -Mathf.Sign(Vector2.Dot(playerRb.linearVelocity, playerCar.transform.right))
                                 * gForce * cockpitSway * 0.5f;

            cockpitSwayX = Mathf.Lerp(cockpitSwayX, swayTarget, Time.deltaTime * 5f);
            cockpitSwayY = Mathf.Lerp(cockpitSwayY,
                           -playerCar.ThrottleInput * 0.3f + playerCar.BrakeInput * 0.5f,
                           Time.deltaTime * 4f);

            cockpitVCam.m_Lens.Dutch = cockpitSwayX * 3f;
        }

        IEnumerator _TVDirector()
        {
            while (CurrentMode == CameraMode.TVBroadcast)
            {
                float interval = Random.Range(tvCutMinInterval, tvCutMaxInterval);
                yield return new WaitForSeconds(interval);

                // Cut to next trackside camera
                if (tvCams != null && tvCams.Length > 0)
                {
                    SetPriority(tvCams[currentTVCam], 0);
                    currentTVCam = (currentTVCam + 1) % tvCams.Length;
                    SetPriority(tvCams[currentTVCam], 15);
                }
            }
        }

        static void SetPriority(CinemachineVirtualCameraBase vcam, int priority)
        {
            if (vcam) vcam.Priority = priority;
        }
    }
}
