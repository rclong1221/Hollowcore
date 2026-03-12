using Unity.Entities;
using UnityEngine;
using Player.Components;
using DIG.Combat.Authoring;
using DIG.Combat.Components;

namespace Player.Authoring
{
    /// <summary>
    /// Marker component to identify the owner of hitboxes.
    /// Add to the root character GameObject that receives damage.
    ///
    /// When placed on a child of a DamageableAuthoring entity,
    /// bakes a DamageableLink so the fixup system can copy the correct
    /// MaxHealth from the root entity at runtime.
    /// </summary>
    public class HitboxOwnerMarker : MonoBehaviour
    {
        private class Baker : Baker<HitboxOwnerMarker>
        {
            public override void Bake(HitboxOwnerMarker authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<HasHitboxes>(entity);
                AddBuffer<HitboxElement>(entity);

                // If a parent has DamageableAuthoring, link back to it
                // so the fixup system can copy the correct MaxHealth.
                var damageable = GetComponentInParent<DamageableAuthoring>();
                if (damageable != null && damageable.gameObject != authoring.gameObject)
                {
                    var rootEntity = GetEntity(damageable, TransformUsageFlags.Dynamic);
                    AddComponent(entity, new DamageableLink { DamageableRoot = rootEntity });

                    // NOTE: Reverse link (ROOT → CHILD via HitboxOwnerLink) is baked
                    // by DamageableAuthoringBaker on the ROOT entity, since only the baker
                    // that owns the ROOT entity can add components to it.
                }
            }
        }
    }
}
