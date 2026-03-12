using UnityEngine;
using Unity.Entities;
using DIG.Survival.EVA;

namespace DIG.Survival.Authoring
{
    /// <summary>
    /// Authoring component for metal surfaces that magnetic boots can attach to.
    /// Add to ship hulls, walkways, and other metallic surfaces in the scene.
    /// </summary>
    public class MetalSurfaceAuthoring : MonoBehaviour
    {
    }

    /// <summary>
    /// Baker for MetalSurfaceAuthoring.
    /// </summary>
    public class MetalSurfaceBaker : Baker<MetalSurfaceAuthoring>
    {
        public override void Bake(MetalSurfaceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent<MetalSurface>(entity);
        }
    }
}
