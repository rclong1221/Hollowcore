using Unity.Entities;
using UnityEngine;

namespace DIG.VFX.Authoring
{
    /// <summary>
    /// EPIC 16.7: Authoring component for VFX LOD distance thresholds singleton.
    /// Place on a GameObject in a subscene to override default distances.
    /// </summary>
    public class VFXLODConfigAuthoring : MonoBehaviour
    {
        [Header("LOD Distance Thresholds (meters)")]
        [Tooltip("Full LOD: all particles, sub-emitters, trails")]
        public float FullDistance = 15f;

        [Tooltip("Reduced LOD: 50% emission, no sub-emitters")]
        public float ReducedDistance = 40f;

        [Tooltip("Minimal LOD: billboard only. Beyond = culled")]
        public float MinimalDistance = 80f;

        private class Baker : Baker<VFXLODConfigAuthoring>
        {
            public override void Bake(VFXLODConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new VFXLODConfig
                {
                    FullDistance = authoring.FullDistance,
                    ReducedDistance = authoring.ReducedDistance,
                    MinimalDistance = authoring.MinimalDistance
                });
            }
        }
    }
}
