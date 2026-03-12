using Unity.Entities;
using Unity.NetCode;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: 8 bytes on player entity. Points to the party entity that
    /// holds all membership/state data. Entity.Null = not in a party.
    /// Follows SaveStateLink/TalentLink child entity pattern.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PartyLink : IComponentData
    {
        [GhostField] public Entity PartyEntity;
    }
}
