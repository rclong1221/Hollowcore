namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Category classification for music tracks.
    /// </summary>
    public enum MusicTrackCategory : byte
    {
        Exploration = 0,
        Combat = 1,
        Boss = 2,
        Ambient = 3,
        Town = 4,
        Dungeon = 5
    }

    /// <summary>
    /// EPIC 17.5: Category classification for stinger events.
    /// </summary>
    public enum StingerCategory : byte
    {
        LevelUp = 0,
        QuestComplete = 1,
        Death = 2,
        RareItem = 3,
        Achievement = 4,
        BossIntro = 5,
        Discovery = 6
    }

    /// <summary>
    /// EPIC 17.5: Default priority values for stinger categories.
    /// Higher value = more important (interrupts lower).
    /// </summary>
    public static class StingerPriority
    {
        public const byte Death = 100;
        public const byte BossIntro = 90;
        public const byte LevelUp = 80;
        public const byte QuestComplete = 70;
        public const byte Achievement = 60;
        public const byte RareItem = 50;
        public const byte Discovery = 40;
    }
}
