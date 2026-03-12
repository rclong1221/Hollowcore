# EPIC19.1 - Input Mode Abstraction

**Status:** Future
**Dependencies:** EPIC14.8
**Goal:** Decouple input from immediate action execution to support turn-based and command-queue gameplay.

---

## Overview

Current system assumes: "player presses key → action happens immediately." This EPIC abstracts input handling to support games where actions are queued and executed later.

---

## Input Modes

| Mode | Description | Games |
|------|-------------|-------|
| `RealtimeInput` | Key down → immediate action | DIG, Shooters, Action |
| `TurnBasedInput` | Key → queue → execute on End Turn | XCOM, Fire Emblem |
| `PauseWithCommands` | Pause → queue → unpause to execute | Baldur's Gate, Dragon Age |
| `AsyncInput` | Queue → wait for other players | Online turn-based |

---

## IInputMode Interface

| Method | Description |
|--------|-------------|
| `HandleInput(input)` | Process raw input |
| `QueueAction(action)` | Add to action queue |
| `GetQueuedActions()` | View pending actions |
| `ExecuteQueue()` | Run all queued actions |
| `CancelAction(index)` | Remove from queue |
| `ClearQueue()` | Clear all pending |
| `IsExecutionPhase()` | Are actions currently executing? |
| `CanAcceptInput()` | Is input allowed now? |

---

## Action Queue System

**QueuedAction struct:**

| Field | Type | Description |
|-------|------|-------------|
| ActionType | enum | Move, Attack, UseItem, Ability |
| SourceEntity | Entity | Who performs |
| TargetEntity | Entity | Who receives (optional) |
| TargetPosition | float3 | Where (optional) |
| Parameters | Dictionary | Extra data |
| Priority | int | Execution order |

---

## Implementations

### RealtimeInput
- No queue, immediate execution
- `HandleInput` → directly triggers systems
- Already implemented (current behavior)

### TurnBasedInput
- Actions queue during "Planning Phase"
- `EndTurn()` triggers "Execution Phase"
- All queued actions execute in order
- Then switches to enemy turn

### PauseWithCommands
- Spacebar pauses game time
- While paused, player queues actions
- Unpause → actions execute in real-time
- Can re-pause to adjust

### AsyncInput
- Like TurnBased but waits for network
- All players submit actions
- Server resolves simultaneously
- Results broadcast

---

## UI Integration

| Mode | UI Elements |
|------|-------------|
| Realtime | None (immediate feedback) |
| TurnBased | Action preview, queue display, End Turn button |
| PauseWithCommands | Pause indicator, timeline scrubber |
| Async | Waiting indicator, opponent status |

---

## Tasks

### Phase 1: Interface & Data
- [ ] Create `IInputMode` interface
- [ ] Create `QueuedAction` struct
- [ ] Create `ActionQueue` component

### Phase 2: Implementations
- [ ] Extract `RealtimeInput` from current code
- [ ] Create `TurnBasedInput`
- [ ] Create `PauseWithCommandsInput`
- [ ] Create `AsyncInput`

### Phase 3: Integration
- [ ] Refactor input systems to use `IInputMode`
- [ ] Add mode switching support
- [ ] Create action preview system

### Phase 4: UI
- [ ] Queue display widget
- [ ] End Turn button
- [ ] Action preview visualization

---

## Verification

- [ ] Realtime mode works as before
- [ ] Turn-based queues and executes correctly
- [ ] Pause mode pauses/resumes properly
- [ ] Queue can be modified before execution
- [ ] UI reflects current queue state

---

## Success Criteria

- [ ] Input mode swappable via config
- [ ] All four modes functional
- [ ] DIG unchanged (using Realtime)
- [ ] Turn-based feels tactical
