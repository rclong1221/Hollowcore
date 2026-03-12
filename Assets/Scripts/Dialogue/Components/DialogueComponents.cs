using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Speaker configuration on NPC entities.
    /// References dialogue tree(s) and bark collection.
    /// </summary>
    public struct DialogueSpeakerData : IComponentData
    {
        public int DefaultTreeId;
        public FixedString64Bytes GreetingText;
        public BlobAssetReference<BlobArray<DialogueContextEntry>> ContextRules;
        public int BarkCollectionId;
    }

    /// <summary>
    /// Context rule entry in BlobArray. First matching condition selects TreeId.
    /// </summary>
    public struct DialogueContextEntry
    {
        public byte ConditionType;
        public int ConditionValue;
        public int TreeId;
    }

    /// <summary>
    /// EPIC 16.16: Active dialogue session state on NPC entity.
    /// Ghost:All so clients can display dialogue UI for interpolated NPC ghosts.
    /// Server-authoritative — client sends DialogueChoiceRpc, server mutates.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct DialogueSessionState : IComponentData
    {
        [GhostField] public bool IsActive;
        [GhostField] public int CurrentNodeId;
        [GhostField] public Entity InteractingPlayer;
        [GhostField] public uint SessionStartTick;
        [GhostField] public int CurrentTreeId;
        [GhostField] public byte ValidChoicesMask;
    }

    /// <summary>
    /// EPIC 16.16: Persistent flag on NPC entity. Set/cleared by dialogue actions.
    /// Used for branching on repeat visits (e.g., "already talked about X").
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DialogueFlag : IBufferElementData
    {
        public int FlagId;
        public uint SetAtTick;
    }

    /// <summary>
    /// EPIC 16.16: Transient tag marking an NPC that has pending dialogue actions to execute.
    /// Added by DialogueAdvanceSystem, consumed by DialogueActionSystem same frame.
    /// </summary>
    public struct DialogueActionPending : IComponentData
    {
        public int ActionNodeIndex;
        public int TreeId;
    }

    /// <summary>
    /// EPIC 16.16: Transient entity created by EncounterTriggerSystem for PlayDialogue action.
    /// Consumed by EncounterDialogueBridgeSystem.
    /// </summary>
    public struct PlayDialogueTrigger : IComponentData
    {
        public Entity BossEntity;
        public int DialogueIdOrBarkId;
    }
}
