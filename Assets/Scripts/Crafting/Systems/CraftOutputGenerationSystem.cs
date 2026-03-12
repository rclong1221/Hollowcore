using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Creates CraftOutputElement entries from completed CraftQueueElement items.
    /// Server-only managed SystemBase (needs registry access for recipe output data).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CraftQueueProcessingSystem))]
    public partial class CraftOutputGenerationSystem : SystemBase
    {
        private EntityQuery _stationQuery;
        private EntityQuery _registryQuery;

        protected override void OnCreate()
        {
            _stationQuery = GetEntityQuery(
                ComponentType.ReadOnly<CraftingStation>(),
                ComponentType.ReadWrite<CraftQueueElement>(),
                ComponentType.ReadWrite<CraftOutputElement>());
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<RecipeRegistryManaged>());
        }

        protected override void OnUpdate()
        {
            if (_registryQuery.CalculateEntityCount() == 0) return;

            var registryEntity = _registryQuery.GetSingletonEntity();
            var registry = EntityManager.GetComponentObject<RecipeRegistryManaged>(registryEntity);

            var entities = _stationQuery.ToEntityArray(Allocator.Temp);

            for (int s = 0; s < entities.Length; s++)
            {
                var stationEntity = entities[s];
                var queue = EntityManager.GetBuffer<CraftQueueElement>(stationEntity);
                var outputs = EntityManager.GetBuffer<CraftOutputElement>(stationEntity);

                // Iterate backwards to safely remove completed items
                for (int i = queue.Length - 1; i >= 0; i--)
                {
                    var elem = queue[i];
                    if (elem.State != CraftState.Complete) continue;

                    if (!registry.ManagedEntries.TryGetValue(elem.RecipeId, out var recipeDef))
                    {
                        queue.RemoveAt(i);
                        continue;
                    }

                    var output = recipeDef.Output;
                    outputs.Add(new CraftOutputElement
                    {
                        RecipeId = elem.RecipeId,
                        OutputItemTypeId = output.ItemTypeId,
                        OutputQuantity = output.Quantity,
                        OutputType = (byte)output.OutputType,
                        OutputResourceType = (byte)output.ResourceType,
                        ForPlayer = elem.RequestingPlayer
                    });

                    queue.RemoveAt(i);

                    // EPIC 16.14: Grant crafting XP to requesting player
                    // XP scales with recipe tier: Tier * CraftXPBase(50)
                    if (elem.RequestingPlayer != Entity.Null)
                    {
                        int craftXP = System.Math.Max(1, recipeDef.Tier) * 50;
                        DIG.Progression.XPGrantAPI.GrantXP(EntityManager, elem.RequestingPlayer,
                            craftXP, DIG.Progression.XPSourceType.Crafting);
                    }

                    CraftingEventQueue.Enqueue(CraftingUIEventType.CraftCompleted, elem.RecipeId);
                }
            }

            entities.Dispose();
        }
    }
}
