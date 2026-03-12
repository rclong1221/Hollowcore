using Unity.Entities;
using UnityEngine;
using DIG.Weapons;

namespace DIG.Weapons.Authoring
{
    /// <summary>
    /// EPIC 15.13: Authoring for stick-on-impact behavior.
    /// Add this to projectiles that embed in targets (throwing knives, arrows).
    /// </summary>
    [AddComponentMenu("DIG/Projectiles/Stick On Impact")]
    public class StickOnImpactAuthoring : MonoBehaviour
    {
        [Header("Stick Behavior")]
        [Tooltip("Stick to entities (players, enemies)")]
        public bool stickToEntities = true;

        [Tooltip("Stick to world geometry")]
        public bool stickToWorld = true;

        [Tooltip("How deep to embed into surfaces")]
        public float penetrationDepth = 0.1f;

        [Tooltip("Rotate projectile to align with surface normal")]
        public bool alignToSurface = true;

        public class Baker : Baker<StickOnImpactAuthoring>
        {
            public override void Bake(StickOnImpactAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new StickOnImpact
                {
                    StickToEntities = authoring.stickToEntities,
                    StickToWorld = authoring.stickToWorld,
                    PenetrationDepth = authoring.penetrationDepth,
                    AlignToSurface = authoring.alignToSurface,
                    IsStuck = false
                });
            }
        }
    }
}
