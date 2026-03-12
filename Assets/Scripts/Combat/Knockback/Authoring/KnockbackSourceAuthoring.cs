using Unity.Entities;
using UnityEngine;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Authoring for environmental knockback sources (steam vents, push traps, etc.).
    /// KnockbackTriggerSystem creates KnockbackRequests when entities enter this trigger volume.
    /// </summary>
    public class KnockbackSourceAuthoring : MonoBehaviour
    {
        [Header("Knockback Settings")]
        [Tooltip("Force in Newtons. 200=light, 500=medium, 1000=heavy")]
        public float Force = 500f;

        public KnockbackType Type = KnockbackType.Push;
        public KnockbackEasing Easing = KnockbackEasing.EaseOut;
        public KnockbackFalloff Falloff = KnockbackFalloff.None;

        [Tooltip("0 = contact only, >0 = area effect")]
        public float Radius = 0f;

        [Tooltip("Trigger interrupt on target")]
        public bool TriggersInterrupt = false;

        [Tooltip("Seconds between knockbacks on same target")]
        public float Cooldown = 1.0f;

        private class Baker : Baker<KnockbackSourceAuthoring>
        {
            public override void Bake(KnockbackSourceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new KnockbackSourceConfig
                {
                    Force = authoring.Force,
                    Type = authoring.Type,
                    Easing = authoring.Easing,
                    Falloff = authoring.Falloff,
                    Radius = authoring.Radius,
                    TriggersInterrupt = authoring.TriggersInterrupt,
                    Cooldown = authoring.Cooldown,
                    LastTriggerTime = -999f
                });
            }
        }
    }
}
