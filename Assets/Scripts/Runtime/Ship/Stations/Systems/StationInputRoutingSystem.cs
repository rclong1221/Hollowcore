using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Ship.Stations
{
    /// <summary>
    /// System that routes player input to station input when operating.
    /// Runs on both client (prediction) and server (authority).
    /// </summary>
    // [BurstCompile]  // Disable for debug logging
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    
    public partial struct StationInputRoutingSystem : ISystem
    {
        private ComponentLookup<StationInput> _stationInputLookup;
        private ComponentLookup<OperableStation> _stationLookup;

        // [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();

            _stationInputLookup = state.GetComponentLookup<StationInput>(false);
            _stationLookup = state.GetComponentLookup<OperableStation>(true);
        }

        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _stationInputLookup.Update(ref state);
            _stationLookup.Update(ref state);

            // Route input for all players operating stations
            foreach (var (operating, playerInput, entity) in
                     SystemAPI.Query<RefRO<OperatingStation>, RefRO<PlayerInput>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                UnityEngine.Debug.Log($"[StationInputRouting] Found player {entity.Index} with OperatingStation. IsOperating={operating.ValueRO.IsOperating}, Station={operating.ValueRO.StationEntity.Index}");
                
                if (!operating.ValueRO.IsOperating)
                {
                    UnityEngine.Debug.Log($"[StationInputRouting] Skipping - IsOperating is false");
                    continue;
                }

                Entity stationEntity = operating.ValueRO.StationEntity;

                // Validate station exists and has StationInput
                if (!_stationInputLookup.HasComponent(stationEntity))
                {
                    UnityEngine.Debug.Log($"[StationInputRouting] FAIL: Station {stationEntity.Index} missing StationInput component");
                    continue;
                }
                if (!_stationLookup.HasComponent(stationEntity))
                    continue;

                var station = _stationLookup[stationEntity];
                var input = playerInput.ValueRO;

                // Build station input based on station type
                var stationInput = new StationInput();

                switch (operating.ValueRO.StationType)
                {
                    case StationType.Helm:
                        // Helm: movement controls thrust/yaw, look controls pitch/roll
                        // Note: Horizontal/Vertical are -1, 0, or 1 (not scaled to 100)
                        stationInput.Move = new float2(input.Horizontal, input.Vertical);
                        stationInput.Look = input.LookDelta;
                        stationInput.Primary = input.Sprint.IsSet ? (byte)1 : (byte)0; // Boost
                        stationInput.Secondary = input.Crouch.IsSet ? (byte)1 : (byte)0; // Brake
                        stationInput.Modifier = input.Jump.IsSet ? (byte)1 : (byte)0; // Vertical thrust
                        break;

                    case StationType.DrillControl:
                        // Drill: movement aims drill, primary activates
                        stationInput.Move = new float2(input.Horizontal, input.Vertical);
                        stationInput.Look = input.LookDelta;
                        stationInput.Primary = input.Use.IsSet ? (byte)1 : (byte)0;
                        stationInput.Secondary = input.AltUse.IsSet ? (byte)1 : (byte)0;
                        break;

                    case StationType.WeaponStation:
                        // Weapons: look aims, primary/secondary fire
                        stationInput.Look = input.LookDelta;
                        stationInput.Primary = input.Use.IsSet ? (byte)1 : (byte)0;
                        stationInput.Secondary = input.AltUse.IsSet ? (byte)1 : (byte)0;
                        stationInput.Modifier = input.Reload.IsSet ? (byte)1 : (byte)0;
                        break;

                    case StationType.SystemsPanel:
                        // Systems: navigation input for menu, primary to select
                        stationInput.Move = new float2(input.Horizontal, input.Vertical);
                        stationInput.Primary = input.Use.IsSet ? (byte)1 : (byte)0;
                        stationInput.Secondary = input.AltUse.IsSet ? (byte)1 : (byte)0;
                        stationInput.Cancel = input.Crouch.IsSet ? (byte)1 : (byte)0;
                        break;

                    case StationType.Engineering:
                    case StationType.Communications:
                        // Generic station input
                        stationInput.Move = new float2(input.Horizontal, input.Vertical);
                        stationInput.Look = input.LookDelta;
                        stationInput.Primary = input.Use.IsSet ? (byte)1 : (byte)0;
                        stationInput.Secondary = input.AltUse.IsSet ? (byte)1 : (byte)0;
                        break;
                }

                // Apply deadzone to prevent noisy input
                if (math.length(stationInput.Move) < 0.1f)
                    stationInput.Move = float2.zero;
                if (math.length(stationInput.Look) < 0.5f)
                    stationInput.Look = float2.zero;

                _stationInputLookup[stationEntity] = stationInput;
                UnityEngine.Debug.Log($"[StationInputRouting] WROTE StationInput to {stationEntity.Index}: Move={stationInput.Move}, Primary={stationInput.Primary}");
            }
        }
    }

    /// <summary>
    /// System that suppresses player movement when operating a station.
    /// Gates movement systems based on OperatingStation component.
    /// </summary>
    /// <remarks>
    /// This system runs early and sets a flag that other movement systems check.
    /// Alternative: Movement systems can query for !OperatingStation directly.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct StationMovementSuppressionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system doesn't need to do anything actively.
            // Movement systems should check for OperatingStation component
            // and skip processing if player is operating.
            //
            // Example in movement system:
            // if (SystemAPI.HasComponent<OperatingStation>(entity))
            //     continue; // Skip movement for this player
            //
            // The presence of OperatingStation.IsOperating == true
            // is the signal to suppress movement.
        }
    }
}
