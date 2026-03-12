using DIG.Settings.Core;
using DIG.Targeting;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Settings.Pages
{
    /// <summary>
    /// EPIC 18.2: Controls settings page.
    /// Bridges to TargetLockSettingsManager for aim assist / target lock.
    /// Mouse sensitivity and invert-Y are simple PlayerPrefs values.
    /// Keybinds link to the existing UGUI KeybindPanel.
    /// </summary>
    public class ControlsSettingsPage : ISettingsPage
    {
        public string PageId => "Controls";
        public string DisplayName => "Controls";
        public int SortOrder => 2;

        private const string PrefMouseSens = "Settings_MouseSensitivity";
        private const string PrefInvertY = "Settings_InvertY";

        // Snapshot
        private float _snapMouseSens;
        private bool _snapInvertY;
        private bool _snapAimAssist;
        private bool _snapTargetLock;
        private bool _snapShowIndicator;

        // Current
        private float _currentMouseSens;
        private bool _currentInvertY;
        private bool _currentAimAssist;
        private bool _currentTargetLock;
        private bool _currentShowIndicator;

        public bool HasUnsavedChanges =>
            !Mathf.Approximately(_currentMouseSens, _snapMouseSens) ||
            _currentInvertY != _snapInvertY ||
            _currentAimAssist != _snapAimAssist ||
            _currentTargetLock != _snapTargetLock ||
            _currentShowIndicator != _snapShowIndicator;

        public void TakeSnapshot()
        {
            _snapMouseSens = PlayerPrefs.GetFloat(PrefMouseSens, 5f);
            _snapInvertY = PlayerPrefs.GetInt(PrefInvertY, 0) == 1;

            var targeting = TargetLockSettingsManager.Instance;
            _snapAimAssist = targeting.AllowAimAssist;
            _snapTargetLock = targeting.AllowTargetLock;
            _snapShowIndicator = targeting.ShowIndicator;

            _currentMouseSens = _snapMouseSens;
            _currentInvertY = _snapInvertY;
            _currentAimAssist = _snapAimAssist;
            _currentTargetLock = _snapTargetLock;
            _currentShowIndicator = _snapShowIndicator;
        }

        public void BuildUI(VisualElement container)
        {
            container.Add(SettingsScreenController.CreateSectionHeader("Mouse & Camera"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Mouse Sensitivity", 0.1f, 20f, _currentMouseSens,
                val => _currentMouseSens = val, "F1"));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Invert Y-Axis", _currentInvertY,
                val => _currentInvertY = val));

            container.Add(SettingsScreenController.CreateSectionHeader("Targeting"));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Aim Assist", _currentAimAssist,
                val => _currentAimAssist = val));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Target Lock", _currentTargetLock,
                val => _currentTargetLock = val));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Lock-On Indicator", _currentShowIndicator,
                val => _currentShowIndicator = val));

            container.Add(SettingsScreenController.CreateSectionHeader("Keybinds"));

            var keybindBtn = new Button(OpenKeybindPanel) { text = "Customize Keybinds..." };
            keybindBtn.AddToClassList("pro-button");
            keybindBtn.AddToClassList("settings-keybind-btn");
            container.Add(keybindBtn);
        }

        public void OnPageShown() { }

        public void ApplyChanges()
        {
            PlayerPrefs.SetFloat(PrefMouseSens, _currentMouseSens);
            PlayerPrefs.SetInt(PrefInvertY, _currentInvertY ? 1 : 0);
            PlayerPrefs.Save();

            var targeting = TargetLockSettingsManager.Instance;
            targeting.AllowAimAssist = _currentAimAssist;
            targeting.AllowTargetLock = _currentTargetLock;
            targeting.ShowIndicator = _currentShowIndicator;
            targeting.Save();
        }

        public void RevertChanges()
        {
            _currentMouseSens = _snapMouseSens;
            _currentInvertY = _snapInvertY;
            _currentAimAssist = _snapAimAssist;
            _currentTargetLock = _snapTargetLock;
            _currentShowIndicator = _snapShowIndicator;

            // Revert targeting manager live state
            var targeting = TargetLockSettingsManager.Instance;
            targeting.AllowAimAssist = _snapAimAssist;
            targeting.AllowTargetLock = _snapTargetLock;
            targeting.ShowIndicator = _snapShowIndicator;
        }

        public void ResetToDefaults()
        {
            _currentMouseSens = 5f;
            _currentInvertY = false;
            _currentAimAssist = true;
            _currentTargetLock = true;
            _currentShowIndicator = true;
        }

        private static void OpenKeybindPanel()
        {
            // Find and activate the existing UGUI KeybindPanel
            // FindAnyObjectByType doesn't search inactive objects; use FindObjectOfType with includeInactive
            var panel = Object.FindFirstObjectByType<DIG.Core.Input.Keybinds.UI.KeybindPanel>(FindObjectsInactive.Include);
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[ControlsSettingsPage] KeybindPanel not found in scene.");
            }
        }
    }
}
