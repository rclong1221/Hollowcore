using System;
using System.Collections.Generic;
using DIG.Accessibility;
using DIG.Accessibility.Config;
using DIG.Accessibility.Motor;
using DIG.Settings.Core;
using DIG.Widgets.Config;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Settings.Pages
{
    /// <summary>
    /// EPIC 18.2 + 18.12: Accessibility settings page.
    /// Bridges to WidgetAccessibilityManager, AudioAccessibilityConfig, and AccessibilityService.
    /// All changes preview live; Apply persists to PlayerPrefs.
    /// </summary>
    public class AccessibilitySettingsPage : ISettingsPage
    {
        public string PageId => "Accessibility";
        public string DisplayName => "Accessibility";
        public int SortOrder => 4;

        // ── Snapshot — widget ───────────────────────────────────────
        private float _snapFontScale;
        private ColorblindMode _snapColorblind;
        private bool _snapReducedMotion;
        private bool _snapHighContrast;
        private float _snapWidgetSize;

        // ── Snapshot — audio accessibility ──────────────────────────
        private bool _snapSoundRadar;
        private bool _snapDirSubtitles;
        private float _snapSubtitleScale;

        // ── Snapshot — EPIC 18.12 additions ─────────────────────────
        private float _snapColorblindIntensity;
        private bool _snapScreenReader;
        private float _snapSpeechRate;
        private bool _snapMonoAudio;
        private float _snapSubtitleBgOpacity;
        private float _snapAimAssist;
        private bool _snapSimplifiedHUD;
        private float _snapEnemyHP;
        private float _snapEnemyDamage;
        private float _snapTimingWindow;
        private float _snapResourceGain;
        private RespawnPenalty _snapRespawnPenalty;
        private bool _snapToggleSprint;
        private bool _snapToggleAim;
        private bool _snapToggleCrouch;
        private float _snapDoubleTap;
        private float _snapHoldThreshold;
        private int _snapInputBuffer;

        // ── Current — widget ────────────────────────────────────────
        private float _currentFontScale;
        private ColorblindMode _currentColorblind;
        private bool _currentReducedMotion;
        private bool _currentHighContrast;
        private float _currentWidgetSize;

        // ── Current — audio accessibility ───────────────────────────
        private bool _currentSoundRadar;
        private bool _currentDirSubtitles;
        private float _currentSubtitleScale;

        // ── Current — EPIC 18.12 additions ──────────────────────────
        private float _currentColorblindIntensity;
        private bool _currentScreenReader;
        private float _currentSpeechRate;
        private bool _currentMonoAudio;
        private float _currentSubtitleBgOpacity;
        private float _currentAimAssist;
        private bool _currentSimplifiedHUD;
        private float _currentEnemyHP;
        private float _currentEnemyDamage;
        private float _currentTimingWindow;
        private float _currentResourceGain;
        private RespawnPenalty _currentRespawnPenalty;
        private bool _currentToggleSprint;
        private bool _currentToggleAim;
        private bool _currentToggleCrouch;
        private float _currentDoubleTap;
        private float _currentHoldThreshold;
        private int _currentInputBuffer;

        // Cached audio config
        private Audio.Accessibility.AudioAccessibilityConfig _audioConfig;

        public bool HasUnsavedChanges =>
            !Mathf.Approximately(_currentFontScale, _snapFontScale) ||
            _currentColorblind != _snapColorblind ||
            _currentReducedMotion != _snapReducedMotion ||
            _currentHighContrast != _snapHighContrast ||
            !Mathf.Approximately(_currentWidgetSize, _snapWidgetSize) ||
            _currentSoundRadar != _snapSoundRadar ||
            _currentDirSubtitles != _snapDirSubtitles ||
            !Mathf.Approximately(_currentSubtitleScale, _snapSubtitleScale) ||
            // 18.12 additions
            !Mathf.Approximately(_currentColorblindIntensity, _snapColorblindIntensity) ||
            _currentScreenReader != _snapScreenReader ||
            !Mathf.Approximately(_currentSpeechRate, _snapSpeechRate) ||
            _currentMonoAudio != _snapMonoAudio ||
            !Mathf.Approximately(_currentSubtitleBgOpacity, _snapSubtitleBgOpacity) ||
            !Mathf.Approximately(_currentAimAssist, _snapAimAssist) ||
            _currentSimplifiedHUD != _snapSimplifiedHUD ||
            !Mathf.Approximately(_currentEnemyHP, _snapEnemyHP) ||
            !Mathf.Approximately(_currentEnemyDamage, _snapEnemyDamage) ||
            !Mathf.Approximately(_currentTimingWindow, _snapTimingWindow) ||
            !Mathf.Approximately(_currentResourceGain, _snapResourceGain) ||
            _currentRespawnPenalty != _snapRespawnPenalty ||
            _currentToggleSprint != _snapToggleSprint ||
            _currentToggleAim != _snapToggleAim ||
            _currentToggleCrouch != _snapToggleCrouch ||
            !Mathf.Approximately(_currentDoubleTap, _snapDoubleTap) ||
            !Mathf.Approximately(_currentHoldThreshold, _snapHoldThreshold) ||
            _currentInputBuffer != _snapInputBuffer;

        public void TakeSnapshot()
        {
            // Widget accessibility
            if (WidgetAccessibilityManager.HasInstance)
            {
                var wam = WidgetAccessibilityManager.Instance;
                _snapFontScale = wam.FontScale;
                _snapColorblind = wam.Colorblind;
                _snapReducedMotion = wam.ReducedMotion;
                _snapHighContrast = wam.HighContrast;
                _snapWidgetSize = wam.WidgetSizeScale;
            }
            else
            {
                _snapFontScale = 1f;
                _snapColorblind = ColorblindMode.None;
                _snapReducedMotion = false;
                _snapHighContrast = false;
                _snapWidgetSize = 1f;
            }

            // Audio accessibility
            if (_audioConfig == null)
                _audioConfig = Resources.Load<Audio.Accessibility.AudioAccessibilityConfig>("AudioAccessibilityConfig");
            if (_audioConfig != null)
            {
                _audioConfig.LoadFromPrefs();
                _snapSoundRadar = _audioConfig.EnableSoundRadar;
                _snapDirSubtitles = _audioConfig.EnableDirectionalSubtitles;
                _snapSubtitleScale = _audioConfig.SubtitleFontScale;
            }
            else
            {
                _snapSoundRadar = false;
                _snapDirSubtitles = false;
                _snapSubtitleScale = 1f;
            }

            // EPIC 18.12 — AccessibilityService
            if (AccessibilityService.HasInstance)
            {
                var svc = AccessibilityService.Instance;
                _snapColorblindIntensity = svc.ColorblindIntensity;
                _snapScreenReader = svc.ScreenReaderEnabled;
                _snapSpeechRate = svc.SpeechRate;
                _snapMonoAudio = svc.MonoAudio;
                _snapSubtitleBgOpacity = svc.SubtitleBgOpacity;
                _snapAimAssist = svc.AimAssistStrength;
                _snapSimplifiedHUD = svc.SimplifiedHUD;
                _snapEnemyHP = svc.EnemyHPMultiplier;
                _snapEnemyDamage = svc.EnemyDamageMultiplier;
                _snapTimingWindow = svc.TimingWindowMultiplier;
                _snapResourceGain = svc.ResourceGainMultiplier;
                _snapRespawnPenalty = svc.RespawnPenalty;
            }
            else
            {
                _snapColorblindIntensity = 1f;
                _snapScreenReader = false;
                _snapSpeechRate = 1f;
                _snapMonoAudio = false;
                _snapSubtitleBgOpacity = 0.7f;
                _snapAimAssist = 0f;
                _snapSimplifiedHUD = false;
                _snapEnemyHP = 1f;
                _snapEnemyDamage = 1f;
                _snapTimingWindow = 1f;
                _snapResourceGain = 1f;
                _snapRespawnPenalty = RespawnPenalty.Normal;
            }

            // Motor — hold-to-toggle
            _snapToggleSprint = HoldToToggleService.IsToggleEnabled("Sprint");
            _snapToggleAim = HoldToToggleService.IsToggleEnabled("Aim");
            _snapToggleCrouch = HoldToToggleService.IsToggleEnabled("Crouch");

            // Motor — input timing
            _snapDoubleTap = InputTimingService.DoubleTapWindow;
            _snapHoldThreshold = InputTimingService.HoldThreshold;
            _snapInputBuffer = InputTimingService.InputBufferMs;

            // Copy snap → current
            CopySnapToCurrent();
        }

        private void CopySnapToCurrent()
        {
            _currentFontScale = _snapFontScale;
            _currentColorblind = _snapColorblind;
            _currentReducedMotion = _snapReducedMotion;
            _currentHighContrast = _snapHighContrast;
            _currentWidgetSize = _snapWidgetSize;
            _currentSoundRadar = _snapSoundRadar;
            _currentDirSubtitles = _snapDirSubtitles;
            _currentSubtitleScale = _snapSubtitleScale;
            _currentColorblindIntensity = _snapColorblindIntensity;
            _currentScreenReader = _snapScreenReader;
            _currentSpeechRate = _snapSpeechRate;
            _currentMonoAudio = _snapMonoAudio;
            _currentSubtitleBgOpacity = _snapSubtitleBgOpacity;
            _currentAimAssist = _snapAimAssist;
            _currentSimplifiedHUD = _snapSimplifiedHUD;
            _currentEnemyHP = _snapEnemyHP;
            _currentEnemyDamage = _snapEnemyDamage;
            _currentTimingWindow = _snapTimingWindow;
            _currentResourceGain = _snapResourceGain;
            _currentRespawnPenalty = _snapRespawnPenalty;
            _currentToggleSprint = _snapToggleSprint;
            _currentToggleAim = _snapToggleAim;
            _currentToggleCrouch = _snapToggleCrouch;
            _currentDoubleTap = _snapDoubleTap;
            _currentHoldThreshold = _snapHoldThreshold;
            _currentInputBuffer = _snapInputBuffer;
        }

        public void BuildUI(VisualElement container)
        {
            // ── Visual ──────────────────────────────────────────────
            container.Add(SettingsScreenController.CreateSectionHeader("Visual"));

            var cbNames = new List<string>(Enum.GetNames(typeof(ColorblindMode)));
            container.Add(SettingsScreenController.CreateDropdownRow(
                "Colorblind Mode", cbNames, (int)_currentColorblind,
                idx =>
                {
                    _currentColorblind = (ColorblindMode)idx;
                    if (WidgetAccessibilityManager.HasInstance)
                        WidgetAccessibilityManager.Instance.SetColorblindMode(_currentColorblind);
                }));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Colorblind Intensity", 0f, 1f, _currentColorblindIntensity,
                val =>
                {
                    _currentColorblindIntensity = val;
                    if (AccessibilityService.HasInstance)
                        AccessibilityService.Instance.SetColorblindIntensity(val);
                }, "P0"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Font Scale", 0.75f, 2f, _currentFontScale,
                val =>
                {
                    _currentFontScale = val;
                    if (WidgetAccessibilityManager.HasInstance)
                        WidgetAccessibilityManager.Instance.SetFontScale(val);
                }, "F2"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Widget Size", 0.75f, 2f, _currentWidgetSize,
                val =>
                {
                    _currentWidgetSize = val;
                    if (WidgetAccessibilityManager.HasInstance)
                        WidgetAccessibilityManager.Instance.SetWidgetSizeScale(val);
                }, "F2"));

            container.Add(SettingsScreenController.CreateToggleRow(
                "High Contrast", _currentHighContrast,
                val =>
                {
                    _currentHighContrast = val;
                    if (WidgetAccessibilityManager.HasInstance)
                        WidgetAccessibilityManager.Instance.SetHighContrast(val);
                }));

            // ── Motion ──────────────────────────────────────────────
            container.Add(SettingsScreenController.CreateSectionHeader("Motion"));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Reduced Motion", _currentReducedMotion,
                val =>
                {
                    _currentReducedMotion = val;
                    if (WidgetAccessibilityManager.HasInstance)
                        WidgetAccessibilityManager.Instance.SetReducedMotion(val);
                }));

            // ── Screen Reader ───────────────────────────────────────
            container.Add(SettingsScreenController.CreateSectionHeader("Screen Reader"));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Enable Screen Reader", _currentScreenReader,
                val => _currentScreenReader = val));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Speech Rate", 0.5f, 2f, _currentSpeechRate,
                val => _currentSpeechRate = val, "F1"));

            // ── Motor Accessibility ─────────────────────────────────
            container.Add(SettingsScreenController.CreateSectionHeader("Motor Accessibility"));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Toggle Sprint (instead of hold)", _currentToggleSprint,
                val => _currentToggleSprint = val));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Toggle Aim (instead of hold)", _currentToggleAim,
                val => _currentToggleAim = val));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Toggle Crouch (instead of hold)", _currentToggleCrouch,
                val => _currentToggleCrouch = val));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Double-Tap Window", 0.1f, 1f, _currentDoubleTap,
                val => _currentDoubleTap = val, "F2"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Hold Threshold", 0.1f, 1f, _currentHoldThreshold,
                val => _currentHoldThreshold = val, "F2"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Input Buffer (ms)", 0f, 500f, _currentInputBuffer,
                val => _currentInputBuffer = Mathf.RoundToInt(val), "F0"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Aim Assist Strength", 0f, 1f, _currentAimAssist,
                val => _currentAimAssist = val, "P0"));

            var presetNames = new List<string>(OneHandedPresets.PresetNames);
            container.Add(SettingsScreenController.CreateDropdownRow(
                "One-Handed Preset", presetNames, 0,
                idx => OneHandedPresets.ApplyPreset(idx)));

            // ── Audio Accessibility ─────────────────────────────────
            container.Add(SettingsScreenController.CreateSectionHeader("Audio Accessibility"));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Sound Radar", _currentSoundRadar,
                val => _currentSoundRadar = val));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Directional Subtitles", _currentDirSubtitles,
                val => _currentDirSubtitles = val));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Subtitle Font Scale", 1f, 2f, _currentSubtitleScale,
                val => _currentSubtitleScale = val, "F1"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Subtitle Background Opacity", 0f, 1f, _currentSubtitleBgOpacity,
                val => _currentSubtitleBgOpacity = val, "P0"));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Mono Audio", _currentMonoAudio,
                val => _currentMonoAudio = val));

            // ── Cognitive Accessibility ──────────────────────────────
            container.Add(SettingsScreenController.CreateSectionHeader("Difficulty & Cognitive"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Enemy HP", 0.25f, 2f, _currentEnemyHP,
                val => _currentEnemyHP = val, "P0"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Enemy Damage", 0.25f, 2f, _currentEnemyDamage,
                val => _currentEnemyDamage = val, "P0"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Timing Window", 0.5f, 2f, _currentTimingWindow,
                val => _currentTimingWindow = val, "P0"));

            container.Add(SettingsScreenController.CreateSliderRow(
                "Resource Gain", 0.5f, 3f, _currentResourceGain,
                val => _currentResourceGain = val, "P0"));

            var penaltyNames = new List<string>(Enum.GetNames(typeof(RespawnPenalty)));
            container.Add(SettingsScreenController.CreateDropdownRow(
                "Respawn Penalty", penaltyNames, (int)_currentRespawnPenalty,
                idx => _currentRespawnPenalty = (RespawnPenalty)idx));

            container.Add(SettingsScreenController.CreateToggleRow(
                "Simplified HUD", _currentSimplifiedHUD,
                val => _currentSimplifiedHUD = val));
        }

        public void OnPageShown() { }

        public void ApplyChanges()
        {
            // Widget accessibility
            if (WidgetAccessibilityManager.HasInstance)
            {
                var wam = WidgetAccessibilityManager.Instance;
                wam.SetFontScale(_currentFontScale);
                wam.SetColorblindMode(_currentColorblind);
                wam.SetReducedMotion(_currentReducedMotion);
                wam.SetHighContrast(_currentHighContrast);
                wam.SetWidgetSizeScale(_currentWidgetSize);
            }

            // Audio accessibility
            if (_audioConfig != null)
            {
                _audioConfig.EnableSoundRadar = _currentSoundRadar;
                _audioConfig.EnableDirectionalSubtitles = _currentDirSubtitles;
                _audioConfig.SubtitleFontScale = _currentSubtitleScale;
                _audioConfig.SaveToPrefs();
            }

            // EPIC 18.12 — AccessibilityService
            if (AccessibilityService.HasInstance)
            {
                var svc = AccessibilityService.Instance;
                svc.SetColorblindIntensity(_currentColorblindIntensity);
                svc.SetScreenReaderEnabled(_currentScreenReader);
                svc.SetSpeechRate(_currentSpeechRate);
                svc.SetMonoAudio(_currentMonoAudio);
                svc.SetSubtitleBgOpacity(_currentSubtitleBgOpacity);
                svc.SetAimAssistStrength(_currentAimAssist);
                svc.SetSimplifiedHUD(_currentSimplifiedHUD);
                svc.SetEnemyHPMultiplier(_currentEnemyHP);
                svc.SetEnemyDamageMultiplier(_currentEnemyDamage);
                svc.SetTimingWindowMultiplier(_currentTimingWindow);
                svc.SetResourceGainMultiplier(_currentResourceGain);
                svc.SetRespawnPenalty(_currentRespawnPenalty);
            }

            // Motor — hold-to-toggle
            HoldToToggleService.SetToggleEnabled("Sprint", _currentToggleSprint);
            HoldToToggleService.SetToggleEnabled("Aim", _currentToggleAim);
            HoldToToggleService.SetToggleEnabled("Crouch", _currentToggleCrouch);

            // Motor — input timing
            InputTimingService.SetDoubleTapWindow(_currentDoubleTap);
            InputTimingService.SetHoldThreshold(_currentHoldThreshold);
            InputTimingService.SetInputBufferMs(_currentInputBuffer);
        }

        public void RevertChanges()
        {
            CopySnapToCurrent();

            // Restore live widget state
            if (WidgetAccessibilityManager.HasInstance)
            {
                var wam = WidgetAccessibilityManager.Instance;
                wam.SetFontScale(_snapFontScale);
                wam.SetColorblindMode(_snapColorblind);
                wam.SetReducedMotion(_snapReducedMotion);
                wam.SetHighContrast(_snapHighContrast);
                wam.SetWidgetSizeScale(_snapWidgetSize);
            }

            // Restore audio config
            if (_audioConfig != null)
            {
                _audioConfig.EnableSoundRadar = _snapSoundRadar;
                _audioConfig.EnableDirectionalSubtitles = _snapDirSubtitles;
                _audioConfig.SubtitleFontScale = _snapSubtitleScale;
            }

            // Restore accessibility service state
            if (AccessibilityService.HasInstance)
            {
                var svc = AccessibilityService.Instance;
                svc.SetColorblindIntensity(_snapColorblindIntensity);
            }
        }

        public void ResetToDefaults()
        {
            _currentFontScale = 1f;
            _currentColorblind = ColorblindMode.None;
            _currentReducedMotion = false;
            _currentHighContrast = false;
            _currentWidgetSize = 1f;
            _currentSoundRadar = false;
            _currentDirSubtitles = false;
            _currentSubtitleScale = 1f;
            _currentColorblindIntensity = 1f;
            _currentScreenReader = false;
            _currentSpeechRate = 1f;
            _currentMonoAudio = false;
            _currentSubtitleBgOpacity = 0.7f;
            _currentAimAssist = 0f;
            _currentSimplifiedHUD = false;
            _currentEnemyHP = 1f;
            _currentEnemyDamage = 1f;
            _currentTimingWindow = 1f;
            _currentResourceGain = 1f;
            _currentRespawnPenalty = RespawnPenalty.Normal;
            _currentToggleSprint = false;
            _currentToggleAim = false;
            _currentToggleCrouch = false;
            _currentDoubleTap = 0.3f;
            _currentHoldThreshold = 0.4f;
            _currentInputBuffer = 100;

            if (WidgetAccessibilityManager.HasInstance)
                WidgetAccessibilityManager.Instance.ResetToDefaults();
        }
    }
}
