using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;

namespace DIG.Performance
{
    /// <summary>
    /// Epic 7.7.2: Memory optimization utilities for collision systems.
    /// 
    /// Provides capacity calculation helpers and pre-allocation hints to minimize
    /// NativeContainer resizing during gameplay.
    /// 
    /// Usage:
    ///   int capacity = MemoryOptimizationUtility.CalculatePlayerListCapacity(expectedPlayerCount);
    ///   var players = new NativeList<PlayerData>(capacity, Allocator.TempJob);
    /// </summary>
    public static class MemoryOptimizationUtility
    {
        // === Capacity Calculation Constants ===
        
        /// <summary>
        /// Default expected player count for initial allocations.
        /// Based on typical match size (16 players).
        /// </summary>
        public const int DefaultPlayerCount = 16;
        
        /// <summary>
        /// Maximum expected player count for stress scenarios.
        /// Used to prevent excessive reallocation in large lobbies.
        /// </summary>
        public const int MaxPlayerCount = 100;
        
        /// <summary>
        /// Average collisions per player per frame (empirical estimate).
        /// Used for collision pair buffer pre-allocation.
        /// </summary>
        public const int AvgCollisionsPerPlayer = 3;
        
        /// <summary>
        /// Capacity overhead multiplier to reduce reallocation frequency.
        /// 1.5x = allocate 50% more than expected.
        /// </summary>
        public const float CapacityOverheadMultiplier = 1.5f;
        
        // === Memory Profiler Markers ===
        
        /// <summary>Tracks player list allocation cost</summary>
        public static readonly ProfilerMarker PlayerListAllocation = 
            new ProfilerMarker(ProfilerCategory.Memory, "DIG.Memory.PlayerListAllocation");
        
        /// <summary>Tracks collision pair allocation cost</summary>
        public static readonly ProfilerMarker CollisionPairAllocation = 
            new ProfilerMarker(ProfilerCategory.Memory, "DIG.Memory.CollisionPairAllocation");
        
        /// <summary>Tracks NativeContainer disposal cost</summary>
        public static readonly ProfilerMarker ContainerDisposal = 
            new ProfilerMarker(ProfilerCategory.Memory, "DIG.Memory.ContainerDisposal");
        
        /// <summary>Tracks DynamicBuffer resize operations</summary>
        public static readonly ProfilerMarker BufferResize = 
            new ProfilerMarker(ProfilerCategory.Memory, "DIG.Memory.BufferResize");
        
        // === Capacity Calculation Helpers ===
        
        /// <summary>
        /// Calculates optimal capacity for player data lists.
        /// Includes overhead to minimize reallocations.
        /// </summary>
        /// <param name="expectedPlayerCount">Expected number of players (0 = use default)</param>
        /// <returns>Recommended capacity for NativeList allocation</returns>
        public static int CalculatePlayerListCapacity(int expectedPlayerCount = 0)
        {
            int baseCount = expectedPlayerCount > 0 ? expectedPlayerCount : DefaultPlayerCount;
            return (int)(baseCount * CapacityOverheadMultiplier);
        }
        
        /// <summary>
        /// Calculates optimal capacity for collision pair storage.
        /// Uses formula: playerCount * avgCollisions * overhead
        /// </summary>
        /// <param name="playerCount">Current player count</param>
        /// <returns>Recommended capacity for collision pair arrays</returns>
        public static int CalculateCollisionPairCapacity(int playerCount)
        {
            // O(N²) worst case is N*(N-1)/2, but typical is much lower
            // Use empirical estimate: 3 collisions per player on average
            int expectedPairs = playerCount * AvgCollisionsPerPlayer / 2;
            return (int)(expectedPairs * CapacityOverheadMultiplier);
        }
        
        /// <summary>
        /// Calculates maximum theoretical collision pairs for N players.
        /// Formula: N * (N-1) / 2 (handshake problem)
        /// </summary>
        /// <param name="playerCount">Number of players</param>
        /// <returns>Maximum possible collision pairs</returns>
        public static int CalculateMaxCollisionPairs(int playerCount)
        {
            return playerCount * (playerCount - 1) / 2;
        }
        
        /// <summary>
        /// Gets recommended allocator for per-frame collections.
        /// TempJob: Fastest allocation, auto-disposed at job completion.
        /// </summary>
        public static Allocator GetPerFrameAllocator() => Allocator.TempJob;
        
        /// <summary>
        /// Gets recommended allocator for persistent collections.
        /// Persistent: Long-lived, manual disposal required.
        /// </summary>
        public static Allocator GetPersistentAllocator() => Allocator.Persistent;
    }
    
    /// <summary>
    /// Epic 7.7.2: Struct-of-Arrays (SoA) layout for collision pairs.
    /// 
    /// SoA improves cache locality when processing specific fields:
    /// - Process all distances together (vectorizable)
    /// - Process all directions together (vectorizable)
    /// - Entities only accessed when collision confirmed
    /// 
    /// Compared to Array-of-Structs (AoS):
    ///   AoS: [E1,E2,D,Dir][E1,E2,D,Dir]... (interleaved)
    ///   SoA: [E1,E1,E1...][E2,E2,E2...][D,D,D...][Dir,Dir,Dir...] (grouped)
    /// 
    /// SoA allows SIMD to process 4-8 distances in parallel.
    /// </summary>
    public struct CollisionPairArrays : System.IDisposable
    {
        /// <summary>First entity in each collision pair</summary>
        public NativeList<Entity> EntityA;
        
        /// <summary>Second entity in each collision pair</summary>
        public NativeList<Entity> EntityB;
        
        /// <summary>Distance between entities (horizontal)</summary>
        public NativeList<float> Distances;
        
        /// <summary>Direction from A to B (normalized, horizontal)</summary>
        public NativeList<float3> Directions;
        
        /// <summary>Contact point (midpoint between entities)</summary>
        public NativeList<float3> ContactPoints;
        
        /// <summary>Impact speed (combined approach speed)</summary>
        public NativeList<float> ImpactSpeeds;
        
        /// <summary>Whether collision was processed (for deduplication)</summary>
        public NativeList<bool> Processed;
        
        /// <summary>Current number of pairs stored</summary>
        public int Length => EntityA.IsCreated ? EntityA.Length : 0;
        
        /// <summary>
        /// Creates a new CollisionPairArrays with pre-allocated capacity.
        /// </summary>
        /// <param name="capacity">Initial capacity for all arrays</param>
        /// <param name="allocator">Memory allocator to use</param>
        public CollisionPairArrays(int capacity, Allocator allocator)
        {
            EntityA = new NativeList<Entity>(capacity, allocator);
            EntityB = new NativeList<Entity>(capacity, allocator);
            Distances = new NativeList<float>(capacity, allocator);
            Directions = new NativeList<float3>(capacity, allocator);
            ContactPoints = new NativeList<float3>(capacity, allocator);
            ImpactSpeeds = new NativeList<float>(capacity, allocator);
            Processed = new NativeList<bool>(capacity, allocator);
        }
        
        /// <summary>
        /// Adds a collision pair to all arrays.
        /// </summary>
        public void Add(Entity entityA, Entity entityB, float distance, float3 direction, 
            float3 contactPoint, float impactSpeed)
        {
            EntityA.Add(entityA);
            EntityB.Add(entityB);
            Distances.Add(distance);
            Directions.Add(direction);
            ContactPoints.Add(contactPoint);
            ImpactSpeeds.Add(impactSpeed);
            Processed.Add(false);
        }
        
        /// <summary>
        /// Clears all arrays for reuse (capacity preserved).
        /// </summary>
        public void Clear()
        {
            if (EntityA.IsCreated) EntityA.Clear();
            if (EntityB.IsCreated) EntityB.Clear();
            if (Distances.IsCreated) Distances.Clear();
            if (Directions.IsCreated) Directions.Clear();
            if (ContactPoints.IsCreated) ContactPoints.Clear();
            if (ImpactSpeeds.IsCreated) ImpactSpeeds.Clear();
            if (Processed.IsCreated) Processed.Clear();
        }
        
        /// <summary>
        /// Disposes all arrays and releases memory.
        /// </summary>
        public void Dispose()
        {
            if (EntityA.IsCreated) EntityA.Dispose();
            if (EntityB.IsCreated) EntityB.Dispose();
            if (Distances.IsCreated) Distances.Dispose();
            if (Directions.IsCreated) Directions.Dispose();
            if (ContactPoints.IsCreated) ContactPoints.Dispose();
            if (ImpactSpeeds.IsCreated) ImpactSpeeds.Dispose();
            if (Processed.IsCreated) Processed.Dispose();
        }
    }
    
    /// <summary>
    /// Epic 7.7.2: Pre-sized player data for collision detection.
    /// 
    /// Uses fixed-size allocation to avoid per-frame reallocation.
    /// Capacity based on MaxPlayerCount constant.
    /// </summary>
    public struct PreallocatedPlayerData : System.IDisposable
    {
        /// <summary>Player entities</summary>
        public NativeList<Entity> Entities;
        
        /// <summary>Player positions (for distance calculations)</summary>
        public NativeList<float3> Positions;
        
        /// <summary>Player velocities (for approach speed)</summary>
        public NativeList<float3> Velocities;
        
        /// <summary>Collision radii</summary>
        public NativeList<float> Radii;
        
        /// <summary>Whether entity has Simulate component (can be modified)</summary>
        public NativeList<bool> HasSimulate;
        
        /// <summary>Current number of players stored</summary>
        public int Length => Entities.IsCreated ? Entities.Length : 0;
        
        /// <summary>
        /// Creates pre-allocated player data arrays.
        /// </summary>
        /// <param name="capacity">Maximum player capacity</param>
        /// <param name="allocator">Memory allocator</param>
        public PreallocatedPlayerData(int capacity, Allocator allocator)
        {
            Entities = new NativeList<Entity>(capacity, allocator);
            Positions = new NativeList<float3>(capacity, allocator);
            Velocities = new NativeList<float3>(capacity, allocator);
            Radii = new NativeList<float>(capacity, allocator);
            HasSimulate = new NativeList<bool>(capacity, allocator);
        }
        
        /// <summary>
        /// Adds a player to all arrays.
        /// </summary>
        public void Add(Entity entity, float3 position, float3 velocity, float radius, bool hasSimulate)
        {
            Entities.Add(entity);
            Positions.Add(position);
            Velocities.Add(velocity);
            Radii.Add(radius);
            HasSimulate.Add(hasSimulate);
        }
        
        /// <summary>
        /// Clears all arrays for reuse (capacity preserved).
        /// </summary>
        public void Clear()
        {
            if (Entities.IsCreated) Entities.Clear();
            if (Positions.IsCreated) Positions.Clear();
            if (Velocities.IsCreated) Velocities.Clear();
            if (Radii.IsCreated) Radii.Clear();
            if (HasSimulate.IsCreated) HasSimulate.Clear();
        }
        
        /// <summary>
        /// Disposes all arrays.
        /// </summary>
        public void Dispose()
        {
            if (Entities.IsCreated) Entities.Dispose();
            if (Positions.IsCreated) Positions.Dispose();
            if (Velocities.IsCreated) Velocities.Dispose();
            if (Radii.IsCreated) Radii.Dispose();
            if (HasSimulate.IsCreated) HasSimulate.Dispose();
        }
    }
}
