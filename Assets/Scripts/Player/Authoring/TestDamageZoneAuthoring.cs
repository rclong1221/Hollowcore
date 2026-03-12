using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    // T3: Damage Zone Authoring
    public class TestDamageZoneAuthoring : MonoBehaviour
    {
        public float DamagePerSecond = 5.0f;
        public float Radius = 5.0f;
        public DamageType Type = DamageType.Physical;

        class Baker : Baker<TestDamageZoneAuthoring>
        {
            public override void Bake(TestDamageZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new TestDamageZone
                {
                    DamagePerSecond = authoring.DamagePerSecond,
                    Radius = authoring.Radius,
                    Type = authoring.Type
                });
            }
        }
    }
}
