# EPIC 5.1 Setup Guide: Echo Generation

**Status:** Planned
**Requires:** EPIC 4 (district exit triggers, ExpeditionGraphEntity), Framework Quest/ system

---

## Overview

When a player leaves a district with uncompleted side goals, each skipped goal mutates into an Echo Mission. Echoes are generated deterministically from the original quest + district echo flavor + expedition seed. Each district has a unique EchoFlavorSO that defines thematic mutation weights (e.g., Necrospire favors TemporalAnomaly, Burn favors EnemyUpgrade). Echoes are stored in district persistence and await the player's return.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 4.1-4.3 | District exit event, district entity | Triggers echo generation on gate transition |
| Framework Quest/ | QuestDefinitionSO, quest tracking | Source of skipped side goals |
| EPIC 4.6 | ExpeditionSeedState | Deterministic echo mutation selection |
| EPIC 4.2 | DistrictPersistenceModule | Stores echo data across district transitions |

### New Setup Required
1. Create `Assets/Scripts/Echo/` folder with Components/, Definitions/, Systems/ subfolders
2. Create `Hollowcore.Echo.asmdef` referencing DIG.Shared, Hollowcore.Chassis, Hollowcore.Expedition
3. Create one EchoFlavorSO per district
4. Add `EchoTemplate` field to each QuestDefinitionSO (links to echo override data)
5. Hook EchoGenerationSystem to district exit event

---

## 1. Creating an EchoFlavorSO

**Create:** `Assets > Create > Hollowcore/Echo/Echo Flavor`
**Recommended location:** `Assets/Data/Echo/Flavors/`

Naming convention: `{DistrictName}_EchoFlavor.asset`
Example: `Necrospire_EchoFlavor.asset`, `Wetmarket_EchoFlavor.asset`

### 1.1 Identity Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `DistrictId` | Must match the DistrictDefinitionSO.DistrictId this flavor belongs to | — | 1-999 |
| `ThemeName` | Short thematic label (e.g., "Rotting Memories", "Drowned Versions") | — | — |
| `ThemeDescription` | Flavor text describing how echoes manifest in this district | — | — |

### 1.2 Mutation Weights
Add one MutationWeight entry per mutation type you want active in this district.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `Type` | EchoMutationType enum value | — | See enum table |
| `Weight` | Relative probability (higher = more likely) | — | 0.0-1.0 |

### 1.3 EchoMutationType Enum
| Value | Description | Thematic Use |
|-------|-------------|-------------|
| `EnemyUpgrade` (0) | Harder enemy variants replace originals, 1.5x spawn count | Aggressive districts |
| `MechanicChange` (1) | Objective type changes (rescue to escort, kill to survive) | Puzzle/narrative districts |
| `LayoutDistortion` (2) | Zone paths altered, new hazards, uncanny level design | Environmental districts |
| `FactionSwap` (3) | Different faction takes over the encounter | Cross-contamination feel |
| `TemporalAnomaly` (4) | Enemies reset on death once, time-loop effects | Temporal/horror districts |

### 1.4 Visual/Audio Overrides
| Field | Description | Default |
|-------|-------------|---------|
| `EchoPostProcessProfile` | Post-process profile name applied in echo zones | — |
| `EchoAmbientAudio` | Ambient audio loop key for echo zones | — |
| `EchoEnemyVisualOverride` | Shader variant or color shift key for echo enemies | — |

**Tuning tip:** Give each district a dominant mutation (weight 0.4-0.5) and spread the rest. Example for Necrospire: TemporalAnomaly=0.4, EnemyUpgrade=0.25, MechanicChange=0.2, LayoutDistortion=0.1, FactionSwap=0.05. The build validator warns if any mutation type is completely absent.

---

## 2. District Echo Flavor Assignments

Ensure every DistrictDefinitionSO has a matching EchoFlavorSO:

| District | DistrictId | Echo Theme | Dominant Mutation |
|----------|-----------|------------|-------------------|
| Necrospire | 1 | Rotting Memories | TemporalAnomaly |
| Wetmarket | 2 | Drowned Versions | LayoutDistortion |
| Glitch Quarter | 3 | Time-Looped | TemporalAnomaly |
| Chrome Cathedral | 4 | Blessed Corruption | EnemyUpgrade |
| Burn | 5 | Scorched Echoes | EnemyUpgrade |

The build validator (`EchoFlavorBuildValidator`, callbackOrder=1) checks this at build time and warns about missing flavors.

---

## 3. Quest Integration

Each `QuestDefinitionSO` that can generate echoes needs:

| Field | Description |
|-------|-------------|
| `IsEchoEligible` | True for side goals, false for main chain quests |
| `EchoTemplate` | Reference to echo-specific override data (optional) |

The EchoGenerationSystem filters: only quests where `IsEchoEligible=true` AND `!IsCompleted` AND `!IsMainChain` generate echoes on district exit.

---

## 4. EchoMissionEntry (Runtime Buffer)

**File:** `Assets/Scripts/Echo/Components/EchoComponents.cs`

Stored as `DynamicBuffer<EchoMissionEntry>` on the district entity (capacity=8).

| Field | Type | Description |
|-------|------|-------------|
| `EchoId` | int | Unique: `hash(SourceQuestId, ExpeditionSeed, DistrictId)` |
| `SourceQuestId` | int | Which quest was skipped |
| `ZoneId` | int | Zone where echo manifests (original quest zone) |
| `MutationType` | EchoMutationType | Selected from EchoFlavorSO weights via seed |
| `DifficultyMultiplier` | float | `1.5 + (0.25 * ExpeditionsPersisted)` |
| `RewardMultiplier` | float | `2.0 + (0.5 * ExpeditionsPersisted)` |
| `ExpeditionsPersisted` | int | Cross-expedition persistence count (EPIC 5.4) |
| `IsCompleted` | bool | Set true on echo mission completion |

---

## 5. EchoRuntimeConfig (Live Tuning)

**File:** `Assets/Scripts/Echo/Components/EchoRuntimeConfig.cs`

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `BaseDifficultyMultiplier` | Starting difficulty for new echoes | 1.5 | 1.0-3.0 |
| `DifficultyPerPersistence` | Difficulty added per expedition persisted | 0.25 | 0.0-1.0 |
| `BaseRewardMultiplier` | Starting reward multiplier | 2.0 | 1.0-5.0 |
| `RewardPerPersistence` | Reward added per expedition persisted | 0.5 | 0.0-2.0 |
| `DebugEchoMainChain` | Generate echoes for main chain quests too (debug only) | false | — |
| `MaxEchoesPerDistrict` | Hard cap on echoes per district | 8 | 1-16 |

**Tuning tip:** At BaseDifficultyMultiplier=1.5, echoes are 50% harder than the original quest. This feels noticeable but manageable for skilled players. Increase if testing shows echoes are too easy. The reward multiplier should always be higher than difficulty to maintain the "worth it" feeling.

---

## 6. BlobAsset Pipeline

EchoFlavorSO converts to `EchoFlavorBlob` at bake time for Burst-compatible weighted selection.

**File:** `Assets/Scripts/Echo/Blob/EchoFlavorBlob.cs`

Key feature: precomputed CDF (cumulative distribution function) for O(1) weighted random mutation selection during echo generation. The blob stores:
- Parallel arrays of MutationTypes + MutationWeights (sorted by weight descending)
- Precomputed MutationCDF for binary search selection

---

## 7. Echo Config Panel (Editor Tool)

**File:** `Assets/Editor/EchoWorkstation/EchoConfigPanel.cs`

| Feature | Description |
|---------|-------------|
| EchoFlavorSO selector | Dropdown for all district flavors |
| Mutation weight sliders | Per-type sliders with live pie chart |
| "Normalize Weights" button | Scales all weights to sum to 1.0 |
| Preview panel | Given sample quest: shows mutation type, difficulty, reward |
| District coverage matrix | Table showing which districts have flavors, gap highlighting |
| Seed preview | Enter seed + district ID, see generated echoes |

---

## 8. Debug Visualization

**Toggle:** F7 key or debug menu
**File:** `Assets/Scripts/Echo/Debug/EchoDebugOverlay.cs`

| Element | Description |
|---------|-------------|
| Zone markers on minimap | Spiral icons colored by mutation type |
| Color key | EnemyUpgrade=red, MechanicChange=blue, LayoutDistortion=purple, FactionSwap=orange, TemporalAnomaly=cyan |
| Proximity rings | 30m (audio threshold) and 15m (visual threshold) as dashed circles |
| Per-echo tooltip | EchoId, source quest, mutation type, difficulty, reward preview |
| Wrongness gradient | Screen-space shader intensity (debug shows raw value) |
| Generation log | Echoes generated on last district exit |

---

## 9. Scene & Subscene Checklist

- [ ] `Assets/Scripts/Echo/` folder with assembly definition `Hollowcore.Echo.asmdef`
- [ ] One EchoFlavorSO per DistrictDefinitionSO at `Assets/Data/Echo/Flavors/`
- [ ] All EchoFlavorSO.DistrictId values match their target DistrictDefinitionSO.DistrictId
- [ ] Each side-goal QuestDefinitionSO has `IsEchoEligible=true`
- [ ] District entity prefab can hold `DynamicBuffer<EchoMissionEntry>` (capacity=8)
- [ ] HasActiveEchoes (IEnableableComponent) baked disabled on district entity
- [ ] EchoGenerationSystem hooked to district exit event in transition flow
- [ ] EchoRuntimeConfig singleton authoring in persistent subscene

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| EchoFlavorSO.DistrictId doesn't match DistrictDefinitionSO | Echoes generated with wrong theme | Verify DistrictId matches exactly |
| Mutation weights sum to 0 | "Mutation weights sum to 0" error, no mutation selected | Add at least one weight > 0 |
| Main chain quest marked IsEchoEligible | Main objective generates echo on district exit | Set IsEchoEligible=false for main chain |
| Missing EchoFlavorSO for a district | Build warning: "District has no EchoFlavorSO" | Create flavor asset and set DistrictId |
| Duplicate EchoMutationType in weights | Warning in OnValidate | Remove duplicate, adjust weights |
| Not hooking to district exit event | No echoes ever generated | Wire EchoGenerationSystem to EPIC 4.3 transition flow |
| Using non-deterministic random for mutation selection | Different echoes for same seed | Use `Unity.Mathematics.Random` seeded from SeedDerivationUtility |

---

## Verification

- [ ] Leave district with 3 uncompleted side goals: 3 echoes generated
- [ ] Main chain quest does NOT generate echo
- [ ] Echo mutation types match district's weighted probabilities over many runs
- [ ] DifficultyMultiplier = BaseDifficultyMultiplier + (DifficultyPerPersistence * ExpeditionsPersisted)
- [ ] Re-entering district: echoes active in correct zones
- [ ] HasActiveEchoes correctly enabled after echo generation
- [ ] Same seed produces same echoes (run twice, compare EchoId + MutationType)
- [ ] MaxEchoesPerDistrict cap respected (excess skipped goals don't generate echoes)
- [ ] Echo generation simulation (1000 exits) shows expected distribution
