using Unity.Entities;
using Unity.NetCode;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Link from player entity to validation child entity.
    /// 8 bytes on player archetype. All validation state lives on the child.
    /// Same pattern as SaveStateLink, TalentLink, PvPRankingLink.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ValidationLink : IComponentData
    {
        public Entity ValidationChild;
    }
}
