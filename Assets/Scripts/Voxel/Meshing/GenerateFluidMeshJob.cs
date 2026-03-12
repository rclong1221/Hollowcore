using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Fluids;
using System.Runtime.CompilerServices;

namespace DIG.Voxel.Meshing
{
    /// <summary>
    /// Generates mesh for fluids using Marching Cubes.
    /// specialized to read from FluidCells instead of Densities.
    /// Handles chunk boundaries by assuming empty (closing the mesh), creating distinct liquid volumes per chunk.
    /// </summary>
    [BurstCompile]
    public struct GenerateFluidMeshJob : IJob
    {
        [ReadOnly] public NativeArray<FluidCell> FluidCells;
        [ReadOnly] public int3 ChunkSize; // Usually 32,32,32
        [ReadOnly] public byte IsoLevel;  // Threshold for surface (e.g. 10)
        
        public NativeList<float3> Vertices;
        public NativeList<float3> Normals;
        public NativeList<int> Indices;
        
        public void Execute()
        {
            // We iterate 0 to Size-1 on all axes to generate cubes
            // Since we don't have neighbor padding, we clamp/close at boundaries.
            
            // Standard size is 32. We iterate 0..31 cubes.
            // We sample at x, x+1.
            // If x+1 == 32, GetLevel returns 0 -> Wall.
            
            int sizeX = ChunkSize.x;
            int sizeY = ChunkSize.y;
            int sizeZ = ChunkSize.z;
            
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        ProcessCube(x, y, z);
                    }
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessCube(int x, int y, int z)
        {
             // 1. Get Corner Densities (Levels)
             //   3---2
             //  /|  /|
             // 7---6 |  Y
             // | 0-|-1  |
             // 4---5/   X-Z
             
             // Corners in local coords relative to cube origin (x,y,z)
             // 0: (x,y,z)
             // 1: (x+1,y,z)
             // etc.
             
            float d0 = GetLevel(x, y, z);
            float d1 = GetLevel(x + 1, y, z);
            float d2 = GetLevel(x + 1, y, z + 1);
            float d3 = GetLevel(x, y, z + 1);
            float d4 = GetLevel(x, y + 1, z);
            float d5 = GetLevel(x + 1, y + 1, z);
            float d6 = GetLevel(x + 1, y + 1, z + 1);
            float d7 = GetLevel(x, y + 1, z + 1);

            // Calculate Cube Index
            // Note: In MC, "Inside" (Solid) usually means Value < Iso.
            // But for Fluids, "Solid" (Liquid) means Level > Iso (High value).
            // So we flip the check: if (Density > Iso) -> 1.
            
            int cubeIndex = 0;
            if (d0 > IsoLevel) cubeIndex |= 1;
            if (d1 > IsoLevel) cubeIndex |= 2;
            if (d2 > IsoLevel) cubeIndex |= 4;
            if (d3 > IsoLevel) cubeIndex |= 8;
            if (d4 > IsoLevel) cubeIndex |= 16;
            if (d5 > IsoLevel) cubeIndex |= 32;
            if (d6 > IsoLevel) cubeIndex |= 64;
            if (d7 > IsoLevel) cubeIndex |= 128;

            if (MarchingCubesTables.EdgeTable[cubeIndex] == 0) return;
            
            // 2. Vertex Interpolation
            float3 p0 = new float3(x, y, z);
            float3 p1 = new float3(x + 1, y, z);
            float3 p2 = new float3(x + 1, y, z + 1);
            float3 p3 = new float3(x, y, z + 1);
            float3 p4 = new float3(x, y + 1, z);
            float3 p5 = new float3(x + 1, y + 1, z);
            float3 p6 = new float3(x + 1, y + 1, z + 1);
            float3 p7 = new float3(x, y + 1, z + 1);
            
            // Edges array
            NativeArray<float3> vertList = new NativeArray<float3>(12, Allocator.Temp);
            
            // Note about Interp: standard function assumes linear interp.
            // If d0=255, d1=0. Iso=127. Point is halfway.
            
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 1) != 0) vertList[0] = VertexInterp(p0, p1, d0, d1);
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 2) != 0) vertList[1] = VertexInterp(p1, p2, d1, d2);
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 4) != 0) vertList[2] = VertexInterp(p2, p3, d2, d3);
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 8) != 0) vertList[3] = VertexInterp(p3, p0, d3, d0);
            
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 16) != 0) vertList[4] = VertexInterp(p4, p5, d4, d5);
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 32) != 0) vertList[5] = VertexInterp(p5, p6, d5, d6);
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 64) != 0) vertList[6] = VertexInterp(p6, p7, d6, d7);
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 128) != 0) vertList[7] = VertexInterp(p7, p4, d7, d4);
            
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 256) != 0) vertList[8] = VertexInterp(p0, p4, d0, d4);
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 512) != 0) vertList[9] = VertexInterp(p1, p5, d1, d5);
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 1024) != 0) vertList[10] = VertexInterp(p2, p6, d2, d6);
            if ((MarchingCubesTables.EdgeTable[cubeIndex] & 2048) != 0) vertList[11] = VertexInterp(p3, p7, d3, d7);

            // 3. Create Triangles
            for (int i = 0; MarchingCubesTables.TriTable[cubeIndex][i] != -1; i += 3)
            {
                int startIndex = Vertices.Length;
                
                float3 v1 = vertList[MarchingCubesTables.TriTable[cubeIndex][i]];
                float3 v2 = vertList[MarchingCubesTables.TriTable[cubeIndex][i+1]];
                float3 v3 = vertList[MarchingCubesTables.TriTable[cubeIndex][i+2]];
                
                // Add vertices (Standard winding)
                // Note: Standard MC tables often need winding check. Original Job flips them. 
                // We'll mimic original job (v1, v3, v2) to be safe or just standard.
                // Standard: v1, v2, v3? 
                // Original: v1, v3, v2. Let's stick to that to match face culling.
                
                Vertices.Add(v1);
                Vertices.Add(v3);
                Vertices.Add(v2);
                
                // Flat Normal (simple)
                float3 normal = math.normalize(math.cross(v3 - v1, v2 - v1));
                Normals.Add(normal);
                Normals.Add(normal);
                Normals.Add(normal);
                
                Indices.Add(startIndex);
                Indices.Add(startIndex + 1);
                Indices.Add(startIndex + 2);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetLevel(int x, int y, int z)
        {
            // Boundary check - Treat out of bounds as Empty (Air) to close mesh
            if (x < 0 || x >= ChunkSize.x ||
                y < 0 || y >= ChunkSize.y ||
                z < 0 || z >= ChunkSize.z)
            {
                return 0f;
            }
            
            int index = x + y * ChunkSize.x + z * ChunkSize.x * ChunkSize.y;
            return (float)FluidCells[index].Level;
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
