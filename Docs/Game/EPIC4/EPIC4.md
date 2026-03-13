# EPIC 4: District Graph & Expedition Structure

**Status**: Planning
**Priority**: Critical — Core game structure
**Dependencies**: Framework: Roguelite/ (run lifecycle, zone system), Persistence/, SceneManagement/
**GDD Sections**: 4.1-4.5 The Expedition, 5.1 Zone Structure, 17.2 Persistence Requirements

---

## Problem

An expedition is not a linear chain of levels. It is a connected, persistent graph of 5-7 Ravenswatch-sized district maps that all continue to exist and evolve simultaneously. The framework's Roguelite/ module assumes a linear zone sequence with one active zone. Hollowcore needs a graph topology where ALL districts persist state, the player can go forward OR backward, and districts keep evolving off-screen.

---

## Overview

The District Graph replaces the framework's linear ZoneSequenceSO with a connected graph of districts. Each node is a full district with its own Front, enemies, bodies, echoes, and events. Edges are gates. The player traverses the graph over the course of an expedition, but previous districts remain alive. This is the structural backbone that every other game system plugs into.

---

## Sub-Epics

### 4.1: Expedition Graph Data Model
The expedition's topology.

- **ExpeditionGraphSO** (ScriptableObject): template graph defining possible district arrangements
  - Nodes: DistrictSlot (which DistrictDefinitionSOs can fill this slot, position in graph)
  - Edges: GateConnection (bidirectional, unlock conditions)
  - Graph is NOT fully connected — creates meaningful path choices
- **ExpeditionGraphState** (runtime): the actual expedition instance
  - Generated from ExpeditionGraphSO + expedition seed
  - 5-7 nodes, each assigned a specific DistrictDefinitionSO
  - Edge unlock state: locked, discovered, open, collapsed
  - Starting node always the same (first district)
  - Final boss node unlocked after clearing N districts (configurable, 5-7)
- **DistrictNode** (per-node runtime state):
  - DistrictDefinitionSO reference
  - FrontState (EPIC 3)
  - Completion status (main chain, side goals)
  - Seeded events (bodies, echoes, merchants, vaults)
  - Visit history (times entered, time spent, deaths)

### 4.2: District Persistence
Every district remembers everything.

- **DistrictSaveState**: full snapshot of a district's runtime state
  - Front phase + zone conversion states
  - Enemy kill/alive state per zone
  - Dead player bodies with inventory (EPIC 2)
  - Active echo missions (EPIC 5)
  - Seeded events (merchants, vaults, rare spawns)
  - Loot already collected (prevent re-looting)
  - Quest/objective completion state
- **DistrictPersistenceModule** (ISaveModule): serializes all district states
- **Lightweight persistence**: districts store EVENTS not GEOMETRY
  - Zone IDs are deterministic from seed — layout regenerates identically
  - Only delta state (kills, loot, events, Front) needs persistence
  - GDD §11.2: "doesn't store geometry — stores events at zone coordinates"
- **Per-district tick budget**: off-screen districts only advance Front + bleed (cheap)

### 4.3: District Loading & Transitions
Moving between districts.

- **DistrictLoadSystem**: implements IZoneProvider interface from framework
  - On gate transition: save current district state → load target district
  - Target may be previously visited (restore saved state) or new (generate from seed)
  - Loading screen shows Gate Selection UI (EPIC 6) during transition
- **Scene strategy**:
  - Each district is a scene (or additive scene set)
  - Common elements (player, UI, persistent systems) in persistent scene
  - District scene loaded/unloaded on transition
  - Previously visited: regenerate from seed + apply saved delta
- **Transition flow**:
  1. Player reaches district exit → extraction sequence
  2. Save current district state
  3. Gate Selection screen (EPIC 6)
  4. Unload current district scene
  5. Generate/load target district from seed + saved state
  6. Spawn player at entry point
  7. Resume gameplay

### 4.4: Zone Structure Within Districts
Internal layout of each district map.

- **ZoneDefinitionSO** (extends framework's): ZoneId, ZoneType (Combat, Elite, Boss, Shop, Event, Rest, Support)
  - Per-district zone count: 8-15 interconnected zones (Ravenswatch-sized)
  - Zone connectivity graph within district (not linear — multiple paths)
  - Zone threat faction assignment
  - Zone resource placement (loot, salvage, vendors)
- **ZoneFrontState** per zone: Safe, Contested, Hostile, Overrun (driven by EPIC 3)
- **Entry points**: each district has 1-3 entry points depending on which gate you came through
  - Entry point affects early routes and resource access (GDD §4.2)
- **Zone generation**: IZoneProvider per district type
  - GDD §17.3: "2-3 topology variants per district" — seed selects variant
  - Landmark POIs have scene composition rules
  - Threats are zone-based — different zones have different active factions

### 4.5: The Three-Act Structure
Pacing within each district.

- **Act detection**: not scripted — emergent from Front phase + player progress
  - Act 1 (Freedom): Front Phase 1, most zones Safe/Contested, multiple routes open
  - Act 2 (Squeeze): Front Phase 2-3, Front visibly advancing, routes narrowing
  - Act 3 (Intensity): Front Phase 3-4, restricted zones, combat survival
- **Returning to cleared district = permanently Act 3** (GDD §4.5)
  - Front has advanced while away
  - Safe zones converted to Hostile/Overrun
  - New content seeded (echoes, events, reanimated bodies)
- **Act transitions**: detected by system, triggers music changes, UI warnings, atmosphere shifts
- **Per-district run loop** (GDD §4.2): ~20-30 minutes
  - Drop-in → Explore & Fight → 3-5 Missions → Squeeze → Decision Point → Boss → Extraction

### 4.6: Expedition Seed & Determinism
Reproducible expeditions.

- **ExpeditionSeed**: master seed for entire expedition
  - Derives: district assignment seed, zone layout seeds, encounter seeds, loot seeds, event seeds
  - Uses framework's RunSeedUtility (math.hash chain)
- **Determinism requirements** (GDD §11.2):
  - Same seed = same district graph, same zone layouts, same initial enemy placements
  - Player actions create divergence (kills, deaths, loot taken, Front counterplay)
  - Re-entering a district regenerates same layout from seed, applies saved delta
- **Seed sharing**: players can share expedition seeds for comparable runs (same graph, different execution)

### 4.7: Expedition End Conditions
How expeditions conclude.

- **Victory**: defeat final boss (unlocked after clearing enough districts)
  - Final boss selection based on influence meters (EPIC 15)
  - Triggers expedition summary, Scar Map review, Compendium awards
- **Full Wipe**: total party kill with no revival options (EPIC 2)
  - Expedition ends, run progress lost
  - Compendium entries from completed districts survive
- **Abandonment**: player quits expedition
  - Expedition state can be saved and resumed (if persistence is implemented)
  - OR treated as full wipe (simpler for initial implementation)

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Roguelite/ (RunLifecycleSystem) | Expedition wraps the run — RunPhase extended for graph navigation |
| Roguelite/ (IZoneProvider) | Each district implements IZoneProvider for its generation tech |
| Roguelite/ (ZoneSequenceSO) | Replaced by ExpeditionGraphSO — non-linear |
| SceneManagement/ | District loading/unloading uses existing scene pipeline |
| Persistence/ (ISaveModule) | DistrictPersistenceModule for cross-district state |
| Roguelite/ (RunSeedUtility) | Seed derivation chain for all procedural content |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 4.1 (Graph Model) | None — foundation | — |
| 4.2 (Persistence) | 4.1 | All other game epics (they write district state) |
| 4.3 (Loading) | 4.1, 4.2 | EPIC 6 (gate selection during transition) |
| 4.4 (Zone Structure) | 4.1 | EPIC 3 (Front drives zone state) |
| 4.5 (Three-Act) | 4.4, EPIC 3 | — |
| 4.6 (Seed) | 4.1, 4.4 | — |
| 4.7 (End Conditions) | 4.1 | EPIC 2, EPIC 15 |

---

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Graph vs linear zone sequence | Graph (non-linear) | GDD §4.1: "not a linear chain — a connected, persistent graph" |
| District loading | Full scene swap with persistent player scene | Cleanest memory management for Ravenswatch-sized maps |
| Persistence granularity | Events at zone coordinates, not geometry | GDD §11.2, §17.2: lightweight, deterministic regeneration |
| Off-screen simulation | Front advance only (cheap) | Full enemy simulation would be prohibitive |
| District count | 5-7 per expedition | GDD §4.1: "5-7 procedurally generated district maps" |

---

## Vertical Slice Scope

- 4.1 (graph model), 4.2 (persistence), 4.3 (loading) required
- 4.4 (zones) required for at least 2-3 districts
- 4.5 (three-act) emerges from EPIC 3 + 4.4
- 4.6 (seed) required for determinism
- 4.7 (end conditions) at least full wipe + boss victory path

---

## Tooling & Quality

| Sub-Epic | BlobAsset | Validation | Editor Tool | Live Tuning | Debug Viz | Simulation |
|---|---|---|---|---|---|---|
| 4.1 Graph Model | ExpeditionGraphBlob | Graph connectivity BFS, duplicate edge, depth constraints | **Expedition Workstation** (node-graph editor, seed explorer) | EdgePruneProbability, MinEdgesPerNode, debug gates | Graph overlay (minimap), node/edge state colors | 1000-graph batch: path-to-boss, branching factor, district distribution |
| 4.2 Persistence | -- | DistrictSaveState.Validate(), save file scanner | Persistence inspector (live state, dump JSON) | -- | Kill/loot/body/echo markers in-world | Round-trip fidelity, stress test 100 districts, delta apply verification |
| 4.3 Loading | -- | -- | Transition debug panel (phase visualizer, force transition) | ExtractionAnimDuration, GateInteractionRadius, skip toggles | Transition phase banner, gate proximity indicators | Full expedition traverse, rapid transition stress, entry point coverage |
| 4.4 Zones | ZoneTopologyBlob | Internal connectivity BFS, entry point validation, zone type counts | Zone topology visual editor (drag nodes, draw connections) | ConnectionPruneProbability, BoundsExpansionFactor, debug reveal | Zone minimap (FrontZoneState colors, connections, player dot) | 100 variants per topology: path diversity, pruning rate, zone type histogram |
| 4.5 Three-Act | -- | ActThresholds consistency (Act2 < Act3, time ordering) | Act timeline editor (drag thresholds, simulate progression) | Act2/Act3 time fallback scale, force act, modifier scale | Act state banner, condition gauges, transition log | Pacing curve simulation, threshold comparison, revisit scenario |
| 4.6 Seed | -- | Seed uniqueness validation (1000 seeds, collision detection) | Seed explorer (derivation tree, compare seeds, daily preview) | -- | Seed display overlay (expedition + district seeds) | Determinism verification (100 seeds x2), seed distribution analysis |
| 4.7 End Conditions | -- | ExpeditionRewardConfigSO field validation | End condition debug panel (force result, reward formula preview) | WipeGracePeriod, MetaCurrencyMultiplier, abandon partial reward | End condition status bar, revival status, meta-currency estimate | Reward Monte Carlo (1000 runs), priority resolution, resume fidelity |
