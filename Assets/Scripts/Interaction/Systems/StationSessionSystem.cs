using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 2: Manages station session lifecycle.
    ///
    /// Handles:
    /// - Session exit via interact/cancel input
    /// - Distance-based auto-exit (MaxDistance)
    /// - Ability locking during session (BlockedAbilitiesMask)
    /// - Clearing station occupancy on exit
    ///
    /// Runs AFTER InteractAbilitySystem so session entry is already set by TriggerInteractionEffect.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InteractAbilitySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct StationSessionSystem : ISystem
    {
        private const int AllAbilitiesBlockedMask = 0xFF;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StationSessionState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (sessionState, ability, request, transform, entity) in
                     SystemAPI.Query<RefRW<StationSessionState>, RefRW<InteractAbility>,
                                     RefRW<InteractRequest>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                if (!sessionState.ValueRO.IsInSession)
                    continue;

                Entity stationEntity = sessionState.ValueRO.SessionEntity;

                // Validate station still exists
                if (stationEntity == Entity.Null ||
                    !SystemAPI.HasComponent<InteractionSession>(stationEntity))
                {
                    // Restore abilities and clear session
                    ability.ValueRW.BlockedAbilitiesMask = sessionState.ValueRO.PreviousBlockedAbilitiesMask;
                    sessionState.ValueRW.IsInSession = false;
                    sessionState.ValueRW.SessionEntity = Entity.Null;
                    sessionState.ValueRW.PreviousBlockedAbilitiesMask = 0;
                    continue;
                }

                var stationSession = SystemAPI.GetComponent<InteractionSession>(stationEntity);

                // Apply ability locking on first frame of session
                if (stationSession.LockAbilities && ability.ValueRO.BlockedAbilitiesMask != AllAbilitiesBlockedMask)
                {
                    sessionState.ValueRW.PreviousBlockedAbilitiesMask = ability.ValueRO.BlockedAbilitiesMask;
                    ability.ValueRW.BlockedAbilitiesMask = AllAbilitiesBlockedMask;
                }

                // --- Check exit conditions ---
                bool shouldExit = false;

                // Exit via interact press while in session
                if (request.ValueRO.StartInteract)
                {
                    request.ValueRW.StartInteract = false;
                    shouldExit = true;
                }

                // Exit via cancel
                if (!shouldExit && request.ValueRO.CancelInteract)
                {
                    request.ValueRW.CancelInteract = false;
                    shouldExit = true;
                }

                // Distance-based auto-exit
                if (!shouldExit && stationSession.MaxDistance > 0)
                {
                    if (SystemAPI.HasComponent<LocalTransform>(stationEntity))
                    {
                        var stationTransform = SystemAPI.GetComponent<LocalTransform>(stationEntity);
                        float distance = math.distance(transform.ValueRO.Position, stationTransform.Position);

                        if (distance > stationSession.MaxDistance)
                        {
                            shouldExit = true;
                        }
                    }
                }

                if (shouldExit)
                {
                    // Clear station occupancy
                    if (SystemAPI.HasComponent<InteractionSession>(stationEntity))
                    {
                        var stationRef = SystemAPI.GetComponentRW<InteractionSession>(stationEntity);
                        stationRef.ValueRW.IsOccupied = false;
                        stationRef.ValueRW.OccupantEntity = Entity.Null;
                    }

                    // Restore abilities and clear session
                    ability.ValueRW.BlockedAbilitiesMask = sessionState.ValueRO.PreviousBlockedAbilitiesMask;
                    sessionState.ValueRW.IsInSession = false;
                    sessionState.ValueRW.SessionEntity = Entity.Null;
                    sessionState.ValueRW.PreviousBlockedAbilitiesMask = 0;
                }
            }
        }
    }
}
