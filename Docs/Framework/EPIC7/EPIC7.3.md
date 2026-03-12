### Epic 7.3: Collision Response & Push Mechanics ✅ COMPLETE
**Priority**: HIGH  
**Goal**: Implement proper player-vs-player collision response and push forces with high-performance Burst-compiled architecture

**IMPORTANT: Leverage Unity's Built-In Systems**
Unity Physics and NetCode for Entities already provide:
- ✅ **Burst-compiled collision detection** (`BuildPhysicsWorld`, `StepPhysicsWorld`)
- ✅ **Optimized BVH spatial partitioning** (automatic, no custom code needed)
- ✅ **Parallel collision processing** (`ICollisionEventsJob` is parallel by default)
- ✅ **Deterministic physics** (IEEE-754 floating point, cross-platform)
- ✅ **Rollback/prediction** (NetCode for Entities handles snapshots automatically)
- ✅ **SIMD auto-vectorization** (Unity.Mathematics + Burst compiler)
- ✅ **Contact manifold generation** (contact points, normals, penetration depth)
- ✅ **Collision filtering** (`CollisionFilter` in `PhysicsCollider`)

**What We Actually Need to Implement**:
- Custom push force logic (Unity provides contacts, we apply gameplay forces)
- Stance-based collision modifiers (prone, crouch, sprint)
- Collision audio/VFX event generation
- Game-specific collision profiles and behaviors

**Sub-Epic 7.3.1: Unity Physics Integration (Two-Phase Architecture)** ✅ COMPLETE
**Goal**: Use Unity Physics for collision detection, custom gameplay logic for response
**Design Notes**:
- Unity Physics handles broadphase/narrowphase detection (Burst-optimized, BVH-accelerated)
- Collision events provide entity pairs + contact data, but NOT gameplay state (stance, velocity intent)
- Response logic requires full component access for asymmetric outcomes (7.3.5), directional bonuses (7.3.6), state changes (7.3.8, 7.4.1)
- Architecture: Detection Job → Collision Pair Buffer → Response System (IJobEntity with component access)

**Implementation Summary (Dec 11, 2025)**:
- Created `PlayerCollisionPair.cs` - collision pair data structure with physics and gameplay metrics
- Updated `PlayerCollisionState.cs` - added stagger/knockdown fields for 7.3.5-7.4.1
- Updated `PlayerCollisionSettings.cs` - added separation, stagger, directional, and knockdown settings
- Updated `CollisionEvent.cs` - added HitDirection, ImpactSpeed, PowerRatio for audio/VFX differentiation
- Refactored `PlayerCollisionResponseSystem.cs` - Two-Phase Architecture:
  - Phase 1: `PlayerCollisionDetectionJob : ICollisionEventsJob` outputs `NativeList<PlayerCollisionPair>`
  - Phase 2: `PlayerCollisionResponseJob : IJob` with full component access for gameplay logic
- Updated `PlayerMovementState` enum - added Staggered (11), Knockdown (12), Tackling (13)

**Tasks**:
- [X] **PHASE 1 - DETECTION**: Use `ICollisionEventsJob` for player-player collision detection
  - [X] Subscribe to `SimulationSingleton.CollisionEvents` (Unity Physics provides this)
  - [X] Unity handles AABB broadphase, narrow phase, and contact generation automatically
  - [X] Filter for player-player collisions only (check both entities have `PlayerCollisionState`)
- [X] Create `PlayerCollisionDetectionJob : ICollisionEventsJob` with `[BurstCompile]`
  - [X] Output collision pairs to `NativeList<PlayerCollisionPair>` (not final response)
  - [X] `PlayerCollisionPair` struct: `EntityA`, `EntityB`, `ContactPoint`, `ContactNormal`, `PenetrationDepth`, `EventTick`
  - [X] Added gameplay metrics: `ImpactSpeed`, `ImpactForce`, `Overlap`, `VelocityA/B`, `PositionA/B`
  - [X] This job does NOT apply forces or state changes (insufficient component access)
- [X] **PHASE 2 - RESPONSE**: Create `PlayerCollisionResponseSystem` as `ISystem` with `IJob`
  - [X] Consumes `NativeList<PlayerCollisionPair>` from detection phase
  - [X] Full component access: `PlayerState`, `PhysicsVelocity`, `LocalTransform`, `PlayerCollisionSettings`
  - [X] Calculates asymmetric outcomes per 7.3.5 (power ratio, stagger distribution)
  - [X] Applies directional bonuses per 7.3.6 (facing dot product)
  - [X] Writes state changes: `PlayerMovementState`, `PlayerCollisionState`, `CollisionEventBuffer`
- [X] Add collision layer filtering via `CollisionFilter` (in `PhysicsCollider`)
  - [X] Created `CollisionLayers.cs` with layer constants (Player, Environment, Hazards, Ship, Creature, etc.)
  - [X] Updated `CharacterControllerAuthoring` to use `CollisionLayers.Player` and `CollisionLayers.PlayerCollidesWith`
  - [X] Player layer collides with: Player, Environment, Hazards, Ship, Creature, Default
- [ ] Profile: Detection <0.3ms, Response <0.5ms for 50 players
- [ ] Add debug visualization using `PhysicsDebugDisplay` + custom gizmos for collision pairs

**Sub-Epic 7.3.2: Leverage Unity Physics BVH (Built-In Spatial Partitioning)**
**Status**: ✅ COMPLETE
**Tasks**:
- [X] **USE UNITY'S BVH**: `PhysicsWorld.CollisionWorld` already uses optimized Bounding Volume Hierarchy
  - [X] Unity's BVH is production-proven, Burst-compiled, and O(log n) queries
  - [X] No custom spatial partitioning needed - Unity handles this automatically
  - [X] `PlayerCollisionDetectionJob` (ICollisionEventsJob) uses Unity's BVH internally
- [X] **OPTIONAL**: Created `CollisionSpatialQueryUtility.cs` for custom queries using BVH
  - [X] `OverlapBox()` - AABB query using Unity's BVH broadphase
  - [X] `OverlapSphere()` - Radius query using Unity's CalculateDistance
  - [X] `FindClosest()` - Nearest neighbor query
  - [X] `CountInRadius()` - Density check without allocating hit list
- [X] Add visualization using `CollisionDebugVisualizer.cs` (custom gizmo-based)
  - [X] Color-coded contact points (green=gentle, yellow=medium, red=heavy)
  - [X] Contact normal arrows
  - [X] Configurable via `PlayerCollisionSettings.DebugVisualizationEnabled`
- [X] Added debug settings to `PlayerCollisionSettings`:
  - [X] `DebugVisualizationEnabled`, `DebugGizmoDuration`, `DebugContactPointRadius`, `DebugNormalArrowLength`
- [ ] Profile: Unity's BVH already scales to 1000+ bodies efficiently
- [ ] Test with 100+ players: verify Unity's collision world handles it (it will)

**Sub-Epic 7.3.3: Parallel-Safe Collision Response Architecture**
**Status**: ✅ COMPLETE
**Goal**: Process collision responses in parallel while safely writing to multiple components
**Design Notes**:
- Each player can be involved in multiple collisions per frame (must aggregate)
- Response writes to: `PhysicsVelocity`, `PlayerState.MovementState`, `PlayerCollisionState`, `CollisionEventBuffer`
- Race condition risk: two threads processing same player from different collision pairs
- Solution: Per-entity aggregation with atomic accumulation, then single-write pass

**Tasks**:
- [X] **DETECTION PHASE (Parallel)**: `ICollisionEventsJob` processes all collision events in parallel
  - [X] Unity schedules with optimal batch sizes (built-in parallelization)
  - [X] Output: unsorted `NativeList<PlayerCollisionPair>` (append-only, thread-safe)
- [X] **AGGREGATION PHASE**: Sort collision pairs by entity, identify "dominant" collision per player
  - [X] Created `CollisionAggregationJob : IJob` for single-threaded aggregation
  - [X] Created `AggregatedCollisionData` struct to hold per-entity collision summary
  - [X] For each player, find highest-impact collision (max `impactSpeed * powerRatio`)
  - [X] Only dominant collision triggers stagger/knockdown (prevents stagger stacking)
  - [X] All collisions contribute to push forces (additive separation via `CumulativePushDirection`)
- [X] **RESPONSE PHASE (Per-Entity Parallel)**: `IJobEntity` processes each player once
  - [X] Use `ScheduleParallel()` - safe because each entity processed by one thread
  - [X] Read aggregated collision data from `NativeHashMap<Entity, AggregatedCollisionData>`
  - [X] Write to components: `LocalTransform`, `PlayerCollisionState`
  - [X] Append to `CollisionEventBuffer` (DynamicBuffer, per-entity = safe)
- [X] **STATE CHANGE SAFETY**: Use `EnabledRefRW<T>` for tag components
  - [X] Created `Staggered` enableable tag component (IEnableableComponent)
  - [X] Created `KnockedDown` enableable tag component (IEnableableComponent)
  - [X] Response job uses `EnabledRefRW<Staggered>` and `EnabledRefRW<KnockedDown>`
  - [X] No structural changes needed - tags enabled/disabled atomically
- [ ] Profile: Aggregation <0.1ms, Response <0.4ms for 50 players
- [ ] Verify scaling: 2x players → <2x cost (sub-linear due to spatial locality)

**Sub-Epic 7.3.4: Physical Separation & Collision Data Output**
**Status**: ✅ COMPLETE
**Goal**: Prevent player overlap with minimal separation force; output collision metrics for gameplay logic
**Design Notes**:
- This sub-epic handles **physics separation only** (prevent clipping)
- Gameplay outcomes (stagger, knockback, knockdown) are handled by 7.3.5
- Separation is symmetric (equal and opposite); gameplay outcomes are asymmetric
- Output collision metrics (impact speed, overlap, contact normal) for 7.3.5 to consume

**Tasks**:
- [X] Create `PlayerSeparationSystem.cs` with `PlayerSeparationJob` as dedicated separation system
  - [X] Runs before PlayerCollisionResponseSystem
  - [X] Uses ICollisionEventsJob to collect separation data
  - [X] Uses IJobEntity with ScheduleParallel for per-entity application
- [X] **SEPARATION FORCE (Symmetric)**:
  - [X] Calculate separation vector: `direction = normalize(ourPos - theirPos)`
  - [X] Calculate overlap: `overlap = combinedRadius - distance` (only if overlap > 0)
  - [X] Compute separation impulse: `impulse = direction * overlap * separationStrength`
  - [X] Apply to `PhysicsVelocity.Linear` (both players pushed apart equally)
  - [X] Clamp to `maxSeparationSpeed` to prevent explosive pops
  - [X] Small position correction for severe overlaps (> 0.1m)
- [X] **COLLISION METRICS OUTPUT** (for 7.3.5 gameplay logic):
  - [X] Calculate `impactSpeed = length(relativeVelocity)` (approach speed) - in detection job
  - [X] Calculate `impactForce = impactSpeed * combinedMass` (for stagger threshold) - in detection job
  - [X] Output to `PlayerCollisionPair`: `ImpactSpeed`, `ImpactForce`, `Overlap`, `ContactNormal`
  - [X] These metrics consumed by 7.3.5's power calculation
- [X] **DO NOT** apply stance multipliers here (moved to 7.3.5 power calculation)
- [X] **DO NOT** trigger stagger or state changes here (7.3.5/7.3.8 responsibility)
- [X] Settings already exist: `SeparationStrength` (default 10.0), `MaxSeparationSpeed` (default 3.0 m/s)
- [ ] Profile: <0.05ms for 50 players (minimal computation per pair)
- [ ] Test: overlapping players smoothly separate without visible pop
- [ ] Test: separation doesn't trigger stagger (that's 7.3.5's job based on impact speed)

**Sub-Epic 7.3.5: Asymmetric Stagger (Mass + Velocity + Stance)**
**Status**: ✅ COMPLETE
**Goal**: Make collision outcomes asymmetric based on "collision power" - heavier/faster/braced players stagger less
**Design Notes**: 
- AAA games don't use pure physics momentum conservation for player collisions
- Instead, they calculate "who wins" based on gameplay factors and apply asymmetric outcomes
- This integrates with Sub-Epic 7.3.8's stagger system

**Tasks**:
- [X] Add `EffectiveMass` field to `PlayerCollisionSettings` (base mass: 80kg)
- [X] Calculate "collision power" for each player in `CollisionAggregationJob`:
  ```
  power = effectiveMass * horizontalSpeed * stanceMultiplier * movementMultiplier
  ```
  - [X] `stanceMultiplier`: Standing=1.0, Crouching=1.3 (lower CoM), Prone=0.5 (can be stepped over)
  - [X] `movementMultiplier`: Sprinting=1.5, Running=1.0, Walking=0.8, Idle=0.6
- [X] Calculate power ratio: `myRatio = myPower / (myPower + theirPower)`
- [X] Distribute stagger duration asymmetrically:
  - [X] Use `math.lerp(MinStaggerDuration, MaxStaggerDuration, 1 - powerRatio)` for scaling
  - [X] Lower power ratio = longer stagger duration
  - [X] Extreme advantage (powerAdvantage >= 0.2, ratio >= 0.7): winner gets NO stagger
- [X] Distribute knockback velocity asymmetrically:
  - [X] Winner knocked back less: `knockbackVel *= (1 - powerRatio)`
  - [X] Loser knocked back more: multiplier approaches 1.0 as ratio approaches 0
- [X] Add `GetStanceMultiplier()` and `GetMovementMultiplier()` helper methods (Burst-compiled)
  - [X] Created `CollisionPowerUtility.cs` with static Burst-compiled methods
  - [X] `CalculatePower()`, `CalculatePowerRatio()`, `CalculateStaggerDurationMultiplier()`
  - [X] `CalculateKnockbackMultiplier()`, `ShouldTriggerKnockdown()`, `ShouldTriggerStagger()`
- [X] Add "knockdown threshold" - if power ratio < 0.2, loser transitions to Knockdown state
  - [X] `KnockdownPowerThreshold` = 0.8 (loser ratio < 0.2 triggers knockdown)
- [ ] Profile: ensure power calculation adds <0.02ms overhead
- [ ] Test: sprinting player vs idle player → idle player staggers longer, sprinter barely affected
- [ ] Test: crouching player vs running player → crouching player resists better due to stance bonus

**Sub-Epic 7.3.6: Directional Collision Bonuses**
**Status**: ✅ COMPLETE
**Goal**: Facing direction affects collision outcome - bracing into collision reduces stagger
**Design Notes**:
- Players facing INTO a collision are "braced" and take less stagger
- Side hits are neutral
- Back hits are vulnerable and take more stagger
- This encourages tactical positioning and awareness

**Tasks**:
- [X] In `CollisionAggregationJob`, get player facing direction from `LocalTransform.Rotation`
  - [X] `CalculateDirectionalBonus()` method handles all directional logic
- [X] Calculate facing dot product: `facingDot = dot(myForward, directionToOther)`
  - [X] `facingDot >= 0.5`: Facing collision (braced) → stagger multiplier = 0.6
  - [X] `facingDot > -0.5`: Side hit (neutral) → stagger multiplier = 1.0
  - [X] `facingDot <= -0.5`: Back hit (vulnerable) → stagger multiplier = 1.4
- [X] Apply directional multiplier to both stagger duration and knockback velocity
  - [X] `DominantDirectionalMultiplier` applied to duration in PlayerCollisionResponseJob
- [X] Add `DirectionalBonusEnabled` toggle to `PlayerCollisionSettings` (for tuning/debug)
- [X] Add directional multiplier values to `PlayerCollisionSettings`:
  - [X] `BracedStaggerMultiplier` (default 0.6)
  - [X] `SideHitStaggerMultiplier` (default 1.0)
  - [X] `BackHitStaggerMultiplier` (default 1.4)
  - [X] `BracedDotThreshold` (default 0.5)
  - [X] `BackHitDotThreshold` (default -0.5)
- [X] Write directional hit type to `CollisionEvent` buffer for audio/VFX differentiation
  - [X] Added `HitDirection` field (0=braced, 1=side, 2=back, 3=evaded)
  - [X] Added `HitDirectionType` static class with constants in PlayerCollisionPair.cs
- [X] Write collision events for BOTH players (aggregation processes EntityA and EntityB)
- [ ] Profile: ensure directional calculation adds <0.01ms overhead
- [ ] Test: player facing collision takes 40% less stagger than player hit from behind
- [ ] Test: audio/VFX system can play different sounds for front vs back hits

**Sub-Epic 7.3.7: Collision Event Buffering**
**Status**: ✅ COMPLETE
**Goal**: Provide collision event data to audio/VFX consumer systems without structural changes
**Design Notes**:
- Events written during collision response phase (per-entity, safe for parallel)
- FIFO eviction when buffer exceeds 8 events (prevents unbounded growth)
- Clear system runs at end of PresentationSystemGroup after consumers read events

**Tasks**:
- [X] Create `CollisionEventBuffer` as `DynamicBuffer<CollisionEvent>`:
  - [X] Fields: `OtherEntity`, `ContactPoint`, `ContactNormal`, `ImpactForce`, `EventTick`, `HitDirection`
  - [X] Additional fields: `ImpactSpeed`, `PowerRatio`, `TriggeredStagger`, `TriggeredKnockdown`
- [X] Write collision events during response phase (read by audio/VFX systems)
  - [X] Events written in `PlayerCollisionResponseJob` after stagger/knockdown logic
- [X] Limit buffer size to 8 events per player per frame (oldest evicted)
  - [X] FIFO eviction: `if (eventBuffer.Length >= 8) eventBuffer.RemoveAt(0);`
- [X] Add `[InternalBufferCapacity(8)]` attribute for cache efficiency
- [X] Create `CollisionEventClearSystem` to clear buffer each frame after consumers process
  - [X] Runs in `PresentationSystemGroup` with `OrderLast = true`
  - [X] Uses `IJobEntity` with `ScheduleParallel` for per-entity buffer clearing
- [ ] Profile: ensure buffer operations add <0.1ms overhead
- [ ] Test: verify audio/VFX systems can read events without structural changes

**Sub-Epic 7.3.8: Stagger State for Collision Response** ✅ COMPLETE
**Tasks**:
- [X] Add `Staggered` value to `PlayerMovementState` enum
- [X] Add `StaggerDuration` and `StaggerVelocity` fields to `PlayerCollisionState`
- [X] In `PlayerCollisionResponseSystem`: set `MovementState = Staggered` on high-impact collisions
  - [X] Trigger stagger when `impactSpeed > staggerThreshold` (configurable, ~3 m/s)
  - [X] Set stagger duration based on impact force (0.15-0.4s range)
  - [X] Apply knockback velocity in pushback direction
- [X] In `PlayerMovementSystem`: skip input processing when `MovementState == Staggered`
  - [X] Apply friction/deceleration to stagger velocity
  - [X] Allow stagger velocity to move player (knockback effect)
- [X] In `PlayerStateSystem`: handle stagger exit transition
  - [X] Decrement stagger timer each frame
  - [X] Transition to `Idle` or `Walking` when timer expires
  - [X] Clear `StaggerVelocity` on exit
- [X] Add stagger animation trigger (see Sub-Epic 7.3.9)
- [ ] Profile: ensure stagger logic adds <0.05ms overhead
- [ ] Test: high-speed collision causes visible knockback, low-speed allows sliding past

**Sub-Epic 7.3.9: Stagger Animation System** ✅ COMPLETE
**Tasks**:
- [X] Create `StaggerAnimatorBridge.cs` MonoBehaviour:
  - [X] Implement `IPlayerAnimationBridge` interface
  - [X] Add `TriggerStagger(float impactSpeed)` method - triggers stagger animation with intensity
  - [X] Add `EndStagger()` method - ends stagger and resets animator state
  - [X] Add animator parameter names (StaggerTrigger, IsStaggered, StaggerIntensity)
  - [X] Cache animator parameter hashes for performance
  - [X] Add debug logging option
- [X] Create `RemotePlayerStaggerAnimationSystem.cs`:
  - [X] Run in `PresentationSystemGroup` (client-side only)
  - [X] Query entities WITHOUT `GhostOwnerIsLocal` (remote players)
  - [X] Detect `MovementState` transition TO `Staggered`
  - [X] Detect `MovementState` transition FROM `Staggered`
  - [X] Use `GhostPresentationGameObjectSystem` to get presentation GameObject
  - [X] Call `StaggerAnimatorBridge.TriggerStagger()` / `EndStagger()`
  - [X] Track per-entity state to detect transitions
  - [X] Clean up tracking for destroyed entities
- [X] Create `LocalPlayerStaggerAnimationSystem.cs`:
  - [X] Run in `PresentationSystemGroup` (client-side only)
  - [X] Query entities WITH `GhostOwnerIsLocal` (local player)
  - [X] Same transition detection as remote system
  - [X] For local player: only trigger on actual state change, not prediction rollbacks
- [X] No separate authoring needed - stagger settings already in `PlayerCollisionSettings` singleton
- [ ] Add StaggerAnimatorBridge to player prefab (manual Unity Editor step)
- [ ] Configure animator with stagger animation clips (manual Unity Editor step)
- [ ] Test: stagger animation plays on high-impact collision
- [ ] Test: animation stops when stagger ends