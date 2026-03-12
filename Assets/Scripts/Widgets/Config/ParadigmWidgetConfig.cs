using UnityEngine;
using DIG.Core.Input;

namespace DIG.Widgets.Config
{
    /// <summary>
    /// EPIC 15.26 Phase 4: Runtime singleton that caches the active ParadigmWidgetProfile.
    /// Subscribes to ParadigmStateMachine.OnParadigmChanged to swap profiles on transitions.
    /// WidgetProjectionSystem reads ActiveProfile for budget, LOD, and scale settings.
    ///
    /// Follows the exact pattern from ParadigmSurfaceConfig (EPIC 15.24).
    /// If not placed in the scene, all systems use Shooter defaults (safe fallback).
    /// </summary>
    [AddComponentMenu("DIG/Widgets/Paradigm Widget Config")]
    public class ParadigmWidgetConfig : MonoBehaviour
    {
        private static ParadigmWidgetConfig _instance;

        /// <summary>Singleton instance. Null if not placed in scene (systems use defaults).</summary>
        public static ParadigmWidgetConfig Instance => _instance;

        /// <summary>Whether an instance exists in the scene.</summary>
        public static bool HasInstance => _instance != null;

        [Header("Profiles")]
        [Tooltip("Widget profiles for each paradigm. Matched by ParadigmWidgetProfile.Paradigm field.")]
        [SerializeField] private ParadigmWidgetProfile[] _profiles;

        [Header("Fallback")]
        [Tooltip("Default profile used when no paradigm-specific profile is found.")]
        [SerializeField] private ParadigmWidgetProfile _fallbackProfile;

        /// <summary>
        /// Currently active widget profile. Never null after initialization
        /// (falls back to _fallbackProfile).
        /// </summary>
        public ParadigmWidgetProfile ActiveProfile { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            // Set initial profile from current paradigm
            if (ParadigmStateMachine.Instance != null)
            {
                var activeParadigmProfile = ParadigmStateMachine.Instance.ActiveProfile;
                if (activeParadigmProfile != null)
                {
                    SetProfileForParadigm(activeParadigmProfile.paradigm);
                }

                ParadigmStateMachine.Instance.OnParadigmChanged += OnParadigmChanged;
            }

            // Ensure we always have a profile
            if (ActiveProfile == null)
            {
                ActiveProfile = _fallbackProfile;
            }
        }

        private void OnDestroy()
        {
            if (ParadigmStateMachine.Instance != null)
            {
                ParadigmStateMachine.Instance.OnParadigmChanged -= OnParadigmChanged;
            }

            if (_instance == this)
                _instance = null;
        }

        private void OnParadigmChanged(InputParadigmProfile paradigmProfile)
        {
            if (paradigmProfile != null)
            {
                SetProfileForParadigm(paradigmProfile.paradigm);
            }
        }

        private void SetProfileForParadigm(InputParadigm paradigm)
        {
            if (_profiles == null) return;

            for (int i = 0; i < _profiles.Length; i++)
            {
                if (_profiles[i] != null && _profiles[i].Paradigm == paradigm)
                {
                    ActiveProfile = _profiles[i];
                    return;
                }
            }

            // No matching profile — use fallback
            if (_fallbackProfile != null)
            {
                ActiveProfile = _fallbackProfile;
            }
        }
    }
}
