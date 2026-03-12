using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Burst-compiled jobs for shape-voxel intersection queries.
    /// Optimized with parallel processing for large shapes.
    /// </summary>
    [BurstCompile]
    public static class VoxelShapeQueryJobs
    {
        /// <summary>
        /// Query result for a single voxel within a shape.
        /// </summary>
        public struct VoxelQueryResult
        {
            public int3 VoxelCoord;
            public float DamageMultiplier; // 0-1, factoring in falloff
            public float DistanceFromCenter;
        }
        
        // ========== SPHERE QUERY - PARALLEL ==========
        
        /// <summary>
        /// Parallel job to query voxels within a sphere.
        /// Each job instance processes one potential voxel coordinate.
        /// </summary>
        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        public struct QuerySphereParallelJob : IJobParallelFor
        {
            [ReadOnly] public float3 Center;
            [ReadOnly] public float Radius;
            [ReadOnly] public VoxelDamageFalloff Falloff;
            [ReadOnly] public float EdgeMultiplier;
            [ReadOnly] public float VoxelSize;
            [ReadOnly] public int3 MinVoxel;
            [ReadOnly] public int3 Dimensions;
            
            [WriteOnly] public NativeQueue<VoxelQueryResult>.ParallelWriter Results;
            
            public void Execute(int index)
            {
                // Convert linear index to 3D coordinate within bounding box
                int x = index % Dimensions.x;
                int y = (index / Dimensions.x) % Dimensions.y;
                int z = index / (Dimensions.x * Dimensions.y);
                
                int3 voxelCoord = MinVoxel + new int3(x, y, z);
                float3 voxelCenter = VoxelToWorld(voxelCoord, VoxelSize);
                float distance = math.length(voxelCenter - Center);
                
                if (distance <= Radius)
                {
                    float normalizedDist = distance / math.max(Radius, 0.001f);
                    float damageMult = CalculateFalloff(normalizedDist, Falloff, EdgeMultiplier);
                    
                    Results.Enqueue(new VoxelQueryResult
                    {
                        VoxelCoord = voxelCoord,
                        DamageMultiplier = damageMult,
                        DistanceFromCenter = distance
                    });
                }
            }
        }
        
        /// <summary>
        /// Schedule a parallel sphere query job.
        /// </summary>
        public static JobHandle ScheduleSphereQuery(
            float3 center, float radius, VoxelDamageFalloff falloff, 
            float edgeMult, float voxelSize,
            ref NativeQueue<VoxelQueryResult> results,
            JobHandle dependency = default)
        {
            int radiusInVoxels = (int)math.ceil(radius / voxelSize) + 1;
            int3 centerVoxel = WorldToVoxel(center, voxelSize);
            int3 minVoxel = centerVoxel - radiusInVoxels;
            int3 dimensions = new int3(radiusInVoxels * 2 + 1);
            int totalVoxels = dimensions.x * dimensions.y * dimensions.z;
            
            var job = new QuerySphereParallelJob
            {
                Center = center,
                Radius = radius,
                Falloff = falloff,
                EdgeMultiplier = edgeMult,
                VoxelSize = voxelSize,
                MinVoxel = minVoxel,
                Dimensions = dimensions,
                Results = results.AsParallelWriter()
            };
            
            return job.Schedule(totalVoxels, 64, dependency);
        }
        
        // ========== CYLINDER QUERY - PARALLEL ==========
        
        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        public struct QueryCylinderParallelJob : IJobParallelFor
        {
            [ReadOnly] public float3 Center;
            [ReadOnly] public float3 Axis;
            [ReadOnly] public float Radius;
            [ReadOnly] public float HalfHeight;
            [ReadOnly] public VoxelDamageFalloff Falloff;
            [ReadOnly] public float EdgeMultiplier;
            [ReadOnly] public float VoxelSize;
            [ReadOnly] public int3 MinVoxel;
            [ReadOnly] public int3 Dimensions;
            
            [WriteOnly] public NativeQueue<VoxelQueryResult>.ParallelWriter Results;
            
            public void Execute(int index)
            {
                int x = index % Dimensions.x;
                int y = (index / Dimensions.x) % Dimensions.y;
                int z = index / (Dimensions.x * Dimensions.y);
                
                int3 voxelCoord = MinVoxel + new int3(x, y, z);
                float3 voxelCenter = VoxelToWorld(voxelCoord, VoxelSize);
                float3 localPos = voxelCenter - Center;
                
                float heightOnAxis = math.dot(localPos, Axis);
                float3 radialVec = localPos - Axis * heightOnAxis;
                float radialDist = math.length(radialVec);
                
                if (math.abs(heightOnAxis) <= HalfHeight && radialDist <= Radius)
                {
                    float normalizedDist = radialDist / math.max(Radius, 0.001f);
                    float damageMult = CalculateFalloff(normalizedDist, Falloff, EdgeMultiplier);
                    
                    Results.Enqueue(new VoxelQueryResult
                    {
                        VoxelCoord = voxelCoord,
                        DamageMultiplier = damageMult,
                        DistanceFromCenter = radialDist
                    });
                }
            }
        }
        
        public static JobHandle ScheduleCylinderQuery(
            float3 center, quaternion rotation, float radius, float height,
            VoxelDamageFalloff falloff, float edgeMult, float voxelSize,
            ref NativeQueue<VoxelQueryResult> results,
            JobHandle dependency = default)
        {
            // Use Z-axis for cylinder to match LookRotation(forward) behavior commonly used for drills/tools
            float3 axis = math.mul(rotation, new float3(0, 0, 1));
            float halfHeight = height * 0.5f;
            
            int maxExtent = (int)math.ceil(math.max(radius, halfHeight) / voxelSize) + 1;
            int3 centerVoxel = WorldToVoxel(center, voxelSize);
            int3 minVoxel = centerVoxel - maxExtent;
            int3 dimensions = new int3(maxExtent * 2 + 1);
            int totalVoxels = dimensions.x * dimensions.y * dimensions.z;
            
            var job = new QueryCylinderParallelJob
            {
                Center = center,
                Axis = axis,
                Radius = radius,
                HalfHeight = halfHeight,
                Falloff = falloff,
                EdgeMultiplier = edgeMult,
                VoxelSize = voxelSize,
                MinVoxel = minVoxel,
                Dimensions = dimensions,
                Results = results.AsParallelWriter()
            };
            
            return job.Schedule(totalVoxels, 64, dependency);
        }
        
        // ========== CONE QUERY - PARALLEL ==========
        
        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        public struct QueryConeParallelJob : IJobParallelFor
        {
            [ReadOnly] public float3 Tip;
            [ReadOnly] public float3 Forward;
            [ReadOnly] public float TanHalfAngle;
            [ReadOnly] public float Length;
            [ReadOnly] public float TipRadius;
            [ReadOnly] public VoxelDamageFalloff Falloff;
            [ReadOnly] public float EdgeMultiplier;
            [ReadOnly] public float VoxelSize;
            [ReadOnly] public int3 MinVoxel;
            [ReadOnly] public int3 Dimensions;
            
            [WriteOnly] public NativeQueue<VoxelQueryResult>.ParallelWriter Results;
            
            public void Execute(int index)
            {
                int x = index % Dimensions.x;
                int y = (index / Dimensions.x) % Dimensions.y;
                int z = index / (Dimensions.x * Dimensions.y);
                
                int3 voxelCoord = MinVoxel + new int3(x, y, z);
                float3 voxelCenter = VoxelToWorld(voxelCoord, VoxelSize);
                float3 toVoxel = voxelCenter - Tip;
                
                float distAlongAxis = math.dot(toVoxel, Forward);
                if (distAlongAxis < 0 || distAlongAxis > Length)
                    return;
                
                float3 radialVec = toVoxel - Forward * distAlongAxis;
                float radialDist = math.length(radialVec);
                float coneRadiusAtDist = TipRadius + distAlongAxis * TanHalfAngle;
                
                if (radialDist <= coneRadiusAtDist)
                {
                    float normalizedDist = radialDist / math.max(coneRadiusAtDist, 0.001f);
                    float damageMult = CalculateFalloff(normalizedDist, Falloff, EdgeMultiplier);
                    
                    Results.Enqueue(new VoxelQueryResult
                    {
                        VoxelCoord = voxelCoord,
                        DamageMultiplier = damageMult,
                        DistanceFromCenter = radialDist
                    });
                }
            }
        }
        
        public static JobHandle ScheduleConeQuery(
            float3 tip, quaternion rotation, float angleDegrees, float length, float tipRadius,
            VoxelDamageFalloff falloff, float edgeMult, float voxelSize,
            ref NativeQueue<VoxelQueryResult> results,
            JobHandle dependency = default)
        {
            float3 forward = math.mul(rotation, new float3(0, 0, 1));
            float tanHalfAngle = math.tan(math.radians(angleDegrees * 0.5f));
            float maxRadius = tipRadius + length * tanHalfAngle;
            
            int maxExtent = (int)math.ceil(math.max(maxRadius, length) / voxelSize) + 1;
            int3 tipVoxel = WorldToVoxel(tip, voxelSize);
            int3 minVoxel = tipVoxel - maxExtent;
            int3 dimensions = new int3(maxExtent * 2 + 1);
            int totalVoxels = dimensions.x * dimensions.y * dimensions.z;
            
            var job = new QueryConeParallelJob
            {
                Tip = tip,
                Forward = forward,
                TanHalfAngle = tanHalfAngle,
                Length = length,
                TipRadius = tipRadius,
                Falloff = falloff,
                EdgeMultiplier = edgeMult,
                VoxelSize = voxelSize,
                MinVoxel = minVoxel,
                Dimensions = dimensions,
                Results = results.AsParallelWriter()
            };
            
            return job.Schedule(totalVoxels, 64, dependency);
        }
        
        // ========== CAPSULE QUERY - PARALLEL ==========
        
        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        public struct QueryCapsuleParallelJob : IJobParallelFor
        {
            [ReadOnly] public float3 Start;
            [ReadOnly] public float3 End;
            [ReadOnly] public float3 LineDir;
            [ReadOnly] public float LineLength;
            [ReadOnly] public float Radius;
            [ReadOnly] public VoxelDamageFalloff Falloff;
            [ReadOnly] public float EdgeMultiplier;
            [ReadOnly] public float VoxelSize;
            [ReadOnly] public int3 MinVoxel;
            [ReadOnly] public int3 Dimensions;
            
            [WriteOnly] public NativeQueue<VoxelQueryResult>.ParallelWriter Results;
            
            public void Execute(int index)
            {
                int x = index % Dimensions.x;
                int y = (index / Dimensions.x) % Dimensions.y;
                int z = index / (Dimensions.x * Dimensions.y);
                
                int3 voxelCoord = MinVoxel + new int3(x, y, z);
                float3 voxelCenter = VoxelToWorld(voxelCoord, VoxelSize);
                
                // Distance to line segment
                float t = math.saturate(math.dot(voxelCenter - Start, LineDir) / math.max(LineLength, 0.001f));
                float3 closest = Start + t * (End - Start);
                float distance = math.length(voxelCenter - closest);
                
                if (distance <= Radius)
                {
                    float normalizedDist = distance / math.max(Radius, 0.001f);
                    float damageMult = CalculateFalloff(normalizedDist, Falloff, EdgeMultiplier);
                    
                    Results.Enqueue(new VoxelQueryResult
                    {
                        VoxelCoord = voxelCoord,
                        DamageMultiplier = damageMult,
                        DistanceFromCenter = distance
                    });
                }
            }
        }
        
        public static JobHandle ScheduleCapsuleQuery(
            float3 center, quaternion rotation, float radius, float length,
            VoxelDamageFalloff falloff, float edgeMult, float voxelSize,
            ref NativeQueue<VoxelQueryResult> results,
            JobHandle dependency = default)
        {
            float3 forward = math.mul(rotation, new float3(0, 0, 1));
            float halfLength = length * 0.5f;
            float3 start = center - forward * halfLength;
            float3 end = center + forward * halfLength;
            
            int maxExtent = (int)math.ceil((halfLength + radius) / voxelSize) + 1;
            int3 centerVoxel = WorldToVoxel(center, voxelSize);
            int3 minVoxel = centerVoxel - maxExtent;
            int3 dimensions = new int3(maxExtent * 2 + 1);
            int totalVoxels = dimensions.x * dimensions.y * dimensions.z;
            
            var job = new QueryCapsuleParallelJob
            {
                Start = start,
                End = end,
                LineDir = forward,
                LineLength = length,
                Radius = radius,
                Falloff = falloff,
                EdgeMultiplier = edgeMult,
                VoxelSize = voxelSize,
                MinVoxel = minVoxel,
                Dimensions = dimensions,
                Results = results.AsParallelWriter()
            };
            
            return job.Schedule(totalVoxels, 64, dependency);
        }
        
        // ========== BOX QUERY - PARALLEL ==========
        
        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        public struct QueryBoxParallelJob : IJobParallelFor
        {
            [ReadOnly] public float3 Center;
            [ReadOnly] public quaternion InvRotation;
            [ReadOnly] public float3 Extents;
            [ReadOnly] public VoxelDamageFalloff Falloff;
            [ReadOnly] public float EdgeMultiplier;
            [ReadOnly] public float VoxelSize;
            [ReadOnly] public int3 MinVoxel;
            [ReadOnly] public int3 Dimensions;
            
            [WriteOnly] public NativeQueue<VoxelQueryResult>.ParallelWriter Results;
            
            public void Execute(int index)
            {
                int x = index % Dimensions.x;
                int y = (index / Dimensions.x) % Dimensions.y;
                int z = index / (Dimensions.x * Dimensions.y);
                
                int3 voxelCoord = MinVoxel + new int3(x, y, z);
                float3 voxelCenter = VoxelToWorld(voxelCoord, VoxelSize);
                
                float3 localPos = math.mul(InvRotation, voxelCenter - Center);
                
                if (math.abs(localPos.x) <= Extents.x &&
                    math.abs(localPos.y) <= Extents.y &&
                    math.abs(localPos.z) <= Extents.z)
                {
                    float3 normalizedPos = localPos / math.max(Extents, new float3(0.001f));
                    float normalizedDist = math.cmax(math.abs(normalizedPos));
                    float damageMult = CalculateFalloff(normalizedDist, Falloff, EdgeMultiplier);
                    
                    Results.Enqueue(new VoxelQueryResult
                    {
                        VoxelCoord = voxelCoord,
                        DamageMultiplier = damageMult,
                        DistanceFromCenter = math.length(localPos)
                    });
                }
            }
        }
        
        public static JobHandle ScheduleBoxQuery(
            float3 center, quaternion rotation, float3 extents,
            VoxelDamageFalloff falloff, float edgeMult, float voxelSize,
            ref NativeQueue<VoxelQueryResult> results,
            JobHandle dependency = default)
        {
            quaternion invRotation = math.inverse(rotation);
            float maxExtent = math.cmax(extents);
            
            int extentInVoxels = (int)math.ceil(maxExtent * 1.5f / voxelSize) + 1;
            int3 centerVoxel = WorldToVoxel(center, voxelSize);
            int3 minVoxel = centerVoxel - extentInVoxels;
            int3 dimensions = new int3(extentInVoxels * 2 + 1);
            int totalVoxels = dimensions.x * dimensions.y * dimensions.z;
            
            var job = new QueryBoxParallelJob
            {
                Center = center,
                InvRotation = invRotation,
                Extents = extents,
                Falloff = falloff,
                EdgeMultiplier = edgeMult,
                VoxelSize = voxelSize,
                MinVoxel = minVoxel,
                Dimensions = dimensions,
                Results = results.AsParallelWriter()
            };
            
            return job.Schedule(totalVoxels, 64, dependency);
        }
        
        // ========== POINT QUERY (SINGLE VOXEL) ==========
        
        public static VoxelQueryResult QueryPoint(float3 position, float voxelSize)
        {
            return new VoxelQueryResult
            {
                VoxelCoord = WorldToVoxel(position, voxelSize),
                DamageMultiplier = 1f,
                DistanceFromCenter = 0f
            };
        }
        
        // ========== UTILITY FUNCTIONS ==========
        
        private static int3 WorldToVoxel(float3 worldPos, float voxelSize)
        {
            return (int3)math.floor(worldPos / voxelSize);
        }
        
        private static float3 VoxelToWorld(int3 voxelCoord, float voxelSize)
        {
            return (float3)voxelCoord * voxelSize + voxelSize * 0.5f;
        }
        
        private static float CalculateFalloff(float normalizedDistance, VoxelDamageFalloff falloff, float edgeMultiplier)
        {
            switch (falloff)
            {
                case VoxelDamageFalloff.None:
                    return 1f;
                    
                case VoxelDamageFalloff.Linear:
                    return math.lerp(1f, edgeMultiplier, normalizedDistance);
                    
                case VoxelDamageFalloff.Quadratic:
                    float quadratic = 1f - normalizedDistance * normalizedDistance;
                    return math.lerp(edgeMultiplier, 1f, quadratic);
                    
                case VoxelDamageFalloff.InverseSquare:
                    // Remapped inverse square to ensure we hit edgeMultiplier at dist=1
                    // Formula: T(d) = 2/(1+d^2) - 1. T(0)=1, T(1)=0.
                    float invSq = 2f / (1f + normalizedDistance * normalizedDistance) - 1f;
                    return math.lerp(edgeMultiplier, 1f, invSq);
                    
                case VoxelDamageFalloff.Shell:
                    return normalizedDistance > 0.8f ? 1f : 0f;
                    
                case VoxelDamageFalloff.Core:
                    // Taper from 1.0 (at 0.3) to 0.0 (at 1.0)
                    if (normalizedDistance < 0.3f) return 1f;
                    float t = 1f - (normalizedDistance - 0.3f) / 0.7f;
                    return math.lerp(edgeMultiplier, 1f, t);
                    
                default:
                    return 1f;
            }
        }
    }
}
