using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Player.Components
{
    /// <summary>
    /// Collision event data stored in buffer for consumption by audio/VFX systems.
    /// Limited to 8 events per player per frame to prevent unbounded growth.
    /// Extended for 7.3.6 directional bonuses and 7.3.5 impact metrics.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct CollisionEvent : IBufferElementData
    {
        /// <summary>
        /// Entity that was collided with.
        /// </summary>
        public Entity OtherEntity;
        
        /// <summary>
        /// World-space position of the collision contact point.
        /// </summary>
        public float3 ContactPoint;
        
        /// <summary>
        /// Contact normal (points away from this entity towards the other entity).
        /// </summary>
        public float3 ContactNormal;
        
        /// <summary>
        /// Magnitude of the collision impact force (for audio volume scaling).
        /// Calculated as ImpactSpeed * CombinedMass.
        /// </summary>
        public float ImpactForce;
        
        /// <summary>
        /// Relative approach speed at collision (m/s).
        /// Used for audio pitch and VFX intensity scaling.
        /// </summary>
        public float ImpactSpeed;
        
        /// <summary>
        /// Network tick when collision occurred (for temporal ordering).
        /// </summary>
        public uint EventTick;
        
        /// <summary>
        /// Hit direction type for this entity (see HitDirectionType constants).
        /// 0 = braced (facing collision), 1 = side, 2 = back (vulnerable), 3 = evaded (dodge).
        /// Used for differentiated audio/VFX feedback.
        /// </summary>
        public byte HitDirection;
        
        /// <summary>
        /// Power ratio for this entity in the collision (0-1).
        /// >0.5 = we had more power (winner), &lt;0.5 = they had more power (loser).
        /// Used for determining grunt intensity.
        /// </summary>
        public float PowerRatio;
        
        /// <summary>
        /// True if this collision triggered a stagger state.
        /// </summary>
        public bool TriggeredStagger;
        
        /// <summary>
        /// True if this collision triggered a knockdown state.
        /// </summary>
        public bool TriggeredKnockdown;
    }
}
