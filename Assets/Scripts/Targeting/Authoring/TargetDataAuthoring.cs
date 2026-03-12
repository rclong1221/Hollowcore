using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Targeting
{
    /// <summary>
    /// Authoring component to add TargetData to an entity.
    /// Attach to player prefab to enable targeting system integration.
    /// </summary>
    public class TargetDataAuthoring : MonoBehaviour
    {
        [Tooltip("Initial targeting mode. Can be changed at runtime.")]
        public TargetingMode InitialMode = TargetingMode.CameraRaycast;
        
        public class Baker : Baker<TargetDataAuthoring>
        {
            public override void Bake(TargetDataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Core targeting data (updated by ITargetingSystem implementations)
                AddComponent(entity, new TargetData
                {
                    TargetEntity = Entity.Null,
                    TargetPoint = float3.zero,
                    AimDirection = new float3(0, 0, 1), // Forward
                    HasValidTarget = false,
                    TargetDistance = 0f,
                    Mode = authoring.InitialMode
                });
                
                // Runtime modifiers (written by stat system for skills/items/buffs)
                AddComponent(entity, TargetingModifiers.Default);
            }
        }
    }
}
