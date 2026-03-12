using UnityEngine;

namespace DIG.Core.Input
{
    /// <summary>
    /// Controls character facing/rotation based on paradigm.
    /// Determines whether character faces camera, movement direction, cursor, etc.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public class FacingController : MonoBehaviour, IFacingController, IParadigmConfigurable
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static FacingController Instance { get; private set; }

        // ============================================================
        // CONFIGURATION
        // ============================================================

        [Header("Debug")]
        [SerializeField] private bool _logStateChanges = false;

        // ============================================================
        // RUNTIME STATE
        // ============================================================

        private MovementFacingMode _facingMode = MovementFacingMode.CameraForward;

        // ============================================================
        // IFacingController IMPLEMENTATION
        // ============================================================

        public MovementFacingMode CurrentFacingMode => _facingMode;

        // ============================================================
        // IParadigmConfigurable IMPLEMENTATION
        // ============================================================

        public int ConfigurationOrder => 200; // Facing after movement
        public string SubsystemName => "FacingController";

        public bool CanConfigure(InputParadigmProfile profile, out string errorReason)
        {
            errorReason = null;
            return true; // Facing can always be configured
        }

        public IConfigSnapshot CaptureSnapshot()
        {
            return new FacingSnapshot
            {
                FacingMode = _facingMode,
            };
        }

        public void Configure(InputParadigmProfile profile)
        {
            _facingMode = profile.facingMode;

            if (_logStateChanges)
            {
                Debug.Log($"[FacingController] Configured: mode={_facingMode}");
            }
        }

        public void Rollback(IConfigSnapshot snapshot)
        {
            if (snapshot is FacingSnapshot facingSnapshot)
            {
                _facingMode = facingSnapshot.FacingMode;
            }
        }

        private class FacingSnapshot : IConfigSnapshot
        {
            public MovementFacingMode FacingMode;
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var go = new GameObject("[FacingController]");
            go.AddComponent<FacingController>();
            Debug.Log("[FacingController] Auto-initialized");
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
            ServiceLocator.Register<IFacingController>(this);
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
                ServiceLocator.Unregister<IFacingController>();
                ParadigmStateMachine.Instance?.UnregisterConfigurable(this);
            }
        }
    }
}
