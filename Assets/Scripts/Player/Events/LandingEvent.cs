using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Emitted when a landing occurs with intensity in [0,1].
/// </summary>
public struct LandingEvent : IComponentData
{
    public int MaterialId;
    public float3 Position;
    public float Intensity;
}
