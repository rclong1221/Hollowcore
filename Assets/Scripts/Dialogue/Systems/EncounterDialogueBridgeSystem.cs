using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Bridges EncounterTriggerSystem.PlayDialogue to the dialogue/bark systems.
    /// Consumes PlayDialogueTrigger transient entities created by the encounter system.
    /// If the trigger references a BarkCollectionId, creates a BarkRequest.
    /// If it references a DialogueTreeId, opens a dialogue session on the boss entity.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EncounterDialogueBridgeSystem : SystemBase
    {
        private EntityQuery _triggerQuery;

        protected override void OnCreate()
        {
            _triggerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayDialogueTrigger>());
        }

        protected override void OnUpdate()
        {
            if (_triggerQuery.IsEmptyIgnoreFilter) return;

            var entities = _triggerQuery.ToEntityArray(Allocator.Temp);
            var triggers = _triggerQuery.ToComponentDataArray<PlayDialogueTrigger>(Allocator.Temp);

            for (int i = 0; i < triggers.Length; i++)
            {
                var trigger = triggers[i];
                var bossEntity = trigger.BossEntity;

                if (bossEntity != Entity.Null &&
                    EntityManager.HasComponent<DialogueSpeakerData>(bossEntity))
                {
                    // Boss has dialogue speaker — check if it's a bark or full dialogue
                    var speaker = EntityManager.GetComponentData<DialogueSpeakerData>(bossEntity);

                    if (trigger.DialogueIdOrBarkId > 0 &&
                        EntityManager.HasComponent<DialogueSessionState>(bossEntity))
                    {
                        // Open full dialogue session
                        var session = EntityManager.GetComponentData<DialogueSessionState>(bossEntity);
                        if (!session.IsActive)
                        {
                            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;
                            session.IsActive = true;
                            session.CurrentTreeId = trigger.DialogueIdOrBarkId;
                            session.SessionStartTick = (uint)tick;

                            if (SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry))
                            {
                                var tree = registry.GetTree(trigger.DialogueIdOrBarkId);
                                if (tree != null)
                                    session.CurrentNodeId = tree.StartNodeId;
                            }

                            EntityManager.SetComponentData(bossEntity, session);
                        }
                    }
                    else if (speaker.BarkCollectionId > 0)
                    {
                        // Create bark request for combat yell
                        if (EntityManager.HasComponent<Unity.Transforms.LocalToWorld>(bossEntity))
                        {
                            var ltw = EntityManager.GetComponentData<Unity.Transforms.LocalToWorld>(bossEntity);
                            var barkEntity = EntityManager.CreateEntity();
                            EntityManager.AddComponentData(barkEntity, new BarkRequest
                            {
                                EmitterEntity = bossEntity,
                                LineIndex = -1, // -1 = random selection
                                Position = ltw.Position
                            });
                        }
                    }
                }

                EntityManager.DestroyEntity(entities[i]);
            }

            entities.Dispose();
            triggers.Dispose();
        }
    }
}
