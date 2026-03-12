namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Stable TypeId constants for ISaveModule implementations.
    /// These are written into save file binary headers and MUST NEVER CHANGE.
    /// </summary>
    public static class SaveModuleTypeIds
    {
        public const int PlayerStats    = 1;
        public const int Inventory      = 2;
        public const int Equipment      = 3;
        public const int Quests         = 4;
        public const int Crafting       = 5;
        public const int World          = 6;
        public const int Settings       = 7;
        public const int StatusEffects  = 8;
        public const int Survival       = 9;
        public const int Progression    = 10;
        public const int Talents        = 11;
        public const int Party          = 12;
        public const int Map            = 13;
        public const int Achievements   = 14;
        public const int PvPRanking     = 15;
        public const int MetaProgression = 16;
        public const int RunHistory      = 17;
        // 18-127: reserved for future modules
    }
}
