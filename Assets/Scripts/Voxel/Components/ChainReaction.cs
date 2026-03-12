using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Interface for entities that can be triggered by chain reactions.
    /// Implemented by explosives, gas pockets, fuel containers, etc.
    /// </summary>
    public interface IChainTriggerable
    {
        /// <summary>Whether this entity can currently be triggered.</summary>
        bool CanTrigger { get; }
        
        /// <summary>Radius at which this entity detects triggering events.</summary>
        float TriggerRadius { get; }
    }
    
    /// <summary>
    /// EPIC 15.10: Component for entities that can be chain triggered by explosions.
    /// </summary>
    public struct ChainTriggerable : IComponentData
    {
        /// <summary>Radius at which explosions can trigger this entity.</summary>
        public float TriggerRadius;
        
        /// <summary>Minimum explosion damage to trigger.</summary>
        public float TriggerThreshold;
        
        /// <summary>Delay before triggering (seconds).</summary>
        public float TriggerDelay;
        
        /// <summary>Whether this has been triggered (waiting for delay).</summary>
        public bool IsTriggered;
        
        /// <summary>Time remaining before detonation.</summary>
        public float TriggerTimer;
        
        /// <summary>Depth in chain (to prevent infinite loops).</summary>
        public int ChainDepth;
        
        /// <summary>Maximum allowed chain depth.</summary>
        public int MaxChainDepth;
        
        /// <summary>Default for explosives.</summary>
        public static ChainTriggerable Explosive => new()
        {
            TriggerRadius = 5f,
            TriggerThreshold = 50f,
            TriggerDelay = 0.1f,
            IsTriggered = false,
            TriggerTimer = 0f,
            ChainDepth = 0,
            MaxChainDepth = 10
        };
        
        /// <summary>Gas pocket - larger trigger radius, instant.</summary>
        public static ChainTriggerable GasPocket => new()
        {
            TriggerRadius = 8f,
            TriggerThreshold = 10f,  // Easy to trigger
            TriggerDelay = 0f,       // Instant
            IsTriggered = false,
            TriggerTimer = 0f,
            ChainDepth = 0,
            MaxChainDepth = 5
        };
        
        /// <summary>Fuel container - moderate trigger.</summary>
        public static ChainTriggerable FuelContainer => new()
        {
            TriggerRadius = 3f,
            TriggerThreshold = 100f,  // Needs significant damage
            TriggerDelay = 0.5f,      // Brief delay
            IsTriggered = false,
            TriggerTimer = 0f,
            ChainDepth = 0,
            MaxChainDepth = 3
        };
    }
    
    /// <summary>
    /// EPIC 15.10: Environmental hazard component.
    /// Marks voxel regions that have special effects when destroyed.
    /// </summary>
    public struct EnvironmentalHazard : IComponentData
    {
        public EnvironmentalHazardType Type;
        public float3 Position;
        public float Radius;
        public float Intensity;
    }
    
    /// <summary>
    /// EPIC 15.10: Types of environmental hazards.
    /// </summary>
    public enum EnvironmentalHazardType : byte
    {
        None = 0,
        GasPocket = 1,        // Explodes when breached
        LavaFlow = 2,         // Deals heat damage over time
        UnstableGround = 3,   // Collapses when damaged
        ToxicVent = 4,        // Releases poison when opened
        WaterPocket = 5,      // Floods area when breached
        CrystalVein = 6       // Reflective surface, amplifies lasers
    }
    
    /// <summary>
    /// EPIC 15.10: Event emitted when a chain reaction is triggered.
    /// </summary>
    public struct ChainReactionEvent : IComponentData
    {
        public Entity SourceEntity;
        public Entity TriggeredEntity;
        public float3 Position;
        public float Damage;
        public int ChainDepth;
    }
    
    /// <summary>
    /// EPIC 15.10: Event emitted when environmental hazard is breached.
    /// </summary>
    public struct EnvironmentalHazardEvent : IComponentData
    {
        public EnvironmentalHazardType Type;
        public float3 Position;
        public float Intensity;
        public Entity SourceEntity;
    }
}
