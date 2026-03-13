# EPIC 8.2 Setup Guide: Trace Sources

**Status:** Planned
**Requires:** EPIC 8.1 (TraceState, TraceConfig), EPIC 5 (Echoes), EPIC 6 (Gate Selection), Framework Quest/

---

## Overview

Trace sources are everything that raises the pressure meter. Six categories feed into a single TraceSourceEvent pipeline: echo missions, alarm extraction, dirty contracts, late boss extraction, elapsed time, and backtracking. The backtracking tax is the primary design lever -- every minute spent in a previously completed district adds Trace. All sources funnel through TraceSourceSystem which increments TraceState and fires UI notifications.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 8.1 | TraceState singleton, TraceConfig | Trace meter state and time accumulation |
| EPIC 5 | EchoMissionEntry | Echo start triggers +1 Trace |
| EPIC 6 | Gate selection events | Dirty contract gate triggers +1 Trace |
| EPIC 4 | District transition | Extraction alarm check, backtrack detection |

### New Setup Required
1. Create `TraceSourceConfig` singleton via authoring in the run bootstrap subscene
2. Create `TraceSourceConfigSO` asset at `Assets/Data/Trace/TraceSourceConfig.asset`
3. Initialize `TraceSourceAPI.Initialize()` in TraceBootstrapSystem
4. Hook gameplay events to `TraceSourceAPI.AddTrace()` calls
5. Create `BacktrackTracker` singleton alongside TraceState

---

## 1. TraceSourceConfig Singleton

**Create:** Add `TraceSourceConfigAuthoring` to the run bootstrap subscene (same entity as TraceConfig or nearby).
**Recommended SO:** `Assets > Create > Hollowcore/Trace/Trace Source Config`
**Recommended location:** `Assets/Data/Trace/TraceSourceConfig.asset`

### 1.1 Inspector Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **AlarmThresholdForTrace** | Alarm count in a district that triggers +1 Trace at extraction | 3 | 1-10 |
| **LateBossLingerThreshold** | Seconds after boss kill before late extraction triggers +1 | 60 | 10-300 |
| **BacktrackTimePerTrace** | Seconds in a backtrack district per +1 Trace | 60 | 15-300 |

**Tuning tip:** `BacktrackTimePerTrace=60` means spending 5 minutes backtracking costs 5 Trace. This should feel punishing enough to discourage casual backtracking but not so harsh that quick retrieval runs are impossible. Compare with `TimePerTracePoint` (EPIC 8.1) -- backtrack tax should be harsher than passive time tax.

---

## 2. TraceSourceCategory Enum

Six source categories, each with distinct gameplay triggers:

| Category | Value | Trigger | Amount | Frequency |
|----------|-------|---------|--------|-----------|
| `EchoMission` | 0 | Starting an echo encounter | +1 | Per echo attempted |
| `AlarmExtraction` | 1 | Extraction when alarm count > threshold | +1 | Per district exit (conditional) |
| `DirtyContract` | 2 | Choosing a high-reward gate option | +1 | Per gate selection (conditional) |
| `LateBossExtraction` | 3 | Lingering after boss kill > threshold seconds | +1 | Per district exit (conditional) |
| `TimePassed` | 4 | TimePerTracePoint seconds elapsed (EPIC 8.1) | +1 | Periodic (passive) |
| `Backtracking` | 5 | BacktrackTimePerTrace seconds in a previous district | +1 | Periodic (while backtracking) |

---

## 3. TraceSourceAPI (Cross-System Bridge)

**File:** `Assets/Scripts/Trace/TraceSourceAPI.cs`

Static helper for cross-system Trace submissions. Follows the `DamageVisualQueue` / `XPGrantAPI` pattern.

### 3.1 Initialization
Call `TraceSourceAPI.Initialize()` in `TraceBootstrapSystem.OnCreate()` -- same place as `TraceState` creation.

### 3.2 Usage from Gameplay Systems

| System | Call |
|--------|------|
| Echo quest start | `TraceSourceAPI.AddTrace(1, TraceSourceCategory.EchoMission, "Echo: Memory Fragment")` |
| Extraction alarm check | `TraceSourceAPI.AddTrace(1, TraceSourceCategory.AlarmExtraction)` |
| Gate dirty contract | `TraceSourceAPI.AddTrace(1, TraceSourceCategory.DirtyContract, contractName)` |
| Late boss extraction | `TraceSourceAPI.AddTrace(1, TraceSourceCategory.LateBossExtraction)` |

The `TraceEventListenerSystem` drains the `PendingRequests` queue each frame and creates transient `TraceSourceEvent` entities.

### 3.3 Disposal
Call `TraceSourceAPI.Dispose()` in `TraceBootstrapSystem.OnDestroy()`.

---

## 4. BacktrackTracker Singleton

**Create:** Baked alongside TraceState on the TraceConfig entity.

### 4.1 Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `CurrentDistrictIndex` | District the player is currently in | 0 | 0-7 |
| `HighestDistrictReached` | Forward progress watermark | 0 | 0-7 |
| `BacktrackAccumulatedTime` | Seconds toward next backtrack Trace point | 0.0 | 0.0-BacktrackTimePerTrace |

### 4.2 Backtrack Detection
`IsBacktracking` is true when `CurrentDistrictIndex < HighestDistrictReached`.

The `BacktrackTaxSystem` ticks `BacktrackAccumulatedTime` only while `IsBacktracking == true`. When the player moves forward, `BacktrackAccumulatedTime` resets to 0.

**Tuning tip:** Update `CurrentDistrictIndex` on every district transition. Update `HighestDistrictReached` only when `CurrentDistrictIndex > HighestDistrictReached`. This ensures forward progress is monotonic.

---

## 5. System Execution Order

```
TraceTimeAccumulatorSystem        (SimulationSystemGroup, before TraceSourceSystem)
  |-- Ticks TraceState.AccumulatedTime, creates TimePassed events

BacktrackTaxSystem                (SimulationSystemGroup, before TraceSourceSystem)
  |-- Ticks BacktrackAccumulatedTime, creates Backtracking events

TraceEventListenerSystem          (SimulationSystemGroup, before TraceSourceSystem)
  |-- Drains TraceSourceAPI.PendingRequests queue, creates transient entities

TraceSourceSystem                 (SimulationSystemGroup, before TraceThresholdSystem)
  |-- Reads all TraceSourceEvent entities
  |-- Increments TraceState.CurrentTrace
  |-- Enqueues UI notifications
  |-- Destroys TraceSourceEvent entities
```

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Run bootstrap subscene | TraceSourceConfig singleton authoring | Configures alarm threshold, backtrack rate, linger threshold |
| Run bootstrap subscene | BacktrackTracker singleton (on TraceConfig entity) | Tracks forward progress watermark |
| TraceBootstrapSystem | `TraceSourceAPI.Initialize()` call | Creates the NativeQueue for cross-system submissions |
| Quest system | `TraceSourceAPI.AddTrace()` on echo start | EPIC 5 integration |
| Gate system | `TraceSourceAPI.AddTrace()` on dirty contract | EPIC 6 integration |
| Extraction system | Alarm count check + `TraceSourceAPI.AddTrace()` | EPIC 4.3 integration |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| `TraceSourceAPI.Initialize()` not called | `AddTrace()` silently drops (queue not created) | Call in TraceBootstrapSystem.OnCreate() |
| BacktrackTracker.HighestDistrictReached not updated on forward progress | Player moving forward still counted as backtracking | Update on every forward district transition |
| BacktrackAccumulatedTime not reset on forward move | Stale accumulated time carries into new district | Reset to 0 when CurrentDistrictIndex changes |
| AlarmThresholdForTrace set to 0 | Every district exit triggers +1 Trace (unintended) | Set minimum 1 via validation |
| LateBossLingerThreshold set to 0 | Immediate extraction after boss kill penalizes player | Set minimum > 0 (recommended 30-60s) |
| TraceSourceEvent entities not destroyed | Events accumulate, Trace increments duplicate next frame | TraceSourceSystem must destroy all events after processing |
| Multiple TraceSourceAPI.AddTrace calls for same event | Double Trace gain | Ensure each trigger calls AddTrace exactly once |

---

## Verification

- [ ] TraceSourceEvent entities are created and consumed in a single frame
- [ ] Echo mission start increments Trace by 1 (category=EchoMission)
- [ ] Extraction with alarms > AlarmThresholdForTrace increments Trace by 1
- [ ] Dirty contract gate selection increments Trace by 1
- [ ] Lingering > LateBossLingerThreshold after boss kill increments Trace by 1
- [ ] Time accumulator fires +1 after TimePerTracePoint seconds (EPIC 8.1)
- [ ] Backtrack tax fires +1 per BacktrackTimePerTrace seconds when in a previous district
- [ ] Moving forward resets BacktrackAccumulatedTime to 0
- [ ] HighestDistrictReached updates only on forward progress (never decreases)
- [ ] TraceSourceAPI.AddTrace works from arbitrary systems
- [ ] Multiple sources in the same frame all apply correctly (no clobbering)
- [ ] UI notification queue populated with category and context label for each source
- [ ] Run Trace simulation (Expedition Workstation > Trace Simulation) with expected distribution
