using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FormulaSim.Audio;

namespace FormulaSim.Race
{
    /// <summary>
    /// Manages flag states: green, yellow (per sector), safety car, VSC, red flag.
    /// Yellow flag sectors slow AI and disable overtaking in that sector.
    /// </summary>
    public class FlagSystem : MonoBehaviour
    {
        [Header("Flag Timings")]
        [SerializeField] float safetyCarDeployDelay = 3f;   // sec after incident before SC
        [SerializeField] float safetyCarMinLaps     = 2f;   // minimum SC laps
        [SerializeField] float vscSpeedLimit        = 40f;  // m/s VSC delta-time target

        [Header("References")]
        [SerializeField] AudioManager audio;

        // ── State ──────────────────────────────────────────────────────────────
        public Core.RaceFlag CurrentFlag  { get; private set; } = Core.RaceFlag.Green;
        public bool  IsYellow(int sector) => _yellowSectors.Contains(sector);
        public bool  IsSC                 => CurrentFlag == Core.RaceFlag.SafetyCar;
        public bool  IsVSC                => CurrentFlag == Core.RaceFlag.VirtualSafetyCar;
        public float SCTargetSpeed        => 30f;    // m/s behind safety car

        readonly HashSet<int> _yellowSectors = new();
        Coroutine _scRoutine;

        public event System.Action<Core.RaceFlag> OnFlagChanged;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Deploy yellow flag in a specific sector (0-based).</summary>
        public void DeployYellow(int sector)
        {
            _yellowSectors.Add(sector);
            _EvaluateSCNeed();
            OnFlagChanged?.Invoke(Core.RaceFlag.Yellow);
        }

        public void ClearYellow(int sector)
        {
            _yellowSectors.Remove(sector);
            if (_yellowSectors.Count == 0 && CurrentFlag == Core.RaceFlag.Yellow)
                SetFlag(Core.RaceFlag.Green);
        }

        /// <summary>Trigger safety car (heavy incident).</summary>
        public void DeploySafetyCar()
        {
            if (_scRoutine != null) StopCoroutine(_scRoutine);
            _scRoutine = StartCoroutine(_SCSequence(virtual_: false));
        }

        /// <summary>Trigger virtual safety car (moderate incident).</summary>
        public void DeployVSC()
        {
            if (IsSC) return;   // full SC takes priority
            SetFlag(Core.RaceFlag.VirtualSafetyCar);
            StartCoroutine(_VSCEnd());
        }

        public void DeployRedFlag()
        {
            SetFlag(Core.RaceFlag.Red);
            _yellowSectors.Clear();
        }

        public void SetGreen()
        {
            _yellowSectors.Clear();
            SetFlag(Core.RaceFlag.Green);
        }

        // ── Safety car sequence ────────────────────────────────────────────────

        IEnumerator _SCSequence(bool virtual_)
        {
            yield return new WaitForSeconds(safetyCarDeployDelay);
            SetFlag(Core.RaceFlag.SafetyCar);
            _yellowSectors.Clear();

            // Minimum SC duration
            yield return new WaitForSeconds(safetyCarMinLaps * 90f);   // approx lap time 90s

            // SC in at end of next lap
            SetFlag(Core.RaceFlag.Green);
            _scRoutine = null;
        }

        IEnumerator _VSCEnd()
        {
            yield return new WaitForSeconds(60f);   // VSC typical duration
            if (IsVSC) SetFlag(Core.RaceFlag.Green);
        }

        void _EvaluateSCNeed()
        {
            // If all 3 sectors are yellow → full safety car
            if (_yellowSectors.Count >= 3)
                DeploySafetyCar();
        }

        void SetFlag(Core.RaceFlag flag)
        {
            if (CurrentFlag == flag) return;
            CurrentFlag = flag;
            Core.GameManager.Instance?.SetFlag(flag);
            OnFlagChanged?.Invoke(flag);
        }

        // ── Speed limit helpers for AI ─────────────────────────────────────────

        /// <summary>Returns max speed multiplier for AI in a given sector.</summary>
        public float GetSpeedLimitMultiplier(int sector)
        {
            if (IsVSC)            return vscSpeedLimit / 60f;
            if (IsSC)             return SCTargetSpeed / 60f;
            if (IsYellow(sector)) return 0.70f;   // AI lifts to 70% speed in yellow sector
            return 1f;
        }
    }
}
