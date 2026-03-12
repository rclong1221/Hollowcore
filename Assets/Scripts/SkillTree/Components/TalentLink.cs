using Unity.Entities;
using Unity.NetCode;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: 8-byte link on player entity pointing to talent child entity.
    /// All talent data lives on the child (TalentState, buffers, passives).
    /// Follows SaveStateLink/CraftingKnowledgeLink child entity pattern.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct TalentLink : IComponentData
    {
        [GhostField] public Entity TalentChild;
    }
}
