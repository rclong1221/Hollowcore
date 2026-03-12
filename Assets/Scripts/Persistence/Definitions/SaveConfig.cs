using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Configuration for the save/load system.
    /// Loaded from Resources/SaveConfig by PersistenceBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "SaveConfig", menuName = "DIG/Persistence/Save Config", order = 0)]
    public class SaveConfig : ScriptableObject
    {
        [Header("Save Slots")]
        [Range(1, 10)] public int SaveSlotCount = 3;
        [Tooltip("Slot index used for autosave (0-based)")]
        public int AutosaveSlot = 0;
        [Tooltip("Slot index for quicksave (-1 = disabled)")]
        public int QuicksaveSlot = -1;

        [Header("Autosave")]
        [Tooltip("Seconds between autosaves. 0 = disabled.")]
        [Min(0)] public float AutosaveIntervalSeconds = 300f;

        [Header("Checkpoint Save")]
        public bool EnableCheckpointSave = true;
        [Tooltip("Minimum seconds between checkpoint saves")]
        [Min(0)] public float CheckpointCooldown = 30f;

        [Header("Shutdown")]
        public bool ShutdownSaveEnabled = true;

        [Header("Format")]
        [Tooltip("Current save format version. Bump when binary layout changes.")]
        public int SaveFormatVersion = 1;

        [Header("World Data")]
        [Tooltip("Maximum voxel modification records to persist")]
        [Min(1000)] public int MaxWorldDeltaRecords = 50000;
        public bool CompressWorldData = true;

        [Header("Directory")]
        [Tooltip("Subdirectory under Application.persistentDataPath")]
        public string SaveDirectory = "saves";
    }
}
