# EPIC 13.9: Remaining Districts (Post-Vertical Slice)

**Status**: Planning
**Epic**: EPIC 13 — District Content Pipeline
**Priority**: Medium — Built after vertical slice validates the pipeline
**Dependencies**: 13.1-13.5 (all pipeline sub-epics), 13.6-13.8 (vertical slice districts validate the pipeline)

---

## Overview

Districts 2-5, 7, 9-15 follow the same template established in 13.1-13.5 and proven by the vertical slice districts (13.6 Necrospire, 13.7 The Burn, 13.8 The Lattice). This sub-epic provides brief template entries for all 12 remaining districts, prioritized into 4 production tiers by mechanic diversity and technical dependency. Each entry specifies: theme, unique mechanic, topology style, 4 faction summaries, Front type, side goal count, boss name, echo theme, and reanimation flavor. Full detail (equivalent to 13.6-13.8) is authored per-district during production using the pipeline.

---

## Component Definitions

### DistrictProductionStatus

```csharp
// File: Assets/Scripts/District/Definitions/DistrictProductionEnums.cs
namespace Hollowcore.District
{
    /// <summary>
    /// Production tier for scheduling district content creation.
    /// </summary>
    public enum DistrictProductionTier : byte
    {
        /// <summary>Vertical slice: Necrospire, Burn, Lattice. Already defined in 13.6-13.8.</summary>
        VerticalSlice = 0,

        /// <summary>System showcase: each introduces a major unique mechanic.</summary>
        Tier1_SystemShowcase = 1,

        /// <summary>Identity districts: strong thematic identity, moderate new mechanics.</summary>
        Tier2_Identity = 2,

        /// <summary>Environment districts: environmental hazard focus, reuse existing mechanics.</summary>
        Tier3_Environment = 3,

        /// <summary>Economy districts: economic/social systems focus, most reuse.</summary>
        Tier4_Economy = 4
    }

    /// <summary>
    /// Production state tracking for each district's content pipeline.
    /// </summary>
    public enum DistrictContentState : byte
    {
        NotStarted = 0,
        DefinitionAuthored = 1,   // DistrictDefinitionSO created
        FactionsAuthored = 2,     // 4 FactionDefinitionSOs + enemy prefabs
        GoalsAuthored = 3,        // Side goals + main chain QuestDefinitionSOs
        GeometryCreated = 4,      // Zone rooms/scenes created, IZoneProvider works
        FrontTuned = 5,           // Front phases balanced and tested
        POIsPlaced = 6,           // Landmarks + micro-POIs in all zones
        BossImplemented = 7,      // Boss fight functional (EPIC 14)
        PlaytestComplete = 8,     // Full district playthrough validated
        Shipped = 9               // Content-locked
    }
}
```

---

## Production Tiers

### Tier 1: System Showcase (3 districts, ~3-4 weeks each)

These districts each introduce a major gameplay mechanic that no other district shares. They should be built first after the vertical slice because their unique systems may need framework support.

### Tier 2: Identity (3 districts, ~2-3 weeks each)

Strong thematic identity with moderate new mechanics. Leverage existing systems with unique faction behaviors and social/psychological themes.

### Tier 3: Environment (3 districts, ~2-3 weeks each)

Environmental hazard-focused. Primarily reuse mechanics from Tier 1 and the vertical slice, with district-specific hazard configurations.

### Tier 4: Economy (3 districts, ~2 weeks each)

Economic, social, and meta-game focused. Highest reuse of existing systems. These can be built in parallel with less engineering support.

---

## District Templates

### District 2: THE WETMARKET (Tier 1)

```
Theme: Flooded market district — submerged corridors, air pockets, amphibious threats
Unique Mechanic: Water traversal — swimming system (framework Swimming/), breath management,
  underwater combat with modified physics. Dry zones vs flooded zones.
Topology: Horizontal grid with water level variation. Some zones fully submerged,
  others at waterline, a few elevated and dry.
Generation Mode: Hybrid (prefab rooms + procedural water level)

Factions:
  1. Tide Merchants — amphibious traders gone feral, net traps, harpoon attacks
  2. Drowned Circuit — waterlogged cybernetics, electric discharge in water
  3. The Undertow — deep-water predators, pull players under, pressure damage
  4. Bilge Rats — scavengers, fast swimmers, loot-stealing AI

Front: Rising Tide — water level rises zone by zone
  Phase 1: Puddles, Phase 2: Knee-deep, Phase 3: Chest-deep, Phase 4: Submerged
  Pulses: Tidal surge, Electric discharge, Pressure crush

Side Goals: 6 (drain valves, salvage operations, rescue drowning NPCs)
Main Chain: Seal the Breach
Boss: THE HARBORMASTER
Echo Theme: Water memory — breath debuffs, aquatic loot rewards
Reanimation: Drowned corpse ambush from underwater
```

### District 3: THE GLITCH QUARTER (Tier 1)

```
Theme: Reality-unstable zone — physics glitches, spatial anomalies, broken simulation
Unique Mechanic: Physics manipulation — gravity zones (inverted, zero-G, heavy),
  spatial warps (teleport loops, impossible geometry), glitch fields that randomize
  player stats temporarily.
Topology: Non-Euclidean. Zones connect in ways that defy physical space.
  Walking through a door may lead to a zone that spatially overlaps another.
Generation Mode: HandCraftedProcedural (spatial tricks require hand-authored scenes)

Factions:
  1. Glitch Walkers — entities that exploit physics bugs, teleport attacks
  2. Reality Anchors — stabilizer drones trying to patch the simulation
  3. The Overflow — data corruption entities, grow stronger near glitch fields
  4. Patch Hunters — scavengers who harvest glitch energy for profit

Front: Simulation Decay — reality becomes increasingly unstable
  Phase 1: Minor glitches, Phase 2: Gravity shifts, Phase 3: Spatial loops,
  Phase 4: Full desync (random teleportation, physics chaos)
  Pulses: Gravity flip, Spatial collapse, Stat scramble

Side Goals: 7 (stabilize anchors, navigate impossible rooms, exploit glitches)
Main Chain: Recompile the Quarter
Boss: THE KERNEL PANIC
Echo Theme: Glitch memory — random stat shifts, impossible shortcut rewards
Reanimation: Glitched clone that teleports and phases unpredictably
```

### District 4: THE CHROME CATHEDRAL (Tier 1)

```
Theme: Sonic architecture — a cathedral built from sound, resonance chambers, frequency warfare
Unique Mechanic: Sound propagation — noise attracts enemies (stealth vs loud playstyle),
  resonance weapons that deal AOE through walls, harmonic puzzles, silence zones
  that disable abilities.
Topology: Acoustic chambers connected by resonance corridors. Sound carries between
  adjacent zones (combat in one zone alerts neighbors).
Generation Mode: PrefabAssembly (acoustic chamber prefabs with audio properties)

Factions:
  1. The Choir — sonic cultists, resonance attacks, group harmonics buff
  2. Frequency Hounds — hunt by sound, stealth gameplay, echolocation
  3. Silent Order — monks of silence, ability-suppressing auras, melee focus
  4. Broadcast Pirates — weaponized speakers, sonic turrets, area denial

Front: Resonance Cascade — escalating sound levels
  Phase 1: Hum, Phase 2: Cacophony, Phase 3: Shatter frequency,
  Phase 4: Total resonance (constant sonic damage)
  Pulses: Frequency spike, Silence bomb, Harmonic overload

Side Goals: 6 (tune resonators, silence broadcasts, harmonic puzzles)
Main Chain: Silence the Source
Boss: THE CONDUCTOR
Echo Theme: Sound memory — tinnitus debuff, harmonic weapon rewards
Reanimation: Sonic echo of the player that mimics their attack patterns with sound delay
```

### District 5: THE SHOALS (Tier 2)

```
Theme: Underwater ruins — deeper than Wetmarket, full submarine environment
Unique Mechanic: Deep-water pressure — pressure increases with depth, requires
  pressure-rated limbs. Bioluminescence for vision. Submarine traversal vehicles.
Topology: Vertical descent (surface → shallows → deep → abyss).
  Pressure zones replace heat zones from The Burn.
Generation Mode: VoxelGeneration (underwater cave systems)

Factions:
  1. Pearl Divers — adapted scavengers, speargun combat, ink grenades
  2. Abyssal Cult — deep-dwellers, pressure immunity, crushing attacks
  3. Coral Constructs — living reef entities, regenerate from environment
  4. Salvage Subs — vehicular enemies, torpedo attacks, depth charges

Front: Pressure Breach — hull failures flood safe zones
Side Goals: 5 | Main Chain: Seal the Abyss
Boss: THE LEVIATHAN
Echo Theme: Pressure memory — crushing debuff, deep-sea salvage rewards
Reanimation: Waterlogged corpse with bioluminescent lure attack
```

### District 7: MIRRORTOWN (Tier 2)

```
Theme: City of reflections — holographic facades, identity theft, social deception
Unique Mechanic: Social deception — NPCs may be disguised enemies, enemies may
  appear as allies. Mirror checks reveal truth. Reputation system with factions
  that shifts based on who you fight/ally with.
Topology: Urban grid with mirrored architecture. Some zones are reflections of
  others (shared layout, different inhabitants).
Generation Mode: PrefabAssembly (mirrored room pairs)

Factions:
  1. The Reflections — holographic duplicates, mimic player abilities
  2. Face Merchants — identity brokers, disguise and betrayal mechanics
  3. True Sight — anti-deception enforcers, see through all disguises
  4. The Masked — anarchists, constantly shifting identity/allegiance

Front: Identity Erosion — player UI increasingly unreliable
Side Goals: 7 | Main Chain: Break the Mirror
Boss: THE DOUBLE
Echo Theme: Identity memory — NPC confusion debuff, true-sight augment rewards
Reanimation: Perfect mirror of the player, identical abilities and loadout
```

### District 9: SYNAPSE ROW (Tier 2)

```
Theme: Psychic district — neural networks, mind-link combat, thought weapons
Unique Mechanic: Psychic link — player can mind-link with enemies (read intentions,
  predict attacks) at the cost of vulnerability to psychic damage. Thought-weapon
  crafting from extracted memories.
Topology: Neural network layout — hub-and-spoke with synaptic pathways.
Generation Mode: Hybrid (hub rooms + procedural synaptic corridors)

Factions:
  1. Mind Weavers — psychic attackers, confusion/hallucination abilities
  2. Thought Police — neural security, suppress psychic abilities
  3. Memory Eaters — consume memories for power, steal player abilities temporarily
  4. The Disconnected — psych-immune scavengers, brute force approach

Front: Psychic Storm — escalating hallucinations and mind control
Side Goals: 6 | Main Chain: Sever the Network
Boss: THE HIVEMIND
Echo Theme: Thought memory — hallucination debuff, psychic weapon rewards
Reanimation: Psychic projection that fights using the player's planned actions
```

### District 10: QUARANTINE (Tier 3)

```
Theme: Biological hazard zone — plague, mutation, organic horror
Unique Mechanic: Infection — similar to Heat (The Burn) but biological.
  Infection stacks, causes mutations (some beneficial, some harmful).
  Cure stations vs embracing mutation for power at a cost.
Topology: Medical facility layout — clean rooms, contaminated wards, lab zones.
Generation Mode: PrefabAssembly (medical facility prefabs)

Factions:
  1. The Infected — mutated victims, organic ranged attacks
  2. Quarantine Enforcers — hazmat soldiers, flamethrower cleansing
  3. Lab Remnants — escaped experiments, unpredictable abilities
  4. Cure Seekers — desperate survivors, will trade or fight for cure materials

Front: Outbreak Spiral — infection zones expand
Side Goals: 6 | Main Chain: Synthesize the Cure
Boss: PATIENT ZERO
Echo Theme: Infection memory — mutation debuff, bio-augment rewards
Reanimation: Infected corpse that spreads plague on proximity
```

### District 11: OLD GROWTH (Tier 3)

```
Theme: Reclaimed nature — jungle overtaking city ruins, bioluminescent ecosystem
Unique Mechanic: Living terrain — plants react to player presence (thorns close paths,
  flowers provide buffs, vines as grapple points). Day/night cycle affects flora behavior.
Topology: Organic sprawl — no grid, flowing paths through overgrown structures.
Generation Mode: VoxelGeneration (organic terrain with ruin shells)

Factions:
  1. Root Walkers — plant-human hybrids, terrain manipulation
  2. Spore Colony — fungal network, area denial, hallucination spores
  3. Last Gardeners — eco-terrorists, use plant traps, animal companions
  4. Lumber Corps — corporate harvesters, heavy machinery, deforestation

Front: Overgrowth — plants become hostile, paths close
Side Goals: 6 | Main Chain: Find the Heart Tree
Boss: THE ARBORIST
Echo Theme: Growth memory — entanglement debuff, organic augment rewards
Reanimation: Overgrown corpse as a plant trap
```

### District 12: THE AUCTION (Tier 4)

```
Theme: Corporate trading floor — high-stakes economy, bid wars, hostile acquisitions
Unique Mechanic: Economy warfare — dynamic pricing, auction events, corporate reputation.
  Can buy passage through zones, bribe factions, or go hostile and crash the market.
Topology: Corporate tower — lobby, trading floors, executive suites, vault.
Generation Mode: HandCraftedProcedural (set-piece corporate environments)

Factions:
  1. The Board — corporate executives, drone security, buyout attacks
  2. Floor Traders — desperate traders, explosive market manipulation
  3. Hostile Takeover — corporate raiders, aggressive acquisition tactics
  4. The Auditors — enforcement, anti-fraud, relentless pursuit AI

Front: Market Crash — prices spike, factions become desperate
Side Goals: 5 | Main Chain: Hostile Takeover
Boss: THE CEO
Echo Theme: Debt memory — currency penalty debuff, investment reward multiplier
Reanimation: Corporate clone in player's image, hired as security
```

### District 13: DEADWAVE (Tier 3)

```
Theme: Anti-technology zone — EMP fields, analog weapons only, signal darkness
Unique Mechanic: Tech suppression — cybernetic limbs and electronic weapons
  degrade/disable in EMP zones. Analog alternatives required. Forces loadout adaptation.
Topology: Signal-dead corridors radiating from EMP source.
Generation Mode: PrefabAssembly (industrial bunker prefabs)

Factions:
  1. Static Monks — anti-tech zealots, EMP grenades, melee focus
  2. Signal Ghosts — entities that exist only in electromagnetic noise
  3. Analog Militia — survivors using pre-digital weapons
  4. The Broadcast — rogue AI trying to restore signal, electronic attacks

Front: Signal Death — EMP zones expand, tech progressively fails
Side Goals: 5 | Main Chain: Kill the Signal
Boss: THE ANTENNA
Echo Theme: Static memory — tech failure debuff, analog weapon rewards
Reanimation: EMP-pulsing corpse that disables nearby tech
```

### District 14: THE NURSERY (Tier 4)

```
Theme: AI nursery — nascent artificial intelligences, digital playground, virtual threats
Unique Mechanic: AI companion/threat — encounter baby AIs that can be befriended
  (temporary companion) or harvested (resources). Moral choice with gameplay consequences.
Topology: Virtual environments — each zone is a different AI's "nursery" world.
Generation Mode: HandCraftedProcedural (themed virtual environments)

Factions:
  1. Nanny Bots — protective caretakers, defensive swarm tactics
  2. Child AIs — unpredictable, reality-warping, tantrum attacks
  3. Debuggers — corporate cleanup crew, deletion attacks
  4. The Graduated — mature AIs that escaped, manipulative, boss-tier intelligence

Front: Corruption Upload — malware infects AI nurseries
Side Goals: 6 | Main Chain: Save or Purge the Nursery
Boss: THE PRINCIPAL
Echo Theme: Digital memory — AI companion curse/boon, data harvest rewards
Reanimation: AI creates a digital twin of the player in the nursery
```

### District 15: SKYFALL RUINS (Tier 4)

```
Theme: Orbital debris field — zero-gravity zones, space station wreckage, cosmic exposure
Unique Mechanic: Variable gravity — zero-G combat and traversal, magnetic boots,
  decompression hazards. Leverages framework Environment/ gravity system.
Topology: Fragmented space station sections connected by vacuum gaps.
Generation Mode: PrefabAssembly (station module prefabs with vacuum connectors)

Factions:
  1. Void Walkers — zero-G adapted, jetpack combat, 3D movement
  2. Hull Breakers — demolition specialists, decompression weapons
  3. The Tethered — entities connected by cables, group movement
  4. Station AI — automated defense systems, turrets, drones, containment fields

Front: Orbital Decay — station sections detach, artificial gravity fails
Side Goals: 5 | Main Chain: Stabilize the Orbit
Boss: THE STATION MIND
Echo Theme: Void memory — gravity sickness debuff, zero-G mobility rewards
Reanimation: Corpse frozen in vacuum, activated by proximity, explosive decompression
```

---

## Production Schedule

| Tier | Districts | Est. Time Each | Engineering Dependency |
|------|-----------|----------------|----------------------|
| VS | Necrospire, Burn, Lattice | 4-6 weeks | Pipeline creation (13.1-13.5) |
| T1 | Wetmarket, Glitch Quarter, Chrome Cathedral | 3-4 weeks | Swimming/, Physics manipulation, Sound system |
| T2 | Shoals, Mirrortown, Synapse Row | 2-3 weeks | Deep-water pressure, Social deception, Psychic system |
| T3 | Quarantine, Old Growth, Deadwave | 2-3 weeks | Infection (reuse Heat), Living terrain, Tech suppression |
| T4 | Auction, Nursery, Skyfall Ruins | 2 weeks | Economy warfare, AI companion, Zero-G (reuse Environment/) |

**Total estimated**: 12 districts x ~2.5 weeks average = ~30 weeks (7.5 months) post-vertical-slice

**Parallelization**: After Tier 1, content teams can work on 2-3 districts simultaneously since the pipeline is proven and framework systems are stable.

---

## Shared Pipeline (13.1-13.5)

Every district above uses the same pipeline:

1. **DistrictDefinitionSO** (13.1) — master definition with all references
2. **IZoneProvider implementation** (13.2) — generation mode per district
3. **4 FactionDefinitionSOs** (13.3) — 3-5 enemy prefabs each
4. **6-8 QuestDefinitionSOs + DistrictGoalExtensionSOs** (13.4) — side goals + main chain
5. **LandmarkPOIs + MicroPOIPoolSO** (13.5) — 5-6 landmarks + environmental details
6. **FrontDefinitionSO** (EPIC 3) — 4 phases + pulses
7. **BossDefinitionSO** (EPIC 14) — boss fight per district
8. **EchoFlavorSO** (EPIC 5) — echo theme per district

---

## Setup Guide

1. **Do not start remaining districts until 13.6-13.8 vertical slice is validated** — the pipeline must be proven before scaling
2. **Create `Assets/Data/Districts/<DistrictName>/` folder** per district as production begins
3. **Follow Tier order** — Tier 1 first (system dependencies), then Tier 2-4
4. **For each district**: author DistrictDefinitionSO first, then factions, then goals, then geometry, then Front tuning, then POIs, then boss
5. **Track production state** with DistrictContentState enum per district
6. **Reuse enemy archetypes** across districts where possible (e.g., Quarantine Enforcers share AI profile with Waste Management from The Burn)

---

## Verification

- [ ] All 12 district templates have: theme, unique mechanic, topology, 4 factions, Front, goals, boss, echo, reanimation
- [ ] Production tiers ordered by engineering dependency (T1 needs new systems, T4 reuses existing)
- [ ] No two districts share the same unique mechanic
- [ ] Each district's Front type is mechanically distinct from all others
- [ ] Faction roster across all 15 districts = 60 unique factions (no ID collisions)
- [ ] Total enemy prefab estimate: 60 factions x 3-5 types = 180-300 enemy types
- [ ] Production estimate realistic: ~2-4 weeks per district with proven pipeline
- [ ] Shared pipeline components (13.1-13.5) sufficient for all district types
- [ ] Engineering dependencies identified for Tier 1 districts (Swimming/, Physics, Sound)
- [ ] Parallelization plan: 2-3 districts in simultaneous production after Tier 1

---

## Validation

```csharp
// File: Assets/Editor/District/AllDistrictsValidator.cs
// Build-time sweep across all 15 districts:
//
//   1. Run DistrictCompletenessValidator (13.1) on every DistrictDefinitionSO
//   2. Cross-district checks:
//      [ERROR] Two districts share the same DistrictId
//      [ERROR] FactionId collision across any two FactionDefinitionSOs
//      [ERROR] Any district missing from DistrictId enum (15 expected)
//   3. Production state tracking:
//      [INFO] Districts at each DistrictContentState (summary report)
//      [WARNING] Any Tier 1 district not at GoalsAuthored+ before Tier 2 starts
//   4. Mechanic uniqueness:
//      [WARNING] Two districts share the same Front type name
//      [WARNING] Two districts share identical faction behavior profiles
//
// Output: AllDistrictsReport with per-district scores and global issues
```

---

## Simulation & Testing

```csharp
// File: Assets/Tests/District/AllDistrictsGenerationTest.cs
// [Test] AllDistricts_CompletenessScore_Above70
//   Run DistrictCompletenessValidator on every authored DistrictDefinitionSO,
//   verify all score >= 70% (build gate).
//
// [Test] AllDistricts_FactionIdUniqueness
//   Collect all FactionId values from all FactionDefinitionSOs,
//   verify no duplicates (60 unique factions expected at full content).
//
// [Test] AllDistricts_FrontTypeDiversity
//   Collect all FrontDefinitionSO names,
//   verify all 15 are distinct.
//
// [Test] RemainingDistricts_TemplateFields
//   For each of the 12 remaining districts in this file:
//   verify theme, factions (4), Front type, side goal count, boss name,
//   echo theme, and reanimation are all non-empty strings.
```
