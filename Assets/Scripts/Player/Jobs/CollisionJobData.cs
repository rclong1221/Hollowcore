using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Player.Jobs
{
    /// <summary>
    /// Epic 7.7.5: Collision pair data structure for job pipeline.
    /// 
    /// Packed to fit in cache line (64 bytes) for optimal memory access.
    /// Contains all data needed for narrowphase and force calculation.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CollisionPair
    {
        // Entities involved (16 bytes)
        public Entity EntityA;
        public Entity EntityB;
        
        // Indices into player data arrays (8 bytes)
        public int IndexA;
        public int IndexB;
        
        // Cell index where pair was found (4 bytes)
        public int CellIndex;
        
        // Padding to 32 bytes for alignment
        public int Padding;
    }
    
    /// <summary>
    /// Epic 7.7.5: Validated collision data after narrowphase.
    /// 
    /// Contains computed distance and direction for pairs that passed
    /// the distance threshold check.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ValidatedCollision
    {
        // Entities (16 bytes)
        public Entity EntityA;
        public Entity EntityB;
        
        // Indices for player data lookup (8 bytes)
        public int IndexA;
        public int IndexB;
        
        // Computed collision data (28 bytes)
        public float3 Direction;      // Normalized direction A → B
        public float3 ContactPoint;   // Midpoint between A and B
        public float Distance;        // Horizontal distance between centers
        public float ApproachSpeed;   // Relative velocity along direction
        public float ImpactSpeed;     // Combined speeds
        
        // Padding to 64 bytes
        public float Padding1;
        public float Padding2;
    }
    
    /// <summary>
    /// Epic 7.7.5: Player position/velocity data for jobs (SoA-friendly).
    /// 
    /// Extracted during gather phase for cache-friendly iteration in jobs.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct PlayerPositionData
    {
        public Entity Entity;
        public float3 Position;
        public float3 Velocity;
        public float Radius;
        public bool HasSimulate;
        public bool IsOnCooldown;
        public bool IsStaggeredOrKnockedDown;
        public bool HasGracePeriod;
    }
    
    /// <summary>
    /// Epic 7.7.5: Additional player data for collision calculations.
    /// 
    /// Accessed only for pairs that pass broadphase, reducing cache pressure.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct PlayerCollisionData
    {
        public int PlayerStateStance;   // Enum as int for Burst compatibility
        public int PlayerMode;          // PlayerMode enum
        public bool IsDodging;
        public float DodgeElapsed;
        public float DodgeInvulnStart;
        public float DodgeInvulnEnd;
        public bool InIFrameWindow;
        public uint TeamId;
        public bool IgnorePlayerCollision;  // From grace period
    }
}
