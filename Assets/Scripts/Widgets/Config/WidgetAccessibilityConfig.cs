using UnityEngine;

namespace DIG.Widgets.Config
{
    /// <summary>
    /// EPIC 15.26 Phase 7: Accessibility settings for widgets.
    /// Font scaling, colorblind modes, reduced motion, and high contrast.
    ///
    /// Create via: Assets > Create > DIG/Widgets/Widget Accessibility Config
    /// </summary>
    [CreateAssetMenu(fileName = "WidgetAccessibility_Default", menuName = "DIG/Widgets/Widget Accessibility Config")]
    public class WidgetAccessibilityConfig : ScriptableObject
    {
        [Header("Font Scaling")]
        [Tooltip("Global font scale multiplier for all text-based widgets.")]
        [Range(0.75f, 2f)]
        public float FontScaleMultiplier = 1f;

        [Header("Colorblind Support")]
        [Tooltip("Colorblind mode for color remapping.")]
        public ColorblindMode Mode = ColorblindMode.None;

        [Header("Motion")]
        [Tooltip("Disable spawn/despawn animations, damage shake, and other motion effects.")]
        public bool ReducedMotion = false;

        [Header("Contrast")]
        [Tooltip("Add dark outlines behind all text and increase bar border thickness.")]
        public bool HighContrast = false;

        [Header("Widget Size")]
        [Tooltip("Additional scale multiplier on top of paradigm scale. For users who want bigger UI elements.")]
        [Range(0.75f, 2f)]
        public float WidgetSizeMultiplier = 1f;
    }

    /// <summary>
    /// Colorblind simulation modes for widget color remapping.
    /// </summary>
    public enum ColorblindMode : byte
    {
        /// <summary>No remapping — default palette.</summary>
        None = 0,

        /// <summary>Red-green blindness (most common, ~8% of males). Green→Blue.</summary>
        Deuteranopia = 1,

        /// <summary>Red-green blindness variant. Red→Yellow.</summary>
        Protanopia = 2,

        /// <summary>Blue-yellow blindness (rare). Blue→Pink.</summary>
        Tritanopia = 3
    }
}
