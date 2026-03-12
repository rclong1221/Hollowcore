using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.UI
{
    /// <summary>
    /// ECS Component that tracks health bar visibility state for an entity.
    /// This is updated by the HealthBarVisibilitySystem and consumed by the bridge.
    /// </summary>
    public struct HealthBarVisibilityState : IComponentData
    {
        /// <summary>Time (in seconds, unscaled) when this entity last took damage.</summary>
        public double LastDamageTime;
        
        /// <summary>Time when combat state ended (for timeout modes).</summary>
        public double CombatEndedTime;
        
        /// <summary>Time when local player last dealt damage to this entity.</summary>
        public double LastPlayerDamageTime;
        
        /// <summary>Previous HP value for detecting damage events.</summary>
        public float PreviousHP;
        
        /// <summary>Current visibility alpha (0-1) for smooth transitions.</summary>
        public float CurrentAlpha;
        
        /// <summary>Target alpha we're fading towards.</summary>
        public float TargetAlpha;
        
        /// <summary>Current scale for the health bar.</summary>
        public float CurrentScale;
        
        /// <summary>Whether the health bar is currently supposed to be shown.</summary>
        public bool IsVisible;
        
        /// <summary>Whether this entity has been discovered (bestiary system).</summary>
        public bool IsDiscovered;
        
        /// <summary>Whether this entity has been scanned (metroidvania style).</summary>
        public bool IsScanned;
        
        /// <summary>Whether local player has dealt damage to this entity this session.</summary>
        public bool PlayerHasDamaged;
        
        /// <summary>
        /// Create default state for a new entity.
        /// </summary>
        public static HealthBarVisibilityState Default => new HealthBarVisibilityState
        {
            LastDamageTime = -100,
            CombatEndedTime = -100,
            LastPlayerDamageTime = -100,
            PreviousHP = -1,
            CurrentAlpha = 0,
            TargetAlpha = 0,
            CurrentScale = 1,
            IsVisible = false,
            IsDiscovered = true, // Default to discovered unless bestiary system says otherwise
            IsScanned = false,
            PlayerHasDamaged = false
        };
    }
    
    /// <summary>
    /// Optional component for per-entity visibility overrides.
    /// When present, this overrides the global config for this specific entity.
    /// </summary>
    public struct HealthBarVisibilityOverride : IComponentData
    {
        /// <summary>Override the primary visibility mode.</summary>
        public HealthBarVisibilityMode OverrideMode;
        
        /// <summary>Force visibility on (ignores all conditions).</summary>
        public bool ForceVisible;
        
        /// <summary>Force visibility off (ignores all conditions).</summary>
        public bool ForceHidden;
        
        /// <summary>Custom timeout override (-1 to use global).</summary>
        public float CustomTimeout;
    }
    
    /// <summary>
    /// Tag component indicating this entity is currently targeted by local player.
    /// Added/removed by the targeting system.
    /// </summary>
    public struct IsTargeted : IComponentData { }
    
    /// <summary>
    /// Tag component indicating mouse/look is hovering over this entity.
    /// Added/removed by the hover detection system.
    /// </summary>
    public struct IsHovered : IComponentData { }
    
    /// <summary>
    /// Component indicating this entity has aggro on a specific player.
    /// Replicated so clients can show "aggroed on me" indicators.
    /// </summary>
    [Unity.NetCode.GhostComponent(PrefabType = Unity.NetCode.GhostPrefabType.All)]
    public struct HasAggroOn : IComponentData
    {
        [Unity.NetCode.GhostField]
        public Entity TargetPlayer;
    }
    
    /// <summary>
    /// Component for entity tier classification (used for filtering).
    /// </summary>
    public struct EntityTierComponent : IComponentData
    {
        public EntityTier Tier;
    }
    
    /// <summary>
    /// Component for named/unique entities.
    /// </summary>
    public struct NamedEntity : IComponentData
    {
        public Unity.Collections.FixedString64Bytes DisplayName;
    }
}
