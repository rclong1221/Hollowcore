using System.Collections.Generic;

namespace DIG.Localization
{
    /// <summary>
    /// Static registry for ILocalizableUI providers.
    /// Follows the CombatUIRegistry pattern.
    /// </summary>
    public static class LocalizationUIRegistry
    {
        private static readonly List<ILocalizableUI> _providers = new();

        public static int ProviderCount => _providers.Count;

        public static void RegisterProvider(ILocalizableUI provider)
        {
            if (provider != null && !_providers.Contains(provider))
                _providers.Add(provider);
        }

        public static void UnregisterProvider(ILocalizableUI provider)
        {
            _providers.Remove(provider);
        }

        public static void NotifyLocaleChanged()
        {
            for (int i = _providers.Count - 1; i >= 0; i--)
            {
                if (i < _providers.Count && _providers[i] != null)
                    _providers[i].OnLocaleChanged();
            }
        }

        public static void UnregisterAll()
        {
            _providers.Clear();
        }
    }
}
