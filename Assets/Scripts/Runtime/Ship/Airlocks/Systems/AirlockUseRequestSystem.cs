using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// Client-side system that generates airlock use requests when player interacts.
    /// Runs in PredictedSimulationSystemGroup for client prediction.
    /// </summary>
    /// <remarks>
    /// Implements Sub-Epic 3.1.2: Request Buffer + Client Prediction
    /// - When interact input pressed and prompt valid, appends AirlockUseRequest
    /// - Includes debounce to prevent multiple requests per frame
    /// - Server validates and may reject request
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct AirlockUseRequestSystem : ISystem
    {
        private ComponentLookup<Airlock> _airlockLookup;
        private ComponentLookup<AirlockInteractable> _interactableLookup;
        private ComponentLookup<AirlockLocked> _lockedLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            
            _airlockLookup = state.GetComponentLookup<Airlock>(true);
            _interactableLookup = state.GetComponentLookup<AirlockInteractable>(true);
            _lockedLookup = state.GetComponentLookup<AirlockLocked>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // System runs on both client and server:
            // - Client: adds requests for prediction
            // - Server: adds requests to be processed by AirlockCycleSystem

            _airlockLookup.Update(ref state);
            _interactableLookup.Update(ref state);
            _lockedLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.TickIndexForValidTick;

            // Collect airlocks for distance checks - use LocalToWorld for world-space position
            // Store StableId for cross-world entity identification
            var airlockEntities = new NativeList<Entity>(Allocator.Temp);
            var airlockPositions = new NativeList<float3>(Allocator.Temp);
            var airlockStableIds = new NativeList<int>(Allocator.Temp);

            foreach (var (localToWorld, airlock, entity) in
                     SystemAPI.Query<RefRO<LocalToWorld>, RefRO<Airlock>>()
                     .WithAll<AirlockInteractable>()
                     .WithEntityAccess())
            {
                airlockEntities.Add(entity);
                airlockPositions.Add(localToWorld.ValueRO.Position);
                airlockStableIds.Add(airlock.ValueRO.StableId);
            }

            // Process all players with input - works on both client and server
            // Client: processes local player input (predicted)
            // Server: processes all players who have input (authoritative)
            foreach (var (playerState, playerTransform, playerInput, debounce, requestBuffer, entity) in
                     SystemAPI.Query<RefRO<PlayerState>, RefRO<LocalTransform>, RefRO<PlayerInput>, 
                                     RefRW<AirlockInteractDebounce>, DynamicBuffer<AirlockUseRequest>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Check if interact button is pressed (using interaction input)
                bool interactPressed = playerInput.ValueRO.Interact.IsSet;



                if (!interactPressed)
                    continue;



                // Debounce check
                ref var debounceState = ref debounce.ValueRW;
                if (currentTick - debounceState.LastRequestTick < debounceState.DebounceTickCount)
                {

                    continue;
                }

                // Check player is in valid mode for airlock use
                PlayerMode mode = playerState.ValueRO.Mode;
                if (mode != PlayerMode.EVA && mode != PlayerMode.InShip)
                {

                    continue;
                }

                // Check if player is already transitioning
                if (SystemAPI.HasComponent<AirlockTransitionPending>(entity))
                {

                    continue;
                }

                // Find best airlock in range
                float3 playerPos = playerTransform.ValueRO.Position;
                Entity bestAirlock = Entity.Null;
                float bestDistance = float.MaxValue;



                for (int i = 0; i < airlockEntities.Length; i++)
                {
                    Entity airlockEntity = airlockEntities[i];
                    float3 airlockPos = airlockPositions[i];

                    if (!_interactableLookup.HasComponent(airlockEntity))
                    {

                        continue;
                    }
                    if (!_airlockLookup.HasComponent(airlockEntity))
                    {

                        continue;
                    }

                    var interactable = _interactableLookup[airlockEntity];
                    var airlock = _airlockLookup[airlockEntity];

                    float distance = math.distance(playerPos, airlockPos);



                    // Check range
                    if (distance > interactable.Range)
                    {

                        continue;
                    }

                    // Check if airlock is available
                    if (_lockedLookup.HasComponent(airlockEntity))
                    {

                        continue;
                    }
                    if (airlock.State != AirlockState.Idle)
                    {

                        continue;
                    }
                    if (airlock.CurrentUser != Entity.Null)
                    {

                        continue;
                    }

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestAirlock = airlockEntity;

                    }
                }

                if (bestAirlock == Entity.Null)
                {

                    continue;
                }

                // Find the stable ID for the best airlock
                int bestIndex = -1;
                for (int i = 0; i < airlockEntities.Length; i++)
                {
                    if (airlockEntities[i] == bestAirlock)
                    {
                        bestIndex = i;
                        break;
                    }
                }

                if (bestIndex < 0)
                {
                    UnityEngine.Debug.Log($"[AirlockUseRequest] Could not find stable ID for best airlock!");
                    continue;
                }

                int bestStableId = airlockStableIds[bestIndex];

                // Determine direction based on player mode
                AirlockDirection direction = mode == PlayerMode.EVA
                    ? AirlockDirection.EnterShip
                    : AirlockDirection.ExitShip;

                // Append request to buffer using StableId for cross-world identification
                requestBuffer.Add(new AirlockUseRequest
                {
                    AirlockStableId = bestStableId,
                    Direction = direction,
                    ClientTick = currentTick
                });

                UnityEngine.Debug.Log($"[AirlockUseRequest] *** REQUEST ADDED! StableId={bestStableId}, Direction={direction} ***");

                // Update debounce
                debounceState.LastRequestTick = currentTick;
            }

            airlockEntities.Dispose();
            airlockPositions.Dispose();
            airlockStableIds.Dispose();
        }
    }
}
