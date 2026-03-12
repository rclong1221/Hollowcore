using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Tools;
using DIG.Interaction;

namespace DIG.Survival.Resources
{
    /// <summary>
    /// Handles player interaction input for resource collection.
    /// Creates collection requests when player interacts with resource nodes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(DIG.Interaction.Systems.InteractableDetectionSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ResourceCollectionInputSystem : ISystem
    {
        private ComponentLookup<ResourceInteractable> _resourceNodeLookup;
        private ComponentLookup<ActiveTool> _activeToolLookup;
        private ComponentLookup<Tool> _toolLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _resourceNodeLookup = state.GetComponentLookup<ResourceInteractable>(true);
            _activeToolLookup = state.GetComponentLookup<ActiveTool>(true);
            _toolLookup = state.GetComponentLookup<Tool>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _resourceNodeLookup.Update(ref state);
            _activeToolLookup.Update(ref state);
            _toolLookup.Update(ref state);

            foreach (var (input, ability, progress, entity) in
                     SystemAPI.Query<RefRO<PlayerInput>, RefRO<InteractAbility>, RefRW<CollectionProgress>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var prog = ref progress.ValueRW;

                // Check for interact input (E key / Use button)
                bool interactPressed = input.ValueRO.Interact.IsSet;

                // If not pressing interact, cancel any collection in progress
                if (!interactPressed)
                {
                    prog.IsCollecting = false;
                    prog.ElapsedTime = 0f;
                    prog.TargetNode = Entity.Null;
                    continue;
                }

                // Check if we have a valid target
                if (ability.ValueRO.TargetEntity == Entity.Null)
                {
                    prog.IsCollecting = false;
                    continue;
                }

                var targetEntity = ability.ValueRO.TargetEntity;

                // Check if target is a resource node
                if (!_resourceNodeLookup.HasComponent(targetEntity))
                    continue;

                var node = _resourceNodeLookup[targetEntity];

                // Check if depleted
                if (node.CurrentAmount <= 0)
                {
                    prog.IsCollecting = false;
                    continue;
                }

                // Check drill requirement
                if (node.RequiresTool)
                {
                    bool hasDrill = false;
                    if (_activeToolLookup.HasComponent(entity))
                    {
                        var activeTool = _activeToolLookup[entity];
                        if (activeTool.ToolEntity != Entity.Null &&
                            _toolLookup.HasComponent(activeTool.ToolEntity))
                        {
                            var tool = _toolLookup[activeTool.ToolEntity];
                            hasDrill = tool.ToolType == ToolType.Drill;
                        }
                    }

                    if (!hasDrill)
                    {
                        prog.IsCollecting = false;
                        continue;
                    }
                }

                // Start or continue collection
                if (prog.TargetNode != targetEntity)
                {
                    // New target, reset progress
                    prog.TargetNode = targetEntity;
                    prog.ElapsedTime = 0f;
                    prog.RequiredTime = node.CollectionTime;
                }

                prog.IsCollecting = true;
                prog.ElapsedTime += SystemAPI.Time.DeltaTime;
                prog.CollectorEntity = entity;

                // Check if collection complete (handled by server system)
            }
        }
    }

    /// <summary>
    /// Server-authoritative system that processes resource collection.
    /// Transfers resources to player inventory when collection completes.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ResourceCollectionServerSystem : ISystem
    {
        private BufferLookup<DIG.Shared.InventoryItem> _inventoryLookup;
        private ComponentLookup<ResourceInteractable> _resourceNodeLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _inventoryLookup = state.GetBufferLookup<DIG.Shared.InventoryItem>();
            _resourceNodeLookup = state.GetComponentLookup<ResourceInteractable>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _inventoryLookup.Update(ref state);
            _resourceNodeLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (progress, entity) in
                     SystemAPI.Query<RefRW<CollectionProgress>>()
                     .WithEntityAccess())
            {
                ref var prog = ref progress.ValueRW;

                if (!prog.IsCollecting || prog.TargetNode == Entity.Null)
                    continue;

                // Check if collection time complete
                if (prog.ElapsedTime < prog.RequiredTime)
                    continue;

                // Validate target still exists and has resources
                if (!_resourceNodeLookup.HasComponent(prog.TargetNode))
                {
                    prog.IsCollecting = false;
                    prog.TargetNode = Entity.Null;
                    continue;
                }

                var node = _resourceNodeLookup[prog.TargetNode];
                if (node.CurrentAmount <= 0)
                {
                    prog.IsCollecting = false;
                    prog.TargetNode = Entity.Null;
                    continue;
                }

                // Calculate amount to collect
                int amountToCollect = node.AmountPerCollection > 0 ? node.AmountPerCollection : 1;
                amountToCollect = amountToCollect > node.CurrentAmount ? node.CurrentAmount : amountToCollect;

                // Add to player inventory
                if (_inventoryLookup.HasBuffer(entity))
                {
                    var inventory = _inventoryLookup[entity];
                    AddToInventory(ref inventory, (DIG.Shared.ResourceType)node.ResourceTypeId, amountToCollect);
                }

                // Decrement node
                node.CurrentAmount -= amountToCollect;
                _resourceNodeLookup[prog.TargetNode] = node;

                // Check if depleted
                if (node.CurrentAmount <= 0)
                {
                    ecb.AddComponent(prog.TargetNode, new ResourceDepleted
                    {
                        TimeSinceDepletion = 0f
                    });
                }

                // Reset for next collection cycle (or stop if instant)
                prog.ElapsedTime = 0f;
                if (node.CollectionTime <= 0f)
                {
                    prog.IsCollecting = false;
                    prog.TargetNode = Entity.Null;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void AddToInventory(ref DynamicBuffer<DIG.Shared.InventoryItem> inventory, DIG.Shared.ResourceType type, int amount)
        {
            // Find existing stack
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ResourceType == type)
                {
                    var item = inventory[i];
                    item.Quantity += amount;
                    inventory[i] = item;
                    return;
                }
            }

            // Create new stack
            inventory.Add(new DIG.Shared.InventoryItem
            {
                ResourceType = type,
                Quantity = amount
            });
        }
    }
}
