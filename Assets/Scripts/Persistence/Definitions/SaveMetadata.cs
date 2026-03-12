using System;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: JSON sidecar metadata written alongside .dig/.digw files.
    /// Provides human-readable information for editor tooling and save browsers.
    /// </summary>
    [Serializable]
    public class SaveMetadata
    {
        public int SlotIndex;
        public string PlayerName;
        public int CharacterLevel;
        public float PlaytimeSeconds;
        public long SaveTimestampUtcMs;
        public string GameVersion;
        public int SaveFormatVersion;
        public int ModuleCount;
        public string[] ModuleNames;
    }
}
