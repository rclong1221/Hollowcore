using Unity.Mathematics;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Animation/VFX/sound event types emitted from Timeline signals.
    /// </summary>
    public enum CinematicAnimEventType : byte
    {
        PlayAnimation   = 0,  // Drive NPC entity animation state
        SpawnVFX        = 1,  // Spawn VFX at marker position
        PlaySound       = 2,  // Play sound effect
        TriggerDialogue = 3,  // Start dialogue node
        FadeToBlack     = 4   // Screen fade
    }

    /// <summary>
    /// EPIC 17.9: Event struct bridged from Timeline SignalReceiver to ECS consumers.
    /// Used in CinematicAnimEventQueue static queue (NOT an IComponentData).
    /// </summary>
    public struct CinematicAnimEvent
    {
        public CinematicAnimEventType EventType;  // 1 byte
        public int TargetId;                       // 4 bytes -- NPC entity identifier or VFX type
        public int IntParam;                       // 4 bytes -- animation hash, dialogue tree id, etc.
        public float FloatParam;                   // 4 bytes -- duration, intensity
        public float3 Position;                    // 12 bytes -- world position for VFX/sound
        // Total: ~28 bytes (with padding)
    }
}
