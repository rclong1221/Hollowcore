namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Interface for UI components that display music state.
    /// Registered with MusicUIRegistry, called by MusicUIBridgeSystem.
    /// Follows CombatUIRegistry / ICombatUIProvider pattern.
    /// </summary>
    public interface IMusicUIProvider
    {
        void OnTrackChanged(string trackName, MusicTrackCategory category);
        void OnCombatIntensityChanged(float intensity);
        void OnStingerPlayed(string stingerName, StingerCategory category);
    }
}
