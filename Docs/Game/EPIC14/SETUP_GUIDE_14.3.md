# EPIC 14.3 Setup Guide: Arena Definition & Hazard Placement

**Status:** Planned
**Requires:** EPIC 14.1 (BossDefinitionSO references ArenaDefinitionSO); Framework: Combat/ (EncounterState)

---

## Overview

Each boss fights in a unique arena that is itself a gameplay element. Arenas contain environmental hazards (damage zones, knockback fields, rising water, collapsing floors) that affect both the player and the boss, plus interactable objects (cover, traps, levers, explosive barrels). Arenas can reconfigure between boss phases via layout variants. This guide covers creating arena assets, placing hazards in subscenes, and configuring layout transitions.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| BossDefinitionSO (14.1) | Boss definition referencing this arena | ArenaDefinition field |
| Arena subscene | Unity subscene for the arena geometry | Contains all arena entities |
| ArenaComponents.cs | ECS components | ArenaState, ArenaHazardElement, ArenaInteractableElement |

### New Setup Required

1. Create `ArenaDefinitionSO` via `Assets > Create > Hollowcore/Boss/Arena Definition`.
2. Build the arena geometry in a dedicated subscene.
3. Place hazard trigger volumes matching `ArenaHazardDefinition` positions.
4. Place interactable prefabs matching `ArenaInteractableDefinition` positions.
5. Add `ArenaAuthoring` to the arena root entity.
6. Create `ArenaLayoutSO` variants for phase-based reconfiguration.
7. Reference the `ArenaDefinitionSO` from the boss's `BossDefinitionSO.ArenaDefinition`.

---

## 1. Creating an Arena Definition Asset

**Create:** `Assets > Create > Hollowcore/Boss/Arena Definition`
**Recommended location:** `Assets/Data/Boss/Arenas/`
**Naming convention:** `Arena_[DistrictName]_[ArenaName].asset` (e.g., `Arena_Necrospire_CathedralOfScreens.asset`)

### 1.1 Identity & Layout

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `ArenaId` | Unique integer identifier | 0 | Sequential, matching boss ArenaId references |
| `ArenaName` | Display name | empty | For debug overlay and workstation |
| `Description` | Arena theme/lore | empty | |
| `ArenaPrefab` | Scene/prefab for arena geometry | null | The subscene root or prefab containing walls, floor, lighting |
| `Layouts` | List of `ArenaLayoutSO` | empty | At least one layout required. Multiple for phase reconfiguration |

### 1.2 Bounds Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `ArenaBounds` | Bounding box -- entities outside are pushed back or take damage | -- | Size per axis: min 5m, max 500m. Volume: warn if < 500m^3 or > 500,000m^3 |
| `KillPlaneY` | Y coordinate below which entities are destroyed | -50 | Must be at least 5m below `ArenaBounds.min.y` |

**Tuning tip:** Set ArenaBounds to tightly wrap the playable area. The bounds enforcement system will push entities back when they approach the edge. KillPlaneY catches entities that fall through geometry.

### 1.3 Hazard Definitions

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `Hazards` | List of `ArenaHazardDefinition` | empty | Up to 16 hazards per arena (ushort bitmask) |

**Per-hazard fields:**

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `HazardName` | Debug label | empty | |
| `HazardType` | `ArenaHazardType` enum | DamageZone | See hazard type table |
| `Position` | Center in arena-local space | (0,0,0) | Must be inside ArenaBounds |
| `Extents` | Radius or half-extents | (1,1,1) | Warn if extends outside ArenaBounds |
| `DamagePerSecond` | DPS for damage-type hazards | 0 | Warn if > 500 (kills player in < 2s) or == 0 for damage types |
| `IsCyclic` | Toggles on/off on timer | false | |
| `CycleOnDuration` | Seconds hazard is active per cycle | 3.0 | Must be > 0 if IsCyclic. Warn if > 10s |
| `CycleOffDuration` | Seconds hazard is inactive per cycle | 5.0 | Must be > 0 if IsCyclic. Warn if < 1s (no safe window) |
| `ActiveInLayouts` | Which layout indices activate this hazard (-1 = all) | [-1] | Must be valid layout indices or -1 |

**Hazard Type Reference:**

| Type | Effect | Notes |
|------|--------|-------|
| `DamageZone` (0) | Periodic damage while standing in area | Most common type |
| `KnockbackZone` (1) | Pushes entities away from center | Useful near edges/pits |
| `SlowZone` (2) | Reduces movement speed | Tactical positioning |
| `FallingPlatform` (3) | Collapses after weight timer | One-shot, platform entity disabled |
| `RisingWater` (4) | Water level rises, drowning damage | Escalating danger over time |
| `MovingWall` (5) | Walls shift, crushing damage on contact | Requires moving entities |
| `HeatVent` (6) | Periodic burst of heat damage | Cone or directional |
| `ElectricField` (7) | Continuous lightning damage | Visual-heavy |
| `CollapsingFloor` (8) | Floor sections break progressively | Arena shrinks over time |
| `GravityShift` (9) | Gravity direction changes | Most complex, Skyfall arena |

### 1.4 Interactable Definitions

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `Interactables` | List of `ArenaInteractableDefinition` | empty | Up to 16 (ushort bitmask) |

**Per-interactable fields:**

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `InteractableName` | Debug label | empty | |
| `InteractableType` | `ArenaInteractableType` enum | Cover | See interactable type table |
| `Prefab` | World-space prefab to spawn | null | Must have interaction component |
| `Position` | Arena-local position | (0,0,0) | Must be inside ArenaBounds |
| `MaxUses` | Uses before depletion (-1 = infinite) | 1 | |
| `Cooldown` | Seconds between uses | 0 | |
| `ActiveInLayouts` | Layout indices or -1 for all | [-1] | |

---

## 2. Arena Subscene Construction

### 2.1 Subscene Structure

```
ArenaRoot (Entity)
  ├── ArenaAuthoring (bakes ArenaState + buffers)
  ├── Geometry (static meshes, floor, walls, ceiling)
  ├── Hazards/
  │   ├── DamageZone_01 (trigger collider + HazardAuthoring)
  │   ├── HeatVent_01 (trigger collider + HazardAuthoring)
  │   └── ...
  ├── Interactables/
  │   ├── Cover_01 (interactable prefab instance)
  │   ├── ExplosiveBarrel_01 (interactable prefab instance)
  │   └── ...
  ├── SpawnPoints/
  │   ├── BossSpawnPoint
  │   ├── PlayerEntryPoint
  │   └── ReinforcementSpawnPoints (3-5 positions)
  └── EncounterTrigger (trigger volume at entrance)
```

### 2.2 Hazard Placement

For each hazard in the ArenaDefinitionSO:
1. Create an empty GameObject at the specified Position.
2. Add a trigger collider matching the Extents (BoxCollider for rectangular zones, SphereCollider for radial).
3. Add `ArenaHazardAuthoring` component and configure to match the ArenaHazardDefinition.
4. Set collision layer to "ArenaHazard".

**Tuning tip:** Hazards damage the boss too. Place hazards such that the boss AI can use or avoid them tactically. This creates emergent gameplay where players can lure the boss into its own hazards.

---

## Scene & Subscene Checklist

- [ ] Arena subscene created with ArenaRoot entity
- [ ] ArenaAuthoring component on root entity references ArenaDefinitionSO
- [ ] All hazard trigger volumes placed at correct positions
- [ ] All interactable prefabs placed at correct positions
- [ ] Encounter trigger volume at arena entrance
- [ ] Boss spawn point positioned
- [ ] Player entry point positioned
- [ ] Reinforcement spawn points placed (3-5 minimum)
- [ ] ArenaBounds set tightly around playable area
- [ ] KillPlaneY set at least 5m below arena floor
- [ ] At least one ArenaLayoutSO defined
- [ ] ArenaDefinitionSO referenced from BossDefinitionSO.ArenaDefinition

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Hazard position outside ArenaBounds | Validator warning, hazard unreachable | Reposition inside bounds |
| Interactable position outside ArenaBounds | Validator error, unreachable object | Reposition inside bounds |
| KillPlaneY >= ArenaBounds.min.y | Validator error, entities killed on arena floor | Lower KillPlaneY by at least 5m |
| IsCyclic = true but CycleOnDuration = 0 | Hazard never activates | Set CycleOnDuration > 0 |
| ActiveInLayouts references invalid layout index | Validator error | Use valid layout indices (0 to Layouts.Count-1) or -1 |
| No layouts defined | Validator error | Add at least one ArenaLayoutSO |
| Hazard colliders not set to trigger | Physics collision instead of overlap detection | Set `Is Trigger = true` on all hazard colliders |
| DamagePerSecond = 0 on DamageZone | Hazard exists but deals no damage | Set a meaningful DPS value |

---

## Verification

- [ ] ArenaState initializes correctly from ArenaDefinitionSO
- [ ] ArenaHazardElement buffer populates with all defined hazards
- [ ] Cyclic hazards toggle on/off with correct timing
- [ ] DamageZone hazards deal damage to player
- [ ] DamageZone hazards deal damage to boss (not just player)
- [ ] Arena layout changes when CurrentLayoutIndex updates
- [ ] Hazards activate/deactivate based on layout transitions
- [ ] Interactables respond to player interaction
- [ ] Lever interactables toggle linked hazards
- [ ] ExplosiveBarrel deals AOE damage and destroys itself
- [ ] Arena bounds push entities back or deal damage at edges
- [ ] Kill plane destroys entities below threshold
- [ ] ArenaInteractableElement.UsesRemaining depletes correctly
- [ ] Cooldown prevents rapid re-use of interactables
