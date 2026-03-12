using Unity.Entities;
using UnityEngine;
using DIG.Voxel.Components;

namespace DIG.Voxel.Authoring
{
    /// <summary>
    /// EPIC 15.10: Authoring for tools that explode instantly on click (Power Hammers, Explosive Knives).
    /// </summary>
    [AddComponentMenu("DIG/Voxel/Instant Explosion Tool")]
    public class InstantExplosionTool : MonoBehaviour
    {
        public float Radius = 4f;
        public float Damage = 100f;
        public float Range = 3f;
        public float Cooldown = 1f;
    }

    public class InstantExplosionBaker : Baker<InstantExplosionTool>
    {
        public override void Bake(InstantExplosionTool authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new InstantExplosionConfig
            {
                Radius = authoring.Radius,
                Damage = authoring.Damage,
                Range = authoring.Range,
                Cooldown = authoring.Cooldown
            });
            AddComponent(entity, new InstantExplosionState { CooldownTimer = 0f });
        }
    }
}
