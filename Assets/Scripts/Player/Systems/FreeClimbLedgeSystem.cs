using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;
using DIG.Player.Components;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Player.Systems 
{
    /// <summary>
    /// EPIC 14.26: Object Gravity Vault System
    /// 
    /// Responsibilities:
    /// 1. Respond to "Climb Up" intent (Input Up at Ledge Top)
    /// 2. Verify landing space (CanClimbUp)
    /// 3. Trigger Vault Transition (IsClimbingUp = true)
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FreeClimbMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct FreeClimbLedgeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var parentLookup = SystemAPI.GetComponentLookup<Parent>(isReadOnly: true);
            var currentTime = SystemAPI.Time.ElapsedTime;
            var isServer = state.WorldUnmanaged.IsServer();
            
            new FreeClimbLedgeJob
            {
                PhysicsWorld = physicsWorld.PhysicsWorld,
                ParentLookup = parentLookup,
                CurrentTime = currentTime,
                IsServer = isServer
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct FreeClimbLedgeJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public double CurrentTime;
            [ReadOnly] public bool IsServer;
            
            // Re-using same constants for consistency
            private const float HAND_HEIGHT_OFFSET = 1.4f;
            
            private void Execute(
                Entity entity, 
                ref FreeClimbState climb, 
                ref LocalTransform lt, 
                ref PhysicsVelocity vel,
                RefRO<FreeClimbSettings> settings, 
                RefRO<PlayerInput> input,
                RefRO<CharacterControllerSettings> charSettings)
            {
                // Only vault if climbing or hanging
                if (!climb.IsClimbing) return;
                
                // Block if already vaulting or mid-transition
                if (climb.IsClimbingUp || climb.IsTransitioning || climb.IsHangTransitioning) return;
                
                // TRIGGER CONDITION:
                // 1. Must be at Ledge Top (Detected by HangDetectionSystem)
                // 2. Input must be positive vertical (Pushing UP)
                // 3. OR jump key pressed while at top? (Let's stick to Up/Jump)
                
                bool intentToVault = (climb.AtLedgeTop && input.ValueRO.Vertical > 0.5f) || 
                                     (input.ValueRO.Jump.IsSet && climb.AtLedgeTop);

                if (!intentToVault) return;

                // VALIDATE LANDING
                var cfg = settings.ValueRO;
                float3 surfaceNormal = climb.SurfaceNormal;
                float3 up = new float3(0, 1, 0);
                
                // Where are we trying to go?
                // Anchor is ContactPoint.
                // Target is: Top of ledge + Inward + Up
                
                float3 anchorPos = climb.SurfaceContactPoint;
                
                // Safety: Guard against NaN
                if (!math.all(math.isfinite(anchorPos)) || !math.all(math.isfinite(lt.Position))) return;
                if (!math.all(math.isfinite(surfaceNormal))) surfaceNormal = new float3(0,0,1);
                
                // Scan to find the actual ledge surface height
                float3 scanOrigin = anchorPos + (up * (cfg.LedgeCheckHeight + 0.5f)) + (surfaceNormal * 0.2f); // Above and pulled away slightly? No, push IN.
                // Actually: Wall is at anchor. Ledge top is somewhere above.
                // We know wall ENDS at LedgeCheckHeight.
                // Raycast DOWN from above to find the floor.
                
                float3 landOrigin = anchorPos + (up * 2.5f) - (surfaceNormal * 0.5f); // High up and INTO the wall
                float3 checkDir = -up;
                
                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = cfg.LedgeTopLayers,
                    GroupIndex = 0
                };
                
                if (PhysicsWorld.CastRay(new RaycastInput { Start = landOrigin, End = landOrigin + (checkDir * 3.0f), Filter = filter }, out var hit))
                {
                    // Found a floor!
                    // Check if it's walkable
                    float angle = math.degrees(math.acos(math.clamp(math.dot(hit.SurfaceNormal, up), -1f, 1f)));
                    if (angle < cfg.MinSurfaceAngle)
                    {
                        // VALID VAULT!
                        StartClimbUp(ref climb, ref lt, ref vel, hit.Position, hit.SurfaceNormal, CurrentTime, cfg, up, anchorPos);
                    }
                }
            }
            
            private void StartClimbUp(
                ref FreeClimbState climb, 
                ref LocalTransform lt,
                ref PhysicsVelocity vel,
                float3 targetStandingPos,
                float3 targetNormal,
                double time,
                FreeClimbSettings cfg,
                float3 worldUp,
                float3 gripPos)
            {
                // Set state
                climb.IsClimbingUp = true;
                climb.IsTransitioning = true;
                climb.TransitionStartTime = time;
                climb.TransitionProgress = 0f;
                
                // Align rotation to face the ledge (standardize)
                // If we are hanging, we are already facing it.
                // Just ensuring consistency.
                
                // Calculate Start/End for animation system to use (or simple lerp if no Anim system)
                climb.TransitionStartPos = lt.Position;
                // Safe Normalize for Rotation
                quaternion startRot = lt.Rotation;
                if (math.lengthsq(startRot.value) > 0.001f) startRot = math.normalize(startRot);
                else startRot = quaternion.identity;

                climb.TransitionStartRot = startRot;
                climb.TransitionTargetPos = targetStandingPos + (worldUp * 0.9f);
                climb.TransitionTargetRot = startRot; // Maintain valid rotation
                
                // UnityEngine.Debug.Log($"[CLIMB_VAULT_DEBUG] StartPos={climb.TransitionStartPos} TargetPos={climb.TransitionTargetPos}");
                // UnityEngine.Debug.Log($"[CLIMB_VAULT_DEBUG] StartRot={climb.TransitionStartRot.value} TargetRot={climb.TransitionTargetRot.value}");
                
                // Needs Crouch?
                // Calculate headroom:
                // float headroom = ...
                climb.NeedsCrouchAfterVault = false; 
                
                // Zero velocity
                vel.Linear = float3.zero;
                vel.Angular = float3.zero;
                                
                // The Animation System will detect IsClimbingUp and trigger "PullUp"
                // The Animation Event System will handle "VaultComplete" clearing IsClimbingUp
            }
        }
    }
}
