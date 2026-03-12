using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Interaction;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Reads ProximityZoneOccupant buffers and emits QuestEvent(ReachZone, zoneEntityIndex)
    /// when a player has been in the zone long enough (threshold: 1 second).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(QuestEvaluationSystemGroup))]
    public partial class ZoneQuestEventEmitterSystem : SystemBase
    {
        private const float TimeInZoneThreshold = 1.0f;
        private EntityQuery _zoneQuery;

        protected override void OnCreate()
        {
            _zoneQuery = GetEntityQuery(
                ComponentType.ReadOnly<ProximityZone>(),
                ComponentType.ReadOnly<ProximityZoneOccupant>()
            );
            RequireForUpdate(_zoneQuery);
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var ltwLookup = GetComponentLookup<LocalToWorld>(true);
            var interactableLookup = GetComponentLookup<Interactable>(true);

            var entities = _zoneQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (int z = 0; z < entities.Length; z++)
            {
                var zoneEntity = entities[z];
                var occupants = EntityManager.GetBuffer<ProximityZoneOccupant>(zoneEntity, true);

                // Use InteractableID if available, otherwise entity index
                int zoneId = zoneEntity.Index;
                if (interactableLookup.HasComponent(zoneEntity))
                    zoneId = interactableLookup[zoneEntity].InteractableID;

                float3 zonePosition = float3.zero;
                if (ltwLookup.HasComponent(zoneEntity))
                    zonePosition = ltwLookup[zoneEntity].Position;

                for (int i = 0; i < occupants.Length; i++)
                {
                    var occupant = occupants[i];
                    if (occupant.TimeInZone < TimeInZoneThreshold)
                        continue;

                    // Only emit once per second (avoid spamming every frame)
                    float fractional = occupant.TimeInZone - TimeInZoneThreshold;
                    float dt = SystemAPI.Time.DeltaTime;
                    if (fractional > dt)
                        continue;

                    var questEvent = ecb.CreateEntity();
                    ecb.AddComponent(questEvent, new QuestEvent
                    {
                        EventType = ObjectiveType.ReachZone,
                        TargetId = zoneId,
                        Count = 1,
                        SourcePlayer = occupant.OccupantEntity,
                        Position = zonePosition
                    });
                    ecb.AddComponent(questEvent, new QuestEventTag());
                }
            }

            entities.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
