using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Physics;
using Player.Components;
using DIG.Player.Components;

/// <summary>
/// Updates player movement state based on input and physics
/// Runs in PredictedFixedStepSimulationSystemGroup for proper physics-based prediction
/// </summary>
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerGroundCheckSystem))]
[UpdateBefore(typeof(PlayerMovementSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial struct PlayerStateSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        var elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        
        // Check if we're on the server - on server, process all simulated players
        // On client, only process entities with GhostOwnerIsLocal (the local player)
        // This prevents client-side prediction from overwriting replicated state for remote players
        bool isServer = state.WorldUnmanaged.IsServer();
        var collisionSettings = SystemAPI.GetSingleton<PlayerCollisionSettings>();

        if (isServer)
        {
            // Server processes all simulated players
            foreach (var (playerState, playerInput, velocity, collisionState, config, entity) in
                     SystemAPI.Query<RefRW<PlayerState>, RefRO<PlayerInput>, RefRO<PhysicsVelocity>, RefRW<PlayerCollisionState>, RefRO<PlayerStanceConfig>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var pState = ref playerState.ValueRW;
                var input = playerInput.ValueRO;
                var vel = velocity.ValueRO;
                ref var collision = ref collisionState.ValueRW;
                var stanceConfig = config.ValueRO;
                bool isClimbing = SystemAPI.HasComponent<FreeClimbState>(entity) && SystemAPI.GetComponent<FreeClimbState>(entity).IsClimbing;

                // Epic 7.4.1: Handle knockdown timing and state transitions
                bool disableStagger = false;
                bool disableKnockdown = false;
                HandleKnockdownTiming(ref pState, ref collision, deltaTime, collisionSettings, out disableStagger, out disableKnockdown);
                
                // Handle component enabled state changes (must be done from OnUpdate context)
                if (disableStagger && SystemAPI.IsComponentEnabled<Staggered>(entity))
                {
                    SystemAPI.SetComponentEnabled<Staggered>(entity, false);
                }
                if (disableKnockdown && SystemAPI.IsComponentEnabled<KnockedDown>(entity))
                {
                    SystemAPI.SetComponentEnabled<KnockedDown>(entity, false);
                }
                
                // Epic 7.4.3: Disable Evading tag when no longer in dodge state
                bool isCurrentlyDodging = pState.MovementState == PlayerMovementState.Rolling || 
                                          pState.MovementState == PlayerMovementState.Diving;
                if (!isCurrentlyDodging && SystemAPI.IsComponentEnabled<Evading>(entity))
                {
                    SystemAPI.SetComponentEnabled<Evading>(entity, false);
                }

                UpdateMovementState(ref pState, input, vel, isClimbing, collision.IsKnockedDown);
                UpdateStanceHeight(ref pState, stanceConfig);
                
                if (math.abs(pState.CurrentHeight - pState.TargetHeight) > 0.01f)
                {
                    pState.CurrentHeight = math.lerp(pState.CurrentHeight, pState.TargetHeight, 
                                                    deltaTime * pState.HeightTransitionSpeed);
                }
                else
                {
                    pState.CurrentHeight = pState.TargetHeight;
                }
            }
        }
        else
        {
            // Client only processes the local player - remote players get their state from replication
            foreach (var (playerState, playerInput, velocity, collisionState, config, entity) in
                     SystemAPI.Query<RefRW<PlayerState>, RefRO<PlayerInput>, RefRO<PhysicsVelocity>, RefRW<PlayerCollisionState>, RefRO<PlayerStanceConfig>>()
                     .WithAll<Simulate, GhostOwnerIsLocal>()
                     .WithEntityAccess())
            {
                ref var pState = ref playerState.ValueRW;
                var input = playerInput.ValueRO;
                var vel = velocity.ValueRO;
                ref var collision = ref collisionState.ValueRW;
                var stanceConfig = config.ValueRO;
                bool isClimbing = SystemAPI.HasComponent<FreeClimbState>(entity) && SystemAPI.GetComponent<FreeClimbState>(entity).IsClimbing;

                // Epic 7.4.1: Handle knockdown timing and state transitions
                bool disableStagger = false;
                bool disableKnockdown = false;
                HandleKnockdownTiming(ref pState, ref collision, deltaTime, collisionSettings, out disableStagger, out disableKnockdown);
                
                // Handle component enabled state changes (must be done from OnUpdate context)
                if (disableStagger && SystemAPI.IsComponentEnabled<Staggered>(entity))
                {
                    SystemAPI.SetComponentEnabled<Staggered>(entity, false);
                }
                if (disableKnockdown && SystemAPI.IsComponentEnabled<KnockedDown>(entity))
                {
                    SystemAPI.SetComponentEnabled<KnockedDown>(entity, false);
                }
                
                // Epic 7.4.3: Disable Evading tag when no longer in dodge state
                bool isCurrentlyDodging = pState.MovementState == PlayerMovementState.Rolling || 
                                          pState.MovementState == PlayerMovementState.Diving;
                if (!isCurrentlyDodging && SystemAPI.IsComponentEnabled<Evading>(entity))
                {
                    SystemAPI.SetComponentEnabled<Evading>(entity, false);
                }

                UpdateMovementState(ref pState, input, vel, isClimbing, collision.IsKnockedDown);
                UpdateStanceHeight(ref pState, stanceConfig);
                
                if (math.abs(pState.CurrentHeight - pState.TargetHeight) > 0.01f)
                {
                    pState.CurrentHeight = math.lerp(pState.CurrentHeight, pState.TargetHeight, 
                                                    deltaTime * pState.HeightTransitionSpeed);
                }
                else
                {
                    pState.CurrentHeight = pState.TargetHeight;
                }
            }
        }
    }
    
    /// <summary>
    /// Epic 7.4.1: Handles knockdown, stagger timing, and cooldowns
    /// </summary>
    private static void HandleKnockdownTiming(ref PlayerState state, ref PlayerCollisionState collision, float deltaTime, in PlayerCollisionSettings collisionSettings, out bool disableStagger, out bool disableKnockdown)
    {
        disableStagger = false;
        disableKnockdown = false;
        
        // Countdown collision cooldown
        if (collision.CollisionCooldown > 0)
        {
            collision.CollisionCooldown -= deltaTime;
            if (collision.CollisionCooldown < 0)
                collision.CollisionCooldown = 0;
        }
        
        // Countdown stagger time
        if (collision.StaggerTimeRemaining > 0)
        {
            collision.StaggerTimeRemaining -= deltaTime;
            
            if (collision.StaggerTimeRemaining <= 0)
            {
                // Stagger ended
                collision.StaggerTimeRemaining = 0;
                collision.StaggerIntensity = 0;
                collision.StaggerVelocity = float3.zero;
                
                // Signal to disable stagger tag component
                disableStagger = true;
            }
        }
        
        // Countdown knockdown time
        if (collision.KnockdownTimeRemaining > 0)
        {
            collision.KnockdownTimeRemaining -= deltaTime;
            
            if (collision.KnockdownTimeRemaining <= 0)
            {
                // Knockdown ended - start recovery
                collision.KnockdownTimeRemaining = 0;
                collision.IsRecoveringFromKnockdown = true;
                
                // Set recovery duration from settings
                collision.KnockdownRecoveryTimeRemaining = collisionSettings.KnockdownRecoveryDuration;
                
                // Signal to disable knockdown tag component
                disableKnockdown = true;
            }
        }
        
        // Countdown recovery time
        if (collision.IsRecoveringFromKnockdown && collision.KnockdownRecoveryTimeRemaining > 0)
        {
            collision.KnockdownRecoveryTimeRemaining -= deltaTime;
            
            if (collision.KnockdownRecoveryTimeRemaining <= 0)
            {
                // Recovery complete - return to normal
                collision.IsRecoveringFromKnockdown = false;
                collision.KnockdownRecoveryTimeRemaining = 0;
                
                // Clear knockdown state
                state.MovementState = PlayerMovementState.Idle; // Will be updated by UpdateMovementState
            }
        }
        
        // Update movement state for knockdown
        if (collision.IsKnockedDown)
        {
            state.MovementState = PlayerMovementState.Knockdown;
        }
    }
    
    /// <summary>
    /// Updates the movement state based on input and velocity
    /// </summary>
    private static void UpdateMovementState(ref PlayerState state, in PlayerInput input, in PhysicsVelocity velocity, bool isClimbing, bool isKnockedDown)
    {
        // Epic 7.4.1: Knockdown state overrides everything
        if (isKnockedDown)
        {
            state.MovementState = PlayerMovementState.Knockdown;
            return;
        }
        
        // Don't override special movement states (managed by their own systems)
        if (state.MovementState == PlayerMovementState.Rolling ||
            state.MovementState == PlayerMovementState.Diving ||
            state.MovementState == PlayerMovementState.Sliding ||
            state.MovementState == PlayerMovementState.Tackling ||  // Epic 7.4.2
            state.MovementState == PlayerMovementState.Staggered)   // Epic 7.3.8
        {
            return;
        }

        if (isClimbing)
        {
            state.MovementState = PlayerMovementState.Climbing;
            return;
        }

        // Check if in air
        if (!state.IsGrounded)
        {
            // Determine if jumping or falling based on vertical velocity
            if (velocity.Linear.y > 0.5f)
                state.MovementState = PlayerMovementState.Jumping;
            else
                state.MovementState = PlayerMovementState.Falling;
            return;
        }

        // On ground - check horizontal movement
        bool hasMovementInput = input.Horizontal != 0 || input.Vertical != 0;

        if (!hasMovementInput)
        {
            state.MovementState = PlayerMovementState.Idle;
            return;
        }

        // Has movement input - determine speed based on sprint/crouch
        if (input.Sprint.IsSet && state.Stance == PlayerStance.Standing)
        {
            state.MovementState = PlayerMovementState.Sprinting;
        }
        else if (state.Stance == PlayerStance.Crouching || state.Stance == PlayerStance.Prone)
        {
            state.MovementState = PlayerMovementState.Walking; // Slower when crouched/prone
        }
        else
        {
            state.MovementState = PlayerMovementState.Running; // Normal speed
        }
    }
    
    /// <summary>
    /// Updates the target height based on current stance
    /// </summary>
    private static void UpdateStanceHeight(ref PlayerState state, in PlayerStanceConfig config)
    {
        switch (state.Stance)
        {
            case PlayerStance.Standing:
                state.TargetHeight = config.StandingHeight;
                break;
            case PlayerStance.Crouching:
                state.TargetHeight = config.CrouchingHeight;
                break;
            case PlayerStance.Prone:
                state.TargetHeight = config.ProneHeight;
                break;
        }
    }
}

