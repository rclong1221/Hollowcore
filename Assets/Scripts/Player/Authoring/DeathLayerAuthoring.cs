using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    public class DeathLayerAuthoring : MonoBehaviour
    {
        public int DeadLayerIndex = 0; // Default

        class Baker : Baker<DeathLayerAuthoring>
        {
            public override void Bake(DeathLayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new DeathLayerSettings
                {
                    DeadLayer = authoring.DeadLayerIndex,
                    DeadCollisionMask = 0 // Handled by System
                });
            }
        }
    }
}
