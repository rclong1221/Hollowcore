using Unity.Entities;
using UnityEngine;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Authoring component for player crafting knowledge.
    /// Place on the player prefab. Baker creates a child entity with
    /// CraftingKnowledgeTag + KnownRecipeElement buffer + RecipeUnlockRequest buffer.
    /// Links to player via CraftingKnowledgeLink (8 bytes on player archetype).
    /// Follows TargetingModuleLink child entity pattern.
    ///
    /// SETUP: Add this as a child GameObject of the player prefab
    /// (same pattern as TargetingModuleAuthoring).
    /// </summary>
    [AddComponentMenu("DIG/Crafting/Crafting Knowledge")]
    public class CraftingKnowledgeAuthoring : MonoBehaviour
    {
        [Tooltip("Recipe IDs the player starts with. AlwaysAvailable recipes don't need to be listed.")]
        public int[] StarterRecipeIds = new int[0];

        public class Baker : Baker<CraftingKnowledgeAuthoring>
        {
            public override void Bake(CraftingKnowledgeAuthoring authoring)
            {
                // This is a child entity (placed on child GO under player prefab)
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent<CraftingKnowledgeTag>(entity);

                var knownBuffer = AddBuffer<KnownRecipeElement>(entity);
                if (authoring.StarterRecipeIds != null)
                {
                    foreach (int recipeId in authoring.StarterRecipeIds)
                    {
                        knownBuffer.Add(new KnownRecipeElement { RecipeId = recipeId });
                    }
                }

                AddBuffer<RecipeUnlockRequest>(entity);

                // The parent Player entity needs CraftingKnowledgeLink
                // This is handled by CraftingKnowledgeLinkSystem at runtime,
                // which finds CraftingKnowledgeTag entities with Parent component
                // and sets the link on the parent player entity.
            }
        }
    }
}
