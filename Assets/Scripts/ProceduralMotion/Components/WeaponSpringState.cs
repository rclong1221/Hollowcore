using Unity.Entities;
using Unity.Mathematics;

namespace DIG.ProceduralMotion
{
    /// <summary>
    /// EPIC 15.25 Phase 1: Client-only weapon spring state for 6DOF procedural motion.
    /// NOT ghost-replicated — purely cosmetic presentation layer.
    /// Solved by WeaponSpringSolverSystem using analytical second-order springs.
    /// </summary>
    public struct WeaponSpringState : IComponentData
    {
        // Current displacement
        public float3 PositionValue;
        public float3 PositionVelocity;
        public float3 RotationValue;    // Euler degrees
        public float3 RotationVelocity; // deg/s

        // Spring parameters (Hz + damping ratio)
        public float3 PositionFrequency;
        public float3 PositionDampingRatio;
        public float3 RotationFrequency;
        public float3 RotationDampingRatio;

        // Clamp bounds
        public float3 PositionMin;
        public float3 PositionMax;
        public float3 RotationMin;
        public float3 RotationMax;

        // Whether spring solver is frozen (vault/climb states)
        public bool IsFrozen;
    }
}
