using UnityEngine;

namespace DIG.Core.Input
{
    /// <summary>
    /// Routes movement input based on active paradigm.
    /// Decides whether WASD, click-to-move, or both are active.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public class MovementRouter : MonoBehaviour, IMovementRouter, IParadigmConfigurable
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static MovementRouter Instance { get; private set; }

        // ============================================================
        // CONFIGURATION
        // ============================================================

        [Header("Debug")]
        [SerializeField] private bool _logStateChanges = false;

        // ============================================================
        // RUNTIME STATE
        // ============================================================

        private bool _wasdEnabled = true;
        private bool _clickToMoveEnabled;
        private ClickToMoveButton _clickToMoveButton = ClickToMoveButton.None;
        private bool _usePathfinding;
        private bool _adTurnsCharacter;

        // ============================================================
        // IMovementRouter IMPLEMENTATION
        // ============================================================

        public bool IsWASDEnabled => _wasdEnabled;
        public bool IsClickToMoveEnabled => _clickToMoveEnabled;
        public ClickToMoveButton ClickToMoveButton => _clickToMoveButton;
        public bool UsePathfinding => _usePathfinding;
        public bool ADTurnsCharacter => _adTurnsCharacter;

        // ============================================================
        // IParadigmConfigurable IMPLEMENTATION
        // ============================================================

        public int ConfigurationOrder => 100; // Movement systems
        public string SubsystemName => "MovementRouter";

        public bool CanConfigure(InputParadigmProfile profile, out string errorReason)
        {
            errorReason = null;

            // Validate that at least one movement method is enabled
            if (!profile.wasdEnabled && !profile.clickToMoveEnabled)
            {
                errorReason = "At least one movement method must be enabled";
                return false;
            }

            // Validate click-to-move has a button assigned
            if (profile.clickToMoveEnabled && profile.clickToMoveButton == ClickToMoveButton.None)
            {
                errorReason = "Click-to-move enabled but no button assigned";
                return false;
            }

            return true;
        }

        public IConfigSnapshot CaptureSnapshot()
        {
            return new MovementSnapshot
            {
                WASDEnabled = _wasdEnabled,
                ClickToMoveEnabled = _clickToMoveEnabled,
                ClickToMoveButton = _clickToMoveButton,
                UsePathfinding = _usePathfinding,
                ADTurnsCharacter = _adTurnsCharacter,
            };
        }

        public void Configure(InputParadigmProfile profile)
        {
            _wasdEnabled = profile.wasdEnabled;
            _clickToMoveEnabled = profile.clickToMoveEnabled;
            _clickToMoveButton = profile.clickToMoveButton;
            _usePathfinding = profile.usePathfinding;
            _adTurnsCharacter = profile.adTurnsCharacter;

            if (_logStateChanges)
            {
                Debug.Log($"[MovementRouter] Configured: WASD={_wasdEnabled}, click={_clickToMoveEnabled}, " +
                          $"button={_clickToMoveButton}, path={_usePathfinding}, adTurn={_adTurnsCharacter}");
            }
        }

        public void Rollback(IConfigSnapshot snapshot)
        {
            if (snapshot is MovementSnapshot moveSnapshot)
            {
                _wasdEnabled = moveSnapshot.WASDEnabled;
                _clickToMoveEnabled = moveSnapshot.ClickToMoveEnabled;
                _clickToMoveButton = moveSnapshot.ClickToMoveButton;
                _usePathfinding = moveSnapshot.UsePathfinding;
                _adTurnsCharacter = moveSnapshot.ADTurnsCharacter;
            }
        }

        private class MovementSnapshot : IConfigSnapshot
        {
            public bool WASDEnabled;
            public bool ClickToMoveEnabled;
            public ClickToMoveButton ClickToMoveButton;
            public bool UsePathfinding;
            public bool ADTurnsCharacter;
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var go = new GameObject("[MovementRouter]");
            go.AddComponent<MovementRouter>();
            Debug.Log("[MovementRouter] Auto-initialized");
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

            // Register with ServiceLocator
            ServiceLocator.Register<IMovementRouter>(this);
        }

        private void Start()
        {
            // Register with ParadigmStateMachine
            ParadigmStateMachine.Instance?.RegisterConfigurable(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<IMovementRouter>();
                ParadigmStateMachine.Instance?.UnregisterConfigurable(this);
            }
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// Check if a mouse button should trigger click-to-move.
        /// </summary>
        public bool ShouldProcessClickToMove(int mouseButton)
        {
            if (!_clickToMoveEnabled) return false;

            return _clickToMoveButton switch
            {
                ClickToMoveButton.LeftButton => mouseButton == 0,
                ClickToMoveButton.RightButton => mouseButton == 1,
                _ => false,
            };
        }

        /// <summary>
        /// Check if the A/D input should be treated as strafing (true) or turning (false).
        /// In MMO mode, this changes based on RMB state.
        /// </summary>
        public bool IsStrafing()
        {
            if (!_adTurnsCharacter)
                return true; // Always strafe (Shooter mode)

            // MMO mode: strafe when RMB is held
            var orbitController = CameraOrbitController.Instance;
            if (orbitController != null && orbitController.CurrentOrbitMode == CameraOrbitMode.ButtonHoldOrbit)
            {
                return orbitController.IsOrbitActive; // RMB held = strafe
            }

            return false; // Default to turning
        }
    }
}
