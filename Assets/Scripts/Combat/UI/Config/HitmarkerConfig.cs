// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · HitmarkerConfig
// Configuration for enhanced hitmarker visuals
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;

namespace DIG.Combat.UI.Config
{
    /// <summary>
    /// EPIC 15.9: Configuration for hitmarker visuals and animations.
    /// Create via: Assets → Create → DIG → Combat → Hitmarker Config
    /// </summary>
    [CreateAssetMenu(fileName = "HitmarkerConfig", menuName = "DIG/Combat/Hitmarker Config")]
    public class HitmarkerConfig : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────
        // Base Hitmarker Settings
        // ─────────────────────────────────────────────────────────────────
        [Header("Base Settings")]
        
        [Tooltip("Default hitmarker sprite")]
        public Sprite DefaultSprite;
        
        [Tooltip("Headshot/critical hitmarker sprite")]
        public Sprite CriticalSprite;
        
        [Tooltip("Kill confirmation hitmarker sprite")]
        public Sprite KillSprite;
        
        [Tooltip("Default hitmarker size")]
        [Range(16f, 64f)]
        public float DefaultSize = 32f;
        
        [Tooltip("Duration hitmarker stays visible")]
        [Range(0.1f, 0.5f)]
        public float DisplayDuration = 0.15f;
        
        // ─────────────────────────────────────────────────────────────────
        // Colors
        // ─────────────────────────────────────────────────────────────────
        [Header("Colors")]
        
        [Tooltip("Color for normal hits")]
        public Color NormalHitColor = Color.white;
        
        [Tooltip("Color for critical/headshot hits")]
        public Color CriticalHitColor = new Color(1f, 0.8f, 0.2f, 1f);
        
        [Tooltip("Color for kill confirmation")]
        public Color KillConfirmColor = new Color(1f, 0.3f, 0.3f, 1f);
        
        [Tooltip("Color for hits blocked by armor")]
        public Color ArmorHitColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        [Tooltip("Color for hits blocked by shield")]
        public Color ShieldHitColor = new Color(0.3f, 0.7f, 1f, 1f);
        
        // ─────────────────────────────────────────────────────────────────
        // Animation
        // ─────────────────────────────────────────────────────────────────
        [Header("Animation")]
        
        [Tooltip("Enable scale punch on hit")]
        public bool EnableScalePunch = true;
        
        [Tooltip("Scale punch amount (multiplier)")]
        [Range(1f, 2f)]
        public float ScalePunchAmount = 1.2f;
        
        [Tooltip("Scale punch duration")]
        [Range(0.05f, 0.2f)]
        public float ScalePunchDuration = 0.1f;
        
        [Tooltip("Enable rotation shake on hit")]
        public bool EnableRotationShake = false;
        
        [Tooltip("Rotation shake amount (degrees)")]
        [Range(0f, 15f)]
        public float RotationShakeAmount = 5f;
        
        [Tooltip("Fade out curve")]
        public AnimationCurve FadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        
        // ─────────────────────────────────────────────────────────────────
        // Hit Type Variations
        // ─────────────────────────────────────────────────────────────────
        [Header("Hit Type Variations")]
        
        [Tooltip("Scale multiplier for critical hits")]
        [Range(1f, 2f)]
        public float CriticalScaleMultiplier = 1.5f;
        
        [Tooltip("Duration multiplier for kill confirmation")]
        [Range(1f, 3f)]
        public float KillDurationMultiplier = 2f;
        
        [Tooltip("Enable screen-edge hit indicators for incoming damage")]
        public bool EnableDamageIndicators = true;
        
        // ─────────────────────────────────────────────────────────────────
        // Audio
        // ─────────────────────────────────────────────────────────────────
        [Header("Audio")]
        
        [Tooltip("Enable hit confirmation sounds")]
        public bool EnableHitSounds = true;
        
        [Tooltip("Normal hit sound")]
        public AudioClip NormalHitSound;
        
        [Tooltip("Critical hit sound")]
        public AudioClip CriticalHitSound;
        
        [Tooltip("Kill confirmation sound")]
        public AudioClip KillSound;
        
        [Tooltip("Hit sound volume")]
        [Range(0f, 1f)]
        public float HitSoundVolume = 0.5f;
        
        // ─────────────────────────────────────────────────────────────────
        // Accessibility
        // ─────────────────────────────────────────────────────────────────
        [Header("Accessibility")]
        
        [Tooltip("Enable high contrast mode for colorblind support")]
        public bool HighContrastMode = false;
        
        [Tooltip("Use shapes instead of colors for hit types")]
        public bool UseShapeDifferentiation = false;
    }
}
