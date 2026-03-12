using Unity.Entities;
using Unity.NetCode;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Link from player entity to crafting knowledge child entity.
    /// Only 8 bytes on player archetype — follows TargetingModuleLink pattern.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct CraftingKnowledgeLink : IComponentData
    {
        [GhostField] public Entity KnowledgeEntity;
    }

    /// <summary>
    /// EPIC 16.13: Tag on the crafting knowledge CHILD entity.
    /// </summary>
    public struct CraftingKnowledgeTag : IComponentData { }

    /// <summary>
    /// EPIC 16.13: Known recipes buffer on the crafting knowledge CHILD entity.
    /// Ghost-replicated so clients can display available recipes.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    [InternalBufferCapacity(16)]
    public struct KnownRecipeElement : IBufferElementData
    {
        [GhostField] public int RecipeId;
    }

    /// <summary>
    /// EPIC 16.13: Transient request to unlock a recipe. Written by QuestRewardSystem or other systems.
    /// Processed and cleared by RecipeUnlockSystem.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct RecipeUnlockRequest : IBufferElementData
    {
        public int RecipeId;
    }
}
