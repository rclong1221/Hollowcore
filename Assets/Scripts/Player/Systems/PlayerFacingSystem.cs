using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;
using Player.Systems;
using DIG.Core.Input;
using DIG.Targeting;
using UnityEngine;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Handles character facing based on input paradigm.
    /// 
    /// Facing modes:
    /// - CameraForward: Character always faces camera direction (Shooter)
    /// - MovementDirection: Character faces movement direction (MMO, ARPG)
    /// - CursorDirection: Character always faces cursor position (Twin-Stick)
    /// - TargetLocked: Character faces locked target (handled by PlayerMovementSystem)
    /// - ManualTurn: No automatic facing (player controls via input)
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [BurstCompile]
    public partial struct PlayerFacingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Requires at least one player entity
            state.RequireForUpdate<PlayerTag>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            // EPIC 15.20: Read facing mode from ECS singleton (Burst-compatible)
            MovementFacingMode facingMode = MovementFacingMode.CameraForward;
            bool useScreenRelativeMovement = false;
            float singletonCameraYaw = 0f;
            bool adTurnsCharacter = false;
            if (SystemAPI.HasSingleton<ParadigmSettings>())
            {
                var settings = SystemAPI.GetSingleton<ParadigmSettings>();
                if (settings.IsValid)
                {
                    facingMode = settings.FacingMode;
                    useScreenRelativeMovement = settings.UseScreenRelativeMovement;
                    singletonCameraYaw = settings.CameraYaw;
                    adTurnsCharacter = settings.ADTurnsCharacter;
                }
            }

            foreach (var (transform, playerInput, camSettings, entity) in
                SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlayerInput>,
                        RefRO<PlayerCameraSettings>>()
                    .WithAll<Simulate>()
                    .WithEntityAccess())
            {
                var input = playerInput.ValueRO;

                // Skip if target locked (handled by PlayerMovementSystem)
                if (input.IsLockedOn != 0)
                    continue;
                
                // EPIC 15.20: Skip all facing in screen-relative mode - PlayerMovementSystem handles it
                // (including Twin-Stick: faces movement when walking, faces cursor when attacking)
                if (useScreenRelativeMovement)
                    continue;

                // Handle facing based on mode
                switch (facingMode)
                {
                    case MovementFacingMode.CameraForward:
                        // Already handled by steering mode in PlayerMovementSystem
                        break;

                    case MovementFacingMode.MovementDirection:
                        // EPIC 15.20 FIX: Skip when adTurnsCharacter is true (MMO mode)
                        // In MMO, A/D keys turn the character via PlayerMovementSystem
                        // Applying MovementDirection facing here would overwrite that rotation
                        // and cause animation issues (character wouldn't strafe/backpedal properly)
                        if (!adTurnsCharacter)
                        {
                            ApplyMovementDirectionFacing(ref transform.ValueRW, input, deltaTime, useScreenRelativeMovement, singletonCameraYaw);
                        }
                        break;

                    case MovementFacingMode.CursorDirection:
                        ApplyCursorDirectionFacing(ref transform.ValueRW, input, singletonCameraYaw);
                        break;

                    case MovementFacingMode.ManualTurn:
                        // No automatic facing
                        break;

                    case MovementFacingMode.TargetLocked:
                        // Handled by PlayerMovementSystem lock-on logic
                        break;
                }
            }

            // EPIC 15.20 Phase 4b: Sync TargetData.AimDirection from existing rotation.
            // This runs AFTER all rotation is finalized (by this system or PlayerMovementSystem)
            // so it reads the correct facing without modifying it.
            // Needed for CursorDirection (Twin-Stick) and MovementDirection (ARPG) paradigms
            // where no MonoBehaviour targeting system writes AimDirection.
            //
            // EPIC 18.19 Phase 8: Skip when TargetingMode == CursorAim — CursorAimTargetingSystem
            // already writes AimDirection from the cursor raycast. Writing rotation-forward here
            // would overwrite it with stale/default direction.
            if (facingMode == MovementFacingMode.CursorDirection ||
                facingMode == MovementFacingMode.MovementDirection)
            {
                foreach (var (transform, targetData, entity) in
                    SystemAPI.Query<RefRO<LocalTransform>, RefRW<TargetData>>()
                        .WithAll<Simulate, PlayerTag>()
                        .WithEntityAccess())
                {
                    if (targetData.ValueRO.Mode == TargetingMode.CursorAim)
                        continue;
                    targetData.ValueRW.AimDirection = math.forward(transform.ValueRO.Rotation);
                }
            }

            // CameraForward (Shooter) mode: construct 3D aim direction from camera yaw+pitch.
            // Character body rotation only contains yaw (no pitch), so we must use the
            // replicated CameraYaw/CameraPitch from PlayerInput to get the full aim vector.
            // Without this, TargetData.AimDirection stays at its baked default (0,0,1) = North,
            // causing WeaponFireSystem to always fire North.
            if (facingMode == MovementFacingMode.CameraForward)
            {
                foreach (var (input, targetData) in
                    SystemAPI.Query<RefRO<PlayerInput>, RefRW<TargetData>>()
                        .WithAll<Simulate, PlayerTag>())
                {
                    if (input.ValueRO.CameraYawValid != 0)
                    {
                        quaternion aimRot = quaternion.Euler(
                            math.radians(input.ValueRO.CameraPitch),
                            math.radians(input.ValueRO.CameraYaw), 0f);
                        targetData.ValueRW.AimDirection = math.mul(aimRot, math.forward());
                    }

                }
            }

            // EPIC 18.19 Phase 8: CursorAim — replicate cursor aim direction to server.
            // On the server, ParadigmSettings doesn't exist, so facingMode defaults to
            // CameraForward and AimDirection gets computed from CameraYaw/Pitch (wrong for
            // isometric cursor aim). PlayerInputSystem copies the client's cursor-computed
            // AimDirection into PlayerInput.CursorAimDirection, which is replicated via
            // IInputComponentData. This block writes it to TargetData on the server.
            //
            // Guard: only when Mode != CursorAim. On client, ModeDispatcher sets Mode=CursorAim
            // and CursorAimTargetingSystem writes the fresh aim direction — skip to avoid
            // overwriting with the one-frame-stale input value.
            foreach (var (input, targetData) in
                SystemAPI.Query<RefRO<PlayerInput>, RefRW<TargetData>>()
                    .WithAll<Simulate, PlayerTag>())
            {
                if (input.ValueRO.CursorAimValid != 0 && targetData.ValueRO.Mode != TargetingMode.CursorAim)
                    targetData.ValueRW.AimDirection = input.ValueRO.CursorAimDirection;
            }
        }

        /// <summary>
        /// Face the direction of movement (MMO/ARPG style).
        /// Character turns to face where they're moving.
        /// </summary>
        private void ApplyMovementDirectionFacing(ref LocalTransform transform, in PlayerInput input, 
            float deltaTime, bool useScreenRelative, float singletonCameraYaw)
        {
            // Only rotate if there's significant movement input
            float2 moveInput = new float2(input.Horizontal, input.Vertical);
            if (math.lengthsq(moveInput) < 0.1f)
                return;

            // Calculate desired facing direction
            float3 desiredForward;
            
            if (useScreenRelative)
            {
                // SCREEN-RELATIVE: Use camera yaw from ParadigmSettings singleton
                // This works without a netcode player entity
                float3 screenUp;
                float3 screenRight;
                
                var yawRotation = quaternion.Euler(0f, math.radians(singletonCameraYaw), 0f);
                screenUp = math.mul(yawRotation, new float3(0, 0, 1));
                screenRight = math.mul(yawRotation, new float3(1, 0, 0));
                
                // Movement direction in world space
                desiredForward = (screenUp * input.Vertical) + (screenRight * input.Horizontal);
                desiredForward.y = 0;
                desiredForward = math.normalizesafe(desiredForward);
            }
            else
            {
                // CAMERA-RELATIVE: Original behavior - input relative to current character facing
                float inputYaw = math.atan2(moveInput.x, moveInput.y);
                quaternion inputRot = quaternion.Euler(0, inputYaw, 0);
                desiredForward = math.mul(inputRot, new float3(0, 0, 1));
            }
            
            if (math.lengthsq(desiredForward) < 0.01f)
                return;

            // Calculate target rotation
            quaternion targetRot = quaternion.LookRotationSafe(desiredForward, math.up());

            // ARPG/TwinStick style: Instant snap to face movement direction
            // This gives the responsive feel of Diablo/Path of Exile
            transform.Rotation = targetRot;
        }

        /// <summary>
        /// Face the cursor/aim position (Twin-Stick style).
        /// Character always faces where the player is aiming.
        /// </summary>
        private void ApplyCursorDirectionFacing(ref LocalTransform transform, in PlayerInput input, float singletonCameraYaw)
        {
            // Use camera yaw from ParadigmSettings singleton
            // In twin-stick, the character faces the aim direction (where cursor points)
            float targetYaw = singletonCameraYaw;

            // Apply rotation to face aim direction
            quaternion targetRot = quaternion.Euler(0, math.radians(targetYaw), 0);

            // Instant facing for responsive twin-stick feel
            transform.Rotation = targetRot;
        }

    }
}
