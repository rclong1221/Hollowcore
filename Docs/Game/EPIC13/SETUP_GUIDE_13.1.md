# EPIC 13.1 Setup Guide: District Definition Template — Complete Authoring Workflow

**Status:** Planned
**Requires:** Framework: Roguelite/Definitions/ZoneDefinitionSO, Quest/QuestDefinitionSO, Loot/LootTableSO, AI/EncounterPoolSO

---

## Overview

The `DistrictDefinitionSO` is the master ScriptableObject that defines everything about a single district: identity, zone graph, factions, Front behavior, goals, boss, POIs, echo flavor, reanimation rules, currency, and reward focus. Every district in the game has one. This guide walks through the complete workflow from creating the SO to populating every field, authoring supporting assets (FactionDefinitionSO, FrontDefinitionSO, TopologyVariants), and validating completeness.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Framework: ZoneDefinitionSO | `DIG.Roguelite.Zones.ZoneDefinitionSO` | Defines zone behavior per type |
| Framework: QuestDefinitionSO | `DIG.Quest.QuestDefinitionSO` | Side goals and main chain |
| Framework: EncounterPoolSO | `DIG.Roguelite.Zones.EncounterPoolSO` | Enemy spawn configuration |
| Framework: LootTableSO | `DIG.Loot.LootTableSO` | Per-enemy loot drops |

### New Setup Required

| Asset | Location | Type |
|-------|----------|------|
| DistrictEnums.cs | `Assets/Scripts/District/Definitions/` | C# (enums) |
| DistrictDefinitionSO.cs | `Assets/Scripts/District/Definitions/` | C# (ScriptableObject) |
| FactionDefinitionSO.cs | `Assets/Scripts/District/Definitions/` | C# (ScriptableObject) |
| DistrictComponents.cs | `Assets/Scripts/District/Components/` | C# (ECS components) |
| DistrictBlobs.cs | `Assets/Scripts/District/Blobs/` | C# (BlobAsset) |
| DistrictLoadSystem.cs | `Assets/Scripts/District/Systems/` | C# (ISystem) |
| DistrictGoalTrackingSystem.cs | `Assets/Scripts/District/Systems/` | C# (ISystem) |
| Hollowcore.District.asmdef | `Assets/Scripts/District/` | Assembly Definition |
| DistrictCompletenessValidator.cs | `Assets/Editor/District/` | Editor validator |
| DistrictWorkstationWindow.cs | `Assets/Editor/DistrictWorkstation/` | EditorWindow |
| Per-district DistrictDefinitionSO assets | `Assets/Data/Districts/[Name]/` | ScriptableObject |

---

## 1. Create Folder Structure and Assembly Definition

### 1.1 Folder Layout
```
Assets/Scripts/District/
  Definitions/
    DistrictEnums.cs         (DistrictId, DistrictRewardFocus, ZoneGraphEntry, TopologyVariant)
    DistrictDefinitionSO.cs  (Master SO)
    FactionDefinitionSO.cs   (Per-faction SO)
    FactionEnums.cs          (FactionId, EnemyTier, BehaviorAggression)
  Components/
    DistrictComponents.cs    (DistrictState, DistrictGraphLink, ZoneGraphBufferEntry)
  Systems/
    DistrictLoadSystem.cs
    DistrictGoalTrackingSystem.cs
  Blobs/
    DistrictBlobs.cs
  Authoring/
    DistrictRegistryAuthoring.cs
  Debug/
    DistrictDebugOverlay.cs

Assets/Data/Districts/
  Necrospire/
    Necrospire_District.asset      (DistrictDefinitionSO)
    Factions/                       (4 FactionDefinitionSO assets)
    Goals/                          (QuestDefinitionSO assets)
    Encounters/                     (EncounterPoolSO assets)
    POIs/                           (Landmark/Micro POI data)
    Rooms/                          (Zone prefabs)
  Burn/
    ...
  Lattice/
    ...
```

### 1.2 Assembly Definition
**Create:** `Assets > Create > Assembly Definition`
**Name:** `Hollowcore.District`

| Reference | Why |
|-----------|-----|
| `DIG.Shared` | Shared types |
| `DIG.Roguelite` | IZoneProvider, ZoneDefinitionSO, EncounterPoolSO |
| `DIG.Quest` | QuestDefinitionSO |
| `Unity.Entities` | ECS core |
| `Unity.NetCode` | GhostField, GhostComponent |
| `Unity.Collections` | NativeArray |
| `Unity.Burst` | Burst compilation |
| `Unity.Mathematics` | math |

---

## 2. Create the DistrictDefinitionSO
**Create:** `Assets > Create > Hollowcore/District/District Definition`
**Recommended location:** `Assets/Data/Districts/[DistrictName]/[Name]_District.asset`

### 2.1 Identity Section
| Field | Type | Description | Required | Notes |
|-------|------|-------------|----------|-------|
| Id | DistrictId (enum) | Unique district identifier | YES | Necrospire=1, Wetmarket=2, ... Lattice=8, etc. |
| DisplayName | string | Human-readable name | YES | Shown in UI, Scar Map, Gate Screen |
| Description | string (TextArea) | Flavor text | Recommended | 2-3 sentences describing the district |
| Icon | Sprite | District icon for UI | Recommended | 128x128, used in gate screen and Scar Map |
| ArtTheme | string | Art direction keyword | Recommended | e.g., "data-necropolis", "industrial-hell", "vertical-slum" |

### 2.2 Topology Section
| Field | Type | Description | Required | Validation |
|-------|------|-------------|----------|------------|
| DefaultZoneGraph | ZoneGraphEntry[] | Default zone node array | YES | 8-15 entries, fully connected (BFS from entry), must include Boss zone |
| TopologyVariants | TopologyVariant[] | Alternate layouts per seed | YES | 2-3 entries minimum (GDD S17.3) |
| DefaultEntryPoints | int[] | Entry zone indices | YES | At least 1, must reference valid zone index |

### 2.3 Threats Section
| Field | Type | Description | Required | Validation |
|-------|------|-------------|----------|------------|
| Factions | FactionDefinitionSO[4] | Exactly 4 factions | YES | All 4 non-null, no duplicate FactionId |

### 2.4 Front Section
| Field | Type | Description | Required | Validation |
|-------|------|-------------|----------|------------|
| FrontDefinition | FrontDefinitionSO | How the Front spreads and transforms | YES | Non-null, must have 4 phase definitions |

### 2.5 Goals Section
| Field | Type | Description | Required | Validation |
|-------|------|-------------|----------|------------|
| Goals | QuestDefinitionSO[] | Side goals + main chain | YES | Minimum 2 (1 main + 1 side), recommended 7-8 total |

### 2.6 Boss Section
| Field | Type | Description | Required | Validation |
|-------|------|-------------|----------|------------|
| BossDefinition | ScriptableObject | Boss encounter definition | YES | Non-null (EPIC 14 reference) |

### 2.7 POIs Section
| Field | Type | Description | Required | Validation |
|-------|------|-------------|----------|------------|
| LandmarkPOIs | LandmarkPOIDefinition[] | Named landmark locations | Recommended | 5-6 entries |
| MicroPOIPool | MicroPOIPoolSO | Procedural environmental details | Recommended | Non-null for visual richness |

### 2.8 Thematic Section
| Field | Type | Description | Required | Validation |
|-------|------|-------------|----------|------------|
| EchoFlavor | ScriptableObject | How echoes mutate in this district | Optional | EPIC 5 integration |
| ReanimationDefinition | ScriptableObject | How dead bodies are used | Optional | Per-district reanimation behavior |

### 2.9 Economy Section
| Field | Type | Description | Required | Validation |
|-------|------|-------------|----------|------------|
| DistrictCurrency | ScriptableObject | District-specific currency variant | Optional | Currency system integration |
| PrimaryRewardFocus | DistrictRewardFocus (enum) | Primary loot category | YES | Determines loot table weighting |

**DistrictRewardFocus values:**

| Value | Name | Description |
|-------|------|-------------|
| 0 | Limbs | District prioritizes limb drops |
| 1 | Currency | Primarily drops currency/credits |
| 2 | Weapons | Weapon-focused loot |
| 3 | Chassis | Chassis parts and upgrades |
| 4 | Recipes | Crafting recipe drops |
| 5 | Augments | Augmentation-focused loot |
| 6 | Intel | Data/lore/intel reward focus |

---

## 3. Author the ZoneGraphEntry Array

Each zone in the district is one `ZoneGraphEntry`:

### 3.1 ZoneGraphEntry Fields
| Field | Type | Description | Default |
|-------|------|-------------|---------|
| ZoneIndex | int | Unique index within this district (0-based) | (sequential) |
| Type | ZoneType | Zone category | Combat |
| ZoneDefinition | ZoneDefinitionSO | Framework zone behavior reference | (per type) |
| ConnectedZoneIndices | int[] | Indices of connected zones | (per topology) |
| PrimaryFactionIndex | int | Index into Factions[0-3] | 0 |

### 3.2 ZoneType Quick Reference
| ZoneType | Purpose | Typical Count |
|----------|---------|---------------|
| Combat | Standard combat encounters | 3-5 |
| Elite | Harder encounters, better loot | 1-3 |
| Boss | Final encounter arena | 1 |
| Shop | Vendor/merchant area | 1 |
| Event | Special scripted encounter | 1-2 |
| Rest | Safe zone, healing, save | 1 |
| Support | Crafting, upgrades | 0-1 |
| Transition | Corridor between major areas | 0-2 |

### 3.3 Zone Graph Design Rules
1. **8-15 zones per district** (GDD specification)
2. **Fully connected**: BFS from any entry point must reach all zones
3. **Exactly 1 Boss zone**: Always at the end of the main chain
4. **At least 1 Rest zone**: Safe haven for the player
5. **ConnectedZoneIndices are bidirectional**: if zone 0 connects to zone 3, zone 3 must connect back to zone 0
6. **Faction distribution**: all 4 factions must have at least 1 zone assigned

**Tuning tip:** Use the District Workstation Zone Graph Editor (see step 7) for visual graph editing rather than manually entering index arrays in the inspector.

---

## 4. Author TopologyVariant Entries

Each district needs 2-3 topology variants to prevent sameness across runs.

### 4.1 TopologyVariant Fields
| Field | Type | Description |
|-------|------|-------------|
| VariantName | string | Human-readable name (e.g., "Clockwise", "Split", "Inverted") |
| ZoneGraph | ZoneGraphEntry[] | Overrides DefaultZoneGraph. Null = use default |
| EntryPointIndices | int[] | Overrides DefaultEntryPoints. Null = use default |
| ZonePrefabs | GameObject[] | Prefab/scene references per zone index |

### 4.2 Variant Selection at Runtime
```
variantIndex = seed % TopologyVariants.Length
```
The run seed deterministically selects one variant per district per expedition.

### 4.3 Design Guidelines for Variants
- **Change connectivity, not content**: Same zones, different connections
- **Shift entry points**: Player starts at different positions, changes routing decisions
- **Block/unblock paths**: One variant has a collapsed bridge that forces a detour
- **Shift Front origin**: Changes which zones are threatened first

**Example (Necrospire):**
| Variant | Name | Entry | Key Difference |
|---------|------|-------|----------------|
| A | Clockwise | Zone 0 | Standard spiral inward |
| B | Split | Zone 0, 2 | Zone 4 blocked until zone 7 cleared |
| C | Inverted | Zone 6 | Start mid-ring, push outward and inward |

---

## 5. Create FactionDefinitionSO Assets
**Create:** `Assets > Create > Hollowcore/District/Faction Definition`
**Recommended location:** `Assets/Data/Districts/[Name]/Factions/[FactionName].asset`

Create exactly 4 per district. See SETUP_GUIDE_13.3 for detailed faction authoring.

### 5.1 Key Fields Quick Reference
| Field | Description | Default | Validation |
|-------|-------------|---------|------------|
| Id | FactionId enum (globally unique) | -- | No duplicates across districts |
| DisplayName | Human-readable name | -- | Non-empty |
| FactionColor | Color used in debug viz and Scar Map | -- | Distinct from other 3 factions |
| EnemyRoster | 3-5 FactionEnemyEntry structs | -- | Non-empty, all prefabs non-null |
| BaseAggression | Passive/Defensive/Aggressive/Berserker | Defensive | -- |
| AlarmRadius | Help-call distance | 15.0 | 5.0-50.0 |
| PreferredZoneTypes | ZoneType filter (empty = all) | [] | -- |
| FrontPhaseOverrides[0-3] | EncounterPool swap per Front phase | [null x4] | null = no change |

---

## 6. Wire the DistrictState ECS Components

`DistrictState` is added to the RunState entity at district load time by `DistrictLoadSystem`.

### 6.1 DistrictState Fields (Runtime)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| DistrictId | byte | Yes | Current district |
| TopologyVariantIndex | byte | Yes | Selected variant for this run |
| FrontPhase | byte | Yes | Current Front phase 0-3 |
| GoalsCompleted | byte | Yes | Side goals completed |
| GoalsTotal | byte | Yes | Total side goals available |
| MainChainStep | byte | Yes | Current main chain step |
| BossUnlocked | bool | Yes | Whether boss zone is accessible |
| BossDefeated | bool | Yes | Whether boss has been killed |

### 6.2 DistrictGraphLink Pattern
To avoid bloating the RunState entity archetype (see MEMORY.md 16KB limit), the zone graph is stored on a **child entity**:
- RunState entity gets `DistrictGraphLink { GraphEntity }` (8 bytes)
- Child entity gets `ZoneGraphBufferEntry` DynamicBuffer (0 InternalBufferCapacity)

---

## 7. Use the District Workstation

**Open:** `Window > Hollowcore > District Workstation`

### 7.1 Zone Graph Editor Tab
- Drag-and-drop node graph UI
- Color-coded by ZoneType
- Click between nodes to create/remove connections
- Entry point toggle (green border)
- BFS connectivity check: disconnected nodes highlighted red
- Auto-layout button for clean visualization

### 7.2 Content Checklist Tab
Shows completeness score:
```
Factions:  [4/4]    Goals:    [8]      Boss:    [OK]
Front:     [OK]     POIs:     [5/5]    Variants: [3]
Overall: 100%
```
Each row is clickable -- jumps to the relevant inspector field.

### 7.3 Topology Variant Previewer Tab
- Enter seed number, click "Generate"
- See which variant is selected
- Side-by-side comparison of up to 3 variants
- Highlights added/removed connections between variants

### 7.4 Faction Distribution Tab
- Pie chart: zones colored by PrimaryFactionIndex
- Warning if any faction has 0 zones
- Enemy count per faction

---

## 8. Complete Walkthrough: Authoring the Necrospire

Step-by-step for the first district:

1. **Create folder**: `Assets/Data/Districts/Necrospire/`
2. **Create DistrictDefinitionSO**: Right-click > Create > Hollowcore/District/District Definition. Name: `Necrospire_District`
3. **Set Identity**: Id=Necrospire, DisplayName="The Necrospire", Description="Towering data necropolis..."
4. **Author DefaultZoneGraph**: 10 zones (see EPIC 13.6 topology table). Enter ZoneIndex, Type, ConnectedZoneIndices, PrimaryFactionIndex for each
5. **Author 3 TopologyVariants**: Clockwise (standard), Split (zone 4 blocked), Inverted (entry at zone 6)
6. **Create 4 FactionDefinitionSOs** in `Necrospire/Factions/`:
   - MourningCollective.asset (Id=10)
   - RecursiveSpecters.asset (Id=11)
   - ArchiveWardens.asset (Id=12)
   - TheInheritors.asset (Id=13)
7. **Wire factions** into Factions[0-3] slots on the DistrictDefinitionSO
8. **Create FrontDefinitionSO**: Corruption Bloom with 4 phases (see EPIC 13.6)
9. **Author 8 QuestDefinitionSOs** for side goals + 1 main chain (4 steps)
10. **Set BossDefinition**: Reference Grandmother Null (EPIC 14)
11. **Author 5 LandmarkPOIDefinitions**: Hologram Shrine Plaza, Relay Node Chapel, Credential Forge, Purge Corridor, Upload Vault
12. **Create MicroPOIPoolSO**: broken terminals, grief totems, biometric scanners, drone nests
13. **Set PrimaryRewardFocus**: Intel (district rewards data/lore)
14. **Run validator**: Window > Hollowcore > District Workstation > Checklist. Target: 100%

---

## Scene & Subscene Checklist

- [ ] `Hollowcore.District.asmdef` created with all required references
- [ ] `DistrictRegistryAuthoring` in subscene references all DistrictDefinitionSO assets (for blob baking)
- [ ] DistrictState added to RunState entity archetype at district load (system-driven, no prefab change)
- [ ] Zone graph child entity created via DistrictLoadSystem (no prefab change)
- [ ] Each DistrictDefinitionSO has all required fields populated (validator passes)

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| Fewer than 2 TopologyVariants | Validator ERROR, no anti-sameness across runs | Add at least 2 variants with different connectivity or entry points |
| Disconnected zone graph | Validator ERROR, some zones unreachable in gameplay | Ensure BFS from every entry point reaches all zones. Use Workstation graph editor |
| Missing Boss zone | Validator ERROR, main chain cannot complete | Add exactly one zone with ZoneType.Boss |
| FactionId collision between districts | Runtime: wrong faction resolved | Use district-scoped IDs: district * 10 + index (Necrospire=10-13, Burn=60-63) |
| Bidirectional connections missing | Zone A connects to B but B does not connect to A, causing one-way-only travel | Add reciprocal ConnectedZoneIndices entry |
| PrimaryFactionIndex out of range | Index-out-of-bounds when resolving faction for a zone | PrimaryFactionIndex must be 0-3 (index into the Factions[4] array) |
| FrontDefinition is null | Validator ERROR, Front system has nothing to spread | Every district must have a FrontDefinitionSO |
| Using RewardCategory instead of DistrictRewardFocus | Compile error: name collision with Hollowcore.Economy.RewardCategoryType | Use DistrictRewardFocus enum (renamed to avoid EPIC 10 collision) |
| Zone count < 8 or > 15 | Validator WARNING, may not provide enough content variety | Aim for 8-15 zones per GDD specification |
| Goals array has no main chain quest | Validator ERROR, boss never unlocks | At least one quest must be flagged as main chain |

---

## Verification

- [ ] DistrictDefinitionSO created with all required fields for vertical slice district
- [ ] Zone graph has 8-15 entries with valid bidirectional connections
- [ ] BFS from every entry point reaches all zones (no disconnected nodes)
- [ ] 4 FactionDefinitionSO slots populated with distinct FactionIds
- [ ] TopologyVariants array has 2-3 entries
- [ ] `GetZoneGraph(seed)` returns different variants for different seeds
- [ ] DistrictState correctly populated on RunState entity at district load
- [ ] DistrictGraphLink references valid child entity with ZoneGraphBufferEntry buffer
- [ ] ZoneType enum values match framework ZoneType
- [ ] DistrictGoalTrackingSystem increments GoalsCompleted on quest completion
- [ ] BossUnlocked transitions to true when main chain final step completes
- [ ] District Workstation shows 100% completeness score
- [ ] Build-time validator produces 0 errors
