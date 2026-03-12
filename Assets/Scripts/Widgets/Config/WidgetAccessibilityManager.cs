using UnityEngine;

namespace DIG.Widgets.Config
{
    /// <summary>
    /// EPIC 15.26 Phase 7: Singleton manager for widget accessibility state.
    /// Reads from WidgetAccessibilityConfig and exposes values for adapters/renderers.
    /// Persists user preferences via PlayerPrefs.
    /// </summary>
    [AddComponentMenu("DIG/Widgets/Widget Accessibility Manager")]
    public class WidgetAccessibilityManager : MonoBehaviour
    {
        private static WidgetAccessibilityManager _instance;
        public static WidgetAccessibilityManager Instance => _instance;
        public static bool HasInstance => _instance != null;

        [Header("Config")]
        [Tooltip("Default accessibility config asset. Overridden by PlayerPrefs at runtime.")]
        [SerializeField] private WidgetAccessibilityConfig _defaultConfig;

        // ── Runtime state (read by adapters) ───────────────────────

        /// <summary>Font scale multiplier (paradigm * accessibility * user pref).</summary>
        public float FontScale { get; private set; } = 1f;

        /// <summary>Active colorblind mode.</summary>
        public ColorblindMode Colorblind { get; private set; } = ColorblindMode.None;

        /// <summary>Whether reduced motion is enabled (no animations).</summary>
        public bool ReducedMotion { get; private set; }

        /// <summary>Whether high contrast is enabled (outlines, thicker borders).</summary>
        public bool HighContrast { get; private set; }

        /// <summary>Additional widget size multiplier from accessibility.</summary>
        public float WidgetSizeScale { get; private set; } = 1f;

        // PlayerPrefs keys
        private const string PrefFontScale = "Widget_FontScale";
        private const string PrefColorblind = "Widget_Colorblind";
        private const string PrefReducedMotion = "Widget_ReducedMotion";
        private const string PrefHighContrast = "Widget_HighContrast";
        private const string PrefWidgetSize = "Widget_SizeScale";

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            LoadSettings();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>Load settings from PlayerPrefs, falling back to config defaults.</summary>
        public void LoadSettings()
        {
            float defaultFont = _defaultConfig != null ? _defaultConfig.FontScaleMultiplier : 1f;
            int defaultCB = _defaultConfig != null ? (int)_defaultConfig.Mode : 0;
            bool defaultRM = _defaultConfig != null && _defaultConfig.ReducedMotion;
            bool defaultHC = _defaultConfig != null && _defaultConfig.HighContrast;
            float defaultSize = _defaultConfig != null ? _defaultConfig.WidgetSizeMultiplier : 1f;

            FontScale = PlayerPrefs.GetFloat(PrefFontScale, defaultFont);
            Colorblind = (ColorblindMode)PlayerPrefs.GetInt(PrefColorblind, defaultCB);
            ReducedMotion = PlayerPrefs.GetInt(PrefReducedMotion, defaultRM ? 1 : 0) == 1;
            HighContrast = PlayerPrefs.GetInt(PrefHighContrast, defaultHC ? 1 : 0) == 1;
            WidgetSizeScale = PlayerPrefs.GetFloat(PrefWidgetSize, defaultSize);
        }

        /// <summary>Persist current settings to PlayerPrefs.</summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(PrefFontScale, FontScale);
            PlayerPrefs.SetInt(PrefColorblind, (int)Colorblind);
            PlayerPrefs.SetInt(PrefReducedMotion, ReducedMotion ? 1 : 0);
            PlayerPrefs.SetInt(PrefHighContrast, HighContrast ? 1 : 0);
            PlayerPrefs.SetFloat(PrefWidgetSize, WidgetSizeScale);
            PlayerPrefs.Save();
        }

        /// <summary>Reset to config defaults and save.</summary>
        public void ResetToDefaults()
        {
            if (_defaultConfig != null)
            {
                FontScale = _defaultConfig.FontScaleMultiplier;
                Colorblind = _defaultConfig.Mode;
                ReducedMotion = _defaultConfig.ReducedMotion;
                HighContrast = _defaultConfig.HighContrast;
                WidgetSizeScale = _defaultConfig.WidgetSizeMultiplier;
            }
            else
            {
                FontScale = 1f;
                Colorblind = ColorblindMode.None;
                ReducedMotion = false;
                HighContrast = false;
                WidgetSizeScale = 1f;
            }
            SaveSettings();
        }

        // ── Setters for UI sliders ─────────────────────────────────

        public void SetFontScale(float scale)
        {
            FontScale = Mathf.Clamp(scale, 0.75f, 2f);
            SaveSettings();
        }

        public void SetColorblindMode(ColorblindMode mode)
        {
            Colorblind = mode;
            SaveSettings();
        }

        public void SetReducedMotion(bool enabled)
        {
            ReducedMotion = enabled;
            SaveSettings();
        }

        public void SetHighContrast(bool enabled)
        {
            HighContrast = enabled;
            SaveSettings();
        }

        public void SetWidgetSizeScale(float scale)
        {
            WidgetSizeScale = Mathf.Clamp(scale, 0.75f, 2f);
            SaveSettings();
        }
    }
}
