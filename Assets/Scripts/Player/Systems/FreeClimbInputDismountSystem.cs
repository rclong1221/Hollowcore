using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 13.20.2: Handles input-based dismount from climbing.
    /// 
    /// - Crouch input = Drop (dismount without jump)
    /// - Jump + Back = Jump Off (push away from wall)
    /// - Re-mount cooldown prevents sticky re-climbing
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FreeClimbMovementSystem))]
    [UpdateBefore(typeof(FreeClimbWallJumpSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct FreeClimbInputDismountSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTime = SystemAPI.Time.ElapsedTime;
            var isServer = state.WorldUnmanaged.IsServer();

            new InputDismountJob
            {
                CurrentTime = currentTime,
                IsServer = isServer
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct InputDismountJob : IJobEntity
        {
            public double CurrentTime;
            [ReadOnly] public bool IsServer;

            private void Execute(
                Entity entity,
                ref FreeClimbState climb,
                RefRO<FreeClimbSettings> settings,
                RefRO<PlayerInput> input,
                ref LocalTransform lt,
                ref PhysicsVelocity vel)
            {
                // SERVER-ONLY: Client receives dismount via replication
                if (!IsServer)
                    return;
                    
                // Skip if not climbing or in transitions
                // EPIC 14.24: Also block during hang transition
                if (!climb.IsClimbing || climb.IsTransitioning || climb.IsClimbingUp || 
                    climb.IsWallJumping || climb.IsHangTransitioning)
                    return;

                var cfg = settings.ValueRO;
                var playerInput = input.ValueRO;

                // === CROUCH TO DROP ===
                // Crouch input triggers immediate drop without force
                if (playerInput.Crouch.IsSet)
                {
                    UnityEngine.Debug.Log($"[CLIMB_ABORT] Input Dismount (Crouch)");
                    PerformDismount(ref climb, ref lt, ref vel, cfg, CurrentTime, applyForce: false);
                    return;
                }

                // === JUMP OFF ===
                // Triggers "Jump Off" (Dismount with push)
                // We ONLY trigger this if input is NEUTRAL or BACK.
                // If the player is holding UP/LEFT/RIGHT, we let FreeClimbWallJumpSystem handle it (Wall Jump).
                if (playerInput.Jump.IsSet)
                {
                    bool hasDirectionalInput = math.lengthsq(new float2(playerInput.Horizontal, playerInput.Vertical)) > 0.01f;
                    bool isBackInput = playerInput.Vertical < -0.1f;
                    
                    // If simply pressing Jump (Neutral) OR Jump+Back -> Jump Off
                    if (!hasDirectionalInput || isBackInput)
                    {
                        UnityEngine.Debug.Log($"[CLIMB_ABORT] Input Dismount (Jump Off). Vertical={playerInput.Vertical}");
                        PerformDismount(ref climb, ref lt, ref vel, cfg, CurrentTime, applyForce: true);
                        return;
                    }
                    
                    // Otherwise (Jump + Up/Side), fall through to WallJumpSystem
                }
            }

            private void PerformDismount(
                ref FreeClimbState climb,
                ref LocalTransform lt,
                ref PhysicsVelocity vel,
                FreeClimbSettings cfg,
                double currentTime,
                bool applyForce)
            {
                // Record dismount info for anti-stick cooldown
                climb.LastDismountTime = currentTime;
                climb.LastClimbedSurface = climb.SurfaceEntity;

                // Clear climbing state
                climb.IsClimbing = false;
                climb.IsTransitioning = false;
                climb.TransitionProgress = 0f;
                climb.IsFreeHanging = false;

                if (applyForce)
                {
                    // Apply push-away force (jump off)
                    float3 pushDirection = math.normalizesafe(climb.GripWorldNormal);
                    float pushForce = cfg.DismountPushForce;
                    float upForce = cfg.DismountUpForce;

                    vel.Linear = pushDirection * pushForce + new float3(0, upForce, 0);
                }
                else
                {
                    // Simple drop - zero velocity for gravity to take over
                    vel.Linear = float3.zero;
                }

                vel.Angular = float3.zero;
            }
        }
    }
}
