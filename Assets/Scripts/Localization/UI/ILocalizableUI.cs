namespace DIG.Localization
{
    /// <summary>
    /// Implement on any UI MonoBehaviour that displays localized text.
    /// Register with LocalizationUIRegistry to receive OnLocaleChanged callbacks.
    /// </summary>
    public interface ILocalizableUI
    {
        void OnLocaleChanged();
    }
}
