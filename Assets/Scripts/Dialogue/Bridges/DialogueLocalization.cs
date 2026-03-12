using DIG.Localization;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16 / 17.12: Localization bridge for dialogue text.
    /// Resolves string keys via LocalizationManager when initialized,
    /// otherwise returns the raw key as a fallback.
    /// </summary>
    public static class DialogueLocalization
    {
        public static string Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (LocalizationManager.IsInitialized)
                return LocalizationManager.Get(key);

            return key;
        }
    }
}
