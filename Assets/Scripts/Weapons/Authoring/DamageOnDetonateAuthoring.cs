using Unity.Entities;
using UnityEngine;
using DIG.Weapons;

namespace DIG.Weapons.Authoring
{
    /// <summary>
    /// EPIC 15.13: Authoring for explosion damage on detonation.
    /// Add this to explosives that deal area damage (grenades, rockets).
    /// </summary>
    [AddComponentMenu("DIG/Projectiles/Explosion Damage")]
    public class DamageOnDetonateAuthoring : MonoBehaviour
    {
        [Header("Explosion Damage")]
        [Tooltip("Damage at explosion center")]
        public float damage = 100f;

        [Tooltip("Radius of damage effect")]
        public float radius = 5f;

        [Tooltip("Type of damage for resistance calculations")]
        public global::Player.Components.DamageType damageType = global::Player.Components.DamageType.Explosion;

        [Tooltip("Falloff exponent for damage over distance. 1.0 = linear, 2.0 = quadratic (realistic)")]
        [Range(0f, 3f)]
        public float falloffExponent = 2f;

        [Tooltip("Minimum damage multiplier at edge of radius")]
        [Range(0f, 1f)]
        public float edgeDamageMultiplier = 0.1f;

        public class Baker : Baker<DamageOnDetonateAuthoring>
        {
            public override void Bake(DamageOnDetonateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new DamageOnDetonate
                {
                    Damage = authoring.damage,
                    Radius = authoring.radius,
                    DamageType = authoring.damageType,
                    FalloffExponent = authoring.falloffExponent,
                    EdgeDamageMultiplier = authoring.edgeDamageMultiplier
                });
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw damage radius
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, radius);

            // Draw full damage core
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius * 0.2f);
        }
#endif
    }
}
