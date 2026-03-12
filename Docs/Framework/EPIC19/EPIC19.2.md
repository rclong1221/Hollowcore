# EPIC19.2 - Time Mode Abstraction

**Status:** Future
**Dependencies:** EPIC19.1 (Input Mode)
**Goal:** Control how game time flows to support turn-based, phase-based, and action-point systems.

---

## Overview

Current system assumes continuous realtime gameplay. This EPIC abstracts time flow for games with discrete turns, phases, or action point budgets.

---

## Time Modes

| Mode | Description | Games |
|------|-------------|-------|
| `Realtime` | Continuous, systems always run | DIG, Action games |
| `TurnBased` | Discrete turns, phases | XCOM, Civilization |
| `ActionPoints` | Limited actions per turn | Divinity, Wasteland |
| `HybridRealtime` | Realtime with tactical pause | Baldur's Gate |

---

## ITimeMode Interface

| Method | Description |
|--------|-------------|
| `GetCurrentPhase()` | Player, Enemy, Environment, etc. |
| `EndTurn()` | Advance to next turn |
| `GetTurnNumber()` | Current turn count |
| `GetRemainingActions()` | Action points left |
| `SpendAction(cost)` | Use action points |
| `IsPlayerPhase()` | Can player act? |
| `PauseTime()` | Freeze game time |
| `ResumeTime()` | Unfreeze game time |
| `GetTimeScale()` | Current time multiplier |

---

## Turn Structure

### TurnBased Mode

```
Turn 1:
├── Player Phase
│   ├── Planning (input queued)
│   └── Execution (actions resolve)
├── Enemy Phase
│   ├── AI Decision
│   └── Execution
└── Environment Phase (optional)
    └── Effects, spawns, etc.

Turn 2: ...
```

### ActionPoints Mode

| Element | Description |
|---------|-------------|
| AP Pool | Each character has action points |
| AP Costs | Move = 1, Attack = 2, Ability = 3 |
| End Turn | When out of AP or manual end |
| Refresh | AP restored at turn start |

---

## Phase System

**TurnPhase enum:**

| Phase | Description |
|-------|-------------|
| `PlayerPlanning` | Player queuing actions |
| `PlayerExecution` | Player actions resolving |
| `EnemyPlanning` | AI calculating moves |
| `EnemyExecution` | AI actions resolving |
| `Environment` | Hazards, effects tick |
| `Cleanup` | End-of-turn processing |

---

## Implementations

### RealtimeTimeMode
- No phases, continuous execution
- `GetCurrentPhase()` returns null
- All systems run every frame
- Already implemented (current behavior)

### TurnBasedTimeMode
- Strict phase progression
- Systems only run in appropriate phase
- Wait for phase completion before advancing

### ActionPointsTimeMode
- Like TurnBased but with AP budget
- Can act until AP depleted
- More flexible action order

### HybridRealtimeTimeMode
- Realtime by default
- Pause freezes time
- While paused, behaves like TurnBased

---

## System Integration

| System Type | Realtime | TurnBased |
|-------------|----------|-----------|
| Player Input | Every frame | Planning phase only |
| Enemy AI | Every frame | Enemy phase only |
| Physics | Continuous | Execution phases |
| Animation | Continuous | May need sync |
| Effects | Continuous | Per-turn tick |

---

## Tasks

### Phase 1: Interface & Data
- [ ] Create `ITimeMode` interface
- [ ] Create `TurnPhase` enum
- [ ] Create `TurnState` component
- [ ] Create `ActionPointPool` component

### Phase 2: Implementations
- [ ] Extract `RealtimeTimeMode`
- [ ] Create `TurnBasedTimeMode`
- [ ] Create `ActionPointsTimeMode`
- [ ] Create `HybridRealtimeTimeMode`

### Phase 3: System Integration
- [ ] Add phase-awareness to all systems
- [ ] Create phase transition events
- [ ] Handle animation during turn execution

### Phase 4: UI
- [ ] Turn counter display
- [ ] Phase indicator
- [ ] Action point display
- [ ] End Turn button

---

## Verification

- [ ] Realtime works as before
- [ ] TurnBased cycles through phases correctly
- [ ] ActionPoints track spending correctly
- [ ] Hybrid pauses/resumes properly
- [ ] Systems respect current phase

---

## Success Criteria

- [ ] Time mode swappable via config
- [ ] All four modes functional
- [ ] DIG unchanged
- [ ] Turn-based feels strategic
