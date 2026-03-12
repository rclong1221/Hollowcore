namespace DIG.Tutorial.Config
{
    /// <summary>
    /// EPIC 18.4: How a tutorial step is presented to the player.
    /// </summary>
    public enum TutorialStepType : byte
    {
        Tooltip = 0,
        Highlight = 1,
        ForcedAction = 2,
        Popup = 3,
        WorldMarker = 4,
        Delay = 5,
        Branch = 6,
    }
}
