using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Items;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Reads PickupEvent and emits QuestEvent(Collect, itemTypeId).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(QuestEvaluationSystemGroup))]
    public partial class CollectQuestEventEmitterSystem : SystemBase
    {
        private EntityQuery _pickupQuery;

        protected override void OnCreate()
        {
            _pickupQuery = GetEntityQuery(
                ComponentType.ReadOnly<PickupEvent>()
            );
            RequireForUpdate(_pickupQuery);
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var itemPickupLookup = GetComponentLookup<ItemPickup>(true);
            var ltwLookup = GetComponentLookup<LocalToWorld>(true);

            foreach (var (evt, entity) in SystemAPI.Query<RefRO<PickupEvent>>().WithEntityAccess())
            {
                var pickupEntity = evt.ValueRO.PickupEntity;
                var playerEntity = evt.ValueRO.PlayerEntity;

                if (pickupEntity == Entity.Null || playerEntity == Entity.Null)
                    continue;

                int targetId = 0;
                int quantity = 1;
                if (itemPickupLookup.HasComponent(pickupEntity))
                {
                    targetId = itemPickupLookup[pickupEntity].ItemTypeId;
                    quantity = itemPickupLookup[pickupEntity].Quantity;
                }

                float3 position = float3.zero;
                if (ltwLookup.HasComponent(pickupEntity))
                    position = ltwLookup[pickupEntity].Position;

                var questEvent = ecb.CreateEntity();
                ecb.AddComponent(questEvent, new QuestEvent
                {
                    EventType = ObjectiveType.Collect,
                    TargetId = targetId,
                    Count = quantity,
                    SourcePlayer = playerEntity,
                    Position = position
                });
                ecb.AddComponent(questEvent, new QuestEventTag());
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
