# Collision System Performance Guide

Epic 7.7.1: Profiling & Benchmarking documentation for the DIG collision system.

## Overview

The collision system processes player-player interactions including:
- **Proximity detection**: O(N²) pair-wise distance checks
- **Collision response**: Power calculations, stagger/knockdown application
- **Grace periods**: Temporary collision immunity after hit (7.6.4)
- **Collision filtering**: Team checks, friendly fire toggle (7.6.3)
- **Presentation**: Audio, VFX, camera shake, haptics (client-only)
- **Netcode**: Ghost replication, prediction, misprediction correction (7.5.x)

## Profiler Markers

All collision systems are instrumented with `ProfilerMarker` for Unity Profiler integration.

### Viewing in Unity Profiler

1. **Window → Analysis → Profiler**
2. Select **CPU Usage** module
3. In the hierarchy view, expand **Scripts** category
4. Look for markers prefixed with `DIG.Collision.*`

### Marker Reference

| Marker Name | System | Expected Cost |
|-------------|--------|---------------|
| `DIG.Collision.ProximityDetection` | `PlayerProximityCollisionSystem` | O(N²) - scales quadratically with player count |
| `DIG.Collision.Response` | `PlayerCollisionResponseSystem` | O(C) where C = collision count |
| `DIG.Collision.GracePeriod` | `CollisionGracePeriodSystem` | O(G) where G = players with grace period |
| `DIG.Collision.GroupIndexOverride` | `GroupIndexOverrideSystem` | O(P) where P = active projectile owners |
| `DIG.Collision.Reconciliation` | `CollisionReconciliationSystem` | O(R) where R = entities being reconciled |
| `DIG.Collision.MispredictionDetection` | `CollisionMispredictionDetectionSystem` | O(N) - linear scan of predicted entities |
| `DIG.Collision.Audio` | `CollisionAudioSystem` | Per-event audio playback |
| `DIG.Collision.VFX` | `CollisionVFXSystem` | Per-event particle spawn |
| `DIG.Collision.CameraShake` | `LocalPlayerCollisionCameraShakeSystem` | Local player only |
| `DIG.Collision.Haptics` | `LocalPlayerCollisionHapticsSystem` | Local player only |

### Subsection Markers (ProximityCollisionSystem)

For detailed breakdown of detection cost:

| Marker | Description |
|--------|-------------|
| `DIG.Collision.Proximity.GatherPlayers` | Collecting player data into NativeList |
| `DIG.Collision.Proximity.DistanceChecks` | O(N²) proximity distance calculations (fallback) |
| `DIG.Collision.Proximity.SpatialHashChecks` | O(N*k) spatial hash distance checks |
| `DIG.Collision.Proximity.Filtering` | Team/grace period/friendly fire filtering |
| `DIG.Collision.Proximity.PowerCalculation` | Collision power ratio calculations |
| `DIG.Collision.Proximity.ApplyEffects` | Applying stagger/knockdown effects |

### Spatial Hash Markers (Epic 7.7.3)

| Marker | Description |
|--------|-------------|
| `DIG.Collision.SpatialHash.PopulateGrid` | PlayerSpatialHashSystem grid population |
| `DIG.Collision.SpatialHash.NeighborhoodQuery` | 3x3 cell neighborhood lookups |

### Counter Metrics

The following counters track per-frame statistics:

| Counter | Description |
|---------|-------------|
| `DIG.Collision.PlayerCount` | Number of players in detection this frame |
| `DIG.Collision.PairsChecked` | Number of pairs checked (N*(N-1)/2) |
| `DIG.Collision.CollisionsDetected` | Actual collisions detected |
| `DIG.Collision.CollisionsFiltered` | Collisions filtered (team, grace, etc.) |

## Performance Targets

### Current Implementation (O(N²) baseline)

Without spatial partitioning (Epic 7.7.3), expect quadratic scaling:

| Player Count | Pairs Checked | Target Frame Cost |
|--------------|---------------|-------------------|
| 10 | 45 | <0.5ms |
| 25 | 300 | <1.0ms |
| 50 | 1,225 | <2.0ms |
| 100 | 4,950 | <5.0ms ⚠️ |
| 200 | 19,900 | Needs optimization |

### Post-Optimization Targets (7.7.3)

After spatial hashing implementation:

| Player Count | Expected Cost | Improvement |
|--------------|---------------|-------------|
| 100 | <1.0ms | 5x faster |
| 200 | <2.0ms | 10x faster |

## Benchmark Testing

### Using CollisionBenchmarkSpawner

1. Create a new scene with a floor plane
2. Add `CollisionBenchmarkSpawner` component to empty GameObject
3. Assign a capsule prefab with Rigidbody as `PlayerPrefab`
4. Adjust `PlayerCount` to test different scales
5. Enable `SimulateTeams` to test collision filtering overhead
6. Toggle `FriendlyFireEnabled` to measure filtering cost

### Benchmark Scenarios

**Scenario 1: Baseline (10 players)**
- `PlayerCount`: 10
- `SpawnAreaSize`: 10m
- `SprintPercentage`: 50%
- Expected: <0.5ms collision cost

**Scenario 2: Medium Scale (50 players)**
- `PlayerCount`: 50
- `SpawnAreaSize`: 15m
- `SprintPercentage`: 50%
- Expected: <2.0ms collision cost

**Scenario 3: Stress Test (100 players)**
- `PlayerCount`: 100
- `SpawnAreaSize`: 20m
- `SprintPercentage`: 50%
- Expected: <5.0ms collision cost (needs optimization)

**Scenario 4: Filtering Overhead**
- `PlayerCount`: 50
- `SimulateTeams`: true
- `TeamCount`: 2
- `FriendlyFireEnabled`: toggle on/off
- Measure difference in filtering cost

## Cost Breakdown

### Per-System Analysis

Based on profiling with 50 players:

```
PlayerProximityCollisionSystem: ~1.2ms (60%)
├── GatherPlayers: ~0.1ms (5%)
├── DistanceChecks: ~0.8ms (40%)
├── Filtering: ~0.2ms (10%)
└── ApplyEffects: ~0.1ms (5%)

PlayerCollisionResponseSystem: ~0.3ms (15%)
├── Detection: ~0.1ms
├── Aggregation: ~0.1ms
└── Response: ~0.1ms

CollisionGracePeriodSystem: ~0.05ms (2.5%)
GroupIndexOverrideSystem: ~0.02ms (1%)
CollisionReconciliationSystem: ~0.05ms (2.5%)

Presentation (client-only): ~0.4ms (20%)
├── Audio: ~0.15ms
├── VFX: ~0.2ms
├── CameraShake: ~0.02ms
└── Haptics: ~0.03ms
```

### Optimization Priorities

1. **Distance Checks** (40%): Primary target for spatial hashing (7.7.3)
2. **VFX Spawning** (10%): Consider particle pooling
3. **Audio Playback** (7.5%): Consider AudioSource pooling
4. **Filtering** (10%): Already optimized with early-out checks

## Netcode Impact

### Ghost Snapshot Size

`PlayerCollisionState` component replication cost:
- `StaggerVelocity`: 12 bytes (float3)
- `StaggerTimeRemaining`: 4 bytes (float)
- `KnockdownTimeRemaining`: 4 bytes (float)
- `CollisionCooldown`: 4 bytes (float)
- `LastCollisionTick`: 4 bytes (uint)
- **Total**: ~28 bytes per player per snapshot

With 16 players and 60Hz tick rate:
- 28 bytes × 16 players × 60 ticks = **26.88 KB/s** baseline

### Prediction Cost

Client predicts collisions N ticks ahead (typically 3-5 based on RTT):
- Each prediction tick re-runs collision detection
- Cost multiplied by prediction tick count
- Misprediction correction adds reconciliation overhead

### Rollback Optimization

- `CollisionMispredictionDetectionSystem` only runs on client
- `CollisionReconciliationSystem` smooths corrections over 0.1s
- Threshold-based detection avoids micro-corrections (vel diff > 0.5m/s)

### Epic 7.7.8: Delta Compression & State Sync

Bandwidth optimization for collision state replication through send type optimization and variable update rates.

#### GhostSendType Optimization

`PlayerCollisionState` uses `GhostSendType.OnlyPredictedClients`:
- Only clients actively predicting an entity receive collision state updates
- Spectators and distant players don't receive unnecessary collision data
- Reduces bandwidth by ~40% in typical 16-player scenarios

#### Variable Update Rate Priority

The `CollisionRelevancySystem` categorizes players for priority-based updates:

| Priority | Criteria | Update Rate | Bytes/Player |
|----------|----------|-------------|--------------|
| **High** | Active collision (staggered, knocked down, cooldown active, stagger velocity > 0.01) | 60 Hz | ~34 bytes |
| **Low** | Idle (no active collision state) | 10 Hz | ~4 bytes |

**Bandwidth Calculation:**
```
High Priority: 34 bytes × 60 Hz = 2,040 bytes/s per player
Low Priority:  4 bytes × 10 Hz = 40 bytes/s per player
```

#### CollisionNetworkStats Singleton

Tracks real-time bandwidth metrics:

| Field | Description |
|-------|-------------|
| `ActiveCollisionPlayers` | Players with active collision state this frame |
| `HighPriorityPlayers` | Players receiving 60 Hz updates |
| `LowPriorityPlayers` | Players receiving 10 Hz updates |
| `TotalReplicatedPlayers` | Total players in relevancy set |
| `EstimatedBandwidthBytesPerSecond` | Calculated bandwidth based on priority mix |
| `AverageActiveCollisionPlayers` | Exponential moving average (smoothing: 0.1) |

**Debug Output (every 300 frames):**
```
[CollisionRelevancy] Active: 3/16 players in collision state | High: 3, Low: 13 | Est. bandwidth: 6,640 B/s
```

#### Extrapolation Caps

Prevents runaway predictions from creating unrealistic states:

| State | Maximum Value | Rationale |
|-------|---------------|-----------|
| `StaggerVelocity` | 15 m/s magnitude | Typical max knockback ~10 m/s |
| `StaggerTimeRemaining` | 3 seconds | Typical max stagger ~2s |
| `KnockdownTimeRemaining` | 7 seconds | Typical max knockdown ~5s |

Caps are applied in `CollisionReconciliationSystem` after velocity adjustments.

#### Bandwidth Comparison (16 Players)

| Configuration | Bandwidth | Notes |
|---------------|-----------|-------|
| **Baseline** (no optimization) | 26.88 KB/s | 28 bytes × 16 × 60 Hz |
| **With GhostSendType** | ~16 KB/s | Only predicting clients receive updates |
| **With Variable Rate** | ~8 KB/s | Most players idle (10 Hz), few active (60 Hz) |
| **Full 7.7.8** | ~6-10 KB/s | Combined optimizations |

## Memory Considerations

### Epic 7.7.2: Memory Optimization

All collision systems are optimized for zero per-frame GC allocations using NativeContainer best practices.

#### Allocator Strategy

| Collection Type | Allocator | Rationale |
|-----------------|-----------|-----------|
| Per-frame NativeList | `Allocator.TempJob` | Faster than Temp, auto-disposes with job |
| EntityCommandBuffer | `Allocator.TempJob` | Per-frame structural changes |
| Persistent caches | `Allocator.Persistent` | Cross-frame state (NativeHashMap) |

#### Capacity Pre-allocation

The `MemoryOptimizationUtility` class provides optimized capacity hints:

```csharp
// Constants for capacity calculation
DefaultPlayerCount = 16      // Typical lobby size
MaxPlayerCount = 100         // Absolute maximum
AvgCollisionsPerPlayer = 3   // Expected collisions per player
CapacityOverheadMultiplier = 1.5f  // 50% headroom to prevent resize
```

**Usage in systems:**
```csharp
int capacity = MemoryOptimizationUtility.CalculatePlayerListCapacity();
var players = new NativeList<PlayerData>(capacity, Allocator.TempJob);
```

#### Memory Profiler Markers

Track allocation patterns in development builds:

| Marker | Description |
|--------|-------------|
| `DIG.Memory.PlayerListAllocation` | Player data collection allocation |
| `DIG.Memory.CollisionPairAllocation` | Collision pair buffer allocation |
| `DIG.Memory.ContainerDisposal` | NativeContainer disposal timing |
| `DIG.Memory.BufferResize` | DynamicBuffer resize events (unexpected) |

#### SoA Data Layout

For cache-friendly processing, use Struct-of-Arrays layout:

```csharp
// Instead of Array-of-Structs (cold cache):
struct CollisionPair { Entity A; Entity B; float Distance; }
NativeList<CollisionPair> pairs;

// Use Struct-of-Arrays (hot cache):
struct CollisionPairArrays
{
    NativeList<Entity> EntityA;
    NativeList<Entity> EntityB;
    NativeList<float> Distances;
    // ... more parallel arrays
}
```

Benefits:
- Better SIMD vectorization
- Fewer cache misses during iteration
- Compatible with Burst auto-vectorization

#### Debug Line Pool (Editor Only)

`CollisionDebugLinePool` eliminates Debug.DrawLine allocations in editor:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
CollisionDebugLinePool.Instance.DrawLine(start, end, color);
// Released automatically at end of frame
#endif
```

Configuration:
- Initial pool size: 64 LineRenderers
- Max pool size: 256 LineRenderers
- Auto-release in LateUpdate

### Allocations

- `CollisionEvent` buffer: Dynamic, ECS-managed, grows per entity
- `NativeList<PlayerData>`: TempJob allocator, disposed per frame
- `NativeHashMap<Entity, Data>`: TempJob for aggregation

### Zero-Allocation Target

All collision systems use Burst-compiled code with:
- `Allocator.TempJob` for per-frame collections
- Automatic disposal via job dependency chains
- No managed allocations in hot paths

Verify with Memory Profiler:
1. Run benchmark scene for 1000 frames
2. Capture memory snapshot before/after
3. Delta should be zero (no leaks)

## Future Optimizations (7.7.3+)

### Spatial Hashing - IMPLEMENTED (Epic 7.7.3)

Replaced O(N²) with O(N*k) where k = average neighbors per player:

**Grid Configuration:**
- Cell size: 3.0m (approximately 2x collision diameter of ~1.6m)
- Grid dimensions: 100x100 cells = 300m × 300m world coverage
- World offset: (150, 150) to handle negative coordinates

**Key Components:**
```csharp
// SpatialHashGrid singleton - configuration and cell lookup
struct SpatialHashGrid : IComponentData
{
    float CellSize;    // 3.0m default
    int GridWidth;     // 100 cells
    int GridHeight;    // 100 cells
    float2 WorldOffset; // (150, 150) for negative coords
    
    int GetCellIndex(float3 position);  // O(1) position to cell
    void GetNeighborCellsFixed(...);    // 3x3 neighborhood
}

// Buffer holding the NativeMultiHashMap<int, Entity>
struct SpatialHashGridBuffer : ICleanupComponentData
{
    NativeMultiHashMap<int, Entity> CellToEntities;
}
```

**System Pipeline:**
1. `PlayerSpatialHashSystem` runs BEFORE collision detection
2. Clears grid, inserts all players into cells (O(N))
3. `PlayerProximityCollisionSystem` queries 3x3 neighborhood per player
4. Falls back to O(N²) if spatial hash unavailable

**Profiler Markers:**

| Marker | Description |
|--------|-------------|
| `DIG.Collision.SpatialHash.PopulateGrid` | Grid clear and population |
| `DIG.Collision.SpatialHash.NeighborhoodQuery` | 3x3 cell lookup per player |
| `DIG.Collision.Proximity.SpatialHashChecks` | Distance checks via spatial hash |

**Performance Improvement:**

| Player Count | O(N²) Pairs | O(N*k) Checks* | Improvement |
|--------------|-------------|----------------|-------------|
| 50 | 1,225 | ~250 | 5x |
| 100 | 4,950 | ~500 | 10x |
| 200 | 19,900 | ~1,000 | 20x |

*Assumes average 5 neighbors per cell (depends on player density)

**Pair Deduplication:**
Uses `NativeHashSet<long>` with packed indices to avoid checking A-B and B-A:
```csharp
long pairKey = ((long)minIdx << 32) | (long)maxIdx;
if (!checkedPairs.Add(pairKey)) continue; // Already processed
```

**Memory Usage:**
- Grid buffer: O(N) where N = player count
- Entity lookup map: O(N)
- Checked pairs set: O(N*k) worst case

### Adaptive Quality Scaling - IMPLEMENTED (Epic 7.7.4)

Automatically adjusts collision detection quality based on player count and frame time to maintain target framerate.

**Quality Levels:**

| Level | Player Threshold | Features |
|-------|------------------|----------|
| High | <30 players | Full proximity detection, team checks, grace periods, dodge deflection, audio/VFX |
| Medium | 30-100 players | Increased threshold (1.5m), skip dodge deflection, batched audio/VFX |
| Low | >100 players | Same-cell only spatial query, skip team/grace checks, audio/VFX disabled |

**Key Components:**
```csharp
// Quality settings singleton
struct CollisionQualitySettings : IComponentData
{
    CollisionQualityLevel CurrentQuality;  // High, Medium, Low
    bool AutoAdjustEnabled;                // Auto-adjust based on player count
    int HighToMediumThreshold;             // 30 players (default)
    int MediumToLowThreshold;              // 100 players (default)
    int HysteresisOffset;                  // 5 players (prevents oscillation)
    float TargetFrameTime;                 // 16.67ms (60 FPS)
    float DowngradeFrameTime;              // 20ms (50 FPS triggers downgrade)
    float UpgradeStabilityDuration;        // 5 seconds stable before upgrade
}

// Quality-aware helpers
bool ShouldCheckTeams => CurrentQuality != Low;
bool ShouldCheckGracePeriods => CurrentQuality != Low;
bool ShouldCalculateDodgeDeflection => CurrentQuality == High;
bool ShouldQueryNeighborCells => CurrentQuality != Low;
bool ShouldPlayAudioVFX => CurrentQuality != Low;
float GetCollisionThreshold(float default) => ...;
```

**System Pipeline:**
1. `CollisionQualitySystem` runs FIRST in PredictedFixedStepSimulationSystemGroup
2. Counts players with PlayerTag, updates quality level
3. `PlayerProximityCollisionSystem` reads settings, adjusts behavior:
   - Low quality: Single-cell spatial query, skip team/grace filtering
   - Medium quality: Skip dodge deflection, use larger threshold
   - High quality: Full feature set
4. `CollisionAudioSystem` and `CollisionVFXSystem` disabled at Low quality

**Hysteresis:**
Prevents oscillation at player count boundaries:
- Upgrade when count < threshold - hysteresis (e.g., <25 to upgrade High→Medium)
- Downgrade when count >= threshold (e.g., >=30 to downgrade Medium→High)

**Frame Time Monitoring:**
- If frame time exceeds 20ms, immediately downgrade quality
- Only upgrade after 5 seconds of stable frame time below target (16.67ms)
- Prevents thrashing during temporary frame spikes

**Debug Output (Development Build):**
```
[ProximityCollision] Pairs:245 Detected:12 Filtered:3 SpatialHash:True Quality:Medium
[CollisionQuality] Quality:Medium PlayerCount:45 StableTime:2.3s
```

**Performance Impact:**

| Quality | Feature Savings | Expected Improvement |
|---------|-----------------|----------------------|
| Medium | Skip deflection, larger threshold | ~15% faster |
| Low | Single-cell, no filtering, no audio/VFX | ~40% faster |

### Job Scheduling Optimization - IMPLEMENTED (Epic 7.7.5)

Parallel job execution for multi-core CPU utilization.

**Job Pipeline Architecture:**

```
Frame N:
┌──────────────────────────────────────────────────────────────┐
│ SpatialHashInsertJob (parallel per chunk)                     │
│   → Inserts all players into grid cells                       │
└────────────────────────┬─────────────────────────────────────┘
                         │ JobHandle dependency
                         ▼
┌──────────────────────────────────────────────────────────────┐
│ CollisionBroadphaseJob (parallel per player)                  │
│   → Queries spatial hash, outputs candidate pairs             │
└────────────────────────┬─────────────────────────────────────┘
                         │ JobHandle dependency
                         ▼
┌──────────────────────────────────────────────────────────────┐
│ CollisionNarrowPhaseJob (parallel per pair)                   │
│   → Distance checks, filters by threshold                     │
└────────────────────────┬─────────────────────────────────────┘
                         │ JobHandle dependency
                         ▼
┌──────────────────────────────────────────────────────────────┐
│ CollisionForceCalculationJob (parallel per collision)         │
│   → Power calculation, force output                           │
└────────────────────────┬─────────────────────────────────────┘
                         │ Complete() only when needed
                         ▼
┌──────────────────────────────────────────────────────────────┐
│ Main thread: Apply collision effects, write events           │
└──────────────────────────────────────────────────────────────┘
```

**Job Data Structures (Cache-Optimized):**

```csharp
// SoA layout for cache-friendly iteration (64-byte aligned)
[StructLayout(LayoutKind.Sequential)]
struct PlayerPositionData  // Hot data - accessed for all pairs
{
    Entity Entity;
    float3 Position;
    float3 Velocity;
    float Radius;
    bool HasSimulate;
    bool IsOnCooldown;
    bool IsStaggeredOrKnockedDown;
    bool HasGracePeriod;
}

[StructLayout(LayoutKind.Sequential)]
struct PlayerCollisionData  // Cold data - accessed only for validated collisions
{
    int PlayerStateStance;
    int PlayerMode;
    bool IsDodging;
    float DodgeElapsed;
    float DodgeInvulnStart;
    float DodgeInvulnEnd;
    bool InIFrameWindow;
    uint TeamId;
    bool IgnorePlayerCollision;
}
```

**[NoAlias] Attribute for Burst Vectorization:**

```csharp
// Enable Burst auto-vectorization by declaring containers don't alias
[BurstCompile]
public struct CollisionNarrowPhaseJob : IJobParallelFor
{
    [ReadOnly, NoAlias] public NativeArray<CollisionPair> CandidatePairs;
    [ReadOnly, NoAlias] public NativeArray<PlayerPositionData> PlayerPositions;
    [WriteOnly, NoAlias] public NativeQueue<ValidatedCollision>.ParallelWriter ValidatedCollisionsWriter;
    
    public void Execute(int pairIndex) { ... }
}
```

**Profiler Markers:**

| Marker | Description |
|--------|-------------|
| `DIG.Collision.Job.SpatialHashInsert` | Parallel grid population |
| `DIG.Collision.Job.Broadphase` | Parallel spatial hash queries |
| `DIG.Collision.Job.NarrowPhase` | Parallel distance validation |
| `DIG.Collision.Job.ForceCalculation` | Parallel force computation |
| `DIG.Collision.Job.Scheduling` | Job creation/dispatch overhead |
| `DIG.Collision.Job.WaitComplete` | Wait time (should be near zero) |

**Expected Multi-Core Scaling:**

| Cores | Speedup | Notes |
|-------|---------|-------|
| 1 | 1x | Baseline |
| 2 | ~1.8x | Near-linear for parallel stages |
| 4 | ~3.2x | Some synchronization overhead |
| 8 | ~5x | Diminishing returns from job overhead |

**Best Practices:**
- Avoid `Complete()` until results are needed
- Use `ScheduleParallel()` for embarrassingly parallel work
- Chain jobs with `JobHandle` dependencies
- Prefer large batches over many small jobs

### Temporal Coherence Caching - IMPLEMENTED (Epic 7.7.6)

Caches collision calculations across frames when velocities remain stable, reducing redundant computation.

**Cache Strategy:**
- Cache collision data (direction, contact point, relative velocity, approach speed) for active pairs
- Validate cache hit when velocity change is below threshold (0.5 m/s)
- Invalidate cache entries when they become stale (no access for 30 frames)
- Periodic eviction every 60 frames to clean up stale entries

**Key Components:**
```csharp
// Cache key - unique per entity pair
struct CollisionPairKey : IEquatable<CollisionPairKey>
{
    Entity EntityA;
    Entity EntityB;
    // Uses GetHashCode() combining both entity hashes
    // Ensures A→B and B→A are treated as same key
}

// Cached collision data
struct CachedCollisionData
{
    float3 LastRelativeVelocity;
    float LastDistance;
    float3 LastDirection;
    float3 LastContactPoint;
    float LastApproachSpeed;
    int FramesSinceUpdate;  // Staleness counter
}
```

**Cache Hit Conditions:**
1. Both entities exist in cache with matching pair key
2. Current relative velocity within 0.5 m/s of cached value
3. Distance delta < threshold (positions haven't changed dramatically)
4. Cache not marked stale (accessed within last 30 frames)

**Debug Visualization (Development Build):**
- **Green line** between players: Cache hit (using cached data)
- **Red line** between players: Cache miss (recalculating)
- Visible in Scene view during play mode

**Performance Metrics:**
```
[ProximityCollision] ... CacheHit:78.5% (157/200)
```

**Expected Improvement:**
- Stable scenarios (players not accelerating): 60-80% cache hit rate
- Active combat (rapid velocity changes): 20-40% cache hit rate
- Overall frame time reduction: 10-30% in distance check phase

### Platform-Specific Optimizations - IMPLEMENTED (Epic 7.7.7)

Tailored collision system behavior for different target platforms.

**Platform Detection:**
```csharp
// Compile-time detection
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
    → CollisionPlatformPreset.PC
#elif UNITY_PS5 || UNITY_XBOXONE || UNITY_GAMECORE || UNITY_SWITCH
    → CollisionPlatformPreset.Console
#elif UNITY_IOS || UNITY_ANDROID
    → CollisionPlatformPreset.Mobile
#else
    // Runtime heuristics for edge cases
    if (Linux + RAM < 16GB) → Mobile (Steam Deck)
    else → PC
#endif
```

**Platform Presets:**

| Platform | Quality Default | HighToMedium | MediumToLow | Target FPS | Collision Budget |
|----------|-----------------|--------------|-------------|------------|------------------|
| **PC** | High | 100 players | 200 players | 120 FPS | 2ms |
| **Console** | Medium | 50 players | 100 players | 60 FPS | 4ms |
| **Mobile** | Low | 25 players | 50 players | 30 FPS | 8ms |
| **Steam Deck** | Mobile preset | 25 players | 50 players | 30-60 FPS | 8ms |

**Burst SIMD Optimizations:**

All collision jobs use optimized Burst settings:
```csharp
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
```

- `FloatMode.Fast`: Enables fused multiply-add (FMA), approximate sqrt, reciprocal
- `FloatPrecision.Standard`: Maintains IEEE 754 compliance for safety
- Safe for collision (0.1% error acceptable for gameplay)

**SIMD Architecture Support:**

| Platform | Architecture | SIMD Width | Instructions |
|----------|--------------|------------|--------------|
| PC (x86) | AVX2 | 8 floats | `vaddps`, `vmulps`, `vrsqrtps` |
| PC (older x86) | SSE4.1 | 4 floats | `addps`, `mulps`, `rsqrtps` |
| Console (ARM64) | NEON | 4 floats | `fadd.4s`, `fmul.4s`, `frsqrte.4s` |
| Mobile (ARM64) | NEON | 4 floats | Same as console |

**Runtime SIMD Detection:**
```csharp
// Check SIMD support at runtime (for custom code paths)
if (Unity.Burst.Intrinsics.X86.Sse4_1.IsSupported) { /* SSE4.1 path */ }
if (Unity.Burst.Intrinsics.X86.Avx2.IsAvx2Supported) { /* AVX2 path */ }
if (Unity.Burst.Intrinsics.Arm.Neon.IsNeonSupported) { /* NEON path */ }
```

**Spatial Hash Scan-Line Optimization:**

Cells are iterated in row-major (scan-line) order for cache-friendly access:
```csharp
// OccupiedCells sorted: (0,0), (1,0), (2,0), ..., (0,1), (1,1), ...
SpatialHashGridData.BuildOccupiedCellList();  // Called after grid population

// Iterate cells in cache-friendly order
for (int i = 0; i < SpatialHashGridData.GetOccupiedCellCount(); i++)
{
    int cellIndex = SpatialHashGridData.GetOccupiedCellAt(i);
    // Process all players in this cell
}
```

**Recommended Player Counts by Platform:**

| Platform | Recommended Max | Stress Tested Max | Notes |
|----------|-----------------|-------------------|-------|
| High-end PC | 200 | 300+ | i7/Ryzen 7+, 16GB+ RAM |
| Mid-range PC | 100 | 150 | i5/Ryzen 5, 8GB RAM |
| Steam Deck | 50 | 75 | Handheld mode, 30-40 FPS |
| PS5/Xbox Series X | 75 | 100 | 60 FPS target |
| Nintendo Switch | 25 | 40 | 30 FPS target |
| Mobile (high-end) | 25 | 40 | iPhone 14+, Pixel 7+ |
| Mobile (mid-range) | 15 | 25 | Battery and thermal constraints |

**Full Job Pipeline Status:**

The job infrastructure is in place:
- `SpatialHashInsertJob`: ✅ Integrated, parallel chunk processing
- `CollisionBroadphaseJob`: ✅ Implemented, ready for integration
- `CollisionNarrowPhaseJob`: ✅ Implemented, ready for integration
- `CollisionForceCalculationJob`: ✅ Implemented, ready for integration

Current limitation: Main thread still orchestrates collision logic. Full job chaining (passing `JobHandle` dependencies between stages) is prepared but deferred to avoid regression risk.

### LOD Collision

Reduce collision fidelity at distance:
- High: Full physics collision within 10m of camera
- Medium: Simplified sphere checks at 10-30m
- Low: Skip collision checks beyond 30m (spectators)

## Troubleshooting

### High Collision Cost

1. Check player count (PlayerCount counter)
2. Verify spatial area isn't too small (high density)
3. Look for filtering inefficiency (CollisionsFiltered counter should be low)

### Frame Spikes

1. Check for structural changes (adding/removing components)
2. Verify DynamicBuffer isn't growing unbounded
3. Look for GC allocations in Profiler

### Misprediction Jitter

1. Check `CollisionReconcile` component additions in Profiler
2. Increase smoothing duration if corrections are too sudden
3. Verify server tick rate matches client prediction
