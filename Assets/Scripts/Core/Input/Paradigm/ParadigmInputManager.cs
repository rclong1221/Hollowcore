using UnityEngine;
using UnityEngine.InputSystem;
using Player.Systems;

namespace DIG.Core.Input
{
    /// <summary>
    /// Manages Input System action map switching based on the active input paradigm.
    /// Uses InputActionAsset runtime API for robust action map access.
    /// 
    /// Action Map Strategy:
    /// - Core: Always enabled (movement, jump, crouch, sprint, interact, reload, etc.)
    /// - Combat_Shooter: Enabled in Shooter paradigm
    /// - Combat_MMO: Enabled in MMO paradigm (includes AutoRun composite)
    /// - Combat_ARPG: Enabled in ARPG paradigm
    /// - UI: Managed separately by UI system
    /// 
    /// EPIC 15.21 - Input Action Layer
    /// </summary>
    public class ParadigmInputManager : MonoBehaviour
    {
        public static ParadigmInputManager Instance { get; private set; }
        
        private DIGInputActions _inputActions;
        private InputParadigm _currentParadigm = InputParadigm.Shooter;
        
        // Cached action maps (using runtime lookup for reliability)
        private InputActionMap _coreMap;
        private InputActionMap _shooterMap;
        private InputActionMap _mmoMap;
        private InputActionMap _arpgMap;
        private InputActionMap _mobaMap;
        private InputActionMap _uiMap;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var existingGO = GameObject.Find("ParadigmInputManager");
                if (existingGO != null)
                {
                    Instance = existingGO.GetComponent<ParadigmInputManager>();
                    if (Instance != null) return;
                }
                
                var go = new GameObject("ParadigmInputManager");
                go.AddComponent<ParadigmInputManager>();
                DontDestroyOnLoad(go);
            }
        }
        
        /// <summary>
        /// Gets the active input actions asset.
        /// </summary>
        public DIGInputActions InputActions => _inputActions;
        
        /// <summary>
        /// Gets the underlying InputActionAsset for runtime action lookup.
        /// </summary>
        public InputActionAsset Asset => _inputActions?.asset;
        
        /// <summary>
        /// Gets the currently active paradigm.
        /// </summary>
        public InputParadigm CurrentParadigm => _currentParadigm;
        
        // Runtime action map accessors (more reliable than generated code)
        public InputActionMap CoreMap => _coreMap;
        public InputActionMap ShooterMap => _shooterMap;
        public InputActionMap MMOMap => _mmoMap;
        public InputActionMap ARPGMap => _arpgMap;
        public InputActionMap MOBAMap => _mobaMap;
        public InputActionMap UIMap => _uiMap;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Get or create input actions from InputContextManager if available
            if (InputContextManager.Instance != null)
            {
                _inputActions = InputContextManager.Instance.InputActions;
            }
            else
            {
                _inputActions = new DIGInputActions();
            }
            
            // Cache action maps using runtime lookup (works without code regeneration)
            CacheActionMaps();
        }
        
        private void CacheActionMaps()
        {
            if (_inputActions?.asset == null)
            {
                Debug.LogError("[ParadigmInputManager] InputActionAsset is null!");
                return;
            }
            
            _coreMap = _inputActions.asset.FindActionMap("Core", throwIfNotFound: false);
            _shooterMap = _inputActions.asset.FindActionMap("Combat_Shooter", throwIfNotFound: false);
            _mmoMap = _inputActions.asset.FindActionMap("Combat_MMO", throwIfNotFound: false);
            _arpgMap = _inputActions.asset.FindActionMap("Combat_ARPG", throwIfNotFound: false);
            _mobaMap = _inputActions.asset.FindActionMap("Combat_MOBA", throwIfNotFound: false);
            _uiMap = _inputActions.asset.FindActionMap("UI", throwIfNotFound: false);
            
            if (_coreMap == null)
                Debug.LogWarning("[ParadigmInputManager] Core action map not found! Check DIGInputActions.inputactions");
            if (_shooterMap == null)
                Debug.LogWarning("[ParadigmInputManager] Combat_Shooter action map not found!");
            if (_mmoMap == null)
                Debug.LogWarning("[ParadigmInputManager] Combat_MMO action map not found!");
            if (_arpgMap == null)
                Debug.LogWarning("[ParadigmInputManager] Combat_ARPG action map not found!");
        }
        
        private void OnEnable()
        {
            // Clear all stale input state before enabling maps.
            // Action map Disable() during scene transitions prevents 'canceled' callbacks,
            // leaving held-action flags (Jump, Sprint, Crouch, etc.) stuck at true.
            PlayerInputState.ResetAll();

            // Enable Core map (always enabled)
            _coreMap?.Enable();
            
            // Subscribe to paradigm changes
            if (ParadigmStateMachine.Instance != null)
            {
                // Get initial paradigm
                var activeProfile = ParadigmStateMachine.Instance.ActiveProfile;
                if (activeProfile != null)
                {
                    _currentParadigm = activeProfile.paradigm;
                }
                
                // Subscribe to paradigm changed event (instance event)
                ParadigmStateMachine.Instance.OnParadigmChanged += OnParadigmChanged;
            }
            
            // Apply initial paradigm
            ApplyParadigmMaps(_currentParadigm);
        }
        
        private void OnDisable()
        {
            if (ParadigmStateMachine.Instance != null)
            {
                ParadigmStateMachine.Instance.OnParadigmChanged -= OnParadigmChanged;
            }
            
            // Disable all maps
            _coreMap?.Disable();
            _shooterMap?.Disable();
            _mmoMap?.Disable();
            _arpgMap?.Disable();
            _mobaMap?.Disable();
        }
        
        private void OnParadigmChanged(InputParadigmProfile profile)
        {
            if (profile == null) return;
            
            _currentParadigm = profile.paradigm;
            ApplyParadigmMaps(_currentParadigm);
            
            Debug.Log($"[ParadigmInputManager] Switched to {_currentParadigm} action maps");
        }
        
        /// <summary>
        /// Applies the correct action maps for the given paradigm.
        /// Disables all combat maps, then enables only the appropriate one.
        /// </summary>
        private void ApplyParadigmMaps(InputParadigm paradigm)
        {
            // Disable all combat maps first
            _shooterMap?.Disable();
            _mmoMap?.Disable();
            _arpgMap?.Disable();
            _mobaMap?.Disable();
            
            // Enable the correct one
            switch (paradigm)
            {
                case InputParadigm.Shooter:
                case InputParadigm.TwinStick:
                    _shooterMap?.Enable();
                    break;
                    
                case InputParadigm.MMO:
                    _mmoMap?.Enable();
                    break;
                    
                case InputParadigm.ARPG:
                    _arpgMap?.Enable();
                    break;

                case InputParadigm.MOBA:
                    _mobaMap?.Enable();
                    break;

                default:
                    // Default to Shooter
                    _shooterMap?.Enable();
                    break;
            }
        }
        
        /// <summary>
        /// Force a paradigm change (for testing or explicit changes).
        /// </summary>
        public void SetParadigm(InputParadigm paradigm)
        {
            _currentParadigm = paradigm;
            ApplyParadigmMaps(paradigm);
        }
        
        /// <summary>
        /// Finds an action by name within all action maps.
        /// </summary>
        public InputAction FindAction(string actionName)
        {
            // Search in order of priority
            var action = _coreMap?.FindAction(actionName, throwIfNotFound: false);
            if (action != null) return action;
            
            action = _shooterMap?.FindAction(actionName, throwIfNotFound: false);
            if (action != null) return action;
            
            action = _mmoMap?.FindAction(actionName, throwIfNotFound: false);
            if (action != null) return action;
            
            action = _arpgMap?.FindAction(actionName, throwIfNotFound: false);
            if (action != null) return action;

            action = _mobaMap?.FindAction(actionName, throwIfNotFound: false);
            return action;
        }
    }
}
