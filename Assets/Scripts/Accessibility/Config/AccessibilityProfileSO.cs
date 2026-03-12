using DIG.Widgets.Config;
using UnityEngine;

namespace DIG.Accessibility.Config
{
    /// <summary>
    /// EPIC 18.12: Master accessibility profile storing all defaults.
    /// Place in Assets/Resources/AccessibilityProfile.asset.
    /// Runtime values are persisted via PlayerPrefs; this SO provides factory defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Accessibility/Profile", fileName = "AccessibilityProfile")]
    public class AccessibilityProfileSO : ScriptableObject
    {
        [Header("Visual — Colorblind")]
        public ColorblindMode ColorblindMode = ColorblindMode.None;

        [Range(0f, 1f)]
        [Tooltip("GPU colorblind correction intensity (0 = off, 1 = full correction).")]
        public float ColorblindIntensity = 1f;

        [Header("Visual — Text & UI")]
        [Range(0.8f, 2f)]
        [Tooltip("Global text scale multiplier.")]
        public float TextScale = 1f;

        [Tooltip("Enable high-contrast UI theme.")]
        public bool HighContrast;

        [Header("Visual — Screen Reader")]
        [Tooltip("Enable platform TTS for UI elements.")]
        public bool ScreenReaderEnabled;

        [Range(0.5f, 2f)]
        [Tooltip("TTS speech rate multiplier.")]
        public float SpeechRate = 1f;

        [Range(0f, 1f)]
        [Tooltip("TTS volume.")]
        public float SpeechVolume = 0.8f;

        [Header("Motor — Hold-to-Toggle")]
        [Tooltip("Action names using toggle instead of hold (e.g., Sprint, Aim, Crouch, Block).")]
        public string[] HoldToToggleActions = System.Array.Empty<string>();

        [Header("Motor — Input Timing")]
        [Range(0.1f, 1f)]
        public float DoubleTapWindow = 0.3f;

        [Range(0.1f, 1f)]
        public float HoldThreshold = 0.4f;

        [Range(0, 500)]
        public int InputBufferMs = 100;

        [Header("Motor — Aim Assist")]
        [Range(0f, 1f)]
        [Tooltip("0 = off, 1 = full lock-on.")]
        public float AimAssistStrength;

        [Header("Audio")]
        [Tooltip("Downmix stereo to mono.")]
        public bool MonoAudio;

        [Header("Audio — Subtitles")]
        public SubtitleSize SubtitleSize = SubtitleSize.Medium;

        [Range(0f, 1f)]
        [Tooltip("Subtitle background opacity.")]
        public float SubtitleBackground = 0.7f;

        [Header("Motion")]
        [Tooltip("Reduce screen shake and animations.")]
        public bool ReducedMotion;

        [Header("Cognitive — Difficulty")]
        [Range(0.25f, 2f)] public float EnemyHPMultiplier = 1f;
        [Range(0.25f, 2f)] public float EnemyDamageMultiplier = 1f;
        [Range(0.5f, 2f)] public float TimingWindowMultiplier = 1f;
        [Range(0.5f, 3f)] public float ResourceGainMultiplier = 1f;
        public RespawnPenalty RespawnPenalty = RespawnPenalty.Normal;

        [Header("Cognitive — HUD")]
        [Tooltip("Hide non-essential HUD elements.")]
        public bool SimplifiedHUD;
    }

    public enum SubtitleSize : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2,
        ExtraLarge = 3
    }

    public enum RespawnPenalty : byte
    {
        None = 0,
        Light = 1,
        Normal = 2,
        Hardcore = 3
    }
}
