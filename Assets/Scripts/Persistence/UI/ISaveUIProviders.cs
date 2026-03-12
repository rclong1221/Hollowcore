namespace DIG.Persistence.UI
{
    /// <summary>
    /// EPIC 16.15: Notification types for save/load feedback.
    /// </summary>
    public enum SaveNotificationType : byte
    {
        SaveStarted,
        SaveCompleted,
        SaveFailed,
        LoadStarted,
        LoadCompleted,
        LoadFailed,
        AutosaveCompleted,
        CheckpointSaved
    }

    /// <summary>
    /// Data structure for save/load notification events.
    /// </summary>
    public struct SaveNotification
    {
        public SaveNotificationType Type;
        public int SlotIndex;
        public string PlayerName;
        public float Timestamp;
    }

    /// <summary>
    /// Data structure for save slot display.
    /// </summary>
    public struct SaveSlotInfo
    {
        public int SlotIndex;
        public bool IsOccupied;
        public string PlayerName;
        public int CharacterLevel;
        public float PlaytimeSeconds;
        public string LastSavedTimestamp;
        public bool IsAutosaveSlot;
    }

    /// <summary>
    /// EPIC 16.15: Interface for save/load notification display.
    /// Shows brief toast-style notifications (e.g. "Game Saved", "Loading...").
    /// </summary>
    public interface ISaveNotificationProvider
    {
        /// <summary>Show a save/load notification.</summary>
        void ShowNotification(SaveNotification notification);

        /// <summary>Hide any currently visible notification.</summary>
        void HideNotification();
    }

    /// <summary>
    /// EPIC 16.15: Interface for save slot selection UI.
    /// Shows a list of save slots for manual save/load operations.
    /// </summary>
    public interface ISaveSlotProvider
    {
        /// <summary>Update the displayed save slots.</summary>
        void RefreshSlots(SaveSlotInfo[] slots);

        /// <summary>Show the save slot selection panel.</summary>
        void Show();

        /// <summary>Hide the save slot selection panel.</summary>
        void Hide();
    }

    /// <summary>
    /// EPIC 16.15: Interface for save progress indicator.
    /// Shows a spinner or progress bar during save/load operations.
    /// </summary>
    public interface ISaveProgressProvider
    {
        /// <summary>Show progress indicator with optional message.</summary>
        void ShowProgress(string message);

        /// <summary>Hide progress indicator.</summary>
        void HideProgress();
    }
}
