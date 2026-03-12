using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// EPIC 15.10: Configuration for tools that cause an immediate explosion on Use.
    /// </summary>
    public struct InstantExplosionConfig : IComponentData
    {
        public float Radius;
        public float Damage;
        public float Range;
        public float Cooldown;
    }

    public struct InstantExplosionState : IComponentData
    {
        public float CooldownTimer;
    }
}
