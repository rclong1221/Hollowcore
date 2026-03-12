using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel;

namespace DIG.Voxel.Authoring
{
    public class ChainTriggerableAuthoring : MonoBehaviour
    {
        [Header("Trigger Settings")]
        public float TriggerRadius = 5f;
        public float TriggerThreshold = 50f;
        public float TriggerDelay = 0.1f;
        public int MaxChainDepth = 10;

    }

    public class ChainTriggerableBaker : Baker<ChainTriggerableAuthoring>
    {
        public override void Bake(ChainTriggerableAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ChainTriggerable
            {
                TriggerRadius = authoring.TriggerRadius,
                TriggerThreshold = authoring.TriggerThreshold,
                TriggerDelay = authoring.TriggerDelay,
                MaxChainDepth = authoring.MaxChainDepth,
                IsTriggered = false,
                TriggerTimer = 0f,
                ChainDepth = 0
            });
        }
    }
    
    public class EnvironmentalHazardAuthoring : MonoBehaviour
    {
        public EnvironmentalHazardType Type = EnvironmentalHazardType.GasPocket;
        public float Radius = 5f;
        public float Intensity = 1f;
    }

    public class EnvironmentalHazardBaker : Baker<EnvironmentalHazardAuthoring>
    {
        public override void Bake(EnvironmentalHazardAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new EnvironmentalHazard
            {
                Type = authoring.Type,
                Position = authoring.transform.position,
                Radius = authoring.Radius,
                Intensity = authoring.Intensity
            });
            
            // Usually environmental hazards are also chain triggerable
            if (GetComponent<ChainTriggerableAuthoring>() == null)
            {
                AddComponent(entity, ChainTriggerable.GasPocket); // Default fallback
            }
        }
    }
}
