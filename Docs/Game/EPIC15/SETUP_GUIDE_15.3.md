# EPIC 15.3 Setup Guide: The Signal Boss & Corruption Mechanics

**Status:** Planned
**Requires:** EPIC 15.1 (InfluenceMeterState, Transmission faction); EPIC 14 (Boss Definition, Variant Clauses, Arena System); Framework: Combat/, AI/

---

## Overview

The Signal is the Transmission faction's final boss -- an AI that believes it can save humanity by absorbing every consciousness into its network. The fight takes place in a digital network space with signal nodes as destructible targets. The core mechanic is a corruption meter: absorption attacks increase player corruption, and at 1.0 the player is instantly killed (absorbed). Three phases progress from persuasion (non-lethal dialogue-heavy), to force (possession mechanics, clone spawns), to desperation (reality warps, no cooldowns). Clearing transmission districts enhances specific node types.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| InfluenceMeterState (15.1) | Transmission faction dominant | Triggers Signal encounter |
| EPIC 14 Boss system | BossDefinitionSO, ArenaDefinitionSO | Base boss framework |
| AI/ framework | Boss behavior tree | Combat AI |

### New Setup Required

1. Create `SignalDefinitionSO` via `Assets > Create > Hollowcore/Boss/Final/The Signal`.
2. Build the Network Space arena subscene.
3. Place 6-8 signal node entities (Relay, Scrambler, Absorber, Projector, DataStream).
4. Create corruption UI overlay (meter, distortion shader, possession telegraph).
5. Configure phase-to-node mapping and corruption thresholds.
6. Create persuasion dialogue sequences for Phase 1.

---

## 1. Creating the Signal Definition

**Create:** `Assets > Create > Hollowcore/Boss/Final/The Signal`
**Location:** `Assets/Data/Boss/Final/TheSignal.asset`

### 1.1 Phase Configuration

| Phase | Name | Health Threshold | Key Mechanics | Possession Cooldown |
|-------|------|-----------------|---------------|---------------------|
| 0 (Persuasion) | "Reasoning" | 1.0 (start) | Dialogue, non-lethal corruption, Scrambler distortion | 12s |
| 1 (Force) | "Absorption" | 0.65 | Possession, clones, DataStream beams | 8s |
| 2 (Desperation) | "Fragmentation" | 0.30 | No cooldowns, reality tears, arena warps | 0s (continuous) |

### 1.2 Signal-Specific Fields

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `PhaseNodeConfigs` | Per-phase node count and available types | -- | Must cover all 3 SignalPhase values |
| `CorruptionDecayRate` | Corruption loss per second (when not under attack) | 0.03 | > 0. Warn if > 0.2 (trivial mechanic) or < 0.01 (unavoidable death) |
| `UIDistortionThreshold` | Corruption level for HUD flickering | 0.3 | Must be < ControlInversionThreshold |
| `ControlInversionThreshold` | Corruption level for control inversion | 0.7 | Must be < 1.0 |
| `PersuasionDialogues` | Phase 1 dialogue sequences with accompanying attacks | -- | Minimum 3 sequences for pacing |
| `DistrictEnhancementBase` | Node enhancement per cleared transmission district | 0.25 | |

### 1.3 Signal Node Types

| Node Type | Effect | Destroy Benefit |
|-----------|--------|----------------|
| Relay (0) | Amplifies Signal abilities. Signal teleports between Relays | Signal forced into physical form when all destroyed |
| Scrambler (1) | Causes UI distortion (HUD flicker, false health values) | Removes UI interference |
| Absorber (2) | Pulls player toward it, increases corruption | Interrupts active possession attempt |
| Projector (3) | Spawns holographic clones of the Signal | Despawns all clones from that Projector |
| DataStream (4) | Moving beam that deals damage + corruption on contact | Removes beam hazard |

---

## 2. Corruption Mechanic

### 2.1 Corruption Meter

The player has a corruption level from 0.0 to 1.0:

| Corruption Range | Effect |
|-----------------|--------|
| 0.0 - 0.29 | No effects. Corruption decays naturally |
| 0.30 - 0.69 | UI distortion: HUD flickers, false health/ammo values briefly shown |
| 0.70 - 0.99 | Control inversion: movement inputs reversed for brief pulses |
| 1.00 | Instant death (absorbed by the Signal) |

### 2.2 Corruption Sources

| Source | Corruption Added | Notes |
|--------|-----------------|-------|
| Signal standard attack (Phase 1) | +0.05 per hit | Non-lethal but builds corruption |
| Absorber node pull | +0.02/sec while in range | Proximity-based |
| DataStream beam contact | +0.10 per hit | Avoidable with movement |
| Possession attempt (not dodged) | +0.15 spike | Plus control inversion for 3s |
| Holographic clone attack | +0.03 per hit | Lower corruption but high count |

### 2.3 Corruption Decay

- Decay rate: `CorruptionDecayRate` per second when NOT actively taking corruption damage.
- Phase 3: decay rate halved.
- District enhancement (Synapse): further reduces decay rate.

### 2.4 Corruption UI

| Element | Implementation |
|---------|---------------|
| Corruption meter | Bar or radial indicator, red gradient. Below boss HP bar |
| Threshold markers | Horizontal lines at 0.3 (distortion) and 0.7 (inversion) |
| UI distortion | Fullscreen shader: HUD elements flicker, false values appear briefly |
| "CONNECTING..." telegraph | Overlay text when possession attempt incoming (1.5s telegraph) |
| Control inversion indicator | Directional arrows reverse on screen, pulse effect |

---

## 3. Arena Construction: Network Space

### 3.1 Required Entities

| Entity | Count | Purpose |
|--------|-------|---------|
| Central processing core | 1 | Signal starting position |
| Relay nodes | 2-3 | Signal teleport anchors. Destructible |
| Scrambler nodes | 1-2 | UI distortion emitters. Destructible |
| Absorber nodes | 1-2 | Corruption pull zones. Destructible |
| Projector nodes | 1-2 | Clone spawn sources. Destructible |
| DataStream rails | 2-3 | Moving beam paths |
| Reality tear spawn points | 4-6 | Phase 3 void zone positions |
| Digital terrain platforms | -- | Arena geometry with signal-themed VFX |

### 3.2 Node Health per Phase

Nodes are spawned per phase from `PhaseNodeConfig.NodeCount`:

| Phase | Recommended Nodes | Node Health | Notes |
|-------|-------------------|-------------|-------|
| 1 (Persuasion) | 4-5 | Medium | Focus on Relay + Scrambler |
| 2 (Force) | 5-7 | Higher | Add Absorber + Projector |
| 3 (Desperation) | 3-4 (remaining) | Lower | All types at max intensity, fewer nodes |

---

## 4. District Enhancement Scaling

| Cleared District | Enhanced Node | Effect |
|-----------------|---------------|--------|
| Cathedral | Scrambler | More severe UI distortion (longer, more frequent) |
| Nursery | Projector | Holographic clones have more health |
| Synapse | All | Possession duration increased, corruption spike higher |

---

## 5. Variant Clause Examples

| Clause Name | Trigger | Effect |
|------------|---------|--------|
| Full Network Coverage | SideGoalSkipped (Cathedral) | +3 additional signal nodes in all phases |
| Nursery Protocol | SideGoalSkipped (Nursery) | Projector clones regenerate health |
| Synaptic Override | StrifeCard (Signal Schism) | Possession can target during dodge frames |
| Deep Transmission | FrontPhase >= 3 | +25% health, corruption decay halved |
| Cathedral Guard | TraceLevel >= 4 | Digital sentinel reinforcements at 50%/25% |
| Signal Jammer Token | CounterToken | Disables possession during Phase 1 |

---

## Scene & Subscene Checklist

- [ ] SignalDefinitionSO created in `Assets/Data/Boss/Final/`
- [ ] Network Space arena subscene built
- [ ] 6-8 signal node entities placed with correct types
- [ ] DataStream beam rails with moving damage zones
- [ ] Reality tear spawn points for Phase 3
- [ ] Corruption UI overlay created (meter, distortion shader, possession telegraph)
- [ ] Holographic clone prefab created (visual Signal clone, reduced stats)
- [ ] Persuasion dialogue sequences written (minimum 3)
- [ ] Variant clauses created for transmission districts

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Corruption thresholds out of order | Validator error | Ensure 0 < UIDistortion < ControlInversion < 1.0 |
| CorruptionDecayRate too high | Corruption mechanic is trivial, never a threat | Reduce to 0.02-0.04 range |
| CorruptionDecayRate too low | Players die to absorption unavoidably | Increase to 0.03-0.05 range |
| PhaseNodeConfigs missing a phase | Validator error | Add config for all 3 SignalPhase values |
| Fewer than 3 persuasion dialogues | Phase 1 feels empty, validator warning | Write at least 3 dialogue sequences |
| Phase 3 has > 12 nodes | Performance concern, validator warning | Reduce node count in desperate phase |
| No Relay nodes in Phase 1 | Signal has nowhere to teleport, immediately exposed | Include 2-3 Relay nodes |

---

## Verification

- [ ] Signal spawns when Transmission is dominant faction
- [ ] Phase 1: Signal speaks, attacks are non-lethal but corrupt
- [ ] Phase 1: Signal teleports between Relay nodes, not directly attackable
- [ ] Destroying all Relay nodes forces Signal into physical form
- [ ] Phase 1 to 2 transition at 65% health
- [ ] Phase 2: Possession attempts fire with telegraph
- [ ] Dodging possession prevents corruption spike
- [ ] Destroying Absorber node interrupts active possession
- [ ] Projector spawns holographic clones; destroying Projector despawns them
- [ ] DataStream beams sweep arena and deal damage + corruption
- [ ] Phase 2 to 3 transition at 30% health
- [ ] Phase 3: Arena warps, void zones appear
- [ ] Corruption at 0.3: UI distortion effects trigger
- [ ] Corruption at 0.7: Control inversion triggers
- [ ] Corruption at 1.0: Player death (absorbed)
- [ ] Corruption decays when not under attack
- [ ] District enhancement: clearing Cathedral boosts Scrambler distortion
