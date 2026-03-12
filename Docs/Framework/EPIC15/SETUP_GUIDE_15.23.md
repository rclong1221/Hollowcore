# EPIC 15.23 Setup Guide: Physics Performance Optimization

This guide covers the Unity Editor setup for the **Physics Performance Optimization** system — configurable solver settings, enemy collision filter enforcement, spatial-hash enemy separation, client physics stripping, tick rate tuning, and sensor frame-spreading.

---

## Overview

EPIC 15.23 solves the O(n²) physics bottleneck caused by creature-creature collision. With 500 enemies, physics was consuming ~48ms/frame generating ~125,000 contact pairs that no gameplay system used.

The optimization provides:

- **Creature-Creature Collision Removal** — Enemies no longer generate physics contacts with each other
- **Enemy Separation System** — O(n) spatial-hash anti-stacking replaces O(n²) physics contacts
- **Configurable Solver** — Designer-tunable solver iterations and broadphase settings
- **Client Physics Stripping** — Remote clients skip solver contacts for enemy ghosts
- **Tick Rate Tuning** — 30Hz server tick with catch-up support at low FPS
- **Sensor Frame-Spreading** — Detection system spreads sensor updates across multiple frames

### Performance Impact

| Metric | Before | After |
|--------|--------|-------|
| Physics (500 enemies) | ~48ms | <5ms |
| Physics (4000 enemies) | Unplayable | <15ms |
| Contact pairs (500 enemies) | ~125,000 | ~500 (player only) |
| Separation cost | N/A | <1ms (O(n) spatial hash) |

---

## Quick Start

### 1. Subscene Setup

Place these authoring components on GameObjects in your **subscene**:

| Component | Purpose | Required |
|-----------|---------|----------|
| `Physics Config` | Solver iterations and broadphase settings | Recommended |
| `Enemy Separation Config` | Separation radius, weight, speed | Recommended |
| `Detection Settings` | Sensor spread frames, raycast budget | Recommended |

All three are singletons — place one of each in the subscene on any GameObject.

### 2. Enemy Prefab Setup

Enemy prefabs require **no changes** — the systems enforce collision filters at spawn automatically. Ensure your enemy prefabs have:

- `PhysicsShapeAuthoring` on the root (or baked `PhysicsCollider`)
- `AIBrain` component (used by the filter enforcement query)

### 3. Reimport Subscenes

After adding the authoring components, reimport your subscenes to bake the singleton components.

---

## Component Reference

### Physics Config

Configures the physics solver and broadphase for the entire world. Place on any GameObject in the subscene.

**Menu Path:** `Add Component > DIG > Core > Physics Config`

| Property | Type | Default | Range | Description |
|----------|------|---------|-------|-------------|
| **Solver Iteration Count** | int | 4 | 1–8 | Solver iterations per physics step. Lower = faster but less stable contact resolution. |
| **Incremental Dynamic Broadphase** | bool | true | — | Only rebuild BVH nodes for bodies that moved. Essential for many kinematic enemies. |
| **Incremental Static Broadphase** | bool | true | — | Only rebuild BVH nodes for static bodies that changed. |

#### Solver Iteration Recommendations

| Scenario | Iterations | Notes |
|----------|-----------|-------|
| Mass enemy encounters (100+) | 2–3 | Enemies use spatial separation, not physics contacts |
| Normal gameplay (<50 enemies) | 4 | Good balance of stability and performance |
| Precise stacking (crates, physics puzzles) | 6–8 | Higher stability for player-interacted physics objects |

**Tip:** Solver iterations only affect contact resolution quality. Raycasts and overlap queries are unaffected.

---

### Enemy Separation Config

Configures the spatial-hash enemy separation system that prevents NPC stacking. Replaces physics creature-creature collision with an O(n) algorithm.

**Menu Path:** `Add Component > DIG > AI > Enemy Separation Config`

| Property | Type | Default | Range | Description |
|----------|------|---------|-------|-------------|
| **Separation Radius** | float | 1.5 | 0.5–5.0 | Distance within which enemies push apart (meters). Should roughly match enemy capsule diameter. |
| **Separation Weight** | float | 8.0 | 0.1–20.0 | Strength of the separation push. Higher = snappier separation, lower = softer blending. |
| **Max Separation Speed** | float | 8.0 | 0.5–20.0 | Maximum separation displacement per second (m/s). Prevents teleporting on deep overlap. |
| **Frame Interval** | int | 1 | 1–8 | Run separation every N frames. 1 = every frame, 2 = every other frame, 4 = every 4th. Must be power of 2. |

#### Tuning Guide

| Feel | Separation Radius | Separation Weight | Max Speed |
|------|-------------------|-------------------|-----------|
| Tight formations, slight overlap allowed | 1.0 | 4.0 | 4.0 |
| Natural spacing (recommended) | 1.5 | 8.0 | 8.0 |
| Loose, well-separated enemies | 2.5 | 12.0 | 10.0 |
| Boss arena (large enemies) | 3.0–5.0 | 6.0 | 6.0 |

**Frame Interval tips:**
- **1** — Smoothest separation, highest CPU cost. Use for <200 enemies.
- **2** — Half the cost, barely visible difference. Good for 200–500 enemies.
- **4** — Quarter cost, slight "pop" on rapid overlaps. For 500+ enemies.

**Algorithm:** The system uses a cell-based spatial hash (cell size = `SeparationRadius * 2`). Each enemy checks its 3x3 cell neighborhood. Push strength uses linear falloff: `1 - (distance / SeparationRadius)`. Only horizontal (XZ) separation is applied — Y axis is ignored.

If no `Enemy Separation Config` singleton exists in the subscene, the system uses the defaults listed above.

---

### Detection Settings (Vision Settings)

Configures the AI detection/vision system, including the frame-spreading optimization that prevents all sensors from firing on the same frame.

**Menu Path:** `Add Component > DIG > Detection > Detection Settings`

| Property | Type | Default | Range | Description |
|----------|------|---------|-------|-------------|
| **Global Update Interval** | float | 0.2 | 0.05–2.0 | Default scan interval for sensors that don't override it (seconds). |
| **Memory Duration** | float | 5.0 | 0.0–30.0 | How long a sensor remembers a target after losing sight (seconds). |
| **Max Raycasts Per Frame** | int | 64 | 1–256 | Maximum occlusion raycasts per frame across all sensors. |
| **Enable Stealth Modifiers** | bool | true | — | Master toggle for stealth modifier application. |

#### Sensor Spread Frames

The ECS component `VisionSettings` includes a `SensorSpreadFrames` field (default: 10) that is **not exposed in the Inspector** — it uses the default value of 10 at runtime.

**How it works:** With `SensorSpreadFrames = 10` and 100 sensors, only ~10 sensors execute per frame. Each sensor's entity index determines its slot:

```
Sensor runs when: entityIndex % SensorSpreadFrames == frameCount % SensorSpreadFrames
```

This prevents "thundering herd" where all 100+ sensors fire OverlapSphere on the same frame.

| Sensor Count | Spread Frames | Sensors/Frame | Effect |
|-------------|---------------|---------------|--------|
| 50 | 10 | ~5 | Smooth, low cost |
| 100 | 10 | ~10 | Good balance |
| 500 | 10 | ~50 | May want to increase spread or reduce Max Raycasts |

**Tip:** If you have 500+ enemies and detection feels expensive, reduce `Max Raycasts Per Frame` to 32 or increase the code-level `SensorSpreadFrames` value.

---

## Automatic Systems (No Setup Required)

These systems run automatically and require no authoring or Inspector configuration:

### Enemy Collision Filter Enforcement

**System:** `EnemyCollisionFilterSystem`

At spawn, every enemy entity with `AIBrain` + `PhysicsCollider` gets its collision filter enforced:
- **BelongsTo:** `Creature` (bit 8 only)
- **CollidesWith:** Player, Environment, Ship, Hazards, Default — but **not Creature**

This ensures enemies never generate physics contact pairs with each other, regardless of what the prefab's `PhysicsShapeAuthoring` says.

**Important for prefab authors:** You do not need to manually set collision filters on enemy prefabs. The system overrides them at runtime. However, if `BelongsTo` is set to `Everything` (all bits) on the PhysicsShapeAuthoring, it will work but is wasteful — every physics query in the game matches against these entities. Set `BelongsTo = Creature` in the authoring component to avoid this.

---

### Client Enemy Physics Stripping

**System:** `ClientEnemyPhysicsOptimizationSystem`

On remote clients (ClientSimulation world only), enemy ghost entities get their `CollidesWith` mask reduced to `PlayerProjectile` only. This:
- **Eliminates** all solver contacts for enemies on remote clients
- **Preserves** broadphase presence so hitscan raycasts and targeting still work
- Reduces client physics cost dramatically with many visible enemies

No configuration needed — runs automatically for all enemy ghosts with `ShowHealthBarTag`.

---

### Tick Rate Configuration

**System:** `TickRateConfigSystem`

Configures the NetCode simulation tick rate on the server:

| Setting | Value | Purpose |
|---------|-------|---------|
| Simulation Tick Rate | 30 Hz | Half the default 60Hz — halves physics step count |
| Network Tick Rate | 30 Hz | Matches simulation rate |
| Max Simulation Steps/Frame | 4 | Allows server to catch up at low FPS |
| Max Step Batch Size | 4 | Groups catch-up ticks for efficiency |

**Why 30Hz:** At 60Hz with 100+ enemies, the server could only run 1 tick/frame and fell behind real time, causing rubber-banding. 30Hz + MaxSteps=4 allows the server to process up to 4 ticks/frame when falling behind.

No Inspector configuration — values are set in code.

---

### Physics Optimization System

**System:** `PhysicsOptimizationSystem`

Reads the `PhysicsConfig` singleton (from `Physics Config` authoring) at world initialization and applies settings to `PhysicsStep`:
- Solver iteration count
- Incremental dynamic broadphase
- Incremental static broadphase

If no `PhysicsConfig` singleton exists, the system applies safe defaults (4 iterations, incremental broadphase enabled).

Runs once at startup, then disables itself.

---

## Profiling

EPIC 15.23 includes profiler markers for performance analysis in the Unity Profiler:

| Marker | System | What It Measures |
|--------|--------|-----------------|
| `AI.EnemySeparation` | EnemySeparationSystem | Spatial hash build + separation pass |
| `AI.EnemyCollisionFilter` | EnemyCollisionFilterSystem | Collision filter enforcement on new enemies |

Open the Unity Profiler (`Window > Analysis > Profiler`) and search for these markers in the CPU timeline to verify performance.

---

## Enemy Prefab Checklist

When creating new enemy prefabs, verify these settings for optimal physics performance:

| Setting | Location | Correct Value | Why |
|---------|----------|---------------|-----|
| **BelongsTo** | PhysicsShapeAuthoring > Collision Filter | `Creature` (bit 8) | Prevents matching all physics queries |
| **CollidesWith** | PhysicsShapeAuthoring > Collision Filter | Player, Environment, Default | Creature bit excluded |
| **Motion Type** | PhysicsBodyAuthoring | Kinematic | Enemies use direct position writes, not velocity integration |
| **AIBrain** | Root entity | Present | Required for filter enforcement and separation queries |

**Tip:** Even if you set BelongsTo/CollidesWith incorrectly on the prefab, the `EnemyCollisionFilterSystem` will fix it at runtime. But setting it correctly in the prefab avoids wasted broadphase matches during the first few frames before the system runs.

---

## Troubleshooting

| Issue | Check |
|-------|-------|
| Enemies stack on top of each other | Verify `Enemy Separation Config` exists in subscene. Increase `Separation Radius` or `Separation Weight` |
| Enemies pop/teleport apart | Reduce `Max Separation Speed`. Increase `Frame Interval` for smoother spread |
| Physics still expensive with many enemies | Check Profiler for `Physics.StepSimulation`. Ensure `PhysicsConfig` singleton is baked. Verify enemy BelongsTo is not `Everything` |
| Server rubber-banding at low FPS | `TickRateConfigSystem` should set 30Hz + MaxSteps=4. Check with Profiler that server ticks don't exceed frame budget |
| Detection feels delayed with 500+ enemies | Reduce `Max Raycasts Per Frame` to 32. Detection still spreads across frames via SensorSpreadFrames |
| Remote client laggy with many enemies | `ClientEnemyPhysicsOptimizationSystem` should strip solver contacts. Verify enemy ghosts have `ShowHealthBarTag` |
| Enemies clip through walls | Solver iterations may be too low. Increase `Solver Iteration Count` to 4–6 |
| New enemy prefab ignores collision filter | Ensure it has `AIBrain` component. The filter system queries `AIBrain + PhysicsCollider` |

---

## File Reference

| File | Purpose |
|------|---------|
| `Assets/Scripts/Core/Physics/PhysicsConfigAuthoring.cs` | Authoring — solver iterations, broadphase (designer-facing) |
| `Assets/Scripts/Core/Physics/PhysicsOptimizationSystem.cs` | Applies PhysicsConfig to PhysicsStep at init |
| `Assets/Scripts/Core/Physics/ClientEnemyPhysicsOptimizationSystem.cs` | Strips enemy physics on remote clients |
| `Assets/Scripts/AI/Authoring/EnemySeparationConfigAuthoring.cs` | Authoring — separation radius, weight, speed (designer-facing) |
| `Assets/Scripts/AI/Components/EnemySeparationConfig.cs` | ECS component for separation settings |
| `Assets/Scripts/AI/Systems/EnemySeparationSystem.cs` | O(n) spatial-hash anti-stacking |
| `Assets/Scripts/AI/Systems/EnemyCollisionFilterSystem.cs` | Enforces BelongsTo/CollidesWith on enemies at spawn |
| `Assets/Scripts/AI/Profiling/AIProfilerMarkers.cs` | Profiler markers for performance analysis |
| `Assets/Scripts/Player/Components/CollisionLayers.cs` | Collision layer definitions (CreatureCollidesWith excludes Creature) |
| `Assets/Scripts/Vision/Authoring/VisionSettingsAuthoring.cs` | Authoring — detection scan intervals, raycast budget (designer-facing) |
| `Assets/Scripts/Vision/Components/VisionSettings.cs` | ECS component with SensorSpreadFrames |
| `Assets/Scripts/Vision/Systems/DetectionSystem.cs` | AI detection with frame-spread optimization |
| `Assets/Scripts/Systems/Network/TickRateConfigSystem.cs` | Server tick rate configuration (30Hz) |

---

## Best Practices

1. **Always place Physics Config in the subscene** — Without it, the system uses safe defaults, but explicit configuration is better for predictability
2. **Set BelongsTo correctly on prefabs** — Even though the runtime system fixes it, correct prefab values avoid wasted broadphase matches in the first frames
3. **Tune separation in Play Mode** — Adjust `Separation Radius` and `Separation Weight` while watching enemies. The values feel different at different enemy densities
4. **Use Frame Interval for large encounters** — 500+ enemies benefit from running separation every 2nd or 4th frame
5. **Profile regularly** — Check `Physics.StepSimulation` and `AI.EnemySeparation` in the Profiler after changing enemy counts or solver settings
6. **Don't over-reduce solver iterations** — Values below 2 can cause player-environment penetration. The savings from 4→2 are modest compared to the creature-creature removal
7. **Keep Max Raycasts Per Frame reasonable** — 64 is fine for 100 enemies. For 500+, reduce to 32 and let the spread system handle distribution
