using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Shield component for entities with regenerating shields.
    /// Shield absorbs damage before health is affected.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct Shield : IComponentData
    {
        [GhostField(Quantization = 100)]
        public float Current;
        
        [GhostField(Quantization = 100)]
        public float Max;
        
        /// <summary>Seconds after damage before regen starts</summary>
        public float RegenDelay;
        
        /// <summary>Shield points per second during regen</summary>
        public float RegenRate;
        
        /// <summary>Server tick when last damaged (for regen delay)</summary>
        [GhostField]
        public uint LastDamageTime;
        
        public float Normalized => Max > 0 ? Current / Max : 0f;
        public bool IsDepleted => Current <= 0f;
        public bool IsFull => Current >= Max;
        
        public static Shield Default => new Shield
        {
            Current = 50f,
            Max = 50f,
            RegenDelay = 3f,
            RegenRate = 10f,
            LastDamageTime = 0
        };
    }
    
    /// <summary>
    /// Tag component marking an entity as damageable.
    /// Used for efficient queries by damage systems.
    /// Carries authored MaxHealth so runtime fixup systems can use the correct value.
    /// </summary>
    public struct DamageableTag : IComponentData
    {
        public float MaxHealth;
    }
    
    /// <summary>
    /// Tag for entities that should automatically respawn after death.
    /// </summary>
    public struct AutoRespawnTag : IComponentData { }
    
    /// <summary>
    /// <summary>
    /// Tag for entities that should display world-space health bars.
    /// Consumed by the MonoBehaviour health bar system.
    /// Replicated to clients so they can display enemy health bars.
    /// </summary>
    [Unity.NetCode.GhostComponent(PrefabType = Unity.NetCode.GhostPrefabType.All)]
    public struct ShowHealthBarTag : IComponentData { }
    
    /// <summary>
    /// Tag for entities that should display floating damage numbers.
    /// Consumed by the damage number system.
    /// </summary>
    public struct ShowDamageNumbersTag : IComponentData { }
}
