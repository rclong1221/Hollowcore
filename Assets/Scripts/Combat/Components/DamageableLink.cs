using Unity.Entities;

namespace DIG.Combat.Components
{
    /// <summary>
    /// Links a physics body entity back to its damageable root entity.
    /// Unity Physics extracts dynamic bodies from hierarchies during baking,
    /// removing Parent components. This link allows hitscan raycasts to resolve
    /// from the hit physics entity back to the entity with Health/DamageEvent.
    /// Baked by DamageableAuthoring onto all child entities in the prefab.
    /// </summary>
    public struct DamageableLink : IComponentData
    {
        public Entity DamageableRoot;
    }
}
