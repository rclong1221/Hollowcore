using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Ship.Cargo.Systems;
using DIG.Shared;

namespace DIG.Ship.Cargo
{
    /// <summary>
    /// Client-side system that handles cargo terminal interactions.
    /// Detects nearby terminals and manages interaction state.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct CargoTerminalInteractionSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<LocalToWorld> _l2wLookup;
        private ComponentLookup<CargoTerminal> _terminalLookup;
        private ComponentLookup<CargoTerminalInteractable> _interactableLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _l2wLookup = state.GetComponentLookup<LocalToWorld>(true);
            _terminalLookup = state.GetComponentLookup<CargoTerminal>(true);
            _interactableLookup = state.GetComponentLookup<CargoTerminalInteractable>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Complete previous jobs that may be writing to LocalToWorld
            state.Dependency.Complete();
            
            _transformLookup.Update(ref state);
            _l2wLookup.Update(ref state);
            _terminalLookup.Update(ref state);
            _interactableLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Find the closest cargo terminal for each player
            foreach (var (playerTransform, playerState, playerEntity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerState>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Get player world position
                float3 playerPos = playerTransform.ValueRO.Position;
                if (_l2wLookup.HasComponent(playerEntity))
                {
                    playerPos = _l2wLookup[playerEntity].Position;
                }

                // Track closest terminal
                Entity closestTerminal = Entity.Null;
                float closestDist = float.MaxValue;
                Entity closestShip = Entity.Null;

                // Check all cargo terminals
                foreach (var (terminal, terminalL2W, terminalEntity) in
                         SystemAPI.Query<RefRO<CargoTerminal>, RefRO<LocalToWorld>>()
                         .WithEntityAccess())
                {
                    float dist = math.distance(playerPos, terminalL2W.ValueRO.Position);
                    if (dist <= terminal.ValueRO.Range && dist < closestDist)
                    {
                        closestDist = dist;
                        closestTerminal = terminalEntity;
                        closestShip = terminal.ValueRO.ShipEntity;
                    }
                }

                // Update interaction state
                bool hasInteraction = SystemAPI.HasComponent<InteractingWithCargo>(playerEntity);
                
                if (closestTerminal != Entity.Null && closestShip != Entity.Null)
                {
                    // Player is near a terminal
                    if (!hasInteraction)
                    {
                        // Add interaction component
                        ecb.AddComponent(playerEntity, new InteractingWithCargo
                        {
                            TerminalEntity = closestTerminal,
                            ShipEntity = closestShip
                        });
                    }
                    else
                    {
                        // Update interaction component (in case they moved to a different terminal)
                        SystemAPI.SetComponent(playerEntity, new InteractingWithCargo
                        {
                            TerminalEntity = closestTerminal,
                            ShipEntity = closestShip
                        });
                    }
                }
                else
                {
                    // Player is not near any terminal
                    if (hasInteraction)
                    {
                        ecb.RemoveComponent<InteractingWithCargo>(playerEntity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// System that updates cargo weight calculations.
    /// Runs on both client and server to keep capacity in sync.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoTransferSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct CargoWeightSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourceWeights>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var resourceWeights = SystemAPI.GetSingleton<ResourceWeights>();

            foreach (var (capacity, cargoBuffer, entity) in
                     SystemAPI.Query<RefRW<ShipCargoCapacity>, DynamicBuffer<ShipCargoItem>>()
                     .WithEntityAccess())
            {
                float totalWeight = 0f;
                for (int i = 0; i < cargoBuffer.Length; i++)
                {
                    totalWeight += cargoBuffer[i].Quantity * resourceWeights.GetWeight(cargoBuffer[i].ResourceType);
                }
                
                capacity.ValueRW.CurrentWeight = totalWeight;
                capacity.ValueRW.IsOverCapacity = totalWeight > capacity.ValueRO.MaxWeight;
            }
        }
    }

    /// <summary>
    /// System that adds CargoTransferRequest buffer to players during connection.
    /// Ensures players can send cargo transfer requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct CargoPlayerInitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Add CargoTransferRequest buffer to players that don't have it
            foreach (var (playerState, entity) in
                     SystemAPI.Query<RefRO<PlayerState>>()
                     .WithNone<CargoTransferRequest>()
                     .WithEntityAccess())
            {
                ecb.AddBuffer<CargoTransferRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
