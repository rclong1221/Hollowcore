using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    public class HealStationAuthoring : MonoBehaviour
    {
        public float HealAmount = 10.0f;
        public float HealInterval = 1.0f;
        public float Radius = 3.0f;

        class Baker : Baker<HealStationAuthoring>
        {
            public override void Bake(HealStationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new HealStation
                {
                    HealAmount = authoring.HealAmount,
                    HealInterval = authoring.HealInterval,
                    Timer = 0,
                    Radius = authoring.Radius
                });
            }
        }
    }
}
