using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Core.Services
{
    /// <summary>
    /// EPIC 18.1: Theme configuration asset.
    /// Applies color and font overrides as inline styles on layer root VisualElements.
    /// Unity 2022.3 lacks a public SetCustomProperty API, so we apply overrides directly.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/UI/Theme")]
    public class UIThemeSO : ScriptableObject
    {
        [Header("Identity")]
        public string ThemeName = "Default";

        [Header("Primary Colors")]
        public Color Primary = new Color(0.13f, 0.59f, 0.95f);       // #2196F3
        public Color PrimaryLight = new Color(0.39f, 0.71f, 0.96f);
        public Color PrimaryDark = new Color(0.10f, 0.46f, 0.82f);

        [Header("Background Colors")]
        public Color Background = new Color(0.10f, 0.10f, 0.18f);     // #1A1A2E
        public Color Surface = new Color(0.09f, 0.13f, 0.24f);        // #16213E
        public Color BackgroundPanel = new Color(0.12f, 0.12f, 0.20f, 0.95f);

        [Header("Text Colors")]
        public Color TextPrimary = new Color(0.88f, 0.88f, 0.88f);    // #E0E0E0
        public Color TextSecondary = new Color(0.62f, 0.62f, 0.62f);

        [Header("Semantic Colors")]
        public Color Success = new Color(0.30f, 0.69f, 0.31f);        // #4CAF50
        public Color Warning = new Color(1.00f, 0.60f, 0.00f);        // #FF9800
        public Color Error = new Color(0.96f, 0.26f, 0.21f);          // #F44336

        [Header("Font Overrides")]
        [Tooltip("Leave null to use stylesheet defaults.")]
        public Font PrimaryFont;
        public Font HeadingFont;

        /// <summary>
        /// Applies this theme's color overrides as inline styles on the given root element.
        /// Call on each layer root (screen, modal, HUD, tooltip).
        /// </summary>
        public void ApplyToRoot(VisualElement root)
        {
            if (root == null) return;

            // Background color on the root container
            root.style.backgroundColor = Background;
            root.style.color = TextPrimary;

            // Font override if specified
            if (PrimaryFont != null)
            {
                root.style.unityFont = PrimaryFont;
            }
        }

        /// <summary>
        /// Applies theme colors to a panel overlay element (e.g., modal backdrop).
        /// </summary>
        public void ApplyToPanel(VisualElement panel)
        {
            if (panel == null) return;
            panel.style.backgroundColor = BackgroundPanel;
            panel.style.color = TextPrimary;
        }
    }
}
