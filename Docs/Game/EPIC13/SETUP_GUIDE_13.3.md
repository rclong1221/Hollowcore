# EPIC 13.3 Setup Guide: Faction Enemy Roster, Spawn Tables & Prefab Requirements

**Status:** Planned
**Requires:** 13.1 (DistrictDefinitionSO, FactionDefinitionSO), Framework: AI/ (AIBrain, EncounterPoolSO), Loot/ (LootTableSO), Roguelite/ (SpawnDirectorSystem)

---

## Overview

Configure the per-district enemy faction pipeline: author FactionDefinitionSO assets with enemy rosters, create enemy prefabs with the required component stack, set up EncounterPoolSOs for the spawn director, and configure Front phase scaling and Strife mutations.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| DistrictDefinitionSO | EPIC 13.1 | References 4 FactionDefinitionSOs |
| Framework AI/ | AIBrainAuthoring, AbilityProfileSO | Enemy behavior |
| Framework Loot/ | LootTableSO | Per-enemy loot drops |
| Framework Roguelite/ | EncounterPoolSO, SpawnDirectorSystem | Runtime spawning |

### New Setup Required

| Asset | Location | Type |
|-------|----------|------|
| FactionEnums.cs | `Assets/Scripts/District/Definitions/` | C# (enums) |
| FactionDefinitionSO.cs | `Assets/Scripts/District/Definitions/` | C# (ScriptableObject) |
| FactionComponents.cs | `Assets/Scripts/District/Components/` | C# (ECS) |
| FactionSpawnStampSystem.cs | `Assets/Scripts/District/Systems/` | C# (ISystem) |
| FrontPhaseScalingSystem.cs | `Assets/Scripts/District/Systems/` | C# (ISystem) |
| 4 FactionDefinitionSO assets per district | `Assets/Data/Districts/[Name]/Factions/` | ScriptableObject |
| 3-5 enemy prefabs per faction | `Assets/Prefabs/Districts/[Name]/Enemies/` | Prefab |
| 1 EncounterPoolSO per faction | `Assets/Data/Districts/[Name]/Encounters/` | ScriptableObject |

---

## 1. Create FactionDefinitionSO
**Create:** `Assets > Create > Hollowcore/District/Faction Definition`
**Recommended location:** `Assets/Data/Districts/[District]/Factions/[FactionName].asset`

### 1.1 Identity Fields
| Field | Type | Description | Required |
|-------|------|-------------|----------|
| Id | FactionId (ushort enum) | Globally unique. Pattern: district * 10 + index | YES |
| DisplayName | string | Human-readable name | YES |
| Description | string | Flavor text for UI/lore | Recommended |
| Icon | Sprite | Faction badge | Recommended |
| FactionColor | Color | Debug viz and map overlay | YES |

### 1.2 FactionId Assignment Pattern

| District | Base | Faction 0 | Faction 1 | Faction 2 | Faction 3 |
|----------|------|-----------|-----------|-----------|-----------|
| Necrospire (1) | 10 | 10 MourningCollective | 11 RecursiveSpecters | 12 ArchiveWardens | 13 TheInheritors |
| The Burn (6) | 60 | 60 SlagWalkers | 61 WasteManagement | 62 TheAshborn | 63 ScrapHives |
| The Lattice (8) | 80 | 80 TheClimbers | 81 CollapseEngineers | 82 ApexDwellers | 83 TheFoundation |

---

## 2. Author the Enemy Roster

### 2.1 FactionEnemyEntry Fields
| Field | Type | Description | Default | Range |
|-------|------|-------------|---------|-------|
| EnemyPrefab | GameObject | Reference to enemy prefab | null | Required (non-null) |
| DisplayName | string | Enemy name for UI/debug | "" | -- |
| Tier | EnemyTier | Common/Elite/Special/Miniboss/Boss | Common | Enum |
| SpawnWeight | float | Frequency within faction (higher = more common) | 1.0 | 0.1-10.0 |
| SpawnCost | int | Budget consumed by spawn director | 1 | 1-10 |
| LootTable | ScriptableObject | Drop table on death | null | Recommended |
| HasRippableLimbs | bool | Can player rip limbs (EPIC 1) | false | -- |
| RippableLimbDefinitions | ScriptableObject[] | Available limbs | [] | Required if HasRippableLimbs |

### 2.2 Recommended Roster Per Faction
| Tier | Count | SpawnWeight | SpawnCost | Description |
|------|-------|-------------|-----------|-------------|
| Common x2 | 2 | 3.0-5.0 | 1-2 | Bread-and-butter enemies |
| Elite x1 | 1 | 1.0-2.0 | 4-5 | Challenging, good loot |
| Special x1 | 1 | 0.5-1.0 | 4-6 | Unique mechanic (healer, commander, spawner) |
| (Optional) Miniboss x1 | 0-1 | 0.2 | 6-8 | Rare, high reward |

**Tuning tip:** SpawnWeight controls relative frequency. A Common with weight 5.0 spawns ~5x more often than a Special with weight 1.0, assuming the spawn director has budget for both.

---

## 3. Enemy Prefab Component Stack

Every enemy prefab MUST have these components:

| Component | Authoring | Purpose | Critical Notes |
|-----------|-----------|---------|----------------|
| AI Brain | `AIBrainAuthoring` | Behavior tree / state machine | Required for any combat behavior |
| Damageable | `DamageableAuthoring` | Health, damage events, death state | Required for taking damage |
| Hitbox Owner | `HitboxOwnerMarker` | Redirects compound collider hits | Required for hitscan resolution |
| Physics Shape | `PhysicsShapeAuthoring` | Collision, raycasts | **BelongsTo = Creature (bit 8)**, NOT Everything |
| Loot Table | `LootTableAuthoring` | Death drops | Recommended |
| Enemy Separation | `EnemySeparationConfigAuthoring` | Anti-stacking | Recommended |
| Faction Assignment | (stamped at runtime by FactionSpawnStampSystem) | Faction identity | Do NOT bake this |

### 3.1 Physics Shape Critical Settings
| Setting | Value | Why |
|---------|-------|-----|
| BelongsTo | Creature (bit 8 only) | **BelongsTo=Everything causes O(N^2) broadphase overhead** (see MEMORY.md) |
| CollidesWith | Player, PlayerProjectile, Environment | Standard enemy collision |
| Raise Collision Events | Off | Only needed for player-player |

### 3.2 Prefab Hierarchy
```
EnemyRoot (ghost prefab)
  Model (SkinnedMeshRenderer, Animator)
  Hitboxes/
    Head (BoxCollider + HitboxAuthoring, multiplier=2.0)
    Torso (BoxCollider + HitboxAuthoring, multiplier=1.0)
  AIBrainAuthoring
  DamageableAuthoring
  HitboxOwnerMarker
  PhysicsShapeAuthoring (capsule, BelongsTo=Creature)
  LootTableAuthoring
  EnemySeparationConfigAuthoring
```

---

## 4. Create EncounterPoolSO per Faction
**Create:** `Assets > Create > Hollowcore/Roguelite/Encounter Pool` (framework path)
**Recommended location:** `Assets/Data/Districts/[Name]/Encounters/[FactionName]_Pool.asset`

| Field | Description |
|-------|-------------|
| Entries | List of (EnemyPrefab, Weight, MinCount, MaxCount) per encounter |
| BudgetPerWave | Director budget for one spawn wave |
| WaveCooldown | Seconds between waves |

The `FactionEncounterPoolResolverSystem` selects which pool to use based on the current zone's PrimaryFactionIndex.

---

## 5. Configure Front Phase Scaling

### 5.1 FrontPhaseOverrides Array
On FactionDefinitionSO, set `FrontPhaseOverrides[0-3]`:

| Index | Phase | Override Effect |
|-------|-------|-----------------|
| 0 | Phase 1 | Usually null (no change), or minor buff pool |
| 1 | Phase 2 | Replace pool with harder variant (elites more frequent) |
| 2 | Phase 3 | Significant escalation (special enemies, double spawns) |
| 3 | Phase 4 | Maximum threat (all Berserker, elite-only pool) |

Null entries mean no change from the base pool for that phase.

### 5.2 FrontPhaseScalingSystem Behavior
When `DistrictState.FrontPhase` increases:
1. All alive enemies with `FactionAssignment` are queried
2. Enemies with `FrontPhaseScaled.AppliedPhase < currentPhase` get stat multipliers
3. Common enemies in Front-converted zones may be promoted to Elite tier

---

## 6. Configure Strife Mutations (Optional)

### 6.1 StrifeMutationEntry Fields
| Field | Description | Default |
|-------|-------------|---------|
| StrifeCardId | Which Strife card triggers this mutation | 0 |
| MutatedPool | EncounterPoolSO override when active | null |
| StatMultipliers | float[3]: [0]=Health, [1]=Damage, [2]=Speed | [1,1,1] |
| BehaviorOverride | AI behavior profile swap | null |

**Example:** When Strife card "Grief Amplifier" is active (StrifeCardId=5), Mourning Collective gets StatMultipliers=[1.5, 1.2, 1.0] (50% more HP, 20% more damage).

---

## Scene & Subscene Checklist

- [ ] 4 FactionDefinitionSO assets per district, all non-null in DistrictDefinitionSO.Factions
- [ ] 3-5 enemy prefabs per faction with complete component stack
- [ ] All enemy PhysicsShapeAuthoring BelongsTo = Creature (NOT Everything)
- [ ] 1 EncounterPoolSO per faction wired into spawn director
- [ ] FrontPhaseOverrides populated for phases 1-3 (phase 0 usually null)
- [ ] FactionColor distinct for each of the 4 factions per district

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| PhysicsShape BelongsTo = Everything | Massive performance drop, every physics query hits every enemy | Set BelongsTo to Creature (bit 8) only |
| Missing AIBrainAuthoring on enemy prefab | Enemies spawn but stand still, no combat behavior | Add AIBrainAuthoring with appropriate behavior profile |
| Missing DamageableAuthoring | Enemies cannot take damage, appear invincible | Add DamageableAuthoring to enemy root |
| Missing HitboxOwnerMarker | Hitscan damage resolution fails, bullets pass through | Add HitboxOwnerMarker to enemy root |
| SpawnWeight = 0 on an entry | Validator ERROR, enemy never spawns | Set SpawnWeight > 0 for all roster entries |
| FactionId collision | Wrong faction stats applied, aggro chains break | Use district-scoped IDs (districtId * 10 + index) |
| HasRippableLimbs=true but no definitions | Runtime error when player attempts limb rip | Populate RippableLimbDefinitions array |
| EncounterPool not wired to faction | Default generic pool used, faction identity lost | Create per-faction EncounterPoolSO and reference in spawn director config |

---

## Verification

- [ ] Each FactionDefinitionSO has 3-5 enemy entries with valid prefabs
- [ ] Enemy prefabs have AIBrainAuthoring, DamageableAuthoring, PhysicsShapeAuthoring, HitboxOwnerMarker
- [ ] PhysicsShapeAuthoring.BelongsTo = Creature on all enemy prefabs
- [ ] FactionSpawnStampSystem stamps FactionAssignment on every spawned enemy
- [ ] FactionEncounterPoolResolverSystem switches pool on zone transition
- [ ] Front phase 2+ applies stat scaling to existing enemies
- [ ] Elite promotion occurs in Front-converted zones
- [ ] Strife mutation replaces encounter pool when matching card active
- [ ] Loot tables produce faction-appropriate drops
- [ ] No FactionId collisions across districts
