using Unity.Entities;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Tag component marking an entity/collider as a metal surface.
    /// Used by magnetic boots to detect attachment surfaces.
    /// Add to ship hulls, walkways, and other metallic surfaces.
    /// </summary>
    public struct MetalSurface : IComponentData { }
}
