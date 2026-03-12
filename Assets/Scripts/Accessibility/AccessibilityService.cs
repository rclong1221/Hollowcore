using System;
using System.Collections.Generic;
using DIG.Accessibility.Config;
using DIG.Accessibility.Visual;
using DIG.Accessibility.Motor;
using DIG.Accessibility.Cognitive;
using DIG.Accessibility.Audio;
using DIG.Widgets.Config;
using UnityEngine;

namespace DIG.Accessibility
{
    /// <summary>
    /// EPIC 18.12: Central accessibility coordinator singleton.
    /// Delegates to existing managers (WidgetAccessibilityManager, AudioAccessibilityConfig)
    /// and new feature modules. All settings are opt-in with zero cost when disabled.
    /// </summary>
    public class AccessibilityService : MonoBehaviour
    {
        private static AccessibilityService _instance;
        public static AccessibilityService Instance => _instance;
        public static bool HasInstance => _instance != null;

        private AccessibilityProfileSO _profile;

        // ── Events ──────────────────────────────────────────────────

        /// <summary>Fired when any accessibility setting changes. Key = setting name.</summary>
        public event Action<string> OnSettingChanged;

        // ── PlayerPrefs keys ────────────────────────────────────────

        private const string PrefColorblindIntensity = "Access_ColorblindIntensity";
        private const string PrefScreenReader = "Access_ScreenReader";
        private const string PrefSpeechRate = "Access_SpeechRate";
        private const string PrefSpeechVolume = "Access_SpeechVolume";
        private const string PrefMonoAudio = "Access_MonoAudio";
        private const string PrefSubtitleBgOpacity = "Access_SubtitleBgOpacity";
        private const string PrefAimAssist = "Access_AimAssist";
        private const string PrefSimplifiedHUD = "Access_SimplifiedHUD";
        private const string PrefEnemyHP = "Access_EnemyHP";
        private const string PrefEnemyDamage = "Access_EnemyDamage";
        private const string PrefTimingWindow = "Access_TimingWindow";
        private const string PrefResourceGain = "Access_ResourceGain";
        private const string PrefRespawnPenalty = "Access_RespawnPenalty";

        // ── Dirty tracking for ApplyAllSettings ────────────────────

        private bool _appliedOnce;
        private bool _lastScreenReaderEnabled;
        private float _lastSpeechRate = -1f;
        private float _lastSpeechVolume = -1f;
        private float _lastAimAssistStrength = -1f;
        private bool _lastMonoAudio;
        private bool _lastSimplifiedHUD;

        // ── Cached state ────────────────────────────────────────────

        private float _colorblindIntensity = 1f;
        private bool _screenReaderEnabled;
        private float _speechRate = 1f;
        private float _speechVolume = 0.8f;
        private bool _monoAudio;
        private float _subtitleBgOpacity = 0.7f;
        private float _aimAssistStrength;
        private bool _simplifiedHUD;
        private float _enemyHPMult = 1f;
        private float _enemyDamageMult = 1f;
        private float _timingWindowMult = 1f;
        private float _resourceGainMult = 1f;
        private RespawnPenalty _respawnPenalty = RespawnPenalty.Normal;

        // ── Properties ──────────────────────────────────────────────

        public float ColorblindIntensity => _colorblindIntensity;
        public bool ScreenReaderEnabled => _screenReaderEnabled;
        public float SpeechRate => _speechRate;
        public float SpeechVolume => _speechVolume;
        public bool MonoAudio => _monoAudio;
        public float SubtitleBgOpacity => _subtitleBgOpacity;
        public float AimAssistStrength => _aimAssistStrength;
        public bool SimplifiedHUD => _simplifiedHUD;
        public float EnemyHPMultiplier => _enemyHPMult;
        public float EnemyDamageMultiplier => _enemyDamageMult;
        public float TimingWindowMultiplier => _timingWindowMult;
        public float ResourceGainMultiplier => _resourceGainMult;
        public RespawnPenalty RespawnPenalty => _respawnPenalty;

        // ── Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _profile = Resources.Load<AccessibilityProfileSO>("AccessibilityProfile");
            LoadSettings();
        }

        private void Start()
        {
            ApplyAllSettings();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ── Public API ──────────────────────────────────────────────

        /// <summary>Refresh all modules from current settings. Skips unchanged values.</summary>
        public void ApplyAllSettings()
        {
            bool force = !_appliedOnce;
            _appliedOnce = true;

            // Screen reader
            if (force || _screenReaderEnabled != _lastScreenReaderEnabled)
            {
                ScreenReaderBridge.SetEnabled(_screenReaderEnabled);
                _lastScreenReaderEnabled = _screenReaderEnabled;
            }
            if (force || !Mathf.Approximately(_speechRate, _lastSpeechRate))
            {
                ScreenReaderBridge.SetRate(_speechRate);
                _lastSpeechRate = _speechRate;
            }
            if (force || !Mathf.Approximately(_speechVolume, _lastSpeechVolume))
            {
                ScreenReaderBridge.SetVolume(_speechVolume);
                _lastSpeechVolume = _speechVolume;
            }

            // Motor
            if (force || !Mathf.Approximately(_aimAssistStrength, _lastAimAssistStrength))
            {
                AimAssistService.SetStrength(_aimAssistStrength);
                _lastAimAssistStrength = _aimAssistStrength;
            }

            // Audio
            if (force || _monoAudio != _lastMonoAudio)
            {
                MonoAudioService.SetEnabled(_monoAudio);
                _lastMonoAudio = _monoAudio;
            }

            // Cognitive
            if (force || _simplifiedHUD != _lastSimplifiedHUD)
            {
                Cognitive.SimplifiedHUD.SetEnabled(_simplifiedHUD);
                _lastSimplifiedHUD = _simplifiedHUD;
            }
        }

        /// <summary>Set colorblind GPU filter intensity (0 = off, 1 = full).</summary>
        public void SetColorblindIntensity(float intensity)
        {
            _colorblindIntensity = Mathf.Clamp01(intensity);
            PlayerPrefs.SetFloat(PrefColorblindIntensity, _colorblindIntensity);
            OnSettingChanged?.Invoke("ColorblindIntensity");
        }

        /// <summary>Enable/disable screen reader TTS.</summary>
        public void SetScreenReaderEnabled(bool enabled)
        {
            _screenReaderEnabled = enabled;
            ScreenReaderBridge.SetEnabled(enabled);
            PlayerPrefs.SetInt(PrefScreenReader, enabled ? 1 : 0);
            OnSettingChanged?.Invoke("ScreenReader");
        }

        /// <summary>Set TTS speech rate.</summary>
        public void SetSpeechRate(float rate)
        {
            _speechRate = Mathf.Clamp(rate, 0.5f, 2f);
            ScreenReaderBridge.SetRate(_speechRate);
            PlayerPrefs.SetFloat(PrefSpeechRate, _speechRate);
            OnSettingChanged?.Invoke("SpeechRate");
        }

        /// <summary>Set TTS volume.</summary>
        public void SetSpeechVolume(float volume)
        {
            _speechVolume = Mathf.Clamp01(volume);
            ScreenReaderBridge.SetVolume(_speechVolume);
            PlayerPrefs.SetFloat(PrefSpeechVolume, _speechVolume);
            OnSettingChanged?.Invoke("SpeechVolume");
        }

        /// <summary>Shortcut: speak text via screen reader.</summary>
        public void Speak(string text)
        {
            ScreenReaderBridge.Speak(text, SpeechPriority.Normal);
        }

        /// <summary>Enable/disable mono audio downmix.</summary>
        public void SetMonoAudio(bool enabled)
        {
            _monoAudio = enabled;
            MonoAudioService.SetEnabled(enabled);
            PlayerPrefs.SetInt(PrefMonoAudio, enabled ? 1 : 0);
            OnSettingChanged?.Invoke("MonoAudio");
        }

        /// <summary>Set subtitle background opacity (0-1).</summary>
        public void SetSubtitleBgOpacity(float opacity)
        {
            _subtitleBgOpacity = Mathf.Clamp01(opacity);
            PlayerPrefs.SetFloat(PrefSubtitleBgOpacity, _subtitleBgOpacity);
            OnSettingChanged?.Invoke("SubtitleBgOpacity");
        }

        /// <summary>Set aim assist strength (0 = off, 1 = max).</summary>
        public void SetAimAssistStrength(float strength)
        {
            _aimAssistStrength = Mathf.Clamp01(strength);
            AimAssistService.SetStrength(_aimAssistStrength);
            PlayerPrefs.SetFloat(PrefAimAssist, _aimAssistStrength);
            OnSettingChanged?.Invoke("AimAssist");
        }

        /// <summary>Enable/disable simplified HUD.</summary>
        public void SetSimplifiedHUD(bool enabled)
        {
            _simplifiedHUD = enabled;
            Cognitive.SimplifiedHUD.SetEnabled(enabled);
            PlayerPrefs.SetInt(PrefSimplifiedHUD, enabled ? 1 : 0);
            OnSettingChanged?.Invoke("SimplifiedHUD");
        }

        /// <summary>Set enemy HP multiplier for difficulty.</summary>
        public void SetEnemyHPMultiplier(float mult)
        {
            _enemyHPMult = Mathf.Clamp(mult, 0.25f, 2f);
            PlayerPrefs.SetFloat(PrefEnemyHP, _enemyHPMult);
            OnSettingChanged?.Invoke("EnemyHP");
        }

        /// <summary>Set enemy damage multiplier for difficulty.</summary>
        public void SetEnemyDamageMultiplier(float mult)
        {
            _enemyDamageMult = Mathf.Clamp(mult, 0.25f, 2f);
            PlayerPrefs.SetFloat(PrefEnemyDamage, _enemyDamageMult);
            OnSettingChanged?.Invoke("EnemyDamage");
        }

        /// <summary>Set timing window multiplier for dodge/parry.</summary>
        public void SetTimingWindowMultiplier(float mult)
        {
            _timingWindowMult = Mathf.Clamp(mult, 0.5f, 2f);
            PlayerPrefs.SetFloat(PrefTimingWindow, _timingWindowMult);
            OnSettingChanged?.Invoke("TimingWindow");
        }

        /// <summary>Set resource gain multiplier.</summary>
        public void SetResourceGainMultiplier(float mult)
        {
            _resourceGainMult = Mathf.Clamp(mult, 0.5f, 3f);
            PlayerPrefs.SetFloat(PrefResourceGain, _resourceGainMult);
            OnSettingChanged?.Invoke("ResourceGain");
        }

        /// <summary>Set respawn penalty level.</summary>
        public void SetRespawnPenalty(RespawnPenalty penalty)
        {
            _respawnPenalty = penalty;
            PlayerPrefs.SetInt(PrefRespawnPenalty, (int)penalty);
            OnSettingChanged?.Invoke("RespawnPenalty");
        }

        /// <summary>Query whether a feature is enabled by key.</summary>
        public bool IsFeatureEnabled(string featureKey)
        {
            return featureKey switch
            {
                "ScreenReader" => _screenReaderEnabled,
                "MonoAudio" => _monoAudio,
                "SimplifiedHUD" => _simplifiedHUD,
                "HighContrast" => WidgetAccessibilityManager.HasInstance && WidgetAccessibilityManager.Instance.HighContrast,
                "ReducedMotion" => WidgetAccessibilityManager.HasInstance && WidgetAccessibilityManager.Instance.ReducedMotion,
                "SoundRadar" => false, // Delegated to AudioAccessibilityConfig
                _ => false
            };
        }

        /// <summary>Generate a CVAA compliance report.</summary>
        public AccessibilityReport GenerateReport()
        {
            return new AccessibilityReport
            {
                HasColorblindSupport = true,
                HasTextScaling = true,
                HasHighContrast = true,
                HasScreenReader = true,
                HasRemappableControls = true,
                HasOneHandedPresets = true,
                HasHoldToToggle = true,
                HasAimAssist = true,
                HasDifficultyModifiers = true,
                HasSubtitles = true,
                HasSoundRadar = true,
                HasMonoAudio = true,
                HasReducedMotion = true
            };
        }

        // ── Private ─────────────────────────────────────────────────

        private void LoadSettings()
        {
            float defIntensity = _profile != null ? _profile.ColorblindIntensity : 1f;
            bool defSR = _profile != null && _profile.ScreenReaderEnabled;
            float defRate = _profile != null ? _profile.SpeechRate : 1f;
            float defVol = _profile != null ? _profile.SpeechVolume : 0.8f;
            bool defMono = _profile != null && _profile.MonoAudio;
            float defSubBg = _profile != null ? _profile.SubtitleBackground : 0.7f;
            float defAim = _profile != null ? _profile.AimAssistStrength : 0f;
            bool defSimp = _profile != null && _profile.SimplifiedHUD;
            float defEHP = _profile != null ? _profile.EnemyHPMultiplier : 1f;
            float defEDmg = _profile != null ? _profile.EnemyDamageMultiplier : 1f;
            float defTW = _profile != null ? _profile.TimingWindowMultiplier : 1f;
            float defRG = _profile != null ? _profile.ResourceGainMultiplier : 1f;
            int defRP = _profile != null ? (int)_profile.RespawnPenalty : (int)RespawnPenalty.Normal;

            _colorblindIntensity = PlayerPrefs.GetFloat(PrefColorblindIntensity, defIntensity);
            _screenReaderEnabled = PlayerPrefs.GetInt(PrefScreenReader, defSR ? 1 : 0) == 1;
            _speechRate = PlayerPrefs.GetFloat(PrefSpeechRate, defRate);
            _speechVolume = PlayerPrefs.GetFloat(PrefSpeechVolume, defVol);
            _monoAudio = PlayerPrefs.GetInt(PrefMonoAudio, defMono ? 1 : 0) == 1;
            _subtitleBgOpacity = PlayerPrefs.GetFloat(PrefSubtitleBgOpacity, defSubBg);
            _aimAssistStrength = PlayerPrefs.GetFloat(PrefAimAssist, defAim);
            _simplifiedHUD = PlayerPrefs.GetInt(PrefSimplifiedHUD, defSimp ? 1 : 0) == 1;
            _enemyHPMult = PlayerPrefs.GetFloat(PrefEnemyHP, defEHP);
            _enemyDamageMult = PlayerPrefs.GetFloat(PrefEnemyDamage, defEDmg);
            _timingWindowMult = PlayerPrefs.GetFloat(PrefTimingWindow, defTW);
            _resourceGainMult = PlayerPrefs.GetFloat(PrefResourceGain, defRG);
            _respawnPenalty = (RespawnPenalty)PlayerPrefs.GetInt(PrefRespawnPenalty, defRP);
        }
    }

    /// <summary>CVAA/WCAG compliance report.</summary>
    public struct AccessibilityReport
    {
        public bool HasColorblindSupport;
        public bool HasTextScaling;
        public bool HasHighContrast;
        public bool HasScreenReader;
        public bool HasRemappableControls;
        public bool HasOneHandedPresets;
        public bool HasHoldToToggle;
        public bool HasAimAssist;
        public bool HasDifficultyModifiers;
        public bool HasSubtitles;
        public bool HasSoundRadar;
        public bool HasMonoAudio;
        public bool HasReducedMotion;
    }
}
