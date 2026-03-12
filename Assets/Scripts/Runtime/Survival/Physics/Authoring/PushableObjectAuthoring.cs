using Unity.Entities;
using UnityEngine;
using DIG.Survival.Physics;

namespace DIG.Survival.Physics.Authoring
{
    public class PushableObjectAuthoring : MonoBehaviour
    {
        [Header("Physics Properties")]
        public float Mass = 50f;
        public float Friction = 0.5f;

        public class Baker : Baker<PushableObjectAuthoring>
        {
            public override void Bake(PushableObjectAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new PushableObject
                {
                    Mass = authoring.Mass,
                    Friction = authoring.Friction
                });
            }
        }
    }
}
