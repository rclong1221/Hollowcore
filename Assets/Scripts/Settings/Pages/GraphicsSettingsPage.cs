using System.Collections.Generic;
using System.Linq;
using DIG.Settings.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Settings.Pages
{
    /// <summary>
    /// EPIC 18.2: Graphics settings page.
    /// Bridges to Screen API, QualitySettings, and existing GraphicsSettingsPanel PlayerPrefs.
    /// </summary>
    public class GraphicsSettingsPage : ISettingsPage
    {
        public string PageId => "Graphics";
        public string DisplayName => "Graphics";
        public int SortOrder => 0;

        private const string PrefWindowMode = "WindowMode";
        private const string PrefFOV = "Settings_FOV";

        // Snapshot values
        private int _snapResolutionIndex;
        private int _snapWindowMode;
        private int _snapQualityLevel;
        private int _snapVsync;
        private float _snapFOV;

        // Current (live-preview) values
        private int _currentResolutionIndex;
        private int _currentWindowMode;
        private int _currentQualityLevel;
        private int _currentVsync;
        private float _currentFOV;

        // Cached resolution list
        private List<string> _resolutionLabels;
        private Resolution[] _resolutions;

        // Cached quality names (avoids alloc per tab switch)
        private List<string> _qualityNames;

        // Cached Camera.main reference
        private static Camera _cachedMainCamera;

        // Resolution confirmation
        private VisualElement _confirmOverlay;
        private IVisualElementScheduledItem _confirmTimer;
        private int _confirmCountdown;
        private int _preConfirmResIndex;
        private int _preConfirmWindowMode;

        public bool HasUnsavedChanges =>
            _currentResolutionIndex != _snapResolutionIndex ||
            _currentWindowMode != _snapWindowMode ||
            _currentQualityLevel != _snapQualityLevel ||
            _currentVsync != _snapVsync ||
            !Mathf.Approximately(_currentFOV, _snapFOV);

        public void TakeSnapshot()
        {
            CacheResolutions();
            _snapWindowMode = PlayerPrefs.GetInt(PrefWindowMode, Screen.fullScreen ? 0 : 1);
            _snapResolutionIndex = GetCurrentResolutionIndex();
            _snapQualityLevel = QualitySettings.GetQualityLevel();
            _snapVsync = QualitySettings.vSyncCount;
            _snapFOV = PlayerPrefs.GetFloat(PrefFOV, 90f);

            _currentWindowMode = _snapWindowMode;
            _currentResolutionIndex = _snapResolutionIndex;
            _currentQualityLevel = _snapQualityLevel;
            _currentVsync = _snapVsync;
            _currentFOV = _snapFOV;
        }

        public void BuildUI(VisualElement container)
        {
            CacheResolutions();

            container.Add(SettingsScreenController.CreateSectionHeader("Display"));

            // Resolution
            container.Add(SettingsScreenController.CreateDropdownRow(
                "Resolution", _resolutionLabels, _currentResolutionIndex,
                idx =>
                {
                    _currentResolutionIndex = idx;
                    PreviewResolution();
                }));

            // Window Mode
            container.Add(SettingsScreenController.CreateDropdownRow(
                "Window Mode", new List<string> { "Fullscreen", "Windowed" }, _currentWindowMode,
                idx =>
                {
                    _currentWindowMode = idx;
                    PreviewWindowMode();
                }));

            // VSync
            container.Add(SettingsScreenController.CreateToggleRow(
                "VSync", _currentVsync > 0,
                val => _currentVsync = val ? 1 : 0));

            container.Add(SettingsScreenController.CreateSectionHeader("Quality"));

            // Quality Preset
            if (_qualityNames == null)
                _qualityNames = QualitySettings.names.ToList();
            container.Add(SettingsScreenController.CreateDropdownRow(
                "Quality Preset", _qualityNames,
                Mathf.Clamp(_currentQualityLevel, 0, _qualityNames.Count - 1),
                idx => _currentQualityLevel = idx));

            container.Add(SettingsScreenController.CreateSectionHeader("Camera"));

            // FOV
            container.Add(SettingsScreenController.CreateSliderRow(
                "Field of View", 60f, 120f, _currentFOV,
                val => _currentFOV = val));
        }

        public void OnPageShown() { }

        public void ApplyChanges()
        {
            // Resolution + window mode
            if (_currentResolutionIndex >= 0 && _currentResolutionIndex < _resolutions.Length)
            {
                var res = _resolutions[_currentResolutionIndex];
                var mode = _currentWindowMode == 0 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
                Screen.SetResolution(res.width, res.height, mode, res.refreshRateRatio);
            }

            PlayerPrefs.SetInt(PrefWindowMode, _currentWindowMode);

            // Quality
            QualitySettings.SetQualityLevel(_currentQualityLevel, true);

            // VSync
            QualitySettings.vSyncCount = _currentVsync;

            // FOV
            PlayerPrefs.SetFloat(PrefFOV, _currentFOV);
            ApplyFOV(_currentFOV);

            PlayerPrefs.Save();
        }

        public void RevertChanges()
        {
            _currentWindowMode = _snapWindowMode;
            _currentResolutionIndex = _snapResolutionIndex;
            _currentQualityLevel = _snapQualityLevel;
            _currentVsync = _snapVsync;
            _currentFOV = _snapFOV;

            // Restore live state
            if (_snapResolutionIndex >= 0 && _snapResolutionIndex < _resolutions.Length)
            {
                var res = _resolutions[_snapResolutionIndex];
                var mode = _snapWindowMode == 0 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
                Screen.SetResolution(res.width, res.height, mode, res.refreshRateRatio);
            }

            QualitySettings.SetQualityLevel(_snapQualityLevel, true);
            QualitySettings.vSyncCount = _snapVsync;
            ApplyFOV(_snapFOV);
        }

        public void ResetToDefaults()
        {
            // Native resolution, fullscreen
            _currentResolutionIndex = _resolutions.Length - 1; // Highest available
            _currentWindowMode = 0;
            _currentQualityLevel = Mathf.Clamp(2, 0, QualitySettings.names.Length - 1); // "Medium" or equivalent
            _currentVsync = 1;
            _currentFOV = 90f;
        }

        // === Private Helpers ===

        private void CacheResolutions()
        {
            if (_resolutions != null) return;

            // Deduplicate resolutions (same width+height, keep highest refresh rate)
            var unique = new Dictionary<(int w, int h), Resolution>();
            foreach (var res in Screen.resolutions)
            {
                var key = (res.width, res.height);
                if (!unique.ContainsKey(key))
                    unique[key] = res;
                else if (res.refreshRateRatio.value > unique[key].refreshRateRatio.value)
                    unique[key] = res;
            }

            _resolutions = unique.Values.OrderBy(r => r.width).ThenBy(r => r.height).ToArray();
            _resolutionLabels = _resolutions
                .Select(r => $"{r.width} x {r.height}")
                .ToList();
        }

        private int GetCurrentResolutionIndex()
        {
            for (int i = 0; i < _resolutions.Length; i++)
            {
                if (_resolutions[i].width == Screen.width && _resolutions[i].height == Screen.height)
                    return i;
            }
            return _resolutions.Length - 1;
        }

        private void PreviewResolution()
        {
            if (_currentResolutionIndex < 0 || _currentResolutionIndex >= _resolutions.Length) return;
            var res = _resolutions[_currentResolutionIndex];
            var mode = _currentWindowMode == 0 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
            Screen.SetResolution(res.width, res.height, mode, res.refreshRateRatio);
        }

        private void PreviewWindowMode()
        {
            Screen.fullScreen = _currentWindowMode == 0;
        }

        private static void ApplyFOV(float fov)
        {
            if (_cachedMainCamera == null)
                _cachedMainCamera = Camera.main;
            if (_cachedMainCamera != null)
                _cachedMainCamera.fieldOfView = fov;
        }
    }
}
