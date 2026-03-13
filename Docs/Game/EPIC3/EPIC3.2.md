# EPIC 3.2: Front Advance & Off-Screen Simulation

**Status**: Planning
**Epic**: EPIC 3 — The Front (District Pressure System)
**Priority**: Critical — The Front must always be ticking
**Dependencies**: EPIC 3.1 (FrontState, FrontPhase, FrontZoneData, FrontDefinitionSO); EPIC 4.1 (ExpeditionGraphState, DistrictNode — for off-screen district enumeration); Framework: Roguelite/ (RunModifierStack)

---

## Overview

The Front never pauses. FrontAdvanceSystem ticks FrontState every frame for the player's current district, applying the advance curve and rate modifiers. FrontOffScreenSimulationSystem handles all other districts: on each gate transition (district change), it calculates elapsed real time since the player last visited and fast-forwards each off-screen district's Front at a reduced rate (0.3x-0.5x). Advance acceleration comes from alarms, failed objectives, and time. Deceleration comes from containment objectives. Each district has a unique spread pattern defined in FrontDefinitionSO that determines how the Front converts zones.

---

## Component Definitions

### FrontAdvanceModifier (IBufferElementData)

```csharp
// File: Assets/Scripts/Front/Components/FrontAdvanceComponents.cs
using Unity.Entities;

namespace Hollowcore.Front
{
    public enum AdvanceModifierSource : byte
    {
        Alarm = 0,           // Triggered alarm in district
        FailedObjective = 1, // Failed containment/side objective
        TimeDecay = 2,       // Natural acceleration over expedition time
        Containment = 3,     // Completed containment objective (negative modifier)
        Counterplay = 4,     // Active counterplay action (negative modifier)
        RunModifier = 5,     // From RunModifierStack (roguelite meta)
        Pulse = 6            // Temporary pulse effect (EPIC 3.5)
    }

    /// <summary>
    /// Stacking rate modifiers on the district entity. Sum of all modifiers
    /// is applied multiplicatively to FrontState.AdvanceRate each frame.
    /// Negative values slow the Front; positive values accelerate it.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct FrontAdvanceModifier : IBufferElementData
    {
        /// <summary>Additive rate modifier (e.g., +0.3 = 30% faster, -0.5 = 50% slower).</summary>
        public float RateModifier;

        /// <summary>Source for debugging and UI display.</summary>
        public AdvanceModifierSource Source;

        /// <summary>
        /// Remaining duration in seconds. 0 = permanent (removed explicitly).
        /// Decremented each frame by FrontAdvanceModifierDecaySystem.
        /// </summary>
        public float RemainingDuration;
    }
}
```

### FrontLastVisitTime (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/FrontAdvanceComponents.cs (continued)
using Unity.NetCode;

namespace Hollowcore.Front
{
    /// <summary>
    /// Tracks when the player last visited this district.
    /// Used by off-screen simulation to calculate elapsed time.
    /// Stored as network tick for deterministic replay.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct FrontLastVisitTime : IComponentData
    {
        [GhostField] public uint LastVisitTick;

        /// <summary>True if the player is currently in this district.</summary>
        [GhostField] public bool IsPlayerPresent;
    }
}
```

### FrontAdvanceConfig (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/FrontAdvanceComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Singleton config for global Front advance tuning.
    /// Created by FrontConfigAuthoring in the persistent scene.
    /// </summary>
    public struct FrontAdvanceConfig : IComponentData
    {
        /// <summary>Off-screen advance rate multiplier (0.3-0.5 of normal).</summary>
        public float OffScreenRateMultiplier;

        /// <summary>Natural time-based acceleration: rate increase per minute of expedition time.</summary>
        public float TimeDecayRatePerMinute;

        /// <summary>Rate increase per triggered alarm in a district.</summary>
        public float AlarmRateBonus;

        /// <summary>Rate increase per failed objective in a district.</summary>
        public float FailedObjectiveRateBonus;

        /// <summary>Maximum total AdvanceRate (prevents runaway acceleration).</summary>
        public float MaxAdvanceRate;

        /// <summary>Minimum total AdvanceRate (ensures Front always moves, even with heavy counterplay).</summary>
        public float MinAdvanceRate;
    }
}
```

---

## Systems

### FrontAdvanceSystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontAdvanceSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: FrontPhaseEvaluationSystem (EPIC 3.1)
//
// Ticks FrontState.SpreadProgress each frame for the player's current district.
//
// For each district entity with FrontState + FrontLastVisitTime where IsPlayerPresent == true:
//   1. If FrontState.IsContained: early out (no advance)
//   2. If current tick < FrontState.PausedUntilTick: early out (paused)
//   3. Look up FrontDefinitionSO via FrontState.FrontDefinitionId
//   4. Compute effective rate:
//      a. Start with FrontDefinitionSO.BaseAdvanceSpeed
//      b. Multiply by AdvanceCurve.Evaluate(SpreadProgress) for non-linear acceleration
//      c. Multiply by FrontState.AdvanceRate (sum of all modifiers)
//      d. Clamp to [MinAdvanceRate, MaxAdvanceRate] from FrontAdvanceConfig
//   5. Advance: SpreadProgress += effectiveRate * deltaTime
//   6. Clamp SpreadProgress to [0, 1]
//   7. If SpreadProgress >= 1.0 and Phase < Phase4_Overrun:
//      a. SpreadProgress = 0
//      b. Phase transition handled by FrontPhaseEvaluationSystem
```

### FrontAdvanceRateSystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontAdvanceRateSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: FrontAdvanceSystem
//
// Recalculates FrontState.AdvanceRate from all active FrontAdvanceModifier entries.
//
// For each district entity with FrontState + FrontAdvanceModifier buffer:
//   1. Base rate = 1.0
//   2. Sum all FrontAdvanceModifier.RateModifier values
//   3. AdvanceRate = max(MinAdvanceRate, min(MaxAdvanceRate, 1.0 + sum))
//   4. Write back to FrontState.AdvanceRate
```

### FrontAdvanceModifierDecaySystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontAdvanceModifierDecaySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: FrontAdvanceRateSystem
//
// Decrements duration on temporary modifiers and removes expired ones.
//
// For each district entity with FrontAdvanceModifier buffer:
//   1. Iterate buffer backward (for safe removal):
//      a. If RemainingDuration > 0: decrement by deltaTime
//      b. If RemainingDuration <= 0 and was > 0 before: remove entry (RemoveAtSwapBack)
//      c. If RemainingDuration == 0 (permanent): skip — removed explicitly by counterplay systems
```

### FrontOffScreenSimulationSystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontOffScreenSimulationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: FrontAdvanceSystem
//
// On gate transition: fast-forwards off-screen districts.
// Does NOT run every frame — triggered by district change event.
//
// When player enters a new district (detected via FrontLastVisitTime.IsPlayerPresent change):
//   1. For each district entity where IsPlayerPresent == false:
//      a. Calculate elapsed ticks since LastVisitTick
//      b. Convert to seconds via NetworkTime.ServerTick
//      c. Compute off-screen advance:
//         - elapsedSeconds * FrontDefinitionSO.BaseAdvanceSpeed * OffScreenRateMultiplier * AdvanceRate
//      d. Add to SpreadProgress
//      e. Evaluate phase thresholds — may jump multiple phases if enough time elapsed
//      f. Run zone conversion logic (batch convert zones that should have converted)
//      g. Update LastVisitTick to current tick
//   2. Mark current district: IsPlayerPresent = true, LastVisitTick = current tick
//   3. Mark previous district: IsPlayerPresent = false, LastVisitTick = current tick
//
// NOTE: Off-screen simulation is deliberately lightweight.
// No pulse triggers, no individual zone conversion animations.
// Just phase + zone state snapshots at the new time point.
```

---

## Per-District Spread Patterns

Each FrontDefinitionSO encodes a unique spread behavior. The FrontZoneConversionSystem (EPIC 3.1) reads the SpreadPattern enum and applies the appropriate logic:

| District | Front Name | Pattern | Behavior |
|---|---|---|---|
| Necrospire | Corruption Bloom | Radial | Outward from core along data conduits |
| Wetmarket | Waterline Rise | Linear | Flood rises by vertical region |
| Glitch Quarter | Reality Desync | Network | Projector network, alters physics rules |
| Chrome Cathedral | Choir Crescendo | Network | Comm relays, spreads via sound graph |
| The Shoals | Tide Swell | Linear | Water level rises, currents intensify |
| The Burn | Overheat Cascade | Radial | Heat zones expand from furnaces |
| Mirrortown | Identity Drift | Network | Identity outbreaks, NPC unreliability |
| The Lattice | Structural Failure | Linear | Collapses cascade vertically downward |
| Synapse Row | Cognitive Overload | Radial | Memetic pressure from central broadcast |
| The Quarantine | Outbreak Surge | Radial | Infection zones expand, containment walls fail |
| Old Growth | Bloom Propagation | Network | Tendrils expand, routes regrow behind you |
| The Auction | Market Volatility | Network | Territory flips, ceasefire windows close |
| Deadwave | Silence Expansion | Radial | Dead zone intensifies, tech fails outward |
| The Nursery | Consciousness Cascade | Network | AI awakens along system connections |
| Skyfall Ruins | Systems Reboot | Linear | Security regains control zone by zone |

---

## Authoring

### FrontConfigAuthoring

```csharp
// File: Assets/Scripts/Front/Authoring/FrontConfigAuthoring.cs
// Added to persistent scene. Creates singleton FrontAdvanceConfig.
//
// Inspector fields:
//   - OffScreenRateMultiplier (default 0.4)
//   - TimeDecayRatePerMinute (default 0.01)
//   - AlarmRateBonus (default 0.15)
//   - FailedObjectiveRateBonus (default 0.2)
//   - MaxAdvanceRate (default 3.0)
//   - MinAdvanceRate (default 0.1)
//
// Baker:
//   1. AddComponent<FrontAdvanceConfig> with inspector values
```

---

## Setup Guide

1. **Add FrontAdvanceComponents.cs** to `Assets/Scripts/Front/Components/`
2. **Create FrontAdvanceSystem, FrontAdvanceRateSystem, FrontAdvanceModifierDecaySystem, FrontOffScreenSimulationSystem** in `Assets/Scripts/Front/Systems/`
3. **Add FrontAdvanceModifier buffer** to FrontAuthoring baker (EPIC 3.1) — add `AddBuffer<FrontAdvanceModifier>` call
4. **Add FrontLastVisitTime** to district entity in FrontAuthoring baker
5. **Create FrontConfigAuthoring** in persistent scene with default tuning values
6. **For each FrontDefinitionSO**, configure:
   - BaseAdvanceSpeed (start with 0.02 for ~50s per full phase at 1x rate)
   - AdvanceCurve (start with linear 1.0 → 1.5 for gentle late-phase acceleration)
   - OffScreenRateMultiplier (0.4 default — 40% of normal speed)
   - SpreadPattern matching the district's GDD description
7. **Test with time scale**: use `Time.timeScale` override or debug FrontAdvanceConfig.TimeDecayRatePerMinute to verify multi-phase advance
8. **Test off-screen**: enter district A, switch to district B, wait 60s, return to A — verify A advanced by ~24s worth (60 * 0.4)

---

## Verification

- [ ] FrontState.SpreadProgress increases each frame when player is present
- [ ] SpreadProgress does not advance when IsContained == true
- [ ] SpreadProgress does not advance when current tick < PausedUntilTick
- [ ] AdvanceRate correctly sums all FrontAdvanceModifier entries
- [ ] Temporary modifiers decay and are removed when duration expires
- [ ] Permanent modifiers persist until explicitly removed
- [ ] AdvanceRate clamps to [MinAdvanceRate, MaxAdvanceRate]
- [ ] Alarm modifier increases advance rate by configured bonus
- [ ] Off-screen districts advance at OffScreenRateMultiplier rate on gate transition
- [ ] Off-screen simulation correctly jumps multiple phases if enough time elapsed
- [ ] Off-screen simulation batch-converts zones without individual animations
- [ ] FrontLastVisitTime.IsPlayerPresent toggles correctly on district entry/exit
- [ ] AdvanceCurve from FrontDefinitionSO applies non-linear acceleration
- [ ] Each spread pattern (Radial, Linear, Network) produces distinct zone conversion sequences

---

## Validation

Validation rules for `FrontAdvanceConfig` singleton:
- `OffScreenRateMultiplier` must be in [0.1, 1.0]
- `TimeDecayRatePerMinute` must be >= 0 and <= 0.1 (prevents runaway acceleration)
- `AlarmRateBonus` must be > 0 and <= 1.0
- `FailedObjectiveRateBonus` must be > 0 and <= 1.0
- `MaxAdvanceRate` must be > `MinAdvanceRate`
- `MinAdvanceRate` must be > 0 (Front must always move)

Validation rules for `FrontAdvanceModifier` buffer entries:
- `RateModifier` must be in [-1.0, 5.0] (prevent nonsensical values)
- `RemainingDuration` must be >= 0
- `Source` must be a valid `AdvanceModifierSource` enum value

---

## Live Tuning

```csharp
// File: Assets/Scripts/Front/Components/FrontAdvanceConfig.cs
// FrontAdvanceConfig is already a singleton IComponentData — serves as the RuntimeConfig.
//
// Live-tunable parameters:
//   - OffScreenRateMultiplier: change to speed up/slow down off-screen Front without restart
//   - TimeDecayRatePerMinute: adjust natural acceleration
//   - AlarmRateBonus / FailedObjectiveRateBonus: balance alarm/failure pressure
//   - MaxAdvanceRate / MinAdvanceRate: set guardrails
//
// Change propagation: FrontAdvanceRateSystem reads FrontAdvanceConfig every frame.
// No dirty flag needed — singleton query is cheap.
//
// Editor integration: FrontConfigAuthoring inspector writes to singleton at runtime
// via a managed "FrontTuningBridge" system in PresentationSystemGroup that syncs
// inspector changes → ECS singleton.
```

Tuning workflow: designer modifies `FrontConfigAuthoring` inspector values during play mode -> `FrontTuningBridge` detects changes via `OnValidate` callback -> writes updated values to `FrontAdvanceConfig` singleton -> `FrontAdvanceRateSystem` picks up new values next frame.

---

## Debug Visualization

**Front Advance Debug Panel** (toggle via debug menu):
- Real-time graph: X-axis = time (last 60s), Y-axis = SpreadProgress for each district
- Advance rate breakdown: stacked bar showing base rate + each modifier source contribution
- Off-screen simulation log: timestamped entries showing elapsed time, advance amount, and resulting phase for each off-screen district on gate transition
- Modifier list: active `FrontAdvanceModifier` entries with source, rate, remaining duration

**Activation**: Debug menu toggle `Front/Advance/ShowGraph`

---

## Simulation & Testing

**Off-Screen Accuracy Test**:
- Automated test: enter district A at Phase 1, switch to B, wait exactly 120s (real time), return to A
- Expected: A's SpreadProgress advanced by `120 * BaseAdvanceSpeed * OffScreenRateMultiplier * AdvanceRate`
- Tolerance: within 0.01 of expected value (accounts for tick quantization)

**Phase Timing Distribution**:
- Monte Carlo (N=1000): for each of 15 district definitions with random modifier histories, simulate full Phase 1 -> Phase 4 progression
- Record: time-to-Phase2, time-to-Phase3, time-to-Phase4
- Balance target: Phase 2 should be reached in 2-4 minutes (player-present time), Phase 4 in 10-15 minutes
