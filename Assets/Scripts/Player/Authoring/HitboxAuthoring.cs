using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    /// <summary>
    /// Authoring component for hitbox regions (13.16.1).
    /// Attach to child GameObjects with colliders to define damage zones.
    /// </summary>
    public class HitboxAuthoring : MonoBehaviour
    {
        [Header("Hitbox Settings")]
        [Tooltip("Damage multiplier for this region. 2.0 = headshot, 0.5 = legs")]
        [Range(0.1f, 5.0f)]
        public float DamageMultiplier = 1.0f;

        [Tooltip("Region type for effects and feedback")]
        public HitboxRegion Region = HitboxRegion.Torso;

        private class Baker : Baker<HitboxAuthoring>
        {
            public override void Bake(HitboxAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Find the owner entity (parent with Health component)
                // During baking, we'll set this to Entity.Null and resolve at runtime
                // Or we can try to find the parent here
                Transform parent = authoring.transform.parent;
                Entity ownerEntity = Entity.Null;

                // Walk up hierarchy to find the root character
                while (parent != null)
                {
                    // Check if parent has a Health-related authoring component
                    if (parent.GetComponent<HitboxOwnerMarker>() != null)
                    {
                        ownerEntity = GetEntity(parent, TransformUsageFlags.Dynamic);
                        break;
                    }
                    parent = parent.parent;
                }

                // If no explicit marker, use the root
                if (ownerEntity == Entity.Null && authoring.transform.root != authoring.transform)
                {
                    ownerEntity = GetEntity(authoring.transform.root, TransformUsageFlags.Dynamic);
                }

                AddComponent(entity, new Hitbox
                {
                    OwnerEntity = ownerEntity,
                    DamageMultiplier = authoring.DamageMultiplier,
                    Region = authoring.Region
                });
            }
        }
    }
}
