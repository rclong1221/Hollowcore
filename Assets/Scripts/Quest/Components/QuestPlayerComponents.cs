using Unity.Entities;
using Unity.NetCode;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Tracks completed quests on the player entity.
    /// Used for prerequisite checks and repeatable quest gating.
    /// ~144 bytes on player archetype (header + 16 * 8 inline).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    [InternalBufferCapacity(16)]
    public struct CompletedQuestEntry : IBufferElementData
    {
        [GhostField] public int QuestId;
        [GhostField] public uint CompletedAtTick;
    }
}
