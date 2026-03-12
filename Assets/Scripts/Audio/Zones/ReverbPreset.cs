namespace Audio.Zones
{
    /// <summary>
    /// Reverb environment presets matching AudioMixer snapshot configurations.
    /// EPIC 15.27 Phase 4.
    /// </summary>
    public enum ReverbPreset : byte
    {
        OpenField = 0,
        Forest = 1,
        SmallRoom = 2,
        LargeHall = 3,
        Tunnel = 4,
        Cave = 5,
        Underwater = 6,
        Ship_Interior = 7,
        Ship_Exterior = 8,
        Custom = 255
    }
}
