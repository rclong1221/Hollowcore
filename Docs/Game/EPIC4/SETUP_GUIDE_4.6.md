# EPIC 4.6 Setup Guide: Expedition Seed & Determinism

**Status:** Planned
**Requires:** EPIC 4.1 (Graph Data Model), EPIC 4.4 (Zone Structure), Framework Roguelite/ (RunSeedUtility)

---

## Overview

Every procedural element of an expedition derives from a single master seed via a deterministic hash chain. The SeedDerivationUtility provides a structured derivation tree: master seed branches into graph, loot, event, and encounter seeds; each district derives zone layout, spawn, loot, event, and Front seeds. Same seed guarantees same expedition. Player actions create divergence; geometry always regenerates identically for revisits.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 4.1 | ExpeditionGraphEntity | Hosts ExpeditionSeedState + DistrictSeedData buffer |
| Framework Roguelite/ | RunSeedUtility | Base hash chain primitive (`math.hash`) |
| Unity.Mathematics | `Random`, `math.hash` | Burst-compatible deterministic RNG |

### New Setup Required
1. Create `SeedDerivationUtility.cs` at `Assets/Scripts/Expedition/Utility/`
2. Add `ExpeditionSeedState` component to ExpeditionGraphEntity
3. Add `DynamicBuffer<DistrictSeedData>` to ExpeditionGraphEntity
4. Wire seed input into expedition start flow
5. Replace all `UnityEngine.Random` / `System.Random` in generation systems
6. Run determinism verification

---

## 1. Seed Derivation Tree

The master seed branches into a tree of derived seeds. Every branch uses `math.hash(new uint2(...))` with unique salt constants.

```
MasterSeed
  |-- GraphSeed        (0x47524150 "GRAP") -> district assignment, edge pruning
  |-- LootSeed         (0x4C4F4F54 "LOOT") -> global loot rolls
  |-- EventSeed        (0x45564E54 "EVNT") -> global event placement
  |-- EncounterSeed    (0x454E434F "ENCO") -> encounter composition
  +-- Per-District (node index N):
        DistrictSeed   (0x44495354 + N)
          |-- ZoneLayoutSeed    (0x5A4F4E45 "ZONE") -> topology variant + placement
          |-- SpawnSeed         (0x5350574E "SPWN") -> enemy positions/types
          |-- LootSeed          (0x444C4F54 "DLOT") -> container contents
          |-- EventSeed         (0x44455654 "DEVT") -> merchants, vaults, rare spawns
          +-- FrontSeed         (0x46524E54 "FRNT") -> initial spread pattern
```

### Zone-Level Derivation (from district seeds)
```
DistrictSeed
  +-- Per-Zone (index I):
        ZoneId              (0x5A494400 + I) -> deterministic zone identity
        ZoneEncounterSeed   (SpawnSeed, 0x5A454E00 + I) -> per-zone enemy comp
        ZoneLootSeed        (LootSeed, 0x5A4C5400 + I) -> per-zone container rolls
```

---

## 2. Seed Input Sources

Wire seed into the expedition start flow via `SeedInitializationSystem`.

| Source | How to Generate | Use Case |
|--------|----------------|----------|
| Normal Run | `(uint)UnityEngine.Random.Range(1, int.MaxValue)` at server run creation | Default gameplay |
| Shared Run | Player enters seed string, hash via `math.hash(new uint4(...))` | Social, competitive |
| Daily Challenge | `math.hash(new uint2(dayOfYear, year))` | Rotating community challenge |

### 2.1 Configuration
The seed source is determined by the expedition start parameters passed to `RunLifecycleSystem`. No inspector configuration needed beyond the run flow.

**Tuning tip:** For daily challenges, use `DateTime.UtcNow` to ensure all players worldwide get the same seed regardless of timezone.

---

## 3. ExpeditionSeedState (Singleton on ExpeditionGraphEntity)

**File:** `Assets/Scripts/Expedition/Components/SeedComponents.cs`

| Field | Type | GhostField | Description |
|-------|------|-----------|-------------|
| `MasterSeed` | uint | Server-only | Master seed for entire expedition |
| `GraphSeed` | uint | Server-only | Derived: graph topology |
| `LootSeed` | uint | Server-only | Derived: global loot |
| `EventSeed` | uint | Server-only | Derived: global events |
| `EncounterSeed` | uint | Server-only | Derived: encounter composition |

---

## 4. DistrictSeedData (Buffer on ExpeditionGraphEntity)

**File:** `Assets/Scripts/Expedition/Components/SeedComponents.cs`

One entry per active node, parallel to `GraphNodeState` buffer (same indices).

| Field | Type | Description |
|-------|------|-------------|
| `NodeIndex` | int | Must match GraphNodeState index |
| `DistrictSeed` | uint | Master seed for this district |
| `ZoneLayoutSeed` | uint | Topology variant + zone placement |
| `SpawnSeed` | uint | Enemy positions and types |
| `LootSeed` | uint | Container contents |
| `EventSeed` | uint | Merchants, vaults, rare spawns |
| `FrontSeed` | uint | Initial Front spread pattern |

---

## 5. Using Seeds in Generation Systems

### CRITICAL RULES
1. **NEVER** use `UnityEngine.Random` or `System.Random` for gameplay-affecting rolls
2. **ALWAYS** derive a new `Unity.Mathematics.Random` from a deterministic seed
3. **NEVER** persist a Random instance across frames — prediction rollback desyncs it
4. **ALWAYS** use `SeedDerivationUtility` to derive sub-seeds

### 5.1 Usage Pattern in Systems
```
// Correct: derive fresh Random from deterministic seed per usage
var rng = new Unity.Mathematics.Random(
    SeedDerivationUtility.DeriveZoneEncounterSeed(spawnSeed, zoneIndex));
int enemyType = rng.NextInt(0, enemyPool.Length);
float3 offset = rng.NextFloat3(-spread, spread);

// WRONG: using persistent random
static Random _persistentRng; // <-- NEVER do this
```

### 5.2 Seed Derivation for Common Operations
| Operation | Seed to Use | Derivation |
|-----------|-------------|-----------|
| Select topology variant | ZoneLayoutSeed | `ZoneLayoutSeed % TopologyVariants` |
| Place enemy in zone | ZoneEncounterSeed | `DeriveZoneEncounterSeed(SpawnSeed, zoneIndex)` |
| Roll loot in zone container | ZoneLootSeed | `DeriveZoneLootSeed(LootSeed, zoneIndex)` |
| Place merchant in zone | EventSeed | `DeriveDistrictEventSeed(DistrictSeed)` |
| Select district for slot | GraphSeed | Used in ExpeditionGraphGenerationSystem |
| Generate echo mutation | EventSeed | `hash(SourceQuestId, ExpeditionSeed, DistrictId)` |

---

## 6. Seed Explorer (Editor Tool)

**Open:** Expedition Workstation > "Seed Explorer" tab
**File:** `Assets/Editor/ExpeditionWorkstation/SeedExplorerModule.cs`

### Input Panel
| Control | Description |
|---------|-------------|
| Seed field | Enter uint directly or string (auto-hashed) |
| "Random Seed" button | Generate random seed for quick exploration |
| Template selector | Choose ExpeditionGraphSO to generate against |

### Output Panel
| Feature | Description |
|---------|-------------|
| Derivation tree | Expandable tree showing MasterSeed down to per-zone seeds (hex values) |
| Click any seed | Copies to clipboard |
| Graph preview | Same node-graph canvas showing generated result with active/inactive nodes |
| Compare Seeds | Enter two seeds side-by-side, highlights differences |
| Seed Sharing preview | Green=deterministic, Yellow=player-dependent |

---

## 7. Determinism Verification

### Editor Validation
**Menu:** `Hollowcore > Validation > Verify Seed Determinism (1000 seeds)`
**File:** `Assets/Editor/Validation/SeedDeterminismValidator.cs`

Runs `SeedDerivationUtility.ValidateSeedUniqueness()` for 1000 seeds. Reports collision count.

### In-Code Validation
`SeedDerivationUtility.ValidateSeedUniqueness(seed, out report)`:
- Generates all expedition-level + district-level derived seeds (up to 8 districts)
- Checks for hash collisions
- Returns pass/fail + collision details

### Simulation Tab
**Open:** Expedition Workstation > "Determinism" tab

| Test | Description |
|------|-------------|
| Determinism Verification | Run graph generation twice per seed (100 seeds), compare outputs |
| Seed Distribution | 10000 seeds: district selection frequency, edge pruning rate, node count histogram |
| Daily Challenge Preview | Enter date, see seed + expedition preview. Calendar view for next 7 days |

---

## 8. Debug Visualization

**Toggle:** F11 key
**File:** `Assets/Scripts/Expedition/Debug/SeedDebugOverlay.cs`

| Display | Location |
|---------|----------|
| `Expedition Seed: 0xABCD1234` | Top-right, small text |
| `District Seed: 0x5678EFAB` | Below expedition seed (current district) |
| Per-zone IDs on minimap | Hex values, hover for full seed chain |
| "Copy Seed" button | Debug menu |

---

## 9. Scene & Subscene Checklist

- [ ] ExpeditionGraphEntity authoring includes ExpeditionSeedState component
- [ ] ExpeditionGraphEntity authoring includes DynamicBuffer\<DistrictSeedData\>
- [ ] SeedDerivationUtility.cs created at `Assets/Scripts/Expedition/Utility/`
- [ ] SeedInitializationSystem runs before ExpeditionGraphGenerationSystem
- [ ] No `UnityEngine.Random` or `System.Random` in any procedural generation system
- [ ] All generation systems derive `Unity.Mathematics.Random` from SeedDerivationUtility

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| Using `UnityEngine.Random` in generation | Different results each run with same seed | Replace with `Unity.Mathematics.Random` seeded from SeedDerivationUtility |
| Persisting Random across frames | Desync after prediction rollback | Create fresh Random from seed each time |
| MasterSeed = 0 | `Unity.Mathematics.Random(0)` has degenerate behavior | Ensure seed >= 1 |
| DistrictSeedData buffer not parallel to GraphNodeState | Wrong seeds for wrong districts | Populate in same order, verify NodeIndex matches |
| Seed collision at build | "Collision at district N" from validator | Extremely rare with math.hash — verify salt constants are unique |
| Daily challenge using local time | Different seeds in different timezones | Use `DateTime.UtcNow` |
| Hash chain uses same salt for different purposes | Correlated random sequences | Each derivation function uses a unique 4-byte salt |

---

## Verification

- [ ] Same MasterSeed produces identical ExpeditionSeedState derived seeds (run twice, compare)
- [ ] Same GraphSeed produces identical district assignments and edge topology
- [ ] Same DistrictSeed produces identical zone layouts (count, types, connectivity)
- [ ] Same SpawnSeed produces identical enemy placements per zone
- [ ] Same LootSeed produces identical container contents
- [ ] Different MasterSeeds produce meaningfully different expeditions
- [ ] Re-entering a district regenerates identical geometry from stored DistrictSeed
- [ ] Validator passes for 1000 seeds with 0 collisions
- [ ] No `UnityEngine.Random` or `System.Random` found in generation system files
- [ ] Seed Explorer shows correct derivation tree for entered seed
