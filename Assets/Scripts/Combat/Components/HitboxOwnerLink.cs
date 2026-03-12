using Unity.Entities;

namespace DIG.Combat.Components
{
    /// <summary>
    /// Reverse link from a DamageableAuthoring ROOT entity to the HitboxOwnerMarker CHILD entity.
    ///
    /// When physics raycasts or overlap queries hit the ROOT entity's compound collider
    /// (which contains Head/Torso hitbox shapes), damage systems use this link to redirect
    /// damage to the CHILD entity where DamageEvent buffer and Health are properly tracked.
    ///
    /// Baked by HitboxOwnerMarker onto the parent DamageableAuthoring entity.
    /// Counterpart of DamageableLink (CHILD → ROOT for MaxHealth lookup).
    /// </summary>
    public struct HitboxOwnerLink : IComponentData
    {
        public Entity HitboxOwner;
    }
}
