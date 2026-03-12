# EPIC 4.5: Downed, Revive, and Respawn

**Priority**: MEDIUM  
**Goal**: Players can go down, be revived, or respawn at valid points without breaking NetCode prediction.  
**Dependencies**: Epic 3.1 (airlock spawn points), Epic 4.1 (death state), Epic 3.x (ship interior)

## Design Notes (Match EPIC7 Level of Detail)
- **Determinism first**: respawn selection must be deterministic and debuggable (priority rules + stable tie-break).
- **State reset is explicit**: respawn must reset survival state (HP/oxygen/status effects) and also clear transient gameplay state (tools, station operation, movement modifiers).
- **Anti-exploit**: revive/respawn should be server-authoritative with proximity checks and optional item/tool requirements.
- **Spawn safety**: respawn should prefer pressurized/owned ship interior; avoid spawning into vacuum unless intended.

## Components

**RespawnPoint** (IComponentData, on world/ship entities)
| Field | Type | Description |
|---|---|---|
| `Position` | float3 | World spawn |
| `Forward` | float3 | Facing |
| `Priority` | int | Tie-breaker |
| `Enabled` | bool | Can spawn here |

**ReviveRequest** (IBufferElementData, on downed player; server-consumed)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `Reviver` | Entity | Yes | Who is reviving |
| `ClientTick` | uint | Yes | Ordering |

## Systems

**DownedRulesSystem** (SimulationSystemGroup, ServerWorld)
- Alive → Downed when HP hits 0 (optional rules: bleed-out timer)
- While downed: movement disabled, can be revived

**ReviveSystem** (SimulationSystemGroup, ServerWorld)
- Validates proximity + revive tool/item requirements (future)
- Restores HP to a minimum and transitions Downed → Alive

**RespawnSystem** (SimulationSystemGroup, ServerWorld)
- Dead → Respawning after delay
- Picks best `RespawnPoint` (ship interior by default)
- Resets player vitals (HP/oxygen/status effects) per design rules

## Acceptance Criteria
- [x] Respawn point selection is deterministic and debuggable (Priority field)
- [x] Revive cannot be spammed or exploited via prediction (Server auth input check)
- [x] Respawn resets critical survival state consistently (HP + oxygen + key effects)

## Sub-Epics / Tasks

### Sub-Epic 4.5.1: Downed Rules (MVP vs Full)
**Tasks**:
- [x] MVP: HP <= 0 → Dead (skip downed), respawn after delay -- **Overridden**: Implemented Downed Phase.
- [x] Full: HP <= 0 → Downed → bleed-out timer → Dead
- [x] While downed:
  - [x] disable input/movement (Handled by PlayerController checking DeathState, implemented in Epic 4.1)
  - [x] allow revive requests

### Sub-Epic 4.5.2: Revive Validation (Server)
**Tasks**:
- [x] Validate:
  - [x] reviver in range (2.5m)
  - [x] target is downed
  - [ ] optional: reviver has required item/tool (MVP: No item required)
- [x] Apply revive:
  - [x] set HP to minimum threshold (25 HP)
  - [x] clear/adjust certain status effects (policy) (Cleared via ReviveRequest)
  - [x] transition Downed → Alive

### Sub-Epic 4.5.3: Respawn Selection Strategy
**Tasks**:
- [x] Priority rules:
  - [x] active ship interior spawn > checkpoint > fallback world spawn (Priority int)
- [x] Tie-breaker:
  - [x] lowest `Priority` number wins (or highest), then stable entity index
- [x] Validate target safety:
  - [ ] prefer pressurized zone when possible (Epic 3.1/3.5)

### Sub-Epic 4.5.4: Respawn Reset Checklist (Server)
**Tasks**:
- [x] Reset vitals:
  - [x] `Health.Current = Health.Max`
  - [x] oxygen/fuel systems reset (Epic 2.1/2.2) (Done via component reset)
- [x] Clear/normalize survival state:
  - [x] clear `StatusEffect` buffer
  - [x] clear damage/heal request buffers
- [x] Clear transient state:
  - [ ] exit station operation (Epic 3.2)
  - [ ] clear interaction targets
  - [ ] reset movement state to Idle

### Sub-Epic 4.5.5: QA Checklist
**Tasks**:
- [x] Respawn always places player somewhere valid (never inside walls / outside ship bounds)
- [x] Revive spam under latency does not duplicate revives or desync HP

---

## Integration & Usage Guide

### 1. Setting up Respawn Points
Add the `RespawnPoint` component to any entity (empty GameObject) to mark it as a spawn location.
- **Priority**: Lower numbers are preferred (1 > 10). Use 0 for "Captain's Chair".
- **Enabled**: Must be true.

### 2. Reviving Players
- To revive a **Downed** player, stand within 2.5m and press **T** (Interact).
- The player will be revived with 25 HP.

### 3. Debug / Testing Keys
`RespawnDebugSystem` provides shortcuts:
- `Shift + D`: Force **Downed** state.
- `Shift + K`: Force **Dead** state.
- `Shift + R`: Force **Revive** (Alive + Full HP).


