using Unity.Entities;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Configuration for environmental knockback sources.
    /// Steam vents, push traps, geyser tiles, conveyor belt ends.
    /// KnockbackTriggerSystem creates KnockbackRequests when entities enter trigger volume.
    /// </summary>
    public struct KnockbackSourceConfig : IComponentData
    {
        public float Force;
        public KnockbackType Type;
        public KnockbackEasing Easing;
        public KnockbackFalloff Falloff;
        public float Radius;
        public bool TriggersInterrupt;
        public float Cooldown;
        public float LastTriggerTime;
    }
}
