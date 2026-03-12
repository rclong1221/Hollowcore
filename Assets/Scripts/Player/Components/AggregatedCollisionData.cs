using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace DIG.Player.Components
{
    /// <summary>
    /// Aggregated collision data for a single entity.
    /// Combines all collisions involving this entity into a single response.
    /// Epic 7.3.3: Aggregation phase identifies dominant collision per player.
    /// </summary>
    public struct AggregatedCollisionData
    {
        /// <summary>Entity this aggregation is for.</summary>
        public Entity Entity;
        
        /// <summary>Number of collisions this entity was involved in.</summary>
        public int CollisionCount;
        
        // === Dominant Collision (highest impact) ===
        
        /// <summary>Entity from the dominant collision (highest impact * power ratio).</summary>
        public Entity DominantOther;
        
        /// <summary>Impact speed of the dominant collision.</summary>
        public float DominantImpactSpeed;
        
        /// <summary>Impact force of the dominant collision.</summary>
        public float DominantImpactForce;
        
        /// <summary>Power ratio for this entity in the dominant collision.</summary>
        public float DominantPowerRatio;
        
        /// <summary>Hit direction for the dominant collision.</summary>
        public byte DominantHitDirection;
        
        /// <summary>Directional multiplier for the dominant collision.</summary>
        public float DominantDirectionalMultiplier;
        
        /// <summary>Contact point of the dominant collision.</summary>
        public float3 DominantContactPoint;
        
        /// <summary>Contact normal of the dominant collision.</summary>
        public float3 DominantContactNormal;
        
        /// <summary>Knockback direction from dominant collision.</summary>
        public float3 KnockbackDirection;
        
        /// <summary>Whether the dominant collision triggers stagger.</summary>
        public bool TriggerStagger;
        
        /// <summary>Whether the dominant collision triggers knockdown.</summary>
        public bool TriggerKnockdown;
        
        // === Cumulative Separation (from all collisions) ===
        
        /// <summary>Cumulative push direction (normalized after aggregation).</summary>
        public float3 CumulativePushDirection;
        
        /// <summary>Cumulative overlap to resolve (max of all overlaps).</summary>
        public float MaxOverlap;
        
        /// <summary>Total push impulse magnitude from all collisions.</summary>
        public float TotalPushImpulse;
    }
    
    /// <summary>
    /// Buffer element for storing all collision pairs an entity is involved in.
    /// Used during aggregation to track per-entity collisions.
    /// </summary>
    public struct CollisionPairIndex : IBufferElementData
    {
        /// <summary>Index into the collision pairs array.</summary>
        public int Index;
        
        /// <summary>True if this entity is EntityA in the pair, false if EntityB.</summary>
        public bool IsEntityA;
    }
}
