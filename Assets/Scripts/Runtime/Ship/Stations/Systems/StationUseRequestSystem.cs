using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.Stations
{
    /// <summary>
    /// Client-side system that creates station use requests when player interacts.
    /// Appends StationUseRequest to player buffer for server processing.
    /// </summary>
    /// <remarks>
    /// Updated to run on both Client (local player only) and Server (simulated players).
    /// </remarks>
    // [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)] // System runs on both by default
    [RequireMatchingQueriesForUpdate]
    
    public partial struct StationUseRequestSystem : ISystem
    {
        private ComponentLookup<OperableStation> _stationLookup;
        private ComponentLookup<StationDisabled> _disabledLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<DIG.Ship.LocalSpace.InShipLocalSpace> _inShipLookup;
        private ComponentLookup<LocalToWorld> _l2wLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();

            _stationLookup = state.GetComponentLookup<OperableStation>(true);
            _disabledLookup = state.GetComponentLookup<StationDisabled>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _inShipLookup = state.GetComponentLookup<DIG.Ship.LocalSpace.InShipLocalSpace>(true);
            _l2wLookup = state.GetComponentLookup<LocalToWorld>(true);
        }

        // [BurstCompile]  // Disable Burst to allow interpolated string logging
        public void OnUpdate(ref SystemState state)
        {
            // Ensure all previous jobs (writing to L2W etc) are finished
            state.Dependency.Complete();

            _stationLookup.Update(ref state);
            _disabledLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _inShipLookup.Update(ref state);
            _l2wLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();

            // Collect station data including Local Position and Parent Ship match
            var stationEntities = new NativeList<Entity>(Allocator.Temp);
            var stationLocalPositions = new NativeList<float3>(Allocator.Temp);
            var stationShipEntities = new NativeList<Entity>(Allocator.Temp);
            var stationStableIds = new NativeList<int>(Allocator.Temp);

            foreach (var (transform, station, entity) in
                        SystemAPI.Query<RefRO<LocalTransform>, RefRO<OperableStation>>()
                        .WithEntityAccess())
            {
                stationEntities.Add(entity);
                stationShipEntities.Add(station.ValueRO.ShipEntity);
                
                // Use LocalToWorld for correct World Space distance comparison
                // (Player LocalTransform.Position is World Space due to ShipInertialCorrectionSystem)
                if (_l2wLookup.HasComponent(entity))
                {
                    stationLocalPositions.Add(_l2wLookup[entity].Position);
                }
                else
                {
                    stationLocalPositions.Add(transform.ValueRO.Position); // Fallback (likely incorrect if parented)
                }

                stationStableIds.Add(station.ValueRO.StableId);
            }

            // Process Local Players (Client) and Simulated Players (Server)
            foreach (var (playerInput, playerTransform, playerState, requestBuffer, debounce, entity) in
                     SystemAPI.Query<RefRO<PlayerInput>, RefRO<LocalTransform>, RefRO<PlayerState>, DynamicBuffer<StationUseRequest>, RefRW<StationInteractDebounce>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Client: Only process local player to avoid ghost inputs
                if (!state.WorldUnmanaged.IsServer() && !SystemAPI.HasComponent<GhostOwnerIsLocal>(entity))
                    continue;

                // Server: Process all simulated players

                // Check for Interact input
                bool interactPressed = false;
                
                #if ENABLE_INPUT_SYSTEM
                interactPressed = playerInput.ValueRO.Interact.IsSet;
                #else
                interactPressed = playerInput.ValueRO.Interact.IsSet;
                #endif

                if (!interactPressed)
                    continue;

                var currentTick = SystemAPI.Time.ElapsedTime;
                
                // Only log detailed debug info on client to reduce server spam
                string prefix = state.WorldUnmanaged.IsServer() ? "[SERVER] " : "[CLIENT] ";
                // if (!state.WorldUnmanaged.IsServer())
                    UnityEngine.Debug.Log($"{prefix}[StationDebug] 'T' (Interact) Pressed by Entity {entity.Index}. Checking {stationEntities.Length} stations. Mode: {playerState.ValueRO.Mode}");

                // Debounce check
                ref var debounceState = ref debounce.ValueRW;
                if (currentTick - debounceState.LastRequestTick < debounceState.DebounceTickCount)
                {
                    // if (!state.WorldUnmanaged.IsServer())
                        UnityEngine.Debug.Log($"{prefix}[StationDebug] Interact ignored due to debounce. Tick: {currentTick}, Last: {debounceState.LastRequestTick}");
                    continue;
                }

                // Check if player is currently operating a station - if so, exit using their current StableId
                bool isOperating = SystemAPI.HasComponent<OperatingStation>(entity);

                if (isOperating)
                {
                    // Request to exit current station
                    var operating = SystemAPI.GetComponent<OperatingStation>(entity);
                    
                    int exitStableId = 0;
                    if (_stationLookup.HasComponent(operating.StationEntity))
                    {
                        exitStableId = _stationLookup[operating.StationEntity].StableId;
                    }

                    // if (!state.WorldUnmanaged.IsServer())
                        UnityEngine.Debug.Log($"{prefix}[StationDebug] Exiting Station (StableID: {exitStableId})");

                    requestBuffer.Add(new StationUseRequest
                    {
                        StationStableId = exitStableId,
                        Action = StationUseAction.Exit,
                        ClientTick = (uint)currentTick
                    });
                    
                    debounceState.LastRequestTick = (uint)currentTick;
                    // if (!state.WorldUnmanaged.IsServer())
                        UnityEngine.Debug.Log($"{prefix}[StationDebug] Sending Exit Request for Station {exitStableId}");
                    continue;
                }

                // Not operating -> Look for station to enter
                
                // Allow interactions in both InShip and EVA modes (assuming valid distance)
                var mode = playerState.ValueRO.Mode;
                if (mode != PlayerMode.InShip && mode != PlayerMode.EVA)
                {
                    UnityEngine.Debug.Log($"{prefix}[StationDebug] Enter Failed: Player Mode {playerState.ValueRO.Mode} != InShip/EVA.");
                    continue;
                }

                // Find nearest station
                Entity playerShip = Entity.Null;
                float3 playerPos = playerTransform.ValueRO.Position; // Always World Position (see ShipInertialCorrectionSystem)

                if (mode == PlayerMode.InShip)
                {
                    if (_inShipLookup.HasComponent(entity))
                    {
                        playerShip = _inShipLookup[entity].ShipEntity;
                    }
                    else
                    {
                         UnityEngine.Debug.LogError($"{prefix}[StationDebug] Enter Failed: Mode InShip but Missing InShipLocalSpace component.");
                         continue;
                    }
                }

                // UnityEngine.Debug.Log($"{prefix}[StationDebug] Player Pos: {playerPos}. Searching {stationEntities.Length} stations. Ship: {playerShip}");

                Entity bestStation = Entity.Null;
                int bestIndex = -1;
                float bestDistance = float.MaxValue;

                for (int i = 0; i < stationEntities.Length; i++)
                {
                    Entity stationEntity = stationEntities[i];
                    
                    if (!_stationLookup.HasComponent(stationEntity)) continue;

                    // If InShip, prioritize/restrict to current ship stations?
                    // Actually, if using World Distance, we don't strictly *need* to filter by ship, 
                    // but it's a good optimization and "wall" check.
                    if (mode == PlayerMode.InShip && playerShip != Entity.Null)
                    {
                            if (stationShipEntities[i] != playerShip)
                                continue;
                    }

                    // Distance Check (World Space vs World Space)
                    float3 stationPos = stationLocalPositions[i];
                    float distance = math.distance(playerPos, stationPos);
                    var station = _stationLookup[stationEntity];

                    if (distance > station.Range)
                    {
                            // if (interactPressed) 
                            // { 
                                // Only log specific near-misses if needed or keep silence to avoid spam
                                // UnityEngine.Debug.Log($"{prefix}[StationDebug] Station {stationEntity.Index} too far: {distance:F2} > {station.Range:F2}");
                            // }
                            continue;
                    }

                    if (_disabledLookup.HasComponent(stationEntity)) continue;
                    if (station.CurrentOperator != Entity.Null) continue;

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestStation = stationEntity;
                        bestIndex = i;
                    }
                }

                if (bestStation != Entity.Null)
                {
                    int bestStableId = stationStableIds[bestIndex];
                    // if (!state.WorldUnmanaged.IsServer())
                        UnityEngine.Debug.Log($"{prefix}[StationDebug] Sending Enter Request: Station {bestStableId}");

                    requestBuffer.Add(new StationUseRequest
                    {
                        StationStableId = bestStableId,
                        Action = StationUseAction.Enter,
                        ClientTick = (uint)currentTick
                    });
                    
                    debounceState.LastRequestTick = (uint)currentTick;
                }
                else
                {
                    // if (!state.WorldUnmanaged.IsServer())
                        UnityEngine.Debug.Log($"{prefix}[StationDebug] No valid station found in range.");
                }
            }
            
            stationEntities.Dispose();
            stationLocalPositions.Dispose();
            stationShipEntities.Dispose();
            stationStableIds.Dispose();
        }
    }
}
