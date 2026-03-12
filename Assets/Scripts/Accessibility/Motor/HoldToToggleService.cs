using System.Collections.Generic;
using UnityEngine;

namespace DIG.Accessibility.Motor
{
    /// <summary>
    /// EPIC 18.12: Hold-to-toggle conversion for motor accessibility.
    /// When enabled for an action, first press activates, second press deactivates.
    /// Checked inline by PlayerInputReader hold-action callbacks.
    /// </summary>
    public static class HoldToToggleService
    {
        private static readonly HashSet<string> _enabledActions = new();
        private static readonly Dictionary<string, bool> _toggleStates = new();

        private const string PrefPrefix = "Access_Toggle_";

        /// <summary>Supported toggle actions.</summary>
        public static readonly string[] SupportedActions = { "Sprint", "Aim", "Crouch", "Block" };

        /// <summary>Whether toggle mode is enabled for this action.</summary>
        public static bool IsToggleEnabled(string actionName) => _enabledActions.Contains(actionName);

        /// <summary>Get current toggle state for an action (true = active).</summary>
        public static bool GetToggleState(string actionName) =>
            _toggleStates.TryGetValue(actionName, out bool state) && state;

        /// <summary>Enable/disable toggle mode for an action.</summary>
        public static void SetToggleEnabled(string actionName, bool enabled)
        {
            if (enabled)
                _enabledActions.Add(actionName);
            else
            {
                _enabledActions.Remove(actionName);
                _toggleStates.Remove(actionName);
            }
            PlayerPrefs.SetInt(PrefPrefix + actionName, enabled ? 1 : 0);
        }

        /// <summary>
        /// Called when a hold-action's performed callback fires.
        /// Returns the new state (true = should be active, false = should be inactive).
        /// </summary>
        public static bool OnActionPerformed(string actionName)
        {
            if (!_enabledActions.Contains(actionName)) return true; // Not toggled, normal press = active

            bool current = _toggleStates.TryGetValue(actionName, out bool state) && state;
            bool newState = !current;
            _toggleStates[actionName] = newState;
            return newState;
        }

        /// <summary>
        /// Called when a hold-action's canceled callback fires.
        /// Returns true if the action should remain active (toggle mode keeps it on).
        /// </summary>
        public static bool ShouldRemainActive(string actionName)
        {
            if (!_enabledActions.Contains(actionName)) return false; // Normal hold: release = deactivate
            return _toggleStates.TryGetValue(actionName, out bool state) && state;
        }

        /// <summary>Load toggle settings from PlayerPrefs.</summary>
        public static void LoadSettings()
        {
            _enabledActions.Clear();
            _toggleStates.Clear();

            foreach (var action in SupportedActions)
            {
                if (PlayerPrefs.GetInt(PrefPrefix + action, 0) == 1)
                    _enabledActions.Add(action);
            }
        }

        /// <summary>Reset all toggle states (called on scene transition).</summary>
        public static void ResetToggleStates()
        {
            _toggleStates.Clear();
        }
    }
}
