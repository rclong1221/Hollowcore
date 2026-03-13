# EPIC 15.2 Setup Guide: The Architect Boss & Infrastructure Arena

**Status:** Planned
**Requires:** EPIC 15.1 (InfluenceMeterState, Infrastructure faction); EPIC 14 (Boss Definition, Variant Clauses, Arena System); Framework: Combat/, AI/

---

## Overview

The Architect is the Infrastructure faction's final boss -- a city planner who weaponizes the city itself. The fight takes place in the city's control room where infrastructure systems (conveyors, heat vents, pistons, slag floods) serve as both attack vectors and hazards. Three phases progress from defensive (Architect hides behind ShieldWalls), to offensive (all systems weaponized), to desperate (the city destroys itself). Clearing infrastructure districts enhances specific systems, making the boss harder in thematic ways.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| InfluenceMeterState (15.1) | Infrastructure faction dominant | Triggers Architect encounter |
| EPIC 14 Boss system | BossDefinitionSO, ArenaDefinitionSO | Base boss framework |
| AI/ framework | Boss behavior tree | Combat AI |

### New Setup Required

1. Create `ArchitectDefinitionSO` via `Assets > Create > Hollowcore/Boss/Final/The Architect`.
2. Build the City Control Room arena subscene.
3. Place 8 infrastructure system entities (Conveyor, HeatVent, ShieldWall, etc.).
4. Configure phase-to-system mapping in `PhaseInfraConfigs`.
5. Create variant clauses for infrastructure district side goals.
6. Wire district enhancement scaling.

---

## 1. Creating the Architect Definition

**Create:** `Assets > Create > Hollowcore/Boss/Final/The Architect`
**Location:** `Assets/Data/Boss/Final/TheArchitect.asset`

`ArchitectDefinitionSO` extends `BossDefinitionSO` with infrastructure-specific fields:

### 1.1 Phase Configuration

| Phase | Name | Health Threshold | Key Systems | Command Cooldown |
|-------|------|-----------------|-------------|------------------|
| 0 (Defensive) | "Fortification" | 1.0 (start) | ShieldWall, ConveyorTrap, HeatVent | 5s |
| 1 (Offensive) | "Infrastructure Assault" | 0.65 | All 8 systems active | 3s |
| 2 (Desperate) | "Structural Collapse" | 0.30 | All at max intensity + integrity drain | 1.5s |

### 1.2 Architect-Specific Fields

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `PhaseInfraConfigs` | Per-phase list of active InfraSystemTypes | -- | Must cover all 3 ArchitectPhase values |
| `IntegrityDrainRate` | Structural integrity loss per second in Phase 3 | 0.02 | 1.0/rate >= 30s (fight lasts at least 30s in Phase 3) |
| `DistrictEnhancementBase` | Damage boost per cleared infra district | 0.25 | +25% per district. Warn if > 0.5 |
| `DistrictDialogues` | Boss lines referencing cleared districts | -- | "You destroyed my Burn district? Let me show you what it could REALLY do." |

### 1.3 Infrastructure System Types

| System | Phase 1 | Phase 2 | Phase 3 | Effect |
|--------|---------|---------|---------|--------|
| ConveyorTrap (0) | Active | Active | Active | Pushes player into hazards |
| HeatVent (1) | Active | Active | Active | Directional heat blasts |
| StructuralCollapse (2) | -- | Active | Active (continuous) | Ceiling/wall sections fall, arena shrinks |
| ShieldWall (3) | Active | -- | -- | Movable cover for Architect. Player must destroy |
| FloorPanel (4) | -- | Active | Active | Floors open/close over pits |
| PistonStrike (5) | -- | Active | Active | Piston crushes at player position (1.5s telegraph) |
| SlagFlood (6) | -- | Active | Active | Molten slag fills low areas |
| PowerSurge (7) | -- | -- | Active | Random floor sections electrified |

---

## 2. Arena Construction: City Control Room

### 2.1 Required Entities

| Entity | Count | Purpose |
|--------|-------|---------|
| Central control platform | 1 | Architect starting position |
| ShieldWall entities | 4 | Movable, destructible cover. Phase 1 only |
| ConveyorTrap lanes | 2-3 | Directional movement belts |
| HeatVent entities | 3-4 | Cone damage zones with visual telegraph |
| Floor panel entities | 4-6 | Open/close over pits |
| Piston entities | 2-3 | Vertical crush with telegraph visual |
| SlagFlood volume | 1 | Rising level hazard |
| PowerSurge grid overlay | 1 | Grid of electrifiable floor sections |

### 2.2 Structural Integrity

- Phase 3 exclusive mechanic.
- `StructuralIntegrity` starts at 1.0, drains at `IntegrityDrainRate` per second.
- At 0.0: arena fully collapses (wipe condition).
- Visual: ceiling cracks, debris falls, lighting flickers.

**Tuning tip:** At IntegrityDrainRate = 0.02, Phase 3 lasts 50 seconds before collapse. This must be longer than the expected kill time for the remaining 30% HP. Use the DPS Calculator to verify.

---

## 3. District Enhancement Scaling

When the player cleared infrastructure districts before reaching the Architect:

| Cleared District | Enhanced System | Effect |
|-----------------|-----------------|--------|
| Burn | HeatVent + SlagFlood | +25% damage |
| Lattice | StructuralCollapse + PistonStrike | Shorter telegraph (1.5s -> 1.0s) |
| Auction | PowerSurge | Larger coverage area |

Each `ArchitectInfraSystem` buffer element has:
- `EnhancedByDistrictId`: which cleared district boosts this system.
- `EnhancementMultiplier`: applied when that district was cleared.

---

## 4. Variant Clause Examples

| Clause Name | Trigger | Effect |
|------------|---------|--------|
| Full Infrastructure Network | SideGoalSkipped (Lattice) | +2 additional systems active in Phase 1 |
| Emergency Protocols | SideGoalSkipped (Burn) | ShieldWalls regenerate after destruction |
| Market Leverage | StrifeCard (Economic Pressure) | Architect summons merc reinforcements |
| Deep Foundations | FrontPhase >= 3 | +25% health, integrity drains 50% slower |
| Warden Garrison | TraceLevel >= 4 | Warden reinforcements at 50% and 25% |
| Lattice Override Key | CounterToken | Disables StructuralCollapse telegraph reduction |

---

## Scene & Subscene Checklist

- [ ] ArchitectDefinitionSO created in `Assets/Data/Boss/Final/`
- [ ] City Control Room arena subscene built
- [ ] All 8 infrastructure system entities placed
- [ ] ShieldWall entities are destructible with own health pools
- [ ] ConveyorTrap lanes have directional movement
- [ ] SlagFlood volume has rising level mechanic
- [ ] PowerSurge grid has per-section activation
- [ ] Boss spawn point at central control platform
- [ ] Encounter trigger at arena entrance
- [ ] Variant clauses created for infrastructure districts

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| PhaseInfraConfigs missing a phase | Validator error, systems don't activate | Add config for all 3 ArchitectPhase values |
| IntegrityDrainRate too high | Phase 3 ends in < 30s (unfair wipe) | Reduce rate so 1.0/rate >= 30 |
| EnhancedByDistrictId invalid | Enhancement never triggers | Use valid infra district IDs (Lattice, Burn, Auction) |
| EnhancementMultiplier > 1.5 | Single district creates huge spike | Keep at 1.25-1.5 range |
| ShieldWalls not destructible | Player cannot expose Architect in Phase 1 | Add Health component and destruction logic |
| Arena too small for 8 systems | Hazard overlap leaves no safe spots | Expand ArenaBounds, spread system positions |

---

## Verification

- [ ] Architect spawns when Infrastructure is dominant faction
- [ ] Phase 1: ShieldWalls protect Architect, ConveyorTraps push player
- [ ] Phase 1 to 2 transition at 65% health
- [ ] Phase 2: all infrastructure systems weaponized
- [ ] Phase 2 to 3 transition at 30% health
- [ ] Phase 3: StructuralIntegrity drains, arena shrinks
- [ ] Phase 3: wipe condition if StructuralIntegrity reaches 0
- [ ] Infrastructure systems damage both player AND Architect
- [ ] District enhancement: clearing Burn boosts HeatVent/SlagFlood
- [ ] Variant clauses activate/deactivate correctly
- [ ] Counter tokens disable correct clauses
- [ ] CommandCooldown respects per-phase configuration
- [ ] Architect dialogue references cleared districts
