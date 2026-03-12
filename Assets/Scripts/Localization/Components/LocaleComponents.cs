using Unity.Entities;

namespace DIG.Localization
{
    /// <summary>
    /// Client-only singleton storing the active locale index.
    /// Created by LocalizationBootstrapSystem. Not ghost-replicated.
    /// </summary>
    public struct LocaleConfig : IComponentData
    {
        public byte CurrentLocaleId;
        public byte FallbackLocaleId;
        public ushort Reserved;
    }

    /// <summary>
    /// Zero-size tag added when the locale changes.
    /// LocalizedTextRefreshSystem dispatches to all ILocalizableUI providers then removes it.
    /// </summary>
    public struct LocaleChangedTag : IComponentData { }
}
