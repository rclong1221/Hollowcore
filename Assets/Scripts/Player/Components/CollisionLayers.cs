using Unity.Physics;
using Unity.Entities;

namespace DIG.Player.Components
{
    /// <summary>
    /// Collision layer constants for Unity Physics CollisionFilter.
    /// Uses bit flags for BelongsTo and CollidesWith masks.
    /// 
    /// Usage in CollisionFilter:
    ///   BelongsTo = CollisionLayers.Player
    ///   CollidesWith = CollisionLayers.Player | CollisionLayers.Environment | CollisionLayers.Hazards
    /// 
    /// Note: These are ECS Physics layers, separate from Unity's GameObject layers.
    /// 
    /// === GroupIndex Usage (Epic 7.6.5) ===
    /// GroupIndex provides advanced filtering beyond layer masks:
    /// 
    /// - Negative GroupIndex: Entities with the same negative GroupIndex NEVER collide with each other.
    ///   Use case: Projectiles ignore their owner (projectile and player share same negative index).
    /// 
    /// - Positive GroupIndex: Entities with the same positive GroupIndex ALWAYS collide with each other.
    ///   Use case: Force collision between entities that would normally be filtered out.
    /// 
    /// - Zero (0): Use normal layer-based filtering (BelongsTo/CollidesWith masks).
    ///   Default behavior - most entities should use GroupIndex = 0.
    /// 
    /// Examples:
    ///   // Make projectile ignore its owner (player entity.Index = 42)
    ///   projectileFilter.GroupIndex = -42;  // Negative of owner's entity index
    ///   playerFilter.GroupIndex = -42;      // Temporarily while firing
    ///   
    ///   // Reset player after short delay (0.1s)
    ///   playerFilter.GroupIndex = 0;        // Normal filtering resumes
    ///   
    ///   // Team-based projectile ignore (all team 1 members ignore each other's projectiles)
    ///   if (teamId.Value == 1)
    ///       projectileFilter.GroupIndex = -1000;  // Shared negative for team 1
    /// </summary>
    public static class CollisionLayers
    {
        // === Layer Definitions (bit positions) ===
        
        /// <summary>Default layer - static geometry, props (bit 0)</summary>
        public const uint Default = 1u << 0;
        
        /// <summary>Player entities (bit 1)</summary>
        public const uint Player = 1u << 1;
        
        /// <summary>Environment - terrain, walls, floors (bit 2)</summary>
        public const uint Environment = 1u << 2;
        
        /// <summary>Hazards - damage zones, traps (bit 3)</summary>
        public const uint Hazards = 1u << 3;
        
        /// <summary>Player projectiles - bullets, thrown items (bit 4)</summary>
        public const uint PlayerProjectile = 1u << 4;
        
        /// <summary>Interactables - doors, buttons, pickups (bit 5)</summary>
        public const uint Interactable = 1u << 5;
        
        /// <summary>Triggers - non-physical detection zones (bit 6)</summary>
        public const uint Trigger = 1u << 6;
        
        /// <summary>Ship - ship hull and interior (bit 7)</summary>
        public const uint Ship = 1u << 7;
        
        /// <summary>Creatures - AI enemies (bit 8)</summary>
        public const uint Creature = 1u << 8;
        
        /// <summary>Climbable surfaces - ladders, pipes, rock walls (bit 9)</summary>
        public const uint Climbable = 1u << 9;
        
        /// <summary>Ragdoll - physics-driven character parts (bit 10)</summary>
        public const uint Ragdoll = 1u << 10;
        
        // === Pre-configured Masks ===
        
        /// <summary>Everything - collides with all layers</summary>
        public const uint Everything = ~0u;
        
        /// <summary>Nothing - collides with no layers</summary>
        public const uint Nothing = 0u;
        
        /// <summary>
        /// What players collide with:
        /// - Other players (for push/stagger)
        /// - Environment (walls, floors)
        /// - Hazards (damage zones)
        /// - Ship (interior/exterior)
        /// - Creatures (enemies)
        /// - Default (props)
        /// 
        /// NOTE: Trigger layer (bit 6) is EXCLUDED to prevent physics simulation contacts.
        /// Even with CollisionResponsePolicy.RaiseTriggerEvents, the solver can still apply
        /// separation forces. Zone detection uses spatial overlap queries instead.
        /// </summary>
        public const uint PlayerCollidesWith = 
            Player | Environment | Hazards | Ship | Creature | Default;
        
        /// <summary>
        /// What environment collides with (static geometry):
        /// - Players, creatures, projectiles, ragdolls
        /// </summary>
        public const uint EnvironmentCollidesWith = 
            Player | Creature | PlayerProjectile | Ragdoll | Default;
        
        /// <summary>
        /// What projectiles collide with:
        /// - Environment, creatures, ship
        /// - NOT the player who fired them (handled by GroupIndex)
        /// </summary>
        public const uint ProjectileCollidesWith = 
            Environment | Creature | Ship | Hazards | Default;
        
        /// <summary>
        /// What creatures collide with:
        /// - Players, environment, ship, hazards, default props
        /// - NOTE: Creature-Creature collision removed (EPIC 15.23) — O(n²) scaling
        ///   was the #1 physics bottleneck. Enemy separation handled by EnemySeparationSystem.
        /// </summary>
        public const uint CreatureCollidesWith =
            Player | Environment | Ship | Hazards | Default;
    }
}
