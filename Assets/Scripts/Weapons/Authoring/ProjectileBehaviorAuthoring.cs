using Unity.Entities;
using UnityEngine;
using DIG.Weapons;

namespace DIG.Weapons.Authoring
{
    /// <summary>
    /// EPIC 15.13: Authoring for direct impact damage.
    /// Add this to projectiles that deal damage on direct hit (throwing knives, arrows, rocks).
    /// </summary>
    [AddComponentMenu("DIG/Projectiles/Damage On Impact")]
    public class DamageOnImpactAuthoring : MonoBehaviour
    {
        [Header("Impact Damage")]
        [Tooltip("Damage dealt on direct hit")]
        public float damage = 25f;

        [Tooltip("Type of damage for resistance calculations")]
        public global::Player.Components.DamageType damageType = global::Player.Components.DamageType.Physical;

        [Tooltip("Apply damage to the entity we directly hit")]
        public bool applyToHitEntity = true;

        [Header("Splash Damage (Optional)")]
        [Tooltip("Splash damage radius (0 = no splash)")]
        public float damageRadius = 0f;

        [Tooltip("Falloff exponent for splash damage. 1.0 = linear, 2.0 = quadratic")]
        [Range(0f, 3f)]
        public float damageFalloff = 1f;

        public class Baker : Baker<DamageOnImpactAuthoring>
        {
            public override void Bake(DamageOnImpactAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new DamageOnImpact
                {
                    Damage = authoring.damage,
                    DamageType = authoring.damageType,
                    ApplyToHitEntity = authoring.applyToHitEntity,
                    DamageRadius = authoring.damageRadius,
                    DamageFalloff = authoring.damageFalloff
                });
            }
        }
    }

    /// <summary>
    /// EPIC 15.13: Authoring for status effect on impact.
    /// Add this to projectiles that apply effects (poison arrows, fire arrows).
    /// </summary>
    [AddComponentMenu("DIG/Projectiles/Apply Status On Hit")]
    public class ApplyStatusOnHitAuthoring : MonoBehaviour
    {
        public enum StatusType : byte
        {
            None = 0,
            Burning = 1,
            Poisoned = 2,
            Slowed = 3,
            Stunned = 4,
            Bleeding = 5
        }

        [Header("Status Effect")]
        [Tooltip("Status effect to apply on hit")]
        public StatusType statusType = StatusType.Burning;

        [Tooltip("Duration of the effect in seconds")]
        public float duration = 5f;

        [Tooltip("Intensity/magnitude of the effect")]
        public float intensity = 1f;

        public class Baker : Baker<ApplyStatusOnHitAuthoring>
        {
            public override void Bake(ApplyStatusOnHitAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ApplyStatusOnHit
                {
                    StatusType = (byte)authoring.statusType,
                    Duration = authoring.duration,
                    Intensity = authoring.intensity
                });
            }
        }
    }

    /// <summary>
    /// EPIC 15.13: Authoring for area effect creation on detonation.
    /// Add this to projectiles that create persistent areas (molotov, smoke grenade).
    /// </summary>
    [AddComponentMenu("DIG/Projectiles/Create Area On Detonate")]
    public class CreateAreaOnDetonateAuthoring : MonoBehaviour
    {
        public enum AreaType : byte
        {
            Fire = 0,
            Smoke = 1,
            Gas = 2,
            Light = 3
        }

        [Header("Area Effect")]
        [Tooltip("Type of area to create")]
        public AreaType areaType = AreaType.Fire;

        [Tooltip("Radius of the area effect")]
        public float radius = 3f;

        [Tooltip("How long the area persists")]
        public float duration = 10f;

        [Tooltip("Prefab to spawn for the area (optional)")]
        public GameObject areaPrefab;

        public class Baker : Baker<CreateAreaOnDetonateAuthoring>
        {
            public override void Bake(CreateAreaOnDetonateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new CreateAreaOnDetonate
                {
                    AreaType = (byte)authoring.areaType,
                    Radius = authoring.radius,
                    Duration = authoring.duration,
                    AreaPrefab = authoring.areaPrefab != null
                        ? GetEntity(authoring.areaPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null
                });
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Color areaColor = areaType switch
            {
                AreaType.Fire => new Color(1f, 0.5f, 0f, 0.3f),
                AreaType.Smoke => new Color(0.5f, 0.5f, 0.5f, 0.3f),
                AreaType.Gas => new Color(0f, 1f, 0f, 0.3f),
                AreaType.Light => new Color(1f, 1f, 0f, 0.3f),
                _ => new Color(1f, 1f, 1f, 0.3f)
            };

            Gizmos.color = areaColor;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
