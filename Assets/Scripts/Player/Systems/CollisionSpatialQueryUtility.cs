using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Collections;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Utility for performing spatial queries using Unity Physics BVH.
    /// Epic 7.3.2: Leverages Unity's built-in BVH spatial partitioning.
    /// 
    /// NOTE: These utilities should be called from within systems where
    /// PhysicsWorld is available via SystemAPI or ComponentSystemBase.
    /// </summary>
    public static class CollisionSpatialQueryUtility
    {
        /// <summary>
        /// Performs a raycast query using Unity Physics BVH.
        /// Must be called from within a system context with access to PhysicsWorld.
        /// </summary>
        public static bool Raycast(
            in PhysicsWorld physicsWorld,
            in float3 origin,
            in float3 direction,
            float maxDistance,
            out RaycastHit hit,
            CollisionFilter filter)
        {
            var input = new RaycastInput
            {
                Start = origin,
                End = origin + direction * maxDistance,
                Filter = filter
            };
            
            return physicsWorld.CastRay(input, out hit);
        }
        
        /// <summary>
        /// Performs a raycast query that returns all hits along the ray.
        /// Must be called from within a system context with access to PhysicsWorld.
        /// </summary>
        public static bool RaycastAll(
            in PhysicsWorld physicsWorld,
            in float3 origin,
            in float3 direction,
            float maxDistance,
            ref NativeList<RaycastHit> hits,
            CollisionFilter filter)
        {
            var input = new RaycastInput
            {
                Start = origin,
                End = origin + direction * maxDistance,
                Filter = filter
            };
            
            return physicsWorld.CastRay(input, ref hits);
        }
        
        /// <summary>
        /// Performs an overlap sphere query using Unity Physics BVH.
        /// Returns all colliders within the specified distance from center.
        /// Must be called from within a system context with access to PhysicsWorld.
        /// </summary>
        public static void OverlapSphere(
            in PhysicsWorld physicsWorld,
            in float3 center,
            float radius,
            ref NativeList<DistanceHit> hits,
            CollisionFilter filter)
        {
            var input = new PointDistanceInput
            {
                Position = center,
                MaxDistance = radius,
                Filter = filter
            };
            
            physicsWorld.CalculateDistance(input, ref hits);
        }
        
        /// <summary>
        /// Performs an AABB overlap query using Unity Physics BVH.
        /// Returns all body indices within the AABB.
        /// Must be called from within a system context with access to PhysicsWorld.
        /// </summary>
        public static void OverlapAabb(
            in PhysicsWorld physicsWorld,
            in float3 min,
            in float3 max,
            ref NativeList<int> bodyIndices,
            CollisionFilter filter)
        {
            var input = new OverlapAabbInput
            {
                Aabb = new Aabb { Min = min, Max = max },
                Filter = filter
            };
            
            physicsWorld.CollisionWorld.OverlapAabb(input, ref bodyIndices);
        }
        
        /// <summary>
        /// Finds the closest point on a collider to a given position.
        /// Must be called from within a system context with access to PhysicsWorld.
        /// </summary>
        public static bool ClosestPoint(
            in PhysicsWorld physicsWorld,
            in float3 position,
            float maxDistance,
            out DistanceHit closestHit,
            CollisionFilter filter)
        {
            var input = new PointDistanceInput
            {
                Position = position,
                MaxDistance = maxDistance,
                Filter = filter
            };
            
            return physicsWorld.CalculateDistance(input, out closestHit);
        }
        
        /// <summary>
        /// Checks if a point is inside any collider.
        /// Must be called from within a system context with access to PhysicsWorld.
        /// </summary>
        public static bool PointInsideCollider(
            in PhysicsWorld physicsWorld,
            in float3 position,
            CollisionFilter filter)
        {
            var input = new PointDistanceInput
            {
                Position = position,
                MaxDistance = 0f,
                Filter = filter
            };
            
            return physicsWorld.CalculateDistance(input, out _);
        }
        
        /// <summary>
        /// Creates a standard collision filter for player-environment collisions.
        /// </summary>
        public static CollisionFilter CreatePlayerEnvironmentFilter()
        {
            return new CollisionFilter
            {
                BelongsTo = 1u << 0,  // Player layer
                CollidesWith = 1u << 1, // Environment layer
                GroupIndex = 0
            };
        }
        
        /// <summary>
        /// Creates a collision filter that collides with everything.
        /// </summary>
        public static CollisionFilter CreateAllFilter()
        {
            return CollisionFilter.Default;
        }
    }
}
