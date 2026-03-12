using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.Stations
{
    /// <summary>
    /// Client-side system that displays interaction prompts for nearby stations.
    /// Shows "Press E to operate" or "Press E to exit" based on player state.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    
    public partial struct StationPromptSystem : ISystem
    {
        private ComponentLookup<OperableStation> _stationLookup;
        private ComponentLookup<StationInteractable> _interactableLookup;
        private ComponentLookup<StationDisabled> _disabledLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();

            _stationLookup = state.GetComponentLookup<OperableStation>(true);
            _interactableLookup = state.GetComponentLookup<StationInteractable>(true);
            _disabledLookup = state.GetComponentLookup<StationDisabled>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _stationLookup.Update(ref state);
            _interactableLookup.Update(ref state);
            _disabledLookup.Update(ref state);
            _transformLookup.Update(ref state);

            // Collect all station entities with positions
            var stationEntities = new NativeList<Entity>(Allocator.Temp);
            var stationPositions = new NativeList<float3>(Allocator.Temp);

            foreach (var (transform, station, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<OperableStation>>()
                     .WithEntityAccess())
            {
                stationEntities.Add(entity);
                stationPositions.Add(transform.ValueRO.Position);
            }

            // Process local player
            foreach (var (playerState, transform, promptState, entity) in
                     SystemAPI.Query<RefRO<PlayerState>, RefRO<LocalTransform>, RefRW<StationPromptState>>()
                     .WithAll<GhostOwnerIsLocal>()
                     .WithEntityAccess())
            {
                float3 playerPos = transform.ValueRO.Position;
                PlayerMode playerMode = playerState.ValueRO.Mode;

                // Check if player is already operating a station
                bool isOperating = SystemAPI.HasComponent<OperatingStation>(entity);
                
                ref var prompt = ref promptState.ValueRW;

                if (isOperating)
                {
                    // Show "Press E to exit" prompt
                    var operating = SystemAPI.GetComponent<OperatingStation>(entity);
                    
                    if (_interactableLookup.HasComponent(operating.StationEntity))
                    {
                        var interactable = _interactableLookup[operating.StationEntity];
                        prompt.TargetStation = operating.StationEntity;
                        prompt.PromptText = interactable.PromptExit;
                        prompt.IsPromptVisible = true;
                        prompt.CanInteract = true;
                        prompt.Distance = 0f;
                    }
                    else
                    {
                        prompt.TargetStation = operating.StationEntity;
                        prompt.PromptText = new FixedString64Bytes("Press E: Exit Station");
                        prompt.IsPromptVisible = true;
                        prompt.CanInteract = true;
                        prompt.Distance = 0f;
                    }
                }
                else
                {
                    // Find best nearby station
                    Entity bestStation = Entity.Null;
                    float bestDistance = float.MaxValue;

                    for (int i = 0; i < stationEntities.Length; i++)
                    {
                        Entity stationEntity = stationEntities[i];
                        float3 stationPos = stationPositions[i];

                        if (!_stationLookup.HasComponent(stationEntity))
                            continue;

                        var station = _stationLookup[stationEntity];
                        float distance = math.distance(playerPos, stationPos);

                        if (distance <= station.Range && distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestStation = stationEntity;
                        }
                    }

                    if (bestStation == Entity.Null)
                    {
                        // No station in range
                        prompt.TargetStation = Entity.Null;
                        prompt.IsPromptVisible = false;
                        prompt.CanInteract = false;
                        prompt.PromptText = default;
                        prompt.Distance = 0f;
                    }
                    else
                    {
                        var station = _stationLookup[bestStation];
                        bool isDisabled = _disabledLookup.HasComponent(bestStation);
                        bool isOccupied = station.CurrentOperator != Entity.Null;

                        prompt.TargetStation = bestStation;
                        prompt.Distance = bestDistance;
                        prompt.IsPromptVisible = true;

                        // Determine prompt text
                        if (_interactableLookup.HasComponent(bestStation))
                        {
                            var interactable = _interactableLookup[bestStation];

                            if (isDisabled)
                            {
                                prompt.PromptText = interactable.PromptDisabled;
                                prompt.CanInteract = false;
                            }
                            else if (isOccupied)
                            {
                                prompt.PromptText = interactable.PromptOccupied;
                                prompt.CanInteract = false;
                            }
                            else if (playerMode != PlayerMode.InShip)
                            {
                                // Player must be InShip to operate stations
                                prompt.IsPromptVisible = false;
                                prompt.CanInteract = false;
                            }
                            else
                            {
                                prompt.PromptText = interactable.PromptEnter;
                                prompt.CanInteract = true;
                            }
                        }
                        else
                        {
                            // Default prompts
                            if (isDisabled)
                            {
                                prompt.PromptText = new FixedString64Bytes("Station Disabled");
                                prompt.CanInteract = false;
                            }
                            else if (isOccupied)
                            {
                                prompt.PromptText = new FixedString64Bytes("Station Occupied");
                                prompt.CanInteract = false;
                            }
                            else if (playerMode != PlayerMode.InShip)
                            {
                                prompt.IsPromptVisible = false;
                                prompt.CanInteract = false;
                            }
                            else
                            {
                                prompt.PromptText = new FixedString64Bytes("Press E: Operate Station");
                                prompt.CanInteract = true;
                            }
                        }
                    }
                }
            }

            stationEntities.Dispose();
            stationPositions.Dispose();
        }
    }
}
