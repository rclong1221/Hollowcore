using Unity.Entities;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// Tracks the current LOD level of a chunk.
    /// </summary>
    public struct ChunkLODState : IComponentData
    {
        public int CurrentLOD;      // Current LOD level (0 = highest detail)
        public int TargetLOD;       // Target LOD level (pending transition)
        public float DistanceToCamera;
        public bool NeedsLODUpdate; // Flag to trigger mesh regeneration at new LOD
    }

    /// <summary>
    /// Tag component for chunks that need LOD mesh regeneration.
    /// </summary>
    public struct ChunkNeedsLODMesh : IComponentData, IEnableableComponent { }
}
