# EPIC 6.5: Gate Transition Flow

**Status**: Planning
**Epic**: EPIC 6 — Gate Selection & Navigation
**Dependencies**: 6.1 (ForwardGateOption); 6.2 (BacktrackGateInfo); 6.4 (VoteState — vote resolution); EPIC 4.3 (district loading/generation); EPIC 4.2 (DistrictSaveState); EPIC 8 (Trace — gate screen is safe time)

---

## Overview

This sub-epic covers the full pipeline from district extraction through the gate screen to spawning in the next district. The sequence: player reaches the extraction point, an extraction cinematic plays, the gate screen appears (overlapping with background loading), the party selects a gate, and the target district loads. Forward gates generate a new district from seed; backtrack gates regenerate from seed plus saved delta. Time spent on the gate screen does NOT increment Trace — the gate screen is a safe decision space.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Gate/Components/GateTransitionComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Gate
{
    /// <summary>
    /// Phases of the full extraction→gate→spawn pipeline.
    /// </summary>
    public enum TransitionPhase : byte
    {
        None = 0,
        ExtractionTriggered = 1,   // Player reached exit point
        CinematicPlaying = 2,      // Extraction cinematic/animation
        GateScreenActive = 3,      // Gate UI visible, vote in progress
        LoadingTarget = 4,         // Target district being loaded/generated
        SpawningPlayers = 5,       // Players being placed in new district
        Complete = 6               // Transition done, normal gameplay resumes
    }

    /// <summary>
    /// Whether the target is a new district or a revisited one.
    /// Determines loading strategy.
    /// </summary>
    public enum TransitionTarget : byte
    {
        None = 0,
        Forward = 1,     // Generate new district from seed
        Backtrack = 2    // Regenerate from seed + apply DistrictSaveState delta
    }

    /// <summary>
    /// Singleton tracking the transition pipeline state. Server-authoritative.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct GateTransitionState : IComponentData
    {
        public TransitionPhase Phase;
        public TransitionTarget Target;
        public int TargetDistrictId;              // Resolved from vote result
        public uint TargetDistrictSeed;           // Seed for generation/regeneration
        public float PhaseTimer;                  // Time spent in current phase
        public float CinematicDuration;           // Configured per extraction type
        public bool TraceTimerPaused;             // True during GateScreenActive phase
    }

    /// <summary>
    /// Tag on extraction zone trigger volumes.
    /// When player enters, begins extraction sequence.
    /// </summary>
    public struct ExtractionZoneTag : IComponentData { }

    /// <summary>
    /// Event: player has entered an extraction zone.
    /// Transient — consumed by ExtractionSequenceSystem.
    /// </summary>
    public struct ExtractionTriggerEvent : IComponentData
    {
        public Entity PlayerEntity;
        public int PlayerNetworkId;
    }

    /// <summary>
    /// Snapshot of district state saved on extraction for backtrack delta.
    /// Written to DistrictSaveState (EPIC 4.2) before transition.
    /// </summary>
    public struct ExtractionSnapshot : IComponentData
    {
        public int DistrictId;
        public byte FrontPhaseOnExit;
        public int ActiveEchoCount;
        public int ExpeditionTurnOnExit;
        public float PlayerHealthOnExit;          // For UI display in backtrack gate
    }

    /// <summary>
    /// Request to begin loading a target district. Consumed by district loading (EPIC 4.3).
    /// </summary>
    public struct DistrictLoadRequest : IComponentData
    {
        public int DistrictId;
        public uint Seed;
        public TransitionTarget LoadType;         // Forward=fresh generate, Backtrack=regenerate+delta
    }

    /// <summary>
    /// Response from district loading system. Transient.
    /// </summary>
    public struct DistrictLoadComplete : IComponentData
    {
        public int DistrictId;
        public bool Success;
        public Entity SpawnPointEntity;           // Where players should spawn
    }
}
```

---

## Systems

### ExtractionSequenceSystem

```csharp
// File: Assets/Scripts/Gate/Systems/ExtractionSequenceSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Monitors extraction triggers and manages the cinematic phase:
//   1. Detect ExtractionTriggerEvent entities (from physics trigger overlap):
//      a. Validate: is this player alive? Is extraction allowed (not mid-combat lock)?
//      b. In co-op: require all players in extraction zone OR majority + timer
//      c. Create GateTransitionState singleton:
//         - Phase = ExtractionTriggered
//         - TraceTimerPaused = false
//   2. ExtractionTriggered → CinematicPlaying:
//      - Disable player input (set InputLock flag)
//      - Trigger extraction VFX/animation (camera pull-out, district fade)
//      - Snapshot current district state → ExtractionSnapshot
//      - Write ExtractionSnapshot to DistrictSaveState (EPIC 4.2)
//   3. CinematicPlaying → GateScreenActive:
//      - After CinematicDuration elapsed:
//      - Set GateSelectionState.IsActive = true (triggers 6.1 + 6.2 systems)
//      - Set TraceTimerPaused = true (EPIC 8 Trace does NOT tick)
//      - Begin background pre-loading of likely districts (top-weighted forward gates)
//   4. Destroy ExtractionTriggerEvent entities
```

### GateTransitionSystem

```csharp
// File: Assets/Scripts/Gate/Systems/GateTransitionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: PartyVoteSystem
//
// Manages the post-vote loading and spawn pipeline:
//   1. GateScreenActive → LoadingTarget (when VoteState.VoteComplete == true):
//      a. Read VoteState.WinningDirection and WinningGateIndex
//      b. Resolve target:
//         - Forward: read ForwardGateOption[WinningGateIndex].TargetDistrictId + seed
//         - Backtrack: WinningGateIndex IS the DistrictId; seed from DistrictSaveState
//      c. Set GateTransitionState: Target, TargetDistrictId, TargetDistrictSeed
//      d. Create DistrictLoadRequest entity
//      e. Set GateSelectionState.IsActive = false (triggers gate cleanup systems)
//      f. Display loading screen UI (replaces gate screen)
//
//   2. LoadingTarget → SpawningPlayers (when DistrictLoadComplete received):
//      a. Validate DistrictLoadComplete.Success
//         - On failure: retry once, then fallback to random forward gate
//      b. For forward target:
//         - New district generated from seed by EPIC 4.3
//         - Front starts at Phase 1 (or modified by Unknown Clause trap)
//      c. For backtrack target:
//         - District regenerated from original seed by EPIC 4.3
//         - DistrictSaveState delta applied:
//           * Front advanced to simulated phase
//           * Defeated enemies remain dead
//           * Opened containers remain open
//           * Echoes in their current state (alive/dead/mutated)
//           * Seeded events (merchant, vault) placed if flagged
//      d. Move all players to SpawnPointEntity position
//      e. Set Phase = SpawningPlayers
//
//   3. SpawningPlayers → Complete:
//      a. Re-enable player input (clear InputLock)
//      b. Resume Trace timer (TraceTimerPaused = false)
//      c. Update ExpeditionGraphState: mark new district as current, previous as visited
//      d. Increment expedition turn counter
//      e. Set Phase = Complete
//      f. Destroy GateTransitionState singleton (or reset Phase = None)
```

### TraceTimerPauseSystem

```csharp
// File: Assets/Scripts/Gate/Systems/TraceTimerPauseSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: TraceTickSystem (EPIC 8)
//
// Reads GateTransitionState.TraceTimerPaused:
//   - If true: set TraceState.IsPaused = true (EPIC 8 TraceTickSystem skips tick)
//   - If false or singleton doesn't exist: set TraceState.IsPaused = false
//
// Ensures the gate screen is a safe decision space with no time pressure
// from the Trace mechanic. The vote timer (EPIC 6.4) is the only time pressure.
```

---

## Transition Pipeline Diagram

```
    GAMEPLAY                  EXTRACTION              GATE SCREEN              LOADING              GAMEPLAY
   ─────────►              ─────────────►           ──────────────►          ──────────►          ─────────►

  Player at exit    ┌─────────────────┐     ┌──────────────────┐     ┌─────────────┐     ┌──────────┐
  trigger volume───►│ Cinematic plays │────►│ Gate cards shown │────►│ Load target │────►│ Spawn in │
                    │ Snapshot saved  │     │ Vote in progress │     │ Apply delta  │     │ new dist │
                    │ 2-3 sec         │     │ Trace PAUSED     │     │ if backtrack │     │ Resume   │
                    └─────────────────┘     │ 30-60 sec timer  │     └─────────────┘     └──────────┘
                                            └──────────────────┘
                                              Pre-load likely
                                              targets in background
```

---

## Setup Guide

1. Add `GateTransitionComponents.cs` to `Assets/Scripts/Gate/Components/`
2. Add `ExtractionSequenceSystem.cs`, `GateTransitionSystem.cs`, `TraceTimerPauseSystem.cs` to `Assets/Scripts/Gate/Systems/`
3. Place `ExtractionZoneTag` on trigger volume entities at district exit points (physics trigger collider + authoring)
4. Physics trigger detection: use an `ITriggerEventsJob` or `StatefulTriggerEvent` to detect player entering extraction zone → create `ExtractionTriggerEvent`
5. Configure `CinematicDuration` on `GateTransitionState` authoring (default 2.5 seconds)
6. District loading integration: `DistrictLoadRequest` must be consumed by EPIC 4.3's district generation/loading system. `DistrictLoadComplete` must be created by that system when done
7. Backtrack delta application: EPIC 4.3 must support regeneration from seed + delta from `DistrictSaveState`:
   - Dead enemies: `EntityDeathRecord` list in save state
   - Opened containers: `ContainerOpenRecord` list
   - Front phase: advance to simulated phase
   - Echo states: restore from `EchoSaveState`
8. Trace pause: ensure EPIC 8's `TraceTickSystem` reads `TraceState.IsPaused` flag
9. Player input lock: set `InputDisabledTag` (or equivalent) on player entities during cinematic and loading phases
10. Background pre-loading: during gate screen, begin async scene/subscene loading for the most likely forward gate (highest vote count or first gate if no votes yet)
11. Loading screen UI: crossfade from gate screen; show target district name, thumbnail, and loading bar

---

## Verification

- [ ] Entering extraction zone triggers extraction sequence
- [ ] Co-op: extraction requires all players in zone (or majority + grace timer)
- [ ] Extraction cinematic plays for configured duration
- [ ] ExtractionSnapshot correctly captures: FrontPhase, EchoCount, ExpeditionTurn
- [ ] DistrictSaveState updated with snapshot before gate screen opens
- [ ] Gate screen appears after cinematic (GateSelectionState.IsActive = true)
- [ ] Trace timer is paused during entire gate screen duration
- [ ] Forward gate selection: creates DistrictLoadRequest with Forward type and correct seed
- [ ] Backtrack gate selection: creates DistrictLoadRequest with Backtrack type and saved seed
- [ ] Forward district generates fresh from seed (Phase 1, no prior state)
- [ ] Backtrack district regenerates with delta: dead enemies stay dead, containers stay open, Front at simulated phase
- [ ] Players spawn at correct spawn point in target district
- [ ] Player input disabled during cinematic and loading, re-enabled on spawn
- [ ] Trace timer resumes after spawn
- [ ] ExpeditionGraphState updated: new district = current, old = visited
- [ ] DistrictLoadComplete failure: retry once, then fallback
- [ ] Background pre-loading of likely target does not block gate screen interactivity
- [ ] Full pipeline completes in under 10 seconds for forward gates on target hardware

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Gate/Debug/TransitionDebugOverlay.cs
// Managed SystemBase, ClientSimulation | LocalSimulation, PresentationSystemGroup
//
// Gate Transition Pipeline Overlay — toggled via debug console: `gate.transition.debug`
//
// Displays:
//   1. GateTransitionState:
//      - Current TransitionPhase (highlighted in pipeline diagram)
//      - TransitionTarget (Forward / Backtrack)
//      - TargetDistrictId, TargetDistrictSeed
//      - PhaseTimer (elapsed in current phase)
//      - TraceTimerPaused flag
//   2. Pipeline progress bar:
//      - [Extraction] → [Cinematic] → [Gate Screen] → [Loading] → [Spawning] → [Complete]
//      - Active phase highlighted, completed phases green, pending phases grey
//   3. Loading metrics:
//      - DistrictLoadRequest: pending / complete
//      - Pre-load status for background loading
//      - Time spent in LoadingTarget phase
//   4. ExtractionSnapshot: FrontPhaseOnExit, ActiveEchoCount, ExpeditionTurnOnExit
//
// Useful for profiling transition pipeline latency and diagnosing loading stalls.
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/GateWorkstation/TransitionPipelineTest.cs
using UnityEditor;

namespace Hollowcore.Gate.Editor
{
    /// <summary>
    /// Transition pipeline validation.
    /// Menu: Hollowcore > Simulation > Gate Transition Pipeline
    /// </summary>
    public static class TransitionPipelineTest
    {
        [MenuItem("Hollowcore/Simulation/Gate Transition Pipeline")]
        public static void RunPipelineTests()
        {
            // Test: Phase ordering
            //   - Verify phases transition in strict order:
            //     ExtractionTriggered → CinematicPlaying → GateScreenActive
            //     → LoadingTarget → SpawningPlayers → Complete
            //   - Verify no phase is skipped

            // Test: Trace timer pause
            //   - Verify TraceTimerPaused=true during GateScreenActive
            //   - Verify TraceTimerPaused=false during all other phases
            //   - Verify Trace does not increment during gate screen

            // Test: Backtrack delta application
            //   - Create mock DistrictSaveState with 3 dead enemies, 2 opened containers
            //   - Verify regeneration from seed + delta produces district with:
            //     * Dead enemies remain dead
            //     * Opened containers remain open
            //     * Front at simulated phase (not Phase 1)

            // Test: Forward generation
            //   - Verify forward target always starts at Phase 1
            //   - Verify seed produces identical district layout on repeated generation

            // Test: Loading failure fallback
            //   - Simulate DistrictLoadComplete.Success=false
            //   - Verify retry once
            //   - Verify fallback to random forward gate after retry failure
        }
    }
}
```
