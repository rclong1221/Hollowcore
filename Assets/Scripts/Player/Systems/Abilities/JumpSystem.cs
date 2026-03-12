using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.Abilities;
using Player.Components;

namespace DIG.Player.Systems.Abilities
{
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    public partial struct JumpSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var currentTime = SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;

            var job = new JumpJob
            {
                PhysicsWorld = physicsWorld,
                CurrentTime = currentTime,
                DeltaTime = deltaTime
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct JumpJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            public double CurrentTime;
            public float DeltaTime;

            private void Execute(Entity entity, ref JumpAbility jumpAbility, ref PhysicsVelocity velocity, ref PlayerState playerState, in JumpSettings jumpSettings, in PlayerInput playerInput, in LocalTransform transform)
            {
                // 13.13.10 Gravity Reset on Stop (Landing)
                // Use WasGrounded to detect landing transition
                if (playerState.IsGrounded && !playerState.WasGrounded)
                {
                    jumpAbility.FramesRemaining = 0;
                    jumpAbility.IsWaitingForEvent = false;
                    jumpAbility.IsJumping = false;
                    jumpAbility.HoldForce = 0f;
                    jumpAbility.AirborneJumpsUsed = 0;
                    jumpAbility.LastLandTime = CurrentTime; // Fix: LandTime -> LastLandTime
                    // jumpAbility.TimeSinceGrounded = 0f; // Fix: Use LastGroundedTime instead
                    jumpAbility.LastGroundedTime = CurrentTime;
                    jumpAbility.JustJumped = false; 
                }
                
                // Update TimeSinceJump
                if (jumpAbility.TimeSinceJump < 1000f) // Prevent overflow
                {
                    jumpAbility.TimeSinceJump += DeltaTime;
                }
                
                // Track time since grounded
                if (playerState.IsGrounded)
                {
                     jumpAbility.LastGroundedTime = CurrentTime;
                }
                
                // Calculate time since grounded
                double timeSinceGrounded = CurrentTime - jumpAbility.LastGroundedTime;

                bool isGrounded = playerState.IsGrounded;
                bool isCoyote = !isGrounded && timeSinceGrounded <= jumpSettings.CoyoteTime;
                bool canJump = isGrounded || isCoyote;

                // 13.13.9 Recurrence Delay
                if (isGrounded && jumpSettings.RecurrenceDelay > 0 && 
                    CurrentTime < jumpAbility.LastLandTime + jumpSettings.RecurrenceDelay) 
                {
                    canJump = false; 
                }
                
                // 13.14.P7: Lazy GetRigidBodyIndex - moved inside check block below

                // 13.13.2 Slope Limit Prevention
                if (canJump && !CheckSlopeLimit(playerState.GroundNormal, math.up(), jumpSettings.PreventSlopeLimitJump, jumpSettings.SlopeLimit))
                {
                    canJump = false;
                }



                // Simple jump detection: did player just press jump this frame?
                bool jumpPressed = playerInput.Jump.IsSet;
                bool jumpJustPressed = jumpPressed && !jumpAbility.WasJumpPressed;

                // GROUNDED JUMP: on ground (or coyote) and just pressed jump
                if (jumpJustPressed && canJump)
                {
                    // 13.13.1 Ceiling Check (13.14.P5 Lazy Check)
                    bool ceilingClear = true;
                    if (jumpSettings.MinCeilingJumpHeight > 0)
                    {
                         int rigidBodyIndex = PhysicsWorld.GetRigidBodyIndex(entity);
                         if (!CheckCeilingClearance(ref PhysicsWorld, transform.Position, math.up(), jumpSettings.MinCeilingJumpHeight, rigidBodyIndex))
                         {
                             ceilingClear = false;
                         }
                    }

                    if (ceilingClear)
                    {
                        StartJumpForce(ref jumpAbility, ref velocity, in jumpSettings, in playerInput, false);
                        playerState.IsGrounded = false;
                        jumpAbility.TimeSinceJump = 0f;
                        jumpAbility.IsJumping = true;
                    }
                }
                // AIRBORNE JUMP: in air, just pressed jump, have jumps remaining
                else if (jumpJustPressed && !isGrounded && jumpAbility.AirborneJumpsUsed < jumpSettings.MaxAirborneJumps)
                {
                    ApplyJumpForceImmediate(ref jumpAbility, ref velocity, in jumpSettings, in playerInput, true);
                    playerState.IsGrounded = false;
                    jumpAbility.TimeSinceJump = 0f;
                    jumpAbility.IsJumping = true;
                }

                // Track for next frame
                jumpAbility.WasJumpPressed = jumpPressed;
                
                // 13.13.7 Hold For Height
                if (jumpPressed && jumpAbility.IsJumping && velocity.Linear.y > 0)
                {
                    jumpAbility.HoldForce += jumpSettings.ForceHold;
                    jumpAbility.HoldForce /= (1f + jumpSettings.ForceDampingHold);
                    velocity.Linear.y += jumpAbility.HoldForce;
                }
                
                // Update WasGrounded for next frame
                playerState.WasGrounded = isGrounded;

                // 13.13.4 Multi-Frame Force Application
                if (jumpAbility.FramesRemaining > 0)
                {
                     ApplyMultiFrameForce(ref velocity, ref jumpAbility, in jumpSettings);
                }
            }
            
            private void StartJumpForce(ref JumpAbility jumpAbility, ref PhysicsVelocity velocity, 
                                        in JumpSettings jumpSettings, in PlayerInput playerInput, bool isAirborneJump)
            {
                // 13.13.5 Animation Event Wait
                if (jumpSettings.WaitForAnimationEvent && !isAirborneJump)
                {
                    jumpAbility.IsWaitingForEvent = true;
                     return; 
                }
             
                ApplyJumpForceImmediate(ref jumpAbility, ref velocity, in jumpSettings, in playerInput, isAirborneJump);
            }
            
             private void ApplyJumpForceImmediate(ref JumpAbility jumpAbility, ref PhysicsVelocity velocity, 
                                        in JumpSettings jumpSettings, in PlayerInput playerInput, bool isAirborneJump)
             {
                float jumpForce = jumpSettings.JumpForce;

                if (isAirborneJump)
                {
                    // 13.14.P10: Precompute Default Multipliers (remove ternary check)
                    jumpForce *= jumpSettings.AirborneJumpForce;
                    jumpAbility.AirborneJumpsUsed++;
                    velocity.Linear.y = 0;
                }

                // Directional
                float2 inputDir = new float2(playerInput.Horizontal, playerInput.Vertical);
                jumpForce = ApplyDirectionalMultiplier(jumpForce, inputDir, in jumpSettings);

                // Multi-Frame
                if (jumpSettings.JumpFrames > 1)
                {
                    jumpAbility.FramesRemaining = jumpSettings.JumpFrames;
                    ApplyMultiFrameForce(ref velocity, ref jumpAbility, in jumpSettings, jumpForce);
                }
                else
                {
                    velocity.Linear.y = jumpForce;
                }
                
                // 13.13.6 Presentation Flag
                if (jumpSettings.SpawnSurfaceEffect)
                {
                    jumpAbility.JustJumped = true;
                }
             }

            private void ApplyMultiFrameForce(ref PhysicsVelocity velocity, ref JumpAbility jumpAbility, 
                                             in JumpSettings jumpSettings, float specificForce = -1f)
            {
                int totalFrames = jumpSettings.JumpFrames > 0 ? jumpSettings.JumpFrames : 1;
                float baseForce = jumpSettings.JumpForce; 
                float forcePerFrame = baseForce / totalFrames;
                
                if (specificForce >= 0)
                {
                    velocity.Linear.y += (specificForce / totalFrames);
                }
                else
                {
                    velocity.Linear.y += forcePerFrame;
                }

                jumpAbility.FramesRemaining--;
            }

            private static bool CheckCeilingClearance(ref PhysicsWorld physicsWorld, in float3 position, in float3 up, 
                                                       float minCeilingHeight, int selfRigidBodyIndex)
            {
                if (minCeilingHeight <= 0) return true;

                float3 rayStart = position + up * 1.8f; 
                float3 rayEnd = rayStart + up * minCeilingHeight;

                var rayInput = new RaycastInput
                {
                    Start = rayStart,
                    End = rayEnd,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u, 
                        GroupIndex = 0
                    }
                };

                if (physicsWorld.CastRay(rayInput, out var hit))
                {
                    if (hit.RigidBodyIndex == selfRigidBodyIndex) return true;
                    // Optimization: simple dot check (13.14.P6)
                    // cos(30) = 0.866
                    float dot = math.dot(-up, hit.SurfaceNormal);
                    if (dot > 0.866f) return false; 
                }
                return true;
            }

            private static bool CheckSlopeLimit(in float3 groundNormal, in float3 up, bool preventSlopeLimitJump, float slopeLimit)
            {
                if (!preventSlopeLimitJump) return true;
                if (slopeLimit <= 0) slopeLimit = 45f;
                
                // Optimization: simple dot check (13.14.P6)
                float dot = math.dot(groundNormal, up);
                float cosThreshold = math.cos(math.radians(slopeLimit));
                return dot >= cosThreshold;
            }

            private static float ApplyDirectionalMultiplier(float force, in float2 inputDir, in JumpSettings settings)
            {
                if (math.lengthsq(inputDir) < 0.01f) return force;

                // 13.14.P10: Removed runtime default checks. Assumes Authoring sets defaults correctly.
                float sidewaysMultiplier = settings.SidewaysForceMultiplier;
                float backwardsMultiplier = settings.BackwardsForceMultiplier;

                if (inputDir.y < -0.1f)
                {
                    float backwardsInfluence = math.saturate(-inputDir.y);
                    force = math.lerp(force, force * backwardsMultiplier, backwardsInfluence);
                }
                
                float sidewaysInfluence = math.saturate(math.abs(inputDir.x) - math.abs(inputDir.y));
                if (sidewaysInfluence > 0.1f)
                {
                    force = math.lerp(force, force * sidewaysMultiplier, sidewaysInfluence);
                }

                return force;
            }
        }
    }
}
