using Unity.Entities;
using UnityEngine;
using DIG.Survival.Physics;

namespace DIG.Survival.Physics.Authoring
{
    public class PushInteractionAuthoring : MonoBehaviour
    {
        public class Baker : Baker<PushInteractionAuthoring>
        {
            public override void Bake(PushInteractionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new ActivePushConstraint
                {
                    IsPushing = false,
                    TargetObject = Entity.Null,
                    PhysicsJoint = Entity.Null
                });
                
                AddComponent(entity, PushSettings.Default);
            }
        }
    }
}
