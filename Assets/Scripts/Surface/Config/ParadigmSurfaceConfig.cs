using UnityEngine;
using DIG.Core.Input;

namespace DIG.Surface.Config
{
    /// <summary>
    /// EPIC 15.24 Phase 7: Runtime singleton that caches the active ParadigmSurfaceProfile.
    /// Subscribes to ParadigmStateMachine.OnParadigmChanged to swap profiles on paradigm transitions.
    /// SurfaceImpactPresenterSystem reads ActiveProfile for effect scaling.
    /// </summary>
    public class ParadigmSurfaceConfig : MonoBehaviour
    {
        private static ParadigmSurfaceConfig _instance;
        public static ParadigmSurfaceConfig Instance => _instance;
        public static bool HasInstance => _instance != null;

        [Header("Profiles")]
        [Tooltip("Surface profiles for each paradigm. Matched by ParadigmSurfaceProfile.Paradigm field.")]
        [SerializeField] private ParadigmSurfaceProfile[] _profiles;

        [Header("Fallback")]
        [Tooltip("Default profile used when no paradigm-specific profile is found.")]
        [SerializeField] private ParadigmSurfaceProfile _fallbackProfile;

        /// <summary>
        /// Currently active surface profile. Never null after initialization.
        /// </summary>
        public ParadigmSurfaceProfile ActiveProfile { get; private set; }

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

            // No matching profile found — use fallback
            if (_fallbackProfile != null)
            {
                ActiveProfile = _fallbackProfile;
            }
        }
    }
}
