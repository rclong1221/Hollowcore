namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Interface for the XP bar UI component.
    /// </summary>
    public interface IXPBarProvider
    {
        void UpdateXPBar(int level, int currentXP, int xpToNextLevel, float percent, int unspentStatPoints, float restedXP);
    }

    /// <summary>
    /// EPIC 16.14: Interface for level-up popup/notification UI.
    /// </summary>
    public interface ILevelUpPopupProvider
    {
        void ShowLevelUp(int newLevel, int previousLevel, int statPointsAwarded);
    }

    /// <summary>
    /// EPIC 16.14: Interface for floating XP gain numbers.
    /// </summary>
    public interface IXPGainProvider
    {
        void ShowXPGain(int amount, XPSourceType source);
    }

    /// <summary>
    /// EPIC 16.14: Interface for stat allocation panel UI.
    /// </summary>
    public interface IStatAllocationProvider
    {
        void UpdateStatAllocation(int unspentPoints, int strength, int dexterity, int intelligence, int vitality);
    }
}
