using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 4: Manages mount/seat lifecycle.
    ///
    /// Handles:
    /// - Mount/dismount transitions (lerp to seat, timed transition)
    /// - Ability locking during mount (BlockedAbilitiesMask)
    /// - Position sync while mounted (player tracks mount seat)
    /// - Dismount via interact/cancel input
    /// - Ladder movement (up/down along local Y axis)
    /// - Zipline movement (auto-forward along local Z axis)
    /// - Graceful cleanup if mount entity is destroyed
    ///
    /// Runs AFTER InteractAbilitySystem so mount entry is already set by TriggerInteractionEffect.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InteractAbilitySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct MountSystem : ISystem
    {
        private const int AllAbilitiesBlockedMask = 0xFF;
        private ComponentLookup<PlayerInput> _playerInputLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MountState>();
            _playerInputLookup = state.GetComponentLookup<PlayerInput>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _playerInputLookup.Update(ref state);
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (mountState, ability, request, transform, entity) in
                     SystemAPI.Query<RefRW<MountState>, RefRW<InteractAbility>,
                                     RefRW<InteractRequest>, RefRW<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Skip if not mounted and not transitioning
                if (!mountState.ValueRO.IsMounted && !mountState.ValueRO.IsTransitioning)
                    continue;

                Entity mountEntity = mountState.ValueRO.MountedOn;

                // --- Validate mount still exists ---
                if (mountEntity == Entity.Null || !SystemAPI.HasComponent<MountPoint>(mountEntity))
                {
                    // Mount destroyed — graceful cleanup
                    CleanupMount(ref mountState.ValueRW, ref ability.ValueRW);
                    continue;
                }

                var mountPoint = SystemAPI.GetComponent<MountPoint>(mountEntity);

                // --- Handle transition phase ---
                if (mountState.ValueRO.IsTransitioning)
                {
                    float duration = mountPoint.MountTransitionDuration > 0
                        ? mountPoint.MountTransitionDuration : 0.5f;

                    mountState.ValueRW.TransitionProgress += deltaTime / duration;

                    if (mountState.ValueRO.TransitionProgress >= 1f)
                    {
                        mountState.ValueRW.TransitionProgress = 1f;
                        mountState.ValueRW.IsTransitioning = false;

                        if (!mountState.ValueRO.IsMounted)
                        {
                            // Mounting complete → enter mounted state
                            mountState.ValueRW.IsMounted = true;

                            // Lock abilities
                            mountState.ValueRW.PreviousBlockedAbilitiesMask =
                                ability.ValueRO.BlockedAbilitiesMask;
                            ability.ValueRW.BlockedAbilitiesMask = AllAbilitiesBlockedMask;
                        }
                        else
                        {
                            // Dismounting complete → exit mounted state
                            FinalizeDismount(ref state, ref mountState.ValueRW, ref ability.ValueRW,
                                ref transform.ValueRW, mountEntity, mountPoint);
                        }
                    }
                    else if (!mountState.ValueRO.IsMounted)
                    {
                        // During mount transition: lerp towards seat
                        if (SystemAPI.HasComponent<LocalTransform>(mountEntity))
                        {
                            var mountTransform = SystemAPI.GetComponent<LocalTransform>(mountEntity);
                            float3 seatPos = mountTransform.Position +
                                math.mul(mountTransform.Rotation, mountPoint.SeatOffset);
                            float t = math.saturate(mountState.ValueRO.TransitionProgress);
                            transform.ValueRW.Position = math.lerp(
                                transform.ValueRO.Position, seatPos, t);
                        }
                    }
                    continue;
                }

                // --- Mounted state: position sync ---
                if (SystemAPI.HasComponent<LocalTransform>(mountEntity))
                {
                    var mountTransform = SystemAPI.GetComponent<LocalTransform>(mountEntity);

                    // Handle special mount types
                    switch (mountState.ValueRO.ActiveMountType)
                    {
                        case MountType.Ladder:
                            ProcessLadder(ref mountState.ValueRW, ref transform.ValueRW,
                                mountTransform, mountPoint, entity, deltaTime);
                            break;

                        case MountType.Zipline:
                            bool reachedEnd = ProcessZipline(ref mountState.ValueRW,
                                ref transform.ValueRW, mountTransform, mountPoint, deltaTime);
                            if (reachedEnd)
                            {
                                StartDismount(ref mountState.ValueRW);
                                continue;
                            }
                            break;

                        default:
                            // Seat/Turret/Passenger: sync position to seat
                            float3 seatWorldPos = mountTransform.Position +
                                math.mul(mountTransform.Rotation, mountPoint.SeatOffset);
                            transform.ValueRW.Position = seatWorldPos;
                            transform.ValueRW.Rotation = math.mul(
                                mountTransform.Rotation, mountPoint.SeatRotation);
                            break;
                    }
                }

                // --- Check dismount conditions ---
                if (request.ValueRO.StartInteract)
                {
                    request.ValueRW.StartInteract = false;
                    StartDismount(ref mountState.ValueRW);
                }
                else if (request.ValueRO.CancelInteract)
                {
                    request.ValueRW.CancelInteract = false;
                    StartDismount(ref mountState.ValueRW);
                }
            }
        }

        private void ProcessLadder(ref MountState mountState, ref LocalTransform transform,
            LocalTransform mountTransform, MountPoint mountPoint, Entity playerEntity, float deltaTime)
        {
            // Read vertical input for ladder movement
            float verticalInput = 0;
            if (_playerInputLookup.HasComponent(playerEntity))
            {
                verticalInput = _playerInputLookup[playerEntity].Vertical;
            }

            // Move along ladder
            mountState.LadderOffset += verticalInput * mountPoint.LadderSpeed * deltaTime;
            mountState.LadderOffset = math.clamp(mountState.LadderOffset,
                mountPoint.LadderMinY, mountPoint.LadderMaxY);

            // Position player on ladder
            float3 ladderOffset = mountPoint.SeatOffset + new float3(0, mountState.LadderOffset, 0);
            transform.Position = mountTransform.Position +
                math.mul(mountTransform.Rotation, ladderOffset);
            transform.Rotation = math.mul(mountTransform.Rotation, mountPoint.SeatRotation);
        }

        /// <summary>
        /// Returns true if the zipline endpoint has been reached.
        /// </summary>
        private static bool ProcessZipline(ref MountState mountState, ref LocalTransform transform,
            LocalTransform mountTransform, MountPoint mountPoint, float deltaTime)
        {
            // Auto-move forward along mount's local Z axis
            mountState.LadderOffset += mountPoint.ZiplineSpeed * deltaTime;

            float3 ziplineOffset = mountPoint.SeatOffset + new float3(0, 0, mountState.LadderOffset);
            transform.Position = mountTransform.Position +
                math.mul(mountTransform.Rotation, ziplineOffset);
            transform.Rotation = math.mul(mountTransform.Rotation, mountPoint.SeatRotation);

            // Check if reached endpoint (LadderMaxY repurposed as max Z distance for ziplines)
            return mountPoint.LadderMaxY > 0 && mountState.LadderOffset >= mountPoint.LadderMaxY;
        }

        private static void StartDismount(ref MountState mountState)
        {
            mountState.IsTransitioning = true;
            mountState.TransitionProgress = 0;
        }

        private void FinalizeDismount(ref SystemState state, ref MountState mountState,
            ref InteractAbility ability, ref LocalTransform transform,
            Entity mountEntity, MountPoint mountPoint)
        {
            // Calculate dismount position
            if (SystemAPI.HasComponent<LocalTransform>(mountEntity))
            {
                var mountTransform = SystemAPI.GetComponent<LocalTransform>(mountEntity);
                float3 dismountWorldPos = mountTransform.Position +
                    math.mul(mountTransform.Rotation, mountPoint.DismountOffset);
                transform.Position = dismountWorldPos;
            }
            else
            {
                // Fallback: use pre-mount position
                transform.Position = mountState.PreMountPosition;
            }

            // Clear mount point occupancy
            if (SystemAPI.HasComponent<MountPoint>(mountEntity))
            {
                var mp = SystemAPI.GetComponentRW<MountPoint>(mountEntity);
                mp.ValueRW.IsOccupied = false;
                mp.ValueRW.OccupantEntity = Entity.Null;
            }

            // Clear MountInput if present
            if (SystemAPI.HasComponent<MountInput>(mountEntity))
            {
                var input = SystemAPI.GetComponentRW<MountInput>(mountEntity);
                input.ValueRW = default;
            }

            // Restore abilities
            ability.BlockedAbilitiesMask = mountState.PreviousBlockedAbilitiesMask;

            // Reset mount state
            mountState.IsMounted = false;
            mountState.MountedOn = Entity.Null;
            mountState.ActiveMountType = MountType.Seat;
            mountState.PreviousBlockedAbilitiesMask = 0;
            mountState.LadderOffset = 0;
        }

        private static void CleanupMount(ref MountState mountState, ref InteractAbility ability)
        {
            ability.BlockedAbilitiesMask = mountState.PreviousBlockedAbilitiesMask;
            mountState.IsMounted = false;
            mountState.IsTransitioning = false;
            mountState.MountedOn = Entity.Null;
            mountState.TransitionProgress = 0;
            mountState.ActiveMountType = MountType.Seat;
            mountState.PreviousBlockedAbilitiesMask = 0;
            mountState.LadderOffset = 0;
        }
    }
}
