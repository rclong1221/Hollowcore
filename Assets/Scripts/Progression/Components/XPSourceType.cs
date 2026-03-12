namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Source of an XP award, used for UI display and analytics.
    /// </summary>
    public enum XPSourceType : byte
    {
        Kill = 0,
        Quest = 1,
        Crafting = 2,
        Exploration = 3,
        Interaction = 4,
        Bonus = 5
    }
}
