namespace Audio.Config
{
    /// <summary>
    /// Bus routing enum matching AudioMixer groups.
    /// Each bus maps to an AudioMixerGroup on the master mixer.
    /// EPIC 15.27 Phase 1.
    /// </summary>
    public enum AudioBusType : byte
    {
        Combat = 0,
        Ambient = 1,
        Music = 2,
        Dialogue = 3,
        UI = 4,
        Footstep = 5
    }
}
