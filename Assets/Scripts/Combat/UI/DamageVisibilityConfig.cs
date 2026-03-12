using UnityEngine;

namespace DIG.Combat.UI
{
    /// <summary>
    /// EPIC 18.17 Phase 2: Single source of truth for damage number visibility policy.
    /// Separated from DamageFeedbackProfile (visual config) so the server can access
    /// visibility rules without requiring a MonoBehaviour adapter chain.
    ///
    /// Lives in Resources/ for Resources.Load access from ECS server systems.
    /// Create via: Assets > Create > DIG > Combat > Damage Visibility Config
    /// </summary>
    [CreateAssetMenu(fileName = "DamageVisibilityConfig", menuName = "DIG/Combat/Damage Visibility Config")]
    public class DamageVisibilityConfig : ScriptableObject
    {
        [Header("Visibility Policy")]

        [Tooltip("Default visibility mode for damage numbers in multiplayer.\n" +
                 "All = see all damage. SelfOnly = only yours. Nearby = within range.\n" +
                 "Party = party members only. None = disabled.")]
        public DamageNumberVisibility DefaultVisibility = DamageNumberVisibility.All;

        [Tooltip("Allow players to override the default visibility in gameplay settings")]
        public bool AllowPlayerVisibilityOverride = true;

        [Header("Nearby Mode")]

        [Tooltip("Max distance (meters) for Nearby visibility mode")]
        public float NearbyDistance = 50f;

        // ─────────────────────────────────────────────────────────────────
        // Cached singleton access via Resources.Load
        // ─────────────────────────────────────────────────────────────────
        private static DamageVisibilityConfig _instance;

        public static DamageVisibilityConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = UnityEngine.Resources.Load<DamageVisibilityConfig>("DamageVisibilityConfig");
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            _instance = null;
        }
    }
}
