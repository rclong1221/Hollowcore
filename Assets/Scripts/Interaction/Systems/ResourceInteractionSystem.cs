using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// Handles resource-specific collection logic.
    /// Works with base Interactable + ResourceInteractable components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(InteractAbilitySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ResourceInteractionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Process collection progress for players
            foreach (var (ability, progress, entity) in 
                     SystemAPI.Query<RefRO<InteractAbility>, RefRW<CollectionProgress>>()
                     .WithAll<Simulate, CanInteract>()
                     .WithEntityAccess())
            {
                ref var progressRef = ref progress.ValueRW;

                // Check if we should be collecting
                if (ability.ValueRO.IsInteracting && ability.ValueRO.TargetEntity != Entity.Null)
                {
                    // Is this a resource interactable?
                    if (SystemAPI.HasComponent<ResourceInteractable>(ability.ValueRO.TargetEntity))
                    {
                        var resource = SystemAPI.GetComponent<ResourceInteractable>(ability.ValueRO.TargetEntity);
                        
                        // Skip if depleted
                        if (resource.CurrentAmount <= 0)
                        {
                            progressRef.IsCollecting = false;
                            continue;
                        }

                        // Start or continue collection
                        if (!progressRef.IsCollecting || progressRef.TargetNode != ability.ValueRO.TargetEntity)
                        {
                            progressRef.IsCollecting = true;
                            progressRef.TargetNode = ability.ValueRO.TargetEntity;
                            progressRef.CollectorEntity = entity;
                            progressRef.ElapsedTime = 0f;
                            progressRef.RequiredTime = resource.CollectionTime;
                        }
                        else
                        {
                            progressRef.ElapsedTime += deltaTime;

                            // Check if collection complete
                            if (progressRef.ElapsedTime >= progressRef.RequiredTime)
                            {
                                // Collect resources
                                CollectResource(ref state, ability.ValueRO.TargetEntity, entity, resource);
                                
                                // Reset for next collection
                                progressRef.ElapsedTime = 0f;
                            }
                        }
                    }
                }
                else
                {
                    // Not interacting - stop collection
                    progressRef.IsCollecting = false;
                    progressRef.TargetNode = Entity.Null;
                    progressRef.ElapsedTime = 0f;
                }
            }

            // Update resource node state
            foreach (var (resource, interactable, entity) in 
                     SystemAPI.Query<RefRO<ResourceInteractable>, RefRW<Interactable>>()
                     .WithEntityAccess())
            {
                // Disable interaction when depleted
                interactable.ValueRW.CanInteract = resource.ValueRO.CurrentAmount > 0;
            }

            // Handle respawning
            foreach (var (depleted, resource, entity) in 
                     SystemAPI.Query<RefRW<ResourceDepleted>, RefRW<ResourceInteractable>>()
                     .WithEntityAccess())
            {
                ref var depletedRef = ref depleted.ValueRW;
                ref var resourceRef = ref resource.ValueRW;

                if (resourceRef.RespawnTime > 0)
                {
                    depletedRef.TimeSinceDepletion += deltaTime;
                    
                    if (depletedRef.TimeSinceDepletion >= resourceRef.RespawnTime)
                    {
                        // Respawn
                        resourceRef.CurrentAmount = resourceRef.MaxAmount;
                        
                        // Remove depleted tag via ECB
                        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
                        ecb.RemoveComponent<ResourceDepleted>(entity);
                        ecb.Playback(state.EntityManager);
                        ecb.Dispose();
                    }
                }
            }
        }

        private void CollectResource(ref SystemState state, Entity resourceEntity, Entity collector, ResourceInteractable resource)
        {
            // Get mutable resource
            var resourceRW = SystemAPI.GetComponentRW<ResourceInteractable>(resourceEntity);
            
            int amountToCollect = resource.AmountPerCollection;
            if (amountToCollect <= 0) amountToCollect = 1;
            amountToCollect = Unity.Mathematics.math.min(amountToCollect, resourceRW.ValueRO.CurrentAmount);

            // Deduct from resource
            resourceRW.ValueRW.CurrentAmount -= amountToCollect;

            // Add to inventory via buffer
            if (SystemAPI.HasBuffer<DIG.Shared.InventoryItem>(collector))
            {
                var inventory = SystemAPI.GetBuffer<DIG.Shared.InventoryItem>(collector);
                
                // Find existing stack or create new
                bool found = false;
                for (int i = 0; i < inventory.Length; i++)
                {
                    if ((byte)inventory[i].ResourceType == resourceRW.ValueRO.ResourceTypeId)
                    {
                        var item = inventory[i];
                        item.Quantity += amountToCollect;
                        inventory[i] = item;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    inventory.Add(new DIG.Shared.InventoryItem
                    {
                        ResourceType = (DIG.Shared.ResourceType)resourceRW.ValueRO.ResourceTypeId,
                        Quantity = amountToCollect
                    });
                }
            }

            // Check if depleted
            if (resourceRW.ValueRO.CurrentAmount <= 0 && resource.RespawnTime > 0)
            {
                // Add depleted tag
                var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
                ecb.AddComponent(resourceEntity, new ResourceDepleted { TimeSinceDepletion = 0f });
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }
    }
}
