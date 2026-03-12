using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Ship.Stations
{
    /// <summary>
    /// System that processes station use requests.
    /// Runs on both client (for prediction) and server (authoritative).
    /// Validates and assigns station occupancy, updates player mode.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    
    public partial struct StationOccupancySystem : ISystem
    {
        private ComponentLookup<OperableStation> _stationLookup;
        private ComponentLookup<StationDisabled> _disabledLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<PlayerState> _playerStateLookup;
        private ComponentLookup<OperatingStation> _operatingLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();

            _stationLookup = state.GetComponentLookup<OperableStation>(false);
            _disabledLookup = state.GetComponentLookup<StationDisabled>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _playerStateLookup = state.GetComponentLookup<PlayerState>(false);
            _operatingLookup = state.GetComponentLookup<OperatingStation>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _stationLookup.Update(ref state);
            _disabledLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _playerStateLookup.Update(ref state);
            _operatingLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Phase 1: Process incoming requests
            ProcessRequests(ref state, ref ecb);

            // Phase 2: Handle edge cases (operator disconnect, death, left ship)
            HandleEdgeCases(ref state, ref ecb);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Process station use requests from players.
        /// </summary>
        private void ProcessRequests(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (requestBuffer, playerTransform, playerState, playerEntity) in
                     SystemAPI.Query<DynamicBuffer<StationUseRequest>, RefRO<LocalTransform>, RefRO<PlayerState>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                if (requestBuffer.Length == 0)
                    continue;

                // Process most recent request
                var request = requestBuffer[requestBuffer.Length - 1];
                requestBuffer.Clear();

                // Look up station entity by StableId
                int stationStableId = request.StationStableId;
                Entity stationEntity = Entity.Null;

                foreach (var (station, entity) in
                         SystemAPI.Query<RefRO<OperableStation>>()
                         .WithEntityAccess())
                {
                    if (station.ValueRO.StableId == stationStableId)
                    {
                        stationEntity = entity;
                        break;
                    }
                }

                if (stationEntity == Entity.Null)
                {
                    UnityEngine.Debug.Log($"[StationOccupancy] FAIL: Could not find station for StableId {stationStableId}");
                    continue;
                }

                if (request.Action == StationUseAction.Enter)
                {
                    ProcessEnterRequest(ref state, ref ecb, playerEntity, stationEntity,
                        playerTransform.ValueRO.Position, playerState.ValueRO);
                }
                else
                {
                    ProcessExitRequest(ref state, ref ecb, playerEntity, stationEntity);
                }
            }
        }

        /// <summary>
        /// Validate and process station enter request.
        /// </summary>
        private void ProcessEnterRequest(ref SystemState state, ref EntityCommandBuffer ecb,
            Entity playerEntity, Entity stationEntity, float3 playerPos, PlayerState playerState)
        {

            // Validate station exists
            if (!_stationLookup.HasComponent(stationEntity))
            {
                UnityEngine.Debug.Log($"[StationOccupancy] FAIL: Station {stationEntity} not found in lookup");
                return;
            }
            if (!_transformLookup.HasComponent(stationEntity))
            {
                UnityEngine.Debug.Log($"[StationOccupancy] FAIL: Station {stationEntity} transform not found");
                return;
            }

            // Validate station not disabled
            if (_disabledLookup.HasComponent(stationEntity))
            {
                UnityEngine.Debug.Log($"[StationOccupancy] FAIL: Station {stationEntity} is disabled");
                return;
            }

            var station = _stationLookup[stationEntity];
            var stationTransform = _transformLookup[stationEntity];

            // Validate station not occupied
            if (station.CurrentOperator != Entity.Null)
            {
                UnityEngine.Debug.Log($"[StationOccupancy] FAIL: Station {stationEntity} is occupied by {station.CurrentOperator}");
                return;
            }

            // Validate player in range
            float3 stationWorldPos;
            if (SystemAPI.HasComponent<LocalToWorld>(stationEntity))
            {
                stationWorldPos = SystemAPI.GetComponent<LocalToWorld>(stationEntity).Position;
            }
            else
            {
                stationWorldPos = stationTransform.Position;
            }

            // Player Position is always World Position (overwritten by ShipInertialCorrectionSystem)
            float distance = math.distance(playerPos, stationWorldPos);
            
            if (distance > station.Range)
            {
                UnityEngine.Debug.Log($"[StationOccupancy] FAIL: Player too far. Dist: {distance}, Range: {station.Range}");
                return;
            }

            // Validate player is InShip or EVA and alive
            if (playerState.Mode != PlayerMode.InShip && playerState.Mode != PlayerMode.EVA)
            {
                UnityEngine.Debug.Log($"[StationOccupancy] FAIL: Player not InShip or EVA (Mode: {playerState.Mode})");
                return;
            }

            // Validate player not already operating a station
            if (_operatingLookup.HasComponent(playerEntity))
            {
                UnityEngine.Debug.Log($"[StationOccupancy] FAIL: Player already operating");
                return;
            }

            // All validations passed - assign operator
            UnityEngine.Debug.Log($"[StationOccupancy] SUCCESS: Assigned player {playerEntity} to station {stationEntity}");
            
            // Update station
            station.CurrentOperator = playerEntity;
            _stationLookup[stationEntity] = station;

            // Add OperatingStation to player
            ecb.AddComponent(playerEntity, new OperatingStation
            {
                StationEntity = stationEntity,
                StationType = station.Type,
                IsOperating = true
            });

            // Update player mode to Piloting (for helm) or keep InShip (for other stations)
            if (_playerStateLookup.HasComponent(playerEntity))
            {
                var pState = _playerStateLookup[playerEntity];
                if (station.Type == StationType.Helm)
                {
                    pState.Mode = PlayerMode.Piloting;
                }
                _playerStateLookup[playerEntity] = pState;
            }

            // FORCE ATTACHMENT: Ensure player is attached to the ship's local space
            // This handles cases where player spawned in ship or bypassed airlock
            if (station.ShipEntity != Entity.Null && _transformLookup.HasComponent(station.ShipEntity))
            {
                if (!SystemAPI.HasComponent<DIG.Ship.LocalSpace.InShipLocalSpace>(playerEntity))
                {
                    ecb.AddComponent(playerEntity, new DIG.Ship.LocalSpace.InShipLocalSpace
                    {
                        ShipEntity = station.ShipEntity,
                        IsAttached = true,
                        LocalPosition = float3.zero, // CaptureSystem will correct this based on world pos
                        LocalRotation = quaternion.identity
                    });
                }
                else
                {
                    // Update existing component to ensure attached
                    var localSpace = SystemAPI.GetComponent<DIG.Ship.LocalSpace.InShipLocalSpace>(playerEntity);
                    localSpace.ShipEntity = station.ShipEntity;
                    localSpace.IsAttached = true;
                    ecb.SetComponent(playerEntity, localSpace);
                }
            }

            // Snap player to interaction point (only if valid)
            if (_transformLookup.HasComponent(playerEntity))
            {
                var playerTransform = _transformLookup[playerEntity];
                
                // Validate interaction point is finite (not NaN/Infinity)
                bool hasValidPosition = math.all(math.isfinite(station.InteractionPoint));
                bool hasValidForward = math.lengthsq(station.InteractionForward) > 0.001f &&
                                       math.all(math.isfinite(station.InteractionForward));

                if (hasValidPosition)
                {
                    // InteractionPoint is in local coordinates relative to the station
                    // Transform to world space using the station's current world position
                    if (SystemAPI.HasComponent<LocalToWorld>(stationEntity))
                    {
                        var stationL2W = SystemAPI.GetComponent<LocalToWorld>(stationEntity);
                        float4 worldPos = math.mul(stationL2W.Value, new float4(station.InteractionPoint, 1f));
                        playerTransform.Position = worldPos.xyz;
                    }
                    else
                    {
                        // Fallback: add station's local transform position
                        playerTransform.Position = stationTransform.Position + station.InteractionPoint;
                    }
                }
                
                if (hasValidForward)
                {
                    // Transform forward direction to world space as well
                    float3 worldForward;
                    if (SystemAPI.HasComponent<LocalToWorld>(stationEntity))
                    {
                        var stationL2W = SystemAPI.GetComponent<LocalToWorld>(stationEntity);
                        worldForward = math.normalize(math.mul(stationL2W.Rotation, station.InteractionForward));
                    }
                    else
                    {
                        worldForward = math.normalizesafe(station.InteractionForward);
                    }
                    playerTransform.Rotation = quaternion.LookRotationSafe(worldForward, new float3(0, 1, 0));
                }
                
                // Ensure rotation is valid
                if (!math.all(math.isfinite(playerTransform.Rotation.value)) ||
                    math.lengthsq(playerTransform.Rotation.value) < 0.001f)
                {
                    playerTransform.Rotation = quaternion.identity;
                }
                
                _transformLookup[playerEntity] = playerTransform;
            }

            // Add StationInput component to station if not present
            if (!SystemAPI.HasComponent<StationInput>(stationEntity))
            {
                ecb.AddComponent<StationInput>(stationEntity);
            }
        }

        /// <summary>
        /// Validate and process station exit request.
        /// </summary>
        private void ProcessExitRequest(ref SystemState state, ref EntityCommandBuffer ecb,
            Entity playerEntity, Entity stationEntity)
        {

            // Validate player is operating this station
            if (!_operatingLookup.HasComponent(playerEntity))
                return;

            var operating = _operatingLookup[playerEntity];
            if (operating.StationEntity != stationEntity)
                return;

            // Clear station operator
            if (_stationLookup.HasComponent(stationEntity))
            {
                var station = _stationLookup[stationEntity];
                if (station.CurrentOperator == playerEntity)
                {
                    station.CurrentOperator = Entity.Null;
                    _stationLookup[stationEntity] = station;
                }
            }

            // Remove OperatingStation from player
            ecb.RemoveComponent<OperatingStation>(playerEntity);

            // Reset player mode
            if (_playerStateLookup.HasComponent(playerEntity))
            {
                var playerState = _playerStateLookup[playerEntity];
                if (playerState.Mode == PlayerMode.Piloting)
                {
                    playerState.Mode = PlayerMode.InShip;
                }
                _playerStateLookup[playerEntity] = playerState;
            }
            
            // Push player back from station to prevent clipping into nearby geometry
            // This gives them some space when they start walking
            if (_transformLookup.HasComponent(playerEntity) && _stationLookup.HasComponent(stationEntity))
            {
                var station = _stationLookup[stationEntity];
                var playerTransform = _transformLookup[playerEntity];
                
                // Calculate exit offset: push player backwards (opposite of interaction forward)
                float3 exitOffset = float3.zero;
                if (math.lengthsq(station.InteractionForward) > 0.001f)
                {
                    // Transform the local forward to world space
                    float3 worldForward;
                    if (SystemAPI.HasComponent<LocalToWorld>(stationEntity))
                    {
                        var stationL2W = SystemAPI.GetComponent<LocalToWorld>(stationEntity);
                        worldForward = math.normalize(math.mul(stationL2W.Rotation, station.InteractionForward));
                    }
                    else
                    {
                        worldForward = math.normalizesafe(station.InteractionForward);
                    }
                    // Push back 0.5 meters from the console
                    exitOffset = -worldForward * 0.5f;
                }
                
                playerTransform.Position += exitOffset;
                _transformLookup[playerEntity] = playerTransform;
            }

            // Reset StationInput
            if (SystemAPI.HasComponent<StationInput>(stationEntity))
            {
                ecb.SetComponent(stationEntity, new StationInput());
            }
        }

        /// <summary>
        /// Handle edge cases: operator disconnect, death, left ship via airlock.
        /// </summary>
        private void HandleEdgeCases(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            // Check for players with OperatingStation whose station is invalid
            foreach (var (operating, playerState, playerEntity) in
                     SystemAPI.Query<RefRO<OperatingStation>, RefRO<PlayerState>>()
                     .WithEntityAccess())
            {
                Entity stationEntity = operating.ValueRO.StationEntity;
                bool shouldExit = false;

                // Check if station still exists
                if (!_stationLookup.HasComponent(stationEntity))
                {
                    shouldExit = true;
                }
                // Check if player died
                else if (playerState.ValueRO.Mode == PlayerMode.Dead)
                {
                    shouldExit = true;
                }
                // Check if player left ship (EVA via airlock)
                else if (playerState.ValueRO.Mode == PlayerMode.EVA)
                {
                    shouldExit = true;
                }

                if (shouldExit)
                {
                    // Clear station if it exists
                    if (_stationLookup.HasComponent(stationEntity))
                    {
                        var station = _stationLookup[stationEntity];
                        if (station.CurrentOperator == playerEntity)
                        {
                            station.CurrentOperator = Entity.Null;
                            _stationLookup[stationEntity] = station;
                        }
                    }

                    // Remove OperatingStation
                    ecb.RemoveComponent<OperatingStation>(playerEntity);

                    // Reset player mode if needed
                    if (_playerStateLookup.HasComponent(playerEntity))
                    {
                        var pState = _playerStateLookup[playerEntity];
                        if (pState.Mode == PlayerMode.Piloting)
                        {
                            pState.Mode = PlayerMode.InShip;
                            _playerStateLookup[playerEntity] = pState;
                        }
                    }
                }
            }

            // Check for stations with operators that no longer exist
            foreach (var (station, stationEntity) in
                     SystemAPI.Query<RefRW<OperableStation>>()
                     .WithEntityAccess())
            {
                ref var stationRef = ref station.ValueRW;

                if (stationRef.CurrentOperator == Entity.Null)
                    continue;

                // Check if operator entity still exists
                if (!state.EntityManager.Exists(stationRef.CurrentOperator))
                {
                    stationRef.CurrentOperator = Entity.Null;
                }
            }
        }
    }
}
