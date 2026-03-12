#pragma warning disable CS0162 // Unreachable code detected - intentional debug toggle
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Player.Components;

namespace DIG.Player.Systems
{
    /// <summary>
    /// System that tracks collision state for relevancy-based bandwidth optimization.
    /// Counts active collision players and updates CollisionNetworkStats singleton.
    /// 
    /// Epic 7.7.8: Delta Compression & State Sync
    /// - Tracks which players have active collision state (stagger/knockdown)
    /// - Updates CollisionNetworkStats for bandwidth monitoring
    /// - Enables variable update rate prioritization (high priority for active, low for idle)
    /// 
    /// Note: Actual relevancy filtering is handled by NetCode's GhostImportance system.
    /// This system provides metrics and marks entities for priority consideration.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerProximityCollisionSystem))]
    public partial struct CollisionRelevancySystem : ISystem
    {
        private bool _initialized;
        private Entity _statsEntity;
        
        // Logging throttle
        private int _framesSinceLastLog;
        private const int LogIntervalFrames = 300; // Log every 5 seconds at 60 FPS
        // Toggle debug logging for this system. Set to true to enable logs.
        private const bool DebugEnabled = false;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _initialized = false;
            _framesSinceLastLog = 0;
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Stats entity will be cleaned up with the world
        }
        
        // Note: OnUpdate is NOT Burst-compiled because debug logging uses managed strings.
        // The main loop is still efficient as it's mostly SystemAPI queries.
        public void OnUpdate(ref SystemState state)
        {
            // Initialize singleton on first update
            if (!_initialized)
            {
                InitializeStatsSingleton(ref state);
                _initialized = true;
            }
            
            // Get or create stats singleton
            CollisionNetworkStats stats;
            if (SystemAPI.HasSingleton<CollisionNetworkStats>())
            {
                stats = SystemAPI.GetSingleton<CollisionNetworkStats>();
            }
            else
            {
                stats = CollisionNetworkStats.CreateDefault();
            }
            
            // Reset frame counters
            stats.ActiveCollisionPlayers = 0;
            stats.HighPriorityPlayers = 0;
            stats.LowPriorityPlayers = 0;
            stats.TotalReplicatedPlayers = 0;
            
            // Count players with active collision state
            foreach (var (collisionState, entity) in 
                SystemAPI.Query<RefRO<PlayerCollisionState>>()
                    .WithEntityAccess())
            {
                stats.TotalReplicatedPlayers++;
                
                var state_ro = collisionState.ValueRO;
                
                // Check if player has active collision state
                bool isActive = state_ro.IsStaggered || 
                               state_ro.IsKnockedDown || 
                               state_ro.CollisionCooldown > 0 ||
                               math.lengthsq(state_ro.StaggerVelocity) > 0.01f;
                
                if (isActive)
                {
                    stats.ActiveCollisionPlayers++;
                    stats.HighPriorityPlayers++;
                }
                else
                {
                    stats.LowPriorityPlayers++;
                }
            }
            
            // Update running averages
            stats.UpdateAverages(0.1f);
            
            // Write back singleton
            if (SystemAPI.HasSingleton<CollisionNetworkStats>())
            {
                SystemAPI.SetSingleton(stats);
            }
            
            // Periodic debug logging
            _framesSinceLastLog++;
            if (_framesSinceLastLog >= LogIntervalFrames && stats.TotalReplicatedPlayers > 0)
            {
                _framesSinceLastLog = 0;
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DebugEnabled)
                {
                    UnityEngine.Debug.Log(stats.ToDebugString());
                }
                #endif
            }
        }
        
        private void InitializeStatsSingleton(ref SystemState state)
        {
            // Check if singleton already exists
            if (SystemAPI.HasSingleton<CollisionNetworkStats>())
            {
                return;
            }
            
            // Create singleton entity
            _statsEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(_statsEntity, CollisionNetworkStats.CreateDefault());
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            state.EntityManager.SetName(_statsEntity, "CollisionNetworkStats");
            #endif
        }
    }
    
    /// <summary>
    /// Component added to entities that have high-priority collision state.
    /// Used as a hint for relevancy systems and debugging.
    /// </summary>
    public struct CollisionHighPriority : IComponentData
    {
        /// <summary>
        /// Reason for high priority status.
        /// </summary>
        public CollisionPriorityReason Reason;
        
        /// <summary>
        /// Frame count when priority was assigned.
        /// </summary>
        public uint AssignedFrame;
    }
    
    /// <summary>
    /// Reason why an entity has high collision priority.
    /// </summary>
    public enum CollisionPriorityReason : byte
    {
        /// <summary>Player is currently staggered.</summary>
        Staggered = 0,
        
        /// <summary>Player is knocked down.</summary>
        KnockedDown = 1,
        
        /// <summary>Player has active stagger velocity.</summary>
        ActiveVelocity = 2,
        
        /// <summary>Player has collision cooldown active.</summary>
        Cooldown = 3
    }
}
