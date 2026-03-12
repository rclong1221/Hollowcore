# EPIC 16.2: Swarm Entity Framework

**Status:** Planning
**Priority:** High (Core Infrastructure)
**Dependencies:**
- `DIG.AI` (EPIC 15.31 — AI Brain HFSM)
- `DIG.AI` (EPIC 15.32 — Ability Framework)
- `Unity.NetCode`
- `Unity.Physics`
- `Unity.Mathematics`
- `Unity.Burst`

**Feature:** A tiered entity framework for rendering and simulating massive enemy swarms (10,000–50,000+) by splitting enemies into lightweight flow-field particles, aware-tier agents, and full combat entities — with seamless promotion/demotion between tiers based on player proximity.

---

## Overview

### Problem

The current enemy pipeline treats every enemy identically: each BoxingJoe-class entity carries 20+ ECS components (AIBrain, AIState, AbilityExecutionState, AbilityDefinition buffer, AbilityCooldownState buffer, AggroState, Health, DamageEvent buffer, StatusEffect buffer, StatusEffectRequest buffer, CombatState, AttackStats, DefenseStats, MoveTowardsAbility, SpawnPosition, DamageResistance, DeathState), a full kinematic physics body in the broadphase, and hitbox child entities with their own component stacks.

At 50,000 enemies this means:
- **50,000+ physics bodies** in the broadphase tree (kinematic bodies still participate in collision queries)
- **100,000+ total entities** (root + hitbox children)
- **50,000 AI brain ticks** per server frame (state transitions, ability selection, combat behavior, aggro evaluation)
- **50,000 ability cooldown updates** per frame
- **Zero distance culling** — every enemy runs full simulation regardless of proximity to any player

This architecture works well for 50–200 distinct enemies. It cannot scale to World War Z-style flowing hordes.

### Solution

Split enemies into **three simulation tiers** with automatic promotion/demotion based on player proximity:

| Tier | Entity Type | Components | Physics | Rendering | Typical Count |
|------|-------------|-----------|---------|-----------|---------------|
| **Particle** | SwarmParticle | 3 components (Position, Velocity, AnimState) | None | GPU Instanced (VAT) | 49,000+ |
| **Aware** | SwarmAgent | 6 components (+ FlowTarget, GroupID) | None | GPU Instanced (VAT) | 500 |
| **Combat** | Full Entity | Stripped-down BoxingJoe (Health, single ability, capsule collider) | Kinematic capsule | Skinned mesh | 100 |

**Particle tier** entities follow a shared **flow field** (a grid of direction vectors pointing toward players). They have no individual AI, no physics body, no health, no abilities. They are positions in a buffer that move along vectors and get rendered via GPU instancing with baked vertex animation textures.

**Aware tier** entities have been noticed by the flow field as approaching a player threshold (~30m). They gain individual flow targets and group cohesion behavior but still no physics body or health.

**Combat tier** entities are within attack range of a player (~8m). They promote to real ECS entities with health, a single melee ability, a kinematic physics capsule, and ghost replication. They use the existing `AbilityExecutionSystem` and `CombatResolutionSystem` pipelines.

The `AIBrainArchetype.Swarm` enum value (already defined) gates all swarm behavior. Existing Melee/Ranged/Elite/Boss enemies are completely unaffected.

### Principles

1. **Additive, not invasive** — Zero changes to existing AI systems. Swarm systems query `SwarmParticle`/`SwarmAgent` components that non-swarm enemies don't have. Existing `AIBrain`/`AbilityExecutionSystem`/`AggroSystem` queries exclude swarm particles entirely.
2. **Simulate the crowd, individualize the threat** — Only enemies that can actually interact with the player need individual simulation. The other 49,900 are a fluid.
3. **Flow field over pathfinding** — One shared grid replaces 50,000 individual A* paths. Direction vectors are computed from player positions, obstacle maps, and terrain. All particles read the same grid.
4. **GPU rendering for particles** — No `RenderMesh` components, no transform hierarchy. Raw `Matrix4x4` arrays fed to `Graphics.DrawMeshInstanced` with vertex animation textures. Follows the proven `DecoratorInstancingSystem` pattern.
5. **Deterministic client simulation** — Swarm particles are NOT ghost-replicated. The flow field grid is replicated. Clients run particle simulation locally from the same flow field, producing visually identical results. Only combat-tier entities get ghost replication (and there are only ~100).
6. **Seamless tier transitions** — Promotion spawns a real entity at the particle's exact position/rotation/animation frame. Demotion records state and inserts a particle. No visible pop at transition distances.

---

## Tier Architecture

### Tier Comparison

| Property | Particle | Aware | Combat |
|----------|----------|-------|--------|
| **ECS Entity** | Yes (minimal) | Yes (minimal) | Yes (full) |
| **Components** | SwarmParticle, SwarmAnimState | + SwarmAgent, SwarmGroupID | Health, AIBrain(Swarm), AbilityExecutionState, AbilityCooldownState, DeathState, DamageResistance, PhysicsCollider |
| **Movement** | Flow field sample | Flow field + cohesion | Direct position write (existing AI pattern) |
| **AI** | None | Group cohesion only | Single-ability combat (existing pipeline) |
| **Physics Body** | None | None | Kinematic capsule |
| **Hitboxes** | None | None | Single capsule (no split-entity Head/Torso) |
| **Health** | None | None | Yes (stripped — no shield, no block) |
| **Ghost Replicated** | No | No | Yes |
| **Rendering** | GPU Instanced (VAT) | GPU Instanced (VAT) | Skinned mesh |
| **Cost/Entity** | ~0.001ms | ~0.005ms | ~0.05ms (same as BoxingJoe) |
| **Damage Response** | Area removal (die in AOE) | Area removal | Full damage pipeline |

### Promotion/Demotion Rules

```
Particle → Aware:    distance to nearest player < AwareRange (30m)
Aware → Combat:      distance to nearest player < CombatRange (8m)
Combat → Aware:      distance to all players > DemoteRange (15m) AND not in active ability cast
Aware → Particle:    distance to all players > AwareRange + Hysteresis (35m)
```

Hysteresis gaps prevent thrashing at tier boundaries. A 5m buffer between promote and demote thresholds ensures entities don't flip-flop every frame.

---

## Phase 1: Flow Field Infrastructure

### Problem

50,000 individual pathfinding queries per frame is impossible. Swarm members need a shared navigation solution that costs O(1) per entity (a single grid lookup) rather than O(N * pathfinding).

### Components

```
FlowFieldGrid : IComponentData [Ghost: Server]
    int GridWidth                   // Grid dimensions (cells)
    int GridHeight
    float CellSize                  // World units per cell (default: 2m)
    float3 WorldOrigin              // Grid origin (bottom-left corner)
    float UpdateInterval            // How often to rebuild (default: 0.25s)
    float LastUpdateTime            // Timer

FlowFieldCell : IBufferElementData  // On the FlowFieldGrid entity (server-owned)
    float2 Direction                // Normalized XZ direction toward nearest player
    float Distance                  // Distance to nearest player (for intensity falloff)
    byte Cost                       // Terrain cost (0=passable, 255=impassable)
    byte Flags                      // Wall, cliff, water, etc.
```

### Systems

- **`FlowFieldBuildSystem`** (SimulationSystemGroup, Server|LocalSimulation)
    - Runs at `UpdateInterval` (not every frame — flow fields are stable over short periods)
    - **Integration field:** BFS flood-fill from each player position. Each cell stores distance to nearest player.
    - **Flow field:** For each cell, direction = normalize(lowest-cost neighbor position - cell position)
    - **Obstacle integration:** Reads terrain/voxel data for impassable cells (Cost=255). Reads physics colliders for large static obstacles.
    - **Multi-player support:** Multiple simultaneous BFS sources (one per player). Each cell points toward nearest player.
    - **Job-parallel:** Grid cells are independent — parallelizes across cell rows via `IJobParallelFor`
    - Output: `FlowFieldCell` buffer on the grid entity

- **`FlowFieldReplicationSystem`** (SimulationSystemGroup, Server)
    - Compresses flow field into a compact representation for client replication
    - Direction quantized to 8 directions (3 bits) + distance quantized to 8 levels (3 bits) = 6 bits per cell
    - Packed into `DynamicBuffer<FlowFieldPackedChunk>` with ghost replication
    - Clients unpack into local `FlowFieldCell` buffer for particle simulation
    - Only sends delta updates (cells that changed since last send)

- **`FlowFieldDebugSystem`** (Managed, PresentationSystemGroup, ClientSimulation, Editor-only)
    - Draws direction arrows on grid cells in Scene view
    - Color-coded by distance (red=close to player, blue=far)
    - Toggle via `FlowFieldDebugSettings` singleton

### Implementation Tasks

- [ ] Define `FlowFieldGrid` component and `FlowFieldCell` buffer
- [ ] Implement BFS flood-fill integration field builder (Burst, parallel)
- [ ] Implement flow field direction computation from integration field
- [ ] Integrate terrain/voxel cost map (impassable cells)
- [ ] Implement `FlowFieldReplicationSystem` with delta compression
- [ ] Create `FlowFieldAuthoring` MonoBehaviour + Baker (grid dimensions, cell size, update interval)
- [ ] Implement `FlowFieldDebugSystem` (Scene view visualization)
- [ ] **Test:** Single player, 200×200 grid — flow field builds in < 2ms
- [ ] **Test:** 4 players — BFS from 4 sources produces correct nearest-player directions
- [ ] **Test:** Impassable cells (walls, cliffs) are correctly avoided in flow field
- [ ] **Test:** Client receives flow field, unpacks, matches server state

---

## Phase 2: Swarm Particle Simulation

### Problem

The particle tier needs to move 49,000+ entities per frame using the flow field. Each particle samples its grid cell's direction vector and moves accordingly. No individual AI, no physics, no collision resolution between particles.

### Components

```
SwarmParticle : IComponentData
    float3 Position                 // World-space position (NOT LocalTransform — no transform hierarchy)
    float3 Velocity                 // Current movement velocity
    float Speed                     // Base movement speed (from SwarmConfig)
    uint ParticleID                 // Unique identifier for promotion/demotion matching

SwarmAnimState : IComponentData
    byte AnimClipIndex              // Current animation clip (0=Walk, 1=Run, 2=Attack, 3=Die)
    float AnimTime                  // Normalized time within clip (0-1)
    float AnimSpeed                 // Playback speed multiplier

SwarmConfig : IComponentData [Ghost: Server]
    Entity ParticlePrefab           // Minimal prefab for particle entities
    Entity CombatPrefab             // Full prefab for promoted combat entities
    float BaseSpeed                 // Default movement speed (m/s)
    float SpeedVariance             // ±variance for visual diversity
    float AwareRange                // Distance to promote Particle → Aware (30m)
    float CombatRange               // Distance to promote Aware → Combat (8m)
    float DemoteRange               // Distance to demote Combat → Aware (15m)
    float AwareHysteresis           // Extra distance before Aware → Particle (5m)
    int MaxCombatEntities           // Hard cap on simultaneous combat entities (100)
    int MaxAwareEntities            // Hard cap on aware entities (500)
    float SeparationRadius          // Minimum distance between particles (0.5m)
    float SeparationWeight          // Separation force strength (1.0)
    float CohesionWeight            // Cohesion force strength (0.3)
    float AlignmentWeight           // Alignment force strength (0.2)
    float FlowFieldWeight           // Flow field following strength (2.0)
    float NoiseScale                // Perlin noise scale for organic movement (0.1)
    float NoiseStrength             // Perlin noise amplitude (0.5)

SwarmSpawner : IComponentData [Ghost: Server]
    int TotalParticles              // Total swarm size
    int BatchSize                   // Particles per frame during spawn
    float SpawnRadius               // Initial scatter radius
    int SpawnedCount                // Runtime counter
    bool IsComplete                 // All spawned
    bool SpawnOnStart               // Auto-trigger
```

### Systems

- **`SwarmSpawnerSystem`** (SimulationSystemGroup, Server)
    - Frame-budgeted batch instantiation (same pattern as `EnemySpawnerSystem`)
    - Creates minimal particle entities: `SwarmParticle` + `SwarmAnimState` only
    - Random scatter within `SpawnRadius` using deterministic seed
    - Sets initial velocity toward nearest flow field direction

- **`SwarmParticleMovementSystem`** (SimulationSystemGroup, Server|LocalSimulation, Burst)
    - For each particle: sample flow field at particle position → get direction
    - Apply movement: `Position += (FlowDirection * FlowFieldWeight + Noise) * Speed * dt`
    - Add Perlin noise offset for organic, non-uniform movement
    - Y-axis clamped to terrain height (simple raycast or heightmap lookup)
    - Update `SwarmAnimState.AnimTime` based on speed
    - **Job-parallel:** Each particle is independent — `IJobParallelFor` over particle array

- **`SwarmSeparationSystem`** (SimulationSystemGroup, Server|LocalSimulation, Burst)
    - Lightweight neighbor avoidance using spatial grid (reuse `NativeParallelMultiHashMap` pattern)
    - For each particle: query 9 neighboring cells → accumulate separation force from nearby particles
    - Only applies separation (no cohesion/alignment at particle tier — that's flow field's job)
    - Grid cell size = `SeparationRadius * 2` for efficient queries
    - Runs every 2nd frame to save budget (separation is visually forgiving)

- **`SwarmAnimationSystem`** (SimulationSystemGroup, Server|LocalSimulation, Burst)
    - Advances `AnimTime` based on movement speed
    - Selects `AnimClipIndex` based on speed thresholds:
        - Speed < 0.1: AnimClip 0 (Idle)
        - Speed < BaseSpeed * 0.6: AnimClip 1 (Walk)
        - Speed >= BaseSpeed * 0.6: AnimClip 2 (Run)

### Data Layout

Particles use `IComponentData` on minimal archetype entities rather than a raw `NativeArray` buffer. This allows:
- Standard `SystemAPI.Query` iteration (no custom scheduling)
- `EntityCommandBuffer` for creation/destruction
- Compatible with Burst `IJobEntity`
- Entities filtered by standard component presence (promotion removes `SwarmParticle`, adds combat components)

### Implementation Tasks

- [ ] Define `SwarmParticle`, `SwarmAnimState`, `SwarmConfig`, `SwarmSpawner` components
- [ ] Implement `SwarmSpawnerSystem` (batch creation, scatter placement)
- [ ] Implement `SwarmParticleMovementSystem` (flow field sampling, noise, movement)
- [ ] Implement spatial grid for particle separation (reuse `NativeParallelMultiHashMap` pattern)
- [ ] Implement `SwarmSeparationSystem` (neighbor query, separation force)
- [ ] Implement `SwarmAnimationSystem` (speed-based clip selection, time advance)
- [ ] Create `SwarmSpawnerAuthoring` MonoBehaviour + Baker
- [ ] Create `SwarmConfigAuthoring` MonoBehaviour + Baker
- [ ] **Test:** 10,000 particles following flow field — total movement cost < 1ms
- [ ] **Test:** 50,000 particles — total simulation cost < 5ms
- [ ] **Test:** Separation prevents visible clumping at doorways
- [ ] **Test:** Perlin noise creates organic, non-uniform crowd movement

---

## Phase 3: Tier Promotion & Demotion

### Problem

Particles need to seamlessly become real entities when near a player and revert to particles when far away. The transition must be invisible to the player — no popping, no teleporting, no animation discontinuity.

### Components

```
SwarmAgent : IComponentData
    float3 FlowTarget               // Individual target position (overrides flow field)
    float AgentTimer                 // Time since promotion to aware
    uint SourceParticleID            // ParticleID this agent was promoted from

SwarmGroupID : IComponentData
    int GroupIndex                   // Which swarm group this belongs to (for coordinated behavior)

SwarmCombatTag : IComponentData      // Tag identifying a promoted combat entity
    uint SourceParticleID            // ParticleID for demotion tracking
    float PromotionTime              // When this entity was promoted (for minimum lifetime)

SwarmPromotionEvent : IComponentData // Transient — consumed by promotion system
    uint ParticleID
    float3 Position
    float3 Velocity
    byte AnimClipIndex
    float AnimTime

SwarmDemotionEvent : IComponentData  // Transient — consumed by demotion system
    Entity CombatEntity
    float3 Position
    float3 Velocity
```

### Systems

- **`SwarmTierEvaluationSystem`** (SimulationSystemGroup, Server, Burst)
    - Every N frames (configurable, default 4): evaluate all particles against player positions
    - Uses player spatial grid (existing `SpatialHashGridData`) to find nearest player efficiently
    - **Promote Particle → Aware:** Distance < `AwareRange` AND aware count < `MaxAwareEntities`
        - Remove `SwarmParticle`, add `SwarmAgent` + `SwarmGroupID` via ECB
    - **Promote Aware → Combat:** Distance < `CombatRange` AND combat count < `MaxCombatEntities`
        - Create `SwarmPromotionEvent` entity via ECB
    - **Demote Combat → Aware:** Distance to ALL players > `DemoteRange` AND `AbilityExecutionState.Phase == Idle` AND alive for > 2 seconds
        - Create `SwarmDemotionEvent` entity via ECB
    - **Demote Aware → Particle:** Distance to ALL players > `AwareRange + AwareHysteresis`
        - Remove `SwarmAgent`, add `SwarmParticle` via ECB
    - Respects hard caps: never promote more than `MaxCombatEntities` total

- **`SwarmPromotionSystem`** (SimulationSystemGroup, Server)
    - Reads `SwarmPromotionEvent` entities
    - For each event:
        1. Destroy the aware-tier entity (remove from particle simulation)
        2. Instantiate `SwarmConfig.CombatPrefab` at event position
        3. Set `LocalTransform` to match particle position/rotation
        4. Set initial `Health` to full
        5. Set `AIBrain.Archetype = Swarm` (selects swarm combat behavior)
        6. Set `SwarmCombatTag.SourceParticleID` for demotion tracking
        7. Add `GhostOwner` for ghost replication (becomes visible to clients)
    - Destroy the event entity after processing

- **`SwarmDemotionSystem`** (SimulationSystemGroup, Server)
    - Reads `SwarmDemotionEvent` entities
    - For each event:
        1. Read combat entity's position, velocity, animation state
        2. Create new particle-tier entity with `SwarmParticle` + `SwarmAnimState`
        3. Set particle position/velocity to match combat entity
        4. Destroy the combat entity (removes ghost, frees physics body)
    - Destroy the event entity after processing

- **`SwarmCombatBehaviorSystem`** (SimulationSystemGroup, Server|LocalSimulation, Burst)
    - Specialized combat AI for `AIBrainArchetype.Swarm` entities
    - Simpler than full AI: chase nearest player, attack when in range
    - No patrol, no investigate, no flee, no circle-strafe
    - Single ability: basic melee bite/claw (uses existing `AbilityExecutionSystem`)
    - Movement: direct position write toward target (existing kinematic pattern)
    - Query: `WithAll<SwarmCombatTag, AIBrain>` — only processes promoted swarm entities

### Visual Transition

The transition from GPU-instanced particle to skinned-mesh entity happens at ~8m from the player. At that distance:
- Single-frame mesh swap is invisible in a crowd
- Animation continuity: `SwarmAnimState.AnimClipIndex` and `AnimTime` transferred to `Animator` state on promotion
- Velocity continuity: combat entity inherits particle velocity for first frame

On demotion (~15m away), the reverse happens. The skinned mesh entity is destroyed and a GPU-instanced particle appears at the same position. In a crowd of hundreds, this is imperceptible.

### Implementation Tasks

- [ ] Define `SwarmAgent`, `SwarmGroupID`, `SwarmCombatTag`, promotion/demotion event components
- [ ] Implement `SwarmTierEvaluationSystem` (distance checks, tier decisions, hard caps)
- [ ] Implement `SwarmPromotionSystem` (entity instantiation, state transfer)
- [ ] Implement `SwarmDemotionSystem` (entity destruction, particle insertion)
- [ ] Implement `SwarmCombatBehaviorSystem` (simplified chase + attack)
- [ ] Create `SwarmCombatPrefab` — stripped-down enemy: Health, AIBrain(Swarm), single AbilityDefinition, capsule PhysicsCollider, DeathState, DamageResistance (NO StatusEffect buffers, NO shield, NO block, NO hitbox children)
- [ ] Wire promotion animation handoff (AnimClipIndex → Animator state)
- [ ] Implement hysteresis thresholds to prevent tier thrashing
- [ ] **Test:** Particle promotes to combat entity at 8m — no visible pop
- [ ] **Test:** Combat entity demotes to particle at 15m — no visible pop
- [ ] **Test:** Hard cap of 100 combat entities respected under all conditions
- [ ] **Test:** Entity in active ability cast does not demote mid-attack
- [ ] **Test:** Promoted entity uses existing damage pipeline (takes damage, dies, drops loot)

---

## Phase 4: GPU Instanced Swarm Rendering

### Problem

50,000 individual `RenderMesh` components with skinned mesh renderers is impossible. Particles need GPU-instanced rendering with baked animations sampled in the vertex shader.

### Vertex Animation Textures (VAT)

Bake skeletal animations into textures at editor time:
- **Position texture:** Each row = one frame, each pixel = one vertex's XYZ position (encoded as RGB)
- **Normal texture:** Same layout for normals (for lighting)
- Clip metadata: frame count, FPS, loop flag stored in material properties
- Shader samples texture at `(vertexIndex / vertexCount, animTime)` to get vertex position
- No `Animator`, no `SkinnedMeshRenderer`, no bone transforms per entity

### Components

```
SwarmRenderGroup : IComponentData [Ghost: Server]
    int GroupID                      // Which visual group (zombie type A, B, C)
    int MeshIndex                    // Index into SwarmRenderConfig mesh array
    int MaterialIndex                // Index into SwarmRenderConfig material array

SwarmRenderConfig : IComponentData   // Singleton
    // Managed references stored in companion SwarmRenderConfigManaged MonoBehaviour
    // ECS component just holds capacity/count metadata
    int MeshCount
    int MaterialCount
    int MaxInstancesPerBatch         // 1023 (GPU instancing limit)
```

### Systems

- **`SwarmRenderBuildSystem`** (Managed, PresentationSystemGroup, ClientSimulation)
    - Collects all particle + aware tier positions into `Matrix4x4[]` arrays per render group
    - Frustum culling: skip particles behind camera or outside view frustum
    - Distance culling: skip particles beyond max render distance (200m)
    - Builds transform matrices: position + rotation from velocity direction + uniform scale
    - Sets per-instance shader properties: `_AnimClipIndex`, `_AnimTime` (via `MaterialPropertyBlock`)

- **`SwarmRenderDrawSystem`** (Managed, PresentationSystemGroup, ClientSimulation)
    - Calls `Graphics.DrawMeshInstanced(mesh, 0, material, matrices, count, propertyBlock)` per batch
    - Batches of 1023 (GPU instancing limit)
    - Shadow casting: optional, distance-culled (no shadows beyond 30m)
    - Layer: `Creature` layer for consistent culling with camera system

- **`SwarmVATBakeEditorTool`** (Editor-only)
    - Bakes `AnimationClip` assets into position/normal textures
    - Outputs: VAT textures + metadata ScriptableObject
    - Shader: custom `SwarmVAT.shader` that samples position texture per vertex
    - Supports multiple clips per mesh (walk, run, idle, attack, die)

### LOD Tiers for Rendering

| Distance | Mesh | Animation | Shadows |
|----------|------|-----------|---------|
| 0–30m | Full mesh | VAT sampled | Yes |
| 30–80m | Reduced mesh (50% polys) | VAT sampled (half-rate) | No |
| 80–200m | Billboard quad | No animation | No |
| 200m+ | Not rendered | — | — |

### Implementation Tasks

- [ ] Create VAT baker editor tool (AnimationClip → position/normal textures)
- [ ] Create `SwarmVAT.shader` (vertex shader samples position texture, fragment shader standard lighting)
- [ ] Create `SwarmVATShadow.shader` (shadow caster variant)
- [ ] Define `SwarmRenderGroup`, `SwarmRenderConfig` components
- [ ] Implement `SwarmRenderBuildSystem` (frustum cull, distance cull, matrix building)
- [ ] Implement `SwarmRenderDrawSystem` (batched `DrawMeshInstanced` calls)
- [ ] Create `SwarmRenderConfigManaged` MonoBehaviour (holds Mesh/Material/VAT texture references)
- [ ] Implement LOD distance tiers (full mesh → reduced → billboard → culled)
- [ ] Implement per-instance `MaterialPropertyBlock` for animation state
- [ ] **Test:** 10,000 particles rendered at 60fps with VAT animations
- [ ] **Test:** 50,000 particles rendered — GPU frame time < 4ms
- [ ] **Test:** Frustum culling eliminates off-screen particles from draw calls
- [ ] **Test:** LOD transitions not visible at transition distances

---

## Phase 5: Area Damage & Swarm Death

### Problem

When a player throws a grenade into 500 swarm particles, you can't promote all 500 to combat entities. Particle-tier damage must be resolved without entity promotion — spatial query, mark dead, spawn VFX.

### Components

```
SwarmDamageZone : IComponentData     // Transient event entity
    float3 Center                    // World position of damage source
    float Radius                     // Effect radius
    float Damage                     // Damage amount
    DamageType Type                  // Elemental type (for death VFX theming)
    Entity Source                    // Who caused the damage (for kill credit)

SwarmDeathVFXRequest : IComponentData // Transient — consumed by VFX system
    float3 Position
    byte DeathType                   // 0=normal, 1=explosion, 2=fire, 3=ice
    byte Count                       // How many died at this position (for VFX intensity)
```

### Systems

- **`SwarmAreaDamageSystem`** (SimulationSystemGroup, Server, Burst)
    - Listens for area damage events (existing `DamageEvent` with radius, grenade explosions, AOE abilities)
    - Creates `SwarmDamageZone` event entity for each area damage source
    - Queries swarm particle spatial grid for particles within radius
    - Particles in radius: removed from simulation (destroyed via ECB)
    - Groups nearby deaths into `SwarmDeathVFXRequest` clusters (max 1 VFX per 2m² area)
    - Does NOT promote particles — they just die
    - Combat-tier entities in the same radius take damage through the normal `DamageEvent` pipeline

- **`SwarmDeathVFXSystem`** (Managed, PresentationSystemGroup, ClientSimulation)
    - Reads `SwarmDeathVFXRequest` entities
    - Spawns GPU particle bursts at death positions (no individual entity per death)
    - Scales particle count by `SwarmDeathVFXRequest.Count` (more deaths = bigger burst)
    - Pools particle systems for reuse
    - Themed by `DeathType`: blood splatter (normal), explosion debris, fire embers, ice shards

- **`SwarmProjectileDamageSystem`** (SimulationSystemGroup, Server, Burst)
    - Handles single-target hits against swarm particles (hitscan, projectile impacts)
    - When a raycast/projectile hits a particle-tier entity: destroy it, spawn death VFX
    - No health check — particles die in one hit (they're fodder)
    - Optional: promote nearby particles to aware tier (scatter/flee response)

### Damage Pipeline Integration

```
Existing AOE/Grenade
    |
    v
DamageEvent (existing) ──> DamageApplySystem (existing, combat-tier entities only)
    |
    v
SwarmAreaDamageSystem ──> Query particle spatial grid ──> Remove particles ──> SwarmDeathVFXRequest
    |
    v
SwarmDeathVFXSystem ──> GPU particle bursts (no entities)
```

The existing `DamageApplySystem` and `SimpleDamageApplySystem` continue to handle combat-tier swarm entities through the normal pipeline. `SwarmAreaDamageSystem` only handles particle-tier deaths.

### Implementation Tasks

- [ ] Define `SwarmDamageZone`, `SwarmDeathVFXRequest` components
- [ ] Implement `SwarmAreaDamageSystem` (spatial query, particle removal, VFX clustering)
- [ ] Implement `SwarmDeathVFXSystem` (GPU particle bursts, pooling, theming)
- [ ] Implement `SwarmProjectileDamageSystem` (single-target particle kills)
- [ ] Wire grenade/AOE systems to create `SwarmDamageZone` events
- [ ] Create death VFX prefabs (blood burst, explosion, fire, ice variants)
- [ ] **Test:** Grenade kills 200 particles — 200 removed, clustered VFX spawned, no promoted entities
- [ ] **Test:** Hitscan shot kills single particle — particle removed, single death VFX
- [ ] **Test:** Combat-tier entity in same grenade radius takes damage through normal pipeline
- [ ] **Test:** Kill 1,000 particles in one frame — no frame spike (VFX clustered, not per-particle)

---

## Phase 6: Networking & Client Prediction

### Problem

Ghost-replicating 50,000 entities is impossible — NetCode's snapshot budget would be consumed instantly. Swarm particles must be simulated client-side from shared state (the flow field) rather than individually replicated.

### Replication Strategy

| Data | Replication Method | Bandwidth |
|------|-------------------|-----------|
| **Flow field** | `DynamicBuffer<FlowFieldPackedChunk>` ghost buffer, delta-compressed | ~2 KB/update (200×200 grid, 6 bits/cell, delta only) |
| **Swarm config** | `SwarmConfig` ghost component | 64 bytes (once) |
| **Spawn events** | RPC: position, count, seed | 16 bytes/event |
| **Combat entities** | Standard ghost replication (100 entities) | ~10 KB/snapshot |
| **Kill events** | RPC: position, radius, count | 12 bytes/event |
| **Total** | | ~15 KB/snapshot (vs 500+ KB for 50k ghosts) |

### Components

```
SwarmSpawnRPC : IRpcCommand
    float3 Origin                    // Spawn center
    int Count                        // Number of particles
    uint Seed                        // Deterministic RNG seed
    float Radius                     // Scatter radius

SwarmKillRPC : IRpcCommand
    float3 Center                    // Kill zone center
    float Radius                     // Kill zone radius
    int Count                        // Approximate count killed

FlowFieldPackedChunk : IBufferElementData [Ghost: All]
    // 64 cells packed into one buffer element (6 bits each = 48 bytes)
    // Direction: 3 bits (8 cardinal directions)
    // Distance tier: 3 bits (8 distance bands)
    FixedBytes48 PackedData
```

### Systems

- **`SwarmNetworkSyncSystem`** (SimulationSystemGroup, Server)
    - On spawn: send `SwarmSpawnRPC` with origin, count, seed
    - On area kill: send `SwarmKillRPC` with center, radius, count
    - Flow field: updated via ghost-replicated `FlowFieldPackedChunk` buffer

- **`SwarmClientSimulationSystem`** (SimulationSystemGroup, ClientSimulation)
    - Receives `SwarmSpawnRPC` → creates local particle entities with same seed (deterministic positions)
    - Receives `SwarmKillRPC` → removes particles within radius of center
    - Runs same `SwarmParticleMovementSystem` logic using client's unpacked flow field
    - Minor desync over time is acceptable — in 50,000 particles, individual position errors are invisible
    - Periodic resync: every 10 seconds, server sends authoritative particle count per sector → client adjusts

- **`SwarmResyncSystem`** (SimulationSystemGroup, Server, every 10 seconds)
    - Divides world into sectors (e.g., 4×4 grid)
    - Sends particle count per sector to clients
    - Client compares local count → spawns/removes particles to match server count
    - Positions are approximate (randomly placed in sector) — invisible in a dense swarm

### Implementation Tasks

- [ ] Define `SwarmSpawnRPC`, `SwarmKillRPC`, `FlowFieldPackedChunk` components
- [ ] Implement `SwarmNetworkSyncSystem` (spawn/kill RPCs, flow field packing)
- [ ] Implement `SwarmClientSimulationSystem` (RPC handling, local particle sim)
- [ ] Implement `SwarmResyncSystem` (periodic sector-based count correction)
- [ ] Implement flow field packing (6 bits/cell into `FixedBytes48` chunks)
- [ ] Implement flow field unpacking on client
- [ ] **Test:** Client spawns particles from RPC — positions match server (deterministic seed)
- [ ] **Test:** Client kills particles from RPC — visual matches server
- [ ] **Test:** 10-second resync corrects client drift without visible pop
- [ ] **Test:** Total swarm networking bandwidth < 20 KB/s for 50k particles + 100 combat ghosts

---

## Phase 7: Swarm Behaviors & Formation

### Problem

Raw flow-field-following produces a uniform stream. Real zombie hordes exhibit emergent group behaviors: funneling through doorways, piling up at walls, splitting around obstacles, forming crescents around defenders.

### Components

```
SwarmFormationConfig : IComponentData [Ghost: Server]
    SwarmFormationType Formation     // Current group behavior mode
    float DensityTarget              // Target particles per m² (for piling)
    float FunnelWidth                // Narrowest passage width (for funneling behavior)
    float WallPileHeight             // Max height of wall pile (for climbing behavior)

SwarmFormationType : byte enum
    Flow = 0            // Default — follow flow field
    Funnel = 1          // Compress through narrow passage
    Pile = 2            // Stack at obstacle (wall climbing)
    Surround = 3        // Encircle target position
    Scatter = 4         // Flee from threat (explosion response)

SwarmEmergentState : IComponentData
    float LocalDensity               // Particles per m² around this particle
    byte NearWall                    // 1 if wall detected within 2m
    byte Stalled                     // 1 if velocity near zero for > 1 second
    float StallTimer                 // Time at near-zero velocity
```

### Systems

- **`SwarmDensitySystem`** (SimulationSystemGroup, Server|LocalSimulation, Burst)
    - Every 4th frame: calculate `LocalDensity` per particle using spatial grid neighbor count
    - Detect stalled particles (velocity < 0.1 for > 1 second)
    - Detect wall proximity (flow field cell Cost > 200 within 2m)

- **`SwarmFormationSystem`** (SimulationSystemGroup, Server|LocalSimulation, Burst)
    - Reads `SwarmFormationConfig` and `SwarmEmergentState`
    - **Flow:** Default — pure flow field following (no modification)
    - **Funnel:** High-density + narrow passage detected → compress laterally, increase forward speed
    - **Pile:** Stalled at wall → increment Y position slowly (climb over each other), look for alternate paths
    - **Surround:** Near target with high density → spread laterally, reduce forward speed, form crescent
    - **Scatter:** Explosion/threat event → invert flow direction temporarily, add random spread
    - Emergent: formation type auto-detected from local conditions, not globally set

- **`SwarmSoundSystem`** (Managed, PresentationSystemGroup, ClientSimulation)
    - Ambient swarm audio based on nearby particle density
    - Low density: individual footsteps/groans (spatial audio, few sources)
    - High density: crowd ambience loop (non-spatial, intensity scales with count)
    - Combat proximity: add combat vocalizations
    - Uses audio pooling (max 8 spatial sources for swarm)

### Implementation Tasks

- [ ] Define `SwarmFormationConfig`, `SwarmFormationType`, `SwarmEmergentState` components
- [ ] Implement `SwarmDensitySystem` (spatial grid density calculation, stall detection, wall detection)
- [ ] Implement `SwarmFormationSystem` (funnel, pile, surround, scatter behaviors)
- [ ] Implement wall-pile Y-axis climbing (particles stack vertically at obstacles)
- [ ] Implement scatter response to explosions (temporary flow reversal)
- [ ] Implement `SwarmSoundSystem` (density-based ambient audio)
- [ ] **Test:** 1,000 particles funneling through doorway — natural compression, no stuck particles
- [ ] **Test:** Particles pile at wall — visible stacking, alternate paths found
- [ ] **Test:** Grenade scatter — particles flee explosion radius, reform 3 seconds later
- [ ] **Test:** Surround behavior — crescent formation around isolated player

---

## Phase 8: Designer Tooling & Polish

### Editor Tools

- [ ] **Swarm Spawner Inspector** — Live preview of spawn area, particle count slider, density visualization
- [ ] **Flow Field Debugger** (`Window > DIG > Swarm > Flow Field`)
    - Real-time flow field visualization (direction arrows, cost heatmap)
    - Player position markers, BFS wavefront display
    - Obstacle cell highlighting
    - Grid resolution controls
- [ ] **Swarm Profiler** (`Window > DIG > Swarm > Profiler`)
    - Per-tier entity counts (particle / aware / combat)
    - Promotion/demotion rates per second
    - Flow field build time
    - Particle simulation time
    - Rendering batch count and draw call count
    - Network bandwidth usage
- [ ] **VAT Baker Window** (`Window > DIG > Swarm > VAT Baker`)
    - Select source mesh + animation clips
    - Preview baked animation in Scene view
    - Output texture resolution controls
    - Batch bake multiple enemy types

### Swarm Prefab Wizard

- [ ] **Create Swarm Enemy** (`DIG > Swarm > Create Swarm Enemy`)
    - Step 1: Select base mesh + animations → auto-bake VAT
    - Step 2: Configure combat prefab (health, damage, speed)
    - Step 3: Configure swarm settings (particle count, ranges, formation)
    - Output: particle prefab, combat prefab, VAT textures, SwarmConfig SO

### Profiler Markers

```
DIG.Swarm.FlowField.Build          // Flow field BFS + direction computation
DIG.Swarm.Particle.Movement        // Particle position updates
DIG.Swarm.Particle.Separation      // Separation force calculation
DIG.Swarm.Tier.Evaluation          // Distance checks for promotion/demotion
DIG.Swarm.Tier.Promotion           // Entity instantiation on promotion
DIG.Swarm.Tier.Demotion            // Entity destruction on demotion
DIG.Swarm.Render.Build             // Matrix building + frustum culling
DIG.Swarm.Render.Draw              // DrawMeshInstanced calls
DIG.Swarm.Damage.Area              // Area damage spatial queries
DIG.Swarm.Network.Sync             // RPC send/receive
```

### Sample Scenes

- [ ] `Scenes/Samples/SwarmShowcase` — 10,000 zombies flowing toward a fortified position
- [ ] `Scenes/Samples/SwarmStressTest` — 50,000 particles, configurable tier thresholds

---

## Design Considerations

### Why Three Tiers Instead of Two

A two-tier system (particle + combat) creates a jarring transition: at 8m, a GPU-instanced blob suddenly becomes a full skinned-mesh entity. The aware tier provides a middle ground at 30m where entities gain individual flow targets and subtle behavioral variety before the player gets close enough to scrutinize them.

### Why Not Ghost-Replicate All Particles

NetCode's snapshot system serializes component data per entity per tick. At 50,000 entities with even a minimal 8-byte position, that's 400KB per snapshot at 30Hz = 12MB/s of bandwidth. Ghost relevancy could reduce this but still requires per-entity tracking overhead on the server.

The flow field approach costs ~2KB per update. The server sends the navigation grid, and clients simulate particles locally. Minor position desync between server and client is invisible in a crowd of thousands.

### Why IComponentData Instead of Raw NativeArray

Particles could be stored in a single `NativeArray<SwarmParticle>` on a singleton entity rather than as individual ECS entities. However:
- `EntityCommandBuffer` handles creation/destruction cleanly during promotion
- `SystemAPI.Query` provides standard iteration patterns (no custom job scheduling)
- Burst `IJobEntity` works directly on component queries
- Entity queries can filter by component presence (promoted entities lose `SwarmParticle`)
- Memory layout is identical to a NativeArray for single-archetype entities (ECS chunk = contiguous array)

The performance difference is negligible for query iteration. The ergonomic benefit of standard ECS patterns is significant.

### Interaction with Existing AI Systems

| Existing System | Swarm Interaction |
|----------------|-------------------|
| `AIStateTransitionSystem` | Queries `RefRW<AIState>` — swarm particles don't have `AIState`, invisible to system |
| `AICombatBehaviorSystem` | Queries `RefRW<AIState>, RefRO<AIBrain>` — only combat-tier swarm entities match (they have AIBrain) |
| `AbilitySelectionSystem` | Queries `RefRW<AbilityExecutionState>` — only combat-tier entities match |
| `AbilityExecutionSystem` | Queries `RefRW<AbilityExecutionState>` — only combat-tier entities match |
| `AggroSystem` | Queries `RefRW<AggroState>` — swarm combat entities can have simplified aggro |
| `EnemySpawnerSystem` | Queries `EnemySpawner` — completely separate from `SwarmSpawner` |

Zero query overlap between particle/aware tiers and existing AI systems. Combat-tier entities integrate with existing systems through standard component presence.

### NetCode Safety

1. **SwarmParticle** — NOT ghost-replicated (client simulates locally from flow field)
2. **FlowFieldCell** — Stored on server-owned grid entity, packed into ghost-replicated buffer
3. **SwarmCombatTag** — Ghost-replicated (combat entities are standard ghosts)
4. **SwarmSpawnRPC / SwarmKillRPC** — Standard NetCode RPCs (server → client)
5. **SwarmDeathVFXRequest** — Client-only transient event (never replicated)
6. **NO new IBufferElementData on ghost-replicated player entities** — all buffers are on server-owned or swarm-owned entities

### Performance Budget

| System | Target | Notes |
|--------|--------|-------|
| Flow Field Build | < 2ms | Runs every 0.25s, not every frame. Amortized: < 0.1ms/frame |
| Particle Movement (50k) | < 3ms | Burst parallel, simple math per entity |
| Particle Separation (50k) | < 1ms | Runs every 2nd frame, spatial grid queries |
| Tier Evaluation | < 0.5ms | Runs every 4th frame, distance checks only |
| Promotion/Demotion | < 0.2ms | ECB structural changes, max 10-20 per frame |
| Render Build (50k) | < 2ms | Frustum cull + matrix build, managed |
| Render Draw (50k) | < 4ms GPU | ~50 batches of 1023, GPU instanced |
| Network Sync | < 20 KB/s | Flow field delta + RPCs + 100 combat ghosts |
| **Total CPU Budget** | < 8ms/frame | At 30Hz server tick, leaves 25ms for everything else |

### Dynamic Obstacle Handling

The flow field handles static terrain at build time. Dynamic obstacles (destructible walls, doors, vehicles) require runtime updates:

1. **Dynamic obstacle markers:** Entities with `FlowFieldObstacle : IComponentData` tag + `PhysicsCollider` are sampled when the flow field rebuilds. Their AABB is rasterized into the cost grid as impassable cells.
2. **Partial rebuild:** When a `FlowFieldObstacle` entity is created/destroyed/moved, set a `FlowFieldGrid.NeedsRebuild` flag. BFS only re-floods from affected sectors (not the entire grid).
3. **Doors:** Door open → remove `FlowFieldObstacle` tag → next rebuild clears those cells → particles flow through. Door close → add tag → cells become impassable.
4. **Destructibles:** On destruction event, the `FlowFieldObstacle` entity is destroyed → cells clear on next rebuild.
5. **Moving vehicles:** Moving obstacle cells are marked `Cost=128` (high but passable) rather than 255 (impassable). Particles slow down and flow around rather than pathfinding completely.

### Terrain Height Sampling

50,000 raycasts per frame for Y-axis clamping is prohibitive. Strategy:

1. **Heightmap cache:** `SwarmTerrainCache` singleton stores a `NativeArray<float>` heightmap sampled from terrain/voxel data at flow field resolution (same grid). Built once alongside the flow field, same rebuild interval.
2. **Bilinear interpolation:** Particle Y = bilinear sample of the 4 nearest heightmap cells. Cost: 4 array lookups + 2 lerps per particle. No raycasts.
3. **Cache invalidation:** When terrain/voxels change (excavation, building), mark affected heightmap cells dirty. Re-sample only dirty cells on next rebuild.
4. **Fallback:** If heightmap is unavailable (no terrain in scene), particles stay at spawn Y. This supports flat indoor environments.

### Quality Presets & Graceful Degradation

| Preset | Max Particles | Aware Cap | Combat Cap | Separation | Flow Field Res | Render Distance |
|--------|--------------|-----------|------------|------------|---------------|-----------------|
| Ultra | 50,000 | 500 | 100 | Every 2 frames | 200×200 (2m cells) | 200m |
| High | 25,000 | 300 | 80 | Every 4 frames | 150×150 (3m cells) | 150m |
| Medium | 10,000 | 200 | 50 | Every 8 frames | 100×100 (4m cells) | 100m |
| Low | 5,000 | 100 | 30 | Disabled | 80×80 (5m cells) | 60m |

**Runtime adaptive scaling:** `SwarmPerformanceMonitor` system (runs every 60 frames) measures:
- If `SwarmParticleMovementSystem` exceeds 5ms for 3 consecutive measurements → reduce particle count by 20% (destroy farthest particles)
- If GPU frame time exceeds target → reduce render distance by 25%
- If total swarm budget exceeds 10ms → drop to next lower preset

Presets stored in `SwarmQualityConfig` singleton, selectable via Graphics Settings UI.

### Multi-Swarm Support

Multiple swarm types (zombies, insects, rats) coexist via `SwarmGroupID`:

1. Each swarm type has its own `SwarmConfig` entity with unique `SwarmGroupID.GroupIndex`
2. `SwarmSpawnerSystem` creates particles with the spawner's group index
3. `SwarmRenderBuildSystem` batches by `SwarmRenderGroup.GroupID` → different meshes/materials per type
4. `SwarmParticleMovementSystem` reads the same shared flow field (all swarms converge on players)
5. `SwarmSeparationSystem` only separates particles within the same group (zombies don't avoid insects)
6. `SwarmTierEvaluationSystem` respects per-group hard caps (100 zombie combat + 50 insect combat ≠ 150 total)
7. Combat prefabs differ per group: zombie=melee bite, insect=ranged spit

To add a new swarm type: create new SwarmConfig SO + particle prefab + combat prefab + VAT textures. Zero code changes.

### Particle Loot (Aggregate Drops)

Particle-tier kills have no individual `LootTableRef` (they have no Health or damage pipeline). Instead:

1. **Aggregate loot:** `SwarmAreaDamageSystem` tracks total particles killed per `SwarmDamageZone` event.
2. **Batch roll:** Every N particle kills (configurable, default 10), roll the swarm type's shared `LootTableSO` once. Spawn a single loot entity at the center of the kill zone.
3. **Config:** `SwarmConfig.ParticleKillsPerLootRoll = 10`, `SwarmConfig.ParticleLootTableId` (optional — 0 means no loot from particles).
4. Combat-tier kills use the normal `DeathLootSystem` pipeline (they have `LootTableRef`).

This prevents 200 loot entities from a single grenade while still rewarding AOE damage.

### Memory Budget

| Component | Size (bytes) | Count | Total |
|-----------|-------------|-------|-------|
| SwarmParticle | 28 | 50,000 | 1.37 MB |
| SwarmAnimState | 12 | 50,000 | 0.59 MB |
| Entity overhead | 16 | 50,000 | 0.78 MB |
| Chunk headers | ~256 | ~400 | 0.10 MB |
| **Particle Total** | | | **~2.84 MB** |
| SwarmAgent (aware) | 16 | 500 | 0.01 MB |
| Combat entities (full) | ~800 | 100 | 0.08 MB |
| Flow field cells | 4 | 40,000 | 0.16 MB |
| Separation grid | ~8 | 10,000 | 0.08 MB |
| **Grand Total** | | | **~3.17 MB** |

3.17 MB for 50,000 entities is well within budget. By comparison, 50,000 full enemies (800 bytes each) would require 40 MB + 40 MB for hitbox children.

### Save/Load Strategy

Swarm state is intentionally NOT saved to disk. On world load:

1. `SwarmSpawner` entities are scene-placed (persisted in subscene). On load, they re-trigger spawning.
2. If the player saved mid-encounter, the encounter system re-evaluates and respawns swarms at appropriate positions.
3. Combat-tier entities are ghost-replicated and destroyed on server shutdown — they respawn with the swarm.
4. Particle positions are ephemeral — exact positions don't matter in a crowd of thousands.

This avoids serializing 50k entity positions and is consistent with how most AAA games handle disposable enemies.

### Encounter Integration

`EncounterTriggerAction` supports swarm-specific trigger types:

| TriggerAction | Effect |
|---------------|--------|
| `SpawnSwarm` | Activate a `SwarmSpawner` (begins batch spawning) |
| `SetSwarmFormation(type)` | Override `SwarmFormationConfig.Formation` for all particles in group |
| `ScatterSwarm(position, radius)` | Force all particles in radius to `Scatter` formation for 3 seconds |
| `DespawnSwarm` | Destroy all particles in group (encounter ended, retreat) |
| `SetSwarmTarget(entity)` | Override flow field with explicit target (boss-phase target switch) |

Encounter phases can escalate swarms: Phase 1 = 5000 particles. Phase 2 = add 10000. Boss death = `DespawnSwarm`. This uses existing `EncounterPhaseSystem` trigger infrastructure.

### Accessibility

1. **Particle density option:** Settings → Accessibility → "Swarm Density" slider (25%-100%). Multiplied against quality preset max particles. For players with motion sensitivity or visual processing issues.
2. **Reduced screen effects:** Mass death VFX can be disabled or reduced to simple fade-outs. `SwarmDeathVFXSystem` checks `AccessibilityConfig.ReducedEffects` flag.
3. **Colorblind swarm differentiation:** Multi-swarm types differentiated by silhouette shape (not just color). Zombie=humanoid, insect=small/skittery, rat=low/fast. Shape is primary identifier.
4. **Audio cues:** Swarm density produces directional audio (horde approaching from left). Not reliant on visual awareness alone.

---

## Integration Points

| System | EPIC | Integration |
|--------|------|-------------|
| AI Brain Framework | 15.31 | `AIBrainArchetype.Swarm` gates swarm-specific behavior in existing systems |
| Ability Framework | 15.32 | Combat-tier swarm entities use existing `AbilityExecutionSystem` pipeline |
| Combat Resolution | 15.x | Combat-tier damage flows through existing `CombatResolutionSystem` → `CombatResultEvent` |
| Damage Pipeline | 15.9 | Particle-tier area damage integrates with existing `DamageEvent` AOE sources |
| Encounter System | 15.32 | Encounters can trigger swarm spawns via `TriggerActionType.SpawnAddGroup` |
| Physics Optimization | 7.7 | Incremental broadphase (already enabled) critical for combat-tier physics bodies |
| Collision Layers | 7.4 | Swarm combat entities use `Creature` layer (bit 8) |
| Spatial Hashing | 15.23 | Particle separation grid reuses `NativeParallelMultiHashMap` pattern |
| GPU Instancing | 10.9 | Rendering follows `DecoratorInstancingSystem` pattern (batched `DrawMeshInstanced`) |
| Damage Numbers | 15.x | Combat-tier deaths produce `CombatResultEvent` → visible damage numbers |
| Health Bars | 15.x | Combat-tier entities get health bars via existing `EnemyHealthBarBridgeSystem` |

---

## Implementation Order

| Order | Phase | Dependencies | Estimated Scope |
|-------|-------|-------------|-----------------|
| 1 | Phase 1: Flow Field | None | Grid data structure, BFS builder, debug visualization |
| 2 | Phase 2: Particle Simulation | Phase 1 | Spawner, movement, separation, animation state |
| 3 | Phase 4: GPU Rendering | Phase 2 | VAT baker, shader, instanced draw system |
| 4 | Phase 3: Tier Promotion | Phase 2 | Evaluation, promotion/demotion, combat behavior |
| 5 | Phase 5: Area Damage | Phase 2, 3 | Spatial damage queries, particle death, VFX |
| 6 | Phase 6: Networking | Phase 1, 2, 3 | RPCs, client sim, flow field replication, resync |
| 7 | Phase 7: Behaviors | Phase 2 | Density, formations, emergent behavior, audio |
| 8 | Phase 8: Tooling | All phases | Editor tools, profiler, wizard, sample scenes |

Phase 4 (rendering) is ordered before Phase 3 (promotion) because you need to see the particles before the promotion system matters. Phase 6 (networking) comes after core simulation is proven locally.
