using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DIG.Core.Input.Keybinds
{
    /// <summary>
    /// Service layer for keybind management.
    /// Provides rebinding, persistence, and action enumeration.
    /// Decoupled from UI - can be used programmatically.
    /// 
    /// EPIC 15.21 Phase 6: Keybind UI
    /// </summary>
    public static class KeybindService
    {
        private const string KEYBIND_PREFS_KEY = "DIG_Keybinds_v1";
        
        private static InputActionAsset _inputActions;
        private static InputActionRebindingExtensions.RebindingOperation _currentRebind;
        private static bool _initialized;
        
        /// <summary>
        /// Event fired when any binding changes.
        /// </summary>
        public static event Action<string> OnBindingChanged;
        
        /// <summary>
        /// Event fired when rebind operation starts.
        /// </summary>
        public static event Action<string> OnRebindStarted;
        
        /// <summary>
        /// Event fired when rebind operation completes or is cancelled.
        /// </summary>
        public static event Action<string, bool> OnRebindCompleted;
        
        /// <summary>
        /// Initialize the service with the input action asset.
        /// </summary>
        public static void Initialize(InputActionAsset inputActions)
        {
            _inputActions = inputActions;
            _initialized = true;
            LoadBindings();
        }
        
        /// <summary>
        /// Gets all bindable actions organized by category.
        /// </summary>
        public static List<BindableAction> GetAllBindableActions()
        {
            var actions = new List<BindableAction>();
            
            // Core actions (available in all paradigms)
            // EPIC 15.21: Break down Move composite for cleaner UI
            actions.Add(new BindableAction("Move", "Forward", "Movement", "Core", 1));  // Up (W)
            actions.Add(new BindableAction("Move", "Backward", "Movement", "Core", 2)); // Down (S)
            actions.Add(new BindableAction("Move", "Left", "Movement", "Core", 3));     // Left (A)
            actions.Add(new BindableAction("Move", "Right", "Movement", "Core", 4));    // Right (D)
            actions.Add(new BindableAction("Jump", "Jump", "Movement", "Core", 0));
            actions.Add(new BindableAction("Crouch", "Crouch", "Movement", "Core", 0));
            actions.Add(new BindableAction("Sprint", "Sprint", "Movement", "Core", 0));
            actions.Add(new BindableAction("Interact", "Interact", "Interaction", "Core", 0));
            actions.Add(new BindableAction("Reload", "Reload", "Combat", "Core", 0));
            actions.Add(new BindableAction("Grab", "Grab", "Interaction", "Core", 0));
            actions.Add(new BindableAction("Zoom", "Zoom", "Camera", "Core", 0));
            actions.Add(new BindableAction("ToggleFlashlight", "Toggle Flashlight", "Equipment", "Core", 0));
            
            // Equipment slots
            for (int i = 1; i <= 9; i++)
            {
                actions.Add(new BindableAction($"EquipSlot{i}", $"Equip Slot {i}", "Equipment", "Core", 0));
            }
            
            // Shooter-specific
            actions.Add(new BindableAction("Attack", "Attack", "Combat", "Combat_Shooter", 0, InputParadigm.Shooter));
            actions.Add(new BindableAction("AimDownSights", "Aim Down Sights", "Combat", "Combat_Shooter", 0, InputParadigm.Shooter));
            actions.Add(new BindableAction("LeanLeft", "Lean Left", "Movement", "Combat_Shooter", 0, InputParadigm.Shooter));
            actions.Add(new BindableAction("LeanRight", "Lean Right", "Movement", "Combat_Shooter", 0, InputParadigm.Shooter));
            actions.Add(new BindableAction("Prone", "Go Prone", "Movement", "Combat_Shooter", 0, InputParadigm.Shooter));
            actions.Add(new BindableAction("Slide", "Slide", "Movement", "Combat_Shooter", 0, InputParadigm.Shooter));
            actions.Add(new BindableAction("DodgeDive", "Dodge Dive", "Movement", "Combat_Shooter", 0, InputParadigm.Shooter));
            actions.Add(new BindableAction("FreeLook", "Free Look", "Camera", "Combat_Shooter", 0, InputParadigm.Shooter));
            
            // MMO-specific
            actions.Add(new BindableAction("SelectTarget", "Select Target", "Combat", "Combat_MMO", 0, InputParadigm.MMO));
            actions.Add(new BindableAction("CameraOrbit", "Camera Orbit", "Camera", "Combat_MMO", 0, InputParadigm.MMO));
            actions.Add(new BindableAction("DodgeRoll", "Dodge Roll", "Movement", "Combat_MMO", 0, InputParadigm.MMO));
            actions.Add(new BindableAction("StrafeLeft", "Strafe Left", "Movement", "Combat_MMO", 0, InputParadigm.MMO));
            actions.Add(new BindableAction("StrafeRight", "Strafe Right", "Movement", "Combat_MMO", 0, InputParadigm.MMO));
            
            // ARPG-specific
            actions.Add(new BindableAction("AttackAtCursor", "Attack at Cursor", "Combat", "Combat_ARPG", 0, InputParadigm.ARPG));
            actions.Add(new BindableAction("MoveToClick", "Move to Click", "Movement", "Combat_ARPG", 0, InputParadigm.ARPG));
            
            return actions;
        }
        
        /// <summary>
        /// Gets the current binding display text for an action.
        /// </summary>
        public static string GetBindingDisplayString(string actionMap, string actionName, int bindingIndex = 0)
        {
            EnsureInitialized();
            
            var map = _inputActions?.FindActionMap(actionMap);
            if (map == null) return "[Not Found]";
            
            var action = map.FindAction(actionName);
            if (action == null) return "[Not Found]";
            
            if (bindingIndex >= action.bindings.Count) return "[N/A]";
            
            return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontOmitDevice);
        }
        
        /// <summary>
        /// Starts an interactive rebind operation.
        /// </summary>
        public static void StartRebind(BindableAction bindableAction, Action<bool> onComplete = null)
        {
            EnsureInitialized();
            
            // Cancel any existing rebind
            CancelRebind();
            
            var map = _inputActions?.FindActionMap(bindableAction.ActionMap);
            if (map == null)
            {
                Debug.LogWarning($"[KeybindService] Action map '{bindableAction.ActionMap}' not found");
                onComplete?.Invoke(false);
                return;
            }
            
            var action = map.FindAction(bindableAction.ActionName);
            if (action == null)
            {
                Debug.LogWarning($"[KeybindService] Action '{bindableAction.ActionName}' not found in map '{bindableAction.ActionMap}'");
                onComplete?.Invoke(false);
                return;
            }
            
            // Disable action during rebind
            action.Disable();
            
            OnRebindStarted?.Invoke(bindableAction.ActionName);
            
            _currentRebind = action.PerformInteractiveRebinding(bindableAction.BindingIndex)
                .WithControlsExcluding("Mouse") // Don't allow mouse position
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(10f)
                .OnComplete(operation =>
                {
                    action.Enable();
                    _currentRebind?.Dispose();
                    _currentRebind = null;
                    
                    SaveBindings();
                    OnBindingChanged?.Invoke(bindableAction.ActionName);
                    OnRebindCompleted?.Invoke(bindableAction.ActionName, true);
                    onComplete?.Invoke(true);
                })
                .OnCancel(operation =>
                {
                    action.Enable();
                    _currentRebind?.Dispose();
                    _currentRebind = null;
                    
                    OnRebindCompleted?.Invoke(bindableAction.ActionName, false);
                    onComplete?.Invoke(false);
                })
                .Start();
        }
        
        /// <summary>
        /// Cancels the current rebind operation if one is in progress.
        /// </summary>
        public static void CancelRebind()
        {
            if (_currentRebind != null)
            {
                _currentRebind.Cancel();
                _currentRebind.Dispose();
                _currentRebind = null;
            }
        }
        
        /// <summary>
        /// Resets all bindings to their defaults.
        /// </summary>
        public static void ResetToDefaults()
        {
            EnsureInitialized();
            
            if (_inputActions != null)
            {
                _inputActions.RemoveAllBindingOverrides();
                PlayerPrefs.DeleteKey(KEYBIND_PREFS_KEY);
                PlayerPrefs.Save();
                OnBindingChanged?.Invoke(null); // null indicates all bindings changed
            }
        }
        
        /// <summary>
        /// Saves current binding overrides to PlayerPrefs.
        /// </summary>
        public static void SaveBindings()
        {
            EnsureInitialized();
            
            if (_inputActions != null)
            {
                string json = _inputActions.SaveBindingOverridesAsJson();
                PlayerPrefs.SetString(KEYBIND_PREFS_KEY, json);
                PlayerPrefs.Save();
            }
        }
        
        /// <summary>
        /// Loads binding overrides from PlayerPrefs.
        /// </summary>
        public static void LoadBindings()
        {
            EnsureInitialized();
            
            if (_inputActions != null)
            {
                string json = PlayerPrefs.GetString(KEYBIND_PREFS_KEY, "");
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        _inputActions.LoadBindingOverridesFromJson(json);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[KeybindService] Failed to load keybinds: {e.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Checks if a rebind is currently in progress.
        /// </summary>
        public static bool IsRebinding => _currentRebind != null;
        
        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Debug.LogWarning("[KeybindService] Not initialized. Call Initialize() first.");
            }
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _inputActions = null;
            _currentRebind = null;
            _initialized = false;
        }
    }
}
