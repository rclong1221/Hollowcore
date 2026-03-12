using Unity.Entities;
using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Baker that creates an achievement child entity linked to the player.
    /// Place on the player prefab root (alongside ProgressionAuthoring, TalentAuthoring).
    /// Creates a child entity with all achievement components/buffers, adds AchievementLink on parent.
    /// Follows TalentAuthoring / SaveStateAuthoring child entity pattern.
    /// </summary>
    [AddComponentMenu("DIG/Achievement/Player Achievement")]
    public class AchievementAuthoring : MonoBehaviour
    {
        private class Baker : Baker<AchievementAuthoring>
        {
            public override void Bake(AchievementAuthoring authoring)
            {
                var playerEntity = GetEntity(TransformUsageFlags.Dynamic);

                // Create child entity for achievement data
                var childEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, "AchievementData");

                // Player gets the link (8 bytes on player archetype)
                AddComponent(playerEntity, new AchievementLink
                {
                    AchievementChild = childEntity
                });

                // Child entity gets all achievement components
                AddComponent(childEntity, new AchievementChildTag());
                AddComponent(childEntity, new AchievementOwner { Owner = playerEntity });
                AddComponent(childEntity, new AchievementCumulativeStats());
                AddComponent(childEntity, new AchievementDirtyFlags());

                // Progress buffer on child -- initialized empty, populated by AchievementInitializationSystem
                AddBuffer<AchievementProgress>(childEntity);
            }
        }
    }
}
