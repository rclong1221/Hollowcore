using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Systems;
using DIG.Player.Components;

namespace DIG.Player.Systems
{
    /// <summary>
    /// DEBUG SYSTEM: Logs collision detection status to diagnose why knockdown isn't triggering.
    /// Remove this system after debugging is complete.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerCollisionResponseSystem))]
    public partial class CollisionDebugSystem : SystemBase
    {
        private int _frameCounter;
        private bool _initialized;
        private bool _debugEnabled = false;
        
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerCollisionSettings>();
            RequireForUpdate<SimulationSingleton>();
            if (_debugEnabled) UnityEngine.Debug.Log("[CollisionDebug] System created - will log every 60 frames");
        }
        
        protected override void OnUpdate()
        {
            _frameCounter++;
            
            // Log every 60 frames (roughly once per second)
            if (_frameCounter % 60 != 0)
                return;
            
            // Check if settings exist and are enabled
            if (!SystemAPI.TryGetSingleton<PlayerCollisionSettings>(out var settings))
            {
                if (_debugEnabled) UnityEngine.Debug.LogWarning("[CollisionDebug] PlayerCollisionSettings singleton not found!");
                return;
            }
            
            if (!settings.EnableCollisionResponse)
            {
                if (_debugEnabled) UnityEngine.Debug.LogWarning("[CollisionDebug] Collision response is DISABLED in settings!");
                return;
            }
            
            // Count players with PlayerCollisionState
            int playerCount = 0;
            int playersWithSimulate = 0;
            int playersWithKnockdown = 0;
            int playersWithStagger = 0;
            int playersWithRecentCollision = 0;
            int playersOnCooldown = 0;
            float maxCooldown = 0;
            float maxStaggerTime = 0;
            uint currentTick = 0;
            
            if (SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime))
            {
                currentTick = networkTime.ServerTick.TickIndexForValidTick;
            }
            
            foreach (var (collisionState, entity) in SystemAPI.Query<RefRO<PlayerCollisionState>>().WithAll<PlayerTag>().WithEntityAccess())
            {
                playerCount++;
                
                // Check if has Simulate
                if (EntityManager.HasComponent<Simulate>(entity))
                    playersWithSimulate++;
                
                if (collisionState.ValueRO.KnockdownTimeRemaining > 0)
                    playersWithKnockdown++;
                    
                if (collisionState.ValueRO.StaggerTimeRemaining > 0)
                {
                    playersWithStagger++;
                    if (collisionState.ValueRO.StaggerTimeRemaining > maxStaggerTime)
                        maxStaggerTime = collisionState.ValueRO.StaggerTimeRemaining;
                }
                
                if (collisionState.ValueRO.CollisionCooldown > 0)
                {
                    playersOnCooldown++;
                    if (collisionState.ValueRO.CollisionCooldown > maxCooldown)
                        maxCooldown = collisionState.ValueRO.CollisionCooldown;
                }
                
                // Check if collision happened recently (within last 60 ticks ~ 1 second)
                uint ticksSinceCollision = currentTick - collisionState.ValueRO.LastCollisionTick;
                if (ticksSinceCollision < 60 && collisionState.ValueRO.LastCollisionTick > 0)
                    playersWithRecentCollision++;
            }
            
            // Check if physics simulation is running
            var hasSimulation = SystemAPI.TryGetSingleton<SimulationSingleton>(out _);
            var hasPhysicsWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld);
            int numBodies = hasPhysicsWorld ? physicsWorld.PhysicsWorld.NumBodies : 0;
            int numDynamicBodies = hasPhysicsWorld ? physicsWorld.PhysicsWorld.NumDynamicBodies : 0;
            
            if (_debugEnabled) UnityEngine.Debug.Log($"[CollisionDebug] Players={playerCount} (Simulated={playersWithSimulate}), RecentCollisions={playersWithRecentCollision}, " +
                      $"Staggered={playersWithStagger} (max {maxStaggerTime:F2}s), KnockedDown={playersWithKnockdown}, " +
                      $"OnCooldown={playersOnCooldown} (max {maxCooldown:F2}s) | " +
                      $"PhysicsBodies={numBodies}, Dynamic={numDynamicBodies}");
            
            // Log settings on first update
            if (!_initialized)
            {
                _initialized = true;
                if (_debugEnabled) UnityEngine.Debug.Log($"[CollisionDebug] Settings: StaggerThreshold={settings.StaggerThreshold}, " +
                          $"KnockdownPowerThreshold={settings.KnockdownPowerThreshold}, " +
                          $"CombinedPlayerRadius={settings.CombinedPlayerRadius}");
            }
        }
    }
}
