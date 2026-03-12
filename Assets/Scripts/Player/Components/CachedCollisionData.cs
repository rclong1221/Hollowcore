using Unity.Mathematics;

namespace DIG.Player.Components
{
    /// <summary>
    /// Epic 7.7.6: Temporal Coherence - Cached collision data for frame-to-frame reuse.
    /// 
    /// When players collide, we cache the computed collision data. If velocity
    /// changes are small between frames, we reuse cached values instead of
    /// recalculating distance, direction, and power ratios.
    /// 
    /// This optimization targets stable contacts where players are:
    /// - Standing still in contact
    /// - Walking slowly against each other
    /// - Pushing steadily (not sudden impacts)
    /// 
    /// Memory layout: 40 bytes (fits in single cache line with key)
    /// </summary>
    public struct CachedCollisionData
    {
        /// <summary>
        /// Relative velocity at time of caching: velocityA - velocityB.
        /// Used to detect if recalculation is needed (delta > threshold).
        /// </summary>
        public float3 LastRelativeVelocity;
        
        /// <summary>
        /// Cached horizontal distance between players.
        /// Reused when velocity hasn't changed significantly.
        /// </summary>
        public float LastDistance;
        
        /// <summary>
        /// Cached normalized push direction (A → B, Y=0).
        /// Reused for force application when velocities stable.
        /// </summary>
        public float3 LastDirection;
        
        /// <summary>
        /// Cached contact point (midpoint between players).
        /// Used for audio/VFX positioning.
        /// </summary>
        public float3 LastContactPoint;
        
        /// <summary>
        /// Cached approach speed (relative velocity along direction).
        /// </summary>
        public float LastApproachSpeed;
        
        /// <summary>
        /// Number of frames since this cache entry was last updated.
        /// Entries with FramesSinceUpdate > MaxStaleFrames are evicted.
        /// Reset to 0 when collision is detected and cache is refreshed.
        /// </summary>
        public byte FramesSinceUpdate;
        
        /// <summary>
        /// Whether this cache entry was used (hit) this frame.
        /// Used for cache hit rate statistics.
        /// </summary>
        public bool WasUsedThisFrame;
        
        /// <summary>
        /// Maximum staleness before eviction (frames without collision).
        /// </summary>
        public const byte MaxStaleFrames = 5;
        
        /// <summary>
        /// Velocity change threshold for cache invalidation (m/s).
        /// If relative velocity changes more than this, recalculate.
        /// </summary>
        public const float VelocityDeltaThreshold = 0.5f;
        
        /// <summary>
        /// Position change threshold for cache invalidation (m).
        /// If distance changes more than this, recalculate.
        /// </summary>
        public const float DistanceDeltaThreshold = 0.1f;
        
        /// <summary>
        /// Create a new cache entry from current collision data.
        /// </summary>
        public static CachedCollisionData Create(
            float3 relativeVelocity,
            float distance,
            float3 direction,
            float3 contactPoint,
            float approachSpeed)
        {
            return new CachedCollisionData
            {
                LastRelativeVelocity = relativeVelocity,
                LastDistance = distance,
                LastDirection = direction,
                LastContactPoint = contactPoint,
                LastApproachSpeed = approachSpeed,
                FramesSinceUpdate = 0,
                WasUsedThisFrame = true
            };
        }
        
        /// <summary>
        /// Update cache with new collision data.
        /// </summary>
        public void Update(
            float3 relativeVelocity,
            float distance,
            float3 direction,
            float3 contactPoint,
            float approachSpeed)
        {
            LastRelativeVelocity = relativeVelocity;
            LastDistance = distance;
            LastDirection = direction;
            LastContactPoint = contactPoint;
            LastApproachSpeed = approachSpeed;
            FramesSinceUpdate = 0;
            WasUsedThisFrame = true;
        }
        
        /// <summary>
        /// Check if cache is still valid given current velocities.
        /// Returns true if cached data can be reused.
        /// </summary>
        public bool IsValidFor(float3 currentRelativeVelocity, float currentDistance)
        {
            // Check velocity change
            float3 velocityDelta = currentRelativeVelocity - LastRelativeVelocity;
            float velocityChangeSq = math.lengthsq(velocityDelta);
            
            if (velocityChangeSq > VelocityDeltaThreshold * VelocityDeltaThreshold)
                return false;
            
            // Check distance change
            float distanceDelta = math.abs(currentDistance - LastDistance);
            if (distanceDelta > DistanceDeltaThreshold)
                return false;
            
            // Check staleness
            if (FramesSinceUpdate >= MaxStaleFrames)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Mark as used this frame (for hit rate tracking).
        /// </summary>
        public void MarkUsed()
        {
            WasUsedThisFrame = true;
        }
        
        /// <summary>
        /// Increment staleness counter. Called at start of frame for all entries.
        /// </summary>
        public void IncrementStaleness()
        {
            if (FramesSinceUpdate < byte.MaxValue)
                FramesSinceUpdate++;
            WasUsedThisFrame = false;
        }
        
        /// <summary>
        /// Check if this entry should be evicted (too stale).
        /// </summary>
        public bool ShouldEvict => FramesSinceUpdate > MaxStaleFrames;
    }
}
