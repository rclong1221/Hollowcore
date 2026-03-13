# EPIC 8.3 Setup Guide: Trace Sinks

**Status:** Planned
**Requires:** EPIC 8.1 (TraceState), EPIC 8.2 (TraceSourceSystem ordering), Framework Quest/, EPIC 6 (Gate Selection), Economy/

---

## Overview

Trace sinks are deliberately scarce. Players reduce Trace through three channels: completing specific side goals ("Erase trail", "Corrupt comms", "Kill witness"), choosing gates with Trace reduction perks, and spending currency at gates to bribe or erase records. Sinks are rarer than sources by design -- Trace should generally climb, and every reduction should feel like a meaningful tactical win.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 8.1 | TraceState singleton | Target for Trace decrement |
| EPIC 8.2 | TraceSourceSystem ordering | TraceSinkSystem runs after TraceSourceSystem |
| Framework Quest/ | Quest completion events | Bridges side goal completion to Trace reduction |
| EPIC 6 | ForwardGateOption entities | Gates can carry GateTraceModifier |

### New Setup Required
1. Create `TraceSinkConfigSO` asset at `Assets/Data/Trace/TraceSinkConfig.asset`
2. Add `TraceSinkConfig` singleton via authoring in the run bootstrap subscene
3. Initialize `TraceSinkAPI.Initialize()` in TraceBootstrapSystem
4. Tag qualifying side goal quests with `TraceSinkQuestTag` during quest generation
5. Add `GateTraceModifier` to qualifying gate option entities during gate generation

---

## 1. TraceSinkConfig Singleton

**Create:** `Assets > Create > Hollowcore/Trace/Trace Sink Config`
**Recommended location:** `Assets/Data/Trace/TraceSinkConfig.asset`

### 1.1 Inspector Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **SideGoalTraceSinkChance** | Probability a generated side goal reduces Trace | 0.15 | 0.0-0.5 |
| **GateTraceSinkChance** | Probability a forward gate offers free -1 Trace perk | 0.10 | 0.0-0.3 |
| **GateBribeChance** | Probability a gate offers a paid Trace reduction option | 0.08 | 0.0-0.2 |
| **BaseBribeCost** | Base currency cost for gate bribe/erase | 200 | 50-1000 |
| **BribeCostPerTraceLevel** | Multiplier per current Trace level (higher Trace = pricier) | 1.25 | 1.0-2.0 |

**Tuning tip:** Sinks should be noticeably rarer than sources. At `SideGoalTraceSinkChance=0.15`, roughly 1 in 7 side goals reduces Trace. Combined with `GateTraceSinkChance=0.10`, a player might see 1-2 sink opportunities per district. The Balance Dashboard in TraceWorkstation visualizes the source-to-sink ratio -- aim for 2:1 to 4:1.

---

## 2. TraceSinkCategory Enum

| Category | Value | Trigger | Amount |
|----------|-------|---------|--------|
| `SideGoal` | 0 | Completing a TraceSinkQuestTag quest | -1 |
| `GatePerk` | 1 | Selecting a gate with free -1 Trace modifier | -1 |
| `Bribe` | 2 | Paying currency at gate for Trace reduction | -1 |
| `HunterLoot` | 3 | Defeating a hunter (EPIC 8.4) drops Trace-reducing item | -1 |
| `Special` | 4 | One-off narrative events, rare items | varies |

---

## 3. TraceSinkQuestTag Setup

**File:** `Assets/Scripts/Trace/Components/TraceSinkComponents.cs`

During quest generation, when a side goal is selected as Trace-reducing (based on `SideGoalTraceSinkChance`):

1. Add `TraceSinkQuestTag` to the quest entity
2. Set `TraceReduction = 1`
3. The quest's display name should reflect the Trace-reducing nature (e.g., "Erase Trail", "Corrupt Comms", "Kill Witness")

### 3.1 TraceSinkQuestBridgeSystem
Bridges quest completion to Trace reduction:
- Queries entities with `QuestCompleteTag + TraceSinkQuestTag`
- Creates `TraceSinkEvent` with `Amount = TraceReduction, Category = SideGoal`
- Removes `TraceSinkQuestTag` after processing

---

## 4. GateTraceModifier Setup

**File:** `Assets/Scripts/Trace/Components/TraceSinkComponents.cs`

Added to gate option entities during gate generation (EPIC 6.1).

### 4.1 Component Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `TraceChange` | Trace modification when gate selected (negative = reduction) | -1 | -2 to -1 for sinks |
| `BribeCost` | Currency cost for paid options (0 = free perk) | 0 | 0-1000 |
| `IsPaidOption` | True if this is a bribe (requires currency), false if free perk | false | -- |

### 4.2 Gate Generation Integration
In the gate generation utility (EPIC 6):
1. Roll `GateTraceSinkChance` -- if hit, add `GateTraceModifier(TraceChange=-1, IsPaidOption=false)`
2. Roll `GateBribeChance` -- if hit, add `GateTraceModifier(TraceChange=-1, IsPaidOption=true, BribeCost=calculated)`
3. Bribe cost = `BaseBribeCost * (1 + (currentTrace * BribeCostPerTraceLevel / 10))`

**Tuning tip:** At Trace 4 with `BaseBribeCost=200` and `BribeCostPerTraceLevel=1.25`, bribe costs 300 currency. This should feel expensive but not impossible for a player who has been looting efficiently.

---

## 5. TraceSinkAPI (Cross-System Bridge)

**File:** `Assets/Scripts/Trace/TraceSinkAPI.cs`

Mirrors the TraceSourceAPI pattern.

### 5.1 Initialization
Call `TraceSinkAPI.Initialize()` in `TraceBootstrapSystem.OnCreate()`.

### 5.2 Usage
| System | Call |
|--------|------|
| Hunter defeat | `TraceSinkAPI.ReduceTrace(1, TraceSinkCategory.HunterLoot, hunterName)` |
| Special items | `TraceSinkAPI.ReduceTrace(amount, TraceSinkCategory.Special, itemName)` |
| Narrative events | `TraceSinkAPI.ReduceTrace(1, TraceSinkCategory.Special, eventName)` |

---

## 6. System Execution Order

```
TraceSinkQuestBridgeSystem  (SimulationSystemGroup, before TraceSinkSystem)
  |-- Bridges quest completion to TraceSinkEvent

GateTraceModifierSystem     (SimulationSystemGroup, before TraceSinkSystem)
  |-- Processes gate selection Trace changes

TraceSinkSystem             (SimulationSystemGroup, before TraceThresholdSystem, after TraceSourceSystem)
  |-- Reads all TraceSinkEvent entities
  |-- Decrements TraceState.CurrentTrace (clamp to 0)
  |-- Enqueues UI notifications
  |-- Destroys TraceSinkEvent entities
```

---

## 7. Balance Dashboard (Editor Tool)

**Open:** Trace Workstation > "Balance Dashboard" tab
**File:** `Assets/Editor/TraceWorkstation/Modules/TraceBalanceDashboardModule.cs`

| Feature | Description |
|---------|-------------|
| Source column | Expected Trace gain per district per category |
| Sink column | Expected Trace reduction per district per category |
| Balance meter | Source-to-sink ratio bar (Green: 2:1-4:1, Yellow: warning, Red: broken) |
| Threshold line | Horizontal bar showing average Trace at each district with confidence interval |

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Run bootstrap subscene | TraceSinkConfig singleton authoring | Configures sink rarity and bribe costs |
| TraceBootstrapSystem | `TraceSinkAPI.Initialize()` call | Creates NativeQueue for cross-system sink submissions |
| Quest generation | TraceSinkQuestTag on qualifying side goals | Based on SideGoalTraceSinkChance roll |
| Gate generation | GateTraceModifier on qualifying gates | Based on GateTraceSinkChance / GateBribeChance |
| Gate UI | Display "+1 TRACE" / "-1 TRACE" badges | Read from GateTraceModifier |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| Trace drops below 0 | Negative Trace value displayed | TraceSinkSystem must clamp `CurrentTrace = math.max(0, CurrentTrace - Amount)` |
| SideGoalTraceSinkChance > 0.5 | Too many sink quests, pressure never builds | Validation enforces max 0.5; recommended 0.10-0.20 |
| GateBribeChance > GateTraceSinkChance | Paid options more common than free perks (feels bad) | Validation warns; keep bribes rarer than free perks |
| BribeCostPerTraceLevel < 1.0 | Bribes get cheaper at higher Trace (wrong direction) | Validation enforces >= 1.0 |
| TraceSinkQuestTag.TraceReduction = 0 | Quest completes but Trace doesn't decrease | Ensure TraceReduction >= 1 |
| GateTraceModifier not added to gate entity | Gate perk text shows but Trace unchanged | Verify GateTraceModifierSystem reads the component on gate selection |
| TraceSinkAPI.Initialize() not called | ReduceTrace silently drops requests | Call in TraceBootstrapSystem.OnCreate() |

---

## Verification

- [ ] TraceSinkEvent entities consumed in a single frame
- [ ] Completing a TraceSinkQuestTag quest reduces Trace by 1
- [ ] Selecting a gate with free -1 Trace modifier reduces Trace
- [ ] Bribe option deducts currency before reducing Trace
- [ ] Bribe fails gracefully if insufficient currency (no Trace change, no currency loss)
- [ ] Trace never drops below 0
- [ ] TraceThresholdSystem deactivates threshold effects when Trace drops below a breakpoint
- [ ] TraceSinkAPI.ReduceTrace works from arbitrary systems (hunter loot, special items)
- [ ] UI notification queue populated with sink category and context label
- [ ] Sink gates appear at approximately `GateTraceSinkChance` rate (verify over 100 gates)
- [ ] Side goal Trace quests appear at approximately `SideGoalTraceSinkChance` rate
- [ ] Balance Dashboard shows source-to-sink ratio in the 2:1-4:1 green zone
