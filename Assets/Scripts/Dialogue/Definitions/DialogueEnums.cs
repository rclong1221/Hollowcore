namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Node types in a dialogue tree.
    /// </summary>
    public enum DialogueNodeType : byte
    {
        Speech = 0,
        PlayerChoice = 1,
        Condition = 2,
        Action = 3,
        Random = 4,
        Hub = 5,
        End = 6
    }

    /// <summary>
    /// EPIC 16.16: Condition types for gating dialogue branches and choice visibility.
    /// </summary>
    public enum DialogueConditionType : byte
    {
        None = 0,
        QuestCompleted = 1,
        QuestActive = 2,
        HasItem = 3,
        HasCurrency = 4,
        PlayerLevel = 5,
        DialogueFlag = 6,
        DialogueFlagClear = 7,
        Reputation = 8,
        AlertLevelBelow = 9
    }

    /// <summary>
    /// EPIC 16.16: Actions executed by Action nodes during dialogue.
    /// </summary>
    public enum DialogueActionType : byte
    {
        AcceptQuest = 0,
        GiveItem = 1,
        TakeItem = 2,
        GiveCurrency = 3,
        TakeCurrency = 4,
        SetFlag = 5,
        ClearFlag = 6,
        OpenShop = 7,
        OpenCrafting = 8,
        TriggerEncounter = 9,
        PlayVoiceLine = 10
    }

    /// <summary>
    /// EPIC 16.16: Camera behavior during dialogue nodes.
    /// </summary>
    public enum DialogueCameraMode : byte
    {
        None = 0,
        CloseUp = 1,
        OverShoulder = 2,
        Custom = 3
    }

    /// <summary>
    /// EPIC 16.16: Categories for ambient NPC barks.
    /// </summary>
    public enum BarkCategory : byte
    {
        Greeting = 0,
        Idle = 1,
        Combat = 2,
        Flee = 3,
        Death = 4,
        Trade = 5,
        Alert = 6
    }

    /// <summary>
    /// EPIC 18.5: Dialogue priority levels for interrupt/queue behavior.
    /// Higher priority interrupts lower. Same priority queues behind current.
    /// </summary>
    public enum DialoguePriority : byte
    {
        Ambient = 0,
        Exploration = 50,
        Story = 100,
        Combat = 150,
        System = 200
    }

    /// <summary>
    /// EPIC 18.5: What happens to interrupted dialogue.
    /// </summary>
    public enum InterruptBehavior : byte
    {
        Discard = 0,
        Resume = 1,
        Restart = 2
    }
}
