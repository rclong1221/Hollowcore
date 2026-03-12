using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Emitted when a player starts a dodge roll. Consumer systems remove this component after processing.
/// </summary>
public struct RollEvent : IComponentData
{
    public int MaterialId;
    public float3 Position;
    public float Intensity; // 0-1
}
