using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;
using DIG.Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Tackle system for Epic 7.4.2.
    /// Handles tackle input, initiation, direction commitment, and timeout.
    /// 
    /// Tackle is a high-risk action:
    /// - Requires sprinting speed to initiate
    /// - Commits player to forward direction
    /// - Uses stamina
    /// - Has cooldown
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    // [BurstCompile] // Temporarily disabled for debug logging
    public partial struct TackleSystem : ISystem
    {
        // [BurstCompile] // Temporarily disabled for debug logging
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TackleSettings>();
            state.RequireForUpdate<NetworkTime>();
        }
        
        // [BurstCompile] // Temporarily disabled for debug logging
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TackleSettings>(out var settings))
                return;
            
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (tackleState, playerState, playerInput, velocity, transform, stamina, entity) in
                SystemAPI.Query<RefRW<TackleState>, RefRW<PlayerState>, RefRO<PlayerInput>, 
                    RefRO<PhysicsVelocity>, RefRO<LocalTransform>, RefRW<PlayerStamina>>()
                    .WithAll<Simulate>()
                    .WithEntityAccess())
            {
                ref var tackle = ref tackleState.ValueRW;
                ref var pState = ref playerState.ValueRW;
                ref var stam = ref stamina.ValueRW;
                var input = playerInput.ValueRO;
                var vel = velocity.ValueRO;
                var pos = transform.ValueRO;
                
                // Count down cooldown
                if (tackle.TackleCooldown > 0)
                {
                    tackle.TackleCooldown -= deltaTime;
                    if (tackle.TackleCooldown < 0)
                        tackle.TackleCooldown = 0;
                }
                
                // If currently tackling, update timer
                if (tackle.TackleTimeRemaining > 0)
                {
                    tackle.TackleTimeRemaining -= deltaTime;
                    
                    // Tackle ended
                    if (tackle.TackleTimeRemaining <= 0)
                    {
                        tackle.TackleTimeRemaining = 0;
                        
                        // Reset movement state back to normal
                        pState.MovementState = PlayerMovementState.Idle;
                        
                        #if UNITY_EDITOR || DEVELOPMENT_BUILD
                        // UnityEngine.Debug.Log($"[TackleSystem] Tackle ended! DidHit={tackle.DidHitTarget}");
                        #endif
                        
                        // Reset for next tackle
                        tackle.HasProcessedHit = false;
                        tackle.DidHitTarget = false;
                    }
                    else
                    {
                        // Still tackling - maintain Tackling state
                        pState.MovementState = PlayerMovementState.Tackling;
                    }
                    
                    continue; // Don't process new tackle input while tackling
                }
                
                // Check for tackle input
                if (!input.Tackle.IsSet)
                    continue;
                
                // DEBUG: Log tackle attempt
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                // UnityEngine.Debug.Log($"[TackleSystem] Tackle input detected! Checking conditions...");
                #endif
                
                // Check cooldown
                if (tackle.TackleCooldown > 0)
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    // UnityEngine.Debug.Log($"[TackleSystem] BLOCKED: Cooldown remaining: {tackle.TackleCooldown:F2}s");
                    #endif
                    continue;
                }
                
                // Check if already in a state that prevents tackling
                if (pState.MovementState == PlayerMovementState.Staggered ||
                    pState.MovementState == PlayerMovementState.Knockdown ||
                    pState.MovementState == PlayerMovementState.Rolling ||
                    pState.MovementState == PlayerMovementState.Diving ||
                    pState.MovementState == PlayerMovementState.Climbing ||
                    pState.MovementState == PlayerMovementState.Swimming)
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    // UnityEngine.Debug.Log($"[TackleSystem] BLOCKED: Invalid movement state: {pState.MovementState}");
                    #endif
                    continue;
                }
                
                // Check if grounded
                if (!pState.IsGrounded)
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    // UnityEngine.Debug.Log($"[TackleSystem] BLOCKED: Not grounded");
                    #endif
                    continue;
                }
                
                // Check speed requirement
                float horizontalSpeed = math.length(new float3(vel.Linear.x, 0, vel.Linear.z));
                if (horizontalSpeed < settings.TackleMinSpeed)
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    // UnityEngine.Debug.Log($"[TackleSystem] BLOCKED: Speed too low: {horizontalSpeed:F2} m/s (need {settings.TackleMinSpeed:F2})");
                    #endif
                    continue;
                }
                
                // Check stamina
                if (stam.Current < settings.TackleStaminaCost)
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    // UnityEngine.Debug.Log($"[TackleSystem] BLOCKED: Not enough stamina: {stam.Current:F1} (need {settings.TackleStaminaCost:F1})");
                    #endif
                    continue;
                }
                
                // === INITIATE TACKLE ===
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                // UnityEngine.Debug.Log($"[TackleSystem] TACKLE INITIATED! Speed: {horizontalSpeed:F2} m/s, Stamina: {stam.Current:F1}");
                #endif
                
                // Consume stamina
                stam.Current -= settings.TackleStaminaCost;
                
                // Set cooldown
                tackle.TackleCooldown = settings.TackleCooldownDuration;
                
                // Commit to direction (forward based on current velocity)
                float3 horizontalVel = new float3(vel.Linear.x, 0, vel.Linear.z);
                tackle.TackleDirection = math.normalizesafe(horizontalVel);
                
                // Store initial speed for impact calculation
                tackle.TackleSpeed = horizontalSpeed * settings.TackleSpeedMultiplier;
                
                // Start tackle timer
                tackle.TackleTimeRemaining = settings.TackleDuration;
                
                // Reset hit tracking
                tackle.DidHitTarget = false;
                tackle.HasProcessedHit = false;
                
                // Set movement state
                pState.MovementState = PlayerMovementState.Tackling;
            }
        }
    }
}
