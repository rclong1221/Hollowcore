using Unity.Entities;

namespace DIG.ProceduralMotion
{
    /// <summary>
    /// EPIC 15.25 Phase 1: Holds the baked BlobAsset reference for Burst-safe access
    /// to all procedural motion profile data (forces, state overrides, paradigm weights).
    /// NOT ghost-replicated — client-only.
    /// </summary>
    public struct ProceduralMotionConfig : IComponentData
    {
        public BlobAssetReference<ProceduralMotionBlob> ProfileBlob;
    }
}
