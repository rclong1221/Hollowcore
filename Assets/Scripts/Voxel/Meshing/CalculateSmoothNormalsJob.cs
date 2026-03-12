using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace DIG.Voxel.Meshing
{
    /// <summary>
    /// Job to calculate smooth normals from density gradient.
    /// This provides much better lighting than flat face normals.
    ///
    /// OPTIMIZATION 10.9.20: Native Memory Aliasing
    /// - [NoAlias] on distinct buffers for Burst optimization
    /// - [NativeDisableContainerSafetyRestriction] on read-only buffers
    ///
    /// EPIC 14.19: Fixed "folded paper" artifacts at chunk boundaries
    /// - Uses larger gradient sampling distance for smoother normals
    /// - Applies Sobel-like weighted gradient for better quality
    /// - Handles boundary cases with proper extrapolation
    /// </summary>
    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct CalculateSmoothNormalsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias, NativeDisableContainerSafetyRestriction]
        public NativeArray<byte> Densities;

        [ReadOnly, NoAlias, NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> Vertices;

        [WriteOnly, NoAlias]
        public NativeArray<float3> SmoothNormals;

        public byte IsoLevel;

        private const int PADDED_SIZE = 34;

        // EPIC 14.19: Increased gradient sample distance for smoother normals at boundaries
        // Using 1.0 instead of 0.5 helps average out discontinuities
        private const float GRADIENT_SAMPLE_DISTANCE = 1.0f;

        public void Execute(int i)
        {
            // Convert scaled vertex back to density-space position (Assuming scale 1 for 8.12)
            float3 pos = Vertices[i];

            // Calculate normal from density gradient
            SmoothNormals[i] = CalculateNormalFromGradient(pos);
        }


        /// <summary>
        /// Calculate surface normal from density gradient at the given position.
        /// The gradient points toward solid, so we negate it for the surface normal.
        ///
        /// EPIC 14.19: Uses multi-sample Sobel-like gradient for better quality
        /// </summary>
        private float3 CalculateNormalFromGradient(float3 pos)
        {
            // Map to padded buffer coordinates (offset by 1)
            float3 paddedPos = pos + 1f;

            // EPIC 14.19: Check if we're near a boundary (within 1.5 voxels of edge)
            // Near boundaries, use a wider sampling pattern to smooth discontinuities
            bool nearBoundary = paddedPos.x < 2.5f || paddedPos.x > PADDED_SIZE - 2.5f ||
                                paddedPos.y < 2.5f || paddedPos.y > PADDED_SIZE - 2.5f ||
                                paddedPos.z < 2.5f || paddedPos.z > PADDED_SIZE - 2.5f;

            float3 gradient;

            if (nearBoundary)
            {
                // EPIC 14.19: At boundaries, use weighted multi-sample gradient (Sobel-like)
                // This averages multiple gradient samples to reduce discontinuities
                gradient = CalculateSobelGradient(paddedPos);
            }
            else
            {
                // Interior: standard central differences with larger sample distance
                float d = GRADIENT_SAMPLE_DISTANCE;
                float dx = SampleDensity(paddedPos + new float3(d, 0, 0)) -
                           SampleDensity(paddedPos - new float3(d, 0, 0));
                float dy = SampleDensity(paddedPos + new float3(0, d, 0)) -
                           SampleDensity(paddedPos - new float3(0, d, 0));
                float dz = SampleDensity(paddedPos + new float3(0, 0, d)) -
                           SampleDensity(paddedPos - new float3(0, 0, d));

                gradient = new float3(-dx, -dy, -dz);
            }

            // Normalize (with fallback for degenerate cases)
            float len = math.length(gradient);
            if (len > 0.0001f)
                return gradient / len;
            else
                return new float3(0, 1, 0); // Default up
        }

        /// <summary>
        /// EPIC 14.19: Calculate gradient using Sobel-like 3x3x3 kernel
        /// This produces much smoother normals at chunk boundaries by averaging
        /// multiple samples with appropriate weights.
        /// </summary>
        private float3 CalculateSobelGradient(float3 pos)
        {
            // Sobel weights: center slice gets 2x weight, corners get 1x
            // This approximates a Gaussian-weighted gradient

            float dx = 0f, dy = 0f, dz = 0f;

            // Sample at 6 cardinal directions with distance 1.0
            // Plus 8 diagonal samples for smoothing
            float d = GRADIENT_SAMPLE_DISTANCE;

            // Primary axis samples (weight 2)
            dx += 2f * (SampleDensitySafe(pos + new float3(d, 0, 0)) - SampleDensitySafe(pos - new float3(d, 0, 0)));
            dy += 2f * (SampleDensitySafe(pos + new float3(0, d, 0)) - SampleDensitySafe(pos - new float3(0, d, 0)));
            dz += 2f * (SampleDensitySafe(pos + new float3(0, 0, d)) - SampleDensitySafe(pos - new float3(0, 0, d)));

            // Edge samples (weight 1) - sample along edges of the cube
            // XY plane edges
            dx += SampleDensitySafe(pos + new float3(d, d, 0)) - SampleDensitySafe(pos - new float3(d, d, 0));
            dx += SampleDensitySafe(pos + new float3(d, -d, 0)) - SampleDensitySafe(pos - new float3(d, -d, 0));
            dy += SampleDensitySafe(pos + new float3(d, d, 0)) - SampleDensitySafe(pos - new float3(d, d, 0));
            dy += SampleDensitySafe(pos + new float3(-d, d, 0)) - SampleDensitySafe(pos - new float3(-d, d, 0));

            // XZ plane edges
            dx += SampleDensitySafe(pos + new float3(d, 0, d)) - SampleDensitySafe(pos - new float3(d, 0, d));
            dx += SampleDensitySafe(pos + new float3(d, 0, -d)) - SampleDensitySafe(pos - new float3(d, 0, -d));
            dz += SampleDensitySafe(pos + new float3(d, 0, d)) - SampleDensitySafe(pos - new float3(d, 0, d));
            dz += SampleDensitySafe(pos + new float3(-d, 0, d)) - SampleDensitySafe(pos - new float3(-d, 0, d));

            // YZ plane edges
            dy += SampleDensitySafe(pos + new float3(0, d, d)) - SampleDensitySafe(pos - new float3(0, d, d));
            dy += SampleDensitySafe(pos + new float3(0, d, -d)) - SampleDensitySafe(pos - new float3(0, d, -d));
            dz += SampleDensitySafe(pos + new float3(0, d, d)) - SampleDensitySafe(pos - new float3(0, d, d));
            dz += SampleDensitySafe(pos + new float3(0, -d, d)) - SampleDensitySafe(pos - new float3(0, -d, d));

            // Normalize by total weight (2 + 4 samples per axis = 6, but with varying contributions)
            // The gradient direction is what matters, magnitude will be normalized anyway
            return new float3(-dx, -dy, -dz);
        }

        /// <summary>
        /// EPIC 14.19: Sample density with boundary-aware extrapolation
        /// Instead of clamping at boundaries (which creates artificial gradients),
        /// this extrapolates the density trend to produce smoother normals.
        /// </summary>
        private float SampleDensitySafe(float3 pos)
        {
            // Check if position is within safe sampling range
            bool inBounds = pos.x >= 0.5f && pos.x < PADDED_SIZE - 0.5f &&
                            pos.y >= 0.5f && pos.y < PADDED_SIZE - 0.5f &&
                            pos.z >= 0.5f && pos.z < PADDED_SIZE - 0.5f;

            if (inBounds)
            {
                return SampleDensity(pos);
            }

            // EPIC 14.19: Extrapolate from interior instead of clamping
            // This prevents artificial density walls at boundaries
            float3 clampedPos = math.clamp(pos, 0.5f, PADDED_SIZE - 0.5f);
            float3 offset = pos - clampedPos;

            // Sample at clamped position
            float baseDensity = SampleDensity(clampedPos);

            // Estimate gradient at boundary and extrapolate
            // Use a smaller step for gradient estimation at the edge
            float3 gradientDir = math.normalizesafe(offset);
            float stepSize = 0.5f;
            float3 interiorPos = clampedPos - gradientDir * stepSize;
            interiorPos = math.clamp(interiorPos, 0.5f, PADDED_SIZE - 0.5f);

            float interiorDensity = SampleDensity(interiorPos);
            float gradientMag = baseDensity - interiorDensity;

            // Extrapolate: continue the gradient trend
            float extrapolationDist = math.length(offset);
            float extrapolatedDensity = baseDensity + gradientMag * (extrapolationDist / stepSize);

            // Clamp to valid density range
            return math.clamp(extrapolatedDensity, 0f, 255f);
        }

        /// <summary>
        /// Sample density with trilinear interpolation for smooth gradients.
        /// </summary>
        private float SampleDensity(float3 pos)
        {
            // Clamp to valid range
            pos = math.clamp(pos, 0, PADDED_SIZE - 1.001f);

            // Get integer and fractional parts
            int3 p0 = (int3)math.floor(pos);
            float3 frac = pos - p0;

            // Clamp to avoid out of bounds
            int3 p1 = math.min(p0 + 1, PADDED_SIZE - 1);
            p0 = math.max(p0, 0);

            // Sample 8 corners
            float c000 = Densities[p0.x + p0.y * PADDED_SIZE + p0.z * PADDED_SIZE * PADDED_SIZE];
            float c100 = Densities[p1.x + p0.y * PADDED_SIZE + p0.z * PADDED_SIZE * PADDED_SIZE];
            float c010 = Densities[p0.x + p1.y * PADDED_SIZE + p0.z * PADDED_SIZE * PADDED_SIZE];
            float c110 = Densities[p1.x + p1.y * PADDED_SIZE + p0.z * PADDED_SIZE * PADDED_SIZE];
            float c001 = Densities[p0.x + p0.y * PADDED_SIZE + p1.z * PADDED_SIZE * PADDED_SIZE];
            float c101 = Densities[p1.x + p0.y * PADDED_SIZE + p1.z * PADDED_SIZE * PADDED_SIZE];
            float c011 = Densities[p0.x + p1.y * PADDED_SIZE + p1.z * PADDED_SIZE * PADDED_SIZE];
            float c111 = Densities[p1.x + p1.y * PADDED_SIZE + p1.z * PADDED_SIZE * PADDED_SIZE];

            // Trilinear interpolation
            float c00 = math.lerp(c000, c100, frac.x);
            float c10 = math.lerp(c010, c110, frac.x);
            float c01 = math.lerp(c001, c101, frac.x);
            float c11 = math.lerp(c011, c111, frac.x);

            float c0 = math.lerp(c00, c10, frac.y);
            float c1 = math.lerp(c01, c11, frac.y);

            return math.lerp(c0, c1, frac.z);
        }
    }
}
