using Unity.Entities;
using Unity.NetCode;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Progress state of a quest instance.
    /// Lives on separate QuestInstance entities (NOT on the player) to avoid 16KB archetype pressure.
    /// One entity per active quest per player.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct QuestProgress : IComponentData
    {
        [GhostField] public int QuestId;
        [GhostField] public QuestState State;
        [GhostField(Quantization = 100)] public float TimeRemaining;
        [GhostField] public uint AcceptedAtTick;
    }

    /// <summary>
    /// EPIC 16.12: Links a QuestInstance entity to its owning player.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct QuestPlayerLink : IComponentData
    {
        [GhostField] public Entity PlayerEntity;
    }

    /// <summary>
    /// EPIC 16.12: Per-objective progress within a quest.
    /// Buffer on QuestInstance entities. Matches ObjectiveDefinition by ObjectiveId.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    [InternalBufferCapacity(8)]
    public struct ObjectiveProgress : IBufferElementData
    {
        [GhostField] public int ObjectiveId;
        [GhostField] public ObjectiveState State;
        [GhostField] public ObjectiveType Type;
        [GhostField] public int TargetId;
        [GhostField] public int CurrentCount;
        [GhostField] public int RequiredCount;
        [GhostField] public bool IsOptional;
        [GhostField] public bool IsHidden;
        [GhostField] public int UnlockAfterObjectiveId;
    }

    /// <summary>
    /// EPIC 16.12: Managed singleton holding the quest registry.
    /// Created by QuestRegistryBootstrapSystem, accessed via SystemAPI.ManagedAPI.GetSingleton.
    /// </summary>
    public class QuestRegistryManaged : IComponentData
    {
        public QuestDatabaseSO Database;
        public System.Collections.Generic.Dictionary<int, QuestDefinitionSO> ManagedEntries;
    }
}
