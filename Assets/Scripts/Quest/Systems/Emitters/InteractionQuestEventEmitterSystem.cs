using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Interaction;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Reads InteractionCompleteEvent and emits QuestEvent(Interact, interactableId).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(QuestEvaluationSystemGroup))]
    public partial class InteractionQuestEventEmitterSystem : SystemBase
    {
        private EntityQuery _eventQuery;

        protected override void OnCreate()
        {
            _eventQuery = GetEntityQuery(ComponentType.ReadOnly<InteractionCompleteEvent>());
            RequireForUpdate(_eventQuery);
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var interactableLookup = GetComponentLookup<Interactable>(true);
            var ltwLookup = GetComponentLookup<LocalToWorld>(true);

            foreach (var evt in SystemAPI.Query<RefRO<InteractionCompleteEvent>>())
            {
                var interactableEntity = evt.ValueRO.InteractableEntity;
                var interactorEntity = evt.ValueRO.InteractorEntity;

                int targetId = 0;
                if (interactableLookup.HasComponent(interactableEntity))
                    targetId = interactableLookup[interactableEntity].InteractableID;

                float3 position = float3.zero;
                if (ltwLookup.HasComponent(interactableEntity))
                    position = ltwLookup[interactableEntity].Position;

                var questEvent = ecb.CreateEntity();
                ecb.AddComponent(questEvent, new QuestEvent
                {
                    EventType = ObjectiveType.Interact,
                    TargetId = targetId,
                    Count = 1,
                    SourcePlayer = interactorEntity,
                    Position = position
                });
                ecb.AddComponent(questEvent, new QuestEventTag());
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
