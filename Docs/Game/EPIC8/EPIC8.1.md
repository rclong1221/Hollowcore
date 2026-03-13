# EPIC 8.1: Trace State & Threshold Model

**Status**: Planning
**Epic**: EPIC 8 — Trace (Global Pressure Meter)
**Priority**: Critical — Foundation for all Trace mechanics
**Dependencies**: Framework: Roguelite/RunModifierStack

---

## Overview

Core expedition-level pressure meter. Trace is a single integer that climbs throughout a run, representing how much attention the player has attracted. At defined thresholds the system applies escalating consequences: hunter spawns, reduced gate options, boss upgrades, and price inflation. Threshold effects are implemented as RunModifierStack entries so they compose naturally with other run modifiers.

---

## Component Definitions

### TraceState (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Trace
{
    /// <summary>
    /// Expedition-level singleton tracking global Trace pressure.
    /// Created once per run by TraceBootstrapSystem.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct TraceState : IComponentData
    {
        /// <summary>Current Trace level (0+). Primary pressure value.</summary>
        [GhostField] public int CurrentTrace;

        /// <summary>Soft cap for UI display scaling. Does not clamp CurrentTrace.</summary>
        [GhostField] public int MaxTrace;

        /// <summary>
        /// Accumulated seconds toward the next time-based Trace increment.
        /// Resets to 0 each time a time-based +1 is applied.
        /// </summary>
        [GhostField(Quantization = 100)] public float AccumulatedTime;

        /// <summary>
        /// Bitmask of which thresholds have been activated.
        /// Prevents re-triggering one-shot effects on the same threshold.
        /// Bit 0 = threshold 0, bit 1 = threshold 1, etc.
        /// </summary>
        [GhostField] public byte ActivatedThresholdsMask;

        /// <summary>Highest Trace level ever reached this run (for analytics/UI).</summary>
        [GhostField] public int PeakTrace;
    }
}
```

### TraceThreshold Enum

```csharp
// File: Assets/Scripts/Trace/Components/TraceComponents.cs (continued)
namespace Hollowcore.Trace
{
    /// <summary>
    /// Named Trace breakpoints from GDD §9.3.
    /// Integer values correspond to the CurrentTrace level at which the effect activates.
    /// </summary>
    public enum TraceThreshold : byte
    {
        /// <summary>0-1: Baseline. No effects.</summary>
        Baseline = 0,

        /// <summary>2: Hunters common. Hunter enemies begin spawning in districts.</summary>
        HuntersCommon = 2,

        /// <summary>3: Forward gate options drop from 3 to 2.</summary>
        GatesReduced = 3,

        /// <summary>4+: Boss upgrades active, services pricier, all Fronts become Volatile.</summary>
        Escalation = 4
    }
}
```

### TraceThresholdEffect (IBufferElementData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceComponents.cs (continued)
using Unity.Collections;

namespace Hollowcore.Trace
{
    /// <summary>
    /// Buffer on the TraceState singleton entity listing active threshold effects.
    /// Each entry corresponds to a RunModifierStack entry that was applied.
    /// Used for cleanup when Trace drops below a threshold (via sinks).
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TraceThresholdEffect : IBufferElementData
    {
        /// <summary>The threshold level that activated this effect.</summary>
        public byte ThresholdLevel;

        /// <summary>RunModifierStack entry ID for removal when threshold deactivates.</summary>
        public int ModifierEntryId;

        /// <summary>Human-readable tag for debugging.</summary>
        public FixedString32Bytes EffectTag;
    }
}
```

### TraceConfig (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceComponents.cs (continued)
namespace Hollowcore.Trace
{
    /// <summary>
    /// Tuning singleton for Trace system. Created from TraceConfigSO at run start.
    /// </summary>
    public struct TraceConfig : IComponentData
    {
        /// <summary>Seconds between time-based Trace increments (default 300 = 5 minutes).</summary>
        public float TimePerTracePoint;

        /// <summary>Soft cap displayed on UI meter.</summary>
        public int DefaultMaxTrace;

        /// <summary>Vendor price multiplier at Trace 4+ (e.g., 1.5 = 50% more expensive).</summary>
        public float EscalationPriceMultiplier;

        /// <summary>Forward gate count at Trace 3+ (default 2, normally 3).</summary>
        public int ReducedGateCount;

        /// <summary>Hunter spawn rate multiplier per Trace point above 2 (base = 1.0).</summary>
        public float HunterSpawnRatePerTrace;
    }
}
```

---

## ScriptableObject Definitions

### TraceConfigSO

```csharp
// File: Assets/Scripts/Trace/Definitions/TraceConfigSO.cs
using UnityEngine;

namespace Hollowcore.Trace.Definitions
{
    [CreateAssetMenu(fileName = "TraceConfig", menuName = "Hollowcore/Trace/Trace Config")]
    public class TraceConfigSO : ScriptableObject
    {
        [Header("Time")]
        [Tooltip("Seconds between time-based +1 Trace")]
        public float TimePerTracePoint = 300f;

        [Header("Thresholds")]
        public int DefaultMaxTrace = 6;
        public int ReducedGateCount = 2;

        [Header("Escalation (Trace 4+)")]
        public float EscalationPriceMultiplier = 1.5f;
        public float HunterSpawnRatePerTrace = 1.0f;
    }
}
```

---

## Systems

### TraceBootstrapSystem

```csharp
// File: Assets/Scripts/Trace/Systems/TraceBootstrapSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// On run start (when no TraceState singleton exists):
//   1. Create singleton entity with TraceState (CurrentTrace=0, MaxTrace from config)
//   2. Add TraceThresholdEffect buffer (empty)
//   3. Load TraceConfigSO from Resources → create TraceConfig singleton
```

### TraceThresholdSystem

```csharp
// File: Assets/Scripts/Trace/Systems/TraceThresholdSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: TraceSourceSystem, TraceSinkSystem
//
// Reads: TraceState, TraceConfig
// Writes: TraceThresholdEffect buffer, RunModifierStack (via framework API)
//
// Each frame:
//   1. Read TraceState.CurrentTrace
//   2. Update PeakTrace if CurrentTrace > PeakTrace
//   3. For each threshold (HuntersCommon=2, GatesReduced=3, Escalation=4):
//      a. If CurrentTrace >= threshold AND threshold not in ActivatedThresholdsMask:
//         - Set bit in ActivatedThresholdsMask
//         - Create RunModifierStack entry for threshold effect:
//           * Trace 2: HunterSpawnRate modifier (HunterSpawnRatePerTrace * (CurrentTrace - 1))
//           * Trace 3: GateCount modifier (set forward gates to ReducedGateCount)
//           * Trace 4: VendorPriceMultiplier (EscalationPriceMultiplier),
//                      BossDifficultyModifier (+1 tier),
//                      FrontVolatilityModifier (all Fronts → Volatile)
//         - Append TraceThresholdEffect with ModifierEntryId
//      b. If CurrentTrace < threshold AND threshold IS in ActivatedThresholdsMask:
//         - Clear bit in ActivatedThresholdsMask
//         - Remove RunModifierStack entry by stored ModifierEntryId
//         - Remove corresponding TraceThresholdEffect buffer entry
//   4. Update hunter spawn rate modifier if Trace > 2 (scales with exact level)
```

---

## Setup Guide

1. **Create `Assets/Scripts/Trace/` folder** with subfolders: Components/, Definitions/, Systems/, Authoring/
2. **Create assembly definition** `Hollowcore.Trace.asmdef` referencing `DIG.Shared`, `Hollowcore.Roguelite`, `Unity.Entities`, `Unity.NetCode`, `Unity.Collections`, `Unity.Burst`
3. Create `TraceConfigSO` asset at `Assets/Data/Trace/TraceConfig.asset` with default values
4. Verify TraceBootstrapSystem creates the singleton on run start
5. Test threshold activation: manually set CurrentTrace via Entity Inspector, confirm RunModifierStack entries appear/disappear
6. Confirm TraceThresholdEffect buffer tracks active effects and cleans up on Trace decrease

---

## Verification

- [ ] TraceState singleton created on run start with CurrentTrace=0
- [ ] TraceConfig singleton populated from TraceConfigSO
- [ ] TraceThresholdEffect buffer is empty at Trace 0-1
- [ ] Crossing Trace 2 sets ActivatedThresholdsMask bit and creates hunter spawn rate modifier
- [ ] Crossing Trace 3 creates gate count modifier (forward gates = 2)
- [ ] Crossing Trace 4 creates price multiplier, boss difficulty, and Front volatility modifiers
- [ ] Decreasing Trace below a threshold removes the corresponding RunModifierStack entries
- [ ] TraceThresholdEffect buffer correctly tracks and removes entries
- [ ] PeakTrace updates on new highs and never decreases
- [ ] Multiple threshold crossings in a single frame handled correctly (e.g., Trace jumps 0 to 3)

---

## Validation

```csharp
// File: Assets/Editor/TraceWorkstation/TraceValidation.cs
namespace Hollowcore.Trace.Editor
{
    /// <summary>
    /// Validates TraceConfigSO and threshold definitions at import time and on demand.
    /// </summary>
    // Rules:
    //   1. Threshold values must be monotonically increasing (Baseline < HuntersCommon < GatesReduced < Escalation)
    //   2. TimePerTracePoint must be > 0
    //   3. DefaultMaxTrace must be >= highest threshold (currently 4)
    //   4. EscalationPriceMultiplier must be >= 1.0
    //   5. ReducedGateCount must be < default gate count (3) and >= 1
    //   6. HunterSpawnRatePerTrace must be > 0
    //   7. ActivatedThresholdsMask must have enough bits for all defined thresholds (byte = 8 bits, sufficient)
    //
    // Invocation: OnPostprocessAllAssets for TraceConfigSO, or manual via TraceWorkstation validation tab
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Trace/Debug/TraceLiveTuning.cs
namespace Hollowcore.Trace.Debug
{
    /// <summary>
    /// Runtime-editable Trace config exposed to the Run Workstation's Live Tuning module.
    /// Writes directly to TraceConfig singleton each frame when overrides are active.
    /// </summary>
    // Tunable fields:
    //   - TimePerTracePoint (float, min 10, max 600)
    //   - DefaultMaxTrace (int, min 4, max 20)
    //   - EscalationPriceMultiplier (float, min 1.0, max 5.0)
    //   - ReducedGateCount (int, min 1, max 3)
    //   - HunterSpawnRatePerTrace (float, min 0.1, max 5.0)
    //
    // Pattern: static TraceLiveTuningOverrides struct with bool Enabled + override values.
    // TraceBootstrapSystem checks TraceLiveTuningOverrides.Enabled each frame and patches TraceConfig.
    // Editor UI: slider per field in TraceWorkstation Live Tuning tab.
    // Reset button restores TraceConfigSO defaults.
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Trace/Debug/TraceDebugOverlay.cs
namespace Hollowcore.Trace.Debug
{
    /// <summary>
    /// In-game debug overlay for Trace system. Toggled via TraceWorkstation or debug console.
    /// </summary>
    // Overlay elements:
    //   1. Trace meter: large, always-on overlay showing CurrentTrace / MaxTrace / PeakTrace
    //   2. Threshold number line: horizontal bar with threshold markers at 2, 3, 4
    //      - Active thresholds highlighted (filled), upcoming thresholds dimmed
    //      - Current position indicated with animated pip
    //   3. Source/sink event log: scrollable list of last 20 TraceSourceEvent / TraceSinkEvent
    //      - Columns: timestamp, category icon, amount (+1/-1), context label
    //      - Color-coded: red for sources, green for sinks
    //   4. Accumulator progress: bar showing AccumulatedTime / TimePerTracePoint (next time-based +1)
    //   5. Backtrack indicator: if IsBacktracking, show BacktrackAccumulatedTime / BacktrackTimePerTrace
    //
    // Implementation: MonoBehaviour on a debug canvas. Reads TraceState via a dedicated
    // TraceDebugBridgeSystem (PresentationSystemGroup, only active when overlay is enabled).
    // Event log populated by hooking into TraceUINotificationQueue (shared with production UI).
}
```
