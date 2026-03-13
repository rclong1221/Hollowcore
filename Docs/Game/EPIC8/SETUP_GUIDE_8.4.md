# EPIC 8.4 Setup Guide: Hunter Encounters

**Status:** Planned
**Requires:** EPIC 8.1 (TraceState, TraceThresholdSystem), Framework AI/, Combat/, EPIC 4 (District Graph)

---

## Overview

Hunters are elite enemies that spawn exclusively at Trace 2+. They are the teeth of the Trace system -- a tangible, dangerous consequence of accumulated heat. Each district has its own hunter variant with unique tracking behavior. Hunters scale with Trace level: solo at Trace 2, pairs at Trace 3, squads at Trace 4+. Defeating a hunter is one of the few ways to reduce Trace, creating a meaningful risk/reward decision.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 8.1 | TraceState (CurrentTrace >= 2) | Triggers hunter spawn eligibility |
| Framework AI/ | Enemy prefabs, spawn system | Base enemy infrastructure |
| Framework Combat/ | Damage pipeline, DeathTransitionSystem | Combat and death handling |
| EPIC 8.3 | TraceSinkAPI | Trace reduction on hunter defeat |

### New Setup Required
1. Create HunterDefinitionSO assets at `Assets/Data/Trace/Hunters/`
2. Create hunter prefabs at `Assets/Prefabs/Enemies/Hunters/`
3. Create `HunterVariantDatabase` ScriptableObject linking districts to hunter variants
4. Add HunterSpawnCooldown singleton to run bootstrap subscene
5. Set `HunterSpawnRatePerTrace` in TraceConfig (EPIC 8.1)

---

## 1. Creating Hunter Definitions

**Create:** `Assets > Create > Hollowcore/Trace/Hunter Definition`
**Recommended location:** `Assets/Data/Trace/Hunters/`

### 1.1 Identity Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `VariantId` | Unique ID for this hunter variant | -- | > 0, unique |
| `DisplayName` | Player-facing name (e.g., "Headhunter", "Smoke Stalker") | "" | -- |
| `Description` | Flavor text | "" | -- |
| `Portrait` | Sprite for UI/minimap | null | -- |
| `DistrictId` | Which district this variant spawns in (-1 = universal fallback) | -1 | -1 or valid district ID |

### 1.2 Base Stats
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `BaseHealth` | HP before Trace scaling | 500 | 100-2000 |
| `BaseDamage` | Damage per hit before scaling | 40 | 10-200 |
| `BaseMoveSpeed` | Movement speed | 5.0 | 2.0-10.0 |
| `BaseDetectionRange` | Vision range in meters | 25.0 | 10.0-50.0 |
| `TrackingMemoryDuration` | Seconds before losing the player's trail | 120 | 30-300 |

**Tuning tip:** Hunters should feel significantly more dangerous than standard enemies. `BaseHealth=500` is roughly 2-3x a standard elite. `TrackingMemoryDuration=120` means hunters remember the player for 2 full minutes after losing sight -- this is what makes them feel relentless.

### 1.3 Scaling Per Trace Level
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `HealthScalePerTrace` | HP multiplier per Trace level above 2 | 0.25 | 0.0-0.5 |
| `DamageScalePerTrace` | Damage multiplier per level above 2 | 0.15 | 0.0-0.3 |
| `SpeedScalePerTrace` | Speed multiplier per level above 2 | 0.05 | 0.0-0.15 |
| `DetectionScalePerTrace` | Detection range multiplier per level above 2 | 0.10 | 0.0-0.25 |

Scaling formula: `FinalStat = BaseStat * (1 + (TraceLevel - 2) * ScalePerTrace)`

Example at Trace 4: Health = 500 * (1 + 2 * 0.25) = 750 HP

### 1.4 Spawn Configuration
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `HunterPrefab` | Ghost prefab with standard enemy components | null | Required |
| `TraceReductionOnKill` | Guaranteed Trace reduction when killed | 1 | 1-2 |

### 1.5 Loot Table
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `AdditionalLoot` | Extra loot entries beyond guaranteed Trace reducer | [] | -- |
| `AdditionalLoot[].ItemId` | Item definition ID | -- | > 0 |
| `AdditionalLoot[].DropChance` | Probability (0-1) | -- | 0.0-1.0 |

### 1.6 Behavior Type
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `BehaviorType` | Unique tracking AI pattern | Pursuer | HunterBehaviorType enum |

### 1.7 HunterBehaviorType Enum
| Value | Behavior | Recommended District |
|-------|----------|---------------------|
| `Pursuer` (0) | Standard pursuit: tracks last known position, searches area | Universal fallback |
| `Ambusher` (1) | Predicts player path, sets up ambush at intercept point | Lattice district |
| `PackHunter` (2) | Coordinated pairs/squads, flanking formations | Industrial district |
| `Stalker` (3) | Stealth approach, stays out of direct detection, high burst | Quarantine district |
| `Juggernaut` (4) | Direct path, ignores cover, relentless | Burn district |

---

## 2. Hunter Prefab Setup

**Recommended location:** `Assets/Prefabs/Enemies/Hunters/`
**Naming convention:** `Hunter_{BehaviorType}.prefab`

Each hunter prefab needs standard enemy components:
- Health, DamageableAuthoring, HitboxOwnerMarker
- PhysicsShapeAuthoring (capsule, BelongsTo=Creature)
- AI components (AIBrain, AIState, VisionSettings)
- Visual mesh + animations

HunterTag, HunterState, HunterScaling, and HunterLootEntry buffer are added at runtime by HunterSpawnExecutionSystem -- do NOT bake them on the prefab.

**Tuning tip:** Hunter prefabs should have distinctive visual silhouettes so players immediately recognize the threat. Use unique color palettes or size differences.

---

## 3. Hunter Spawn Rules

### 3.1 Spawn Thresholds
| Trace Level | Hunters Spawned | Squad? |
|-------------|----------------|--------|
| 0-1 | 0 | No |
| 2 | 1 | No |
| 3 | 2 | No |
| 4+ | 3 | Yes (SquadLeader assigned) |

### 3.2 Active Hunter Cap
Maximum active hunters = `TraceLevel * 2`

At Trace 4: maximum 8 hunters alive simultaneously.

### 3.3 Spawn Cooldown
The `HunterSpawnCooldown` singleton tracks time until next eligible spawn. Cooldown = base / `TraceConfig.HunterSpawnRatePerTrace`.

| Config Field | Description | Default | Range |
|--------------|-------------|---------|-------|
| `HunterSpawnRatePerTrace` (in TraceConfig) | Spawn cooldown divisor | 1.0 | 0.1-5.0 |

### 3.4 Variant Selection
HunterSpawnSystem selects the variant matching the current district's `DistrictId`. Falls back to `DistrictId=-1` (universal) if no district-specific variant exists.

---

## 4. Hunter Tracking System

Hunters override the standard AI de-escalation with extended tracking memory:

| State | Duration | Behavior |
|-------|----------|----------|
| Player visible | Ongoing | Direct pursuit/attack |
| Player lost, within TrackingMemoryDuration | 0-120s | Override AI target to last known position, stay COMBAT alert |
| Player lost, beyond TrackingMemoryDuration | 120s+ | Normal AI de-escalation, patrol/search pattern |

---

## 5. Hunter Death and Trace Reduction

**File:** `Assets/Scripts/Trace/Systems/HunterDeathSystem.cs`

On hunter death (DeathState newly dead with HunterTag):
1. Read HunterLootEntry buffer
2. Guaranteed Trace reducer: `TraceSinkAPI.ReduceTrace(TraceReductionOnKill, HunterLoot, hunterName)`
3. Roll additional loot entries based on DropChance
4. Decrement active hunter count for spawn cap tracking

---

## 6. Live Tuning

**File:** `Assets/Scripts/Trace/Debug/HunterLiveTuning.cs`

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `HunterSpawnThreshold` | Trace level hunters start spawning | 2 | 1-6 |
| `HunterSpawnRatePerTrace` | Spawn cooldown divisor | 1.0 | 0.1-5.0 |
| `HunterActiveCapMultiplier` | Multiplier on Trace * 2 cap | 1.0 | 0.5-4.0 |
| `ForceSpawnVariantId` | Force specific variant (-1 = normal) | -1 | -1 or valid variant ID |
| `ForceSpawnCount` | Override spawn count (0 = normal) | 0 | 0-10 |

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| `Assets/Data/Trace/Hunters/` | One HunterDefinitionSO per district + one universal fallback | At minimum: `Hunter_Pursuer.asset` (universal) |
| `Assets/Prefabs/Enemies/Hunters/` | One prefab per behavior type | Standard enemy components, no HunterTag (added at runtime) |
| Run bootstrap subscene | HunterSpawnCooldown singleton | Tracks spawn timer |
| TraceConfig | `HunterSpawnRatePerTrace` field | Controls spawn frequency |
| HunterVariantDatabase SO | Maps DistrictId to HunterDefinitionSO | Fallback for unmapped districts |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| No universal hunter variant (DistrictId=-1) | NRE when entering unmapped district at Trace 2+ | Create at least one HunterDefinitionSO with DistrictId=-1 |
| HunterTag baked on prefab | Double-add of HunterTag, potential ghost issues | Let HunterSpawnExecutionSystem add all hunter components at runtime |
| HunterPrefab is null | NRE in HunterSpawnExecutionSystem | Assign prefab reference in HunterDefinitionSO |
| TraceReductionOnKill = 0 | Defeating hunter gives no Trace reduction (defeats purpose) | Set >= 1; validation enforces this |
| BelongsTo on prefab not set to Creature | Hunter detected by everything, massive physics overhead | Set PhysicsShapeAuthoring BelongsTo=Creature |
| Spawn cap not enforced | Dozens of hunters overwhelm player | Verify HunterSpawnSystem checks active count < TraceLevel * 2 |
| SquadLeader not assigned for 3+ spawns | Pack behavior has no coordination target | First spawn in a batch of 3+ is the SquadLeader |

---

## Verification

- [ ] No hunters spawn at Trace 0-1
- [ ] Single hunter spawns at Trace 2
- [ ] Two hunters spawn at Trace 3
- [ ] Hunter squad (3) spawns at Trace 4+ with SquadLeader set
- [ ] Correct district variant selected for current district
- [ ] Universal variant used as fallback for unmapped districts
- [ ] HunterScaling correctly multiplies base stats (verify at Trace 2, 3, 4)
- [ ] Hunters track player for TrackingMemoryDuration seconds after losing sight
- [ ] Hunters do NOT despawn on zone transition within a district
- [ ] Defeating a hunter reduces Trace by TraceReductionOnKill
- [ ] Active hunter cap prevents spawning beyond limit
- [ ] HunterSpawnCooldown prevents rapid sequential spawns
- [ ] Each behavior type exhibits correct tracking pattern
