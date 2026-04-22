using System.Collections.Generic;
using UnityEngine;
using FormulaSim.Cars;

namespace FormulaSim.Audio
{
    /// <summary>
    /// Per-car positional audio. Each AI car gets its own AudioSource with
    /// Doppler, distance falloff, and tunnel reverb. Tire screech intensity
    /// is driven by live slip angle magnitude from TireManager.
    /// </summary>
    public class SpatialAudioManager : MonoBehaviour
    {
        public static SpatialAudioManager Instance { get; private set; }

        [Header("Clips")]
        [SerializeField] AudioClip engineLoopClip;
        [SerializeField] AudioClip tireScreechClip;

        [Header("Distance")]
        [SerializeField] float minDistance  = 5f;
        [SerializeField] float maxDistance  = 120f;

        [Header("Doppler")]
        [SerializeField] float dopplerLevel = 1.2f;

        [Header("Tunnel Reverb")]
        [SerializeField] AudioReverbZone tunnelReverbZone;

        [Header("Tire Screech")]
        [SerializeField] float screechSlipThreshold = 0.08f;   // rad
        [SerializeField] float screechRampSpeed     = 6f;

        class CarAudio
        {
            public AudioSource      engine;
            public AudioSource      screech;
            public F1CarController  ctrl;
            public Rigidbody2D      rb;
            public float            screechVolume;
        }

        readonly List<CarAudio> carAudios = new();

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            var cars = FindObjectsOfType<F1CarController>();
            foreach (var car in cars)
                _RegisterCar(car);
        }

        public void RegisterCar(F1CarController car) => _RegisterCar(car);

        void _RegisterCar(F1CarController car)
        {
            var go = car.gameObject;

            var engineSrc  = _MakeSource(go, "EngineAudio",  engineLoopClip,    true);
            var screechSrc = _MakeSource(go, "ScreechAudio", tireScreechClip,   true);

            screechSrc.volume = 0f;

            carAudios.Add(new CarAudio
            {
                engine  = engineSrc,
                screech = screechSrc,
                ctrl    = car,
                rb      = go.GetComponent<Rigidbody2D>(),
            });
        }

        AudioSource _MakeSource(GameObject go, string childName, AudioClip clip, bool loop)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(go.transform, false);

            var src = child.AddComponent<AudioSource>();
            src.clip          = clip;
            src.loop          = loop;
            src.spatialBlend  = 1f;               // full 3-D
            src.dopplerLevel  = dopplerLevel;
            src.rolloffMode   = AudioRolloffMode.Custom;
            src.minDistance   = minDistance;
            src.maxDistance   = maxDistance;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _FalloffCurve());

            if (clip) src.Play();
            return src;
        }

        static AnimationCurve _FalloffCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f,   1f),
                new Keyframe(0.2f, 0.8f),
                new Keyframe(1f,   0f));
        }

        void Update()
        {
            foreach (var ca in carAudios)
            {
                _UpdateEngine(ca);
                _UpdateScreech(ca);
            }
        }

        void _UpdateEngine(CarAudio ca)
        {
            if (!ca.engine || ca.ctrl == null) return;

            float rpmNorm = Mathf.InverseLerp(800f, 15000f, ca.ctrl.RPM);
            ca.engine.pitch  = Mathf.Lerp(0.6f, 1.4f, rpmNorm);
            ca.engine.volume = Mathf.Lerp(0.3f, 1.0f, rpmNorm);
        }

        void _UpdateScreech(CarAudio ca)
        {
            if (!ca.screech || ca.ctrl == null) return;

            // Lateral slip angle from car velocity relative to heading
            float slipAngle = 0f;
            if (ca.rb != null)
            {
                Vector2 vel   = ca.rb.linearVelocity;
                Vector2 fwd   = ca.ctrl.transform.up;
                float   vFwd  = Vector2.Dot(vel, fwd);
                float   vLat  = Vector2.Dot(vel, (Vector2)ca.ctrl.transform.right);
                slipAngle = Mathf.Abs(Mathf.Atan2(vLat, Mathf.Abs(vFwd)));
            }

            float targetVol = slipAngle > screechSlipThreshold
                ? Mathf.Clamp01((slipAngle - screechSlipThreshold) / screechSlipThreshold)
                : 0f;

            ca.screechVolume = Mathf.MoveTowards(ca.screechVolume, targetVol,
                                                  screechRampSpeed * Time.deltaTime);
            ca.screech.volume = ca.screechVolume;
            ca.screech.pitch  = Mathf.Lerp(0.8f, 1.3f, ca.screechVolume);
        }

        public void SetTunnelReverb(bool active)
        {
            if (tunnelReverbZone) tunnelReverbZone.reverbPreset =
                active ? AudioReverbPreset.Cave : AudioReverbPreset.Off;
        }
    }
}
