# EPIC 4.4 Setup Guide: Zone Structure Within Districts

**Status:** Planned
**Requires:** EPIC 4.1 (Graph Data Model), EPIC 4.6 (Seed & Determinism), Framework Roguelite/ (IZoneProvider)

---

## Overview

Each district contains 8-15 interconnected zones forming a non-linear internal map with multiple paths, dead ends, shortcuts, and chokepoints. Zone layouts are defined by ZoneTopologyTemplate assets (2-3 variants per district), with the expedition seed selecting the variant at runtime. Zones have types (Combat, Elite, Boss, Shop, Event, Rest, Support), faction assignments, resource placements, and Front-driven states.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 4.1 | ExpeditionGraphEntity, DistrictDefinitionSO | District context for zone generation |
| EPIC 4.6 | SeedDerivationUtility, DistrictSeedData | Deterministic zone layout selection |
| Framework Roguelite/ | IZoneProvider | Zone generation interface |
| District scenes | Addressable scenes per district | Scene containers for zone composition |

### New Setup Required
1. Create 2-3 ZoneTopologyTemplate assets per district type
2. Link topology templates to DistrictDefinitionSO
3. Create zone composition prefabs for each ZoneType
4. Place ZoneAuthoring objects in district scenes
5. Add ZoneEntryPointLink authoring to entry point objects

---

## 1. Creating a ZoneTopologyTemplate

**Create:** `Assets > Create > Hollowcore/Expedition/Zone Topology`
**Recommended location:** `Assets/Data/Expedition/Topologies/`

Naming convention: `{DistrictName}_Topology{Letter}.asset`
Example: `Necrospire_TopologyA.asset`, `Necrospire_TopologyB.asset`, `Necrospire_TopologyC.asset`

### 1.1 Layout Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `VariantName` | Human-readable label (e.g., "Hub and Spoke", "Branching Corridor") | — | — |
| `ZoneCount` | Total zones in this variant | — | 8-15 |

### 1.2 ZoneSlots (Per-Zone Configuration)
Add one ZoneSlotTemplate per zone in the topology.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `Type` | Zone purpose — determines enemy density, rewards, mechanics | Combat | See ZoneType enum |
| `RelativePosition` | 2D position for visual layout (editor tool + minimap) | — | -1 to 1 (normalized) |
| `Required` | If true, seed cannot prune this zone | false | — |

### 1.3 ZoneType Enum
| Value | Description | Typical Count Per District |
|-------|-------------|--------------------------|
| `Combat` | Standard enemy encounters | 4-8 |
| `Elite` | Mini-boss or elite encounter | 1-2 |
| `Boss` | District boss (0-1 per topology) | 0-1 |
| `Shop` | Merchant/vendor zone | 0-1 |
| `Event` | Story event, NPC interaction, puzzle | 1-3 |
| `Rest` | Safe zone for healing/resupply | 1-2 |
| `Support` | Crafting, upgrade stations | 0-1 |

**Tuning tip:** A good 12-zone topology might be: 6 Combat, 1 Elite, 1 Boss, 1 Shop, 1 Event, 1 Rest, 1 Support. Mark Boss, Shop, and at least 2 Combat zones as Required.

### 1.4 Connections (Zone Adjacency)
Add ZoneConnectionTemplate entries linking zones.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `ZoneIndexA` | First zone index (0-based into ZoneSlots) | — | 0 to ZoneCount-1 |
| `ZoneIndexB` | Second zone index | — | 0 to ZoneCount-1 |
| `Required` | If true, seed cannot prune this connection | false | — |
| `OneWay` | If true, traversal is A to B only (shortcut/drop-down) | false | — |

**Tuning tip:** Use `Required=true` for connections on the critical path from entry points to the Boss zone. Use `Required=false` for side paths that add exploration variety. One-way connections work well for shortcuts that reward backtracking.

### 1.5 Entry Points
| Field | Description | Range |
|-------|-------------|-------|
| `EntryPointZoneIndices` | Zone indices that serve as player spawn points (1-3 per topology) | 0 to ZoneCount-1 |

Each entry point corresponds to a gate from a different adjacent district. The player spawns in the entry zone matching the gate they entered through.

---

## 2. Linking Topologies to Districts

Open each DistrictDefinitionSO and configure:

| Field | Value |
|-------|-------|
| `TopologyVariants` | Set to the number of ZoneTopologyTemplate assets created for this district (2-3) |

The topology templates are loaded by name convention: the ZoneGenerationSystem selects variant index from `zoneSeed % TopologyVariants` and loads the corresponding template.

---

## 3. Zone Composition Prefabs

**Recommended location:** `Assets/Prefabs/Zones/`

Each ZoneType needs a composition prefab that defines the physical layout:
- Geometry (floors, walls, cover)
- Spawn points for enemies, loot, events
- Environmental hazards
- Navigation data

### 3.1 ZoneDefinitionSO (Per-Zone Override)

**Create:** `Assets > Create > Hollowcore/Expedition/Zone Definition`
**Recommended location:** `Assets/Data/Expedition/Zones/`

Optional — used for fine-grained per-zone overrides beyond what the topology template provides.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `ZoneId` | Deterministic ID (derived from seed at runtime) | — | Auto-assigned |
| `DisplayName` | Name shown on minimap | — | — |
| `Type` | Zone type (matches topology slot) | — | — |
| `PrimaryFaction` | Main enemy faction | None | FactionId enum |
| `SecondaryFaction` | Optional mixed-faction encounters | None | FactionId enum |
| `ThreatMultiplier` | Difficulty scale relative to district baseline | 1.0 | 0.5-3.0 |
| `LootSpawnCount` | Number of loot containers in this zone | — | 0-10 |
| `SalvageNodeCount` | Salvage material nodes | — | 0-5 |
| `CanSpawnVendor` | Whether a vendor can appear here | false | — |
| `ZoneCompositionPrefab` | Physical layout prefab | — | — |
| `LandmarkPrefabs` | Special POI prefabs that can appear | — | — |

---

## 4. District Scene Setup

### 4.1 Zone Authoring in Scenes
For each zone in the district scene:
1. Create an empty GameObject at the zone's world position
2. Add `ZoneAuthoring` component
3. Set the zone bounds (defines the AABB for ZoneTrackingSystem):
   - `BoundsCenter`: world-space center of the zone
   - `BoundsExtents`: half-size in each axis

This bakes `ZoneState` + `DynamicBuffer<ZoneConnection>` on the zone entity.

### 4.2 Entry Point Authoring
For each zone that serves as a district entry:
1. Create an empty GameObject named `EntryPoint_{GateName}` inside the zone
2. Add `ZoneEntryPointLink` authoring
3. Set `ZoneIndex` to match the zone's index in the topology

### 4.3 Enemy Zone Assignment
Enemy spawn points must know which zone they belong to:
1. Add a `ZoneAssignment` IComponentData to enemy prefabs
2. The zone index is baked from the spawn point's parent zone
3. `ZoneClearCheckSystem` uses this to track per-zone enemy counts

---

## 5. Zone Runtime State

Each zone entity carries runtime state tracked by these systems:

| System | Purpose | Update Group |
|--------|---------|-------------|
| `ZoneGenerationSystem` | Creates zone entities from topology + seed | InitializationSystemGroup |
| `ZoneTrackingSystem` | AABB containment test — which zone is the player in | SimulationSystemGroup |
| `ZoneClearCheckSystem` | Marks zones as cleared when all enemies dead | SimulationSystemGroup (after ZoneTracking) |

### 5.1 FrontZoneState (Driven by EPIC 3)
| Value | Description | Visual Indicator |
|-------|-------------|-----------------|
| `Safe` | No front presence | Green on minimap |
| `Contested` | Front approaching, some enemies | Yellow |
| `Hostile` | Front active, heavy enemies | Orange |
| `Overrun` | Front fully consumed zone | Red |

---

## 6. Zone Topology Visual Editor

**Open:** Expedition Workstation > "Zone Topology" tab
**File:** `Assets/Editor/ExpeditionWorkstation/ZoneTopologyEditorModule.cs`

| Feature | Description |
|---------|-------------|
| Canvas | Zones as colored rectangles (color=ZoneType), draggable to set RelativePosition |
| Connections | Lines between zones: solid=required, dashed=prunable, arrow=one-way |
| Click zone | Property panel: Type, Required toggle |
| Click connection | Required, OneWay toggles |
| Right-click | "Add Zone", "Add Connection", "Set Entry Point" |
| Entry points | Arrow icon on entry zones |
| Auto-Layout | Spring-force layout algorithm |
| Validate | Highlights disconnected zones in red |

### Zone Type Color Key
| Type | Color |
|------|-------|
| Combat | Red |
| Elite | Orange |
| Boss | Purple |
| Shop | Green |
| Event | Blue |
| Rest | Cyan |
| Support | Grey |

---

## 7. Live Tuning (Runtime)

**File:** `Assets/Scripts/Expedition/Components/ZoneRuntimeConfig.cs`

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `ConnectionPruneProbability` | Chance of removing non-required connections | 0.2 | 0.0-1.0 |
| `BoundsExpansionFactor` | AABB margin for ZoneTrackingSystem | 1.0 | 0.5-2.0 |
| `DebugRevealAllZones` | Show all zones on minimap immediately | false | — |

---

## 8. Debug Visualization

**Toggle:** M key (minimap) or Tab (full-screen zone map)
**File:** `Assets/Scripts/Expedition/Debug/ZoneDebugOverlay.cs`

| Element | Description |
|---------|-------------|
| Zone rectangles | Colored by FrontZoneState (green/yellow/orange/red) |
| Zone borders | Solid=visited, dashed=unvisited |
| Connections | Thin lines between zone centers |
| Player dot | White circle at current position |
| Zone labels | Type icon + clear/visited status |
| Current zone | Pulsing border highlight |
| Entry points | Gate icon overlay |

---

## 9. Scene & Subscene Checklist

- [ ] 2-3 ZoneTopologyTemplate assets per district at `Assets/Data/Expedition/Topologies/`
- [ ] Each DistrictDefinitionSO has TopologyVariants set to match template count
- [ ] District scenes contain ZoneAuthoring objects with valid BoundsCenter/Extents
- [ ] Entry point GameObjects have ZoneEntryPointLink with correct ZoneIndex
- [ ] Enemy spawn points have ZoneAssignment baked from parent zone
- [ ] Zone composition prefabs placed in district scenes at correct positions
- [ ] Topology templates have at least 1 entry point defined

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| ZoneCount != ZoneSlots.Count in topology | Warning in OnValidate | Match ZoneCount to actual ZoneSlots list length |
| Self-loop connection (ZoneIndexA == ZoneIndexB) | Validation error | Remove the self-referencing connection |
| No entry points defined | "No entry points defined" error | Add at least 1 index to EntryPointZoneIndices |
| Required zone unreachable from entry point | "Required zone N unreachable" error | Add connections or mark intermediate zones as required |
| Zone bounds too small | Player "between zones" with no active zone | Increase BoundsExtents or set BoundsExpansionFactor > 1.0 |
| Zone bounds overlapping | Player detected in multiple zones simultaneously | Shrink overlapping bounds; system uses first match |
| Missing Boss zone in topology | Warning: "No Boss zone" | Add one ZoneSlotTemplate with Type=Boss |
| OneWay connection in wrong direction | Player can't reach target zone | Swap ZoneIndexA and ZoneIndexB (A to B direction) |

---

## Verification

- [ ] ZoneGenerationSystem creates correct number of zone entities from topology
- [ ] Same seed + topology variant produces identical zone IDs and connectivity
- [ ] Different seeds select different topology variants
- [ ] ZoneConnection buffers are bidirectional for non-one-way connections
- [ ] All zones reachable from entry points (connectivity validation passes)
- [ ] ZoneTrackingSystem correctly identifies player's current zone
- [ ] Entering a new zone sets PlayerVisited=true and triggers discovery UI
- [ ] ZoneClearCheckSystem marks zone Cleared when all enemies dead
- [ ] 8-15 zones per district (within configured range)
- [ ] Multiple paths exist between entry points and Boss zone
- [ ] Minimap correctly shows zone layout with FrontZoneState colors
