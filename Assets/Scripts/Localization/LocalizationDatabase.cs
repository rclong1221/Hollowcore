using System.Collections.Generic;
using UnityEngine;

namespace DIG.Localization
{
    [CreateAssetMenu(menuName = "DIG/Localization/Localization Database")]
    public class LocalizationDatabase : ScriptableObject
    {
        [Tooltip("All supported locales. Index 0 is the default/fallback locale.")]
        public List<LocaleDefinition> Locales = new();

        [Tooltip("All string tables across all content domains.")]
        public List<StringTableSO> StringTables = new();

        [Tooltip("Per-locale font asset mappings.")]
        public List<FontMappingSO> FontMappings = new();

        [Tooltip("Default locale code if system detection fails.")]
        public string DefaultLocaleCode = "en-US";

        [Tooltip("Editor-only: generate pseudo-localized test strings via Get().")]
        public bool EnablePseudoLocalization;

        [Tooltip("Synthetic locale code for pseudo-localization.")]
        public string PseudoLocaleCode = "pseudo";
    }
}
