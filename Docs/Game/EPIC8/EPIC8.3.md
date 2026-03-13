# EPIC 8.3: Trace Sinks

**Status**: Planning
**Epic**: EPIC 8 — Trace (Global Pressure Meter)
**Priority**: High — Pressure without relief is frustration, not tension
**Dependencies**: EPIC 8.1 (TraceState); Framework: Quest/, EPIC 6 (Gate Selection), Economy/

---

## Overview

Trace sinks are deliberately scarce. The player can reduce Trace through three channels: completing specific side goals ("Erase trail," "Corrupt comms," "Kill witness"), choosing gate types that offer Trace reduction, and spending currency at gates to bribe or erase records. Sinks are rarer than sources by design -- Trace should generally climb, and every reduction should feel like a meaningful tactical win.

---

## Component Definitions

### TraceSinkEvent (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceSinkComponents.cs
using Unity.Collections;
using Unity.Entities;

namespace Hollowcore.Trace
{
    /// <summary>
    /// Category of Trace reduction for UI and analytics.
    /// </summary>
    public enum TraceSinkCategory : byte
    {
        SideGoal = 0,     // "Erase trail", "Corrupt comms", "Kill witness"
        GatePerk = 1,     // Forward gate that includes -1 Trace
        Bribe = 2,        // Currency spent at gate to reduce Trace
        HunterLoot = 3,   // Defeating a hunter drops Trace-reducing item
        Special = 4       // One-off narrative events, rare items
    }

    /// <summary>
    /// Transient entity requesting a Trace decrement. Created by gameplay systems,
    /// consumed by TraceSinkSystem in the same frame.
    /// </summary>
    public struct TraceSinkEvent : IComponentData
    {
        /// <summary>Amount of Trace to remove (positive value, will be subtracted).</summary>
        public int Amount;

        /// <summary>Why Trace is decreasing (for UI popup and analytics).</summary>
        public TraceSinkCategory Category;

        /// <summary>Optional context string for UI (e.g., "Side Goal: Kill Witness").</summary>
        public FixedString64Bytes ContextLabel;
    }

    /// <summary>
    /// Cleanup tag for TraceSinkEvent transient entities.
    /// </summary>
    public struct TraceSinkCleanup : ICleanupComponentData { }
}
```

### TraceSinkQuestTag (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceSinkComponents.cs (continued)
namespace Hollowcore.Trace
{
    /// <summary>
    /// Marker on quest entities whose completion reduces Trace.
    /// Added during quest generation for "Erase trail" / "Corrupt comms" / "Kill witness" quests.
    /// </summary>
    public struct TraceSinkQuestTag : IComponentData
    {
        /// <summary>Trace reduction on quest completion (usually 1).</summary>
        public int TraceReduction;
    }
}
```

### GateTraceModifier (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceSinkComponents.cs (continued)
namespace Hollowcore.Trace
{
    /// <summary>
    /// Optional component on gate option entities indicating Trace modification.
    /// Positive = adds Trace (dirty contracts, from EPIC 8.2).
    /// Negative = reduces Trace (sink gate perk).
    /// </summary>
    public struct GateTraceModifier : IComponentData
    {
        /// <summary>Trace change when this gate is selected. Negative = reduction.</summary>
        public int TraceChange;

        /// <summary>Currency cost to activate bribe/erase option (0 = free perk).</summary>
        public int BribeCost;

        /// <summary>If true, this is a paid option (bribe/erase) rather than a free gate perk.</summary>
        public bool IsPaidOption;
    }
}
```

### TraceSinkConfig (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/TraceSinkComponents.cs (continued)
namespace Hollowcore.Trace
{
    /// <summary>
    /// Tuning singleton for Trace sink rarity and costs.
    /// </summary>
    public struct TraceSinkConfig : IComponentData
    {
        /// <summary>Probability (0-1) that a generated side goal is a Trace-reducing type.</summary>
        public float SideGoalTraceSinkChance;

        /// <summary>Probability (0-1) that a forward gate offers -1 Trace perk.</summary>
        public float GateTraceSinkChance;

        /// <summary>Probability (0-1) that a gate offers a paid bribe/erase option.</summary>
        public float GateBribeChance;

        /// <summary>Base currency cost for bribe/erase at gate.</summary>
        public int BaseBribeCost;

        /// <summary>Bribe cost multiplier per current Trace level (higher Trace = pricier bribes).</summary>
        public float BribeCostPerTraceLevel;
    }
}
```

---

## Systems

### TraceSinkSystem

```csharp
// File: Assets/Scripts/Trace/Systems/TraceSinkSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: TraceThresholdSystem
// UpdateAfter: TraceSourceSystem
//
// Reads: TraceSinkEvent (transient entities)
// Writes: TraceState (CurrentTrace)
//
// Each frame:
//   1. Query all TraceSinkEvent entities
//   2. For each event:
//      a. Decrement TraceState.CurrentTrace by event.Amount
//      b. Clamp CurrentTrace to minimum 0
//      c. Enqueue UI notification: (Category, Amount, ContextLabel) → TraceUINotificationQueue
//   3. Destroy all TraceSinkEvent entities (cleanup pass)
//   4. NOTE: TraceThresholdSystem runs after this and will deactivate
//      threshold effects if CurrentTrace dropped below a breakpoint
```

### TraceSinkQuestBridgeSystem

```csharp
// File: Assets/Scripts/Trace/Systems/TraceSinkQuestBridgeSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: TraceSinkSystem
//
// Bridges quest completion events to Trace reduction.
//
// For each entity with QuestCompleteTag + TraceSinkQuestTag:
//   1. Read TraceSinkQuestTag.TraceReduction
//   2. Create TraceSinkEvent: Amount=TraceReduction, Category=SideGoal,
//      ContextLabel from quest display name
//   3. Remove TraceSinkQuestTag (quest system handles QuestCompleteTag lifecycle)
```

### GateTraceModifierSystem

```csharp
// File: Assets/Scripts/Trace/Systems/GateTraceModifierSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: TraceSinkSystem
//
// Processes Trace changes from gate selection.
//
// On gate selection event (from EPIC 6 gate system):
//   1. Check selected gate entity for GateTraceModifier
//   2. If GateTraceModifier.TraceChange < 0 (sink):
//      a. If IsPaidOption: deduct BribeCost from player currency
//         - If insufficient funds: skip (gate selection UI should prevent this)
//      b. Create TraceSinkEvent: Amount=|TraceChange|, Category=(IsPaidOption ? Bribe : GatePerk)
//   3. If GateTraceModifier.TraceChange > 0 (source, dirty contract):
//      a. Handled by TraceEventListenerSystem (EPIC 8.2) — not duplicated here
```

---

## Static API

### TraceSinkAPI

```csharp
// File: Assets/Scripts/Trace/TraceSinkAPI.cs
using Unity.Collections;

namespace Hollowcore.Trace
{
    /// <summary>
    /// Static helper for cross-system Trace reduction submissions.
    /// Mirrors TraceSourceAPI pattern.
    /// </summary>
    public static class TraceSinkAPI
    {
        internal static NativeQueue<TraceSinkRequest> PendingRequests;

        public static void Initialize()
        {
            if (!PendingRequests.IsCreated)
                PendingRequests = new NativeQueue<TraceSinkRequest>(Allocator.Persistent);
        }

        public static void Dispose()
        {
            if (PendingRequests.IsCreated)
                PendingRequests.Dispose();
        }

        /// <summary>
        /// Queue a Trace decrement. Used by hunter loot, special items, narrative events.
        /// </summary>
        public static void ReduceTrace(int amount, TraceSinkCategory category,
            FixedString64Bytes contextLabel = default)
        {
            if (!PendingRequests.IsCreated) return;
            PendingRequests.Enqueue(new TraceSinkRequest
            {
                Amount = amount,
                Category = category,
                ContextLabel = contextLabel
            });
        }
    }

    public struct TraceSinkRequest
    {
        public int Amount;
        public TraceSinkCategory Category;
        public FixedString64Bytes ContextLabel;
    }
}
```

---

## Setup Guide

1. Add TraceSinkComponents to `Assets/Scripts/Trace/Components/`
2. Create `TraceSinkConfigSO` asset at `Assets/Data/Trace/TraceSinkConfig.asset`:
   - SideGoalTraceSinkChance: 0.15 (15% of generated side goals reduce Trace)
   - GateTraceSinkChance: 0.10 (10% of forward gates offer free -1 Trace)
   - GateBribeChance: 0.08 (8% of gates offer paid Trace reduction)
   - BaseBribeCost: 200
   - BribeCostPerTraceLevel: 1.25
3. Initialize `TraceSinkAPI.Initialize()` in TraceBootstrapSystem
4. Quest generation system: when rolling a Trace-reducing side goal, add `TraceSinkQuestTag` with TraceReduction=1
5. Gate generation system (EPIC 6): when rolling a Trace sink gate, add `GateTraceModifier` with TraceChange=-1
6. Gate UI: display GateTraceModifier.TraceChange as "-1 Trace" perk; if IsPaidOption, show BribeCost
7. Hunter loot system (EPIC 8.4): on hunter defeat, call `TraceSinkAPI.ReduceTrace(1, HunterLoot)`

---

## Verification

- [ ] TraceSinkEvent entities consumed in a single frame
- [ ] Completing a TraceSinkQuestTag quest reduces Trace by 1
- [ ] Selecting a gate with GateTraceModifier (TraceChange=-1) reduces Trace
- [ ] Bribe option deducts currency before reducing Trace
- [ ] Bribe fails gracefully if player has insufficient currency
- [ ] Trace never drops below 0
- [ ] TraceThresholdSystem deactivates threshold effects when Trace drops below breakpoint
- [ ] TraceSinkAPI.ReduceTrace works from arbitrary systems
- [ ] UI notification queue populated with sink category and context
- [ ] Sink gates appear at configured rarity (approximately 10% of gates)
- [ ] Side goal Trace quests appear at configured rarity (approximately 15%)

---

## Validation

```csharp
// File: Assets/Editor/TraceWorkstation/TraceSinkValidation.cs
namespace Hollowcore.Trace.Editor
{
    /// <summary>
    /// Validates Trace sink configuration for intentional scarcity.
    /// </summary>
    // Rules:
    //   1. SideGoalTraceSinkChance must be in (0, 0.5] — above 50% undermines pressure design
    //   2. GateTraceSinkChance must be in (0, 0.3] — sink gates should be uncommon
    //   3. GateBribeChance must be < GateTraceSinkChance — bribes are rarer than free perks
    //   4. BaseBribeCost must be > 0
    //   5. BribeCostPerTraceLevel must be >= 1.0 (costs scale up, never down)
    //   6. Source/sink balance: total expected sink rate across all channels per district
    //      must be < total expected source rate. Warn with ratio if sinks > 60% of sources
    //   7. TraceSinkQuestTag.TraceReduction must be > 0 and <= 2 (no massive single-event drops)
    //   8. GateTraceModifier.TraceChange must be in [-2, -1] for sinks (bounded reduction)
    //
    // Cross-validation with EPIC 8.2 source config: shared TraceBalanceReport
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/TraceWorkstation/Modules/TraceBalanceDashboardModule.cs
namespace Hollowcore.Trace.Editor.Modules
{
    /// <summary>
    /// TraceWorkstation module: Sources vs Sinks balance dashboard.
    /// Visualizes the economy in a single view for designers.
    /// </summary>
    // Layout:
    //   1. Source column (left): lists all TraceSourceCategory types with expected gain/district
    //      - EchoMission: configurable average echoes * 1
    //      - AlarmExtraction: alarm probability * 1
    //      - DirtyContract: dirty contract probability * 1
    //      - LateBossExtraction: boss linger probability * 1
    //      - TimePassed: district duration / TimePerTracePoint
    //      - Backtracking: backtrack probability * backtrack duration / BacktrackTimePerTrace
    //      - Total expected source per district (bold)
    //
    //   2. Sink column (right): lists all TraceSinkCategory types with expected reduction/district
    //      - SideGoal: SideGoalTraceSinkChance * side goals per district
    //      - GatePerk: GateTraceSinkChance * 1
    //      - Bribe: GateBribeChance * bribe success rate
    //      - HunterLoot: hunter encounter rate * hunter defeat rate * 1
    //      - Total expected sink per district (bold)
    //
    //   3. Balance meter (center): bar showing source-to-sink ratio
    //      - Green zone: 2:1 to 4:1 (intended pressure)
    //      - Yellow zone: 1.5:1 to 2:1 or 4:1 to 6:1 (tuning warning)
    //      - Red zone: <1.5:1 (too easy) or >6:1 (too punishing)
    //
    //   4. Threshold number line: horizontal bar showing Trace thresholds 0-MaxTrace
    //      - Average Trace at each district marked with dots
    //      - Confidence interval (p10-p90) shown as shaded region
    //
    // Data sources: TraceConfigSO, TraceSourceConfigSO, TraceSinkConfigSO
}
```
