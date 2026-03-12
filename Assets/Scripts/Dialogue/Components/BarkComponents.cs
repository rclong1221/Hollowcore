using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Bark emitter state on NPC entities.
    /// Tracks cooldown timing for ambient chatter.
    /// </summary>
    public struct BarkEmitter : IComponentData
    {
        public int BarkCollectionId;
        public float LastBarkTime;
        public float BarkCooldown;
    }

    /// <summary>
    /// EPIC 16.16: Transient entity requesting a bark display.
    /// Created by BarkTimerSystem, consumed by BarkDisplaySystem same frame.
    /// </summary>
    public struct BarkRequest : IComponentData
    {
        public Entity EmitterEntity;
        public int LineIndex;
        public float3 Position;
    }
}
