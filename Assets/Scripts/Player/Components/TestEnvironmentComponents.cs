using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Player.Components;
using Unity.Collections;

namespace Player.Components
{
    // T2/T8: Simple Turret / Damage Source
    public struct TestDamageSource : IComponentData
    {
        public float DamageAmount;
        public float Interval;
        public float Timer;
        public float Range;
        public DamageType Type;
    }

    // T3: Damage Zone (Area Effect)
    public struct TestDamageZone : IComponentData
    {
        public float DamagePerSecond;
        public float Radius;
        public DamageType Type;
    }

    // T9: Resistance Config
    public struct TestResistanceConfig : IComponentData 
    {
    }
}
