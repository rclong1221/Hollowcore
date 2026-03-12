using Unity.Entities;
using Unity.NetCode;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 4: Written by SurfaceMovementModifierSystem onto entities
    /// with GroundSurfaceState. Read by PlayerMovementSystem for speed adjustment
    /// and by SurfaceSlipSystem for ice/slip physics.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SurfaceMovementModifier : IComponentData
    {
        /// <summary>Speed multiplier from ground surface. 1.0 = normal.</summary>
        [GhostField(Quantization = 100)] public float SpeedMultiplier;

        /// <summary>Friction multiplier from ground surface. 1.0 = normal.</summary>
        [GhostField(Quantization = 100)] public float FrictionMultiplier;

        /// <summary>Slip factor from ground surface. 0 = full control. 1 = no control.</summary>
        [GhostField(Quantization = 100)] public float SlipFactor;
    }
}
