using Unity.Entities;
using UnityEngine;
using DIG.Weapons;

namespace DIG.Weapons.Authoring
{
    /// <summary>
    /// EPIC 15.13: Authoring for bounce behavior.
    /// Add this to projectiles that bounce (grenades, rocks).
    /// </summary>
    [AddComponentMenu("DIG/Projectiles/Bounce On Impact")]
    public class BounceOnImpactAuthoring : MonoBehaviour
    {
        [Header("Bounce Behavior")]
        [Tooltip("Energy retained per bounce (0-1). 0.6 = 60% velocity retained")]
        [Range(0f, 1f)]
        public float bounciness = 0.6f;

        [Tooltip("Maximum bounces before stopping")]
        public int maxBounces = 3;

        public class Baker : Baker<BounceOnImpactAuthoring>
        {
            public override void Bake(BounceOnImpactAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ProjectileBounce
                {
                    Bounciness = authoring.bounciness,
                    MaxBounces = authoring.maxBounces,
                    CurrentBounces = 0
                });
            }
        }
    }
}
