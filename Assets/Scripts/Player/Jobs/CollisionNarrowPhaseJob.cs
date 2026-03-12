using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace DIG.Player.Jobs
{
    /// <summary>
    /// Epic 7.7.5: Narrowphase job - validates candidate pairs with distance checks.
    /// 
    /// For each candidate collision pair from broadphase, calculates actual
    /// distance and filters by collision threshold. Only pairs within threshold
    /// and approaching each other are passed to force calculation.
    /// 
    /// Performance: O(P) where P = candidate pairs from broadphase.
    /// Runs parallel across pairs using IJobParallelFor.
    /// 
    /// Epic 7.7.7: FloatMode.Fast enables fused multiply-add and approximate sqrt for SIMD.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CollisionNarrowPhaseJob : IJobParallelFor
    {
        // Candidate pairs from broadphase (input)
        // Epic 7.7.5: [NoAlias] enables Burst auto-vectorization
        [ReadOnly, NoAlias] public NativeArray<CollisionPair> CandidatePairs;
        
        // Player position data
        [ReadOnly, NoAlias] public NativeArray<PlayerPositionData> PlayerPositions;
        
        // Collision threshold (quality-adjusted)
        public float CollisionThreshold;
        
        // Stagger threshold (minimum impact speed to process)
        public float StaggerThreshold;
        
        // Output: validated collisions with computed data
        [WriteOnly, NoAlias]
        public NativeQueue<ValidatedCollision>.ParallelWriter ValidatedCollisionsWriter;
        
        public void Execute(int pairIndex)
        {
            var pair = CandidatePairs[pairIndex];
            
            var playerA = PlayerPositions[pair.IndexA];
            var playerB = PlayerPositions[pair.IndexB];
            
            // Calculate horizontal distance
            float3 toB = playerB.Position - playerA.Position;
            toB.y = 0;
            float horizontalDist = math.length(toB);
            
            // Combined radius for collision detection
            float combinedRadius = playerA.Radius + playerB.Radius;
            float threshold = combinedRadius + CollisionThreshold;
            
            // Skip if too far or too close (avoid division by zero)
            if (horizontalDist >= threshold || horizontalDist <= 0.01f)
                return;
            
            // Calculate direction and relative velocity
            float3 direction = toB / horizontalDist; // Normalized direction A → B
            float3 relativeVel = playerA.Velocity - playerB.Velocity;
            float approachSpeed = math.dot(relativeVel, direction);
            
            // Only process if approaching (positive approach speed)
            if (approachSpeed <= 0.1f)
                return;
            
            // Calculate impact speed (combine both players' contributions)
            float speedA = math.length(new float3(playerA.Velocity.x, 0, playerA.Velocity.z));
            float speedB = math.length(new float3(playerB.Velocity.x, 0, playerB.Velocity.z));
            float impactSpeed = speedA + speedB;
            
            // Skip if impact is too weak
            if (impactSpeed < StaggerThreshold)
                return;
            
            // Calculate contact point (midpoint)
            float3 contactPoint = (playerA.Position + playerB.Position) * 0.5f;
            
            // Output validated collision
            ValidatedCollisionsWriter.Enqueue(new ValidatedCollision
            {
                EntityA = pair.EntityA,
                EntityB = pair.EntityB,
                IndexA = pair.IndexA,
                IndexB = pair.IndexB,
                Direction = direction,
                ContactPoint = contactPoint,
                Distance = horizontalDist,
                ApproachSpeed = approachSpeed,
                ImpactSpeed = impactSpeed
            });
        }
    }
}
