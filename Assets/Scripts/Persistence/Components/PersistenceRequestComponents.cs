using Unity.Collections;
using Unity.Entities;

namespace DIG.Persistence
{
    /// <summary>EPIC 16.15: What triggered a save.</summary>
    public enum SaveTriggerSource : byte
    {
        Manual = 0,
        Autosave = 1,
        Checkpoint = 2,
        Shutdown = 3,
        Reconnect = 4
    }

    /// <summary>EPIC 16.15: Transient entity requesting a save operation.</summary>
    public struct SaveRequest : IComponentData
    {
        public int SlotIndex;
        public SaveTriggerSource TriggerSource;
        public FixedString64Bytes TargetPlayerId;
    }

    /// <summary>EPIC 16.15: Transient entity requesting a load operation.</summary>
    public struct LoadRequest : IComponentData
    {
        public int SlotIndex;
        public FixedString64Bytes TargetPlayerId;
    }

    /// <summary>EPIC 16.15: Transient entity signaling save completion.</summary>
    public struct SaveComplete : IComponentData
    {
        public int SlotIndex;
        public bool Success;
        public FixedString128Bytes ErrorMessage;
        public SaveTriggerSource TriggerSource;
    }

    /// <summary>EPIC 16.15: Transient entity signaling load completion.</summary>
    public struct LoadComplete : IComponentData
    {
        public int SlotIndex;
        public bool Success;
        public FixedString128Bytes ErrorMessage;
    }
}
