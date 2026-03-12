using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 7: Manages cooperative interaction lifecycle.
    ///
    /// Handles:
    /// - Counting occupied slots and updating CurrentPlayers/AllPlayersReady
    /// - Detecting player Use input for ready confirmation
    /// - Simultaneous mode: checking ReadyTimestamp spread within SyncTolerance
    /// - Sequential mode: advancing CurrentSequenceSlot when players confirm in order
    /// - Parallel mode: ticking ChannelProgress while all players are ready
    /// - Cancel handling: resetting state when players leave
    /// - Position sync: moving slotted players to their SlotPosition
    ///
    /// Join/leave is handled by InteractAbilitySystem hooks in TriggerInteractionEffect
    /// and CancelInteraction. This system processes the post-join state machine.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InteractAbilitySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct CoopInteractionSystem : ISystem
    {
        private ComponentLookup<PlayerInput> _playerInputLookup;
        private ComponentLookup<CoopParticipantState> _participantLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CoopInteraction>();
            _playerInputLookup = state.GetComponentLookup<PlayerInput>(true);
            _participantLookup = state.GetComponentLookup<CoopParticipantState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _playerInputLookup.Update(ref state);
            _participantLookup.Update(ref state);
            _transformLookup.Update(ref state);

            float deltaTime = SystemAPI.Time.DeltaTime;
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (coop, coopTransform, coopEntity) in
                     SystemAPI.Query<RefRW<CoopInteraction>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Skip if already complete or failed
                if (coop.ValueRO.CoopComplete || coop.ValueRO.CoopFailed)
                    continue;

                var slots = SystemAPI.GetBuffer<CoopSlot>(coopEntity);

                // --- Count occupied slots and check ready state ---
                int occupiedCount = 0;
                int readyCount = 0;

                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].IsOccupied)
                    {
                        occupiedCount++;

                        // Validate player still exists
                        if (!_participantLookup.HasComponent(slots[i].PlayerEntity))
                        {
                            // Player entity gone — clear slot
                            var slot = slots[i];
                            slot.IsOccupied = false;
                            slot.PlayerEntity = Entity.Null;
                            slot.IsReady = false;
                            slots[i] = slot;
                            occupiedCount--;
                            continue;
                        }

                        if (slots[i].IsReady)
                            readyCount++;
                    }
                }

                coop.ValueRW.CurrentPlayers = occupiedCount;
                coop.ValueRW.AllPlayersReady = occupiedCount >= coop.ValueRO.RequiredPlayers;

                // --- Detect Use input for ready confirmation ---
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i].IsOccupied || slots[i].IsReady)
                        continue;

                    Entity playerEntity = slots[i].PlayerEntity;
                    if (!_playerInputLookup.HasComponent(playerEntity))
                        continue;

                    var playerInput = _playerInputLookup[playerEntity];
                    if (playerInput.Use.IsSet)
                    {
                        var slot = slots[i];
                        slot.IsReady = true;
                        slot.ReadyTimestamp = elapsedTime;
                        slots[i] = slot;

                        if (_participantLookup.HasComponent(playerEntity))
                        {
                            var participant = _participantLookup.GetRefRW(playerEntity);
                            participant.ValueRW.HasConfirmed = true;
                        }
                    }
                }

                // --- Position sync: move slotted players to their positions ---
                float3 coopPos = coopTransform.ValueRO.Position;
                quaternion coopRot = coopTransform.ValueRO.Rotation;

                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i].IsOccupied)
                        continue;

                    Entity playerEntity = slots[i].PlayerEntity;
                    if (!_transformLookup.HasComponent(playerEntity))
                        continue;

                    float3 worldSlotPos = coopPos +
                        math.mul(coopRot, slots[i].SlotPosition);
                    quaternion worldSlotRot = math.mul(coopRot, slots[i].SlotRotation);

                    var playerTransform = _transformLookup.GetRefRW(playerEntity);
                    playerTransform.ValueRW.Position = worldSlotPos;
                    playerTransform.ValueRW.Rotation = worldSlotRot;
                }

                // --- Mode-specific processing ---
                if (!coop.ValueRO.AllPlayersReady)
                {
                    // Not enough players yet — reset mode-specific progress
                    coop.ValueRW.ChannelProgress = 0;
                    continue;
                }

                // Recount ready for mode logic
                readyCount = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].IsOccupied && slots[i].IsReady)
                        readyCount++;
                }

                switch (coop.ValueRO.Mode)
                {
                    case CoopMode.Simultaneous:
                        ProcessSimultaneous(ref coop.ValueRW, ref slots, readyCount, elapsedTime);
                        break;

                    case CoopMode.Sequential:
                        ProcessSequential(ref coop.ValueRW, ref slots);
                        break;

                    case CoopMode.Parallel:
                    case CoopMode.Asymmetric:
                        ProcessParallel(ref coop.ValueRW, ref slots, readyCount, deltaTime);
                        break;
                }
            }
        }

        private static void ProcessSimultaneous(ref CoopInteraction coop,
            ref DynamicBuffer<CoopSlot> slots, int readyCount, float elapsedTime)
        {
            if (readyCount < coop.RequiredPlayers)
                return;

            // All players have pressed ready — check timestamp spread
            float minTime = float.MaxValue;
            float maxTime = float.MinValue;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsOccupied && slots[i].IsReady)
                {
                    if (slots[i].ReadyTimestamp < minTime)
                        minTime = slots[i].ReadyTimestamp;
                    if (slots[i].ReadyTimestamp > maxTime)
                        maxTime = slots[i].ReadyTimestamp;
                }
            }

            float spread = maxTime - minTime;

            if (spread <= coop.SyncTolerance)
            {
                // Within sync tolerance — success!
                coop.CoopComplete = true;
            }
            else
            {
                // Exceeded tolerance — fail and reset ready flags
                coop.CoopFailed = true;
                ResetReadyFlags(ref slots);
            }
        }

        private static void ProcessSequential(ref CoopInteraction coop,
            ref DynamicBuffer<CoopSlot> slots)
        {
            int currentSlotIdx = coop.CurrentSequenceSlot;

            // Find the slot with this index
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].SlotIndex == currentSlotIdx && slots[i].IsOccupied)
                {
                    if (slots[i].IsReady)
                    {
                        // Current slot confirmed — advance to next
                        coop.CurrentSequenceSlot++;

                        if (coop.CurrentSequenceSlot >= coop.RequiredPlayers)
                        {
                            // All slots completed in sequence — success!
                            coop.CoopComplete = true;
                        }
                    }
                    break;
                }
            }
        }

        private static void ProcessParallel(ref CoopInteraction coop,
            ref DynamicBuffer<CoopSlot> slots, int readyCount, float deltaTime)
        {
            if (readyCount < coop.RequiredPlayers)
            {
                // Not all players channeling — reset progress
                coop.ChannelProgress = 0;
                return;
            }

            // All players are channeling — tick progress
            if (coop.ChannelDuration > 0)
            {
                coop.ChannelProgress += deltaTime / coop.ChannelDuration;
                if (coop.ChannelProgress >= 1f)
                {
                    coop.ChannelProgress = 1f;
                    coop.CoopComplete = true;
                }
            }
            else
            {
                // No duration — instant complete when all ready
                coop.ChannelProgress = 1f;
                coop.CoopComplete = true;
            }
        }

        private static void ResetReadyFlags(ref DynamicBuffer<CoopSlot> slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsOccupied)
                {
                    var slot = slots[i];
                    slot.IsReady = false;
                    slot.ReadyTimestamp = 0;
                    slots[i] = slot;
                }
            }
        }
    }
}
