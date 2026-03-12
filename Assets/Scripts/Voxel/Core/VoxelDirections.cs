using Unity.Mathematics;

namespace DIG.Voxel.Core
{
    public static class VoxelDirections
    {
        public static readonly int3[] IntDirections = new int3[]
        {
            new int3(1, 0, 0),  // Right
            new int3(-1, 0, 0), // Left
            new int3(0, 1, 0),  // Up
            new int3(0, -1, 0), // Down
            new int3(0, 0, 1),  // Forward
            new int3(0, 0, -1)  // Back
        };
    }
}
