using UnityEngine;
using TMPro;

namespace DIG.Localization
{
    [CreateAssetMenu(menuName = "DIG/Localization/Locale Definition")]
    public class LocaleDefinition : ScriptableObject
    {
        [Tooltip("IETF BCP 47 code (e.g., en-US, ja-JP, ar-SA).")]
        public string LocaleCode = "en-US";

        [Tooltip("Native display name (e.g., English, 日本語, العربية).")]
        public string DisplayName = "English";

        [Tooltip("English name for editor display.")]
        public string EnglishName = "English";

        [Tooltip("Text direction for this locale.")]
        public TextDirection TextDirection = TextDirection.LTR;

        [Tooltip("Plural rule set for this locale.")]
        public PluralRuleSet PluralRuleSet = PluralRuleSet.English;

        [Tooltip("Primary font for this locale.")]
        public TMP_FontAsset DefaultFont;

        [Tooltip("Fallback fonts for missing glyphs.")]
        public TMP_FontAsset[] FallbackFonts;

        [Tooltip("Line spacing multiplier (1.2 recommended for CJK).")]
        [Range(0.5f, 2.0f)]
        public float LineSpacingMultiplier = 1.0f;

        [Tooltip("Character spacing multiplier for dense scripts.")]
        [Range(0.5f, 2.0f)]
        public float CharacterSpacingMultiplier = 1.0f;

        [Tooltip("Editor flag: true when all string tables are fully translated for this locale.")]
        public bool IsComplete;
    }
}
