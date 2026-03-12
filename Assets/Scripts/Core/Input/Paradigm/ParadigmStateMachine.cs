using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Entities;
using Unity.NetCode;
using DIG.CameraSystem;
using DIG.CameraSystem.Cinemachine;

namespace DIG.Core.Input
{
    /// <summary>
    /// State machine coordinator for input paradigm transitions.
    /// Ensures atomic transitions with rollback support.
    /// 
    /// Key features:
    /// - Pre-validates all subsystems before any configuration
    /// - Captures snapshots for rollback on failure
    /// - Configures subsystems in priority order
    /// - Rolls back on any failure
    /// - Syncs state to ECS for gameplay systems
    /// 
    /// EPIC 15.20 - State Machine Coordinator Architecture
    /// </summary>
    public class ParadigmStateMachine : MonoBehaviour, IInputParadigmProvider
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static ParadigmStateMachine Instance { get; private set; }

        // ============================================================
        // CONFIGURATION
        // ============================================================

        [Header("Configuration")]
        [Tooltip("Default paradigm profile to use on startup.")]
        [SerializeField] private InputParadigmProfile _defaultProfile;

        [Tooltip("All available paradigm profiles.")]
        [SerializeField] private InputParadigmProfile[] _availableProfiles;

        [Header("Debug")]
        [SerializeField] private bool _logTransitions = true;

        // ============================================================
        // RUNTIME STATE
        // ============================================================

        [Header("Runtime State (Read-Only)")]
        [SerializeField] private ParadigmState _state = ParadigmState.Stable;
        [SerializeField] private InputParadigmProfile _activeProfile;
        [SerializeField] private InputModeOverlay _activeModeOverlay = InputModeOverlay.None;

        // Registered configurable subsystems (sorted by ConfigurationOrder)
        private readonly SortedList<int, IParadigmConfigurable> _configurables = new();

        // ============================================================
        // IInputParadigmProvider IMPLEMENTATION
        // ============================================================

        public InputParadigmProfile ActiveProfile => _activeProfile;
        public IReadOnlyList<InputParadigmProfile> AvailableProfiles => _availableProfiles;
        public InputModeOverlay ActiveModeOverlay => _activeModeOverlay;
        public ParadigmState CurrentState => _state;

        public event Action<InputParadigmProfile> OnParadigmChanged;
        public event Action<InputModeOverlay> OnModeOverlayChanged;
        public event Action<InputParadigmProfile, InputParadigmProfile> OnTransitionStarted;
        public event Action<InputParadigmProfile, bool> OnTransitionCompleted;

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        // NOTE: Removed RuntimeInitializeOnLoadMethod - use scene-based instance instead
        // This allows Inspector configuration of default profile and available profiles

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ParadigmStateMachine] Duplicate instance found, destroying this one. Keep only one in scene.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Don't use DontDestroyOnLoad - let scene manage lifecycle

            // Register with ServiceLocator
            ServiceLocator.Register<IInputParadigmProvider>(this);
        }

        private void Start()
        {
            // Subscribe to camera changes for bidirectional compatibility
            if (CameraModeProvider.HasInstance)
            {
                CameraModeProvider.Instance.OnCameraChanged += OnCameraChanged;
            }

            // Delayed initialization to allow subsystems to register
            if (_defaultProfile != null)
            {
                TrySetParadigm(_defaultProfile);
            }
            else if (_availableProfiles != null && _availableProfiles.Length > 0)
            {
                TrySetParadigm(_availableProfiles[0]);
            }
            else
            {
                Debug.LogWarning("[ParadigmStateMachine] No default profile configured. Create profiles via Assets > Create > DIG/Input/Input Paradigm Profile");
            }
        }

        private void Update()
        {
            // Sync dynamic state to ECS each frame (e.g., IsOrbitActive changes with RMB hold)
            SyncDynamicStateToECS();
        }

        private void OnDestroy()
        {
            // Unsubscribe from camera changes
            if (CameraModeProvider.HasInstance)
            {
                CameraModeProvider.Instance.OnCameraChanged -= OnCameraChanged;
            }

            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<IInputParadigmProvider>();
            }
        }

        // ============================================================
        // CAMERA CHANGE HANDLING
        // ============================================================

        /// <summary>
        /// Called when camera mode changes. Validates current paradigm is still compatible.
        /// If not, attempts to switch to a compatible paradigm automatically.
        /// </summary>
        private void OnCameraChanged(CameraMode previousMode, CameraMode newMode)
        {
            if (_activeProfile == null) return;

            // Check if current paradigm is still compatible
            if (_activeProfile.IsCompatibleWith(newMode))
            {
                if (_logTransitions)
                {
                    Debug.Log($"[ParadigmStateMachine] Camera changed to {newMode}, paradigm {_activeProfile.displayName} still compatible");
                }
                return;
            }

            // Current paradigm is incompatible - find a compatible one
            Debug.LogWarning($"[ParadigmStateMachine] Camera changed to {newMode}, paradigm {_activeProfile.displayName} is now incompatible");

            // Try to find a compatible paradigm
            InputParadigmProfile compatibleProfile = null;
            if (_availableProfiles != null)
            {
                foreach (var profile in _availableProfiles)
                {
                    if (profile != null && profile.IsCompatibleWith(newMode))
                    {
                        compatibleProfile = profile;
                        break;
                    }
                }
            }

            if (compatibleProfile != null)
            {
                Debug.Log($"[ParadigmStateMachine] Auto-switching to compatible paradigm: {compatibleProfile.displayName}");
                TrySetParadigm(compatibleProfile);
            }
            else
            {
                Debug.LogError($"[ParadigmStateMachine] No compatible paradigm found for camera mode: {newMode}");
            }
        }

        // ============================================================
        // SUBSYSTEM REGISTRATION
        // ============================================================

        /// <summary>
        /// Register a configurable subsystem.
        /// Called by subsystems in their Awake/OnEnable.
        /// </summary>
        public void RegisterConfigurable(IParadigmConfigurable configurable)
        {
            if (configurable == null) return;

            // Handle duplicate order by offsetting
            var order = configurable.ConfigurationOrder;
            while (_configurables.ContainsKey(order))
            {
                order++;
            }

            _configurables[order] = configurable;

            if (_logTransitions)
            {
                Debug.Log($"[ParadigmStateMachine] Registered: {configurable.SubsystemName} (order: {order})");
            }

            // If we already have an active profile, configure the new subsystem
            if (_activeProfile != null && _state == ParadigmState.Stable)
            {
                try
                {
                    if (configurable.CanConfigure(_activeProfile, out _))
                    {
                        configurable.Configure(_activeProfile);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ParadigmStateMachine] Failed to configure late-registered {configurable.SubsystemName}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Unregister a configurable subsystem.
        /// </summary>
        public void UnregisterConfigurable(IParadigmConfigurable configurable)
        {
            if (configurable == null) return;

            // Find and remove by value since order might have been modified
            int? keyToRemove = null;
            foreach (var kvp in _configurables)
            {
                if (kvp.Value == configurable)
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }

            if (keyToRemove.HasValue)
            {
                _configurables.Remove(keyToRemove.Value);
                if (_logTransitions)
                {
                    Debug.Log($"[ParadigmStateMachine] Unregistered: {configurable.SubsystemName}");
                }
            }
        }

        // ============================================================
        // PARADIGM SWITCHING
        // ============================================================

        public bool TrySetParadigm(InputParadigmProfile profile)
        {
            if (profile == null)
            {
                Debug.LogWarning("[ParadigmStateMachine] Cannot set null profile");
                return false;
            }

            // Already active
            if (_activeProfile == profile)
                return true;

            // Reject if transition in progress
            if (_state == ParadigmState.Transitioning)
            {
                Debug.LogWarning($"[ParadigmStateMachine] Cannot switch to {profile.displayName} — transition in progress");
                return false;
            }

            // Check camera compatibility
            if (!IsParadigmCompatible(profile))
            {
                Debug.LogWarning($"[ParadigmStateMachine] {profile.displayName} incompatible with current camera mode");
                return false;
            }

            return ExecuteTransition(profile);
        }

        public bool TrySetParadigm(InputParadigm paradigm)
        {
            if (_availableProfiles == null) return false;

            var profile = System.Array.Find(_availableProfiles, p => p != null && p.paradigm == paradigm);
            if (profile == null)
            {
                Debug.LogWarning($"[ParadigmStateMachine] No profile found for paradigm: {paradigm}");
                return false;
            }
            return TrySetParadigm(profile);
        }

        /// <summary>
        /// Sets the active paradigm by profile asset name (e.g., "Profile_ARPG_Classic").
        /// Useful when you need to select a specific profile variant.
        /// </summary>
        public bool TrySetParadigmByName(string profileName)
        {
            if (_availableProfiles == null)
            {
                Debug.LogWarning($"[ParadigmStateMachine] TrySetParadigmByName('{profileName}'): _availableProfiles is null!");
                return false;
            }

            Debug.Log($"[ParadigmStateMachine] TrySetParadigmByName('{profileName}'): Searching {_availableProfiles.Length} profiles...");
            
            // Normalize the requested name for flexible matching
            // e.g., "Profile_ARPG_Classic" -> "arpgclassic"
            string normalizedRequest = NormalizeForMatch(profileName.Replace("Profile_", ""));
            
            InputParadigmProfile profile = null;
            int bestScore = 0;
            
            foreach (var p in _availableProfiles)
            {
                if (p == null) continue;
                
                string assetName = p.name ?? "";
                string display = p.displayName ?? "";
                
                Debug.Log($"[ParadigmStateMachine]   Checking: assetName='{assetName}', displayName='{display}'");
                
                // Exact asset name match (highest priority)
                if (!string.IsNullOrEmpty(assetName) && assetName == profileName)
                {
                    profile = p;
                    bestScore = 100;
                    break;
                }
                
                // Normalized displayName match
                // e.g., "Shooter/Souls" -> "shootersouls", "ARPG (Classic)" -> "arpgclassic"
                string normalizedDisplay = NormalizeForMatch(display);
                
                if (normalizedRequest == normalizedDisplay && bestScore < 90)
                {
                    profile = p;
                    bestScore = 90;
                }
                // Check if request is a substring of displayName or vice versa
                else if (normalizedDisplay.Contains(normalizedRequest) && bestScore < 80)
                {
                    profile = p;
                    bestScore = 80;
                }
                else if (normalizedRequest.Contains(normalizedDisplay) && bestScore < 70)
                {
                    profile = p;
                    bestScore = 70;
                }
            }
            
            if (profile == null)
            {
                // List available profiles for debugging
                var names = string.Join(", ", System.Array.FindAll(_availableProfiles, p => p != null)
                    .Select(p => $"'{p.name ?? "(null)"}'/'{p.displayName ?? "(null)"}'"));
                Debug.LogWarning($"[ParadigmStateMachine] No profile found with name: '{profileName}'. Available: [{names}]");
                return false;
            }
            
            Debug.Log($"[ParadigmStateMachine] Found profile: {profile.displayName} (score={bestScore}, cursorFreeByDefault={profile.cursorFreeByDefault})");
            return TrySetParadigm(profile);
        }
        
        /// <summary>
        /// Normalizes a string for flexible profile name matching.
        /// Removes special characters, spaces, and converts to lowercase.
        /// </summary>
        private static string NormalizeForMatch(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        public void SetModeOverlay(InputModeOverlay overlay)
        {
            if (_activeModeOverlay == overlay) return;

            _activeModeOverlay = overlay;
            SyncToECS();
            OnModeOverlayChanged?.Invoke(overlay);

            if (_logTransitions)
            {
                Debug.Log($"[ParadigmStateMachine] Mode overlay: {overlay}");
            }
        }

        public bool IsParadigmCompatible(InputParadigmProfile profile)
        {
            if (profile == null) return false;

            // Get current camera mode
            var cameraProvider = CameraModeProvider.Instance;
            if (cameraProvider == null || !cameraProvider.HasActiveCamera)
                return true; // No camera = allow anything

            return profile.IsCompatibleWith(cameraProvider.CurrentMode);
        }

        /// <summary>
        /// Get all paradigm profiles compatible with the current camera mode.
        /// Useful for UI to show only valid options.
        /// </summary>
        public IReadOnlyList<InputParadigmProfile> GetCompatibleProfiles()
        {
            var cameraProvider = CameraModeProvider.Instance;
            if (cameraProvider == null || !cameraProvider.HasActiveCamera || _availableProfiles == null)
                return _availableProfiles ?? System.Array.Empty<InputParadigmProfile>();

            var currentMode = cameraProvider.CurrentMode;
            var compatible = new List<InputParadigmProfile>();
            
            foreach (var profile in _availableProfiles)
            {
                if (profile != null && profile.IsCompatibleWith(currentMode))
                {
                    compatible.Add(profile);
                }
            }
            
            return compatible;
        }

        /// <summary>
        /// Get all paradigm profiles compatible with a specific camera mode.
        /// Useful for checking compatibility before switching cameras.
        /// </summary>
        public IReadOnlyList<InputParadigmProfile> GetCompatibleProfiles(CameraMode cameraMode)
        {
            if (_availableProfiles == null)
                return System.Array.Empty<InputParadigmProfile>();

            var compatible = new List<InputParadigmProfile>();
            
            foreach (var profile in _availableProfiles)
            {
                if (profile != null && profile.IsCompatibleWith(cameraMode))
                {
                    compatible.Add(profile);
                }
            }
            
            return compatible;
        }

        /// <summary>
        /// Switch to a paradigm, automatically switching camera if needed.
        /// Use this when you want to force a paradigm regardless of current camera.
        /// </summary>
        /// <param name="profile">Target paradigm profile.</param>
        /// <param name="preferredCameraMode">Camera mode to switch to if current is incompatible.</param>
        /// <returns>True if successful.</returns>
        public bool SwitchToParadigmWithCamera(InputParadigmProfile profile, CameraMode preferredCameraMode)
        {
            if (profile == null) return false;

            // Check if current camera is compatible
            if (IsParadigmCompatible(profile))
            {
                return TrySetParadigm(profile);
            }

            // Need to switch camera first
            if (!CinemachineCameraController.HasInstance)
            {
                Debug.LogWarning("[ParadigmStateMachine] Cannot switch camera - no CinemachineCameraController available");
                return false;
            }

            // Switch camera to the preferred mode for this paradigm
            // Map CameraMode enum to CinemachineCameraMode
            var cinemachineMode = preferredCameraMode switch
            {
                CameraMode.IsometricFixed => CinemachineCameraMode.Isometric,
                CameraMode.IsometricRotatable => CinemachineCameraMode.Isometric,
                CameraMode.TopDownFixed => CinemachineCameraMode.Isometric,
                CameraMode.FirstPerson => CinemachineCameraMode.FirstPerson,
                _ => CinemachineCameraMode.ThirdPerson
            };
            
            CinemachineCameraController.Instance.SetCameraMode(cinemachineMode);
            Debug.Log($"[ParadigmStateMachine] Switched camera to {cinemachineMode} for paradigm {profile.displayName}");

            // Camera changed event will fire, but we want specific paradigm
            // So set it directly after camera switch
            return TrySetParadigm(profile);
        }

        /// <summary>
        /// Switch to a paradigm by type, automatically finding compatible camera.
        /// </summary>
        public bool SwitchToParadigmWithCamera(InputParadigm paradigm)
        {
            var profile = System.Array.Find(_availableProfiles, p => p != null && p.paradigm == paradigm);
            if (profile == null)
            {
                Debug.LogWarning($"[ParadigmStateMachine] No profile found for {paradigm}");
                return false;
            }

            // Determine best camera for this paradigm
            CameraMode preferredCamera = paradigm switch
            {
                InputParadigm.Shooter => CameraMode.ThirdPersonFollow,
                InputParadigm.MMO => CameraMode.ThirdPersonFollow,
                InputParadigm.ARPG => CameraMode.IsometricFixed,
                InputParadigm.MOBA => CameraMode.TopDownFixed,
                InputParadigm.TwinStick => CameraMode.IsometricFixed,
                _ => CameraMode.ThirdPersonFollow
            };

            return SwitchToParadigmWithCamera(profile, preferredCamera);
        }

        // ============================================================
        // TRANSITION EXECUTION
        // ============================================================

        private bool ExecuteTransition(InputParadigmProfile targetProfile)
        {
            var previousProfile = _activeProfile;

            // Phase 1: Enter transitioning state
            _state = ParadigmState.Transitioning;
            OnTransitionStarted?.Invoke(previousProfile, targetProfile);

            if (_logTransitions)
            {
                Debug.Log($"[ParadigmStateMachine] Transition started: {previousProfile?.displayName ?? "None"} → {targetProfile.displayName}");
            }

            // Phase 2: Pre-validate all subsystems
            foreach (var kvp in _configurables)
            {
                var subsystem = kvp.Value;
                if (!subsystem.CanConfigure(targetProfile, out string errorReason))
                {
                    Debug.LogError($"[ParadigmStateMachine] Pre-validation failed: {subsystem.SubsystemName} - {errorReason}");
                    _state = ParadigmState.Stable;
                    OnTransitionCompleted?.Invoke(targetProfile, false);
                    return false;
                }
            }

            // Phase 3: Capture snapshots for rollback
            var snapshots = new Dictionary<IParadigmConfigurable, IConfigSnapshot>();
            foreach (var kvp in _configurables)
            {
                var subsystem = kvp.Value;
                try
                {
                    snapshots[subsystem] = subsystem.CaptureSnapshot();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ParadigmStateMachine] Snapshot capture failed: {subsystem.SubsystemName} - {e.Message}");
                    _state = ParadigmState.Stable;
                    OnTransitionCompleted?.Invoke(targetProfile, false);
                    return false;
                }
            }

            // Phase 4: Configure subsystems in order
            var configuredSubsystems = new List<IParadigmConfigurable>();
            bool success = true;

            foreach (var kvp in _configurables)
            {
                var subsystem = kvp.Value;
                try
                {
                    subsystem.Configure(targetProfile);
                    configuredSubsystems.Add(subsystem);

                    if (_logTransitions)
                    {
                        Debug.Log($"[ParadigmStateMachine] Configured: {subsystem.SubsystemName}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ParadigmStateMachine] Configuration failed: {subsystem.SubsystemName} - {e.Message}");
                    success = false;
                    break;
                }
            }

            // Phase 5: Rollback on failure
            if (!success)
            {
                if (_logTransitions)
                {
                    Debug.Log($"[ParadigmStateMachine] Rolling back {configuredSubsystems.Count} subsystems");
                }

                // Rollback in reverse order
                for (int i = configuredSubsystems.Count - 1; i >= 0; i--)
                {
                    var subsystem = configuredSubsystems[i];
                    try
                    {
                        if (snapshots.TryGetValue(subsystem, out var snapshot))
                        {
                            subsystem.Rollback(snapshot);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ParadigmStateMachine] Rollback failed: {subsystem.SubsystemName} - {e.Message}");
                    }
                }

                _state = ParadigmState.Stable;
                OnTransitionCompleted?.Invoke(targetProfile, false);
                return false;
            }

            // Phase 6: Commit
            _activeProfile = targetProfile;
            _state = ParadigmState.Stable;

            // Sync to ECS
            SyncToECS();

            // Notify legacy InputSchemeManager for backwards compatibility
            NotifyLegacySystems(targetProfile);

            // Fire success events
            OnParadigmChanged?.Invoke(targetProfile);
            OnTransitionCompleted?.Invoke(targetProfile, true);

            if (_logTransitions)
            {
                Debug.Log($"[ParadigmStateMachine] Transition complete: {targetProfile.displayName}");
            }

            return true;
        }

        // ============================================================
        // ECS SYNC
        // ============================================================

        private void SyncToECS()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[ParadigmStateMachine] SyncToECS: No world available");
                return;
            }

            var em = world.EntityManager;

            // Find local player entities with InputParadigmState
            using var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<InputParadigmState>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());

            if (query.IsEmpty)
            {
                // Debug: Check if we just don't have both components
                using var stateOnlyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<InputParadigmState>());
                using var localOnlyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<GhostOwnerIsLocal>());
                Debug.LogWarning($"[ParadigmStateMachine] SyncToECS: Query empty! " +
                    $"HasInputParadigmState={!stateOnlyQuery.IsEmpty} HasGhostOwnerIsLocal={!localOnlyQuery.IsEmpty}");
                return;
            }

            // Get runtime state from subsystems
            var movementRouter = MovementRouter.Instance;
            var cameraOrbit = CameraOrbitController.Instance;

            var state = new InputParadigmState
            {
                ActiveParadigm = _activeProfile?.paradigm ?? InputParadigm.Shooter,
                FacingMode = _activeProfile?.facingMode ?? MovementFacingMode.CameraForward,
                IsClickToMoveEnabled = _activeProfile?.clickToMoveEnabled ?? false,
                ClickToMoveButton = _activeProfile?.clickToMoveButton ?? ClickToMoveButton.None,
                ActiveModeOverlay = _activeModeOverlay,
                IsWASDEnabled = movementRouter?.IsWASDEnabled ?? true,
                ADTurnsCharacter = movementRouter?.ADTurnsCharacter ?? false,
                IsOrbitActive = cameraOrbit?.IsOrbitActive ?? false,
                CameraOrbitMode = cameraOrbit?.CurrentOrbitMode ?? CameraOrbitMode.AlwaysOrbit,
                UseScreenRelativeMovement = _activeProfile?.useScreenRelativeMovement ?? false,
            };

            // Debug: Log what we're syncing
            Debug.Log($"[ParadigmStateMachine] SyncToECS: profile={_activeProfile?.displayName} useScreenRelativeMovement={_activeProfile?.useScreenRelativeMovement}");

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                em.SetComponentData(entities[i], state);
            }
            entities.Dispose();
        }

        /// <summary>
        /// Sync dynamic state to ECS each frame (e.g., IsOrbitActive changes with RMB hold).
        /// This is more efficient than full SyncToECS() since it only updates changing fields.
        /// </summary>
        private void SyncDynamicStateToECS()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;

            using var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<InputParadigmState>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());

            if (query.IsEmpty) return;

            var cameraOrbit = CameraOrbitController.Instance;
            bool isOrbitActive = cameraOrbit?.IsOrbitActive ?? false;

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var current = em.GetComponentData<InputParadigmState>(entities[i]);
                if (current.IsOrbitActive != isOrbitActive)
                {
                    current.IsOrbitActive = isOrbitActive;
                    em.SetComponentData(entities[i], current);
                }
            }
            entities.Dispose();
        }

        private void NotifyLegacySystems(InputParadigmProfile profile)
        {
            // Update InputSchemeManager for backwards compatibility
            var schemeManager = InputSchemeManager.Instance;
            if (schemeManager != null)
            {
                schemeManager.TrySetScheme(profile.ToInputScheme());
            }
        }

        // ============================================================
        // EDITOR HELPERS
        // ============================================================

#if UNITY_EDITOR
        [ContextMenu("Log Registered Subsystems")]
        private void LogRegisteredSubsystems()
        {
            Debug.Log($"[ParadigmStateMachine] {_configurables.Count} registered subsystems:");
            foreach (var kvp in _configurables)
            {
                Debug.Log($"  [{kvp.Key}] {kvp.Value.SubsystemName}");
            }
        }

        [ContextMenu("Force Refresh Current Paradigm")]
        private void ForceRefreshParadigm()
        {
            if (_activeProfile != null)
            {
                var profile = _activeProfile;
                _activeProfile = null;
                TrySetParadigm(profile);
            }
        }
#endif
    }
}
