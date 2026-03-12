using Unity.Entities;

namespace DIG.VFX
{
    /// <summary>
    /// EPIC 16.7 Phase 5: Tag component on entities whose material supports dissolve.
    /// CorpseSinkSystem skips entities with this tag, deferring to CorpseDissolveSystem.
    /// </summary>
    public struct DissolveCapable : IComponentData { }

    /// <summary>
    /// EPIC 16.7 Phase 5: Runtime state for dissolve animation.
    /// Managed by CorpseDissolveSystem during corpse fading.
    /// </summary>
    public struct DissolveState : IComponentData, IEnableableComponent
    {
        /// <summary>Current dissolve amount [0-1]. Driven by CorpseDissolveSystem.</summary>
        public float Amount;

        /// <summary>Dissolve rate per second. Computed from FadeOutDuration.</summary>
        public float Rate;
    }
}
