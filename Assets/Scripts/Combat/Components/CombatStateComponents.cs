using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Components
{
    /// <summary>
    /// Tracks persistent combat state for an entity.
    /// Used to determine if entity is "in combat" for systems like:
    /// - Health regeneration blocking
    /// - Battle music triggers
    /// - AI alert posture
    /// - Health bar visibility modes
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CombatState : IComponentData
    {
        /// <summary>
        /// Whether the entity is currently in combat.
        /// </summary>
        [GhostField]
        public bool IsInCombat;
        
        /// <summary>
        /// Time elapsed since the last combat action (hit dealt or received).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TimeSinceLastCombatAction;
        
        /// <summary>
        /// How long without combat action before dropping out of combat (seconds).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CombatDropTime;
        
        /// <summary>
        /// Time when combat was exited (for "time since combat ended" queries).
        /// Set to -infinity when in combat.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CombatExitTime;
        
        /// <summary>
        /// Creates default combat state.
        /// </summary>
        public static CombatState Default => new CombatState
        {
            IsInCombat = false,
            TimeSinceLastCombatAction = 999f,
            CombatDropTime = 5f,
            CombatExitTime = -999f
        };
    }
    
    /// <summary>
    /// Tag added to entities when they enter combat.
    /// Useful for reactive systems.
    /// </summary>
    public struct EnteredCombatTag : IComponentData, IEnableableComponent { }
    
    /// <summary>
    /// Tag added to entities when they exit combat.
    /// Useful for reactive systems.
    /// </summary>
    public struct ExitedCombatTag : IComponentData, IEnableableComponent { }
    
    /// <summary>
    /// Optional settings component for per-entity combat state configuration.
    /// If not present, defaults are used.
    /// </summary>
    public struct CombatStateSettings : IComponentData
    {
        /// <summary>
        /// How long without combat before exiting combat state.
        /// </summary>
        public float CombatDropTime;
        
        /// <summary>
        /// Whether this entity can enter combat (some NPCs may be non-combatants).
        /// </summary>
        public bool CanEnterCombat;
        
        public static CombatStateSettings Default => new CombatStateSettings
        {
            CombatDropTime = 5f,
            CanEnterCombat = true
        };
    }
}
