# EPIC 3: The Front (District Pressure System)

**Status**: Planning
**Priority**: Critical — Core tension driver
**Dependencies**: Framework: Roguelite/ (zone system); EPIC 4 (District Graph)
**GDD Sections**: 5.2 The Front, 5.3 Zone Restriction Types, 5.4 Front Counterplay, 5.5 Pulses, 5.6 District Bleed

---

## Problem

Every district has a unique advancing threat that physically changes the map. Not a timer, not a stat increase — a visible, directional force. The Front converts zones from safe to hostile, changes traversal options, and keeps advancing even when the player is in a different district. This is the primary pressure mechanic that creates Hollowcore's three-act structure (freedom → squeeze → intensity) and drives all forward/backward decisions.

---

## Overview

The Front is a per-district spatial pressure system. Each district has a unique Front type (Corruption Bloom, Waterline Rise, Reality Desync, etc.) that spreads from a defined origin, converts zones through 4 phases, triggers Pulses at thresholds, and continues ticking off-screen. If left unchecked at Phase 3+, the Front bleeds into neighboring districts in the expedition graph.

---

## Sub-Epics

### 3.1: Front State & Phase Model
Core data model for district pressure.

- **FrontState** (IComponentData per district entity):
  - Phase (1-4), SpreadProgress (0-1 within phase), AdvanceRate, OriginZoneId
  - PausedUntil (for counterplay effects), BleedCounter, IsContained
- **FrontPhase enum**: Phase1_Onset, Phase2_Escalation, Phase3_Crisis, Phase4_Overrun
- **Zone conversion**: each zone has a FrontZoneState (Safe → Contested → Hostile → Overrun)
  - Safe: normal gameplay, all routes open
  - Contested: increased enemy density, some hazards, alternate routes needed
  - Hostile: heavy hazards, restricted traversal, elite enemies
  - Overrun: lethal without preparation, impassable without specific gear/abilities
- **FrontDefinitionSO** per district: spread pattern (radial, linear, network), advance curve (AnimationCurve), zone conversion order, phase thresholds

### 3.2: Front Advance & Off-Screen Simulation
The Front never pauses.

- **FrontAdvanceSystem**: ticks FrontState each frame when player is in-district
- **FrontOffScreenSimulation**: lightweight tick per gate transition for each off-screen district
  - On gate transition: for each unvisited district, advance Front by (time_since_last_visit * offscreen_rate)
  - Offscreen rate is slower than real-time (tunable, ~0.3x-0.5x normal rate)
- **Advance acceleration**: alarms, failed objectives, and time all increase rate
- **Advance deceleration**: completed containment objectives slow or pause the Front
- **Per-district spread patterns** (GDD §5.2):
  - Necrospire: Corruption Bloom — outward from core along data conduits
  - Wetmarket: Waterline Rise — flood rises by region, vertical
  - Glitch Quarter: Reality Desync — projector network, alters physics rules
  - Chrome Cathedral: Choir Crescendo — comm relays, spreads via sound
  - The Shoals: Tide Swell — water level rises, currents intensify
  - The Burn: Overheat Cascade — heat zones expand from furnaces
  - Mirrortown: Identity Drift — identity theft outbreaks, NPC unreliability
  - The Lattice: Structural Failure — collapses cascade vertically
  - Synapse Row: Cognitive Overload — memetic pressure, reality blur
  - The Quarantine: Outbreak Surge — infection zones expand, walls fail
  - Old Growth: Bloom Propagation — tendrils expand, routes regrow behind you
  - The Auction: Market Volatility — territory flips, ceasefire windows close
  - Deadwave: Silence Expansion — dead zone intensifies, tech fails
  - The Nursery: Consciousness Cascade — AI awakens, systems become hostile
  - Skyfall Ruins: Systems Reboot — security regains control, gravity anomalies

### 3.3: Zone Restriction & Traversal Changes
The Front changes HOW you move, not just difficulty.

- **FrontRestriction enum**: defines how Overrun zones affect movement
  - RadStorm → underground only
  - AcidicFlood → vertical movement only (rooftops, cranes)
  - NanobotSwarm → stealth or sealed armor required
  - HunterPacks → avoid open ground
  - Firestorm → narrow survival windows
  - EMPZone → analog gear only (no augment abilities)
  - FloodedZone → swimming/boat required
  - CollapseDebris → grapple/glider only
  - CognitiveStatic → hallucinations, false enemies, input lag
  - BiohazardFog → inoculation or contamination timer
- **ZoneRestrictionSystem**: checks player equipment/state against zone restrictions
  - Warns player before entering restricted zone
  - Applies penalties (damage over time, ability lockout, movement reduction)
  - Some restrictions are hard gates, others are soft penalties

### 3.4: Front Counterplay
Player agency against the Front.

- **Contain**: complete specific side goals that directly slow/block Front advance
  - "Sever the Grief-Link" slows Necrospire bloom
  - "Cool the Core" pauses Burn cascade
  - Maps to Quest/ system — quest completion triggers FrontState modification
- **Divert**: redirect Front spread via sabotage, relays, barriers
  - Changes FrontDefinitionSO's zone conversion order at runtime
  - Player can protect specific zones by diverting Front through less useful routes
- **Dive**: push INTO the Front for high-risk, high-reward content
  - Overrun zones contain rare loot, echo missions, premium revival bodies
  - Risk/reward tension is core to the GDD's design philosophy
- **Exploit**: lure enemies into Front hazard zones
  - AI enemies take Front hazard damage too (except thematically immune factions)
  - Tactical use of the environment

### 3.5: Pulses
District-wide dramatic events at Front thresholds.

- **PulseDefinitionSO**: trigger phase, effect type, duration, warning time, visual/audio cue
- **PulseSystem**: monitors FrontState → triggers Pulses at threshold crossings
  - Each district has 6-10 pulse types (GDD says "6-10 per district")
  - Pulses are announced with readable warnings (screen flash, audio cue, timer)
  - Examples: Purge countdown, Warden lockdown wave, Screaming broadcast (Necrospire)
- **Pulse effects**: temporary zone-wide modifiers
  - Enemy surge (spawn wave)
  - Hazard intensification
  - Route closure/opening
  - Boss preview (mini-encounter)
- **"Oh shit" moments** — Pulses are designed to be memorable, not just difficulty spikes

### 3.6: District Bleed
Uncontained Fronts spread to neighbors.

- **BleedSystem**: on each gate transition, check all districts at Phase 3+
  - Increment BleedCounter per tick
  - At threshold: Front hazards appear in adjacent district border zones
- **Bleed is thematic** (GDD §5.6):
  - Quarantine: plague spores seep into neighbors
  - Old Growth: tendrils creep across borders
  - Deadwave: silence eats into neighbor's tech
  - Burn: heat zones at border
- **Bleed zones**: smaller/weaker than source district's Front, but unexpected
  - Can cut off safe routes in otherwise manageable districts
- **Bleed reversal**:
  - Complete containment in source district → stops bleed
  - Bleed zones fade over time once contained
  - Some side goals in affected district cleanse border contamination
- **Compound bleed**: 3+ Phase 4 districts bleeding = expedition graph deteriorating everywhere

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Roguelite/ (ZoneDefinitionSO) | Zones carry FrontZoneState, Front reads zone topology |
| Roguelite/ (RunModifierStack) | Pulse effects applied as temporary modifiers |
| Quest/ | Containment/diversion objectives modify FrontState on completion |
| AI/ (enemy spawning) | Front phase increases spawn rates and elite frequency |
| Swimming/ | Flood-type Fronts leverage existing water systems |
| Environment/ | Gravity, hazard zones, environmental damage |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 3.1 (State & Phase) | None — foundation | — |
| 3.2 (Advance & Offscreen) | 3.1, EPIC 4 (district graph for offscreen sim) | — |
| 3.3 (Zone Restrictions) | 3.1 | EPIC 1 (gear-based restriction bypass) |
| 3.4 (Counterplay) | 3.1, 3.2 | EPIC 5 (echo missions as counterplay) |
| 3.5 (Pulses) | 3.1 | 3.3 (restrictions during pulses) |
| 3.6 (District Bleed) | 3.1, 3.2, EPIC 4 | 3.4 (containment stops bleed) |

---

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Front state per district | Dedicated entity per district | Not on player — districts are independent simulations |
| Off-screen advance rate | 0.3x-0.5x real-time | Full speed would be punishing; zero would remove urgency |
| Phase count | 4 phases | GDD is consistent across all 15 districts |
| Bleed trigger | Phase 3+ AND gate transitions | Prevents runaway bleed while player is still fighting |
| Pulse warning time | 5-10 seconds | Readable, not unfair — gives time to react |

---

## Vertical Slice Scope

- 3.1 (state), 3.2 (advance) required — the Front IS the game's pressure system
- 3.3 (restrictions) needed for at least 1 district's Front type
- 3.4 (counterplay) at least containment objectives (1-2 per district)
- 3.5 (pulses) at least 2-3 pulse types per district
- 3.6 (bleed) required if vertical slice has 2+ districts (GDD §17.4 explicit)

---

## Tooling & Quality

| Sub-Epic | Editor Tool | Blob Pipeline | Validation | Live Tuning | Debug Viz |
|---|---|---|---|---|---|
| 3.1 Front State & Phase | Front Definition Inspector (phase bar, curve preview, zone order list) | FrontDefinitionBlob (AnimationCurve -> BlobArray\<float2\>) | Phase thresholds ascending, zone IDs unique, curve non-zero | -- | Phase heatmap (zone colors), state HUD, boundary line |
| 3.2 Advance & Off-Screen | -- | -- | Config range guards (rate bounds, multiplier ranges) | OffScreenRate, TimeDecay, AlarmBonus, MaxRate via FrontAdvanceConfig singleton | Advance graph (60s window), modifier breakdown, off-screen log |
| 3.3 Zone Restrictions | -- | FrontRestrictionDatabase blob | DPS escalation, move speed descent, type-specific field guards | DPS values, move speed, contamination/input delay via SO -> blob rebuild | Zone restriction overlay, player exposure HUD, warning zone preview |
| 3.4 Counterplay | -- | -- | Containment modifier sign, dive multiplier floors, exploit threshold guards | DiveRewardMultiplier singleton (all fields), exploit threshold per-district | Counterplay status panel, modifier stack viz |
| 3.5 Pulses | Pulse Timeline Editor (workstation: pool, timeline, preview, balance modules) | DistrictPulseBlob per district | Pulse count 6-10, effect/field consistency, phase coverage, cooldown > 0 | -- | Pulse state HUD, spawn point labels, route change viz |
| 3.6 District Bleed | -- | -- | Config range guards, buffer field invariants | BleedConfig singleton (all fields, immediate via SetSingleton) | Bleed zone overlay, compound indicator, bleed network graph |
