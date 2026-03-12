using TMPro;
using UnityEngine;

namespace DIG.Localization
{
    /// <summary>
    /// Attach to any GameObject with a TMP_Text to automatically resolve
    /// a string key on enable and re-resolve when the locale changes.
    /// </summary>
    [AddComponentMenu("DIG/Localization/Localized Text")]
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("String table key to resolve.")]
        public string StringKey;

        [Tooltip("Displayed if key is not found or manager is not initialized.")]
        public string FallbackText;

        [Tooltip("Font style to apply from the locale's font mapping.")]
        public FontStyle FontStyle = FontStyle.Body;

        [Tooltip("Resolve key immediately on enable.")]
        public bool AutoResolveOnEnable = true;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLocaleChanged += HandleLocaleChanged;
            if (AutoResolveOnEnable)
                Resolve();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLocaleChanged -= HandleLocaleChanged;
        }

        private void HandleLocaleChanged()
        {
            Resolve();
        }

        public void Resolve()
        {
            if (_text == null) return;

            if (LocalizationManager.IsInitialized && !string.IsNullOrEmpty(StringKey))
            {
                string resolved = LocalizationManager.Get(StringKey);
                _text.text = resolved;
            }
            else if (!string.IsNullOrEmpty(FallbackText))
            {
                _text.text = FallbackText;
            }

            ApplyFont();
        }

        public void SetKey(string newKey)
        {
            StringKey = newKey;
            Resolve();
        }

        private void ApplyFont()
        {
            if (_text == null) return;

            var font = LocalizationManager.GetFont(FontStyle);
            if (font != null)
                _text.font = font;

            var locale = LocalizationManager.CurrentLocale;
            if (locale != null)
            {
                _text.lineSpacing = (locale.LineSpacingMultiplier - 1f) * 100f;
                _text.characterSpacing = (locale.CharacterSpacingMultiplier - 1f) * 100f;

                _text.isRightToLeftText = locale.TextDirection == TextDirection.RTL;
            }
        }
    }
}
