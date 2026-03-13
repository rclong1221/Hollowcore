# EPIC 6: Gate Selection & Navigation

**Status**: Planning
**Priority**: High — Inter-district decision point
**Dependencies**: EPIC 4 (District Graph), EPIC 3 (Front), EPIC 5 (Echoes)
**GDD Sections**: 7.1-7.4 Gate Selection

---

## Problem

After extracting from a district, the player faces the expedition's most important decision: where to go next. The Gate Screen shows forward paths to new districts AND backward paths to previous ones, each with rich information about risk and reward. This screen IS the strategic layer — it turns the expedition from a sequence of combat encounters into a decision graph.

---

## Overview

The Gate Selection screen is a full-screen UI presented during district transitions. It shows forward gates (2-3 new district options) and backtrack gates (every previous district), each with detailed previews of what awaits. Players can scan gates for more info or reroll forward options at a cost.

---

## Sub-Epics

### 6.1: Forward Gate Presentation
New district options.

- **ForwardGateGenerationSystem**: generates 2-3 forward gate offers from ExpeditionGraphState
  - Fewer gates at high Trace (EPIC 8): Trace 3+ → only 2 forward gates
  - Each gate shows (GDD §7.1):
    - District name + thumbnail/icon
    - Reward focus tag (Face, Memory, Augment, Currency, etc.)
    - Known Threat: 1 confirmed enemy faction
    - Front Forecast: Volatile / Steady / Slow (how fast Front will advance)
    - Strife Interaction tag: how current Strife card affects this district (EPIC 7)
    - Unknown Clause: mystery slot (reveals with scan)
  - Selection: seed-deterministic — same seed + state = same offers

### 6.2: Backtrack Gate Presentation
Previous district status.

- **BacktrackGateSystem**: shows every previously visited district below forward gates
  - Each shows (GDD §7.2):
    - Current Front phase (color-coded: green/yellow/orange/red)
    - Active echoes and what they guard (rewards preview)
    - Seeded events: "Your body is here," "Rare merchant," "Vault unlocked"
    - District Bleed status (if bleeding into adjacent districts)
    - Estimated danger vs when you left (delta)
  - Data pulled from DistrictSaveState (EPIC 4.2)
- **Backtrack temptation** (GDD §6.4): "Is what's back there worth fighting through Phase 4?"
  - UI designed to show both the reward and the danger clearly
  - Scar Map mini-view for spatial context

### 6.3: Gate Scan & Reroll
Information trading.

- **Gate Scan** (GDD §7.3): reveal the Unknown Clause on a forward gate
  - Cost: currency, Compendium Page, or +1 Trace
  - Reveals: hidden threat type, special event, reward modifier, or trap
  - One scan per gate, unlimited gates scannable (at cost)
- **Gate Reroll**: regenerate the forward gate set entirely
  - Higher cost than scan
  - Uses next seed in deterministic chain (no save-scumming)
  - Limited rerolls per expedition (2-3)

### 6.4: Party Choice Flow
Co-op decision making.

- **Co-op vote system** (GDD §7.4):
  - Each player selects a gate preference
  - Majority wins
  - Tie: host decides (or random)
  - Vote timer: 30-60 seconds, then auto-select host's choice
- **Solo**: instant selection, no vote needed
- **Vote UI**: player portraits next to their chosen gate, countdown timer

### 6.5: Gate Transition Flow
The full transition experience.

- **Extraction sequence**: player reaches district exit → extraction cinematic/UI
- **Gate Screen appears** during loading gap
- **Post-selection**: brief loading → spawn in new/returned district
- **Integration with district loading** (EPIC 4.3):
  - Forward gate: generate new district from seed
  - Backtrack gate: regenerate from seed + apply saved delta
- **Trace increment**: time in gate screen doesn't add Trace (safe decision space)

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Roguelite/ (ZoneTransitionSystem) | Gate selection replaces simple zone transition |
| UI/ | Full-screen gate selection UI |
| Party/ | Co-op vote synchronization |
| Persistence/ | Gate state saved with expedition |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 6.1 (Forward Gates) | EPIC 4.1 (graph) | EPIC 7 (Strife tags), EPIC 8 (Trace reduces gates) |
| 6.2 (Backtrack Gates) | EPIC 4.1, 4.2 | EPIC 3 (Front phase), EPIC 5 (echo info) |
| 6.3 (Scan/Reroll) | 6.1 | EPIC 8 (Trace as scan cost), EPIC 9 (Compendium as cost) |
| 6.4 (Party Choice) | 6.1 | — |
| 6.5 (Transition Flow) | 6.1 or 6.2, EPIC 4.3 | — |

---

## Tooling & Quality

| Category | Sub-Epic | Details |
|---|---|---|
| **BlobAsset Pipeline** | 6.1 | `GateDefinitionBlob` — scan costs, unknown clause weights, gate count thresholds baked from `GateDefinitionSO` |
| **Validation** | 6.1 | Gate count constraints (2-3), unknown clause weight sums, scan cost ranges |
| **Validation** | 6.3 | Scan/reroll cost range checks, seed chain uniqueness |
| **Editor Tooling** | 6.1 | Gate Card Preview (sample data rendering without play mode), Gate Layout Editor (full screen composition) |
| **Live Tuning** | 6.1, 6.3 | `GateLiveTuning` singleton — scan costs, reroll costs, gate count thresholds via RunWorkstation |
| **Debug Visualization** | 6.1 | Gate selection screen state overlay (available gates, scan status, vote state) |
| **Debug Visualization** | 6.2 | Backtrack gate state overlay (Front phase, danger delta, seeded events, Scar Map) |
| **Debug Visualization** | 6.4 | Party vote state overlay (per-player votes, tally, timer, tiebreak) |
| **Debug Visualization** | 6.5 | Transition pipeline overlay (phase progress, loading metrics) |
| **Simulation** | 6.1 | Gate diversity test — 100 seeds, verify no 3 identical reward focus categories |
| **Simulation** | 6.2 | Backtrack danger curve validation, seeded event determinism |
| **Simulation** | 6.4 | Vote resolution test cases (solo, majority, tie, timeout, disconnect) |
| **Simulation** | 6.5 | Transition pipeline phase ordering, Trace pause, backtrack delta application |

---

## Vertical Slice Scope

- 6.1 (forward), 6.2 (backtrack), 6.5 (transition) required for GDD §17.4
- 6.3 (scan/reroll) nice to have for vertical slice
- 6.4 (party choice) only needed for co-op testing
