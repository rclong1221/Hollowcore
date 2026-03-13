# EPIC 8: Trace (Global Pressure Meter)

**Status**: Planning
**Priority**: High — Expedition-wide pacing
**Dependencies**: EPIC 4 (District Graph), EPIC 6 (Gate Selection)
**GDD Sections**: 9.1-9.4 Trace

---

## Problem

The expedition needs a global pressure system that makes every action carry weight — not just local district pressure (that's the Front), but expedition-wide heat. Trace represents how much attention the player has attracted. It climbs whether pushing forward or backtracking, and its primary role is as the backtracking tax — ensuring players can't endlessly revisit previous districts without consequence.

---

## Overview

Trace is a single escalating meter for the entire expedition. It rises from combat, alarms, time, echoes, and dirty contracts. It decreases from specific side goals, gate types, and bribes. At thresholds, it reduces forward gate options, spawns hunters, upgrades bosses, and inflates prices. Trace is the cost of greed.

---

## Sub-Epics

### 8.1: Trace State & Threshold Model
Core meter and breakpoints.

- **TraceState** (IComponentData, expedition-level singleton):
  - CurrentTrace (int, 0+), MaxTrace (soft cap for UI), AccumulatedTime (for gradual gain)
- **Trace thresholds** (GDD §9.3):
  - 0-1: Baseline — no effects
  - 2: Hunters common — hunter enemies spawn in districts
  - 3: Forward gates drop to 2 (from 3) — fewer escape routes
  - 4+: Boss upgrades active, services pricier, all Fronts become Volatile
- **TraceThresholdSystem**: monitors TraceState → applies/removes threshold effects
  - Threshold effects implemented as RunModifierStack entries
  - Hunter spawn rate modifier
  - Gate count modifier
  - Boss difficulty modifier
  - Vendor price multiplier

### 8.2: Trace Sources
What raises Trace.

- **TraceSources** (GDD §9.1):
  - Echo Missions: +1 per echo attempted
  - Alarm threshold: +1 at extraction if too many alarms triggered in district
  - Dirty contracts at gate: +1 for choosing better-reward gate option
  - Late boss extraction: +1 if boss killed but stayed in district afterward
  - Time in any district: gradual +1 per threshold (e.g., every 5 minutes)
  - Backtracking: time in previous districts still counts for gradual gain
- **TraceSourceSystem**: listens for events (quest complete, alarm, extraction, timer) → increments TraceState
- **Trace as backtrack tax** (GDD §9.4): every minute in a previous district adds Trace. This is the primary cost of going back

### 8.3: Trace Sinks
What lowers Trace.

- **TraceSinks** (GDD §9.2):
  - Specific side goals: "Erase trail," "Corrupt comms," "Kill witness" → -1 Trace
  - Specific gate types: some forward gates offer -1 Trace as perk
  - Bribe/erase at gate: spend currency or loot to reduce Trace (rare option)
- **TraceSinkSystem**: quest completion / gate selection → decrements TraceState
- **Trace reduction is scarce**: sinks are rarer than sources by design — pressure should build

### 8.4: Hunter Encounters
Trace-triggered enemies.

- **HunterDefinitionSO**: elite enemy type that only appears at Trace 2+
  - Specialized hunter variants per district
  - Hunters have tracking behavior — seek player across zones
  - Scale with Trace level: Trace 2 = solo hunters, Trace 4+ = hunter squads
- **HunterSpawnSystem**: triggered by TraceThresholdSystem
  - Spawns hunters in current district at rate proportional to Trace
  - Hunters persist within district visit (don't despawn on zone transition)
- **Hunter loot**: defeating hunters drops Trace-reducing items (partial sink)

### 8.5: Trace UI
Player-facing pressure feedback.

- **Trace meter**: always-visible HUD element showing current Trace level
  - Color-coded: green (0-1), yellow (2), orange (3), red (4+)
  - Pulsing at threshold crossings
- **Trace source notifications**: brief popup when Trace increases ("Alarm triggered: +1 Trace")
- **Gate screen integration**: forward gates show Trace impact (dirty contracts show +1 warning)
- **Threshold warning**: screen edge effect when approaching next threshold

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Roguelite/ (RunModifierStack) | Trace thresholds are run modifiers |
| Quest/ | Trace-reducing side goals are quest rewards |
| AI/ | Hunter behavior uses existing AI systems |
| EPIC 6 (Gate Selection) | Trace affects gate count and options |
| EPIC 14 (Boss) | Trace 4+ applies boss difficulty modifier |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 8.1 (State & Thresholds) | None — foundation | — |
| 8.2 (Sources) | 8.1 | EPIC 5 (echoes as source) |
| 8.3 (Sinks) | 8.1 | Quest/ (sink quests) |
| 8.4 (Hunters) | 8.1 | AI/ (hunter behavior) |
| 8.5 (UI) | 8.1 | — |

---

## Vertical Slice Scope

- 8.1 (state), 8.2 (sources), 8.3 (sinks) required — Trace is in GDD §17.4
- 8.4 (hunters) at least basic hunter type for Trace 2+
- 8.5 (UI) basic meter required

---

## Tooling & Quality

| Sub-Epic | BlobAsset Pipeline | Validation | Editor Tooling | Live Tuning | Debug Visualization | Simulation |
|---|---|---|---|---|---|---|
| 8.1 (State & Thresholds) | — | Threshold monotonic ordering, config range checks | — | TimePerTracePoint, MaxTrace, prices, gate count, hunter rate | Trace meter overlay with threshold number line, source/sink event log | — |
| 8.2 (Sources) | — | Source config range checks, source/sink balance warning | — | (shared with 8.1) | Accumulator progress bars, backtrack indicator | 100-expedition Monte Carlo: Trace distribution, hunter encounter %, cap hit rate |
| 8.3 (Sinks) | — | Sink rarity bounds, source/sink balance ratio check | Sources vs Sinks balance dashboard, threshold number line | (shared with 8.1) | (shared with 8.1 event log) | (shared with 8.2 simulation) |
| 8.4 (Hunters) | HunterDefinitionBlob + HunterDefinitionDatabase (BlobArray, O(1) variant lookup) | VariantId uniqueness, district cross-ref, base stat positivity, universal fallback exists | (integrated in balance dashboard) | Spawn threshold, spawn rate, active cap, scaling overrides, force spawn | Hunter proximity arrows, active roster, spawn state, scene gizmos | (hunter encounters tracked in 8.2 simulation) |
| 8.5 (UI) | — | — | — | — | Extended Trace UI debug: event log, accumulator bars, vignette intensity | — |
