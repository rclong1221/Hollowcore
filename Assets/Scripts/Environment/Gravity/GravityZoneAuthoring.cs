using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Environment.Gravity
{
    public struct GravityZoneComponent : IComponentData
    {
        public float3 Center;
        public float Radius;
        public float Strength;
        public float Falloff; // 0 = linear, 1 = constant, etc.
    }

    public class GravityZoneAuthoring : MonoBehaviour
    {
        public float Radius = 10f;
        public float Strength = 9.81f;
        [Tooltip("Influence Falloff")]
        public float Falloff = 1f;

        class Baker : Baker<GravityZoneAuthoring>
        {
            public override void Bake(GravityZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GravityZoneComponent
                {
                    Center = float3.zero, // Local to transform? Usually systems handle World transform
                    Radius = authoring.Radius,
                    Strength = authoring.Strength,
                    Falloff = authoring.Falloff
                });
                
                // Ensure we have a ZoneTrigger tag if not already present
                // (Optional, if we want generic zone queries)
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0, 0, 1, 0.2f);
            Gizmos.DrawSphere(transform.position, Radius);
        }
    }
}
