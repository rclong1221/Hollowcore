using Unity.Entities;
using UnityEngine;
using Audio.Components;

namespace Audio.Authoring
{
    public class ImpactAudioAuthoring : MonoBehaviour
    {
        [Tooltip("Surface Material ID (Matches Registry, e.g. 1=Metal)")]
        public int MaterialId = 0;
        
        [Tooltip("Volume multiplier based on mass. 1.0 = Default.")]
        public float MassFactor = 1.0f;
        
        [Tooltip("Minimum velocity (m/s) to trigger impact sound.")]
        public float VelocityThreshold = 1.0f;

        public class Baker : Baker<ImpactAudioAuthoring>
        {
            public override void Bake(ImpactAudioAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new ImpactAudioData
                {
                    MaterialId = authoring.MaterialId,
                    MassFactor = authoring.MassFactor,
                    VelocityThreshold = authoring.VelocityThreshold
                });
            }
        }
    }
}
