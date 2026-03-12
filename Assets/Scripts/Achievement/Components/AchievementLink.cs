using Unity.Entities;
using Unity.NetCode;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: 8-byte link on player entity to child entity holding all achievement data.
    /// Follows TalentLink / SaveStateLink pattern.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct AchievementLink : IComponentData
    {
        [GhostField] public Entity AchievementChild;
    }
}
