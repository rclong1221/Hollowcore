# EPIC 4.2 Setup Guide: District Persistence

**Status:** Planned
**Requires:** EPIC 4.1 (Graph Data Model), Framework Persistence/ (ISaveModule, SaveManager), Framework Roguelite/ (RunSeedUtility)

---

## Overview

Every district remembers everything when the player leaves: killed enemies, collected loot, Front progress, active echoes, quest completions, and player bodies. Persistence is lightweight because zone geometry regenerates deterministically from the seed; only the delta (events at zone coordinates) is serialized. The DistrictPersistenceModule implements ISaveModule with TypeId=22.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 4.1 | ExpeditionGraphEntity + GraphNodeState buffer | Provides node indices for keying saved states |
| Framework Persistence/ | ISaveModule, SaveManager | Module registration and serialization pipeline |
| Framework Roguelite/ | RunSeedUtility | Deterministic zone regeneration on revisit |
| Persistent subscene | DistrictPersistenceRegistry authoring | Tracks which districts have saved state |

### New Setup Required
1. Create `Assets/Scripts/Expedition/Persistence/` folder
2. Register DistrictPersistenceModule with SaveManager (TypeId=22)
3. Add DistrictSaveTag + DistrictDeltaApplyTag to district entity prefab
4. Add DistrictPersistenceRegistry singleton to persistent subscene
5. Wire save/load triggers to district transition flow (EPIC 4.3)

---

## 1. Registering the Persistence Module

**File:** `Assets/Scripts/Expedition/Persistence/DistrictPersistenceModule.cs`

Register with the framework's SaveManager during bootstrap:

```
SaveManager.RegisterModule(new DistrictPersistenceModule());
```

### 1.1 Module Configuration
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `TypeId` | ISaveModule type identifier (must be unique across all modules) | 22 | 1-255 |
| Save file format | Binary (.dig), CRC32 validated, write-ahead .tmp safety | -- | -- |
| Max districts per expedition | Maximum entries in the Dictionary<int,DistrictSaveState> | 8 | 4-16 |

**Tuning tip:** The module stores a `Dictionary<int, DistrictSaveState>` keyed by node index. Maximum 8 districts per expedition means maximum 8 entries. Keep save data lean by only storing events, not geometry.

---

## 2. District Entity Persistence Tags

**File:** `Assets/Scripts/Expedition/Components/DistrictPersistenceComponents.cs`

Add these as baked-disabled IEnableableComponents on the district entity prefab:

| Component | Purpose | Triggered By |
|-----------|---------|-------------|
| `DistrictSaveTag` | Signals "save state before unload" | DistrictTransitionSystem (EPIC 4.3) |
| `DistrictDeltaApplyTag` | Signals "apply saved delta after zone regeneration" | DistrictLoadSystem (EPIC 4.3) |

### 2.1 Baker Setup
In your district entity authoring component:
```
AddComponent<DistrictSaveTag>(entity);
SetComponentEnabled<DistrictSaveTag>(entity, false);
AddComponent<DistrictDeltaApplyTag>(entity);
SetComponentEnabled<DistrictDeltaApplyTag>(entity, false);
```

**Tuning tip:** Both tags must be baked disabled. DistrictSaveTag is enabled by DistrictTransitionSystem on district exit; DistrictDeltaApplyTag is enabled by DistrictLoadSystem after zone regeneration for revisited districts only.

---

## 3. DistrictPersistenceRegistry Singleton

**Place:** Persistent subscene (same one holding ExpeditionConfig)

1. Create an empty GameObject named `DistrictPersistence`
2. Add the DistrictPersistenceRegistry authoring component
3. This bakes a singleton with `HasSavedStateMask` (byte bitmask, max 8 districts)

### 3.1 Registry Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `HasSavedStateMask` | Bit N = 1 means district at node index N has saved state | 0x00 | 0x00-0xFF |

---

## 4. What Gets Persisted (DistrictSaveState)

**File:** `Assets/Scripts/Expedition/Persistence/DistrictSaveState.cs`

| Category | Data Stored | Notes |
|----------|-------------|-------|
| Front State | FrontPhase, SpreadProgress, AdvanceRate, BleedCounter | Restores pressure level on revisit |
| Per-Zone Delta | FrontZoneState, KillCount, AliveCount per zone | Zone IDs are deterministic from seed |
| Enemy Kills | ZoneId, EnemySpawnId, KillTime per kill | Suppresses respawn on revisit |
| Player Bodies | ZoneId, Position (zone-relative), InventorySnapshotId, DeathTime | EPIC 2 integration |
| Active Echoes | EchoMissionId, ZoneId, EchoPhase | EPIC 5 integration |
| Seeded Events | EventId, ZoneId, EventType, Consumed flag | Merchants, vaults, rare spawns |
| Collected Loot | ZoneId, LootSpawnId | Prevents re-looting |
| Quest State | QuestId, Phase, Complete flag | Side goal progress |
| Visit Metadata | VisitCount, TimeSpentSeconds, DeathCount | Copied from GraphNodeState |

**Tuning tip:** Zone geometry is NOT stored. The seed regenerates layout identically. Only the delta needs serialization, keeping save files small even for 8 districts.

---

## 5. Save/Load Flow

### 5.1 Saving (District Exit)
1. DistrictTransitionSystem enables `DistrictSaveTag` on active district entity
2. `DistrictSaveSystem` runs (before scene unload):
   - Calls `state.CompleteDependency()` to flush all async systems
   - Queries all enemies (dead/alive), loot, echoes, events, quests
   - Populates DistrictSaveState
   - Calls `DistrictPersistenceModule.SaveDistrict(nodeIndex, state)`
   - Sets bit in `DistrictPersistenceRegistry.HasSavedStateMask`
   - Disables `DistrictSaveTag`

### 5.2 Loading (District Entry - Revisit)
1. DistrictLoadSystem regenerates zone layout from seed
2. Checks `DistrictPersistenceRegistry.HasSavedStateMask` for this node
3. If saved state exists: enables `DistrictDeltaApplyTag`
4. `DistrictDeltaApplySystem` runs (InitializationSystemGroup):
   - Restores Front state (phase, spread, rate)
   - Suppresses killed enemy respawns (matches by EnemySpawnId)
   - Suppresses collected loot (matches by LootSpawnId)
   - Restores player bodies, echo missions, seeded events, quest state
   - Disables `DistrictDeltaApplyTag`

---

## 6. Editor Tooling

### Persistence Inspector
**Open:** Expedition Workstation > "Persistence" tab
**File:** `Assets/Editor/ExpeditionWorkstation/DistrictPersistenceInspector.cs`

| Feature | Description |
|---------|-------------|
| Live State View | Expandable per-district: kill count, loot count, echo count, Front phase |
| Dump to JSON | Exports current DistrictSaveState for debugging |
| Clear District | Wipes a single district's saved state |
| Force Save Current | Manually triggers DistrictSaveTag |
| Bitmask Visualizer | 8 toggles showing HasSavedStateMask bits |
| Byte Size Estimate | Per-district save size for profiling |

### Validation Menu
**Menu:** `Hollowcore > Validation > Validate Save Files`
**File:** `Assets/Editor/Validation/DistrictPersistenceValidator.cs`

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Persistent subscene | DistrictPersistenceRegistry authoring | Same subscene as ExpeditionConfig |
| District entity prefab | DistrictSaveTag + DistrictDeltaApplyTag (baked disabled) | IEnableableComponent pattern |
| Framework SaveManager | Register DistrictPersistenceModule at TypeId=22 | Bootstrap registration |
| Transition flow | DistrictSaveSystem before scene unload | EPIC 4.3 integration |
| Initialization flow | DistrictDeltaApplySystem after zone generation | InitializationSystemGroup |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| Forgetting `CompleteDependency()` in DistrictSaveSystem | Incomplete kill/loot records (async systems not flushed) | Add `state.CompleteDependency()` at top of OnUpdate |
| DistrictPersistenceModule not registered | Save file missing district data, revisited districts reset | Register in bootstrap/SaveManager module list |
| DistrictSaveTag not baked disabled | Save triggers immediately on district load | Use `SetComponentEnabled<DistrictSaveTag>(entity, false)` in baker |
| Zone geometry not deterministic from seed | Revisited district looks different, delta apply mismatches | Ensure all generation uses SeedDerivationUtility, never UnityEngine.Random |
| Missing DistrictPersistenceRegistry singleton | HasSavedStateMask never updated, system can't check for saved state | Add authoring to persistent subscene |
| EnemySpawnId not deterministic | Kill records don't match on revisit, enemies respawn anyway | Derive from `SeedDerivationUtility.DeriveEnemySpawnId()` |
| Saving after scene unload started | Entity queries return empty, save state is blank | Ensure DistrictSaveSystem runs BEFORE scene unload |

---

## Verification

- [ ] DistrictPersistenceModule registers with TypeId=22 in SaveManager
- [ ] Enter district, kill 3 enemies, collect 2 loot items, exit via gate
- [ ] Re-enter same district: killed enemies are absent, loot containers are empty
- [ ] Front phase matches what it was when you left
- [ ] Entity Debugger: DistrictPersistenceRegistry.HasSavedStateMask has correct bit set
- [ ] Persistence inspector shows correct kill/loot counts for the saved district
- [ ] "Dump to JSON" produces valid JSON with all expected records
- [ ] Round-trip test: serialize + deserialize produces identical state
- [ ] ClearAll() wipes all district states on expedition end
