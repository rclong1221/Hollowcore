using UnityEngine;
using Unity.Entities;
using DIG.CameraSystem;
using PlayerInputState = global::Player.Systems.PlayerInputState;

namespace DIG.Core.Input
{
    /// <summary>
    /// Runtime input scheme switching. Enforces scheme-to-camera-mode compatibility.
    /// Configures cursor state and notifies downstream systems.
    ///
    /// Works alongside InputContextManager (Gameplay/UI), not inside it.
    /// Schemes only apply during Gameplay context — ignored during UI.
    ///
    /// EPIC 15.18
    /// </summary>
    public class InputSchemeManager : MonoBehaviour
    {
        public static InputSchemeManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Default input scheme. Must be compatible with the initial camera mode.")]
        [SerializeField] private InputScheme _defaultScheme = InputScheme.HybridToggle;

        [Header("Debug")]
        [SerializeField] private bool _logSchemeChanges = false;

        /// <summary>Current active input scheme.</summary>
        public InputScheme ActiveScheme { get; private set; }

        /// <summary>True when HybridToggle modifier key is currently held.</summary>
        public bool IsTemporaryCursorActive { get; private set; }

        /// <summary>
        /// True when the cursor is currently free for hover/selection.
        /// Delegates to CursorController (paradigm system) when available for consistency.
        /// Fallback: TacticalCursor mode, OR HybridToggle with modifier held.
        /// </summary>
        public bool IsCursorFree
        {
            get
            {
                // Prefer paradigm system's CursorController as the authority
                var cursorController = CursorController.Instance;
                if (cursorController != null)
                {
                    return cursorController.IsCursorFree;
                }
                
                // Fallback to legacy logic
                return ActiveScheme == InputScheme.TacticalCursor
                    || (ActiveScheme == InputScheme.HybridToggle && IsTemporaryCursorActive);
            }
        }

        /// <summary>Fired when the active scheme changes.</summary>
        public event System.Action<InputScheme> OnSchemeChanged;

        /// <summary>Fired when IsCursorFree transitions. bool = new IsCursorFree state.</summary>
        public event System.Action<bool> OnCursorFreeChanged;

        private bool _wasCursorFree;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var go = new GameObject("[InputSchemeManager]");
            go.AddComponent<InputSchemeManager>();
            Debug.Log("[InputSchemeManager] Auto-initialized");
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

            ActiveScheme = _defaultScheme;
            IsTemporaryCursorActive = false;
            _wasCursorFree = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // EPIC 15.18: Process modifier key regardless of context
            // Health bar hover should work even with UI open
            if (ActiveScheme == InputScheme.HybridToggle)
            {
                // EPIC 15.21: Use PlayerInputState instead of direct keyboard access
                bool modifierHeld = PlayerInputState.ModAlt;
                
                if (modifierHeld && !IsTemporaryCursorActive)
                {
                    IsTemporaryCursorActive = true;
                    NotifyCursorFreeChange();
                }
                else if (!modifierHeld && IsTemporaryCursorActive)
                {
                    IsTemporaryCursorActive = false;
                    NotifyCursorFreeChange();
                }
            }

            // Sync ECS component
            SyncECSState();
        }

        /// <summary>
        /// Returns true if LookDelta should be zeroed (cursor is free, camera should not orbit).
        /// Called by PlayerInputReader.OnLook().
        /// </summary>
        public bool ShouldSuppressLookDelta() => IsCursorFree;

        /// <summary>
        /// Attempt to switch input scheme. 
        /// When CursorController (paradigm system) is active, we're more lenient since it handles cursor state.
        /// </summary>
        public bool TrySetScheme(InputScheme scheme)
        {
            if (scheme == ActiveScheme)
                return true;

            // When paradigm system is active, be lenient - CursorController handles actual cursor state
            bool hasParadigmSystem = CursorController.Instance != null;
            
            if (!hasParadigmSystem && !IsSchemeCompatibleWithCamera(scheme))
            {
                Debug.LogWarning($"[InputSchemeManager] Cannot switch to {scheme} — incompatible with current camera mode " +
                                 $"(SupportsOrbit={GetCameraSupportsOrbit()}, UsesCursorAim={GetCameraUsesCursorAim()})");
                return false;
            }

            var oldScheme = ActiveScheme;
            ActiveScheme = scheme;

            // Reset temporary state when switching schemes
            IsTemporaryCursorActive = false;

            if (_logSchemeChanges)
                Debug.Log($"[InputSchemeManager] Scheme changed: {oldScheme} → {scheme}");

            OnSchemeChanged?.Invoke(scheme);
            NotifyCursorFreeChange();
            return true;
        }

        /// <summary>
        /// Called when the camera mode changes. Auto-selects a compatible scheme.
        /// </summary>
        public void OnCameraModeChanged()
        {
            if (IsSchemeCompatibleWithCamera(ActiveScheme))
                return; // Current scheme still valid

            // Auto-select compatible scheme
            if (GetCameraSupportsOrbit())
            {
                // TPS/FPS camera — use ShooterDirect or HybridToggle
                TrySetScheme(InputScheme.ShooterDirect);
            }
            else
            {
                // Isometric/TopDown camera — use TacticalCursor
                TrySetScheme(InputScheme.TacticalCursor);
            }
        }

        private bool IsSchemeCompatibleWithCamera(InputScheme scheme)
        {
            bool supportsOrbit = GetCameraSupportsOrbit();
            bool usesCursorAim = GetCameraUsesCursorAim();

            return scheme switch
            {
                InputScheme.ShooterDirect => supportsOrbit,
                InputScheme.HybridToggle => supportsOrbit,
                InputScheme.TacticalCursor => usesCursorAim && !supportsOrbit,
                _ => true,
            };
        }

        private bool GetCameraSupportsOrbit()
        {
            var provider = CameraModeProvider.Instance;
            return provider == null || provider.SupportsOrbitRotation;
        }

        private bool GetCameraUsesCursorAim()
        {
            var provider = CameraModeProvider.Instance;
            return provider != null && provider.UsesCursorAiming;
        }

        private void NotifyCursorFreeChange()
        {
            bool nowFree = IsCursorFree;
            if (nowFree != _wasCursorFree)
            {
                _wasCursorFree = nowFree;
                OnCursorFreeChanged?.Invoke(nowFree);
            }
        }

        private void SyncECSState()
        {
            // Find the local player entity and sync InputSchemeState
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            // Use the query approach since we can't easily get the local player entity reference
            using var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<InputSchemeState>(),
                ComponentType.ReadOnly<Unity.NetCode.GhostOwnerIsLocal>());

            if (query.IsEmpty) return;

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                em.SetComponentData(entities[i], new InputSchemeState
                {
                    ActiveScheme = ActiveScheme,
                    IsTemporaryCursorActive = IsTemporaryCursorActive,
                });
            }
            entities.Dispose();
        }
    }
}
