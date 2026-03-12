namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Quest category for UI grouping and filtering.
    /// </summary>
    public enum QuestCategory : byte
    {
        Main = 0,
        Side = 1,
        Daily = 2,
        Event = 3,
        Tutorial = 4
    }

    /// <summary>
    /// EPIC 16.12: Objective type determines which emitter system produces matching QuestEvents.
    /// Adding a new type requires only a new emitter system -- no changes to the evaluator.
    /// </summary>
    public enum ObjectiveType : byte
    {
        Kill = 0,
        Interact = 1,
        Collect = 2,
        ReachZone = 3,
        Escort = 4,
        Survive = 5,
        Craft = 6,
        Custom = 7
    }

    /// <summary>
    /// EPIC 16.12: Lifecycle state of a quest instance.
    /// </summary>
    public enum QuestState : byte
    {
        Available = 0,
        Active = 1,
        Completed = 2,
        Failed = 3,
        TurnedIn = 4
    }

    /// <summary>
    /// EPIC 16.12: Lifecycle state of a single objective within a quest.
    /// </summary>
    public enum ObjectiveState : byte
    {
        Locked = 0,
        Active = 1,
        Completed = 2,
        Failed = 3
    }

    /// <summary>
    /// EPIC 16.12: Type of reward granted on quest completion.
    /// </summary>
    public enum QuestRewardType : byte
    {
        Item = 0,
        Currency = 1,
        Experience = 2,
        RecipeUnlock = 3
    }
}
