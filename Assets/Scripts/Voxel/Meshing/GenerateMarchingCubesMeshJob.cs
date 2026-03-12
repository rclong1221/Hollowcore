using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel.Core;
using System.Runtime.CompilerServices;

namespace DIG.Voxel.Meshing
{
    /// <summary>
    /// OPTIMIZATION 10.9.10: Burst 2.0 Intrinsics
    /// - OptimizeFor.Performance for aggressive optimization
    /// - FloatMode.Fast for relaxed floating point (faster but less precise)
    /// - DisableSafetyChecks for release builds
    /// </summary>
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, DisableSafetyChecks = true)]
    public struct GenerateMarchingCubesMeshJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Densities;
        [ReadOnly] public NativeArray<byte> Materials; // Material IDs for each voxel
        [ReadOnly] public int3 ChunkSize;
        [ReadOnly] public float IsoLevel;
        [ReadOnly] public float VertexScale; // Scale factor for vertices (1.0 = no scale, 32/31 = extend to boundary)
        [ReadOnly] public int VoxelStep; // LOD step: 1=full, 2=half, 4=quarter resolution (Phase 2 optimization)
        
        // Output - Using NativeList for dynamic appending
        public NativeList<float3> Vertices;
        public NativeList<float3> Normals;
        public NativeList<Color32> Colors; // Material ID encoded in vertex color for shader
        public NativeList<int> Indices;
        public NativeList<int3> Triangles; // For DOTS Physics MeshCollider (pre-converted)
        
        public void Execute()
        {
            // LOD-aware iteration: step controls resolution
            // VoxelStep=1: full res (32x32x32 cubes)
            // VoxelStep=2: half res (16x16x16 cubes, 8x fewer triangles)
            // VoxelStep=4: quarter res (8x8x8 cubes, 64x fewer triangles)
            
            int step = math.max(1, VoxelStep);
            int sizeX = ChunkSize.x;
            int sizeY = ChunkSize.y;
            int sizeZ = ChunkSize.z;
            
            int rowStride = 34;
            int sliceStride = 34 * 34;

            // For LOD, we sample every 'step' voxels
            // Start at -step to sample neighbor boundary (scaled)
            for (int x = -step; x < sizeX; x += step)
            {
                for (int y = -step; y < sizeY; y += step)
                {
                    for (int z = -step; z < sizeZ; z += step)
                    {
                        // Map to 34^3 buffer (offset by 1, clamp to valid range)
                        int mapX = math.clamp(x + 1, 0, 33);
                        int mapY = math.clamp(y + 1, 0, 33);
                        int mapZ = math.clamp(z + 1, 0, 33);

                        ProcessCubeLOD(x, y, z, mapX, mapY, mapZ, rowStride, sliceStride, step);
                    }
                }
            }
        }
        
        /// <summary>
        /// Fixed-size vertex list for edge interpolation results.
        /// Eliminates per-cube NativeArray allocation (~32K allocs per chunk).
        /// Burst compiles this to register/stack storage with zero GC.
        /// </summary>
        private struct VertexList12
        {
            public float3 v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float3 Get(int index)
            {
                // Burst optimizes this switch into direct register access
                switch (index)
                {
                    case 0: return v0;
                    case 1: return v1;
                    case 2: return v2;
                    case 3: return v3;
                    case 4: return v4;
                    case 5: return v5;
                    case 6: return v6;
                    case 7: return v7;
                    case 8: return v8;
                    case 9: return v9;
                    case 10: return v10;
                    case 11: return v11;
                    default: return float3.zero;
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessCubeLOD(int x, int y, int z, int mx, int my, int mz, int rowStride, int sliceStride, int step)
        {
             // LOD-aware cube processing
             // For step > 1, we sample corners at step intervals and scale output vertices
             
             // 1. Calculate Cube Index using stepped corners
             //   3---2
             //  /|  /|
             // 7---6 |  Y
             // | 0-|-1  |
             // 4---5/   X-Z
            
            // Clamp stepped indices to valid buffer range
            int mx1 = math.min(mx + step, 33);
            int my1 = math.min(my + step, 33);
            int mz1 = math.min(mz + step, 33);
            
            int index0 = (mx) + (my) * rowStride + (mz) * sliceStride;
            int index1 = (mx1) + (my) * rowStride + (mz) * sliceStride;
            int index2 = (mx1) + (my) * rowStride + (mz1) * sliceStride;
            int index3 = (mx) + (my) * rowStride + (mz1) * sliceStride;
            
            int index4 = (mx) + (my1) * rowStride + (mz) * sliceStride;
            int index5 = (mx1) + (my1) * rowStride + (mz) * sliceStride;
            int index6 = (mx1) + (my1) * rowStride + (mz1) * sliceStride;
            int index7 = (mx) + (my1) * rowStride + (mz1) * sliceStride;

            // OPTIMIZATION 10.9.10: Vectorized cube index calculation
            // Burst auto-vectorizes this with SIMD when possible
            int cubeIndex = 0;
            float iso = IsoLevel;
            cubeIndex |= math.select(0, 1, Densities[index0] < iso);
            cubeIndex |= math.select(0, 2, Densities[index1] < iso);
            cubeIndex |= math.select(0, 4, Densities[index2] < iso);
            cubeIndex |= math.select(0, 8, Densities[index3] < iso);
            cubeIndex |= math.select(0, 16, Densities[index4] < iso);
            cubeIndex |= math.select(0, 32, Densities[index5] < iso);
            cubeIndex |= math.select(0, 64, Densities[index6] < iso);
            cubeIndex |= math.select(0, 128, Densities[index7] < iso);

            // Fully in or out
            if (MarchingCubesTables.EdgeTable[cubeIndex] == 0) return;
            
            // 2. Vertex Interpolation with LOD scaling
            // Corners in Local Space - scaled by step for LOD
            float3 p0 = new float3(x, y, z);
            float3 p1 = new float3(x + step, y, z);
            float3 p2 = new float3(x + step, y, z + step);
            float3 p3 = new float3(x, y, z + step);
            float3 p4 = new float3(x, y + step, z);
            float3 p5 = new float3(x + step, y + step, z);
            float3 p6 = new float3(x + step, y + step, z + step);
            float3 p7 = new float3(x, y + step, z + step);
            
            // Densities
            float d0 = Densities[index0];
            float d1 = Densities[index1];
            float d2 = Densities[index2];
            float d3 = Densities[index3];
            float d4 = Densities[index4];
            float d5 = Densities[index5];
            float d6 = Densities[index6];
            float d7 = Densities[index7];

            // OPTIMIZATION 10.9.1: Use stack-allocated struct instead of NativeArray
            // Eliminates ~32,768 temp allocations per chunk
            var vertList = new VertexList12();
            int edgeFlags = MarchingCubesTables.EdgeTable[cubeIndex];
            
            if ((edgeFlags & 1) != 0) vertList.v0 = VertexInterp(p0, p1, d0, d1);
            if ((edgeFlags & 2) != 0) vertList.v1 = VertexInterp(p1, p2, d1, d2);
            if ((edgeFlags & 4) != 0) vertList.v2 = VertexInterp(p2, p3, d2, d3);
            if ((edgeFlags & 8) != 0) vertList.v3 = VertexInterp(p3, p0, d3, d0);
            
            if ((edgeFlags & 16) != 0) vertList.v4 = VertexInterp(p4, p5, d4, d5);
            if ((edgeFlags & 32) != 0) vertList.v5 = VertexInterp(p5, p6, d5, d6);
            if ((edgeFlags & 64) != 0) vertList.v6 = VertexInterp(p6, p7, d6, d7);
            if ((edgeFlags & 128) != 0) vertList.v7 = VertexInterp(p7, p4, d7, d4);
            
            if ((edgeFlags & 256) != 0) vertList.v8 = VertexInterp(p0, p4, d0, d4);
            if ((edgeFlags & 512) != 0) vertList.v9 = VertexInterp(p1, p5, d1, d5);
            if ((edgeFlags & 1024) != 0) vertList.v10 = VertexInterp(p2, p6, d2, d6);
            if ((edgeFlags & 2048) != 0) vertList.v11 = VertexInterp(p3, p7, d3, d7);

            // 3. Create Triangles
            for (int i = 0; MarchingCubesTables.TriTable[cubeIndex][i] != -1; i += 3)
            {
                int startIndex = Vertices.Length;
                
                float3 v1 = vertList.Get(MarchingCubesTables.TriTable[cubeIndex][i]);
                float3 v2 = vertList.Get(MarchingCubesTables.TriTable[cubeIndex][i+1]);
                float3 v3 = vertList.Get(MarchingCubesTables.TriTable[cubeIndex][i+2]);
                
                // Apply vertex scale
                v1 *= VertexScale;
                v2 *= VertexScale;
                v3 *= VertexScale;
                
                // Add vertices (reversed winding for correct culling)
                Vertices.Add(v1);
                Vertices.Add(v3);
                Vertices.Add(v2);
                
                // Calculate Flat Normal
                float3 normal = math.normalize(math.cross(v3 - v1, v2 - v1));
                Normals.Add(normal);
                Normals.Add(normal);
                Normals.Add(normal);
                
                // Sample material at cube center
                byte materialId = Materials.IsCreated && Materials.Length > 0 
                    ? Materials[index0] 
                    : (byte)0;
                
                Color32 matColor = MaterialIdToColor(materialId);
                Colors.Add(matColor);
                Colors.Add(matColor);
                Colors.Add(matColor);
                
                Indices.Add(startIndex);
                Indices.Add(startIndex + 1);
                Indices.Add(startIndex + 2);
                
                // Pre-convert for DOTS Physics
                Triangles.Add(new int3(startIndex, startIndex + 1, startIndex + 2));
            }
        }
        
        // Legacy method for compatibility (step=1)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessCube(int x, int y, int z, int mx, int my, int mz, int rowStride, int sliceStride)
        {
            ProcessCubeLOD(x, y, z, mx, my, mz, rowStride, sliceStride, 1);
        }
        
        /// <summary>
        /// Convert material ID to vertex color for shader Texture2DArray indexing.
        /// R channel = material ID (0-255), used by shader to sample correct texture slice.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Color32 MaterialIdToColor(byte materialId)
        {
            // Store material ID directly in R channel
            // The shader will multiply by 255 to get integer index for Texture2DArray
            // G,B store same value for debugging/visualization
            return new Color32(materialId, materialId, materialId, 255);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 VertexInterp(float3 p1, float3 p2, float val1, float val2)
        {
            if (math.abs(IsoLevel - val1) < 0.00001f) return p1;
            if (math.abs(IsoLevel - val2) < 0.00001f) return p2;
            if (math.abs(val1 - val2) < 0.00001f) return p1;
            
            float mu = (IsoLevel - val1) / (val2 - val1);
            return p1 + mu * (p2 - p1);
        }
    }
}
