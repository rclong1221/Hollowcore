using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// SlideSystem: Handles slide mechanic including manual trigger, auto-trigger on slopes/slippery surfaces,
    /// physics simulation, and state management.
    /// 
    /// Slide can be triggered by:
    /// 1. Manual: Player presses slide button (X) while moving above min speed
    /// 2. Slope: Auto-triggers on slopes steeper than MinSlopeAngle
    /// 3. Slippery: Auto-triggers on surfaces marked as slippery
    /// 
    /// Updates in PredictedFixedStepSimulationSystemGroup for proper netcode prediction.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerGroundCheckSystem))]
    public partial class SlideSystem : SystemBase
{
    // Diagnostic toggle: set to true to enable internal debug logging
    private static bool s_DiagnosticsEnabled = false;

    // Public accessor for quick toggling in editor or tests
    public static bool DiagnosticsEnabled
    {
        get => s_DiagnosticsEnabled;
        set => s_DiagnosticsEnabled = value;
    }

    private static void DLog(string msg)
    {
        if (s_DiagnosticsEnabled) UnityEngine.Debug.Log(msg);
    }

    private static void DLogWarning(string msg)
    {
        if (s_DiagnosticsEnabled) UnityEngine.Debug.LogWarning(msg);
    }
    protected override void OnCreate()
    {
        // No RequireForUpdate - system handles both PlayerInput (NetCode) and PlayerInputComponent (hybrid) paths
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        float dt = SystemAPI.Time.DeltaTime;
        var currentTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;

        // Process networked/predicted players (uses PlayerInput from NetCode)
        foreach (var (inputRO, slideStateRW, slideComponent, playerState, velocity, localToWorld, entity) in 
                 SystemAPI.Query<RefRO<PlayerInput>, RefRW<SlideState>, RefRO<SlideComponent>, RefRW<PlayerState>, RefRO<PhysicsVelocity>, RefRO<LocalToWorld>>()
                 .WithAll<Simulate>()
                 .WithEntityAccess())
        {
            var input = inputRO.ValueRO;
            ref var slideState = ref slideStateRW.ValueRW;
            var config = slideComponent.ValueRO;
            ref var pState = ref playerState.ValueRW;
            var vel = velocity.ValueRO;
            var transform = localToWorld.ValueRO;

            ProcessSlide(ref slideState, config, input.Slide.IsSet, ref pState, vel, transform, dt, currentTick.TickIndexForValidTick, ecb, entity);
        }

        // Process hybrid/local-only players (PlayerInputComponent path)
        foreach (var (inputRO, slideStateRW, slideComponent, playerState, velocity, localToWorld, entity) in 
                 SystemAPI.Query<RefRO<Player.Components.PlayerInputComponent>, RefRW<SlideState>, RefRO<SlideComponent>, RefRW<PlayerState>, RefRO<PhysicsVelocity>, RefRO<LocalToWorld>>()
                 .WithNone<PlayerInput>()
                 .WithEntityAccess())
        {
            var input = inputRO.ValueRO;
            ref var slideState = ref slideStateRW.ValueRW;
            var config = slideComponent.ValueRO;
            ref var pState = ref playerState.ValueRW;
            var vel = velocity.ValueRO;
            var transform = localToWorld.ValueRO;

            bool slidePressed = input.Slide != 0;
            ProcessSlide(ref slideState, config, slidePressed, ref pState, vel, transform, dt, 0, ecb, entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    /// <summary>
    /// Core slide processing logic - handles start, update, and end of slides
    /// </summary>
    private void ProcessSlide(ref SlideState slideState, in SlideComponent config, bool slideInput,
                              ref PlayerState playerState, in PhysicsVelocity velocity, in LocalToWorld transform,
                              float dt, uint currentTick, EntityCommandBuffer ecb, Entity entity)
    {
        // Update cooldown
        if (slideState.CooldownRemaining > 0f)
        {
            slideState.CooldownRemaining = math.max(0f, slideState.CooldownRemaining - dt);
        }

        // If not sliding, check for slide triggers
        if (!slideState.IsSliding)
        {
            // Check if we should start a slide
            bool shouldStartSlide = false;
            SlideTriggerType triggerType = SlideTriggerType.Manual;

            // Calculate current speed
            float3 horizontalVel = new float3(velocity.Linear.x, 0f, velocity.Linear.z);
            float currentSpeed = math.length(horizontalVel);

            // Manual trigger - slide button pressed while moving fast enough
            if (slideInput)
            {
                DLog($"[SlideSystem] X pressed! speed={currentSpeed:F2}, minSpeed={config.MinSpeed}, cooldown={slideState.CooldownRemaining:F2}, grounded={playerState.IsGrounded}");
            }
            
            if (slideInput && currentSpeed >= config.MinSpeed && slideState.CooldownRemaining <= 0f)
            {
                // Check stamina if present
                if (EntityManager.HasComponent<PlayerStamina>(entity))
                {
                    var stamina = EntityManager.GetComponentData<PlayerStamina>(entity);
                    if (stamina.Current >= config.StaminaCost)
                    {
                        shouldStartSlide = true;
                        triggerType = SlideTriggerType.Manual;
                        
                        // Consume stamina
                        stamina.Current = math.max(0f, stamina.Current - config.StaminaCost);
                        ecb.SetComponent(entity, stamina);
                        DLog($"[SlideSystem] ✓ Starting slide (with stamina check)");
                    }
                    else
                    {
                        DLogWarning($"[SlideSystem] ✗ Not enough stamina: {stamina.Current}/{config.StaminaCost}");
                    }
                }
                else
                {
                    // No stamina component, allow slide
                    shouldStartSlide = true;
                    triggerType = SlideTriggerType.Manual;
                    DLog($"[SlideSystem] ✓ Starting slide (no stamina component)");
                }
            }

            // Auto-trigger on steep slopes
            if (!shouldStartSlide && playerState.IsGrounded && currentSpeed >= config.MinSpeed)
            {
                float slopeAngle = CalculateSlopeAngle(playerState.GroundNormal);
                if (slopeAngle >= config.MinSlopeAngle)
                {
                    shouldStartSlide = true;
                    triggerType = SlideTriggerType.Slope;
                }
            }

            // TODO: Auto-trigger on slippery surfaces (requires SurfaceMaterial integration)
            // This will be implemented when SurfaceMaterial system is extended with IsSlippery flag

            // Start slide if triggered
            if (shouldStartSlide)
            {
                float3 slideDir = math.normalizesafe(horizontalVel);
                if (math.lengthsq(slideDir) < 0.01f)
                {
                    // Use forward direction if no velocity
                    slideDir = math.normalize(new float3(transform.Forward.x, 0f, transform.Forward.z));
                }

                slideState.IsSliding = true;
                slideState.SlideProgress = 0f;
                slideState.CurrentSpeed = currentSpeed;
                slideState.SlideDirection = slideDir;
                slideState.TriggerType = triggerType;
                slideState.StartTick = currentTick;
                slideState.Duration = config.Duration;
                slideState.MinSpeed = config.MinSpeed;
                slideState.MaxSpeed = config.MaxSpeed;
                slideState.Acceleration = config.Acceleration;
                slideState.Friction = config.Friction;

                // Set movement state to Sliding
                playerState.MovementState = PlayerMovementState.Sliding;
                
                DLog($"[SlideSystem] ✓✓ SLIDE STARTED! IsSliding={slideState.IsSliding}, speed={slideState.CurrentSpeed:F2}, triggerType={triggerType}");
            }
        }
        else
        {
            // Update active slide
            slideState.SlideProgress += dt;

            // Calculate speed change based on slope
            float slopeAcceleration = 0f;
            if (playerState.IsGrounded)
            {
                // Apply acceleration down slopes, friction up slopes
                float slopeAngle = CalculateSlopeAngle(playerState.GroundNormal);
                float slopeFactor = math.sin(math.radians(slopeAngle));
                slopeAcceleration = slopeFactor * slideState.Acceleration;
            }

            // Update speed
            float targetSpeed = slideState.CurrentSpeed + (slopeAcceleration - slideState.Friction) * dt;
            slideState.CurrentSpeed = math.clamp(targetSpeed, slideState.MinSpeed, slideState.MaxSpeed);

            // Check slide end conditions
            bool shouldEndSlide = false;

            // End if duration exceeded
            if (slideState.SlideProgress >= slideState.Duration)
            {
                shouldEndSlide = true;
            }

            // End if speed drops below minimum
            if (slideState.CurrentSpeed < slideState.MinSpeed)
            {
                shouldEndSlide = true;
            }

            // End if player jumps
            if (playerState.MovementState == PlayerMovementState.Jumping || 
                playerState.MovementState == PlayerMovementState.Falling)
            {
                shouldEndSlide = true;
            }

            // End if not grounded (hit obstacle or fell off edge)
            if (!playerState.IsGrounded)
            {
                shouldEndSlide = true;
            }

            // End slide
            if (shouldEndSlide)
            {
                slideState.IsSliding = false;
                slideState.SlideProgress = 0f;
                slideState.CurrentSpeed = 0f;
                slideState.CooldownRemaining = config.Cooldown;

                // Reset movement state if still sliding
                if (playerState.MovementState == PlayerMovementState.Sliding)
                {
                    playerState.MovementState = PlayerMovementState.Idle;
                }
            }
        }
    }

        /// <summary>
        /// Calculate slope angle from ground normal (in degrees)
        /// </summary>
        private static float CalculateSlopeAngle(float3 normal)
        {
            // Angle between ground normal and up vector
            float dot = math.dot(normal, new float3(0, 1, 0));
            dot = math.clamp(dot, -1f, 1f);
            float angle = math.degrees(math.acos(dot));
            return angle;
        }
    }
}
