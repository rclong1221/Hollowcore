using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 14.26: Handles exit conditions for Object Gravity climbing.
    /// 1. Adhesion Loss (Falling)
    /// 2. Walkable Ground (Dismounting)
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FreeClimbMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct FreeClimbExitSystem : ISystem
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
            var deltaTime = SystemAPI.Time.DeltaTime;
            var isServer = state.WorldUnmanaged.IsServer();
            
            new FreeClimbExitJob
            {
                PhysicsWorld = physicsWorld.PhysicsWorld,
                CurrentTime = currentTime,
                DeltaTime = deltaTime,
                IsServer = isServer
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct FreeClimbExitJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public double CurrentTime;
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public bool IsServer;

            private void Execute(Entity entity, ref FreeClimbState climb, ref LocalTransform lt, ref PhysicsVelocity vel, ref PhysicsGravityFactor gravity, RefRO<FreeClimbSettings> settings, RefRO<CharacterControllerSettings> charSettings, RefRO<PlayerInput> input)
            {
                // SAFETY: Detect if climbing was canceled externally (e.g. Stamina) but Gravity is still disabled
                if (!climb.IsClimbing) 
                {
                    if (gravity.Value == 0f)
                    {
                        gravity.Value = 1f;
                         // Also ensure velocity is reset or preserved?
                         // CharacterController will take over, 1f gravity is enough.
                    }
                    return;
                }
                
                // Allow transitions to complete before enforcing exit
                if (climb.IsTransitioning || climb.IsHangTransitioning || climb.IsWallJumping || climb.IsClimbingUp)
                {
                    // EPIC 14.27: Safety Timeout (Panic Fall)
                    // If we get stuck in a transition state (geometry glitch), force exit after timeout
                    float timeout = settings.ValueRO.TransitionTimeout > 0 ? settings.ValueRO.TransitionTimeout : 2.0f;
                    if (CurrentTime - climb.TransitionStartTime > timeout)
                    {
                        ExitClimbing(ref climb, ref lt, ref vel, ref gravity, "Transition Timeout (Stuck)");
                    }
                    return;
                }
                
                var cfg = settings.ValueRO;
                var charCfg = charSettings.ValueRO;

                // 1. ADHESION LOSS CHECK
                // FreeClimbMovementSystem updates IsAdhered based on surface presence
                if (!climb.IsAdhered)
                {
                    // If we lost adhesion, we fall.
                    // But maybe allow a tiny grace period or check if we are just transitioning? 
                    // MovementSystem sets IsAdhered=false only if it truly can't find wall in gravity direction.
                    
                    ExitClimbing(ref climb, ref lt, ref vel, ref gravity, "Adhesion Loss");
                    return;
                }

                // 2. WALKABLE GROUND CHECK (Auto-Dismount)
                // If feet are near walkable ground, and we are moving down or just hanging out near floor.
                
                float characterHeight = charCfg.Height * cfg.ClimbColliderHeightMultiplier;
                float3 footPos = lt.Position; // Buffer is handled by ray length
                float3 worldUp = new float3(0, 1, 0);

                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = cfg.ClimbableLayers,
                    GroupIndex = 0
                };

                // Safety guard for position
                if (!math.all(math.isfinite(lt.Position))) return;

                // Raycast Down from simple offset
                // Origin: Slightly up from pivot to avoid penetrating floor
                float3 rayOrigin = lt.Position + (worldUp * 0.5f); 
                float checkDist = 0.5f + cfg.GroundCheckDistance; // 0.5 from origin to pivot, plus extra

                var groundCollector = new FreeClimbMovementSystem.IgnoreEntityCollector(entity);
                RaycastInput groundInput = new RaycastInput
                {
                    Start = rayOrigin,
                    End = rayOrigin - (worldUp * checkDist),
                    Filter = filter
                };

                PhysicsWorld.CastRay(groundInput, ref groundCollector);

                if (groundCollector.HasHit)
                {
                    var hit = groundCollector.ClosestHit;
                    // Check angle
                    float angleFromUp = math.degrees(math.acos(math.clamp(math.dot(hit.SurfaceNormal, worldUp), -1f, 1f)));
                    
                    bool isWalkable = angleFromUp < cfg.MinSurfaceAngle;
                    
                    float distToFloor = (hit.Fraction * checkDist) - 0.5f; // Subtract origin offset
                    bool feetTouching = distToFloor < 0.25f; // Increased to trigger before obstacle blocking (0.3 - 0.4m)

                    if (isWalkable && feetTouching)
                    {
                        // SMART EXIT: Only auto-dismount if:
                        // 1. Player is intentionally moving DOWN (Vertical < -0.5)
                        // 2. AND we are hitting a surface that is EITHER a different entity 
                        //    OR the same entity but we are clearly at a "floor-like" angle.
                        
                        bool intentToExit = input.ValueRO.Vertical < -0.1f; // Loosen slightly for better feel
                        bool isDifferentEntity = hit.Entity != climb.SurfaceEntity;
                        bool isGroundAngle = angleFromUp < 30f; // Clearly a floor
                        
                        if (intentToExit && (isDifferentEntity || isGroundAngle))
                        {
                             lt.Position = hit.Position; 
                             ExitClimbing(ref climb, ref lt, ref vel, ref gravity, "Walkable Ground (Smart Exit)");
                        }
                    }
                }
            }

            private void ExitClimbing(ref FreeClimbState climb, ref LocalTransform lt, ref PhysicsVelocity vel, ref PhysicsGravityFactor gravity, string reason)
            {
                // UnityEngine.Debug.Log($"[CLIMB] Exiting Climb: {reason}");
                
                climb.IsClimbing = false;
                climb.IsAdhered = false;
                climb.IsFreeHanging = false;
                climb.IsTransitioning = false;
                climb.IsClimbingUp = false;      // FAILSAFE: Explicit clear
                climb.IsWallJumping = false;     // FAILSAFE: Explicit clear
                climb.IsHangTransitioning = false; // FAILSAFE: Explicit clear
                climb.SurfaceEntity = Entity.Null;
                
                climb.LastDismountTime = CurrentTime;

                // Reset velocities
                vel.Linear = float3.zero;
                vel.Angular = float3.zero;

                // Restore Gravity
                gravity.Value = 1f;

                // Snap to upright to prevent "lying flat" look
                float3 flatForward = math.mul(lt.Rotation, new float3(0, 0, 1));
                flatForward.y = 0;
                if (math.lengthsq(flatForward) < 0.001f) flatForward = new float3(0, 0, 1);
                lt.Rotation = quaternion.LookRotation(math.normalize(flatForward), new float3(0, 1, 0));
            }
        }
    }
}
