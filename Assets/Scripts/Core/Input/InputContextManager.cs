using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Player.Systems;

namespace DIG.Core.Input
{
    /// <summary>
    /// Context states for input management
    /// </summary>
    public enum InputContext
    {
        Gameplay,
        UI,
        /// <summary>EPIC 18.10: Spectator mode — Core map enabled, cursor free for timeline UI.</summary>
        Spectator,
        /// <summary>EPIC 18.13: Death spectator — Core map for camera movement, limited combat input.</summary>
        DeathSpectator
    }

    /// <summary>
    /// Manages input context switching between Gameplay and UI modes.
    /// Uses a stack-based approach: Push to enter a new context, Pop to return.
    /// 
    /// EPIC 15.21: Updated to work with new action map structure (Core + Combat_* + UI).
    /// Uses runtime action map lookup for reliability.
    /// </summary>
    public class InputContextManager : MonoBehaviour
    {
        public static InputContextManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("Reference to the DIGInputActions asset (auto-created if null)")]
        [SerializeField] private DIGInputActions _inputActions;

        [Header("Debug")]
        [SerializeField] private bool _logContextChanges = false;

        private Stack<InputContext> _contextStack = new();
        
        // Cached action maps (runtime lookup)
        private InputActionMap _coreMap;
        private InputActionMap _uiMap;
        
        /// <summary>Current active input context</summary>
        public InputContext CurrentContext => _contextStack.Count > 0 ? _contextStack.Peek() : InputContext.Gameplay;

        /// <summary>Direct access to the input actions for external subscribers</summary>
        public DIGInputActions InputActions => _inputActions;

        /// <summary>
        /// Auto-create at runtime so input works without manual scene setup.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;
            
            var go = new GameObject("[InputContextManager]");
            go.AddComponent<InputContextManager>();
            // DontDestroyOnLoad is handled in Awake
            Debug.Log("[InputContextManager] Auto-initialized");
        }

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize input actions
            _inputActions ??= new DIGInputActions();
            
            // Cache action maps using runtime lookup
            CacheActionMaps();

            // Start in UI context so menu is usable
            // Gameplay systems should call Push(InputContext.Gameplay) when player joins a game
            _contextStack.Push(InputContext.UI);
            ApplyContext(InputContext.UI);
        }
        
        private void CacheActionMaps()
        {
            if (_inputActions?.asset == null) return;
            
            _coreMap = _inputActions.asset.FindActionMap("Core", throwIfNotFound: false);
            _uiMap = _inputActions.asset.FindActionMap("UI", throwIfNotFound: false);
            
            if (_coreMap == null)
                Debug.LogWarning("[InputContextManager] Core action map not found!");
            if (_uiMap == null)
                Debug.LogWarning("[InputContextManager] UI action map not found!");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            _inputActions?.Dispose();
        }

        /// <summary>
        /// Push a new input context onto the stack and activate it.
        /// </summary>
        public void Push(InputContext context)
        {
            _contextStack.Push(context);
            ApplyContext(context);
            
            if (_logContextChanges)
                Debug.Log($"[InputContextManager] Pushed context: {context} (stack depth: {_contextStack.Count})");
        }

        /// <summary>
        /// Pop the current context and return to the previous one.
        /// Cannot pop the last context (always stays in at least one).
        /// </summary>
        public void Pop()
        {
            if (_contextStack.Count <= 1)
            {
                Debug.LogWarning("[InputContextManager] Cannot pop the last context!");
                return;
            }

            var popped = _contextStack.Pop();
            var current = _contextStack.Peek();
            ApplyContext(current);

            if (_logContextChanges)
                Debug.Log($"[InputContextManager] Popped {popped}, now in: {current} (stack depth: {_contextStack.Count})");
        }

        /// <summary>
        /// Force set a specific context (clears stack and sets this as the only context).
        /// </summary>
        public void SetContext(InputContext context)
        {
            _contextStack.Clear();
            _contextStack.Push(context);
            ApplyContext(context);

            if (_logContextChanges)
                Debug.Log($"[InputContextManager] Force set context: {context}");
        }

        private void ApplyContext(InputContext context)
        {
            // Disable all maps first
            _coreMap?.Disable();
            _uiMap?.Disable();

            // Clear stale input state when switching contexts.
            // Disabling maps above prevents 'canceled' callbacks for held keys,
            // so flags like Jump/Sprint/Crouch can get stuck at true.
            PlayerInputState.ResetAll();

            switch (context)
            {
                case InputContext.Gameplay:
                    // Enable Core map - combat maps are managed by ParadigmInputManager
                    _coreMap?.Enable();
                    // NOTE: Cursor locking is handled by PlayerInputSystem, not here
                    break;

                case InputContext.UI:
                    _uiMap?.Enable();
                    // NOTE: Cursor unlocking is handled by PlayerInputSystem (Escape key)
                    break;

                case InputContext.Spectator:
                    // EPIC 18.10: Core map for WASD camera movement, UI map for timeline interaction
                    _coreMap?.Enable();
                    _uiMap?.Enable();
                    break;

                case InputContext.DeathSpectator:
                    // EPIC 18.13: Core map for camera movement (Tab, 1-9 keys), no combat
                    _coreMap?.Enable();
                    break;
            }
        }

        /// <summary>
        /// Check if we're currently in a specific context
        /// </summary>
        public bool IsInContext(InputContext context) => CurrentContext == context;
    }
}
