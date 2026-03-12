using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Environment.Gravity
{
    /// <summary>
    /// If present and Active, overrides the default scalar gravity in PlayerMovementSystem.
    /// </summary>
    public struct GravityOverride : IComponentData
    {
        public bool IsActive;
        public float3 GravityVector;
        public float Priority; // To handle overlapping zones
    }
}
