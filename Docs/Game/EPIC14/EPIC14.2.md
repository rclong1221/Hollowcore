# EPIC 14.2: Boss Encounter Flow

**Status**: Planning
**Epic**: EPIC 14 — Boss System & Variant Clauses
**Dependencies**: EPIC 14.1 (BossDefinitionSO, BossVariantState); Framework: Combat/ (EncounterState, EncounterTriggerDefinition), AI/

---

## Overview

The full lifecycle of a boss fight from arena entrance to victory or defeat. Boss encounters use the framework's EncounterState machine but extend it with multi-phase health-threshold transitions, mid-fight Strife interruptions, reinforcement waves, and persistent boss health across attempts. The pre-fight sequence evaluates variant clauses and displays active modifiers before combat begins.

---

## Component Definitions

### BossPhaseState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/BossEncounterComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss
{
    public enum BossEncounterPhase : byte
    {
        Inactive = 0,
        PreFight = 1,      // Clause evaluation, modifier display, arena intro
        Combat = 2,        // Active fight
        PhaseTransition = 3, // Between boss phases (brief invulnerability + cinematic)
        MidFightEvent = 4, // Strife interruption or reinforcement wave
        Victory = 5,       // Boss dead, reward sequence
        Failure = 6        // Player dead, respawn flow
    }

    /// <summary>
    /// Tracks the current state of a boss encounter and which combat phase the boss is in.
    /// Lives on the boss encounter entity alongside EncounterState.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct BossPhaseState : IComponentData
    {
        /// <summary>Overall encounter phase (pre-fight, combat, transition, etc.).</summary>
        [GhostField] public BossEncounterPhase EncounterPhase;

        /// <summary>Current boss combat phase index (0-based, maps to BossDefinitionSO.Phases).</summary>
        [GhostField] public byte CurrentPhaseIndex;

        /// <summary>Total number of phases for this boss.</summary>
        [GhostField] public byte TotalPhases;

        /// <summary>Health threshold for the NEXT phase transition (0-1). 0 = no more transitions.</summary>
        [GhostField(Quantization = 100)] public float NextPhaseThreshold;

        /// <summary>Timer for phase transition cinematics and mid-fight events.</summary>
        [GhostField(Quantization = 100)] public float EventTimer;

        /// <summary>Number of player deaths this encounter (boss adapts after repeated attempts).</summary>
        [GhostField] public byte PlayerDeathCount;

        /// <summary>Boss health is persistent across attempts. Stored here for respawn recovery.</summary>
        [GhostField(Quantization = 10)] public float PersistentBossHealthPercent;
    }
}
```

### BossEncounterLink (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/BossEncounterComponents.cs (continued)
namespace Hollowcore.Boss
{
    /// <summary>
    /// Links the boss NPC entity to the encounter management entity.
    /// Placed on the boss NPC prefab.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct BossEncounterLink : IComponentData
    {
        [GhostField] public Entity EncounterEntity;
    }

    /// <summary>
    /// Marks a boss NPC entity for query filtering.
    /// </summary>
    public struct BossTag : IComponentData { }

    /// <summary>
    /// Enableable component toggled during phase transition invulnerability.
    /// Baked disabled, enabled during PhaseTransition state.
    /// </summary>
    public struct BossInvulnerable : IComponentData, IEnableableComponent { }
}
```

---

## Systems

### BossEncounterSystem

```csharp
// File: Assets/Scripts/Boss/Systems/BossEncounterSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BossClauseEvaluationSystem
//
// Master state machine for boss encounters. Drives BossPhaseState transitions.
//
// State flow:
//   Inactive → PreFight:
//     Triggered when player enters boss arena trigger volume.
//     1. Spawn boss encounter entity with BossPhaseState, BossVariantState, ActiveBossClauseBuffer
//     2. Set EncounterPhase = PreFight
//     3. BossClauseEvaluationSystem evaluates variant clauses (EPIC 14.1)
//     4. Fire BossPreFightUIEvent for managed bridge to display modifiers
//     5. After UI dismiss / timer: transition to Combat
//
//   Combat:
//     1. Boss AI active (framework AI/ behavior tree)
//     2. Each tick: check boss Health / MaxHealth against NextPhaseThreshold
//     3. If health <= threshold: transition to PhaseTransition
//     4. Check for mid-fight events (Strife interruptions, reinforcement triggers)
//     5. If boss Health <= 0: transition to Victory
//     6. If player dead: transition to Failure
//
//   PhaseTransition:
//     1. Enable BossInvulnerable on boss entity
//     2. Fire phase transition cinematic event
//     3. Increment CurrentPhaseIndex, update NextPhaseThreshold from definition
//     4. Activate phase-specific attack patterns and arena events
//     5. After EventTimer expires: disable BossInvulnerable, transition to Combat
//
//   MidFightEvent:
//     1. Pause boss AI briefly
//     2. Execute event (Strife interruption, reinforcement spawn)
//     3. After event resolves: transition back to Combat
//
//   Victory:
//     1. Disable boss AI
//     2. Play boss death animation/VFX
//     3. Fire BossRewardEvent (EPIC 14.5 handles reward dump)
//     4. Fire ExtractionOpenEvent
//
//   Failure:
//     1. Store PersistentBossHealthPercent (boss health does NOT reset)
//     2. Increment PlayerDeathCount
//     3. Hand off to standard death system (EPIC 2)
//     4. On player respawn near arena: resume from Inactive with persistent health
```

### BossPhaseTransitionSystem

```csharp
// File: Assets/Scripts/Boss/Systems/BossPhaseTransitionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BossEncounterSystem
//
// Handles the details of transitioning between boss combat phases.
//
// When BossPhaseState.EncounterPhase == PhaseTransition:
//   1. Read BossDefinitionSO phase list for CurrentPhaseIndex + 1
//   2. Queue ArenaEvents from the new phase definition (EPIC 14.3)
//   3. Enable new attack patterns in boss AI state
//   4. If new phase has dialogue: fire BossDialogueEvent
//   5. Decrement EventTimer by deltaTime
//   6. When EventTimer <= 0: set EncounterPhase = Combat
```

### BossMidFightEventSystem

```csharp
// File: Assets/Scripts/Boss/Systems/BossMidFightEventSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BossEncounterSystem
//
// Handles Strife interruptions and reinforcement waves during combat.
//
// When BossPhaseState.EncounterPhase == MidFightEvent:
//   1. If triggered by Strife: execute Strife card's boss-specific effect
//   2. If triggered by TraceLevel reinforcement:
//      a. Spawn reinforcement wave from BossVariantClauseSO.ReinforcementWave
//      b. Reinforcements use framework AI/ standard behavior
//   3. Decrement EventTimer
//   4. When event resolves (enemies dead or timer expired): set EncounterPhase = Combat
//
// Trigger detection (runs during Combat phase):
//   - Check variant clauses with BossClauseEffectType.SpawnReinforcements
//   - Trigger at defined boss health thresholds (e.g., 75%, 50%, 25%)
//   - Each reinforcement clause triggers at most once per encounter
```

### BossPreFightUIBridge

```csharp
// File: Assets/Scripts/Boss/Bridges/BossPreFightUIBridge.cs
// Managed MonoBehaviour — bridges ECS encounter state to UI.
//
// On BossPreFightUIEvent:
//   1. Display boss name, portrait, description
//   2. List active variant clauses with icons and descriptions
//   3. Highlight "insurance" clauses that COULD have been disabled
//   4. Show boss health bar (persistent health if re-attempt)
//   5. Player dismisses UI or auto-timeout → signal ECS to transition to Combat
```

---

## Setup Guide

1. Create BossEncounterComponents.cs in `Assets/Scripts/Boss/Components/`
2. Create boss encounter trigger volumes in each boss arena subscene
   - Use framework EncounterTriggerDefinition with custom BossEncounterTag
3. Wire BossEncounterSystem to read EncounterState transitions from framework
4. Create BossPreFightUI prefab in `Assets/Prefabs/UI/Boss/`
   - Modifier list panel, boss portrait, health bar, dismiss button
5. Add BossInvulnerable as enableable component to boss prefab (baked disabled)
6. For vertical slice: implement state machine for Grandmother Null, The Foreman, King of Heights

---

## Verification

- [ ] Boss encounter activates when player enters arena trigger volume
- [ ] PreFight phase displays active variant clauses in UI
- [ ] Combat phase: boss AI is active, health monitored for phase transitions
- [ ] Phase transition: boss becomes invulnerable, cinematic plays, new attacks enabled
- [ ] PhaseTransition timer expires and returns to Combat correctly
- [ ] Boss health <= 0 triggers Victory state
- [ ] Player death triggers Failure state without resetting boss health
- [ ] PersistentBossHealthPercent carries across death/respawn
- [ ] PlayerDeathCount increments on each failure
- [ ] Mid-fight reinforcement waves spawn at correct health thresholds
- [ ] Strife interruptions fire during combat when matching Strife card is active
- [ ] Victory fires reward and extraction events
- [ ] BossInvulnerable prevents damage during phase transitions only

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Boss/Authoring/BossEncounterBlobBaker.cs
namespace Hollowcore.Boss.Authoring
{
    // BossEncounterFlow bakes phase transition data into the BossBlob (shared with 14.1).
    // Additional encounter-specific blob data:
    //
    //   BlobArray<BossPhaseBlob>.HealthThreshold — used by BossEncounterSystem
    //     to detect phase transitions without loading the SO at runtime.
    //
    //   BlobArray<MidFightEventBlob> MidFightEvents
    //     - float HealthTrigger (0-1 threshold)
    //     - BossClauseEffectType SourceType (SpawnReinforcements, EnvironmentHazard)
    //     - int WaveDefinitionId / ArenaEventId
    //     - bool TriggeredOnce (set at runtime, prevents re-trigger)
    //
    //   float PhaseTransitionDuration — baked from SO, drives EventTimer
    //   float EnrageTimerSeconds — 0 = no enrage
}
```

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/BossEncounterValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Validation rules for boss encounter flow:
    //
    // 1. Phase thresholds monotonic and within (0, 1):
    //    Phases must be strictly decreasing. Phase 0 implicit at 1.0.
    //    Error if Phases[i].HealthThreshold >= Phases[i-1].HealthThreshold.
    //
    // 2. At least 2 phases per boss (minimum: start + one transition).
    //    Warning if only 1 phase (trivial fight).
    //
    // 3. Mid-fight event health triggers must not overlap with phase thresholds
    //    (within tolerance 0.02). Warning if reinforcement wave fires at same HP as phase transition.
    //
    // 4. PhaseTransitionDuration > 0 and < 10s. Error if zero (instant, breaks cinematic).
    //    Warning if > 5s (long invulnerability window frustrates players).
    //
    // 5. Enrage timer validation:
    //    If EnrageTimerSeconds > 0: warn if < 60s (too short for most players)
    //    or > 600s (effectively no enrage). Cross-check with DPS calculator from 14.1 tooling.
    //
    // 6. BossEncounterLink: every boss prefab with BossTag must also have BossEncounterLink.
    //    Error if BossTag present without BossEncounterLink.
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Boss/Debug/BossEncounterLiveTuning.cs
namespace Hollowcore.Boss.Debug
{
    // Encounter flow live tuning (extends BossLiveTuning singleton from 14.1):
    //
    //   float PhaseTransitionDurationOverride  // -1 = use baked, >0 = override seconds
    //   bool SkipPreFightUI                    // jump straight to Combat phase
    //   bool SkipPhaseTransitionCinematic      // instant phase transitions
    //   bool InfiniteBossHealth                // boss takes damage but HP clamps at 1
    //   bool InstantKillBoss                   // next hit kills boss (for testing Victory flow)
    //   bool DisableReinforcementWaves         // suppress all mid-fight reinforcement spawns
    //   bool DisablePersistentHealth           // boss HP resets on player death (easier testing)
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/BossEncounterDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // Encounter flow debug overlay (extends BossDebugOverlay from 14.1):
    //
    // [1] Encounter State Display
    //     - Current BossEncounterPhase as text badge (PreFight / Combat / PhaseTransition / etc.)
    //     - Phase index: "Phase 2/3" with progress bar to next threshold
    //     - EventTimer countdown during PhaseTransition and MidFightEvent
    //
    // [2] Persistent Health Indicator
    //     - Ghost bar behind main HP showing PersistentBossHealthPercent
    //     - PlayerDeathCount badge ("Deaths: 2")
    //     - "HP Persistent" or "HP Reset" status label
    //
    // [3] Mid-Fight Event Queue
    //     - List of pending mid-fight events with HP trigger thresholds
    //     - Triggered events shown as struck-through
    //     - "Next event at: 50% HP" indicator on the HP bar
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/BossWorkstation/BossEncounterSimulator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Extends BossFightSimulator (14.1) with encounter flow analysis.
    //
    // Additional simulation outputs:
    //   float[] phaseTransitionTimings  // mean/median/stddev time to reach each phase
    //   float[] phaseTimeFractions      // % of fight time spent in each phase
    //   float meanReinforcementWavesSurvived
    //   float wipeRateFromEnrage        // fraction of wipes caused by enrage timer
    //   float wipeRateFromMechanics     // wipes from phase-specific mechanics
    //   float meanDeathsBeforeFirstKill // learning curve indicator
    //
    // Phase balance analysis:
    //   Flags if any phase accounts for >60% of fight time (pacing issue).
    //   Flags if any phase accounts for <10% (too short, feels skipped).
    //   Flags if reinforcement waves consistently cause wipes (tune wave difficulty).
}
