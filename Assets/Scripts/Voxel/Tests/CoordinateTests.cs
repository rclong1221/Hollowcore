using NUnit.Framework;
using Unity.Mathematics;
using DIG.Voxel.Core;

namespace DIG.Voxel.Tests
{
    public class CoordinateTests
    {
        [Test]
        public void WorldToChunkPos_PositiveCoordinates_ReturnsCorrectChunk()
        {
            // Position 0,0,0 -> Chunk 0,0,0
            Assert.AreEqual(int3.zero, CoordinateUtils.WorldToChunkPos(float3.zero));
            
            // Position 31.9, 0, 0 -> Chunk 0,0,0
            Assert.AreEqual(int3.zero, CoordinateUtils.WorldToChunkPos(new float3(31.9f, 0, 0)));
            
            // Position 32, 0, 0 -> Chunk 1,0,0
            Assert.AreEqual(new int3(1, 0, 0), CoordinateUtils.WorldToChunkPos(new float3(32f, 0, 0)));
        }

        [Test]
        public void WorldToChunkPos_NegativeCoordinates_ReturnsCorrectChunk()
        {
            // Position -0.1 -> Chunk -1
            Assert.AreEqual(new int3(-1, 0, 0), CoordinateUtils.WorldToChunkPos(new float3(-0.1f, 0, 0)));
            
            // Position -32 -> Chunk -1 (Inclusive of lower bound? No, chunk -1 is -32 to 0)
            // Wait, logic: floor(-32 / 32) = -1. Correct.
            Assert.AreEqual(new int3(-1, 0, 0), CoordinateUtils.WorldToChunkPos(new float3(-32f, 0, 0)));
            
            // Position -32.1 -> Chunk -2
            // floor(-1.003) = -2. Correct.
            Assert.AreEqual(new int3(-2, 0, 0), CoordinateUtils.WorldToChunkPos(new float3(-32.1f, 0, 0)));
        }

        [Test]
        public void WorldToLocalVoxelPos_HandlesBoundaries()
        {
            // 0,0,0 -> 0,0,0
            Assert.AreEqual(int3.zero, CoordinateUtils.WorldToLocalVoxelPos(float3.zero));
            
            // 31.9 -> 31
            Assert.AreEqual(new int3(31, 0, 0), CoordinateUtils.WorldToLocalVoxelPos(new float3(31.9f, 0, 0)));
            
            // -0.1 -> 31 (In chunk -1)
            Assert.AreEqual(new int3(31, 0, 0), CoordinateUtils.WorldToLocalVoxelPos(new float3(-0.1f, 0, 0)));
             
            // -32 -> 0 (In chunk -1)
            Assert.AreEqual(int3.zero, CoordinateUtils.WorldToLocalVoxelPos(new float3(-32f, 0, 0)));
        }

        [Test]
        public void VoxelPosToIndex_RoundTrip()
        {
            int3 original = new int3(15, 8, 22);
            int index = CoordinateUtils.VoxelPosToIndex(original);
            int3 result = CoordinateUtils.IndexToVoxelPos(index);
            
            Assert.AreEqual(original, result);
        }
    }
}
