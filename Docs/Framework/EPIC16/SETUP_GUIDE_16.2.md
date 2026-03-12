# SETUP GUIDE 16.2: Swarm Entity Framework

**Status:** Partially Implemented (Phases 1-4 + Phase 8 Setup Wizard)
**Last Updated:** February 22, 2026
**Requires:** Enemy prefab with AIBrain (EPIC 15.31), SubScene for ECS authoring

This guide covers Unity Editor setup for the swarm entity framework. After setup, thousands of lightweight particle enemies flow toward players via a shared flow field, seamlessly promoting to full combat entities when close enough to fight.

---

## What Changed

Previously, every enemy was a full ECS entity with 20+ components, physics body, hitbox children, and individual AI brain — limiting practical enemy counts to ~200. The swarm framework introduces a three-tier system where only enemies near the player run full simulation.

**Three tiers:**

| Tier | What It Is | Components | Physics | Rendering | Typical Count |
|------|-----------|-----------|---------|-----------|---------------|
| **Particle** | Flow-field follower | 2 (SwarmParticle, SwarmAnimState) | None | GPU Instanced (VAT) | 1,000–49,000+ |
| **Aware** | Near-player agent | +2 (SwarmAgent, SwarmGroupID) | None | GPU Instanced (VAT) | Up to 100 |
| **Combat** | Full entity | Health, AIBrain, abilities, capsule collider | Kinematic | Skinned mesh | Up to 20 |

Promotion and demotion happen automatically based on distance to players.

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Flow field builds from player positions | `FlowFieldBuildSystem` BFS flood-fill every 0.5s, all players as sources |
| Particle movement | Flow field sampling + Perlin noise for natural-looking flow |
| Separation | Spatial hash grid every 4th frame prevents stacking |
| Tier promotion/demotion | `SwarmTierEvaluationSystem` runs every 4th frame, distance-based with hysteresis |
| Animation | Speed-based clip selection (Idle/Walk/Run) baked into movement system |
| LOD rendering | 3 tiers (Full/Reduced/Billboard) based on camera distance |
| Existing enemies unaffected | Swarm systems query `SwarmParticle`/`SwarmAgent` — existing AI queries exclude them entirely |

---

## Quick Start: Setup Wizard

The fastest way to set up a swarm is the **Setup Wizard**.

**Menu:** DIG > Swarm > Setup Wizard

1. Open a SubScene for editing
2. Open **DIG > Swarm > Setup Wizard**
3. The wizard auto-detects your combat prefab (BoxingJoe_ECS or fallback)
4. Configure grid size, population, and tuning in the wizard panels
5. Click **Create Swarm Setup** — it creates all three required GameObjects in the SubScene plus the render config in the main scene
6. Supports full Undo

The wizard creates:
- **SwarmFlowField** — flow field grid authoring
- **SwarmConfig** — global swarm tuning singleton
- **SwarmSpawner** — population spawner
- **SwarmRenderConfig** (main scene) — mesh/material/VAT references for GPU instancing

---

## 1. Flow Field Setup

The flow field is a 2D grid of direction vectors pointing toward players. All particles sample this grid to determine movement direction.

### 1.1 Add the Component

1. In your gameplay SubScene, create an empty GameObject named "SwarmFlowField"
2. Click **Add Component** > search for **Flow Field Authoring**

### 1.2 Inspector Fields

#### Grid Dimensions

| Field | Description | Default | Notes |
|-------|-------------|---------|-------|
| **Grid Width** | Number of cells along X axis | 100 | Coverage = Width * CellSize meters |
| **Grid Height** | Number of cells along Z axis | 100 | Coverage = Height * CellSize meters |
| **Cell Size** | Meters per cell | 2.0 | Smaller = more precise but more memory/CPU |

#### Update

| Field | Description | Default |
|-------|-------------|---------|
| **Update Interval** | Seconds between flow field rebuilds | 0.5 |

#### Debug

| Field | Description | Default |
|-------|-------------|---------|
| **Show Gizmos** | Draw flow field in Scene view | false |
| **Gizmo Arrow Length** | Length of direction arrows | 0.8 |

### 1.3 Scene Gizmos

When **Show Gizmos** is enabled and in Play Mode, the Scene view shows:
- Grid bounds (yellow wireframe)
- Flow direction arrows color-coded: red (close to player) to blue (far from player)
- Grid lines (downsampled to 50x50 max for performance)

### 1.4 Sizing Guide

| Arena Size | Grid Width | Grid Height | Cell Size | Total Cells |
|-----------|-----------|------------|-----------|-------------|
| Small (100m) | 50 | 50 | 2.0 | 2,500 |
| Medium (200m) | 100 | 100 | 2.0 | 10,000 |
| Large (400m) | 200 | 200 | 2.0 | 40,000 |
| Huge (1km) | 200 | 200 | 5.0 | 40,000 |

> The grid is centered at the authoring GameObject's world position. Particles outside the grid move in their last known direction.

---

## 2. Swarm Config (Global Singleton)

### 2.1 Add the Component

1. Create an empty GameObject in your SubScene named "SwarmConfig"
2. Click **Add Component** > search for **Swarm Config Authoring**

### 2.2 Inspector Fields

#### Prefabs

| Field | Description | Default |
|-------|-------------|---------|
| **Particle Prefab** | Prefab for GPU-instanced particles (optional, can be null if using VAT only) | — |
| **Combat Prefab** | Full enemy prefab instantiated on promotion to combat tier (e.g., BoxingSwarm) | — |

#### Movement

| Field | Description | Default |
|-------|-------------|---------|
| **Base Speed** | Base movement speed (m/s) for particles | 3.5 |
| **Speed Variance** | Random speed variation per particle | 0.8 |

#### Tier Thresholds

| Field | Description | Default |
|-------|-------------|---------|
| **Aware Range** | Distance to player for Particle → Aware promotion | 30m |
| **Combat Range** | Distance to player for Aware → Combat promotion | 8m |
| **Demote Range** | Distance from player for Combat → Particle demotion | 15m |
| **Aware Hysteresis** | Extra buffer distance before demoting from Aware | 5m |

#### Hard Caps

| Field | Description | Default |
|-------|-------------|---------|
| **Max Combat Entities** | Maximum simultaneously promoted combat entities | 20 |
| **Max Aware Entities** | Maximum simultaneously promoted aware agents | 100 |

#### Flocking

| Field | Description | Default |
|-------|-------------|---------|
| **Separation Radius** | Min distance between particles | 0.5m |
| **Separation Weight** | Push-apart strength | 1.0 |
| **Cohesion Weight** | Group-toward strength | 0.3 |
| **Alignment Weight** | Match-velocity strength | 0.2 |
| **Flow Field Weight** | Flow direction adherence | 2.0 |

#### Noise

| Field | Description | Default |
|-------|-------------|---------|
| **Noise Scale** | Perlin noise frequency (smaller = larger patterns) | 0.1 |
| **Noise Strength** | How much noise affects movement direction | 0.5 |

#### Performance

| Field | Description | Default |
|-------|-------------|---------|
| **Tier Eval Frame Interval** | Evaluate promotions/demotions every N frames | 4 |

#### Combat (Promoted Entities)

| Field | Description | Default |
|-------|-------------|---------|
| **Combat Melee Range** | Attack range for promoted swarm entities | 2.0m |
| **Combat Chase Speed** | Chase speed for promoted entities | 5.0 |
| **Combat Damage** | Melee damage per hit | 10 |
| **Combat Attack Cooldown** | Seconds between attacks | 1.5 |

---

## 3. Spawner Setup

### 3.1 Add the Component

1. Create an empty GameObject in your SubScene named "SwarmSpawner"
2. Click **Add Component** > search for **Swarm Spawner Authoring**

### 3.2 Inspector Fields

#### Spawn Mode

| Field | Description | Default |
|-------|-------------|---------|
| **Mode** | How particles are placed: Area (random in radius), Edge (along grid perimeter), Continuous (maintains population) | Continuous |

#### Population

| Field | Description | Default |
|-------|-------------|---------|
| **Total Particles** | Initial spawn count (Area/Edge modes) | 1,000 |
| **Target Population** | Maintained count (Continuous mode) — respawns killed particles | 1,000 |
| **Spawn Rate** | Particles spawned per second (Continuous mode) | 200 |

#### Performance

| Field | Description | Default |
|-------|-------------|---------|
| **Batch Size** | Max particles spawned per frame | 250 |

#### Placement

| Field | Description | Default |
|-------|-------------|---------|
| **Spawn Radius** | Radius for Area mode scatter | 50m |
| **Edge Inset** | Distance from grid edge for Edge mode | 5m |

#### Trigger

| Field | Description | Default |
|-------|-------------|---------|
| **Spawn On Start** | Begin spawning immediately on play | true |
| **Seed** | Random seed (0 = derive from entity index) | 0 |

### 3.3 Scene Gizmos

- Area mode: yellow sphere gizmo showing spawn radius
- Handles label showing mode, count, and rate summary

---

## 4. Render Config (Main Scene — NOT SubScene)

The render config is a **MonoBehaviour** that lives in the main scene, not in a SubScene. It holds mesh/material/VAT references that the GPU instancing system reads.

### 4.1 Add the Component

1. In your **main scene** (not SubScene), create an empty GameObject named "SwarmRenderConfig"
2. Click **Add Component** > search for **Swarm Render Config Managed**

> The Setup Wizard creates this automatically.

### 4.2 Inspector Fields

#### Mesh LODs

| Field | Description | Required |
|-------|-------------|----------|
| **Full Mesh** | High-detail mesh for close-range particles | Yes |
| **Reduced Mesh** | Low-poly mesh for medium distance (null = use FullMesh) | No |
| **Billboard Mesh** | Billboard quad for far distance (null = use FullMesh) | No |

#### Materials

| Field | Description | Required |
|-------|-------------|----------|
| **Swarm Material** | Material using `DIG/SwarmVAT` shader | Yes |
| **Billboard Material** | Material for billboard LOD (null = use SwarmMaterial) | No |

#### VAT (Vertex Animation Texture)

| Field | Description | Default |
|-------|-------------|---------|
| **Position VAT** | Texture2D containing baked vertex positions per frame | — |
| **Normal VAT** | Texture2D containing baked vertex normals per frame | — |

#### Animation

| Field | Description | Default |
|-------|-------------|---------|
| **Clip Frame Counts** | Frames per animation clip [Idle, Walk, Run, Attack, Die] | {30, 24, 18, 12, 8} |
| **VAT Frame Rate** | Playback frame rate | 30 |
| **Total VAT Frames** | Total frames across all clips | 92 |

#### Rendering

| Field | Description | Default |
|-------|-------------|---------|
| **Max Render Distance** | Maximum particle render distance | 200m |
| **LOD Distance 1** | Full → Reduced LOD transition | 30m |
| **LOD Distance 2** | Reduced → Billboard LOD transition | 80m |
| **Shadow Distance** | Max distance for shadow casting | 30m |
| **Cast Shadows** | Enable shadow casting | true |

---

## 5. Combat Prefab Setup

The combat prefab is what gets instantiated when a particle promotes to combat tier. It should be a stripped-down enemy with minimal components.

### 5.1 Requirements

The combat prefab needs:
- `AIBrain` with `Archetype = Swarm` (gates swarm-specific combat behavior)
- `Health` + `DeathState` + `DamageResistance`
- `AbilityExecutionState` + at least one `AbilityDefinition` (single melee ability)
- Kinematic `PhysicsBody` + capsule `PhysicsShape`
- Skinned mesh renderer

### 5.2 What It Does NOT Need

Unlike full enemies, swarm combat entities skip:
- Hitbox child entities (single capsule collider, no split Head/Torso)
- Multiple abilities (one melee ability is sufficient)
- Shield, blocking, or complex combat state
- Aggro/threat system (SwarmCombatBehaviorSystem handles targeting directly)

> **BoxingSwarm.prefab** exists at `Assets/Prefabs/BoxingSwarm.prefab` as a reference implementation.

---

## 6. SwarmVAT Shader

The particle rendering uses `DIG/SwarmVAT` — a surface shader with GPU instancing that samples vertex animation textures.

### 6.1 Material Setup

1. Create a new Material
2. Set shader to **DIG/SwarmVAT**
3. Assign your base albedo texture to `_MainTex`
4. Assign position VAT to `_PositionVAT` and normal VAT to `_NormalVAT`
5. Set `_VATFrameCount` and `_VATVertexCount` to match your baked data
6. Set `_BoundsMin` and `_BoundsMax` to the bounding box used during VAT baking
7. **Enable GPU Instancing** on the material

> The Setup Wizard clones the combat prefab's material and enables instancing automatically.

---

## 7. After Setup: Reimport SubScene

After placing or modifying swarm authoring components:

1. Right-click the SubScene > **Reimport**
2. Wait for baking to complete

---

## 8. Tuning Guide

### Small Horde (500–1,000)

| Setting | Value |
|---------|-------|
| Target Population | 500–1,000 |
| Grid Size | 50x50, CellSize 2.0 |
| Max Combat | 10 |
| Max Aware | 50 |

### Medium Horde (5,000–10,000)

| Setting | Value |
|---------|-------|
| Target Population | 5,000–10,000 |
| Grid Size | 100x100, CellSize 2.0 |
| Max Combat | 20 |
| Max Aware | 100 |
| Tier Eval Interval | 4 |

### Massive Horde (50,000+)

| Setting | Value |
|---------|-------|
| Target Population | 50,000 |
| Grid Size | 200x200, CellSize 2.0 |
| Max Combat | 20 |
| Max Aware | 100 |
| Tier Eval Interval | 8 |
| Batch Size | 500 |

---

## 9. Implementation Status

| Feature | Status |
|---------|--------|
| Flow field (BFS flood-fill, direction build) | Implemented |
| Particle spawning (Area/Edge/Continuous) | Implemented |
| Particle movement (flow field + noise) | Implemented |
| Separation (spatial hash, every 4th frame) | Implemented |
| Tier promotion/demotion (Particle↔Aware↔Combat) | Implemented |
| Combat behavior (chase + melee for promoted entities) | Implemented |
| GPU instanced rendering (VAT, 3 LOD tiers) | Implemented |
| Area damage (SwarmDamageZone → particle kill) | Implemented |
| Setup Wizard editor tool | Implemented |
| Profiler markers (11 markers) | Implemented |
| Obstacle cost integration (terrain/voxel) | Partial (field exists, no sampling) |
| Death VFX (SwarmDeathVFXSystem) | Not yet |
| Networking (flow field replication, RPCs) | Not yet |
| Formations/emergent behaviors | Not yet (component types defined) |
| VAT Baker editor tool | Not yet |

---

## 10. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Particles spawn | Enter Play Mode with swarm setup | Particles appear and flow toward player |
| 3 | Flow field gizmos | Enable Show Gizmos on FlowFieldAuthoring | Arrows point toward player positions |
| 4 | Promotion to Aware | Walk near particles (~30m) | Some particles gain individual flow targets |
| 5 | Promotion to Combat | Walk very close (~8m) | Full enemy entities spawn, begin attacking |
| 6 | Demotion | Walk away from combat entities (>15m) | Entities revert to particles |
| 7 | Max caps | Approach large group | Never exceed MaxCombatEntities or MaxAwareEntities |
| 8 | Separation | Observe particle crowd | No stacking — particles push apart |
| 9 | LOD rendering | Zoom camera in/out | Full mesh → reduced → billboard transitions |
| 10 | Continuous respawn | Kill some promoted enemies, wait | Population replenishes via spawner |

---

## 11. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| No particles visible | SwarmRenderConfigManaged missing from main scene | Add to a scene GameObject (not SubScene) |
| Particles don't move | Flow field not built (no players in world) | Ensure at least one entity has PlayerTag |
| Particles all go same direction | Grid too small for arena | Increase GridWidth/GridHeight or decrease CellSize |
| Promotion never happens | AwareRange too small or MaxCombatEntities = 0 | Check SwarmConfig thresholds |
| Combat entities don't attack | Combat prefab missing AIBrain Archetype=Swarm | Set archetype on the prefab's AIBrain |
| Performance drops with 50k+ | Grid rebuild too frequent or separation too costly | Increase UpdateInterval, increase TierEvalFrameInterval |
| VAT animation looks wrong | Frame counts or bounds don't match baked data | Verify ClipFrameCounts, BoundsMin/Max match your VAT export |
| Material is pink | Shader not found or instancing disabled | Set shader to DIG/SwarmVAT, enable GPU Instancing on material |

---

## 12. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| AI Brain, patrol, combat behavior | SETUP_GUIDE_15.31 |
| Ability system, ability execution | SETUP_GUIDE_15.32 |
| Enemy death lifecycle, corpses | SETUP_GUIDE_16.3 |
| Physics optimization, collision filters | SETUP_GUIDE_15.23 |
| **Swarm entity framework** | **This guide (16.2)** |
