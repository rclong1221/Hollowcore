using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Emitted when a footstep should be played. Consumer systems remove this component after processing.
/// </summary>
public struct FootstepEvent : IComponentData
{
    public int MaterialId;
    public float3 Position;
    public float Intensity;
    public int FootIndex; // 0 = left, 1 = right
    public int Stance; // 0 = stand, 1 = crouch, 2 = prone, 3 = run
}
