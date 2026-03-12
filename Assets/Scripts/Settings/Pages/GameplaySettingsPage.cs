using System.Collections.Generic;
using DIG.Settings.Core;
using DIG.Combat.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Settings.Pages
{
    /// <summary>
    /// EPIC 18.2: Gameplay settings page.
    /// Bridges to MotionIntensitySettings and HealthBarSettingsManager.
    /// </summary>
    public class GameplaySettingsPage : ISettingsPage
    {
        public string PageId => "Gameplay";
        public string DisplayName => "Gameplay";
        public int SortOrder => 3;

        private const string PrefShowDmgNumbers = "Settings_ShowDamageNumbers";
        private const string PrefDmgNumVisibility = "Settings_DmgNumVisibility";

        // Snapshot
        private float _snapCameraShake;
        private bool _snapShowDmgNumbers;
        private int _snapDmgVisibility;
        private bool _snapShowNames;
        private bool _snapShowLevels;

        // Current
        private float _currentCameraShake;
        private bool _currentShowDmgNumbers;
        private int _currentDmgVisibility;
        private bool _currentShowNames;
        private bool _currentShowLevels;

        public bool HasUnsavedChanges =>
            !Mathf.Approximately(_currentCameraShake, _snapCameraShake) ||
            _currentShowDmgNumbers != _snapShowDmgNumbers ||
            _currentDmgVisibility != _snapDmgVisibility ||
            _currentShowNames != _snapShowNames ||
            _currentShowLevels != _snapShowLevels;

        public void TakeSnapshot()
        {
            // Camera shake
            _snapCameraShake = DIG.Core.Settings.MotionIntensitySettings.HasInstance
                ? DIG.Core.Settings.MotionIntensitySettings.Instance.GlobalIntensity
                : 1f;

            _snapShowDmgNumbers = PlayerPrefs.GetInt(PrefShowDmgNumbers, 1) == 1;
            _snapDmgVisibility = PlayerPrefs.GetInt(PrefDmgNumVisibility, -1);
            if (_snapDmgVisibility < 0)
                _snapDmgVisibility = (int)DamageNumberVisibilitySettings.EffectiveVisibility;

            // Health bar settings
            if (DIG.Combat.UI.HealthBarSettingsManager.HasInstance)
            {
                _snapShowNames = DIG.Combat.UI.HealthBarSettingsManager.Instance.ShowName;
                _snapShowLevels = DIG.Combat.UI.HealthBarSettingsManager.Instance.ShowLevel;
            }
            else
            {
                _snapShowNames = true;
                _snapShowLevels = true;
            }

            _currentCameraShake = _snapCameraShake;
            _currentShowDmgNumbers = _snapShowDmgNumbers;
            _currentDmgVisibility = _snapDmgVisibility;
            _currentShowNames = _snapShowNames;
            _currentShowLevels = _snapShowLevels;
        }

        public void BuildUI(VisualElement container)
        {
            container.Add(SettingsScreenController.CreateSectionHeader("Camera"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Camera Shake Intensity", 0f, 2f, _currentCameraShake,
                val =>
                {
                    _currentCameraShake = val;
                    // Live preview
                    if (DIG.Core.Settings.MotionIntensitySettings.HasInstance)
                        DIG.Core.Settings.MotionIntensitySettings.Instance.GlobalIntensity = val;
                },
                "F1"));

            container.Add(SettingsScreenController.CreateSectionHeader("HUD"));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Show Damage Numbers", _currentShowDmgNumbers,
                val => _currentShowDmgNumbers = val));

            if (DamageNumberVisibilitySettings.IsPlayerOverrideAllowed)
            {
                container.Add(SettingsScreenController.CreateDropdownRow(
                    "Damage Number Visibility",
                    new List<string> { "All Players", "Self Only", "Nearby", "Party Only", "None" },
                    _currentDmgVisibility,
                    idx => _currentDmgVisibility = idx));
            }

            container.Add(SettingsScreenController.CreateToggleRow(
                "Show Enemy Names", _currentShowNames,
                val =>
                {
                    _currentShowNames = val;
                    if (DIG.Combat.UI.HealthBarSettingsManager.HasInstance)
                        DIG.Combat.UI.HealthBarSettingsManager.Instance.SetShowName(val);
                }));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Show Enemy Levels", _currentShowLevels,
                val =>
                {
                    _currentShowLevels = val;
                    if (DIG.Combat.UI.HealthBarSettingsManager.HasInstance)
                        DIG.Combat.UI.HealthBarSettingsManager.Instance.SetShowLevel(val);
                }));
        }

        public void OnPageShown() { }

        public void ApplyChanges()
        {
            if (DIG.Core.Settings.MotionIntensitySettings.HasInstance)
                DIG.Core.Settings.MotionIntensitySettings.Instance.GlobalIntensity = _currentCameraShake;

            PlayerPrefs.SetInt(PrefShowDmgNumbers, _currentShowDmgNumbers ? 1 : 0);
            DamageNumberVisibilitySettings.SetPlayerOverride((DamageNumberVisibility)_currentDmgVisibility);

            if (DIG.Combat.UI.HealthBarSettingsManager.HasInstance)
            {
                DIG.Combat.UI.HealthBarSettingsManager.Instance.SetShowName(_currentShowNames);
                DIG.Combat.UI.HealthBarSettingsManager.Instance.SetShowLevel(_currentShowLevels);
                DIG.Combat.UI.HealthBarSettingsManager.Instance.SaveSettings();
            }
        }

        public void RevertChanges()
        {
            _currentCameraShake = _snapCameraShake;
            _currentShowDmgNumbers = _snapShowDmgNumbers;
            _currentDmgVisibility = _snapDmgVisibility;
            _currentShowNames = _snapShowNames;
            _currentShowLevels = _snapShowLevels;

            // Restore live state
            if (DIG.Core.Settings.MotionIntensitySettings.HasInstance)
                DIG.Core.Settings.MotionIntensitySettings.Instance.GlobalIntensity = _snapCameraShake;

            if (DIG.Combat.UI.HealthBarSettingsManager.HasInstance)
            {
                DIG.Combat.UI.HealthBarSettingsManager.Instance.SetShowName(_snapShowNames);
                DIG.Combat.UI.HealthBarSettingsManager.Instance.SetShowLevel(_snapShowLevels);
            }
        }

        public void ResetToDefaults()
        {
            _currentCameraShake = 1f;
            _currentShowDmgNumbers = true;
            _currentDmgVisibility = (int)DamageNumberVisibility.All;
            _currentShowNames = true;
            _currentShowLevels = true;

            if (DIG.Core.Settings.MotionIntensitySettings.HasInstance)
                DIG.Core.Settings.MotionIntensitySettings.Instance.GlobalIntensity = 1f;
        }
    }
}
