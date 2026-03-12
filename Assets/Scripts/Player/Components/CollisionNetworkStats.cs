using Unity.Entities;

namespace DIG.Player.Components
{
    /// <summary>
    /// Singleton component for tracking collision-related network bandwidth usage.
    /// Used for profiling and debugging network performance.
    /// 
    /// Epic 7.7.8: Delta Compression & State Sync
    /// - Tracks bandwidth metrics for collision state replication
    /// - Helps identify bandwidth hotspots and optimization opportunities
    /// - Updated each frame by CollisionNetworkStatsSystem
    /// </summary>
    public struct CollisionNetworkStats : IComponentData
    {
        // === Current Frame Metrics ===
        
        /// <summary>
        /// Number of players with active collision state this frame.
        /// Active = IsStaggered || IsKnockedDown || CollisionCooldown > 0
        /// </summary>
        public int ActiveCollisionPlayers;
        
        /// <summary>
        /// Number of players replicated with high priority this frame.
        /// High priority = active collision state requiring frequent updates.
        /// </summary>
        public int HighPriorityPlayers;
        
        /// <summary>
        /// Number of players replicated with low priority this frame.
        /// Low priority = idle players with no active collision state.
        /// </summary>
        public int LowPriorityPlayers;
        
        /// <summary>
        /// Total players being replicated this frame.
        /// </summary>
        public int TotalReplicatedPlayers;
        
        // === Running Averages ===
        
        /// <summary>
        /// Running average of active collision players (smoothed over time).
        /// </summary>
        public float AverageActiveCollisionPlayers;
        
        /// <summary>
        /// Running average of high priority players (smoothed over time).
        /// </summary>
        public float AverageHighPriorityPlayers;
        
        /// <summary>
        /// Estimated bytes per second for collision state replication.
        /// Calculated: (ActiveCollisionPlayers * BytesPerActivePlayer + IdlePlayers * BytesPerIdlePlayer) * UpdateRate
        /// </summary>
        public float EstimatedBandwidthBytesPerSecond;
        
        // === Constants for Bandwidth Estimation ===
        
        /// <summary>
        /// Estimated bytes per player with active collision state per snapshot.
        /// Includes: StaggerVelocity (12B quantized), StaggerTimeRemaining (2B), KnockdownTimeRemaining (2B),
        /// StaggerIntensity (2B), KnockdownImpactSpeed (2B), LastPowerRatio (2B), LastHitDirection (1B),
        /// CollisionCooldown (2B), IsRecoveringFromKnockdown (1B) + NetCode overhead (~8B)
        /// </summary>
        public const int BytesPerActivePlayer = 34;
        
        /// <summary>
        /// Estimated bytes per idle player per snapshot.
        /// Minimal state: just delta-compressed zeros + NetCode overhead (~4B).
        /// SendTypeOptimization reduces this further for non-predicted clients.
        /// </summary>
        public const int BytesPerIdlePlayer = 4;
        
        /// <summary>
        /// High priority update rate (Hz). Active collision players update at this rate.
        /// </summary>
        public const int HighPriorityUpdateRate = 60;
        
        /// <summary>
        /// Low priority update rate (Hz). Idle players update at this rate.
        /// </summary>
        public const int LowPriorityUpdateRate = 10;
        
        // === Helper Methods ===
        
        /// <summary>
        /// Creates default stats with zeroed values.
        /// </summary>
        public static CollisionNetworkStats CreateDefault()
        {
            return new CollisionNetworkStats
            {
                ActiveCollisionPlayers = 0,
                HighPriorityPlayers = 0,
                LowPriorityPlayers = 0,
                TotalReplicatedPlayers = 0,
                AverageActiveCollisionPlayers = 0f,
                AverageHighPriorityPlayers = 0f,
                EstimatedBandwidthBytesPerSecond = 0f
            };
        }
        
        /// <summary>
        /// Updates running averages with exponential smoothing.
        /// </summary>
        /// <param name="smoothingFactor">Smoothing factor (0-1). Lower = smoother, higher = more responsive.</param>
        public void UpdateAverages(float smoothingFactor = 0.1f)
        {
            AverageActiveCollisionPlayers = AverageActiveCollisionPlayers * (1f - smoothingFactor) 
                + ActiveCollisionPlayers * smoothingFactor;
            AverageHighPriorityPlayers = AverageHighPriorityPlayers * (1f - smoothingFactor) 
                + HighPriorityPlayers * smoothingFactor;
            
            // Estimate bandwidth: high priority at 60Hz, low priority at 10Hz
            float highPriorityBandwidth = HighPriorityPlayers * BytesPerActivePlayer * HighPriorityUpdateRate;
            float lowPriorityBandwidth = LowPriorityPlayers * BytesPerIdlePlayer * LowPriorityUpdateRate;
            EstimatedBandwidthBytesPerSecond = highPriorityBandwidth + lowPriorityBandwidth;
        }
        
        /// <summary>
        /// Returns formatted debug string with current stats.
        /// </summary>
        public string ToDebugString()
        {
            float bandwidthKbps = EstimatedBandwidthBytesPerSecond * 8f / 1000f;
            return $"[CollisionNet] Active:{ActiveCollisionPlayers} High:{HighPriorityPlayers} " +
                   $"Low:{LowPriorityPlayers} Total:{TotalReplicatedPlayers} " +
                   $"BW:{bandwidthKbps:F1}Kbps";
        }
    }
}
