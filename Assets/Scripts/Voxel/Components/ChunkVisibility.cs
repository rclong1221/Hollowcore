using Unity.Entities;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// OPTIMIZATION 10.3.17: Skip Simulation for Culled Chunks.
    /// Tracks if a chunk is currently visible to the camera.
    /// Updated by ChunkVisibilitySystem.
    /// Read by FluidSimulationSystem (and others) to throttle logic.
    /// </summary>
    public struct ChunkVisibility : IComponentData
    {
        public bool IsVisible;
    }
}
