# EPIC 15.23: Physics Performance Optimization for Mass AI Enemies

**Status:** Implementation Complete (Pending QA Verification)
**Priority:** Critical (Performance)
**Dependencies:**
- Unity Physics (`com.unity.physics`)
- Unity NetCode (`com.unity.netcode`)
- `DIG.AI` (Enemy AI Framework)
- `DIG.Aggro` (Threat/Aggro System)
- `DIG.Swarm` (Reference pattern for spatial hash separation)

**Feature:** Optimize physics pipeline to support 500-4000+ AI enemies at target framerate by eliminating O(n²) creature-creature collision, adding spatial-hash enemy separation, making solver iterations configurable, and stripping unnecessary physics from client-side enemy ghosts.

---

## Problem Statement

Profiling with 500 enemies shows physics consuming ~48ms/frame (ServerWorld ~25ms + ClientWorld ~23ms). The root cause is `CreatureCollidesWith` including the `Creature` collision layer bit, generating O(n²) creature-creature contact pairs in the physics solver (~125K pairs for 500 enemies, ~8M for 4000). No gameplay system consumes creature-creature collision events — this work is entirely wasted.

AI behavior systems (Burst-compiled `SystemAPI.Query` foreach) are NOT the bottleneck — they don't even appear as profiler line items.

---

## Implementation Plan

### Phase 1A: Remove Creature-Creature Physics Collision

Eliminates O(n²) contact pair generation between enemy physics bodies.

- [x] **Update `CollisionLayers.CreatureCollidesWith` constant**
    - Remove `Creature` bit from the mask
    - File: `Assets/Scripts/Player/Components/CollisionLayers.cs`

- [x] **Create `EnemyCollisionFilterSystem`**
    - Runtime enforcement: sets correct `BelongsTo` and `CollidesWith` on enemy `PhysicsCollider` blobs at spawn
    - Follows `GroupIndexOverrideSystem` pattern for `SetCollisionFilter`
    - `ISystem`, `[BurstCompile]`, `InitializationSystemGroup`, `ServerSimulation | LocalSimulation`
    - Includes `EnemyCollisionFilterEnforced` tag component to track processed entities
    - File: `Assets/Scripts/AI/Systems/EnemyCollisionFilterSystem.cs`

- [x] **Create `AIProfilerMarkers`**
    - Profiler markers for separation and collision filter systems
    - Follows `SwarmProfilerMarkers` pattern
    - File: `Assets/Scripts/AI/Profiling/AIProfilerMarkers.cs`

### Phase 1B: Enemy Separation System (Prevents Stacking)

Replaces physics-based creature-creature collision with an O(n) spatial-hash separation system. Modeled after `SwarmSeparationSystem`.

- [x] **Create `EnemySeparationConfig` component**
    - Fields: `SeparationRadius`, `SeparationWeight`, `MaxSeparationSpeed`, `FrameInterval`
    - File: `Assets/Scripts/AI/Components/EnemySeparationConfig.cs`

- [x] **Create `EnemySeparationConfigAuthoring`**
    - MonoBehaviour + Baker for designer-tunable separation parameters
    - Inspector: `[Range]`, `[Tooltip]`, `[Header]` attributes
    - File: `Assets/Scripts/AI/Authoring/EnemySeparationConfigAuthoring.cs`

- [x] **Create `EnemySeparationSystem`**
    - 2-phase Burst pipeline: `BuildSpatialHashJob` (IJobParallelFor) + `SeparationJob` (IJobEntity)
    - Spatial hash with 3x3 neighborhood lookup, linear falloff, horizontal-only separation
    - Runs every Nth frame (configurable FrameInterval, default 2)
    - `SimulationSystemGroup`, after all AI movement systems, before `CombatResolutionSystem`
    - File: `Assets/Scripts/AI/Systems/EnemySeparationSystem.cs`

### Phase 2: Configurable Solver Iterations

Exposes physics solver settings as a developer-facing Inspector component.

- [x] **Create `PhysicsConfigAuthoring`**
    - `PhysicsConfig` ECS component + `PhysicsConfigAuthoring` MonoBehaviour
    - Fields: `SolverIterationCount` (Range 1-8), `IncrementalDynamicBroadphase`, `IncrementalStaticBroadphase`
    - File: `Assets/Scripts/Core/Physics/PhysicsConfigAuthoring.cs`

- [x] **Modify `PhysicsOptimizationSystem`**
    - Read from `PhysicsConfig` singleton if present; apply to `PhysicsStep`
    - Fallback to current defaults when no config exists
    - File: `Assets/Scripts/Core/Physics/PhysicsOptimizationSystem.cs`

### Phase 3: Strip Physics from Client Enemy Ghosts

Eliminates solver contacts for enemy ghosts on remote clients while preserving broadphase presence for raycasts.

- [x] **Create `ClientEnemyPhysicsOptimizationSystem`**
    - Sets `CollidesWith = PlayerProjectile` on client enemy ghost `PhysicsCollider` blobs
    - Eliminates ALL solver contacts while preserving raycast/projectile hit detection
    - Query: `PhysicsCollider + ShowHealthBarTag + WithNone<GhostOwnerIsLocal> + WithNone<ClientPhysicsOptimized>`
    - `ClientSimulation` only
    - Includes `ClientPhysicsOptimized` tag component
    - File: `Assets/Scripts/Core/Physics/ClientEnemyPhysicsOptimizationSystem.cs`

---

## Expected Performance Impact

| Metric | Before | After Phase 1 | After All Phases |
|--------|--------|---------------|-----------------|
| Physics (500 enemies) | ~48ms | <10ms | <5ms |
| Physics (4000 enemies) | Unplayable | ~30ms | <15ms |
| Contact pairs (500) | ~125,000 | ~500 (player only) | ~500 |
| Contact pairs (4000) | ~8,000,000 | ~4,000 (player only) | ~4,000 |
| Separation cost | N/A | <1ms (O(n) spatial hash) | <1ms |

---

## Verification Checklist

### Phase 1
- [x] Profiler: `Physics.StepWorld` drops from ~48ms to <10ms with 500 enemies
- [x] Enemies do NOT stack on each other (separation system working)
- [x] Enemies still chase, attack, leash, and return home correctly
- [x] Hitscan and projectiles still hit enemies
- [x] Health bars still display
- [x] Enemy death still works (Disabled component applied)

### Phase 2
- [x] `PhysicsConfigAuthoring` visible in Inspector when placed on subscene GameObject
- [x] Changing SolverIterationCount at edit time affects runtime physics
- [x] Player movement/wall collision still feels solid at SolverIterationCount=2

### Phase 3
- [x] Remote client: enemies render and move correctly
- [x] Remote client: weapon hits register on enemies
- [x] Remote client: `Physics.StepWorld` time dramatically reduced
- [x] No console errors about PhysicsCollider access
