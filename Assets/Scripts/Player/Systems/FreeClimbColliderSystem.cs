using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Adjusts the capsule collider size during climbing to prevent wall rejection.
    /// 
    /// When climbing:
    /// - Stores original CharacterControllerSettings.Radius and Height
    /// - Reduces dimensions by ClimbColliderRadiusMultiplier/HeightMultiplier
    /// 
    /// When dismounting:
    /// - Restores original dimensions
    /// 
    /// This matches Invector's approach of using reduced capsule radius (0.5x) during climb.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FreeClimbMountSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct FreeClimbColliderSystem : ISystem
    {
        public static bool EnableDebugLog = false;
        
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (climbState, settings, ccSettings, entity) in 
                SystemAPI.Query<RefRW<FreeClimbState>, RefRO<FreeClimbSettings>, RefRW<CharacterControllerSettings>>()
                    .WithAll<PlayerTag, Simulate>()
                    .WithEntityAccess())
            {
                ref var climb = ref climbState.ValueRW;
                ref var cc = ref ccSettings.ValueRW;
                var cfg = settings.ValueRO;
                
                // Case 1: Start climbing - need to reduce collider
                if (climb.IsClimbing && !climb.ColliderAdjusted)
                {
                    // Store original dimensions
                    climb.OriginalRadius = cc.Radius;
                    climb.OriginalHeight = cc.Height;
                    
                    // Apply reduced dimensions
                    cc.Radius = climb.OriginalRadius * cfg.ClimbColliderRadiusMultiplier;
                    cc.Height = climb.OriginalHeight * cfg.ClimbColliderHeightMultiplier;
                    
                    climb.ColliderAdjusted = true;
                    
                    if (EnableDebugLog)
                    {
                        UnityEngine.Debug.Log($"[FreeClimbCollider] Reduced collider: " +
                            $"Radius {climb.OriginalRadius:F2} -> {cc.Radius:F2}, " +
                            $"Height {climb.OriginalHeight:F2} -> {cc.Height:F2}");
                    }
                }
                // Case 2: Stop climbing - restore collider (but wait for transitions to complete)
                else if (!climb.IsClimbing && !climb.IsTransitioning && climb.ColliderAdjusted)
                {
                    // Restore original dimensions
                    cc.Radius = climb.OriginalRadius;
                    cc.Height = climb.OriginalHeight;
                    
                    climb.ColliderAdjusted = false;
                    
                    if (EnableDebugLog)
                    {
                        UnityEngine.Debug.Log($"[FreeClimbCollider] Restored collider: " +
                            $"Radius {cc.Radius:F2}, Height {cc.Height:F2}");
                    }
                }
            }
        }
    }
}
