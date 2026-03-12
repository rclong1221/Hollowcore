using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.19: Stores the spawn/home position of an AI entity.
    /// Used by LeashSystem to determine when AI should drop aggro and return home.
    /// </summary>
    public struct SpawnPosition : IComponentData
    {
        /// <summary>The world position where this entity was spawned.</summary>
        public float3 Position;
        
        /// <summary>Whether the spawn position has been initialized.</summary>
        public bool IsInitialized;
    }
}
