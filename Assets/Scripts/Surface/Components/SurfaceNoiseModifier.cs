using Unity.Entities;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 3: Written by SurfaceStealthModifierSystem on NPCs.
    /// Read by HearingDetectionSystem to adjust NPC detectability by surface.
    /// NOT ghost-replicated — server-only, used only by server-side hearing detection.
    /// </summary>
    public struct SurfaceNoiseModifier : IComponentData
    {
        /// <summary>1.0 = default, >1 = louder surface, <1 = quieter.</summary>
        public float Multiplier;
    }
}
