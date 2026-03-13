# EPIC 6.5 Setup Guide: Gate Transition Flow

**Status:** Planned
**Requires:** EPIC 6.1 (ForwardGateOption), EPIC 6.2 (BacktrackGateInfo), EPIC 6.4 (VoteState), EPIC 4.3 (district loading), EPIC 4.2 (DistrictSaveState), EPIC 8 (Trace pause)

---

## Overview

The gate transition flow covers the full pipeline from district extraction through the gate screen to spawning in the next district. The sequence: player reaches extraction zone, cinematic plays, gate screen appears (with background loading), party selects a gate, target district loads, and players spawn. Time on the gate screen does NOT increment Trace. This guide covers extraction zone setup, cinematic configuration, loading screen UI, and the transition pipeline.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| District exit points | ExtractionZoneTag (physics trigger) | Detects player reaching the exit |
| EPIC 4.3 district loader | Consumes DistrictLoadRequest | Generates or regenerates districts |
| EPIC 8 TraceTickSystem | Reads TraceState.IsPaused | Pauses Trace during gate screen |
| EPIC 6.1-6.4 gate systems | Gate screen, vote | Run during GateScreenActive phase |

### New Setup Required
1. Place extraction zone trigger volumes at district exit points
2. Configure cinematic duration on the transition pipeline
3. Build the loading screen UI prefab
4. Wire Trace pause to EPIC 8
5. Configure player input lock during transitions

---

## 1. Extraction Zone Setup

### 1.1 Create Extraction Trigger Volume

1. In your district subscene, create an empty GameObject at each exit point
2. Name it `ExtractionZone_[Direction]` (e.g., `ExtractionZone_North`)
3. **Add Component > PhysicsShapeAuthoring**
   - Shape: Box (size the exit doorway/area)
   - Is Trigger: **true**
   - Collision Filter:
     - Belongs To: Environment
     - Collides With: Player
4. **Add Component > ExtractionZoneAuthoring** (bakes `ExtractionZoneTag`)

### 1.2 ExtractionZoneAuthoring Inspector

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| (Tag only) | No configurable fields | -- | -- |

The zone is purely a trigger detector. All configuration lives on the transition pipeline.

### 1.3 Co-op Extraction Rules

In co-op, extraction requires all players in the zone (or a majority after a grace timer):

| Rule | Condition | Timer |
|------|-----------|-------|
| Full party | All players inside zone | Instant extraction |
| Majority | > 50% of players inside | 10s grace timer, then force-extract |
| Solo | 1 player (solo mode) | Instant extraction |

The `ExtractionSequenceSystem` handles this logic. No additional authoring needed.

**Tuning tip:** The 10s grace timer for majority extraction prevents one AFK player from blocking the group indefinitely while giving stragglers time to reach the exit.

---

## 2. Cinematic Phase Configuration

### 2.1 GateTransitionState Defaults

The `GateTransitionState` singleton is created by `ExtractionSequenceSystem` when extraction triggers. Configure defaults via the `GateTransitionConfigAuthoring` component:

**Create:** Add `GateTransitionConfigAuthoring` to the `GateConfig` GameObject in the Gate subscene.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **CinematicDuration** | Seconds for extraction cinematic | 2.5 | 1.0-5.0 |
| **MajorityExtractionGrace** | Seconds before majority-forces extraction in co-op | 10.0 | 5.0-30.0 |
| **LoadingTimeout** | Max seconds to wait for district load before fallback | 30.0 | 10.0-60.0 |

**Tuning tip:** Keep `CinematicDuration` short (2-3s). The gate screen is the interesting part; the cinematic is just a transition buffer.

### 2.2 Extraction Cinematic Sequence

During the cinematic phase, the system:
1. Disables player input (sets `InputDisabledTag` on player entities)
2. Triggers extraction VFX/animation (camera pull-out, district fade)
3. Snapshots current district state to `ExtractionSnapshot`
4. Writes snapshot to `DistrictSaveState` (EPIC 4.2)

No manual setup needed for the sequence itself. VFX are triggered by a managed bridge.

---

## 3. Loading Screen UI

**Create:** `Assets/Prefabs/UI/Gate/LoadingScreen.prefab`

### 3.1 Prefab Structure

```
LoadingScreen (Canvas, ScreenSpace-Overlay)
  +-- Background (Image, full-screen dark overlay)
  +-- DistrictThumbnail (Image, target district art)
  +-- DistrictNameText (TextMeshProUGUI, "Entering: THE BURN")
  +-- LoadingBar
  |     +-- BarBackground (Image)
  |     +-- BarFill (Image, filled)
  +-- TipText (TextMeshProUGUI, random gameplay tip)
  +-- TransitionTypeIndicator
        +-- ForwardLabel ("New District") or BacktrackLabel ("Returning to...")
```

### 3.2 Loading Screen Adapter

The `LoadingScreenAdapter` MonoBehaviour should:
1. Listen for `GateTransitionState.Phase == LoadingTarget`
2. Crossfade from gate screen to loading screen
3. Read `GateTransitionState.Target` to show "New District" vs "Returning to..."
4. Read `GateTransitionState.TargetDistrictId` to display district name and thumbnail
5. Update loading bar from `DistrictLoadComplete` progress (if available) or show indeterminate
6. Fade out when `Phase == SpawningPlayers`

---

## 4. Trace Pause Integration

### 4.1 TraceTimerPauseSystem

**File:** `Assets/Scripts/Gate/Systems/TraceTimerPauseSystem.cs`

This system reads `GateTransitionState.TraceTimerPaused` and sets `TraceState.IsPaused`:
- During `GateScreenActive` phase: `TraceTimerPaused = true` -- Trace does NOT tick
- During all other phases: `TraceTimerPaused = false` -- Trace ticks normally

### 4.2 EPIC 8 Integration

Ensure EPIC 8's `TraceTickSystem` checks `TraceState.IsPaused` before incrementing Trace:
```
if (TraceState.IsPaused) return; // Skip tick
```

No additional authoring needed. The gate screen is a safe decision space by design.

---

## 5. Player Input Lock

During cinematic and loading phases, player input must be disabled:

| Phase | Input State |
|-------|------------|
| ExtractionTriggered | Disabled (InputDisabledTag added) |
| CinematicPlaying | Disabled |
| GateScreenActive | UI input only (gate selection) |
| LoadingTarget | Disabled |
| SpawningPlayers | Disabled (brief, re-enabled on Complete) |
| Complete | Full input restored (InputDisabledTag removed) |

The `ExtractionSequenceSystem` adds `InputDisabledTag` on extraction. The `GateTransitionSystem` removes it on `Complete`.

---

## 6. District Loading Integration

### 6.1 Forward Gate (New District)

When the winning gate is a forward gate:
1. `GateTransitionSystem` creates `DistrictLoadRequest` with:
   - `DistrictId` from `ForwardGateOption.TargetDistrictId`
   - `Seed` from `ForwardGateOption` (generated by seed utility)
   - `LoadType = Forward`
2. EPIC 4.3 generates a fresh district from seed
3. Front starts at Phase 1 (unless Unknown Clause was `Trap` -- starts at Phase 2)

### 6.2 Backtrack Gate (Revisited District)

When the winning gate is a backtrack gate:
1. `GateTransitionSystem` creates `DistrictLoadRequest` with:
   - `DistrictId` from the voted backtrack district
   - `Seed` from `DistrictSaveState` (original generation seed)
   - `LoadType = Backtrack`
2. EPIC 4.3 regenerates the district from original seed
3. `DistrictSaveState` delta applied:
   - Dead enemies remain dead
   - Opened containers remain open
   - Front advanced to simulated phase
   - Echoes in their current state
   - Seeded events (merchant, vault) placed if flagged

### 6.3 Load Failure Fallback

If `DistrictLoadComplete.Success == false`:
1. Retry once with same parameters
2. If retry fails: fallback to a random available forward gate
3. Log error for diagnostics

---

## 7. Background Pre-Loading

During the gate screen phase, the system can pre-load the most likely target:

| Strategy | When |
|----------|------|
| Pre-load first forward gate | On gate screen open (before any votes) |
| Pre-load highest-voted gate | When 50%+ of votes are on one gate |
| Cancel pre-load on reroll | Reroll invalidates pre-loaded data |

Pre-loading is async scene/subscene loading. It must NOT block gate screen interactivity.

---

## 8. Transition Pipeline Phases

```
GAMEPLAY --> EXTRACTION --> CINEMATIC --> GATE SCREEN --> LOADING --> SPAWN --> GAMEPLAY
  |               |              |              |             |          |         |
  v               v              v              v             v          v         v
Normal       Player at     Camera pull    Gate cards    Load target  Move      Resume
play         exit zone     Snapshot save  Vote active   Apply delta  players   Trace
                           2.5s           Trace PAUSED  if backtrack to spawn  Full input
                                          30-60s timer               point
```

---

## 9. Scene & Subscene Checklist

- [ ] Extraction zone trigger volumes at all district exit points
- [ ] Each zone has `PhysicsShapeAuthoring` (trigger, Player collision) + `ExtractionZoneAuthoring`
- [ ] `GateTransitionConfigAuthoring` on Gate subscene `GateConfig` GameObject
- [ ] `LoadingScreen.prefab` exists at `Assets/Prefabs/UI/Gate/LoadingScreen.prefab`
- [ ] `TraceTimerPauseSystem.cs` in `Assets/Scripts/Gate/Systems/`
- [ ] `ExtractionSequenceSystem.cs` and `GateTransitionSystem.cs` in `Assets/Scripts/Gate/Systems/`
- [ ] EPIC 4.3 district loader consumes `DistrictLoadRequest` and produces `DistrictLoadComplete`
- [ ] EPIC 8 `TraceTickSystem` reads `TraceState.IsPaused` flag

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Extraction zone not a trigger | Physics collision pushes player instead of detecting | Set `Is Trigger = true` on PhysicsShapeAuthoring |
| Extraction zone collision filter wrong | Zone never detects player | Set Collides With to include Player layer |
| Trace not paused during gate screen | Trace increments while players deliberate | Wire `TraceTimerPauseSystem` to set `TraceState.IsPaused = true` during GateScreenActive |
| Player input not restored after transition | Player frozen after spawn | Ensure `GateTransitionSystem` removes `InputDisabledTag` on Phase=Complete |
| Backtrack delta not applied | Revisited district appears fresh (dead enemies alive, containers closed) | Verify EPIC 4.3 applies `DistrictSaveState` delta on `LoadType=Backtrack` |
| Loading screen blocks gate screen | Gate screen invisible behind loading overlay | Loading screen should only appear on `Phase=LoadingTarget`, not during `GateScreenActive` |
| Pre-load not cancelled on reroll | Stale pre-loaded district data used after reroll changes selection | Cancel async load operations when `GateRerollSystem` fires |
| Cinematic too long | Players impatient waiting for gate screen | Keep CinematicDuration at 2-3s; the gate screen is the destination |
| No ExtractionSnapshot saved | Backtrack gates show stale/wrong data | `ExtractionSequenceSystem` must snapshot to `DistrictSaveState` before opening gate screen |

---

## Verification

- [ ] Entering extraction zone triggers extraction sequence
- [ ] Co-op: extraction requires all players or majority + grace timer
- [ ] Extraction cinematic plays for configured duration
- [ ] Player input disabled during cinematic and loading phases
- [ ] Gate screen appears after cinematic (GateSelectionState.IsActive = true)
- [ ] Trace timer paused during entire gate screen duration
- [ ] Forward gate: DistrictLoadRequest created with Forward type and correct seed
- [ ] Backtrack gate: DistrictLoadRequest created with Backtrack type and saved seed
- [ ] Forward district starts at Phase 1 (or Phase 2 if Trap clause)
- [ ] Backtrack district has correct delta (dead enemies dead, containers open, Front advanced)
- [ ] Players spawn at correct spawn point in target district
- [ ] Input fully restored after spawn
- [ ] Trace timer resumes after spawn
- [ ] ExpeditionGraphState updated (new district current, old visited)
- [ ] Load failure: retries once, then falls back to random forward gate
- [ ] Full pipeline completes in under 10 seconds on target hardware
- [ ] Run `Hollowcore > Simulation > Gate Transition Pipeline` with all tests passing
