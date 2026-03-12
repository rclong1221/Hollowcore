using UnityEngine;
using Unity.Entities;
using Player.Components;

namespace Player.Authoring
{
    [AddComponentMenu("DIG/Testing/RagdollTestAuthoring")]
    public class RagdollTestAuthoring : MonoBehaviour
    {
    }

    public class RagdollTestAuthoringBaker : Baker<RagdollTestAuthoring>
    {
        public override void Bake(RagdollTestAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic); 
            
            AddComponent(entity, Health.Default);
            AddComponent(entity, DeathState.Default);
            AddBuffer<DamageEvent>(entity);
            AddComponent(entity, new Unity.Physics.PhysicsVelocity()); 
        }
    }
}
