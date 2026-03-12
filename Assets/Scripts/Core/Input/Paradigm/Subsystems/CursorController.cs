using UnityEngine;

namespace DIG.Core.Input
{
    /// <summary>
    /// Controls cursor visibility and lock state based on paradigm.
    /// Implements IParadigmConfigurable for state machine coordination.
    /// 
    /// Syncs with OPSIVE's UnityInputSystem to prevent cursor state conflicts.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public class CursorController : MonoBehaviour, ICursorController, IParadigmConfigurable
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static CursorController Instance { get; private set; }

        // ============================================================
        // CONFIGURATION
        // ============================================================

        [Header("Debug")]
        [SerializeField] private bool _logStateChanges = false;

        // ============================================================
        // RUNTIME STATE
        // ============================================================

        private bool _cursorFreeByDefault;
        private KeyCode _activeTemporaryFreeKey;
        private bool _isTemporaryFree;
        private bool _isOrbitButtonHeld; // True when RMB held (MMO mode)
        private CameraOrbitMode _orbitMode;

        // ============================================================
        // ICursorController IMPLEMENTATION
        // ============================================================

        public bool IsCursorVisible => IsCursorFree;
        public bool IsCursorLocked => !IsCursorFree;
        public bool IsCursorFreeByDefault => _cursorFreeByDefault;
        public bool IsTemporaryCursorFree => _isTemporaryFree;

        /// <summary>
        /// True when cursor should be free (visible, not locked).
        /// 
        /// Logic:
        /// - Menu open → always free
        /// - Shooter mode: locked unless Alt held (temporary free)
        /// - MMO mode: free unless RMB held (for orbit)
        /// </summary>
        public bool IsCursorFree
        {
            get
            {
                // Menu always shows cursor
                if (DIG.UI.MenuState.IsAnyMenuOpen())
                    return true;

                // RMB-hold orbit mode (MMO): cursor hidden when RMB held
                if (_orbitMode == CameraOrbitMode.ButtonHoldOrbit)
                {
                    return !_isOrbitButtonHeld;
                }

                // Standard: free by default OR temporary free key held
                return _cursorFreeByDefault || _isTemporaryFree;
            }
        }

        public void SetTemporaryFree(bool free)
        {
            if (_isTemporaryFree == free) return;
            _isTemporaryFree = free;

            if (_logStateChanges)
            {
                Debug.Log($"[CursorController] Temporary free: {free}");
            }

            UpdateCursorState();

            // Notify InputSchemeManager for backwards compatibility
            var schemeManager = InputSchemeManager.Instance;
            if (schemeManager != null)
            {
                // This triggers the OnCursorFreeChanged event
            }
        }

        // ============================================================
        // IParadigmConfigurable IMPLEMENTATION
        // ============================================================

        public int ConfigurationOrder => 0; // Cursor first
        public string SubsystemName => "CursorController";

        public bool CanConfigure(InputParadigmProfile profile, out string errorReason)
        {
            errorReason = null;
            return true; // Cursor can always be configured
        }

        public IConfigSnapshot CaptureSnapshot()
        {
            return new CursorSnapshot
            {
                CursorFreeByDefault = _cursorFreeByDefault,
                TemporaryFreeKey = _activeTemporaryFreeKey,
                IsTemporaryFree = _isTemporaryFree,
                IsOrbitButtonHeld = _isOrbitButtonHeld,
                OrbitMode = _orbitMode,
            };
        }

        public void Configure(InputParadigmProfile profile)
        {
            var oldCursorFree = _cursorFreeByDefault;
            var oldOrbitMode = _orbitMode;
            
            _cursorFreeByDefault = profile.cursorFreeByDefault;
            _activeTemporaryFreeKey = profile.temporaryCursorFreeKey;
            _orbitMode = profile.cameraOrbitMode;
            _isTemporaryFree = false; // Reset on paradigm change
            _isOrbitButtonHeld = false; // Reset on paradigm change

            // Always log configuration for debugging cursor issues
            Debug.Log($"[CursorController] Configured for {profile.displayName}: " +
                      $"cursorFreeByDefault={_cursorFreeByDefault} (was {oldCursorFree}), " +
                      $"orbitMode={_orbitMode} (was {oldOrbitMode}), " +
                      $"tempKey={_activeTemporaryFreeKey}");

            UpdateCursorState();
        }

        public void Rollback(IConfigSnapshot snapshot)
        {
            if (snapshot is CursorSnapshot cursorSnapshot)
            {
                _cursorFreeByDefault = cursorSnapshot.CursorFreeByDefault;
                _activeTemporaryFreeKey = cursorSnapshot.TemporaryFreeKey;
                _isTemporaryFree = cursorSnapshot.IsTemporaryFree;
                _isOrbitButtonHeld = cursorSnapshot.IsOrbitButtonHeld;
                _orbitMode = cursorSnapshot.OrbitMode;
                UpdateCursorState();
            }
        }

        private class CursorSnapshot : IConfigSnapshot
        {
            public bool CursorFreeByDefault;
            public KeyCode TemporaryFreeKey;
            public bool IsTemporaryFree;
            public bool IsOrbitButtonHeld;
            public CameraOrbitMode OrbitMode;
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var go = new GameObject("[CursorController]");
            go.AddComponent<CursorController>();
            Debug.Log("[CursorController] Auto-initialized");
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
            ServiceLocator.Register<ICursorController>(this);
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
                ServiceLocator.Unregister<ICursorController>();
                ParadigmStateMachine.Instance?.UnregisterConfigurable(this);
            }
        }

        private void Update()
        {
            ProcessTemporaryFreeKey();
            UpdateCursorState();
        }

        // ============================================================
        // PRIVATE METHODS
        // ============================================================

        private void ProcessTemporaryFreeKey()
        {
            // EPIC 15.21: Check for RMB-to-orbit mode (MMO style)
            if (_orbitMode == CameraOrbitMode.ButtonHoldOrbit)
            {
                // Use PlayerInputState.CameraOrbit (decoupled from Weapon Aim)
                bool rmbHeld = global::Player.Systems.PlayerInputState.CameraOrbit;
                if (_isOrbitButtonHeld != rmbHeld)
                {
                    _isOrbitButtonHeld = rmbHeld;
                    if (_logStateChanges)
                    {
                        Debug.Log($"[CursorController] RMB orbit: {rmbHeld} → Cursor free: {!rmbHeld}");
                    }
                }
                return;
            }

            // Reset orbit button state for non-orbit modes
            _isOrbitButtonHeld = false;

            // Only process temporary free key if we're not cursor-free by default
            if (_cursorFreeByDefault || _activeTemporaryFreeKey == KeyCode.None)
            {
                if (_isTemporaryFree)
                {
                    _isTemporaryFree = false;
                }
                return;
            }

            // EPIC 15.21: Standard temporary free key (Alt for Shooter/HybridToggle)
            // Use PlayerInputState Modifiers
            bool keyHeld = false;
            
            if (_activeTemporaryFreeKey == KeyCode.LeftAlt || _activeTemporaryFreeKey == KeyCode.RightAlt)
                keyHeld = global::Player.Systems.PlayerInputState.ModAlt;
            else if (_activeTemporaryFreeKey == KeyCode.LeftControl || _activeTemporaryFreeKey == KeyCode.RightControl)
                keyHeld = global::Player.Systems.PlayerInputState.ModCtrl;
            else if (_activeTemporaryFreeKey == KeyCode.LeftShift || _activeTemporaryFreeKey == KeyCode.RightShift)
                keyHeld = global::Player.Systems.PlayerInputState.ModShift;

            if (_isTemporaryFree != keyHeld)
            {
                _isTemporaryFree = keyHeld;
            }
        }

        private void UpdateCursorState()
        {
            bool shouldBeFree = IsCursorFree;
            
            // Always allow cursor in menus
            if (DIG.UI.MenuState.IsAnyMenuOpen())
            {
                shouldBeFree = true;
            }

            // Debug log state changes
            if (_logStateChanges)
            {
                Debug.Log($"[CursorController] UpdateCursorState: shouldBeFree={shouldBeFree}, " +
                    $"cursorFreeByDefault={_cursorFreeByDefault}, isTemporaryFree={_isTemporaryFree}, " +
                    $"orbitMode={_orbitMode}, isOrbitButtonHeld={_isOrbitButtonHeld}");
            }

            // Set Unity cursor state
            if (shouldBeFree)
            {
                if (Cursor.lockState != CursorLockMode.Confined)
                {
                    Cursor.lockState = CursorLockMode.Confined;
                    Cursor.visible = true;
                    Debug.Log("[CursorController] Cursor set to VISIBLE/CONFINED");
                }
            }
            else
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    Debug.Log("[CursorController] Cursor set to HIDDEN/LOCKED");
                }
            }
        }
    }
}
