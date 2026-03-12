using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Emitted when a player jumps. Consumer systems remove this component after processing.
/// </summary>
public struct JumpEvent : IComponentData
{
    public int MaterialId;
    public float3 Position;
    public float Intensity; // 0-1, can vary based on stamina or jump type
}
