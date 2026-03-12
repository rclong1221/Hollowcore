using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Player.Components;

namespace DIG.Player.Jobs
{
    /// <summary>
    /// Epic 7.7.5: Force calculation result for a collision.
    /// 
    /// Contains all data needed to apply collision effects to an entity.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CollisionForceResult
    {
        public Entity TargetEntity;       // Entity to apply force to
        public Entity OtherEntity;        // Entity they collided with
        public float3 Direction;          // Push direction (away from other)
        public float3 ContactPoint;       // World position of contact
        public float3 CollisionNormal;    // Normal pointing from other to target
        public float ImpactForce;         // Force magnitude
        public float PowerRatio;          // How much of the collision this entity absorbs
        public bool IsImmune;             // True if entity is immune (i-frame or soft collision)
        public bool IsDodging;            // True if entity is currently dodging
        public int HitDirection;          // 0=front, 1=back, 2=side, 3=evaded
    }
    
    /// <summary>
    /// Epic 7.7.5: Force calculation job - computes power ratios and push forces.
    /// 
    /// For each validated collision, calculates power based on speed, mass, and
    /// stance, then determines force distribution between the two entities.
    /// 
    /// Performance: O(V) where V = validated collisions.
    /// Runs parallel across collisions using IJobParallelFor.
    /// 
    /// Epic 7.7.7: FloatMode.Fast enables fused multiply-add and approximate sqrt for SIMD.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CollisionForceCalculationJob : IJobParallelFor
    {
        // Validated collisions from narrowphase (input)
        // Epic 7.7.5: [NoAlias] enables Burst auto-vectorization
        [ReadOnly, NoAlias] public NativeArray<ValidatedCollision> ValidatedCollisions;
        
        // Player data arrays
        [ReadOnly, NoAlias] public NativeArray<PlayerPositionData> PlayerPositions;
        [ReadOnly, NoAlias] public NativeArray<PlayerCollisionData> PlayerCollisionData;
        
        // Collision settings
        public float SprintPowerMultiplier;
        public float DodgeCollisionMultiplier;
        public float DodgeDeflectionAngle;
        public bool CalculateDodgeDeflection;  // Quality-based
        public bool CheckTeams;                 // Quality-based
        public bool FriendlyFireEnabled;
        public bool TeamCollisionEnabled;
        public bool SoftCollisionWhenDisabled;
        public float SoftCollisionForceMultiplier;
        
        // Output: force results for each entity in collision
        [WriteOnly, NoAlias]
        public NativeQueue<CollisionForceResult>.ParallelWriter ForceResultsWriter;
        
        public void Execute(int collisionIndex)
        {
            var collision = ValidatedCollisions[collisionIndex];
            
            var posA = PlayerPositions[collision.IndexA];
            var posB = PlayerPositions[collision.IndexB];
            var dataA = PlayerCollisionData[collision.IndexA];
            var dataB = PlayerCollisionData[collision.IndexB];
            
            // Check grace period for player B
            if (dataB.IgnorePlayerCollision)
                return;
            
            // Check team filtering (if enabled by quality)
            bool isSameTeam = false;
            bool softCollisionOnly = false;
            
            if (CheckTeams)
            {
                isSameTeam = dataA.TeamId != 0 && dataA.TeamId == dataB.TeamId;
                
                if (!FriendlyFireEnabled)
                {
                    if (!SoftCollisionWhenDisabled)
                        return;
                    softCollisionOnly = true;
                }
                
                if (isSameTeam && !TeamCollisionEnabled)
                {
                    if (!SoftCollisionWhenDisabled)
                        return;
                    softCollisionOnly = true;
                }
            }
            
            // Calculate speeds
            float speedA = math.length(new float3(posA.Velocity.x, 0, posA.Velocity.z));
            float speedB = math.length(new float3(posB.Velocity.x, 0, posB.Velocity.z));
            
            // Calculate power for each player
            float powerA = CalculatePower(dataA.PlayerStateStance, speedA);
            float powerB = CalculatePower(dataB.PlayerStateStance, speedB);
            
            // Apply dodge power reduction
            if (dataA.IsDodging)
                powerA *= DodgeCollisionMultiplier;
            if (dataB.IsDodging)
                powerB *= DodgeCollisionMultiplier;
            
            float totalPower = powerA + powerB;
            float powerRatioA = totalPower > 0.001f ? powerA / totalPower : 0.5f;
            float powerRatioB = 1f - powerRatioA;
            
            // Calculate deflect directions
            float3 directionA = -collision.Direction; // Away from B
            float3 directionB = collision.Direction;   // Away from A
            
            if (CalculateDodgeDeflection)
            {
                if (dataA.IsDodging)
                    directionA = DeflectDirection(directionA, DodgeDeflectionAngle);
                if (dataB.IsDodging)
                    directionB = DeflectDirection(directionB, DodgeDeflectionAngle);
            }
            
            // Calculate force multiplier
            float forceMultiplier = softCollisionOnly ? SoftCollisionForceMultiplier : 1f;
            float impactForce = collision.ImpactSpeed * forceMultiplier;
            
            // Output force results for both players
            if (posA.HasSimulate)
            {
                bool immuneA = dataA.InIFrameWindow || softCollisionOnly;
                
                ForceResultsWriter.Enqueue(new CollisionForceResult
                {
                    TargetEntity = collision.EntityA,
                    OtherEntity = collision.EntityB,
                    Direction = directionA,
                    ContactPoint = collision.ContactPoint,
                    CollisionNormal = collision.Direction,
                    ImpactForce = impactForce,
                    PowerRatio = powerRatioA,
                    IsImmune = immuneA,
                    IsDodging = dataA.IsDodging,
                    HitDirection = immuneA ? 3 : 0  // 3 = evaded
                });
            }
            
            if (posB.HasSimulate)
            {
                bool immuneB = dataB.InIFrameWindow || softCollisionOnly;
                
                ForceResultsWriter.Enqueue(new CollisionForceResult
                {
                    TargetEntity = collision.EntityB,
                    OtherEntity = collision.EntityA,
                    Direction = directionB,
                    ContactPoint = collision.ContactPoint,
                    CollisionNormal = -collision.Direction,
                    ImpactForce = impactForce,
                    PowerRatio = powerRatioB,
                    IsImmune = immuneB,
                    IsDodging = dataB.IsDodging,
                    HitDirection = immuneB ? 3 : 0
                });
            }
        }
        
        private float CalculatePower(int stance, float speed)
        {
            // Stance multipliers: 0=Standing(1.0), 1=Crouching(0.8), 2=Prone(0.5)
            float stanceMultiplier = stance switch
            {
                1 => 0.8f,  // Crouching
                2 => 0.5f,  // Prone
                _ => 1.0f   // Standing
            };
            
            // Speed multiplier (sprinting = faster = more power)
            float speedMultiplier = speed > 5f ? SprintPowerMultiplier : 1.0f;
            
            return speed * stanceMultiplier * speedMultiplier;
        }
        
        private float3 DeflectDirection(float3 direction, float angleDegrees)
        {
            // Rotate direction by deflection angle (horizontal plane)
            float angleRad = math.radians(angleDegrees);
            float cos = math.cos(angleRad);
            float sin = math.sin(angleRad);
            
            return new float3(
                direction.x * cos - direction.z * sin,
                direction.y,
                direction.x * sin + direction.z * cos
            );
        }
    }
}
