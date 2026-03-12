using Unity.Entities;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Interface for all destruction sources.
    /// Implemented by tools, weapons, explosives, vehicles that can destroy voxels.
    /// The DestructionMediatorSystem queries for entities with components implementing this pattern.
    /// 
    /// Note: Since Unity ECS doesn't support interfaces on components directly,
    /// this is implemented as a marker interface for managed wrappers and a corresponding
    /// IDestructionSourceComponent for ECS queries.
    /// </summary>
    public interface IDestructionSource
    {
        /// <summary>
        /// Get the current destruction intent from this source.
        /// Returns DestructionIntent.Invalid if no destruction should occur this frame.
        /// </summary>
        DestructionIntent GetDestructionIntent();
        
        /// <summary>
        /// Whether this source is currently active and producing destruction.
        /// </summary>
        bool IsActive { get; }
    }
    
    /// <summary>
    /// ECS-compatible marker component for entities that can produce destruction intents.
    /// Used by DestructionMediatorSystem to find active destruction sources.
    /// </summary>
    public struct DestructionSourceTag : IComponentData
    {
        /// <summary>Whether this source is currently active.</summary>
        public bool IsActive;
    }
    
    /// <summary>
    /// Buffer element for pending destruction intents.
    /// Destruction sources add intents to this buffer, mediator consumes them.
    /// </summary>
    public struct DestructionIntentBuffer : IBufferElementData
    {
        public DestructionIntent Intent;
        
        public static implicit operator DestructionIntent(DestructionIntentBuffer buffer) => buffer.Intent;
        public static implicit operator DestructionIntentBuffer(DestructionIntent intent) => new DestructionIntentBuffer { Intent = intent };
    }
}
