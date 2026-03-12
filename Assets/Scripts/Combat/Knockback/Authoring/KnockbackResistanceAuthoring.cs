using Unity.Entities;
using UnityEngine;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Configures knockback resistance for an entity.
    /// Optional — entities without this receive full knockback (zero resistance).
    /// </summary>
    public class KnockbackResistanceAuthoring : MonoBehaviour
    {
        [Header("Resistance")]
        [Tooltip("0 = full knockback, 0.5 = half, 1.0 = immune")]
        [Range(0f, 1f)]
        public float ResistancePercent = 0f;

        [Tooltip("Force below this threshold is ignored. 0 = any force works. 500 = explosions+ only")]
        public float SuperArmorThreshold = 0f;

        [Header("Immunity")]
        [Tooltip("Seconds of knockback immunity after a knockback ends. Prevents stunlock")]
        public float ImmunityDuration = 0.2f;

        [Tooltip("Start immune (boss prefabs, turrets)")]
        public bool StartImmune = false;

        private class Baker : Baker<KnockbackResistanceAuthoring>
        {
            public override void Bake(KnockbackResistanceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new KnockbackResistance
                {
                    ResistancePercent = authoring.ResistancePercent,
                    SuperArmorThreshold = authoring.SuperArmorThreshold,
                    ImmunityDuration = authoring.ImmunityDuration,
                    ImmunityTimeRemaining = 0f,
                    IsImmune = authoring.StartImmune
                });
            }
        }
    }
}
