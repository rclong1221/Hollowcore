using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    // T2/T8: Turret Authoring
    public class TestTurretAuthoring : MonoBehaviour
    {
        public float DamageAmount = 10.0f;
        public float Interval = 1.0f;
        public float Range = 20.0f;
        public DamageType Type = DamageType.Physical;

        class Baker : Baker<TestTurretAuthoring>
        {
            public override void Bake(TestTurretAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new TestDamageSource
                {
                    DamageAmount = authoring.DamageAmount,
                    Interval = authoring.Interval,
                    Range = authoring.Range,
                    Type = authoring.Type,
                    Timer = 0
                });
            }
        }
    }
}
