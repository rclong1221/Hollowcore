using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Entities;
using DIG.Voxel.Core;

namespace DIG.Voxel.Jobs
{
    /// <summary>
    /// Copies voxel data from source and neighbors into a padded array for Marching Cubes.
    /// This removes the ~4ms overhead from the main thread.
    ///
    /// EPIC 14.19: Improved edge handling for missing neighbors
    /// - Uses gradient extrapolation instead of simple clamping
    /// - Prevents artificial density walls at chunk boundaries
    /// - Produces smoother normals at world edges
    /// </summary>
    [BurstCompile]
    public struct CopyPaddedDataJob : IJob
    {
        [WriteOnly] public NativeArray<byte> PaddedDensities;
        [WriteOnly] public NativeArray<byte> PaddedMaterials;

        [ReadOnly] public BlobAssetReference<VoxelBlob> Source;

        // Neighbor Flags
        [ReadOnly] public bool HasNegX;
        [ReadOnly] public bool HasPosX;
        [ReadOnly] public bool HasNegY;
        [ReadOnly] public bool HasPosY;
        [ReadOnly] public bool HasNegZ;
        [ReadOnly] public bool HasPosZ;

        // Neighbor Data (Only valid if corresponding flag is true)
        [ReadOnly] public BlobAssetReference<VoxelBlob> NegX;
        [ReadOnly] public BlobAssetReference<VoxelBlob> PosX;
        [ReadOnly] public BlobAssetReference<VoxelBlob> NegY;
        [ReadOnly] public BlobAssetReference<VoxelBlob> PosY;
        [ReadOnly] public BlobAssetReference<VoxelBlob> NegZ;
        [ReadOnly] public BlobAssetReference<VoxelBlob> PosZ;

        public void Execute()
        {
            // Padded size is 34 (1 border on each side of 32)
            const int SIZE = 32;
            const int PADDED_SIZE = 34;

            ref var blob = ref Source.Value;

            for (int pz = 0; pz < PADDED_SIZE; pz++)
            {
                for (int py = 0; py < PADDED_SIZE; py++)
                {
                    for (int px = 0; px < PADDED_SIZE; px++)
                    {
                        // Map padded coords to chunk coords (offset by 1)
                        int cx = px - 1;
                        int cy = py - 1;
                        int cz = pz - 1;

                        byte density = GetDensity(cx, cy, cz, ref blob, SIZE);
                        byte material = GetMaterial(cx, cy, cz, ref blob, SIZE);

                        int paddedIndex = px + py * PADDED_SIZE + pz * PADDED_SIZE * PADDED_SIZE;
                        PaddedDensities[paddedIndex] = density;
                        PaddedMaterials[paddedIndex] = material;
                    }
                }
            }
        }

        private byte GetDensity(int cx, int cy, int cz, ref VoxelBlob blob, int SIZE)
        {
            // Inside chunk bounds - use local data
            if (cx >= 0 && cx < SIZE && cy >= 0 && cy < SIZE && cz >= 0 && cz < SIZE)
            {
                return blob.Densities[cx + cy * SIZE + cz * SIZE * SIZE];
            }

            // +X border
            if (cx >= SIZE && cy >= 0 && cy < SIZE && cz >= 0 && cz < SIZE)
            {
                if (HasPosX)
                {
                    return PosX.Value.Densities[(cx - SIZE) + cy * SIZE + cz * SIZE * SIZE];
                }
                // EPIC 14.19: Extrapolate instead of clamp
                return ExtrapolateDensity(ref blob, SIZE - 1, cy, cz, 1, 0, 0, SIZE);
            }

            // -X border
            if (cx < 0 && cy >= 0 && cy < SIZE && cz >= 0 && cz < SIZE)
            {
                if (HasNegX)
                {
                    return NegX.Value.Densities[(cx + SIZE) + cy * SIZE + cz * SIZE * SIZE];
                }
                // EPIC 14.19: Extrapolate instead of clamp
                return ExtrapolateDensity(ref blob, 0, cy, cz, -1, 0, 0, SIZE);
            }

            // +Z border
            if (cz >= SIZE && cx >= 0 && cx < SIZE && cy >= 0 && cy < SIZE)
            {
                if (HasPosZ)
                {
                    return PosZ.Value.Densities[cx + cy * SIZE + (cz - SIZE) * SIZE * SIZE];
                }
                // EPIC 14.19: Extrapolate instead of clamp
                return ExtrapolateDensity(ref blob, cx, cy, SIZE - 1, 0, 0, 1, SIZE);
            }

            // -Z border
            if (cz < 0 && cx >= 0 && cx < SIZE && cy >= 0 && cy < SIZE)
            {
                if (HasNegZ)
                {
                    return NegZ.Value.Densities[cx + cy * SIZE + (cz + SIZE) * SIZE * SIZE];
                }
                // EPIC 14.19: Extrapolate instead of clamp
                return ExtrapolateDensity(ref blob, cx, cy, 0, 0, 0, -1, SIZE);
            }

            // +Y border (above)
            if (cy >= SIZE)
            {
                int clampedX = math.clamp(cx, 0, SIZE - 1);
                int clampedZ = math.clamp(cz, 0, SIZE - 1);
                if (HasPosY)
                {
                    return PosY.Value.Densities[clampedX + (cy - SIZE) * SIZE + clampedZ * SIZE * SIZE];
                }
                // EPIC 14.19: For missing top neighbor, extrapolate toward air
                // This creates a smooth transition at the top of the world
                return ExtrapolateDensityTowardAir(ref blob, clampedX, SIZE - 1, clampedZ, 0, 1, 0, SIZE);
            }

            // -Y border (below)
            if (cy < 0)
            {
                int clampedX = math.clamp(cx, 0, SIZE - 1);
                int clampedZ = math.clamp(cz, 0, SIZE - 1);
                if (HasNegY)
                {
                    return NegY.Value.Densities[clampedX + (cy + SIZE) * SIZE + clampedZ * SIZE * SIZE];
                }
                // EPIC 14.19: For missing bottom neighbor, extrapolate toward solid
                // This creates a smooth transition at the bottom of the world
                return ExtrapolateDensityTowardSolid(ref blob, clampedX, 0, clampedZ, 0, -1, 0, SIZE);
            }

            // Corner case: extrapolate from nearest valid voxel
            int safeX = math.clamp(cx, 0, SIZE - 1);
            int safeY = math.clamp(cy, 0, SIZE - 1);
            int safeZ = math.clamp(cz, 0, SIZE - 1);

            // EPIC 14.19: For corners, use the edge value but slightly extrapolate
            byte edgeValue = blob.Densities[safeX + safeY * SIZE + safeZ * SIZE * SIZE];

            // Determine extrapolation direction
            int dirX = cx < 0 ? -1 : (cx >= SIZE ? 1 : 0);
            int dirY = cy < 0 ? -1 : (cy >= SIZE ? 1 : 0);
            int dirZ = cz < 0 ? -1 : (cz >= SIZE ? 1 : 0);

            // Sample one voxel back into the chunk to get gradient
            int interiorX = math.clamp(safeX - dirX, 0, SIZE - 1);
            int interiorY = math.clamp(safeY - dirY, 0, SIZE - 1);
            int interiorZ = math.clamp(safeZ - dirZ, 0, SIZE - 1);

            byte interiorValue = blob.Densities[interiorX + interiorY * SIZE + interiorZ * SIZE * SIZE];
            int gradient = edgeValue - interiorValue;

            // Extrapolate by one step
            int extrapolated = edgeValue + gradient;
            return (byte)math.clamp(extrapolated, 0, 255);
        }

        /// <summary>
        /// EPIC 14.19: Extrapolate density by continuing the gradient at the edge.
        /// This produces smoother normals than simple clamping.
        /// </summary>
        private byte ExtrapolateDensity(ref VoxelBlob blob, int edgeX, int edgeY, int edgeZ,
                                        int dirX, int dirY, int dirZ, int SIZE)
        {
            // Get edge value
            byte edgeValue = blob.Densities[edgeX + edgeY * SIZE + edgeZ * SIZE * SIZE];

            // Get interior value (one step back from edge)
            int interiorX = math.clamp(edgeX - dirX, 0, SIZE - 1);
            int interiorY = math.clamp(edgeY - dirY, 0, SIZE - 1);
            int interiorZ = math.clamp(edgeZ - dirZ, 0, SIZE - 1);

            byte interiorValue = blob.Densities[interiorX + interiorY * SIZE + interiorZ * SIZE * SIZE];

            // Calculate and apply gradient
            int gradient = edgeValue - interiorValue;
            int extrapolated = edgeValue + gradient;

            return (byte)math.clamp(extrapolated, 0, 255);
        }

        /// <summary>
        /// EPIC 14.19: Extrapolate density toward air (0) for top boundary.
        /// Blends between the edge gradient and a tendency toward air.
        /// </summary>
        private byte ExtrapolateDensityTowardAir(ref VoxelBlob blob, int edgeX, int edgeY, int edgeZ,
                                                  int dirX, int dirY, int dirZ, int SIZE)
        {
            byte edgeValue = blob.Densities[edgeX + edgeY * SIZE + edgeZ * SIZE * SIZE];

            // Get interior value
            int interiorX = math.clamp(edgeX - dirX, 0, SIZE - 1);
            int interiorY = math.clamp(edgeY - dirY, 0, SIZE - 1);
            int interiorZ = math.clamp(edgeZ - dirZ, 0, SIZE - 1);
            byte interiorValue = blob.Densities[interiorX + interiorY * SIZE + interiorZ * SIZE * SIZE];

            // Calculate gradient from interior to edge
            int gradient = edgeValue - interiorValue;

            // Blend gradient extrapolation with tendency toward air
            // If gradient is already going toward air (negative), continue it
            // If gradient is going toward solid, dampen it and bias toward air
            int extrapolated;
            if (gradient <= 0)
            {
                // Already heading toward air - continue
                extrapolated = edgeValue + gradient;
            }
            else
            {
                // Heading toward solid - dampen and bias toward air
                extrapolated = edgeValue - (gradient / 2) - 8;
            }

            return (byte)math.clamp(extrapolated, 0, 255);
        }

        /// <summary>
        /// EPIC 14.19: Extrapolate density toward solid (255) for bottom boundary.
        /// Blends between the edge gradient and a tendency toward solid.
        /// </summary>
        private byte ExtrapolateDensityTowardSolid(ref VoxelBlob blob, int edgeX, int edgeY, int edgeZ,
                                                    int dirX, int dirY, int dirZ, int SIZE)
        {
            byte edgeValue = blob.Densities[edgeX + edgeY * SIZE + edgeZ * SIZE * SIZE];

            // Get interior value
            int interiorX = math.clamp(edgeX - dirX, 0, SIZE - 1);
            int interiorY = math.clamp(edgeY - dirY, 0, SIZE - 1);
            int interiorZ = math.clamp(edgeZ - dirZ, 0, SIZE - 1);
            byte interiorValue = blob.Densities[interiorX + interiorY * SIZE + interiorZ * SIZE * SIZE];

            // Calculate gradient from interior to edge
            int gradient = edgeValue - interiorValue;

            // Blend gradient extrapolation with tendency toward solid
            int extrapolated;
            if (gradient >= 0)
            {
                // Already heading toward solid - continue
                extrapolated = edgeValue + gradient;
            }
            else
            {
                // Heading toward air - dampen and bias toward solid
                extrapolated = edgeValue - (gradient / 2) + 8;
            }

            return (byte)math.clamp(extrapolated, 0, 255);
        }

        private byte GetMaterial(int cx, int cy, int cz, ref VoxelBlob blob, int SIZE)
        {
            // Simple clamp for materials
            int safeX = math.clamp(cx, 0, SIZE - 1);
            int safeY = math.clamp(cy, 0, SIZE - 1);
            int safeZ = math.clamp(cz, 0, SIZE - 1);

            return blob.Materials[safeX + safeY * SIZE + safeZ * SIZE * SIZE];
        }
    }
}
