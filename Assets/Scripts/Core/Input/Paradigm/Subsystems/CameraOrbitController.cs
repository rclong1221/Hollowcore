using UnityEngine;
using DIG.CameraSystem;

namespace DIG.Core.Input
{
    /// <summary>
    /// Controls camera orbit behavior based on paradigm.
    /// Manages whether mouse orbit is always on, button-held, or disabled.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public class CameraOrbitController : MonoBehaviour, ICameraOrbitController, IParadigmConfigurable
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static CameraOrbitController Instance { get; private set; }

        // ============================================================
        // CONFIGURATION
        // ============================================================

        [Header("Debug")]
        [SerializeField] private bool _logStateChanges = false;

        // ============================================================
        // RUNTIME STATE
        // ============================================================

        private CameraOrbitMode _orbitMode = CameraOrbitMode.AlwaysOrbit;
        private bool _qeRotationEnabled;
        private bool _edgePanEnabled;
        private bool _isOrbitButtonHeld;

        // ============================================================
        // ICameraOrbitController IMPLEMENTATION
        // ============================================================

        public CameraOrbitMode CurrentOrbitMode => _orbitMode;

        public bool IsOrbitActive
        {
            get
            {
                return _orbitMode switch
                {
                    CameraOrbitMode.AlwaysOrbit => true,
                    CameraOrbitMode.ButtonHoldOrbit => _isOrbitButtonHeld,
                    CameraOrbitMode.KeyRotateOnly => false,
                    CameraOrbitMode.FollowOnly => false,
                    _ => false,
                };
            }
        }

        public bool IsKeyRotationEnabled => _qeRotationEnabled;
        public bool IsEdgePanEnabled => _edgePanEnabled;

        // ============================================================
        // IParadigmConfigurable IMPLEMENTATION
        // ============================================================

        public int ConfigurationOrder => 10; // After cursor
        public string SubsystemName => "CameraOrbitController";

        public bool CanConfigure(InputParadigmProfile profile, out string errorReason)
        {
            errorReason = null;

            // Validate camera compatibility
            var cameraProvider = CameraModeProvider.Instance;
            if (cameraProvider == null || !cameraProvider.HasActiveCamera)
                return true;

            var currentMode = cameraProvider.CurrentMode;

            // Check if the requested orbit mode is compatible with current camera
            if (profile.cameraOrbitMode == CameraOrbitMode.AlwaysOrbit && !cameraProvider.SupportsOrbitRotation)
            {
                errorReason = $"Camera mode {currentMode} does not support orbit rotation";
                return false;
            }

            return true;
        }

        public IConfigSnapshot CaptureSnapshot()
        {
            return new OrbitSnapshot
            {
                OrbitMode = _orbitMode,
                QERotationEnabled = _qeRotationEnabled,
                EdgePanEnabled = _edgePanEnabled,
                IsOrbitButtonHeld = _isOrbitButtonHeld,
            };
        }

        public void Configure(InputParadigmProfile profile)
        {
            _orbitMode = profile.cameraOrbitMode;
            _qeRotationEnabled = profile.qeRotationEnabled;
            _edgePanEnabled = profile.edgePanEnabled;
            _isOrbitButtonHeld = false; // Reset on paradigm change

            if (_logStateChanges)
            {
                Debug.Log($"[CameraOrbitController] Configured: mode={_orbitMode}, QE={_qeRotationEnabled}, edgePan={_edgePanEnabled}");
            }
        }

        public void Rollback(IConfigSnapshot snapshot)
        {
            if (snapshot is OrbitSnapshot orbitSnapshot)
            {
                _orbitMode = orbitSnapshot.OrbitMode;
                _qeRotationEnabled = orbitSnapshot.QERotationEnabled;
                _edgePanEnabled = orbitSnapshot.EdgePanEnabled;
                _isOrbitButtonHeld = orbitSnapshot.IsOrbitButtonHeld;
            }
        }

        private class OrbitSnapshot : IConfigSnapshot
        {
            public CameraOrbitMode OrbitMode;
            public bool QERotationEnabled;
            public bool EdgePanEnabled;
            public bool IsOrbitButtonHeld;
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var go = new GameObject("[CameraOrbitController]");
            go.AddComponent<CameraOrbitController>();
            Debug.Log("[CameraOrbitController] Auto-initialized");
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
            ServiceLocator.Register<ICameraOrbitController>(this);
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
                ServiceLocator.Unregister<ICameraOrbitController>();
                ParadigmStateMachine.Instance?.UnregisterConfigurable(this);
            }
        }

        private void Update()
        {
            ProcessOrbitButton();
        }

        // ============================================================
        // PRIVATE METHODS
        // ============================================================

        private void ProcessOrbitButton()
        {
            if (_orbitMode != CameraOrbitMode.ButtonHoldOrbit)
            {
                _isOrbitButtonHeld = false;
                return;
            }

            // EPIC 15.21: RMB for camera orbit (MMO style)
            // Use PlayerInputState.CameraOrbit (decoupled from Weapon Aim)
            _isOrbitButtonHeld = global::Player.Systems.PlayerInputState.CameraOrbit;
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// Called by input systems to check if look delta should be applied to camera.
        /// </summary>
        public bool ShouldApplyLookDelta()
        {
            // Don't orbit when menu is open
            if (DIG.UI.MenuState.IsAnyMenuOpen())
                return false;

            return IsOrbitActive;
        }
    }
}
