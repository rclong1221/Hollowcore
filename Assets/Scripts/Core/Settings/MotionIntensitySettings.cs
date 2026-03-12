using UnityEngine;
using DIG.Surface;

namespace DIG.Core.Settings
{
    /// <summary>
    /// EPIC 15.24 Phase 11: Global motion intensity and platform tier settings.
    /// User-facing accessibility slider that scales all VFX, audio, shake, and haptics.
    /// Shared with EPIC 15.25 (ProceduralMotionLayer reads this too).
    /// </summary>
    public class MotionIntensitySettings : MonoBehaviour
    {
        private static MotionIntensitySettings _instance;
        public static MotionIntensitySettings Instance => _instance;
        public static bool HasInstance => _instance != null;

        public enum PlatformTier : byte
        {
            PC = 0,
            Console = 1,
            Mobile = 2
        }

        [Header("Motion Intensity")]
        [Tooltip("Global intensity multiplier for all motion effects (0=disabled, 1=normal, 2=exaggerated).")]
        [Range(0f, 2f)]
        [SerializeField] private float _globalIntensity = 1f;

        [Header("Platform")]
        [Tooltip("Auto-detected platform tier. Affects LOD demotion and effect budgets.")]
        [SerializeField] private PlatformTier _currentTier = PlatformTier.PC;

        /// <summary>
        /// Global motion intensity (0-2). User-facing accessibility control.
        /// 0 = no motion effects, 1 = normal, 2 = exaggerated.
        /// </summary>
        public float GlobalIntensity
        {
            get => _globalIntensity;
            set => _globalIntensity = Mathf.Clamp(value, 0f, 2f);
        }

        /// <summary>
        /// Auto-detected or overridden platform tier.
        /// </summary>
        public PlatformTier CurrentTier => _currentTier;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DetectPlatformTier();
        }

        private void DetectPlatformTier()
        {
#if UNITY_IOS || UNITY_ANDROID
            _currentTier = PlatformTier.Mobile;
#elif UNITY_GAMECORE || UNITY_PS4 || UNITY_PS5 || UNITY_XBOXONE || UNITY_SWITCH
            _currentTier = PlatformTier.Console;
#else
            _currentTier = PlatformTier.PC;
#endif
        }

        /// <summary>
        /// Demote LOD tier based on platform. Mobile demotes by 1, Console stays same, PC stays same.
        /// </summary>
        public EffectLODTier ApplyPlatformScaling(EffectLODTier tier)
        {
            if (_currentTier == PlatformTier.Mobile && tier < EffectLODTier.Culled)
            {
                return tier + 1; // Demote one tier on mobile
            }
            return tier;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
