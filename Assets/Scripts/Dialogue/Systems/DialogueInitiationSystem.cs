using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Interaction;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Opens dialogue sessions when a player uses InteractionVerb.Talk
    /// on an NPC with DialogueSpeakerData. Evaluates context rules for tree selection.
    /// Uses manual EntityQuery per MEMORY.md (SystemAPI.Query issues with transient types).
    /// Uses cached ComponentLookup for O(1) random-access reads.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DialogueInitiationSystem : SystemBase
    {
        private EntityQuery _interactionQuery;
        private ComponentLookup<DialogueSpeakerData> _speakerLookup;
        private ComponentLookup<DialogueSessionState> _sessionLookup;
        private ComponentLookup<InteractableContext> _contextLookup;
        private ComponentLookup<DIG.Combat.Components.CharacterAttributes> _attributesLookup;
        private ComponentLookup<DIG.Economy.CurrencyInventory> _currencyLookup;
        private ComponentLookup<DIG.Aggro.Components.AlertState> _alertLookup;
        private BufferLookup<DialogueFlag> _flagLookup;

        protected override void OnCreate()
        {
            _interactionQuery = GetEntityQuery(ComponentType.ReadOnly<InteractionCompleteEvent>());
            RequireForUpdate<DialogueConfig>();

            _speakerLookup = GetComponentLookup<DialogueSpeakerData>(true);
            _sessionLookup = GetComponentLookup<DialogueSessionState>(false);
            _contextLookup = GetComponentLookup<InteractableContext>(true);
            _attributesLookup = GetComponentLookup<DIG.Combat.Components.CharacterAttributes>(true);
            _currencyLookup = GetComponentLookup<DIG.Economy.CurrencyInventory>(true);
            _alertLookup = GetComponentLookup<DIG.Aggro.Components.AlertState>(true);
            _flagLookup = GetBufferLookup<DialogueFlag>(true);
        }

        protected override void OnUpdate()
        {
            if (_interactionQuery.IsEmptyIgnoreFilter) return;

            // Refresh lookups
            _speakerLookup.Update(this);
            _sessionLookup.Update(this);
            _contextLookup.Update(this);
            _attributesLookup.Update(this);
            _currencyLookup.Update(this);
            _alertLookup.Update(this);
            _flagLookup.Update(this);

            var events = _interactionQuery.ToComponentDataArray<InteractionCompleteEvent>(Allocator.Temp);
            var eventEntities = _interactionQuery.ToEntityArray(Allocator.Temp);
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;

            for (int i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                var npcEntity = evt.InteractableEntity;

                // Must have DialogueSpeakerData
                if (!_speakerLookup.HasComponent(npcEntity)) continue;

                // Check interaction verb is Talk
                if (_contextLookup.HasComponent(npcEntity))
                {
                    var ctx = _contextLookup[npcEntity];
                    if (ctx.Verb != InteractionVerb.Talk) continue;
                }

                // Must have DialogueSessionState
                if (!_sessionLookup.HasComponent(npcEntity)) continue;

                // Reject if NPC already in dialogue
                var session = _sessionLookup[npcEntity];
                if (session.IsActive) continue;

                var speaker = _speakerLookup[npcEntity];

                // Evaluate context rules for tree selection
                int selectedTreeId = speaker.DefaultTreeId;
                if (speaker.ContextRules.IsCreated)
                {
                    ref var rules = ref speaker.ContextRules.Value;
                    for (int r = 0; r < rules.Length; r++)
                    {
                        if (EvaluateCondition(
                            (DialogueConditionType)rules[r].ConditionType,
                            rules[r].ConditionValue,
                            evt.InteractorEntity,
                            npcEntity))
                        {
                            selectedTreeId = rules[r].TreeId;
                            break;
                        }
                    }
                }

                // Look up tree start node
                if (!SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry)) continue;
                var tree = registry.GetTree(selectedTreeId);
                if (tree == null) continue;

                // Open session
                session.IsActive = true;
                session.CurrentNodeId = tree.StartNodeId;
                session.InteractingPlayer = evt.InteractorEntity;
                session.SessionStartTick = (uint)tick;
                session.CurrentTreeId = selectedTreeId;
                session.ValidChoicesMask = 0;
                _sessionLookup[npcEntity] = session;
            }

            events.Dispose();
            eventEntities.Dispose();
        }

        private bool EvaluateCondition(DialogueConditionType type, int value,
            Entity playerEntity, Entity npcEntity)
        {
            switch (type)
            {
                case DialogueConditionType.None:
                    return true;

                case DialogueConditionType.DialogueFlag:
                    return HasDialogueFlag(npcEntity, value);

                case DialogueConditionType.DialogueFlagClear:
                    return !HasDialogueFlag(npcEntity, value);

                case DialogueConditionType.PlayerLevel:
                    if (_attributesLookup.HasComponent(playerEntity))
                        return _attributesLookup[playerEntity].Level >= value;
                    return false;

                case DialogueConditionType.HasCurrency:
                    if (_currencyLookup.HasComponent(playerEntity))
                        return _currencyLookup[playerEntity].Gold >= value;
                    return false;

                case DialogueConditionType.AlertLevelBelow:
                    if (_alertLookup.HasComponent(npcEntity))
                        return _alertLookup[npcEntity].AlertLevel < value;
                    return true;

                default:
                    return false;
            }
        }

        private bool HasDialogueFlag(Entity npcEntity, int flagId)
        {
            if (!_flagLookup.HasBuffer(npcEntity)) return false;
            var flags = _flagLookup[npcEntity];
            for (int i = 0; i < flags.Length; i++)
                if (flags[i].FlagId == flagId) return true;
            return false;
        }
    }
}
