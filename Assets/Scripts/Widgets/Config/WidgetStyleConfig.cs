using UnityEngine;

namespace DIG.Widgets.Config
{
    /// <summary>
    /// EPIC 15.26 Phase 4: Per-style visual settings for health bars.
    /// Defines dimensions, colors, and font sizes for Thin/Standard/Compact styles.
    ///
    /// Create via: Assets > Create > DIG/Widgets/Widget Style Config
    /// </summary>
    [CreateAssetMenu(fileName = "WidgetStyle_Standard", menuName = "DIG/Widgets/Widget Style Config")]
    public class WidgetStyleConfig : ScriptableObject
    {
        [Header("Identity")]
        public HealthBarStyle Style = HealthBarStyle.Standard;

        [Header("Dimensions")]
        [Tooltip("Health bar width in world units.")]
        [Range(0.5f, 5f)]
        public float BarWidth = 1.5f;

        [Tooltip("Health bar height in world units.")]
        [Range(0.05f, 0.5f)]
        public float BarHeight = 0.15f;

        [Tooltip("Border thickness in world units.")]
        [Range(0f, 0.05f)]
        public float BorderWidth = 0.01f;

        [Header("Colors")]
        [Tooltip("Health bar fill color at full health.")]
        public Color HealthFullColor = new Color(0.2f, 0.85f, 0.2f, 1f); // green

        [Tooltip("Health bar fill color at low health.")]
        public Color HealthLowColor = new Color(0.85f, 0.2f, 0.2f, 1f); // red

        [Tooltip("Health threshold for color transition (0-1).")]
        [Range(0f, 0.5f)]
        public float LowHealthThreshold = 0.25f;

        [Tooltip("Health bar background color.")]
        public Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        [Tooltip("Border color.")]
        public Color BorderColor = new Color(0f, 0f, 0f, 1f);

        [Tooltip("Trail (delayed damage) color.")]
        public Color TrailColor = new Color(0.85f, 0.85f, 0.2f, 0.8f);

        [Header("Text")]
        [Tooltip("Font size for name text above bar.")]
        [Range(8f, 24f)]
        public float NameFontSize = 14f;

        [Tooltip("Font size for level text.")]
        [Range(8f, 20f)]
        public float LevelFontSize = 12f;

        [Tooltip("Show name text above health bar.")]
        public bool ShowName = true;

        [Tooltip("Show level text next to name.")]
        public bool ShowLevel = false;
    }
}
