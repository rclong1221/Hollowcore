using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Emitted when a player starts sliding. Consumer systems remove this component after processing.
/// </summary>
public struct SlideEvent : IComponentData
{
    public int MaterialId;
    public float3 Position;
    public float Intensity; // 0-1, based on speed
}
