using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel.Core;
using System.Runtime.CompilerServices;

namespace DIG.Voxel.Meshing
{
    /// <summary>
    /// OPTIMIZATION 10.9.2: Parallel Marching Cubes Mesh Generation
    /// Uses IJobParallelFor for multi-threaded cube processing.
    /// Outputs to NativeStream for thread-safe parallel writes.
    /// Expected speedup: 4-8x on multi-core CPUs.
    /// 
    /// OPTIMIZATION 10.9.20: Native Memory Aliasing
    /// - [NoAlias] on distinct buffers for Burst optimization
    /// - [NativeDisableContainerSafetyRestriction] on read-only buffers
    /// </summary>
    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct GenerateMarchingCubesParallelJob : IJobParallelFor
    {
        [ReadOnly, NoAlias, NativeDisableContainerSafetyRestriction] 
        public NativeArray<byte> Densities;
        
        [ReadOnly, NoAlias, NativeDisableContainerSafetyRestriction] 
        public NativeArray<byte> Materials;
        
        [ReadOnly] public int3 ChunkSize;
        [ReadOnly] public float IsoLevel;
        [ReadOnly] public float VertexScale;
        [ReadOnly] public int VoxelStep;
        
        // Pre-computed dimensions for index mapping
        [ReadOnly] public int CubesPerAxis;  // (32 + step) / step
        [ReadOnly] public int TotalCubes;    // CubesPerAxis^3
        
        // Thread-safe output using NativeStream
        [WriteOnly] public NativeStream.Writer OutputWriter;
        
        // Constants
        private const int ROW_STRIDE = 34;
        private const int SLICE_STRIDE = 34 * 34;
        
        public void Execute(int cubeIndex)
        {
            // CRITICAL: Must call BeginForEachIndex before any writes to NativeStream
            OutputWriter.BeginForEachIndex(cubeIndex);
            
            int step = math.max(1, VoxelStep);
            
            // Convert flat index to 3D cube coordinates
            int cubesPerRow = CubesPerAxis;
            int cubesPerSlice = cubesPerRow * cubesPerRow;
            
            int cz = cubeIndex / cubesPerSlice;
            int rem = cubeIndex % cubesPerSlice;
            int cy = rem / cubesPerRow;
            int cx = rem % cubesPerRow;
            
            // Convert to voxel coordinates (starting from -step for boundary)
            int x = cx * step - step;
            int y = cy * step - step;
            int z = cz * step - step;
            
            // Map to 34^3 buffer (offset by 1, clamp to valid range)
            int mx = math.clamp(x + 1, 0, 33);
            int my = math.clamp(y + 1, 0, 33);
            int mz = math.clamp(z + 1, 0, 33);
            
            ProcessCube(x, y, z, mx, my, mz, step);
            
            // CRITICAL: Must call EndForEachIndex after all writes
            OutputWriter.EndForEachIndex();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessCube(int x, int y, int z, int mx, int my, int mz, int step)
        {
            // Clamp stepped indices to valid buffer range
            int mx1 = math.min(mx + step, 33);
            int my1 = math.min(my + step, 33);
            int mz1 = math.min(mz + step, 33);
            
            int index0 = mx + my * ROW_STRIDE + mz * SLICE_STRIDE;
            int index1 = mx1 + my * ROW_STRIDE + mz * SLICE_STRIDE;
            int index2 = mx1 + my * ROW_STRIDE + mz1 * SLICE_STRIDE;
            int index3 = mx + my * ROW_STRIDE + mz1 * SLICE_STRIDE;
            
            int index4 = mx + my1 * ROW_STRIDE + mz * SLICE_STRIDE;
            int index5 = mx1 + my1 * ROW_STRIDE + mz * SLICE_STRIDE;
            int index6 = mx1 + my1 * ROW_STRIDE + mz1 * SLICE_STRIDE;
            int index7 = mx + my1 * ROW_STRIDE + mz1 * SLICE_STRIDE;

            // Sample scalar field at 8 corners
            int cubeConfig = 0;
            if (Densities[index0] < IsoLevel) cubeConfig |= 1;
            if (Densities[index1] < IsoLevel) cubeConfig |= 2;
            if (Densities[index2] < IsoLevel) cubeConfig |= 4;
            if (Densities[index3] < IsoLevel) cubeConfig |= 8;
            if (Densities[index4] < IsoLevel) cubeConfig |= 16;
            if (Densities[index5] < IsoLevel) cubeConfig |= 32;
            if (Densities[index6] < IsoLevel) cubeConfig |= 64;
            if (Densities[index7] < IsoLevel) cubeConfig |= 128;

            // Fully in or out - no triangles (still need Begin/End, handled by caller)
            int edgeFlags = MarchingCubesTables.EdgeTable[cubeConfig];
            if (edgeFlags == 0) return;
            
            // Corner positions
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

            // Compute edge vertices (reusing 10.9.1's VertexList12 pattern)
            var vertList = new VertexList12();
            
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

            // Get material for this cube
            byte materialId = Materials.IsCreated && Materials.Length > 0 
                ? Materials[index0] 
                : (byte)0;

            // Generate triangles and write to stream
            for (int i = 0; MarchingCubesTables.TriTable[cubeConfig][i] != -1; i += 3)
            {
                float3 v1 = vertList.Get(MarchingCubesTables.TriTable[cubeConfig][i]);
                float3 v2 = vertList.Get(MarchingCubesTables.TriTable[cubeConfig][i+1]);
                float3 v3 = vertList.Get(MarchingCubesTables.TriTable[cubeConfig][i+2]);
                
                // Apply vertex scale
                v1 *= VertexScale;
                v2 *= VertexScale;
                v3 *= VertexScale;
                
                // Calculate flat normal
                float3 normal = math.normalize(math.cross(v3 - v1, v2 - v1));
                
                // Write triangle data to stream (reversed winding for correct culling)
                OutputWriter.Write(new TriangleData
                {
                    V0 = v1,
                    V1 = v3,  // Swapped for winding
                    V2 = v2,  // Swapped for winding
                    Normal = normal,
                    MaterialId = materialId
                });
            }
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
        
        /// <summary>
        /// Fixed-size vertex list for edge interpolation (from 10.9.1).
        /// </summary>
        private struct VertexList12
        {
            public float3 v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float3 Get(int index)
            {
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
    }
    
    /// <summary>
    /// Triangle data written to NativeStream by parallel job.
    /// </summary>
    public struct TriangleData
    {
        public float3 V0;
        public float3 V1;
        public float3 V2;
        public float3 Normal;
        public byte MaterialId;
    }
    
    /// <summary>
    /// Merge job: Reads triangles from NativeStream and writes to output arrays.
    /// Runs after parallel job completes.
    ///
    /// EPIC 14.19: Added vertex welding to enable proper smooth normal averaging.
    /// Vertices at the same position are merged, allowing the smooth normals job
    /// to produce consistent normals across shared edges.
    /// </summary>
    [BurstCompile]
    public struct MergeMarchingCubesOutputJob : IJob
    {
        [ReadOnly] public NativeStream.Reader InputReader;
        public NativeList<float3> Vertices;
        public NativeList<float3> Normals;
        public NativeList<Color32> Colors;
        public NativeList<int> Indices;
        public NativeList<int3> Triangles;
        public int ForEachCount;

        // EPIC 14.19: Vertex welding threshold - vertices closer than this are merged
        private const float WELD_THRESHOLD = 0.001f;
        private const float WELD_THRESHOLD_SQ = WELD_THRESHOLD * WELD_THRESHOLD;

        // EPIC 14.19: Spatial hash cell size for fast vertex lookup
        private const float CELL_SIZE = 0.5f;
        private const float INV_CELL_SIZE = 1f / CELL_SIZE;

        public void Execute()
        {
            // EPIC 14.19: Use spatial hashing for O(1) vertex lookup instead of O(n) search
            // Key: spatial hash, Value: vertex index
            var vertexMap = new NativeParallelHashMap<int, int>(4096, Allocator.Temp);

            // Temporary list to accumulate normals for averaging
            var normalAccumulator = new NativeList<float3>(4096, Allocator.Temp);
            var normalCounts = new NativeList<int>(4096, Allocator.Temp);

            for (int lane = 0; lane < ForEachCount; lane++)
            {
                int itemsInLane = InputReader.BeginForEachIndex(lane);

                for (int i = 0; i < itemsInLane; i++)
                {
                    var tri = InputReader.Read<TriangleData>();

                    // Process each vertex of the triangle
                    int idx0 = GetOrCreateVertex(tri.V0, tri.Normal, tri.MaterialId, ref vertexMap, ref normalAccumulator, ref normalCounts);
                    int idx1 = GetOrCreateVertex(tri.V1, tri.Normal, tri.MaterialId, ref vertexMap, ref normalAccumulator, ref normalCounts);
                    int idx2 = GetOrCreateVertex(tri.V2, tri.Normal, tri.MaterialId, ref vertexMap, ref normalAccumulator, ref normalCounts);

                    // Add indices
                    Indices.Add(idx0);
                    Indices.Add(idx1);
                    Indices.Add(idx2);

                    // Add triangle for physics
                    Triangles.Add(new int3(idx0, idx1, idx2));
                }

                InputReader.EndForEachIndex();
            }

            // EPIC 14.19: Finalize normals by averaging accumulated values
            // Note: The smooth normals job will override these with gradient-based normals,
            // but this averaging helps when smooth normals are disabled
            for (int i = 0; i < Normals.Length; i++)
            {
                if (normalCounts[i] > 1)
                {
                    // Average the accumulated normals
                    Normals[i] = math.normalizesafe(normalAccumulator[i]);
                }
            }

            vertexMap.Dispose();
            normalAccumulator.Dispose();
            normalCounts.Dispose();
        }

        /// <summary>
        /// EPIC 14.19: Get existing vertex index or create new vertex.
        /// Uses spatial hashing for fast lookup of nearby vertices.
        /// </summary>
        private int GetOrCreateVertex(
            float3 position,
            float3 normal,
            byte materialId,
            ref NativeParallelHashMap<int, int> vertexMap,
            ref NativeList<float3> normalAccumulator,
            ref NativeList<int> normalCounts)
        {
            // Compute spatial hash
            int hash = ComputeSpatialHash(position);

            // Check if vertex already exists at this hash location
            if (vertexMap.TryGetValue(hash, out int existingIndex))
            {
                // Verify position match (hash collision check)
                float3 existingPos = Vertices[existingIndex];
                if (math.distancesq(existingPos, position) < WELD_THRESHOLD_SQ)
                {
                    // Accumulate normal for averaging
                    normalAccumulator[existingIndex] += normal;
                    normalCounts[existingIndex]++;
                    return existingIndex;
                }
                // Hash collision with different position - fall through to create new vertex
            }

            // Create new vertex
            int newIndex = Vertices.Length;
            Vertices.Add(position);
            Normals.Add(normal);
            Colors.Add(new Color32(materialId, materialId, materialId, 255));
            normalAccumulator.Add(normal);
            normalCounts.Add(1);

            // Store in hash map (may overwrite on collision, but that's acceptable)
            vertexMap[hash] = newIndex;

            return newIndex;
        }

        /// <summary>
        /// EPIC 14.19: Compute spatial hash for vertex position.
        /// Maps 3D position to integer hash for fast lookup.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ComputeSpatialHash(float3 pos)
        {
            // Quantize position to grid cells
            int x = (int)math.floor(pos.x * INV_CELL_SIZE);
            int y = (int)math.floor(pos.y * INV_CELL_SIZE);
            int z = (int)math.floor(pos.z * INV_CELL_SIZE);

            // Use large primes for hash mixing to reduce collisions
            // These primes are chosen to spread values well across typical chunk coordinates
            unchecked
            {
                return x * 73856093 ^ y * 19349663 ^ z * 83492791;
            }
        }
    }
    
    /// <summary>
    /// Helper to calculate parallel job parameters.
    /// </summary>
    public static class MarchingCubesParallelHelper
    {
        /// <summary>
        /// Calculate the number of cubes to process for a given LOD step.
        /// </summary>
        public static int CalculateCubeCount(int chunkSize, int voxelStep)
        {
            int step = math.max(1, voxelStep);
            // From -step to chunkSize-1, stepping by step
            // Count = ceil((chunkSize + step) / step)
            int cubesPerAxis = (chunkSize + step) / step;
            return cubesPerAxis * cubesPerAxis * cubesPerAxis;
        }
        
        /// <summary>
        /// Calculate cubes per axis for index mapping.
        /// </summary>
        public static int CalculateCubesPerAxis(int chunkSize, int voxelStep)
        {
            int step = math.max(1, voxelStep);
            return (chunkSize + step) / step;
        }
    }
}
