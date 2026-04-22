using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using FormulaSim.Weather;
using FormulaSim.Core;

namespace FormulaSim.Cars
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class F1CarController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] VehicleConfig config;
        [SerializeField] bool          isPlayerCar = true;

        [Header("Visual")]
        [SerializeField] SpriteRenderer bodyRenderer;
        [SerializeField] TrailRenderer  tyreTrailFL, tyreTrailFR, tyreTrailRL, tyreTrailRR;
        [SerializeField] ParticleSystem brakeSparksFX;
        [SerializeField] ParticleSystem tireSmokeFL, tireSmokeRR;
        [SerializeField] ParticleSystem drsShimmerFX;
        [SerializeField] Light2D        headlightL, headlightR;

        // ── Public telemetry ─────────────────────────────────────────────────
        public float      SpeedMs          { get; private set; }
        public float      SpeedKph         => SpeedMs * 3.6f;
        public float      RPM              { get; private set; }
        public int        Gear             { get; private set; } = 1;
        public float      ThrottleInput    { get; private set; }
        public float      BrakeInput       { get; private set; }
        public bool       DrsActive        { get; private set; }
        public TireCompound CurrentCompound { get; private set; } = TireCompound.Medium;
        public TireManager.TireSet Tires   { get; private set; }
        public int        CurrentLap       { get; private set; }
        public float      LapTime          { get; private set; }
        public float      BestLapTime      { get; private set; } = float.MaxValue;
        public bool       InPitLane        { get; private set; }
        public bool       HasRetired       { get; private set; }

        public event Action<int>   OnGearChange;
        public event Action<float> OnLapComplete;
        public event Action        OnFlatSpot;

        // ── Private ──────────────────────────────────────────────────────────
        Rigidbody2D   rb;
        DamageModel   _damage;
        float         steerInput;
        float         throttleAxis;
        float         brakeAxis;
        bool          drsButtonHeld;
        WeatherSystem weather;
        float         weatherGripMult = 1f;
        float         weatherDragMult = 1f;
        bool          drsZoneActive;
        bool          isLockingUp;
        float         cumulativeDamage;

        float shiftCooldown;
        const float SHIFT_CD               = 0.08f;
        const float PIT_SPEED_LIMIT        = 16.67f;   // 60 km/h
        const float RETIRE_DAMAGE_THRESHOLD = 1.0f;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            rb      = GetComponent<Rigidbody2D>();
            _damage = GetComponent<DamageModel>();

            rb.mass           = config.mass;
            rb.linearDamping  = config.linearDamping;
            rb.angularDamping = config.angularDamping;

            weather = FindObjectOfType<WeatherSystem>();
            Tires   = TireManager.NewSet(CurrentCompound);
        }

        void Start()
        {
            if (!isPlayerCar) return;
            GetComponent<PlayerInput>().onActionTriggered += OnInputAction;
        }

        // ── Input (new Input System) ──────────────────────────────────────────

        void OnInputAction(InputAction.CallbackContext ctx)
        {
            switch (ctx.action.name)
            {
                case "Throttle": throttleAxis  = ctx.ReadValue<float>();   break;
                case "Brake":    brakeAxis      = ctx.ReadValue<float>();   break;
                case "Steer":    steerInput     = ctx.ReadValue<float>();   break;
                case "DRS":      drsButtonHeld  = ctx.ReadValueAsButton();  break;
                case "Pause":    if (ctx.performed) GameManager.Instance.TogglePause(); break;
            }
        }

        // ── Physics ───────────────────────────────────────────────────────────

        void FixedUpdate()
        {
            if (GameManager.Instance.State == GameState.Paused) return;
            if (HasRetired) return;

            SpeedMs = rb.linearVelocity.magnitude;

            _AutoShift();
            _UpdateRPM();
            _ApplyDriveForce();
            _ApplyLateralGrip();
            _ApplyAeroDrag();
            _ApplySteering();
            _UpdateDRS();
            _UpdateTires();
            _EnforcePitLaneSpeed();
            _UpdateVisuals();

            LapTime += Time.fixedDeltaTime;
        }

        void _EnforcePitLaneSpeed()
        {
            if (!InPitLane) return;
            if (SpeedMs > PIT_SPEED_LIMIT)
                rb.linearVelocity = rb.linearVelocity.normalized * PIT_SPEED_LIMIT;
            if (DrsActive)
            {
                DrsActive = false;
                drsShimmerFX?.Stop();
            }
            // Report speed to penalty system for speeding check
            Race.PenaltySystem.Instance?.CheckPitLaneSpeeding(this, SpeedMs);
        }

        void _AutoShift()
        {
            if (shiftCooldown > 0) { shiftCooldown -= Time.fixedDeltaTime; return; }
            if (RPM > config.shiftUpRPM && Gear < config.gearRatios.Length) _ShiftUp();
            else if (RPM < config.shiftDownRPM && Gear > 1) _ShiftDown();
        }

        void _ShiftUp()
        {
            Gear++;
            shiftCooldown = SHIFT_CD;
            OnGearChange?.Invoke(Gear);
            FindObjectOfType<Audio.AudioManager>()?.PlayTransmissionEvent(TransmissionEvent.UpshiftCut);
        }

        void _ShiftDown()
        {
            Gear--;
            shiftCooldown = SHIFT_CD;
            OnGearChange?.Invoke(Gear);
            FindObjectOfType<Audio.AudioManager>()?.PlayTransmissionEvent(TransmissionEvent.DownshiftCrackle);
        }

        void _UpdateRPM()
        {
            float ratio       = config.gearRatios[Gear - 1] * config.finalDrive;
            float wheelCircum = Mathf.PI * 0.66f;   // ~33cm radius tyre
            RPM = SpeedMs / wheelCircum * ratio * 60f;
            RPM = Mathf.Clamp(RPM, config.idleRPM, config.maxRPM);
        }

        void _ApplyDriveForce()
        {
            ThrottleInput = isPlayerCar ? throttleAxis : ThrottleInput;
            BrakeInput    = isPlayerCar ? brakeAxis    : BrakeInput;

            float gripMult   = TireManager.GetAvgGrip(Tires) * weatherGripMult
                             * (_damage?.GripMultiplier ?? 1f);
            float driveForce = config.maxDriveForce * ThrottleInput * gripMult;
            float brakeForce = config.maxBrakeForce * BrakeInput    * gripMult;

            if (ThrottleInput < 0.05f && SpeedMs > 2f)
                brakeForce += config.maxBrakeForce * config.engineBraking;

            if (DrsActive) driveForce *= (2f - config.drsDragMultiplier);

            isLockingUp = BrakeInput > 0.85f && SpeedMs > 30f
                && (weather.CurrentState == WeatherState.Heavy || weather.CurrentState == WeatherState.Extreme)
                && (CurrentCompound == TireCompound.Soft || CurrentCompound == TireCompound.Medium || CurrentCompound == TireCompound.Hard);

            rb.AddForce(transform.up * (driveForce - brakeForce));
        }

        void _ApplyLateralGrip()
        {
            Vector2 lateralVel = Vector2.Dot(rb.linearVelocity, transform.right) * (Vector2)transform.right;
            float   downforce  = SpeedMs * SpeedMs * config.downforceCoeff
                               * (_damage?.DownforceMultiplier ?? 1f);
            float   gripMult   = weatherGripMult * TireManager.GetAvgGrip(Tires)
                               * (_damage?.GripMultiplier ?? 1f);
            float   gripForce  = (config.mass * downforce + 800f) * gripMult;
            rb.AddForce(-lateralVel * gripForce * Time.fixedDeltaTime);
        }

        void _ApplyAeroDrag()
        {
            float damageDrag = _damage?.DragMultiplier ?? 1f;
            float dragMult   = weatherDragMult * damageDrag * (DrsActive ? config.drsDragMultiplier : 1f);
            float drag       = SpeedMs * SpeedMs * config.dragCoeff * dragMult;
            rb.AddForce(-rb.linearVelocity.normalized * drag);
        }

        void _ApplySteering()
        {
            if (!isPlayerCar) return;
            float speedFactor = 1f - Mathf.Clamp01(SpeedMs / 80f) * config.steerSpeedSensitivity;
            float rotDeg      = -steerInput * config.maxSteerAngle * speedFactor * Time.fixedDeltaTime * 60f;
            rb.MoveRotation(rb.rotation + rotDeg);
        }

        void _UpdateDRS()
        {
            bool canOpen = drsZoneActive && SpeedMs >= config.drsMinSpeed && !InPitLane;
            if (drsButtonHeld && canOpen && !DrsActive)
            {
                DrsActive = true;
                drsShimmerFX?.Play();
                FindObjectOfType<Audio.AudioManager>()?.PlayTransmissionEvent(TransmissionEvent.DrsOpen);
            }
            else if ((!drsButtonHeld || !canOpen) && DrsActive)
            {
                DrsActive = false;
                drsShimmerFX?.Stop();
                FindObjectOfType<Audio.AudioManager>()?.PlayTransmissionEvent(TransmissionEvent.DrsClose);
            }
        }

        void _UpdateTires()
        {
            var mods = weather.GetPhysicsMods(CurrentCompound);
            weatherGripMult = mods.gripMultiplier;
            weatherDragMult = mods.dragMultiplier;

            var loadData = new TireManager.CornerLoad
            {
                throttlePct = ThrottleInput,
                brakePct    = BrakeInput,
                lateralG    = Vector2.Dot(rb.linearVelocity, transform.right) / 9.81f,
            };
            TireManager.Update(Tires, Time.fixedDeltaTime, SpeedMs, loadData,
                               weather.CurrentState, isLockingUp);

            if (mods.aquaplaneRisk > 0 && !InPitLane)
                _CheckAquaplane(mods.aquaplaneRisk);
        }

        void _CheckAquaplane(float risk)
        {
            if (SpeedMs < 40f) return;
            float prob = (SpeedMs - 40f) * 0.004f * risk * Time.fixedDeltaTime;
            if (UnityEngine.Random.value < prob)
                StartCoroutine(_TriggerAquaplane());
        }

        IEnumerator _TriggerAquaplane()
        {
            FindObjectOfType<UI.WeatherHUD>()?.TriggerAquaplaneAlert(true);
            float savedGrip = weatherGripMult;
            weatherGripMult *= 0.12f;
            yield return new WaitForSeconds(UnityEngine.Random.Range(1.8f, 3.2f));
            weatherGripMult = savedGrip;
            FindObjectOfType<UI.WeatherHUD>()?.TriggerAquaplaneAlert(false);
        }

        void _UpdateVisuals()
        {
            float lateralSlide = Mathf.Abs(Vector2.Dot(rb.linearVelocity, transform.right));
            bool  showTrails   = lateralSlide > 3f || BrakeInput > 0.7f;
            _SetTrailEmitting(showTrails);
            _SetSmokeEmitting(showTrails && SpeedMs > 15f);

            if (brakeSparksFX != null)
            {
                var emit = brakeSparksFX.emission;
                emit.enabled = BrakeInput > 0.85f && SpeedMs > 25f;
            }

            if (headlightL) headlightL.enabled = GameManager.Instance.IsNightRace;
            if (headlightR) headlightR.enabled = GameManager.Instance.IsNightRace;
        }

        void _SetTrailEmitting(bool on)
        {
            if (tyreTrailFL) tyreTrailFL.emitting = on;
            if (tyreTrailFR) tyreTrailFR.emitting = on;
            if (tyreTrailRL) tyreTrailRL.emitting = on;
            if (tyreTrailRR) tyreTrailRR.emitting = on;
        }

        void _SetSmokeEmitting(bool on)
        {
            void Set(ParticleSystem ps) { if (ps == null) return; var e = ps.emission; e.enabled = on; }
            Set(tireSmokeFL); Set(tireSmokeRR);
        }

        // ── Collision / Damage / Retirement ───────────────────────────────────

        void OnCollisionEnter2D(Collision2D col)
        {
            if (HasRetired) return;

            float impactSpeed = col.relativeVelocity.magnitude;
            if (impactSpeed < 4f) return;

            // Drive detailed damage through DamageModel
            if (_damage != null)
            {
                Vector2 contactNormal = col.contacts.Length > 0
                    ? col.contacts[0].normal
                    : (col.transform.position - transform.position).normalized;
                _damage.ApplyCollision(impactSpeed, contactNormal);
            }

            // Cumulative retirement threshold (independent of DamageModel)
            cumulativeDamage += impactSpeed * 0.008f;

            FindObjectOfType<Audio.AudioManager>()?.PlayCrash(impactSpeed);
            FindObjectOfType<FX.CameraRig>()?.TriggerShake(impactSpeed);

            // Report to PenaltySystem if we hit another car
            var otherCtrl = col.gameObject.GetComponent<F1CarController>();
            if (otherCtrl != null)
                Race.PenaltySystem.Instance?.ReportCollision(this, otherCtrl, impactSpeed);

            if (cumulativeDamage >= RETIRE_DAMAGE_THRESHOLD)
                _Retire();
        }

        void _Retire()
        {
            HasRetired         = true;
            rb.linearVelocity  = Vector2.zero;
            rb.angularVelocity = 0f;
            Race.RaceManager.Instance?.RetireCar(this);
            Debug.Log($"[F1Car] {gameObject.name} retired (damage {cumulativeDamage:F2})");
        }

        // ── External API ──────────────────────────────────────────────────────

        public void SetDrsZone(bool active)  => drsZoneActive = active;
        public void SetPitLane(bool inPit)   => InPitLane = inPit;

        public void ChangeTires(TireCompound compound)
        {
            CurrentCompound = compound;
            Tires = TireManager.NewSet(compound);
        }

        public void OnCheckpointHit(CheckpointData cp)
        {
            if (cp.isSector3End)
            {
                float lapT = LapTime;
                OnLapComplete?.Invoke(lapT);
                if (lapT < BestLapTime) BestLapTime = lapT;
                LapTime = 0f;
                CurrentLap++;
                GameManager.Instance.NotifyLapComplete(CurrentLap);
            }
            SetDrsZone(cp.isDrsZone);
        }

        public void SetAIInputs(float throttle, float brake, float steer)
        {
            ThrottleInput = throttle;
            BrakeInput    = brake;
            steerInput    = steer;
        }
    }

    public enum TransmissionEvent { UpshiftCut, DownshiftCrackle, DrsOpen, DrsClose }
}
