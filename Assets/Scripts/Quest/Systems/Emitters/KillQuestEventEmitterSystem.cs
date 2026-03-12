using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Reads KillCredited events on player entities and emits QuestEvent(Kill, ghostType).
    /// KillCredited is created by DeathTransitionSystem via EndSimulationECB.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(QuestEvaluationSystemGroup))]
    public partial class KillQuestEventEmitterSystem : SystemBase
    {
        private EntityQuery _killQuery;

        protected override void OnCreate()
        {
            _killQuery = GetEntityQuery(
                ComponentType.ReadOnly<KillCredited>(),
                ComponentType.ReadOnly<PlayerTag>()
            );
            RequireForUpdate(_killQuery);
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var ghostLookup = GetComponentLookup<GhostInstance>(true);
            var ltw = GetComponentLookup<LocalToWorld>(true);

            var entities = _killQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var kills = _killQuery.ToComponentDataArray<KillCredited>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var kill = kills[i];
                var playerEntity = entities[i];

                // Get victim's ghost type as TargetId (unique per prefab type)
                int targetId = 0;
                if (ghostLookup.HasComponent(kill.Victim))
                    targetId = ghostLookup[kill.Victim].ghostType;

                float3 position = kill.VictimPosition;

                var questEvent = ecb.CreateEntity();
                ecb.AddComponent(questEvent, new QuestEvent
                {
                    EventType = ObjectiveType.Kill,
                    TargetId = targetId,
                    Count = 1,
                    SourcePlayer = playerEntity,
                    Position = position
                });
                ecb.AddComponent(questEvent, new QuestEventTag());
            }

            entities.Dispose();
            kills.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
