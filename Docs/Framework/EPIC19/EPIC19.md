# EPIC19 - Genre Abstraction Layer

**Status:** Future (Deferred from EPIC14)
**Dependencies:** EPIC14.9 (Complete equipment + camera system)
**Goal:** Make the engine truly genre-agnostic to support any game type: turn-based RPG, RTS, point-and-click, 2D, VR, and beyond.

---

## Overview

EPIC 14 builds an equipment system optimized for **realtime 3D action games** (DIG, ARPG roguelite). EPIC 19 expands this to support **any game genre** by abstracting the remaining assumptions.

---

## What EPIC 14 Covers (Not Repeated Here)

| System | EPIC | Status |
|--------|------|--------|
| Equipment slots & categories | 14.5 | Architecture |
| Tooling suite | 14.6 | Editors |
| Targeting abstraction | 14.7 | Camera Raycast, Cursor-Aim, Lock-On, Click-Select |
| Combat resolver abstraction | 14.8 | Physics, Stat-Based, Hybrid |
| Camera modes (3rd-person, Isometric, Top-Down) | 14.9 | For DIG + ARPG |

> [!NOTE]
> **Isometric camera support moved to EPIC 14.9** since it's needed for the ARPG roguelite. EPIC 19.4 now focuses only on 2D, text, and VR modes.

---

## What EPIC 19 Adds

| Abstraction | Enables | Sub-EPIC |
|-------------|---------|----------|
| Input Mode | Turn-based, pause-with-commands | 19.1 |
| Time Mode | Turn phases, action points | 19.2 |
| Entity Selection | Party control, RTS units | 19.3 |
| Visual Mode Expansion | 2D, text, full VR | 19.4 |
| Interaction Mode | Click-to-move, grid movement | 19.5 |
| Game Profile System | One-click genre switching | 19.6 |

---

## Sub-EPICs

### EPIC 19.1 - Input Mode Abstraction

**Goal:** Decouple input from immediate action execution.

| Mode | Description |
|------|-------------|
| `RealtimeInput` | Current: key down → action (already have) |
| `TurnBasedInput` | Key → queue action → execute on "End Turn" |
| `PauseWithCommands` | Pause game → queue actions → unpause to execute |
| `AsyncInput` | For network turn-based (wait for opponent) |

**Interface: `IInputMode`**
- `QueueAction(action)`
- `ExecuteQueue()`
- `IsExecutionPhase()`
- `CancelQueued()`

---

### EPIC 19.2 - Time Mode Abstraction

**Goal:** Control how game time flows.

| Mode | Description |
|------|-------------|
| `Realtime` | Continuous (already have) |
| `TurnBased` | Discrete turns, phases |
| `ActionPoints` | Limited actions per turn |
| `HybridRealtime` | Realtime with tactical pause |

**Interface: `ITimeMode`**
- `GetCurrentPhase()` (Player, Enemy, Environment)
- `EndTurn()`
- `GetRemainingActions()`
- `IsPlayerPhase()`

---

### EPIC 19.3 - Entity Selection Abstraction

**Goal:** Control single character vs party vs RTS units.

| Mode | Description |
|------|-------------|
| `SinglePlayer` | One entity (already have) |
| `PartyControl` | Cycle through party, AI for others |
| `RTSSelection` | Box select, control groups |
| `PossessionSwitch` | Swap control (like GTA) |

**Interface: `IEntitySelector`**
- `GetControlledEntities()`
- `SetActiveEntity(entity)`
- `SelectEntities(list)`
- `IssueCommand(entities, command)`

---

### EPIC 19.4 - Visual Mode Expansion

**Goal:** Support non-3D-third-person rendering.

| Mode | Description |
|------|-------------|
| `ThirdPerson3D` | Already have |
| `FirstPerson3D` | Planned in 14.5 |
| `Isometric3D` | Camera + input changes |
| `TopDown3D` | Camera only |
| `SideScroller2D` | Sprite-based equipment |
| `TopDown2D` | Sprite-based, orthographic |
| `TextOnly` | No visuals, state only |
| `VRHands` | Hand-tracked controllers |

**Interface: `IVisualMode`** (extends 14.5's `IViewModeHandler`)
- `GetCameraRig()` - Returns camera setup
- `TransformInput(input)` - Adapt input to camera
- `GetEquipmentRenderer()` - 3D mesh, 2D sprite, or null

---

### EPIC 19.5 - Interaction Mode Abstraction

**Goal:** Control how player interacts with game world.

| Mode | Description |
|------|-------------|
| `DirectControl` | WASD movement (already have) |
| `ClickToMove` | Click ground → pathfind |
| `PointAndClick` | Click object → interact |
| `GridBased` | Move on tiles |
| `CardBased` | Play cards for actions |

**Interface: `IInteractionMode`**
- `HandleWorldClick(position)`
- `GetMovementTarget()`
- `GetInteractionTarget()`
- `IsMovementMode()`

---

### EPIC 19.6 - Game Profile System

**Goal:** Pre-built configurations for common genres.

**GameProfile (ScriptableObject):**

| Field | Type |
|-------|------|
| ProfileName | string |
| InputMode | IInputMode |
| TimeMode | ITimeMode |
| EntitySelector | IEntitySelector |
| TargetingSystem | ITargetingSystem (from 14.7) |
| CombatResolver | ICombatResolver (from 14.8) |
| VisualMode | IVisualMode |
| InteractionMode | IInteractionMode |

**Pre-Built Profiles:**

| Profile | Configuration |
|---------|---------------|
| ActionShooter | Realtime, Single, CameraRaycast, Physics, ThirdPerson, Direct |
| SoulsLike | Realtime, Single, LockOn, Physics, ThirdPerson, Direct |
| DiabloARPG | Realtime, Single, ClickSelect, StatBased, Isometric, ClickToMove |
| TurnBasedRPG | TurnBased, Party, ClickSelect, DiceRoll, TopDown, GridBased |
| ClassicRTS | Realtime, RTS, None, AutoHit, TopDown, ClickToMove |
| PointAndClick | PauseCommands, Single, ClickSelect, AutoHit, Isometric, PointAndClick |
| VRAction | Realtime, Single, None, Physics, VRHands, Direct |

---

## Why Deferred to EPIC 19

| Reason | Explanation |
|--------|-------------|
| DIG doesn't need it | DIG is realtime single-player 3D action |
| ARPG doesn't need most | ARPG is realtime single-player (maybe isometric) |
| Significant complexity | Each mode is substantial work |
| Can retrofit later | 14.7-14.8 interfaces make extension easy |

---

## Estimated Effort

| Sub-EPIC | Effort | Notes |
|----------|--------|-------|
| 19.1 - Input Mode | Medium | Turn-based is complex |
| 19.2 - Time Mode | Medium | Phases need careful design |
| 19.3 - Entity Selection | Medium | Party AI is significant |
| 19.4 - Visual Expansion | Large | 2D requires new renderers |
| 19.5 - Interaction Mode | Medium | Click-to-move needs pathfinding |
| 19.6 - Game Profiles | Small | Just configuration |

**Total:** 3-4 months if fully completed.

---

## Recommended Order

1. **19.6 first** - Create profile system (even if only using 1/2 profiles)
2. **19.4 next** - Visual modes if doing 2D or VR game
3. **19.5 next** - Interaction if doing click-to-move ARPG
4. **19.1 + 19.2** - Only if doing turn-based game
5. **19.3 last** - Only if doing party RPG or RTS

---

## Success Criteria (Full EPIC 19)

- [ ] All 7 interface types functional
- [ ] All pre-built profiles work out-of-box
- [ ] Can switch profile → game feels different
- [ ] Zero code changes for new genre
- [ ] Sample scenes for each major genre
- [ ] Documentation for each mode
