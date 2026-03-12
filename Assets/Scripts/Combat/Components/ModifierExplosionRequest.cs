using Unity.Entities;
using Unity.Mathematics;
using DIG.Targeting.Theming;

namespace DIG.Combat.Components
{
    /// <summary>
    /// EPIC 15.29: Event entity created by modifier processing for AOE explosion effects.
    /// Consumed and destroyed by ModifierExplosionSystem.
    /// </summary>
    public struct ModifierExplosionRequest : IComponentData
    {
        public float3 Position;
        public Entity SourceEntity;
        public float Damage;
        public float Radius;
        public DamageType Element;
        public float KnockbackForce;
    }
}
