using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Shared;

namespace DIG.Ship.Cargo.Systems
{
    /// <summary>
    /// Server-authoritative system that processes cargo transfer requests.
    /// Validates player position, inventory, and cargo capacity before applying transfers.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct CargoTransferSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<LocalToWorld> _l2wLookup;
        private ComponentLookup<CargoTerminal> _terminalLookup;
        private ComponentLookup<ShipCargoCapacity> _capacityLookup;
        private ComponentLookup<InventoryCapacity> _inventoryCapacityLookup;
        private ComponentLookup<PlayerState> _playerStateLookup;
        private ComponentLookup<LocalSpace.InShipLocalSpace> _inShipLookup;
        private BufferLookup<ShipCargoItem> _cargoBufferLookup;
        private BufferLookup<InventoryItem> _inventoryBufferLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ResourceWeights>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _l2wLookup = state.GetComponentLookup<LocalToWorld>(true);
            _terminalLookup = state.GetComponentLookup<CargoTerminal>(true);
            _capacityLookup = state.GetComponentLookup<ShipCargoCapacity>(false);
            _inventoryCapacityLookup = state.GetComponentLookup<InventoryCapacity>(false);
            _playerStateLookup = state.GetComponentLookup<PlayerState>(true);
            _inShipLookup = state.GetComponentLookup<LocalSpace.InShipLocalSpace>(true);
            _cargoBufferLookup = state.GetBufferLookup<ShipCargoItem>(false);
            _inventoryBufferLookup = state.GetBufferLookup<InventoryItem>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _l2wLookup.Update(ref state);
            _terminalLookup.Update(ref state);
            _capacityLookup.Update(ref state);
            _inventoryCapacityLookup.Update(ref state);
            _playerStateLookup.Update(ref state);
            _inShipLookup.Update(ref state);
            _cargoBufferLookup.Update(ref state);
            _inventoryBufferLookup.Update(ref state);

            var resourceWeights = SystemAPI.GetSingleton<ResourceWeights>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();

            // Process all players with cargo transfer requests
            foreach (var (requestBuffer, inventoryBuffer, playerTransform, playerEntity) in
                     SystemAPI.Query<DynamicBuffer<CargoTransferRequest>, DynamicBuffer<InventoryItem>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                if (requestBuffer.Length == 0)
                    continue;

                // Get player world position
                float3 playerPos = playerTransform.ValueRO.Position;
                if (_l2wLookup.HasComponent(playerEntity))
                {
                    playerPos = _l2wLookup[playerEntity].Position;
                }

                // Check if player is alive (has PlayerState and not dead)
                if (_playerStateLookup.HasComponent(playerEntity))
                {
                    var playerState = _playerStateLookup[playerEntity];
                    // Add death check if applicable
                }

                // Process each request
                for (int i = requestBuffer.Length - 1; i >= 0; i--)
                {
                    var request = requestBuffer[i];
                    
                    // Validate ship entity exists and has cargo
                    if (request.ShipEntity == Entity.Null || !_cargoBufferLookup.HasBuffer(request.ShipEntity))
                    {
                        UnityEngine.Debug.LogWarning($"[CargoTransfer] Invalid ship entity in request from player {playerEntity.Index}");
                        continue;
                    }

                    // Validate player is in/near ship
                    bool playerNearShip = false;
                    
                    // Check if player is in ship's local space
                    if (_inShipLookup.HasComponent(playerEntity))
                    {
                        var inShip = _inShipLookup[playerEntity];
                        if (inShip.IsAttached && inShip.ShipEntity == request.ShipEntity)
                        {
                            playerNearShip = true;
                        }
                    }

                    // Also check proximity to any cargo terminal on this ship
                    if (!playerNearShip)
                    {
                        foreach (var (terminal, terminalL2W, terminalEntity) in
                                 SystemAPI.Query<RefRO<CargoTerminal>, RefRO<LocalToWorld>>()
                                 .WithEntityAccess())
                        {
                            if (terminal.ValueRO.ShipEntity == request.ShipEntity)
                            {
                                float dist = math.distance(playerPos, terminalL2W.ValueRO.Position);
                                if (dist <= terminal.ValueRO.Range)
                                {
                                    playerNearShip = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!playerNearShip)
                    {
                        UnityEngine.Debug.LogWarning($"[CargoTransfer] Player {playerEntity.Index} not near ship {request.ShipEntity.Index}");
                        continue;
                    }

                    // Get cargo and inventory buffers
                    var cargoBuffer = _cargoBufferLookup[request.ShipEntity];
                    var invBuffer = inventoryBuffer;

                    // Validate and apply transfer
                    bool success = false;
                    
                    if (request.Quantity > 0)
                    {
                        // DEPOSIT: Player -> Ship
                        int playerHas = CargoUtility.GetInventoryQuantity(invBuffer, request.ResourceType);
                        int transferAmount = math.min(request.Quantity, playerHas);
                        
                        if (transferAmount > 0)
                        {
                            // Check cargo capacity
                            bool hasCapacity = true;
                            if (_capacityLookup.HasComponent(request.ShipEntity))
                            {
                                var capacity = _capacityLookup[request.ShipEntity];
                                float addedWeight = transferAmount * resourceWeights.GetWeight(request.ResourceType);
                                if (capacity.CurrentWeight + addedWeight > capacity.MaxWeight)
                                {
                                    // Calculate max we can fit
                                    float availableWeight = capacity.MaxWeight - capacity.CurrentWeight;
                                    int maxCanFit = (int)(availableWeight / resourceWeights.GetWeight(request.ResourceType));
                                    transferAmount = math.min(transferAmount, maxCanFit);
                                    
                                    if (transferAmount <= 0)
                                    {
                                        hasCapacity = false;
                                        UnityEngine.Debug.Log($"[CargoTransfer] Ship cargo is full");
                                    }
                                }
                            }
                            
                            if (hasCapacity && transferAmount > 0)
                            {
                                // Apply atomic transfer
                                CargoUtility.RemoveFromInventory(ref invBuffer, request.ResourceType, transferAmount);
                                CargoUtility.AddToCargo(ref cargoBuffer, request.ResourceType, transferAmount);
                                success = true;
                                
                                UnityEngine.Debug.Log($"[CargoTransfer] Deposited {transferAmount}x {request.ResourceType} to ship {request.ShipEntity.Index}");
                            }
                        }
                    }
                    else if (request.Quantity < 0)
                    {
                        // WITHDRAW: Ship -> Player
                        int withdrawAmount = -request.Quantity; // Make positive
                        int shipHas = CargoUtility.GetCargoQuantity(cargoBuffer, request.ResourceType);
                        withdrawAmount = math.min(withdrawAmount, shipHas);
                        
                        if (withdrawAmount > 0)
                        {
                            // Check player inventory capacity
                            bool hasCapacity = true;
                            if (_inventoryCapacityLookup.HasComponent(playerEntity))
                            {
                                var invCapacity = _inventoryCapacityLookup[playerEntity];
                                float addedWeight = withdrawAmount * resourceWeights.GetWeight(request.ResourceType);
                                if (invCapacity.CurrentWeight + addedWeight > invCapacity.MaxWeight)
                                {
                                    // Calculate max player can carry
                                    float availableWeight = invCapacity.MaxWeight - invCapacity.CurrentWeight;
                                    int maxCanCarry = (int)(availableWeight / resourceWeights.GetWeight(request.ResourceType));
                                    withdrawAmount = math.min(withdrawAmount, maxCanCarry);
                                    
                                    if (withdrawAmount <= 0)
                                    {
                                        hasCapacity = false;
                                        UnityEngine.Debug.Log($"[CargoTransfer] Player inventory is full");
                                    }
                                }
                            }
                            
                            if (hasCapacity && withdrawAmount > 0)
                            {
                                // Apply atomic transfer
                                CargoUtility.RemoveFromCargo(ref cargoBuffer, request.ResourceType, withdrawAmount);
                                CargoUtility.AddToInventory(ref invBuffer, request.ResourceType, withdrawAmount);
                                success = true;
                                
                                UnityEngine.Debug.Log($"[CargoTransfer] Withdrew {withdrawAmount}x {request.ResourceType} from ship {request.ShipEntity.Index}");
                            }
                        }
                    }

                    // Update cargo capacity if present
                    if (success && _capacityLookup.HasComponent(request.ShipEntity))
                    {
                        var capacity = _capacityLookup[request.ShipEntity];
                        capacity.CurrentWeight = CalculateCargoWeight(cargoBuffer, resourceWeights);
                        capacity.IsOverCapacity = capacity.CurrentWeight > capacity.MaxWeight;
                        _capacityLookup[request.ShipEntity] = capacity;
                    }
                }

                // Clear processed requests
                requestBuffer.Clear();
            }
        }

        private float CalculateCargoWeight(in DynamicBuffer<ShipCargoItem> cargo, in ResourceWeights weights)
        {
            float total = 0f;
            for (int i = 0; i < cargo.Length; i++)
            {
                total += cargo[i].Quantity * weights.GetWeight(cargo[i].ResourceType);
            }
            return total;
        }
    }
}
