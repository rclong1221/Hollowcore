using Unity.Entities;
using Unity.NetCode;
using DIG.Targeting.Theming;

namespace DIG.Weapons
{
    /// <summary>
    /// EPIC 15.29: Base damage identity for a weapon.
    /// Determines what element the weapon fundamentally deals.
    /// Read by hit systems instead of hardcoding DamageType.Physical.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct DamageProfile : IComponentData
    {
        /// <summary>
        /// Base element this weapon deals (Physical for most weapons).
        /// </summary>
        [GhostField]
        public DamageType Element;
    }
}
