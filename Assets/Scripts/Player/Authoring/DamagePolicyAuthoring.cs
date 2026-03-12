using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    public class DamagePolicyAuthoring : MonoBehaviour
    {
        [Header("Cooldowns (Seconds)")]
        [Tooltip("Minimum time between damage events of this type.")]
        public float PhysicalCooldown = 0f;
        public float HeatCooldown = 0.5f;
        public float RadiationCooldown = 1.0f;
        public float SuffocationCooldown = 0f; // Streaming usually
        public float ExplosionCooldown = 0.1f;
        public float ToxicCooldown = 1.0f;

        class Baker : Baker<DamagePolicyAuthoring>
        {
            public override void Bake(DamagePolicyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new DamagePolicy
                {
                    DefaultPhysicalCooldown = authoring.PhysicalCooldown,
                    DefaultHeatCooldown = authoring.HeatCooldown,
                    DefaultRadiationCooldown = authoring.RadiationCooldown,
                    DefaultSuffocationCooldown = authoring.SuffocationCooldown,
                    DefaultExplosionCooldown = authoring.ExplosionCooldown,
                    DefaultToxicCooldown = authoring.ToxicCooldown
                });
            }
        }
    }
}
