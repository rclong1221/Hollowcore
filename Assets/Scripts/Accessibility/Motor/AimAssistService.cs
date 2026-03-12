using UnityEngine;

namespace DIG.Accessibility.Motor
{
    /// <summary>
    /// EPIC 18.12: Accessibility-layer aim assist configuration.
    /// Wraps strength, magnetism, and slowdown multipliers.
    /// Read by existing targeting systems to scale their aim assist behavior.
    /// </summary>
    public static class AimAssistService
    {
        private const string PrefStrength = "Access_AimAssistStrength";
        private const string PrefMagnetism = "Access_AimMagnetism";
        private const string PrefSlowdown = "Access_AimSlowdown";

        private static float _strength;
        private static float _magnetismMultiplier = 1f;
        private static float _slowdownMultiplier = 1f;

        /// <summary>Overall aim assist strength (0 = off, 1 = full lock-on).</summary>
        public static float Strength => _strength;

        /// <summary>Magnetism pull multiplier (scales sticky aim pull force).</summary>
        public static float MagnetismMultiplier => _magnetismMultiplier;

        /// <summary>Slowdown multiplier (scales sensitivity reduction near enemies).</summary>
        public static float SlowdownMultiplier => _slowdownMultiplier;

        /// <summary>Whether aim assist is active at all.</summary>
        public static bool IsActive => _strength > 0.001f;

        /// <summary>
        /// FOV multiplier for target acquisition cone.
        /// Scales existing TargetSelectionConfig.AcquisitionFOV.
        /// At strength=0 returns 1 (no change). At strength=1 returns 2 (doubled cone).
        /// </summary>
        public static float FOVMultiplier => 1f + _strength;

        public static void SetStrength(float strength)
        {
            _strength = Mathf.Clamp01(strength);
            _magnetismMultiplier = 1f + _strength * 2f; // 1x to 3x at max
            _slowdownMultiplier = 1f + _strength * 1.5f; // 1x to 2.5x at max
            PlayerPrefs.SetFloat(PrefStrength, _strength);
        }

        /// <summary>Load from PlayerPrefs.</summary>
        public static void LoadSettings(float defaultStrength = 0f)
        {
            _strength = PlayerPrefs.GetFloat(PrefStrength, defaultStrength);
            _magnetismMultiplier = 1f + _strength * 2f;
            _slowdownMultiplier = 1f + _strength * 1.5f;
        }
    }
}
