using UnityEngine;
using DIG.Targeting.Core;
using DIG.Targeting.Systems;

namespace DIG.Core.Input
{
    /// <summary>
    /// Syncs lock behavior from the active paradigm profile via LockBehaviorHelper.
    ///
    /// EPIC 18.19 - Paradigm-Targeting Bridge
    /// </summary>
    public class LockBehaviorConfigurable : MonoBehaviour, IParadigmConfigurable
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static LockBehaviorConfigurable Instance { get; private set; }

        // ============================================================
        // CONFIGURATION
        // ============================================================

        [Header("Debug")]
        [SerializeField] private bool _logStateChanges = false;

        // ============================================================
        // RUNTIME STATE
        // ============================================================

        private LockBehaviorType _currentMode = LockBehaviorType.HardLock;

        // ============================================================
        // PUBLIC API
        // ============================================================

        public LockBehaviorType CurrentMode => _currentMode;

        // ============================================================
        // IParadigmConfigurable IMPLEMENTATION
        // ============================================================

        public int ConfigurationOrder => 255; // After TargetingConfigurable(250)
        public string SubsystemName => "LockBehaviorConfigurable";

        public bool CanConfigure(InputParadigmProfile profile, out string errorReason)
        {
            errorReason = null;
            return true;
        }

        public IConfigSnapshot CaptureSnapshot()
        {
            return new LockBehaviorSnapshot
            {
                Mode = _currentMode,
            };
        }

        public void Configure(InputParadigmProfile profile)
        {
            var previousMode = _currentMode;
            _currentMode = profile.defaultLockBehavior;

            LockBehaviorHelper.SetMode(_currentMode);

            if (_logStateChanges)
            {
                Debug.Log($"[LockBehaviorConfigurable] Configured: {previousMode} -> {_currentMode}");
            }
        }

        public void Rollback(IConfigSnapshot snapshot)
        {
            if (snapshot is LockBehaviorSnapshot lbs)
            {
                _currentMode = lbs.Mode;
                LockBehaviorHelper.SetMode(_currentMode);
            }
        }

        private class LockBehaviorSnapshot : IConfigSnapshot
        {
            public LockBehaviorType Mode;
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var go = new GameObject("[LockBehaviorConfigurable]");
            go.AddComponent<LockBehaviorConfigurable>();
            Debug.Log("[LockBehaviorConfigurable] Auto-initialized");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ParadigmStateMachine.Instance?.RegisterConfigurable(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ParadigmStateMachine.Instance?.UnregisterConfigurable(this);
            }
        }
    }
}
