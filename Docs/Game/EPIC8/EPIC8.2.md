# EPIC 8.2: Trace Sources

**Status**: Planning
**Epic**: EPIC 8 — Trace (Global Pressure Meter)
**Priority**: High — Without sources, Trace never rises
**Dependencies**: EPIC 8.1 (TraceState); Framework: Quest/, EPIC 5 (Echoes), EPIC 6 (Gate Selection)

---

## Overview

Defines everything that raises Trace. Six source categories feed into a single TraceSourceEvent pipeline: echo missions, alarm extraction, dirty contracts, late boss extraction, elapsed time, and backtracking. The backtracking tax is the primary design lever -- every minute spent in a previously completed district adds Trace, making retreat increasingly expensive. All source logic funnels through TraceSourceSystem which increments TraceState and fires UI notifications.

---

## Component Definitions

### TraceSourceEvent (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceSourceComponents.cs
using Unity.Collections;
using Unity.Entities;

namespace Hollowcore.Trace
{
    /// <summary>
    /// Source category for Trace gain. Used in events and UI notifications.
    /// </summary>
    public enum TraceSourceCategory : byte
    {
        EchoMission = 0,      // +1 per echo attempted
        AlarmExtraction = 1,   // +1 at extraction if alarm threshold exceeded
        DirtyContract = 2,     // +1 for choosing high-reward gate option
        LateBossExtraction = 3,// +1 if boss killed but player lingered
        TimePassed = 4,        // +1 per TimePerTracePoint seconds
        Backtracking = 5       // +1 per BacktrackTimePerTrace seconds in previous district
    }

    /// <summary>
    /// Transient entity requesting a Trace increment. Created by gameplay systems,
    /// consumed by TraceSourceSystem in the same frame.
    /// </summary>
    public struct TraceSourceEvent : IComponentData
    {
        /// <summary>Amount of Trace to add (usually 1).</summary>
        public int Amount;

        /// <summary>Why Trace is increasing (for UI popup and analytics).</summary>
        public TraceSourceCategory Category;

        /// <summary>Optional context string for UI (e.g., "Echo: Memory Fragment").</summary>
        public FixedString64Bytes ContextLabel;
    }

    /// <summary>
    /// Cleanup tag for TraceSourceEvent transient entities.
    /// </summary>
    public struct TraceSourceCleanup : ICleanupComponentData { }
}
```

### BacktrackTracker (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceSourceComponents.cs (continued)
namespace Hollowcore.Trace
{
    /// <summary>
    /// Singleton tracking backtrack state for the Trace time tax.
    /// </summary>
    public struct BacktrackTracker : IComponentData
    {
        /// <summary>District index the player is currently in.</summary>
        public int CurrentDistrictIndex;

        /// <summary>Highest district index reached this run (forward progress watermark).</summary>
        public int HighestDistrictReached;

        /// <summary>
        /// Accumulated seconds in a backtrack district toward next Trace point.
        /// Resets when a Trace point is awarded or player moves forward.
        /// </summary>
        public float BacktrackAccumulatedTime;

        /// <summary>True if CurrentDistrictIndex less than HighestDistrictReached.</summary>
        public bool IsBacktracking => CurrentDistrictIndex < HighestDistrictReached;
    }
}
```

### TraceSourceConfig (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceSourceComponents.cs (continued)
namespace Hollowcore.Trace
{
    /// <summary>
    /// Tuning values for Trace source rates. Loaded from TraceSourceConfigSO.
    /// </summary>
    public struct TraceSourceConfig : IComponentData
    {
        /// <summary>Alarm count in a district that triggers +1 Trace at extraction.</summary>
        public int AlarmThresholdForTrace;

        /// <summary>Seconds after boss kill before late extraction triggers +1.</summary>
        public float LateBossLingerThreshold;

        /// <summary>Seconds in a backtrack district per +1 Trace (default 60).</summary>
        public float BacktrackTimePerTrace;
    }
}
```

---

## Systems

### TraceSourceSystem

```csharp
// File: Assets/Scripts/Trace/Systems/TraceSourceSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: TraceThresholdSystem
//
// Reads: TraceSourceEvent (transient entities)
// Writes: TraceState (CurrentTrace, AccumulatedTime)
//
// Each frame:
//   1. Query all TraceSourceEvent entities
//   2. For each event:
//      a. Increment TraceState.CurrentTrace by event.Amount
//      b. Enqueue UI notification: (Category, Amount, ContextLabel) → TraceUINotificationQueue (static)
//   3. Destroy all TraceSourceEvent entities (add TraceSourceCleanup, then cleanup pass)
```

### TraceTimeAccumulatorSystem

```csharp
// File: Assets/Scripts/Trace/Systems/TraceTimeAccumulatorSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: TraceSourceSystem
//
// Reads: TraceState, TraceConfig
// Writes: TraceState.AccumulatedTime; creates TraceSourceEvent entities
//
// Each frame:
//   1. TraceState.AccumulatedTime += deltaTime
//   2. If AccumulatedTime >= TraceConfig.TimePerTracePoint:
//      a. AccumulatedTime -= TimePerTracePoint (carry remainder)
//      b. Create TraceSourceEvent entity: Amount=1, Category=TimePassed
```

### BacktrackTaxSystem

```csharp
// File: Assets/Scripts/Trace/Systems/BacktrackTaxSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: TraceSourceSystem
//
// Reads: BacktrackTracker, TraceSourceConfig
// Writes: BacktrackTracker.BacktrackAccumulatedTime; creates TraceSourceEvent entities
//
// Each frame:
//   1. If BacktrackTracker.IsBacktracking:
//      a. BacktrackAccumulatedTime += deltaTime
//      b. If BacktrackAccumulatedTime >= TraceSourceConfig.BacktrackTimePerTrace:
//         - BacktrackAccumulatedTime -= BacktrackTimePerTrace
//         - Create TraceSourceEvent: Amount=1, Category=Backtracking
//   2. On district change (CurrentDistrictIndex changed):
//      a. Update CurrentDistrictIndex
//      b. If new index > HighestDistrictReached: update HighestDistrictReached
//      c. Reset BacktrackAccumulatedTime to 0
```

### TraceEventListenerSystem

```csharp
// File: Assets/Scripts/Trace/Systems/TraceEventListenerSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: TraceSourceSystem
//
// Listens for gameplay events that generate Trace and creates TraceSourceEvent entities.
//
// Event sources:
//   1. Echo mission started (from Quest system callback):
//      → TraceSourceEvent: Amount=1, Category=EchoMission, ContextLabel="Echo: {questName}"
//
//   2. District extraction with alarm count > AlarmThresholdForTrace:
//      → TraceSourceEvent: Amount=1, Category=AlarmExtraction
//
//   3. Gate selection with dirty contract flag:
//      → TraceSourceEvent: Amount=1, Category=DirtyContract, ContextLabel="{contractName}"
//
//   4. Boss killed + player remained > LateBossLingerThreshold seconds:
//      → TraceSourceEvent: Amount=1, Category=LateBossExtraction
//
// Implementation: static NativeQueue<TraceSourceRequest> for cross-system event submission.
// Gameplay systems (quest completion, gate selection, extraction) enqueue requests.
// This system drains the queue and creates transient entities each frame.
```

---

## Static API

### TraceSourceAPI

```csharp
// File: Assets/Scripts/Trace/TraceSourceAPI.cs
using Unity.Collections;

namespace Hollowcore.Trace
{
    /// <summary>
    /// Static helper for cross-system Trace source submissions.
    /// Follows the XPGrantAPI / DamageVisualQueue pattern from the framework.
    /// </summary>
    public static class TraceSourceAPI
    {
        internal static NativeQueue<TraceSourceRequest> PendingRequests;

        public static void Initialize()
        {
            if (!PendingRequests.IsCreated)
                PendingRequests = new NativeQueue<TraceSourceRequest>(Allocator.Persistent);
        }

        public static void Dispose()
        {
            if (PendingRequests.IsCreated)
                PendingRequests.Dispose();
        }

        /// <summary>
        /// Queue a Trace increment from any system. Processed next frame by TraceEventListenerSystem.
        /// </summary>
        public static void AddTrace(int amount, TraceSourceCategory category,
            FixedString64Bytes contextLabel = default)
        {
            if (!PendingRequests.IsCreated) return;
            PendingRequests.Enqueue(new TraceSourceRequest
            {
                Amount = amount,
                Category = category,
                ContextLabel = contextLabel
            });
        }
    }

    public struct TraceSourceRequest
    {
        public int Amount;
        public TraceSourceCategory Category;
        public FixedString64Bytes ContextLabel;
    }
}
```

---

## Setup Guide

1. Add TraceSourceComponents to `Assets/Scripts/Trace/Components/`
2. Create `TraceSourceConfigSO` asset at `Assets/Data/Trace/TraceSourceConfig.asset`:
   - AlarmThresholdForTrace: 3
   - LateBossLingerThreshold: 60 (seconds)
   - BacktrackTimePerTrace: 60 (seconds)
3. Initialize `TraceSourceAPI.Initialize()` in TraceBootstrapSystem alongside TraceState creation
4. Hook quest completion callback to call `TraceSourceAPI.AddTrace(1, EchoMission, echoName)` for echo quests
5. Hook gate selection to call `TraceSourceAPI.AddTrace(1, DirtyContract, contractName)` for dirty contracts
6. Hook extraction system to check alarm count and call `TraceSourceAPI.AddTrace(1, AlarmExtraction)` if threshold exceeded
7. BacktrackTracker: set CurrentDistrictIndex on district transitions, HighestDistrictReached on forward progress

---

## Verification

- [ ] TraceSourceEvent entities are created and consumed in a single frame
- [ ] Echo mission start increments Trace by 1
- [ ] Extraction with alarms above threshold increments Trace by 1
- [ ] Dirty contract gate selection increments Trace by 1
- [ ] Lingering after boss kill increments Trace by 1
- [ ] Time accumulator fires +1 after TimePerTracePoint seconds
- [ ] Backtrack tax fires +1 per BacktrackTimePerTrace seconds when in a previous district
- [ ] Moving forward resets BacktrackAccumulatedTime
- [ ] HighestDistrictReached updates only on forward progress
- [ ] TraceSourceAPI.AddTrace works from arbitrary systems
- [ ] Multiple sources in the same frame all apply correctly
- [ ] UI notification queue populated with category and context for each source

---

## Validation

```csharp
// File: Assets/Editor/TraceWorkstation/TraceSourceValidation.cs
namespace Hollowcore.Trace.Editor
{
    /// <summary>
    /// Validates Trace source configuration for balance and correctness.
    /// </summary>
    // Rules:
    //   1. AlarmThresholdForTrace must be >= 1 (0 would mean every district triggers +1)
    //   2. LateBossLingerThreshold must be > 0 (0 would punish immediate extraction)
    //   3. BacktrackTimePerTrace must be > 0 and < TimePerTracePoint
    //      (backtrack tax must be harsher than idle time tax)
    //   4. Source/sink balance check: across all TraceSourceCategory types, estimate average
    //      Trace gain per district. Compare against TraceSinkConfig sink probabilities.
    //      Warn if expected sinks per district > expected sources (pressure should build)
    //   5. Echo mission count per district must be bounded — warn if echo count * 1 Trace
    //      would exceed MaxTrace in a single district
    //
    // Invocation: TraceWorkstation validation tab, also runs on TraceSourceConfigSO import
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/TraceWorkstation/Modules/TraceSimulationModule.cs
namespace Hollowcore.Trace.Editor.Modules
{
    /// <summary>
    /// Monte Carlo simulation of Trace accumulation across expeditions.
    /// Runs in-editor without Play mode.
    /// </summary>
    // Simulation parameters:
    //   - ExpeditionCount: 100 (default)
    //   - DistrictsPerExpedition: 4-8 (configurable range)
    //   - AverageEchoesPerDistrict: 1.2
    //   - AlarmTriggerProbability: 0.3
    //   - DirtyContractProbability: 0.4
    //   - BacktrackProbability: 0.2 (chance player revisits a previous district)
    //   - BacktrackDurationRange: 30-120 seconds
    //   - SinkEncounterRate: from TraceSinkConfig probabilities
    //
    // Output metrics:
    //   - Trace accumulation distribution (histogram): min/median/max/p90 Trace at extraction
    //   - Hunter encounter frequency: % of runs reaching Trace 2, 3, 4+
    //   - Trace cap hit rate: % of runs hitting MaxTrace
    //   - Average Trace per district (line graph over district progression)
    //   - Source breakdown: pie chart of Trace gained per category
    //   - Sink effectiveness: average Trace reduced per expedition
    //
    // Visualization: EditorGUILayout histogram bars, stat table, export CSV button
}
```
