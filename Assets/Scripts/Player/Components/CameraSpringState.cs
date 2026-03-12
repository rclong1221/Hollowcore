using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// State for procedural camera springs (shakes, recoil, bob)
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CameraSpringState : IComponentData
    {
        // Position Spring
        public float3 PositionValue;
        public float3 PositionVelocity;
        public float3 PositionStiffness;
        public float3 PositionDamping; // 0-1 range
        
        // Rotation Spring
        public float3 RotationValue; // Euler angles
        public float3 RotationVelocity;
        public float3 RotationStiffness;
        public float3 RotationDamping; // 0-1 range

        // Settings
        public float MaxVelocity;
        public float3 MinValue;
        public float3 MaxValue;

        // EPIC 15.25: Analytical solver parameters (Hz + damping ratio).
        // When PositionFrequency > 0, the analytical solver is used instead of Opsive.
        // Default zero = old Opsive solver path, zero regression.
        // NOT [GhostField] — purely local presentation quality.
        public float3 PositionFrequency;
        public float3 PositionDampingRatio;
        public float3 RotationFrequency;
        public float3 RotationDampingRatio;

        // Helper to check if resting
        public bool IsResting => math.lengthsq(PositionVelocity) < 0.0001f && math.lengthsq(RotationVelocity) < 0.0001f;
    }
}
