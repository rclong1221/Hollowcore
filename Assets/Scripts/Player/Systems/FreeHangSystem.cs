using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using UnityEngine;
using Player.Components;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 14.26: Edge & Hang Detection for Object Gravity Climbing
    /// 
    /// Responsibilities:
    /// 1. Detect Ledge Top (No wall above)
    /// 2. Detect Ledge Bottom (No wall below)
    /// 3. Manage Hang State logic (IsFreeHanging)
    /// 
    /// This system analyzes the surface geometry relative to adhesion to determine
    /// if the player is at an edge, which informs Vault/Mount systems.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FreeClimbMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct FreeHangSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var currentTime = SystemAPI.Time.ElapsedTime;
            
            new EdgeDetectionJob
            {
                PhysicsWorld = physicsWorld.PhysicsWorld,
                CurrentTime = currentTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct EdgeDetectionJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public double CurrentTime;

            private void Execute(Entity entity, ref FreeClimbState climb, ref LocalTransform lt, RefRO<FreeClimbSettings> settings)
            {
                if (!climb.IsClimbing || !climb.IsAdhered)
                {
                    // Clear edge states if not climbing/adhered
                    if (climb.AtLedgeTop) climb.AtLedgeTop = false;
                    if (climb.IsFreeHanging && !climb.IsHangTransitioning) 
                    {
                        // If we lost adhesion, we aren't hanging anymore (handled by ExitSystem, but precise cleanup here)
                        // climb.IsFreeHanging = false; 
                    }
                    return;
                }

                // Skip checks during transitions
                if (climb.IsTransitioning || climb.IsHangTransitioning || climb.IsWallJumping || climb.IsClimbingUp)
                    return;

                var cfg = settings.ValueRO;
                float3 surfaceNormal = climb.SurfaceNormal;
                float3 up = new float3(0, 1, 0);

                // Use SurfaceContactPoint as reliable anchor on wall
                float3 anchorPos = climb.SurfaceContactPoint;

                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = cfg.ClimbableLayers,
                    GroupIndex = 0
                };

                // 1. DETECT LEDGE TOP
                // Check if wall exists above current hand height
                // Origin: Anchor + Up * CheckHeight + Normal * Offset (to avoid grazing)
                // Safety: Validate SurfaceNormal to prevent NaN crashes
                bool isNormalValid = math.all(math.isfinite(surfaceNormal)) && math.lengthsq(surfaceNormal) > 0.001f;
                if (!isNormalValid)
                {
                    // Fallback to character forward or default
                     float3 forward = math.forward(lt.Rotation);
                     if (!math.all(math.isfinite(forward)) || math.lengthsq(forward) < 0.001f) forward = new float3(0, 0, 1);
                     surfaceNormal = -forward;
                }
                
                // Safety: Validate Position
                if (!math.all(math.isfinite(anchorPos))) return;

                float checkHeight = cfg.LedgeCheckHeight; 
                float3 topCheckOrigin = anchorPos + (up * checkHeight) + (surfaceNormal * 0.1f);
                float3 checkDir = -surfaceNormal;
                
                bool wallAbove = PhysicsWorld.CastRay(new RaycastInput
                {
                    Start = topCheckOrigin,
                    End = topCheckOrigin + (checkDir * 1.0f), // Cast into wall
                    Filter = filter
                }, out var topHit);

                climb.AtLedgeTop = !wallAbove;

                // 2. DETECT FOOT SUPPORT (For Hang State)
                // Check if wall exists below at foot level
                // Origin: Anchor - Roughly capsule height + Normal * Offset
                float3 footCheckOrigin = anchorPos - (up * 1.5f) + (surfaceNormal * 0.1f);
                
                bool wallBelow = PhysicsWorld.CastRay(new RaycastInput
                {
                    Start = footCheckOrigin,
                    End = footCheckOrigin + (checkDir * 1.0f),
                    Filter = filter
                }, out var footHit);
                
                // Also check if feet are on a ledge (floor below feet)
                // This covers standing on a small outcropping while holding wall
                bool floorBelow = false;
                if (!wallBelow)
                {
                     // Cast DOWN from foot position
                     floorBelow = PhysicsWorld.CastRay(new RaycastInput
                     {
                         Start = footCheckOrigin,
                         End = footCheckOrigin - (up * 0.5f),
                         Filter = filter
                     }, out var floorHit);
                }

                bool feetSupported = wallBelow || floorBelow;

                // 3. DETERMINE HANG STATE
                // Hang if:
                // A. We are at Ledge Top (often feet dangle) OR
                // B. We explicitly have NO foot support
                // C. AND we found a valid wall for hands (implied by IsAdhered)
                
                bool shouldHang = !feetSupported || climb.AtLedgeTop;

                // Simple Hysteresis / Transition Logic
                // (Existing complex logic can be preserved or simplified here)
                
                if (shouldHang && !climb.IsFreeHanging)
                {
                    // Start Entry
                     if (climb.FreeHangEntryRequestTime <= 0.0)
                    {
                        climb.FreeHangEntryRequestTime = CurrentTime;
                    }
                    else if (CurrentTime - climb.FreeHangEntryRequestTime > 0.1f) // 100ms stability
                    {
                         // Enter Hang
                         climb.IsHangTransitioning = true;
                         climb.HangTransitionStartTime = CurrentTime;
                         climb.TransitionStartTime = CurrentTime; // EPIC 14.27: Unify for safety watchdog
                         climb.FreeHangEntryRequestTime = 0.0;
                         // Ideally play animation here via event, but we set flag to trigger anim system
                    }
                }
                else if (!shouldHang && climb.IsFreeHanging)
                {
                    // Start Exit
                     if (climb.FreeHangExitRequestTime <= 0.0)
                    {
                        climb.FreeHangExitRequestTime = CurrentTime;
                    }
                    else if (CurrentTime - climb.FreeHangExitRequestTime > 0.2f)
                    {
                        climb.IsFreeHanging = false;
                        climb.FreeHangExitRequestTime = 0.0;
                    }
                }
                else
                {
                    // Reset timers if condition flickers
                    climb.FreeHangEntryRequestTime = 0.0;
                    climb.FreeHangExitRequestTime = 0.0;
                }
            }
        }
    }
}
