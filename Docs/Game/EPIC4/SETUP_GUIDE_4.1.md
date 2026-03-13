# EPIC 4.1 Setup Guide: Expedition Graph Data Model

**Status:** Planned
**Requires:** Framework Roguelite/ (RunSeedUtility, RunLifecycleSystem), Unity.Entities, Unity.NetCode

---

## Overview

The expedition graph defines the non-linear topology of districts that players traverse during an expedition. You will create an ExpeditionGraphSO template with district slots and gate connections, then wire it to a runtime singleton entity that drives the entire expedition structure. This is the foundation that EPIC 4.2-4.7 and all district-level systems depend on.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Framework `Roguelite/` module | RunSeedUtility, RunLifecycleSystem | Seed chain + run phase triggers |
| `DIG.Shared.asmdef` | — | Shared types (assembly reference) |
| Persistent subscene | — | Hosts the ExpeditionConfig singleton entity |

### New Setup Required
1. Create assembly definition `Hollowcore.Expedition.asmdef`
2. Create at least one ExpeditionGraphSO template
3. Create at least 2-3 DistrictDefinitionSO assets
4. Create an ExpeditionConfig singleton authoring in a persistent subscene
5. Verify graph generation in Entity Debugger

---

## 1. Creating the Assembly Definition

**Create:** `Assets/Scripts/Expedition/Hollowcore.Expedition.asmdef`

Reference these assemblies:
- `DIG.Shared`
- `DIG.Roguelite`
- `Unity.Entities`
- `Unity.Entities.Hybrid`
- `Unity.NetCode`
- `Unity.Collections`
- `Unity.Mathematics`

Create subfolders:
```
Assets/Scripts/Expedition/
  Components/
  Definitions/
  Systems/
  Authoring/
  Blob/
  Utility/
  Debug/
```

---

## 2. Creating a DistrictDefinitionSO

**Create:** `Assets > Create > Hollowcore/Expedition/District Definition`
**Recommended location:** `Assets/Data/Expedition/Districts/`

Create at least 2-3 district definitions before building a graph template.

### 2.1 Inspector Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `DistrictId` | Unique numeric ID. Must be globally unique across all districts | — | 1-999 |
| `DisplayName` | Name shown in UI (Gate Screen, Scar Map) | — | — |
| `Description` | Flavor text for Gate Screen tooltip | — | — |
| `MapIcon` | Sprite for expedition graph minimap | — | — |
| `SceneKey` | Addressable scene key for loading this district | — | Must match Addressables |
| `MinZones` | Minimum zones generated in this district | 8 | 4-20 |
| `MaxZones` | Maximum zones generated | 15 | 4-20 |
| `TopologyVariants` | Number of layout variants the seed can choose from | 3 | 1-5 |
| `FrontDefinition` | FrontDefinitionSO driving pressure (EPIC 3) | — | — |
| `ThreatFactions` | Array of FactionIds that spawn enemies here | — | — |
| `TargetRunMinutes` | Design target for time spent in this district | 25 | 15-40 |

**Tuning tip:** Start with 3 districts for early testing: one easy (8 zones, single faction), one medium (12 zones, two factions), one hard (15 zones, three factions). You can add more later without changing the graph template.

---

## 3. Creating an ExpeditionGraphSO Template

**Create:** `Assets > Create > Hollowcore/Expedition/Graph Template`
**Recommended location:** `Assets/Data/Expedition/DefaultGraph.asset`

### 3.1 Slots (District Nodes)
Add 5-7 DistrictSlot entries. Each slot is a position in the graph that will be filled with a district at runtime.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `SlotName` | Human-readable label (e.g., "Starting District", "Mid-East", "Boss Chamber") | — | — |
| `CandidateDistricts` | List of DistrictDefinitionSOs eligible for this slot. Seed picks one | — | 1+ entries |
| `GraphPosition` | 2D position for visual editor layout (does not affect gameplay) | — | — |
| `MinDepth` | Minimum edge distance from start node | 0 | 0-6 |
| `MaxDepth` | Maximum edge distance from start node | 6 | 0-6 |

### 3.2 Gates (Edges)
Add GateConnectionTemplate entries connecting slots.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `SlotIndexA` | First slot index (0-based into Slots list) | — | 0 to Slots.Count-1 |
| `SlotIndexB` | Second slot index | — | 0 to Slots.Count-1 |
| `Guaranteed` | If true, seed cannot prune this edge | false | — |
| `UnlockCondition` | When this gate opens at runtime | AlwaysOpen | See enum |

### 3.3 Generation Rules
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `MinActiveNodes` | Minimum districts activated from template | 5 | 2-Slots.Count |
| `MaxActiveNodes` | Maximum districts activated | 7 | 2-Slots.Count |
| `StartSlotIndex` | Which slot is the expedition starting point (always activated) | 0 | 0 to Slots.Count-1 |
| `BossSlotIndex` | Which slot holds the final boss (always activated) | last | 0 to Slots.Count-1 |
| `BossUnlockThreshold` | Districts that must be cleared before boss gate opens | 5 | 1 to MaxActiveNodes-1 |

### 3.4 GateUnlockCondition Enum
| Value | Description |
|-------|-------------|
| `AlwaysOpen` | Gate opens immediately on generation |
| `DiscoverBothSides` | Both adjacent districts must be visited |
| `DefeatElite` | Relevant elite enemy kill flag must be set |
| `CompleteObjective` | Adjacent node's CompletionMask must have required bit |
| `BossThreshold` | Opens when DistrictsCleared >= BossUnlockThreshold |

**Tuning tip:** Use `Guaranteed=true` for edges on the critical path (start to boss). Use `Guaranteed=false` for side paths to create run variety. Ensure at least one guaranteed path from start to boss using the "Validate Graph" button.

---

## 4. Wiring the ExpeditionConfig Singleton

### 4.1 Create Authoring Component
Create a MonoBehaviour authoring that bakes:
- `ExpeditionGraphState` (singleton)
- `ExpeditionGraphTemplateRef` (blob reference to the selected ExpeditionGraphSO)
- `DynamicBuffer<GraphNodeState>`
- `DynamicBuffer<GraphEdgeState>`

### 4.2 Place in Persistent Subscene
1. Open your persistent gameplay subscene (the one that survives district loading/unloading)
2. Create an empty GameObject named `ExpeditionConfig`
3. Add the ExpeditionConfig authoring component
4. Assign your `DefaultGraph.asset` to the Template field
5. Save and close the subscene

---

## 5. Expedition Workstation (Editor Tool)

**Open:** `Window > Hollowcore > Expedition Workstation`
**File:** `Assets/Editor/ExpeditionWorkstation/ExpeditionWorkstationWindow.cs`

### Tabs
| Tab | Purpose |
|-----|---------|
| **Graph Editor** | Visual node-graph editor for ExpeditionGraphSO. Drag slots, draw edges, set properties |
| **Seed Explorer** | Enter a seed, see the generated graph. Compare two seeds side-by-side |
| **Simulation** | Generate 1000 graphs, validate statistics (path length, branching factor, district distribution) |
| **District Browser** | Browse all DistrictDefinitionSO assets |

### Graph Editor Usage
- **Add Slot:** Right-click canvas > "Add Slot"
- **Add Edge:** Right-click a node > "Add Edge From Here" > click target node
- **Delete:** Right-click node/edge > "Delete"
- **Validate:** Toolbar > "Validate Graph" runs BFS connectivity + constraint checks
- **Preview:** Toolbar > "Preview With Seed" highlights active vs inactive nodes/edges
- Node colors: green=start, red=boss, grey=normal
- Edge styles: solid=guaranteed, dashed=prunable

---

## 6. Live Tuning (Runtime)

The `ExpeditionRuntimeConfig` singleton provides runtime knobs.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `EdgePruneProbability` | Probability of removing non-guaranteed edges | 0.3 | 0.0-1.0 |
| `MinEdgesPerNode` | Connectivity floor per active node after pruning | 1 | 1-4 |
| `DebugBossUnlockOverride` | Force boss unlock at N districts cleared (0 = use template) | 0 | 0-7 |
| `DebugAllGatesOpen` | All gates start Open (fast traversal debug) | false | — |
| `DebugSkipGateSelection` | Skip Gate Selection UI on transitions | false | — |

---

## 7. Scene & Subscene Checklist

- [ ] Persistent subscene contains ExpeditionConfig authoring with template assigned
- [ ] ExpeditionGraphSO asset saved at `Assets/Data/Expedition/`
- [ ] At least 2 DistrictDefinitionSO assets at `Assets/Data/Expedition/Districts/`
- [ ] Each DistrictDefinitionSO has a valid SceneKey pointing to an Addressable scene
- [ ] Assembly definition `Hollowcore.Expedition.asmdef` created with correct references

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| StartSlotIndex == BossSlotIndex | OnValidate error | Use different slot indices for start and boss |
| Boss unreachable from start via guaranteed edges | "Boss node NOT reachable" error | Ensure at least one guaranteed edge path from start to boss |
| Slot with empty CandidateDistricts list | "Slot N has no candidate districts" error | Add at least one DistrictDefinitionSO to each slot |
| BossUnlockThreshold > MaxActiveNodes-1 | Warning: threshold unreachable | Lower threshold or increase MaxActiveNodes |
| Duplicate edges between same slot pair | "Duplicate edge" validation error | Remove the duplicate GateConnectionTemplate |
| MinDepth > MaxDepth on a slot | Validation error | Ensure MinDepth <= MaxDepth |
| Missing ExpeditionConfig in persistent subscene | ExpeditionGraphEntity never created | Add authoring to persistent subscene and rebake |
| SceneKey mismatch on DistrictDefinitionSO | District fails to load | Verify key matches Addressables group entry exactly |

---

## Verification

- [ ] Enter Play Mode and check Entity Debugger for `ExpeditionGraphState` singleton
- [ ] Singleton has `DynamicBuffer<GraphNodeState>` with 5-7 entries (all `IsActive=true`)
- [ ] Singleton has `DynamicBuffer<GraphEdgeState>` with correct node index pairs
- [ ] Same seed entered twice produces identical graph (compare node DistrictDefinitionIds)
- [ ] Different seeds produce different district assignments
- [ ] Start node edges begin as Discovered; all other edges start Locked
- [ ] "Validate Graph" in Expedition Workstation passes with no errors
- [ ] Simulation tab: 1000-graph batch completes, all graphs have start-to-boss path
