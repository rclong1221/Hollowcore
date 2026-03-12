using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms; // For PostTransformMatrix
using Player.Components;
using DIG.Player.Abilities;
using Unity.Collections;

/// <summary>
/// Handles player stance changes (standing, crouching, prone)
/// Includes cooldowns to prevent spam and collision checks for stance transitions
/// </summary>
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerGroundCheckSystem))]
[UpdateBefore(typeof(PlayerStateSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
public partial struct PlayerStanceSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        
        foreach (var (playerState, playerInput, entity) in 
                 SystemAPI.Query<RefRW<PlayerState>, RefRO<PlayerInput>>()
                 .WithAll<Simulate>()
                 .WithEntityAccess())
        {
            ref var pState = ref playerState.ValueRW;
            var input = playerInput.ValueRO;
            
            var prefs = state.EntityManager.HasComponent<PlayerInputPreferences>(entity) 
                ? state.EntityManager.GetComponentData<PlayerInputPreferences>(entity)
                : PlayerInputPreferences.Default;
            
            bool pronePressed = input.Prone.IsSet && pState.LastProneInput == 0;
            pState.LastProneInput = input.Prone.IsSetByte;

            if (prefs.ProneToggle && pronePressed)
            {
                pState.ProneToggled = !pState.ProneToggled;
            }
            
            bool canChangeStance = elapsedTime - pState.LastStanceChangeTime >= pState.StanceChangeCooldown;
            if (!canChangeStance)
                continue;
            
            PlayerStance desiredStance = DetermineDesiredStance(pState.Stance, input, pState, prefs);
            
            if (desiredStance != pState.Stance)
            {
                if (CanTransitionToStance(pState, desiredStance))
                {
                    pState.Stance = desiredStance;
                    pState.LastStanceChangeTime = elapsedTime;
                }
            }
        }
    }
    
    private static PlayerStance DetermineDesiredStance(PlayerStance currentStance, in PlayerInput input, in PlayerState state, in PlayerInputPreferences prefs)
    {
        bool wantsProne = prefs.ProneToggle ? state.ProneToggled : input.Prone.IsSet;
        if (wantsProne) return PlayerStance.Prone;
        if (currentStance == PlayerStance.Crouching) return PlayerStance.Crouching;
        return PlayerStance.Standing;
    }

    private static bool CanTransitionToStance(in PlayerState state, PlayerStance desiredStance)
    {
        if (desiredStance == PlayerStance.Prone && !state.IsGrounded) return false;
        return true;
    }
}

/// <summary>
/// Updates the player's physics collider height based on stance.
/// LEAK FIX: Uses PostTransformMatrix Scaling instead of modifying BlobAssets.
/// Safe (No Unsafe Code), Zero Allocation.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerStateSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[RequireMatchingQueriesForUpdate]
public partial struct PlayerColliderHeightSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Ensure network is ready and we have at least one player with crouch ability
        state.RequireForUpdate<NetworkTime>();
        state.RequireForUpdate<CrouchAbility>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Safety check: ensure time is valid
        if (!SystemAPI.HasSingleton<NetworkTime>())
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        if (deltaTime <= 0f)
            return;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var matrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(false);

        foreach (var (crouchAbility, crouchSettings, playerState, ccSettings, entity) in
                 SystemAPI.Query<RefRW<CrouchAbility>, RefRO<CrouchSettings>, RefRO<PlayerState>, RefRO<CharacterControllerSettings>>()
                 .WithAll<Simulate>()
                 .WithEntityAccess())
        {
            // Skip if crouchSettings has invalid data (component not fully initialized)
            float standingHeight = crouchSettings.ValueRO.StandingHeight;
            if (standingHeight <= 0f)
                continue;

            PlayerStance currentStance = playerState.ValueRO.Stance;
            bool stanceChanged = currentStance != crouchAbility.ValueRO.LastProcessedStance;
            bool heightMismatch = math.abs(crouchAbility.ValueRO.CurrentHeight - playerState.ValueRO.TargetHeight) > 0.01f;

            if (!stanceChanged && !crouchAbility.ValueRO.StanceDirty && !heightMismatch)
            {
                continue;
            }

            float targetHeight;
            float targetRadius;
            float crouchRadius = crouchSettings.ValueRO.CrouchRadius;
            float crouchHeight = crouchSettings.ValueRO.CrouchHeight;

            // Validate crouch settings - skip if invalid
            if (crouchRadius <= 0f) crouchRadius = 0.35f;
            if (crouchHeight <= 0f) crouchHeight = 1.0f;

            if (currentStance == PlayerStance.Prone)
            {
                targetHeight = playerState.ValueRO.TargetHeight;
                if (targetHeight <= 0f) targetHeight = 0.5f;
                targetRadius = crouchRadius * 0.8f;
            }
            else if (currentStance == PlayerStance.Crouching || crouchAbility.ValueRO.IsCrouching)
            {
                targetHeight = crouchHeight;
                targetRadius = crouchRadius;
            }
            else
            {
                targetHeight = ccSettings.ValueRO.Height;
                targetRadius = ccSettings.ValueRO.Radius;
            }

            // Final validation
            if (targetHeight <= 0f) targetHeight = ccSettings.ValueRO.Height;
            if (targetRadius <= 0f) targetRadius = ccSettings.ValueRO.Radius;

            // SYNC FIX: Use PlayerState.CurrentHeight as authority to match CharacterController
            float newHeight = playerState.ValueRO.CurrentHeight;

            // Initialize Original Dimensions if needed
            if (crouchAbility.ValueRO.OriginalHeight <= 0f)
            {
                // Use ccSettings as authority for the "base" scale
                crouchAbility.ValueRW.OriginalHeight = ccSettings.ValueRO.Height;
                crouchAbility.ValueRW.OriginalRadius = ccSettings.ValueRO.Radius;
            }

            // ========================================================
            // SCALING STRATEGY (Safe, No Allocations)
            // ========================================================
            float originalH = crouchAbility.ValueRO.OriginalHeight;
            float originalR = crouchAbility.ValueRO.OriginalRadius;

            if (originalH < 0.01f || originalR < 0.01f) continue;

            // Ensure targetRadius logic is preserved for XZ scaling
             if (currentStance == PlayerStance.Prone)
            {
                targetRadius = crouchRadius * 0.8f;
            }
            else if (currentStance == PlayerStance.Crouching || crouchAbility.ValueRO.IsCrouching)
            {
                targetRadius = crouchRadius;
            }
            else
            {
                targetRadius = crouchAbility.ValueRO.OriginalRadius > 0f ? crouchAbility.ValueRO.OriginalRadius : 0.35f;
            }

            // Validate Scale
            float scaleY = newHeight / originalH;
            float scaleXZ = targetRadius / originalR;

            // Validate scale values - prevent NaN/Infinity and extreme values
            if (!math.isfinite(scaleY) || !math.isfinite(scaleXZ)) continue;
            scaleY = math.clamp(scaleY, 0.1f, 10f);
            scaleXZ = math.clamp(scaleXZ, 0.1f, 10f);

            // Calculate Vertical Offset. 
            // If the mesh pivot is at the feet, offsetY should be 0.
            // If the mesh pivot is at the center, offsetY = -(originalH - newHeight) * 0.5f.
            // Most humanoid models in Unity use foot pivot. 
            // Unified Stance Fix: Assuming foot pivot (offsetY = 0) to avoid sinking.
            float offsetY = 0f; 

            // Construct Matrix
            float4x4 scaleMatrix = float4x4.TRS(
                new float3(0f, offsetY, 0f),
                quaternion.identity,
                new float3(scaleXZ, scaleY, scaleXZ)
            );

            // Apply Matrix using ComponentLookup (Burst-safe)
            if (matrixLookup.HasComponent(entity))
            {
                matrixLookup[entity] = new PostTransformMatrix { Value = scaleMatrix };
            }
            else
            {
                ecb.AddComponent(entity, new PostTransformMatrix { Value = scaleMatrix });
            }

            // Update CrouchAbility just for record keeping (if needed by other systems)
            crouchAbility.ValueRW.CurrentHeight = newHeight;
            crouchAbility.ValueRW.StanceDirty = false;
            crouchAbility.ValueRW.LastProcessedStance = currentStance;
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
