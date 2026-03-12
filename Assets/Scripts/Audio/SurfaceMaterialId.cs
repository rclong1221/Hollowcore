using Unity.Entities;

/// <summary>
/// Small runtime component storing an integer id for fast SurfaceMaterial lookup.
/// Written by the SurfaceMaterialAuthoring Baker at conversion time.
/// </summary>
public struct SurfaceMaterialId : IComponentData
{
    public int Id;
}
