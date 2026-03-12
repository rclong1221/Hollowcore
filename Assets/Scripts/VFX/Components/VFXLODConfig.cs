using Unity.Entities;

namespace DIG.VFX
{
    /// <summary>
    /// EPIC 16.7: VFX LOD distance thresholds singleton.
    /// Extends existing EffectLODTier with project-wide configurable distances.
    /// </summary>
    public struct VFXLODConfig : IComponentData
    {
        /// <summary>Distance below which Full LOD applies.</summary>
        public float FullDistance;

        /// <summary>Distance below which Reduced LOD applies.</summary>
        public float ReducedDistance;

        /// <summary>Distance below which Minimal LOD applies. Beyond = Culled.</summary>
        public float MinimalDistance;

        public static VFXLODConfig Default => new()
        {
            FullDistance = 15f,
            ReducedDistance = 40f,
            MinimalDistance = 80f
        };
    }
}
