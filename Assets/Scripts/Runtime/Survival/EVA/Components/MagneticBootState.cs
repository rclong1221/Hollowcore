using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Magnetic boot state for EVA movement. Allows walking on metal surfaces
    /// in zero-g by creating artificial gravity toward the surface normal.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct MagneticBootState : IComponentData
    {
        /// <summary>
        /// True if magnetic boots are toggled on by player.
        /// Boots can be on but not attached if not near a metal surface.
        /// </summary>
        [GhostField]
        public bool IsEnabled;

        /// <summary>
        /// True if currently attached to a metal surface.
        /// Only true when IsEnabled AND within DetectRange of metal surface.
        /// </summary>
        [GhostField]
        public bool IsAttached;

        /// <summary>
        /// Surface normal of the attached surface (world space).
        /// Used to override gravity direction (-AttachedNormal = gravity direction).
        /// Allows walking on walls/ceilings.
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float3 AttachedNormal;

        /// <summary>
        /// Downward force applied when attached (default: 20 m/s²).
        /// Keeps player firmly planted on surface.
        /// </summary>
        public float AttachForce;

        /// <summary>
        /// Raycast distance to detect metal surfaces (default: 2m).
        /// Larger values allow attachment from further away.
        /// </summary>
        public float DetectRange;

        /// <summary>
        /// Velocity threshold required to break attachment (default: 5 m/s).
        /// Prevents accidental detachment from small movements.
        /// Jetpack thrust or jumps can exceed this to detach.
        /// </summary>
        public float DetachVelocityThreshold;

        /// <summary>
        /// Gravity direction when attached (-AttachedNormal).
        /// </summary>
        public readonly float3 GravityDirection => -AttachedNormal;

        /// <summary>
        /// True if boots are on and attached to a surface.
        /// </summary>
        public readonly bool IsActivelyAttached => IsEnabled && IsAttached;

        public static MagneticBootState Default => new MagneticBootState
        {
            IsEnabled = false,
            IsAttached = false,
            AttachedNormal = new float3(0, 1, 0), // Default to floor normal
            AttachForce = 20f,
            DetectRange = 2f,
            DetachVelocityThreshold = 5f
        };
    }
}
