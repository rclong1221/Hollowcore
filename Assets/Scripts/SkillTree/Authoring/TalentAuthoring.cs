using Unity.Entities;
using UnityEngine;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Baker that creates a talent child entity linked to the player.
    /// Place on the player prefab root (alongside SaveStateAuthoring, ProgressionAuthoring).
    /// Creates a child entity with all talent components/buffers, and adds TalentLink on the parent.
    /// Follows the SaveStateAuthoring child entity pattern.
    /// </summary>
    [AddComponentMenu("DIG/Skill Tree/Player Talent")]
    public class TalentAuthoring : MonoBehaviour
    {
        private class Baker : Baker<TalentAuthoring>
        {
            public override void Bake(TalentAuthoring authoring)
            {
                var playerEntity = GetEntity(TransformUsageFlags.Dynamic);

                // Create child entity for talent data
                var childEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, "TalentData");

                // Player gets the link (8 bytes on player archetype)
                AddComponent(playerEntity, new TalentLink
                {
                    TalentChild = childEntity
                });

                // Child entity gets all talent components
                AddComponent(childEntity, new TalentChildTag());
                AddComponent(childEntity, new TalentOwner { Owner = playerEntity });

                AddComponent(childEntity, new TalentState
                {
                    TotalTalentPoints = 0,
                    SpentTalentPoints = 0,
                    ActiveTreeCount = 0,
                    RespecCount = 0
                });

                AddComponent(childEntity, new TalentPassiveStats());

                // Buffers on child entity
                AddBuffer<TalentAllocation>(childEntity);
                AddBuffer<TalentAllocationRequest>(childEntity);
                AddBuffer<TalentTreeProgress>(childEntity);
            }
        }
    }
}
