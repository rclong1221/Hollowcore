# EPIC 13: District Content Pipeline

**Status**: Planning
**Priority**: Critical — The actual game world
**Dependencies**: Framework: AI/, Roguelite/ (IZoneProvider), Voxel/, Quest/, Loot/; All game EPICs feed into districts
**GDD Sections**: 14 District Compendium (all 15 districts), 17.2-17.3 Persistence & Anti-Sameness

---

## Problem

15 districts, each a Ravenswatch-sized map with unique topology, 4 threat factions, a unique Front type, 6-8+ side goals, a main chain, a boss, POIs, and thematic identity. This is the content production pipeline — the factory that turns all the systems (Front, Echoes, Chassis, Death, etc.) into playable space. The vertical slice needs 2-3 districts; the full game needs 15. This epic defines how districts are built, what they contain, and how they differ.

---

## Overview

Each district is a self-contained game world with its own personality. The District Content Pipeline defines the template, the authoring workflow, and the per-district content. Districts share a common structure (zones, Front, goals, boss, POIs) but each has unique topology, threats, traversal mechanics, and thematic flavor. This epic covers the pipeline AND the first 3 districts needed for the vertical slice.

---

## Sub-Epics

### 13.1: District Definition Template
The universal structure every district follows.

- **DistrictDefinitionSO**: master definition per district
  - Identity: DistrictId, DisplayName, Description, Icon, ArtTheme
  - Topology: ZoneGraph (connected zones), EntryPoints, TopologyVariants (2-3 per district per GDD §17.3)
  - Threats: 4 FactionDefinitionSOs (zone-based assignment)
  - Front: FrontDefinitionSO (spread pattern, phase effects, zone conversion)
  - Goals: list of QuestDefinitionSOs (side goals + main chain)
  - Boss: BossDefinitionSO reference (EPIC 14)
  - POIs: LandmarkPOIs (5-6 named locations) + MicroPOIs (environmental details)
  - Echo theme: EchoFlavorSO (how echoes mutate in this district)
  - Reanimation: ReanimationDefinitionSO (how dead bodies are used)
  - Currency: DistrictCurrencyDefinitionSO
  - Reward focus: primary DistrictRewardFocus (renamed from RewardCategory to avoid collision with Hollowcore.Economy.RewardCategoryType from EPIC 10)
- **Zone structure within district**:
  - 8-15 interconnected zones per district
  - Zone types: Combat, Elite, Boss, Shop, Event, Rest, Support, Transition
  - Zone threat faction assignment (GDD: "threats are zone-based")
  - Zone POI placement (landmarks at fixed positions, micro-POIs procedural)

### 13.2: District Generation via IZoneProvider
How district geometry is created.

- **Per-district IZoneProvider implementation**:
  - Takes seed + zone graph → produces playable space
  - Options per district type:
    - Prefab assembly (room-by-room, Ravenswatch style)
    - Voxel generation (leverages framework Voxel/ system)
    - Hybrid (prefab rooms + procedural connectors)
    - Hand-crafted scenes with procedural enemy/loot placement
  - Seed selects topology variant (2-3 per district)
- **Common generation contract**:
  - Zone boundaries defined
  - Spawn points for enemies, loot, POIs placed
  - Entry/exit points connected to gate system
  - Front origin point marked
  - NavMesh / pathfinding data generated
- **Anti-sameness rules** (GDD §17.3):
  - 2-3 topology variants per district
  - Landmark POIs with scene composition rules (not random placement)
  - Front changes map usage, not just enemy stats
  - Echoes introduce wrongness mechanics (not just harder enemies)

### 13.3: Faction & Enemy Pipeline
Per-district enemy content.

- **FactionDefinitionSO**: FactionId, DisplayName, EnemyPrefabs, BehaviorProfile, ZoneAffinity
- **Per-district: 4 factions** (GDD is consistent across all 15)
  - Each faction: 3-5 enemy types (common, elite, special)
  - Zone-based: different zones within a district have different primary factions
  - Faction behavior: aggression level, patrol patterns, alarm response
- **Enemy prefab pipeline**: model + animations + AI behavior + loot table + rippable limbs (EPIC 1)
- **Strife mutation overrides**: factions modified by active Strife card (EPIC 7)
- **Front phase scaling**: factions get reinforcements / elite variants as Front advances

### 13.4: Side Goal & Mission Design
Per-district objectives.

- **Side goal template**: QuestDefinitionSO with district-specific content
  - Per district: 6-8 side goals + 1 main chain (GDD shows 7-8 per district)
  - Side goal types: rescue, destroy, collect, escort, survive, stealth, puzzle
  - Side goals as boss insurance (GDD §4.2): completing specific goals disables boss mechanics
  - Side goals as Front counterplay: some goals slow/redirect the Front
  - Side goals as Trace reduction: "Erase trail," "Kill witness"
- **Main chain**: 1 primary objective that unlocks the boss fight
  - Multi-step quest leading to boss arena
  - Always completable regardless of side goal progress
- **Goal→Echo mapping**: each side goal has an EchoDefinitionSO for when skipped (EPIC 5)
- **Goal→Boss mapping**: each side goal has a BossVariantClause for boss fight (EPIC 14)

### 13.5: POI System
Points of Interest within districts.

- **Landmark POIs** (5-6 per district): named, memorable locations
  - Fixed positions within topology variant
  - Scene composition rules: each landmark has a specific layout template
  - Examples (Necrospire): Hologram Shrine Plaza, Relay Node Chapel, Credential Forge, Purge Corridor, Upload Vault
- **Micro POIs**: smaller environmental details, more procedural
  - Examples (Necrospire): Broken terminals, grief totems, biometric scanners, drone nests
  - Placed via weighted random from pool, scaled by zone type
- **POI interaction**: some POIs are interactable (vendors, body shops, lore terminals)
  - Uses existing Interaction/ system
- **POI persistence**: visited/looted state saved in district state (EPIC 4.2)

### 13.6: Vertical Slice District — THE NECROSPIRE
First playable district. GDD §14 District 1.

- **Theme**: towering data necropolis — corrupted uploaded consciousnesses, holographic shrines, grief-mad pilgrims
- **Topology**: stacked concentric rings, hologram occlusion, biometric locks, phase vents
- **Factions**:
  1. Mourning Collective — cultists wired to the dead, synchronized attacks
  2. Recursive Specters — AI constructs that phase/replicate, speak in dead voices
  3. Archive Wardens — century-old security drones, nerve-gas countermeasures
  4. The Inheritors — body-thieves selling clone blanks from dead genetic material
- **Front**: Corruption Bloom — spreads from core along data conduits
  - Phase 1: Flicker onset
  - Phase 2: Lockdown protocols
  - Phase 3: Specter multiplication
  - Phase 4: Full purge
  - Pulses: Purge countdown, Warden lockdown wave, Screaming broadcast
- **Side Goals**: Sever the Grief-Link, Recover the Intact Upload, Data Vampire Cache, Silence the Screaming Server, The Living Will, Debug the Widow, Black Mass Disruption, Mercy Protocol
- **Main Chain**: Purge the Core Corruption
- **Boss**: GRANDMOTHER NULL (EPIC 14)
- **Echo Theme**: Rotting memories — identity drift debuffs, pristine intel rewards
- **Reanimation**: dying moments uploaded into Recursive Specter wearing your face
- **POIs**: Hologram Shrine Plaza, Relay Node Chapel, Credential Forge, Purge Corridor, Upload Vault

### 13.7: Vertical Slice District — THE BURN
Second playable district. GDD §14 District 6.

- **Theme**: perpetual industrial hell — slag rivers, smokestacks, corporate prisoner workers
- **Topology**: conveyor corridors, furnace chambers, slag loops, heat management, coolant gates, moving belts, vent timing
- **Factions**:
  1. Slag Walkers — workers fused to environment, molten blood, lethal heat
  2. Waste Management — heat-armored enforcers, incendiary weapons
  3. The Ashborn — fire-worshippers reforged in furnaces
  4. Scrap Hives — feral recycler swarms
- **Front**: Overheat Cascade — heat zones from furnaces
  - Phase 1: Warm
  - Phase 2: Hazard
  - Phase 3: Furnace heart exposed
  - Phase 4: Flashover
  - Pulses: Furnace vent, Quota crackdown, Recycler burst
- **Side Goals**: Free the Chain Gang, Heat-Cracked Chassis, The Firewalker's Arm, Cool the Core, The Whistleblower
- **Main Chain**: Overload the Central Furnace
- **Boss**: THE FOREMAN
- **Echo Theme**: Heat debt — materials reward, scorch debuff curse
- **Reanimation**: Ashborn forge your body into a heat-servant
- **POIs**: Coolant Cache, Smelter, Quota Board, Ashborn Temple, Furnace Heart

### 13.8: Vertical Slice District — THE LATTICE
Third playable district. GDD §14 District 8.

- **Theme**: vertical slum of abandoned construction — the higher you go, the more dangerous and valuable
- **Topology**: vertical axis (base shadow → mid scaffolds → apex bunkers), ziplines, gliders, wind mapping, fall punishment
- **Factions**:
  1. The Climbers — vertical gangs, grapples and gliders from impossible angles
  2. Collapse Engineers — terrorists rigging charges throughout
  3. Apex Dwellers — elite in penthouse bunkers, drones patrolling down
  4. The Foundation — bottom-dwellers adapted to shadow
- **Front**: Structural Failure Spiral — charges trigger collapses
  - Phase 1: Tense
  - Phase 2: Unstable
  - Phase 3: Collapse
  - Phase 4: Freefall zones
  - Pulses: Collapse event, Apex drone sweep, Foundation surge
- **Side Goals**: Disable the Charges, Grapple-Limb Salvage, The Long Fall, Bridge Builder, Clip the Wings
- **Main Chain**: Anchor the Heart
- **Boss**: THE KING OF HEIGHTS
- **Echo Theme**: The fall remembers — shortcut rewards, fall damage curses
- **Reanimation**: Climbers claim your body for vertical ambush traps
- **POIs**: Charge Control, Wind Beacon, Bridge Station, Apex Node, Foundation Pit

### 13.9: Remaining Districts (Post-Vertical Slice)
Districts 2-5, 7, 9-15 built using the same pipeline.

- **Priority order** (based on mechanic diversity):
  - Tier 1 (system showcase): Wetmarket (water), Glitch Quarter (physics), Chrome Cathedral (sound)
  - Tier 2 (identity): Mirrortown (social), Synapse Row (psychic), The Shoals (underwater)
  - Tier 3 (environment): Quarantine (bio), Old Growth (nature), Deadwave (anti-tech)
  - Tier 4 (economy): The Auction (corporate), The Nursery (AI), Skyfall Ruins (zero-g)
- **Each district follows 13.1-13.5 template**
- **Production estimate**: ~2-4 weeks per district (enemies, zones, quests, boss, Front tuning)

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Roguelite/ (IZoneProvider) | Each district implements IZoneProvider |
| AI/ | Enemy factions use AI behavior system |
| Quest/ | Side goals and main chains are QuestDefinitionSOs |
| Loot/ | Per-district loot tables |
| Voxel/ | Optional terrain generation for some districts |
| Swimming/ | Water districts (Wetmarket, Shoals) use swimming system |
| Environment/ | Gravity (Skyfall), platforms (Lattice), hazards (all) |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 13.1 (Template) | None — definition | — |
| 13.2 (Generation) | 13.1 | — |
| 13.3 (Factions) | 13.1 | — |
| 13.4 (Goals) | 13.1 | EPIC 5 (echo mapping), EPIC 14 (boss mapping) |
| 13.5 (POIs) | 13.1, 13.2 | — |
| 13.6 (Necrospire) | 13.1-13.5, EPIC 3, EPIC 14 | All game EPICs |
| 13.7 (The Burn) | 13.1-13.5, EPIC 3, EPIC 14 | All game EPICs |
| 13.8 (The Lattice) | 13.1-13.5, EPIC 3, EPIC 14 | All game EPICs |
| 13.9 (Remaining) | 13.1-13.5 | — |

---

## Vertical Slice Scope (GDD §17.4)

13.6 (Necrospire), 13.7 (The Burn), 13.8 (The Lattice) — three districts demonstrating:
- Different Front types (data corruption, heat, structural collapse)
- Different topology (rings, corridors, vertical)
- Different traversal mechanics (hologram occlusion, heat management, verticality)
- Different factions (12 unique enemy types across 3 districts)
- Limb memory cross-referencing (burn limbs in burn district, etc.)

---

## Tooling & Quality

| Sub-Epic | BlobAsset | Validation | Editor Tool | Live Tuning | Debug Viz | Sim/Test |
|----------|-----------|------------|-------------|-------------|-----------|----------|
| 13.1 Template | DistrictBlob (zone graph, faction IDs, goals), FactionBlob (roster, aggression, alarm), AnimationCurve→BlobArray&lt;float2&gt; | **CRITICAL**: District completeness validator — Front non-null, 4 factions, 2+ goals, connected graph, boss ref, 2+ topology variants. Build-time scan of ALL district assets | **District Workstation** — zone graph visual editor (node-graph UI), content checklist, topology variant previewer, faction pie chart, completeness score % | Faction spawn weights, goal difficulty multipliers, zone hazard intensity, Front spread rate | Zone graph overlay (visited/unvisited, faction colors), faction territory boundaries, goal progress indicators | Generate 100 districts per template: verify connectivity, faction distribution, goal count, cross-seed variety |
| 13.2 Generation | — | Zone boundary AABB overlap, spawn point count per zone, gate connectivity | — | GenerationTimeoutSeconds | Generated zone wireframes, spawn point dots, gate positions | Layout determinism, NavMesh validity |
| 13.3 Factions | FactionBlob (shared with 13.1) | Enemy prefab completeness (AIBrain, Damageable, PhysicsShape), FactionId uniqueness, BelongsTo=Creature | — | SpawnWeight, AlarmRadius, FrontPhaseOverrides | Faction color per spawned enemy, aggression state labels | Faction spawn distribution across 100 runs |
| 13.4 Goals | — | Quest chain completeness, InsuranceType→BossTargetId validity, zone index bounds | — | CounterplayValue, TraceModifier | Goal objective zone markers, insurance effect log | Goal completion → boss insurance integration |
| 13.5 POIs | — | Landmark distance rules, micro-POI density bounds, interaction type wiring | — | DensityByZoneType, MinSpacing, DiscoveryRadius | POI positions and radii, discovery state labels | POI placement density verification |
| 13.6 Necrospire | — | (inherits 13.1 validator) | — | CorruptionSpreadRate, PhaseVentDPS, HologramOcclusion | Corruption level per zone heat map | Phase timing validation |
| 13.7 Burn | — | (inherits 13.1 validator) | — | AmbientHeatRate, FurnaceHeatRate, CoolantGateReduction, ConveyorSpeed | Heat level per zone, slag river boundaries | Heat accumulation/dissipation curves |
| 13.8 Lattice | — | (inherits 13.1 validator) | — | FallDamagePerMeter, ZiplineSpeed, GliderDescentRate, WindForce | Structural integrity per zone, tier boundaries, wind vectors | Zone collapse sequence, fall damage curves |
| 13.9 Remaining | — | (inherits 13.1 validator, applied to all 12 districts) | — | Per-district config singletons | — | All 15 districts pass completeness validator |
