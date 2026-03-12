using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Player.Components
{
    /// <summary>
    /// Data structure representing a detected player-player collision pair.
    /// Output by detection phase, consumed by response phase.
    /// Contains physics data from Unity's collision detection plus computed gameplay metrics.
    /// </summary>
    public struct PlayerCollisionPair
    {
        // === Entity References ===
        
        /// <summary>First entity in the collision pair.</summary>
        public Entity EntityA;
        
        /// <summary>Second entity in the collision pair.</summary>
        public Entity EntityB;
        
        // === Contact Geometry (from Unity Physics) ===
        
        /// <summary>World-space contact point (average of all contacts).</summary>
        public float3 ContactPoint;
        
        /// <summary>Contact normal pointing from EntityA towards EntityB.</summary>
        public float3 ContactNormal;
        
        /// <summary>Penetration depth (positive = overlapping, negative = separated).</summary>
        public float PenetrationDepth;
        
        // === Computed Physics Metrics (for gameplay logic) ===
        
        /// <summary>
        /// Relative approach speed at moment of collision (m/s).
        /// Higher values = more forceful collision.
        /// </summary>
        public float ImpactSpeed;
        
        /// <summary>
        /// Calculated impact force (ImpactSpeed * combined effective mass).
        /// Used for stagger threshold comparison.
        /// </summary>
        public float ImpactForce;
        
        /// <summary>
        /// Horizontal overlap amount (combinedRadius - horizontalDistance).
        /// Positive when players are overlapping.
        /// </summary>
        public float Overlap;
        
        // === Positions (for response calculations) ===
        
        /// <summary>World-space position of EntityA at collision time.</summary>
        public float3 PositionA;
        
        /// <summary>World-space position of EntityB at collision time.</summary>
        public float3 PositionB;
        
        // === Velocities (for asymmetric response) ===
        
        /// <summary>Linear velocity of EntityA at collision time.</summary>
        public float3 VelocityA;
        
        /// <summary>Linear velocity of EntityB at collision time.</summary>
        public float3 VelocityB;
        
        // === Timing ===
        
        /// <summary>Network tick when collision was detected.</summary>
        public uint EventTick;
        
        // === Helper Properties ===
        
        /// <summary>Direction from EntityA to EntityB (normalized, horizontal only).</summary>
        public float3 DirectionAtoB => math.normalizesafe(new float3(PositionB.x - PositionA.x, 0, PositionB.z - PositionA.z));
        
        /// <summary>Horizontal speed of EntityA.</summary>
        public float HorizontalSpeedA => math.length(new float3(VelocityA.x, 0, VelocityA.z));
        
        /// <summary>Horizontal speed of EntityB.</summary>
        public float HorizontalSpeedB => math.length(new float3(VelocityB.x, 0, VelocityB.z));
    }
    
    /// <summary>
    /// Constants for hit direction types (used in CollisionEvent.HitDirection).
    /// </summary>
    public static class HitDirectionType
    {
        /// <summary>Player was facing the collision (braced).</summary>
        public const byte Braced = 0;
        
        /// <summary>Player was hit from the side.</summary>
        public const byte Side = 1;
        
        /// <summary>Player was hit from behind (vulnerable).</summary>
        public const byte Back = 2;
        
        /// <summary>Collision was evaded (during dodge roll).</summary>
        public const byte Evaded = 3;
    }
}
