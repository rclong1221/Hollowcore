using UnityEngine;

namespace DIG.Combat.UI
{
    /// <summary>
    /// Resolves the effective damage number visibility setting.
    /// Priority: player override (if allowed by config) -> designer default -> All.
    ///
    /// EPIC 18.17 Phase 2: Reads from DamageVisibilityConfig (Resources/ SO) instead
    /// of adapter chain. Caches per-frame to avoid PlayerPrefs reads every dequeue.
    /// </summary>
    public static class DamageNumberVisibilitySettings
    {
        private const string PrefKey = "Settings_DmgNumVisibility";

        private static DamageNumberVisibility _cachedVisibility;
        private static int _cachedFrame = -1;

        /// <summary>
        /// The effective visibility mode for the current player.
        /// Cached per-frame to avoid repeated PlayerPrefs lookups.
        /// </summary>
        public static DamageNumberVisibility EffectiveVisibility
        {
            get
            {
                int frame = Time.frameCount;
                if (_cachedFrame == frame)
                    return _cachedVisibility;

                _cachedFrame = frame;
                var config = DamageVisibilityConfig.Instance;
                if (config != null && config.AllowPlayerVisibilityOverride)
                {
                    int saved = PlayerPrefs.GetInt(PrefKey, -1);
                    _cachedVisibility = saved >= 0
                        ? (DamageNumberVisibility)saved
                        : config.DefaultVisibility;
                }
                else
                {
                    _cachedVisibility = config != null
                        ? config.DefaultVisibility
                        : DamageNumberVisibility.All;
                }
                return _cachedVisibility;
            }
        }

        public static void SetPlayerOverride(DamageNumberVisibility vis)
        {
            PlayerPrefs.SetInt(PrefKey, (int)vis);
            PlayerPrefs.Save();
            _cachedFrame = -1; // Invalidate cache
        }

        public static void ClearPlayerOverride()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
            _cachedFrame = -1; // Invalidate cache
        }

        /// <summary>Whether the active config allows player override.</summary>
        public static bool IsPlayerOverrideAllowed
        {
            get
            {
                var config = DamageVisibilityConfig.Instance;
                return config != null && config.AllowPlayerVisibilityOverride;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            _cachedFrame = -1;
        }
    }
}
