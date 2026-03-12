using UnityEngine;

namespace DIG.Widgets.Config
{
    /// <summary>
    /// EPIC 15.26 Phase 7: Colorblind-safe color palettes for widget rendering.
    /// Maps standard game colors to colorblind-friendly alternatives.
    ///
    /// Create via: Assets > Create > DIG/Widgets/Colorblind Palette
    /// </summary>
    [CreateAssetMenu(fileName = "ColorblindPalette_Default", menuName = "DIG/Widgets/Colorblind Palette")]
    public class ColorblindPalette : ScriptableObject
    {
        [Header("Identity")]
        public ColorblindMode Mode = ColorblindMode.None;

        [Header("Health Colors")]
        [Tooltip("Health bar fill color (default: green → blue for deuteranopia).")]
        public Color HealthFull = new Color(0.2f, 0.85f, 0.2f, 1f);

        [Tooltip("Health bar low health color (default: red → orange for deuteranopia).")]
        public Color HealthLow = new Color(0.85f, 0.2f, 0.2f, 1f);

        [Header("Damage/Healing")]
        [Tooltip("Damage number color (default: red → orange for deuteranopia).")]
        public Color DamageText = new Color(1f, 0.2f, 0.2f, 1f);

        [Tooltip("Healing number color (default: green → cyan for deuteranopia).")]
        public Color HealingText = new Color(0.2f, 1f, 0.2f, 1f);

        [Tooltip("Critical hit color.")]
        public Color CriticalText = new Color(1f, 0.85f, 0f, 1f);

        [Header("Shield/Absorb")]
        [Tooltip("Shield bar color (default: blue → purple for tritanopia).")]
        public Color ShieldColor = new Color(0.3f, 0.6f, 1f, 1f);

        [Header("Status Effects")]
        [Tooltip("Buff (positive) icon tint.")]
        public Color BuffTint = new Color(0.3f, 0.8f, 1f, 1f);

        [Tooltip("Debuff (negative) icon tint.")]
        public Color DebuffTint = new Color(1f, 0.4f, 0.4f, 1f);

        /// <summary>
        /// Get the appropriate palette for a given colorblind mode from an array of palettes.
        /// Returns null if no match found (use default colors).
        /// </summary>
        public static ColorblindPalette GetPaletteForMode(ColorblindPalette[] palettes, ColorblindMode mode)
        {
            if (palettes == null || mode == ColorblindMode.None) return null;

            for (int i = 0; i < palettes.Length; i++)
            {
                if (palettes[i] != null && palettes[i].Mode == mode)
                    return palettes[i];
            }

            return null;
        }
    }
}
