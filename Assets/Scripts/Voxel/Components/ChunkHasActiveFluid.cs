using Unity.Entities;

namespace DIG.Voxel.Components
{
    /// <summary>
    /// OPTIMIZATION 10.3.12: Active Fluid Tracking.
    /// Added to chunks that have moving/unsettled fluid.
    /// Removed when fluid settles for a duration, putting the chunk to sleep.
    /// </summary>
    public struct ChunkHasActiveFluid : IComponentData
    {
        public float TimeSinceLastChange;
    }
}
