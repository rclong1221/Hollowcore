namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Data struct passed to UI providers each frame with quest state.
    /// </summary>
    public struct QuestLogEntry
    {
        public int QuestId;
        public string DisplayName;
        public string Description;
        public QuestCategory Category;
        public QuestState State;
        public float TimeRemaining;
        public ObjectiveEntry[] Objectives;
    }

    /// <summary>
    /// EPIC 16.12: Data struct for a single objective within a quest.
    /// </summary>
    public struct ObjectiveEntry
    {
        public int ObjectiveId;
        public string Description;
        public ObjectiveState State;
        public int CurrentCount;
        public int RequiredCount;
        public bool IsOptional;
        public bool IsHidden;
    }

    /// <summary>
    /// EPIC 16.12: Interface for quest log display (full quest list panel).
    /// Implement with any UI system (Unity UI, UI Toolkit, custom).
    /// </summary>
    public interface IQuestLogProvider
    {
        void UpdateQuestLog(QuestLogEntry[] activeQuests);
        void ClearQuestLog();
    }

    /// <summary>
    /// EPIC 16.12: Interface for on-screen objective tracker (HUD overlay).
    /// Shows tracked quest objectives with progress bars.
    /// </summary>
    public interface IObjectiveTrackerProvider
    {
        void UpdateTrackedObjectives(QuestLogEntry trackedQuest);
        void ClearTracker();
    }

    /// <summary>
    /// EPIC 16.12: Interface for quest notification popups (accepted, completed, failed).
    /// </summary>
    public interface IQuestNotificationProvider
    {
        void ShowQuestAccepted(string questName);
        void ShowQuestCompleted(string questName);
        void ShowQuestFailed(string questName);
        void ShowObjectiveProgress(string objectiveDescription, int current, int required);
    }
}
