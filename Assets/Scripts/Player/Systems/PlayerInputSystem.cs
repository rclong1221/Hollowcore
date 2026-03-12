using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Player.Components;
using DIG.Core.Input;
using DIG.Shared;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Player.Systems
{
    // Writes the runtime PlayerInputState into all entities that have Player.Components.PlayerInputComponent.
    // [UpdateInGroup(typeof(SimulationSystemGroup))]
    // public partial class PlayerInputWriterSystem : SystemBase
    // {
    //     protected override void OnUpdate()
    //     {
    //         // Update all entities that expect player input (simple approach)
    //         var query = GetEntityQuery(ComponentType.ReadWrite<PlayerInputComponent>());
    //         var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
    //         var em = EntityManager;
    //         foreach (var ent in entities)
    //         {
    //             var input = em.GetComponentData<PlayerInputComponent>(ent);
    //             input.Move = PlayerInputState.Move;
    //             input.LookDelta = PlayerInputState.LookDelta;
    //             input.ZoomDelta = PlayerInputState.ZoomDelta;
    //             input.Jump = (byte)(PlayerInputState.Jump ? 1 : 0);
    //             input.Crouch = (byte)(PlayerInputState.Crouch ? 1 : 0);
    //             input.Sprint = (byte)(PlayerInputState.Sprint ? 1 : 0);
    //             input.LeanLeft = (byte)(PlayerInputState.LeanLeft ? 1 : 0);
    //             input.LeanRight = (byte)(PlayerInputState.LeanRight ? 1 : 0);
    //             input.DodgeRoll = (byte)(PlayerInputState.DodgeRoll ? 1 : 0);
    //             input.DodgeDive = (byte)(PlayerInputState.DodgeDive ? 1 : 0);
    //             input.Prone = (byte)(PlayerInputState.Prone ? 1 : 0);
    //             input.Slide = (byte)(PlayerInputState.Slide ? 1 : 0);
    //             em.SetComponentData(ent, input);
    //         }
    //         entities.Dispose();
    //     }
    // }

    /// <summary>
    /// Decodes the networked PlayerInput (IInputComponentData) into the local PlayerInputComponent
    /// used by the CharacterControllerSystem. This ensures inputs are available on the Server
    /// and predicted correctly on the Client.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(CharacterControllerSystem))]
    public partial struct PlayerInputDecodeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (netInput, logicInput) in SystemAPI.Query<RefRO<PlayerInput>, RefRW<PlayerInputComponent>>().WithAll<Simulate>())
            {
                var ni = netInput.ValueRO;

                // EPIC 15.20 Phase 3: Use continuous path direction when path-following
                if (ni.IsPathFollowing != 0)
                    logicInput.ValueRW.Move = new float2(ni.PathMoveX, ni.PathMoveY);
                else
                    logicInput.ValueRW.Move = new float2((float)ni.Horizontal, (float)ni.Vertical);
                logicInput.ValueRW.LookDelta = ni.LookDelta;
                logicInput.ValueRW.ZoomDelta = ni.ZoomDelta;
                logicInput.ValueRW.Jump = (byte)(ni.Jump.IsSet ? 1 : 0);
                logicInput.ValueRW.Crouch = (byte)(ni.Crouch.IsSet ? 1 : 0);
                logicInput.ValueRW.Sprint = (byte)(ni.Sprint.IsSet ? 1 : 0);
                logicInput.ValueRW.Slide = (byte)(ni.Slide.IsSet ? 1 : 0);
                logicInput.ValueRW.DodgeRoll = (byte)(ni.DodgeRoll.IsSet ? 1 : 0);
                logicInput.ValueRW.DodgeDive = (byte)(ni.DodgeDive.IsSet ? 1 : 0);
                logicInput.ValueRW.Prone = (byte)(ni.Prone.IsSet ? 1 : 0);
                logicInput.ValueRW.LeanLeft = (byte)(ni.LeanLeft.IsSet ? 1 : 0);
                logicInput.ValueRW.LeanRight = (byte)(ni.LeanRight.IsSet ? 1 : 0);
            }
        }
    }

    /// <summary>
    /// Samples player input from keyboard/mouse and writes to PlayerInput component
    /// Runs in GhostInputSystemGroup for proper netcode prediction/rollback
    /// NOTE: Uses SystemBase (not ISystem) because Input System requires main thread access
    /// </summary>
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class PlayerInputSystem : SystemBase
    {
        private uint _frameCount;
        private bool _hasResetInput;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<NetworkStreamInGame>();
            RequireForUpdate<PlayerSpawner>();
            _frameCount = 0;
            _hasResetInput = false;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            // Clear all stale input state when transitioning to gameplay.
            // Without this, held-action flags (Jump, Sprint, Crouch, DodgeDive, etc.)
            // can persist from lobby/UI because action map Disable() prevents the
            // 'canceled' callback from firing, leaving static fields stuck at true.
            // This causes non-host players to lunge/dive/jump/switch weapons on spawn.
            PlayerInputState.ResetAll();
            _hasResetInput = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            // Allow reset to fire again if system restarts (e.g., reconnection)
            _hasResetInput = false;
        }

        protected override void OnUpdate()
        {
            _frameCount++;
            // Only sample input for local player entities
            foreach (var (playerInput, entity) in SystemAPI.Query<RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>().WithEntityAccess())
            {
                // Reset input to default state
                playerInput.ValueRW = default;
                playerInput.ValueRW.FrameCount = _frameCount;

                // Try to capture camera yaw if available
                if (EntityManager.HasComponent<PlayerCameraSettings>(entity))
                {
                    var cameraSettings = EntityManager.GetComponentData<PlayerCameraSettings>(entity);
                    playerInput.ValueRW.CameraYaw = cameraSettings.Yaw;
                    playerInput.ValueRW.CameraPitch = cameraSettings.Pitch;
                    playerInput.ValueRW.CameraDistance = cameraSettings.CurrentDistance;
                    playerInput.ValueRW.CameraYawValid = 1;
                }
                else
                {
                    playerInput.ValueRW.CameraYaw = 0f;
                    playerInput.ValueRW.CameraPitch = 0f;
                    playerInput.ValueRW.CameraYawValid = 0;
                }

                // Capture lock-on state for server to apply rotation (Epic 15.16)
                if (EntityManager.HasComponent<Player.Components.CameraTargetLockState>(entity))
                {
                    var lockState = EntityManager.GetComponentData<Player.Components.CameraTargetLockState>(entity);
                    if (lockState.IsLocked && math.lengthsq(lockState.LastTargetPosition) > 1f)
                    {
                        playerInput.ValueRW.LockTargetPosition = lockState.LastTargetPosition;
                        playerInput.ValueRW.IsLockedOn = 1;
                    }
                    else
                    {
                        playerInput.ValueRW.LockTargetPosition = float3.zero;
                        playerInput.ValueRW.IsLockedOn = 0;
                    }
                }
                else
                {
                    playerInput.ValueRW.LockTargetPosition = float3.zero;
                    playerInput.ValueRW.IsLockedOn = 0;
                }

                // Capture hand socket position for accurate throwable spawning (client → server)
                if (EntityManager.HasComponent<SocketPositionData>(entity))
                {
                    var socketData = EntityManager.GetComponentData<SocketPositionData>(entity);
                    if (socketData.IsValid)
                    {
                        playerInput.ValueRW.MainHandPosition = socketData.MainHandPosition;
                        playerInput.ValueRW.MainHandPositionValid = 1;
                    }
                    else
                    {
                        playerInput.ValueRW.MainHandPosition = float3.zero;
                        playerInput.ValueRW.MainHandPositionValid = 0;
                    }
                }
                else
                {
                    playerInput.ValueRW.MainHandPosition = float3.zero;
                    playerInput.ValueRW.MainHandPositionValid = 0;
                }

                // EPIC 18.19 Phase 8: Copy cursor aim direction for server replication.
                // CursorAimTargetingSystem (client-only, SimulationSystemGroup) writes TargetData.AimDirection.
                // The server needs this to fire weapons in the correct direction.
                if (EntityManager.HasComponent<DIG.Targeting.TargetData>(entity))
                {
                    var td = EntityManager.GetComponentData<DIG.Targeting.TargetData>(entity);
                    if (td.Mode == DIG.Targeting.TargetingMode.CursorAim && math.lengthsq(td.AimDirection) > 0.001f)
                    {
                        playerInput.ValueRW.CursorAimDirection = td.AimDirection;
                        playerInput.ValueRW.CursorAimValid = 1;
                    }
                    else
                    {
                        playerInput.ValueRW.CursorAimDirection = float3.zero;
                        playerInput.ValueRW.CursorAimValid = 0;
                    }
                }

                // Sample input from PlayerInputState (populated by PlayerInputReader)
                SampleInput(ref playerInput.ValueRW, _frameCount);
            }
        }

    #if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// Sample all input from PlayerInputState (populated by PlayerInputReader from Input Action callbacks).
        /// This approach decouples the ECS system from hardware, enabling gamepad support and rebinding.
        /// </summary>
        private void SampleInput(ref PlayerInput input, uint frameCount)
        {
            // ===== MOVEMENT (from PlayerInputState, populated by PlayerInputReader) =====
            // EPIC 18.15: Gate WASD input based on paradigm settings.
            // MOBA/ARPG profiles have wasdEnabled=false — only path following moves the character.
            bool wasdEnabled = MovementRouter.Instance == null || MovementRouter.Instance.IsWASDEnabled;
            if (wasdEnabled)
            {
                input.Horizontal = (sbyte)math.round(PlayerInputState.Move.x);
                input.Vertical = (sbyte)math.round(PlayerInputState.Move.y);
            }
            else
            {
                input.Horizontal = 0;
                input.Vertical = 0;
            }

            // EPIC 15.20 Phase 3: Path following direction (continuous float, not quantized)
            if (PlayerInputState.IsPathFollowing)
            {
                input.PathMoveX = PlayerInputState.PathMoveDirection.x;
                input.PathMoveY = PlayerInputState.PathMoveDirection.y;
                input.IsPathFollowing = 1;
            }

            // ===== ACTIONS (from PlayerInputState) =====
            if (PlayerInputState.Jump)
                input.Jump = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.DodgeDive)
            {
                input.DodgeDive = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.DodgeDive = false; // Consume — latched by PlayerInputReader
            }

            if (PlayerInputState.DodgeRoll)
            {
                input.DodgeRoll = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.DodgeRoll = false; // Consume — latched by PlayerInputReader
            }

            if (PlayerInputState.Crouch)
                input.Crouch = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.Sprint)
                input.Sprint = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.Prone)
            {
                input.Prone = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.Prone = false; // Consume — latched by PlayerInputReader
            }

            if (PlayerInputState.Slide)
            {
                input.Slide = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.Slide = false; // Consume — latched by PlayerInputReader
            }

            // ===== CAMERA (from PlayerInputState) =====
            input.LookDelta = PlayerInputState.LookDelta;
            input.ZoomDelta = PlayerInputState.ZoomDelta;

            // NOTE: Cursor locking is now handled by PlayerInputReader via Input Actions
            // The legacy UnityEngine.Input API is disabled when using Input System only mode

            // ===== LEAN (from PlayerInputState) =====
            if (PlayerInputState.LeanLeft)
                input.LeanLeft = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
            if (PlayerInputState.LeanRight)
                input.LeanRight = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            // ===== EQUIPMENT (from DIGEquipmentProvider via PlayerInputState) =====
            if (PlayerInputState.PendingEquipSlot >= 0 && PlayerInputState.PendingEquipQuickSlot > 0)
            {
                input.EquipSlotId = PlayerInputState.PendingEquipSlot;
                input.EquipQuickSlot = PlayerInputState.PendingEquipQuickSlot;
                PlayerInputState.ConsumePendingEquip();
            }
            else
            {
                input.EquipSlotId = -1;
                input.EquipQuickSlot = 0;
            }

            // ===== COMBAT/INTERACTION (from PlayerInputState via Input Actions) =====
            if (PlayerInputState.Fire)
                input.Use = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.Aim || PlayerInputState.CameraOrbit)
                input.AltUse = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.Interact)
                input.Interact = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.Reload)
                input.Reload = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.ToggleFlashlight)
            {
                input.ToggleFlashlight = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.ToggleFlashlight = false; // Consume — latched by PlayerInputReader
            }

            if (PlayerInputState.Grab)
                input.Grab = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.FreeLook)
                input.FreeLook = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            // ===== MMO AUTO-RUN (EPIC 15.21) =====
            if (PlayerInputState.AutoRun)
            {
                input.AutoRun = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.AutoRun = false; // Consume — latched by PlayerInputReader
            }

            // ===== MMO STRAFE (EPIC 15.21) =====
            if (PlayerInputState.MMOStrafeLeft)
                input.MMOStrafeLeft = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
            if (PlayerInputState.MMOStrafeRight)
                input.MMOStrafeRight = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
        }
    #else
        /// <summary>
        /// Fallback: Sample all input from PlayerInputState when the new Input System package is not installed.
        /// The PlayerInputState should still be populated by some mechanism (e.g., a MonoBehaviour fallback).
        /// </summary>
        private void SampleInput(ref PlayerInput input, uint frameCount)
        {
            // ===== MOVEMENT =====
            // EPIC 18.15: Gate WASD input based on paradigm settings.
            bool wasdEnabled = MovementRouter.Instance == null || MovementRouter.Instance.IsWASDEnabled;
            if (wasdEnabled)
            {
                input.Horizontal = (sbyte)math.round(PlayerInputState.Move.x);
                input.Vertical = (sbyte)math.round(PlayerInputState.Move.y);
            }
            else
            {
                input.Horizontal = 0;
                input.Vertical = 0;
            }

            // EPIC 15.20 Phase 3: Path following direction (continuous float, not quantized)
            if (PlayerInputState.IsPathFollowing)
            {
                input.PathMoveX = PlayerInputState.PathMoveDirection.x;
                input.PathMoveY = PlayerInputState.PathMoveDirection.y;
                input.IsPathFollowing = 1;
            }

            // ===== ACTIONS =====
            if (PlayerInputState.Jump)
                input.Jump = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.DodgeDive)
            {
                input.DodgeDive = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.DodgeDive = false; // Consume — latched by PlayerInputReader
            }

            if (PlayerInputState.DodgeRoll)
            {
                input.DodgeRoll = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.DodgeRoll = false; // Consume — latched by PlayerInputReader
            }

            if (PlayerInputState.Crouch)
                input.Crouch = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.Sprint)
                input.Sprint = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.Prone)
            {
                input.Prone = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.Prone = false; // Consume — latched by PlayerInputReader
            }

            if (PlayerInputState.Slide)
            {
                input.Slide = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.Slide = false; // Consume — latched by PlayerInputReader
            }

            // ===== CAMERA =====
            input.LookDelta = PlayerInputState.LookDelta;
            input.ZoomDelta = PlayerInputState.ZoomDelta;

            // ===== LEAN =====
            if (PlayerInputState.LeanLeft)
                input.LeanLeft = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
            if (PlayerInputState.LeanRight)
                input.LeanRight = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            // ===== EQUIPMENT =====
            if (PlayerInputState.PendingEquipSlot >= 0 && PlayerInputState.PendingEquipQuickSlot > 0)
            {
                input.EquipSlotId = PlayerInputState.PendingEquipSlot;
                input.EquipQuickSlot = PlayerInputState.PendingEquipQuickSlot;
                PlayerInputState.ConsumePendingEquip();
            }
            else
            {
                input.EquipSlotId = -1;
                input.EquipQuickSlot = 0;
            }

            // ===== COMBAT/INTERACTION =====
            if (PlayerInputState.Fire)
                input.Use = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.Aim)
                input.AltUse = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.Interact)
                input.Interact = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.Reload)
                input.Reload = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.ToggleFlashlight)
            {
                input.ToggleFlashlight = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
                PlayerInputState.ToggleFlashlight = false; // Consume — latched by PlayerInputReader
            }

            if (PlayerInputState.Grab)
                input.Grab = new InputEvent { IsSetByte = 1, FrameCount = frameCount };

            if (PlayerInputState.FreeLook)
                input.FreeLook = new InputEvent { IsSetByte = 1, FrameCount = frameCount };
        }
    #endif
    }
}
