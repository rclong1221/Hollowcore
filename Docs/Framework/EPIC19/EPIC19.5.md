# EPIC19.5 - Interaction Mode Abstraction

**Status:** Future
**Dependencies:** EPIC14.7 (Targeting), EPIC19.4 (Visual)
**Goal:** Control how player interacts with game world beyond direct WASD control.

---

## Overview

Current system assumes WASD direct-control movement. This EPIC abstracts interaction to support click-to-move, point-and-click adventure, and grid-based movement.

---

## Interaction Modes

| Mode | Description | Games |
|------|-------------|-------|
| `DirectControl` | WASD movement | DIG, Shooters |
| `ClickToMove` | Click ground → pathfind | Diablo, League |
| `PointAndClick` | Click object → interact | Adventure games |
| `GridBased` | Move on tiles | Tactics, Roguelikes |
| `CardBased` | Play cards for actions | Slay the Spire |

---

## IInteractionMode Interface

| Method | Description |
|--------|-------------|
| `HandleWorldClick(hitInfo)` | Process click on world |
| `GetMovementTarget()` | Where to move to |
| `GetInteractionTarget()` | What to interact with |
| `IsMovementMode()` | Currently moving? |
| `IsInteractionMode()` | Currently interacting? |
| `CancelInteraction()` | Stop current action |
| `GetValidActions(target)` | What can do to target |

---

## Implementations

### DirectControl
- WASD/stick controls movement
- Already implemented (current behavior)
- No pathfinding needed

### ClickToMove

| Step | Description |
|------|-------------|
| 1 | Click on ground |
| 2 | Calculate path (NavMesh or A*) |
| 3 | Move along path |
| 4 | Click elsewhere to change |
| 5 | Click enemy to auto-attack |

### PointAndClick

| Step | Description |
|------|-------------|
| 1 | Click on object/character |
| 2 | Show action menu (Look, Use, Talk) |
| 3 | Select action |
| 4 | Character walks to object |
| 5 | Perform action |

### GridBased

| Feature | Description |
|---------|-------------|
| Grid | World divided into tiles |
| Movement | Cell-to-cell, costs vary |
| Actions | Per-tile, discrete |
| Range | Highlighted cells for abilities |

### CardBased

| Feature | Description |
|---------|-------------|
| Hand | Cards available this turn |
| Play | Drag card to target |
| Effects | Card determines action |
| Draw | New cards each turn |

---

## Pathfinding Integration

For `ClickToMove` and `PointAndClick`:

| System | Description |
|--------|-------------|
| NavMesh | Unity's built-in (3D) |
| A* Pathfinding | Custom or A* Pro asset |
| Grid Path | For grid-based |
| None | For direct control |

---

## Action Context Menus

For `PointAndClick` mode:

**ContextAction struct:**

| Field | Type | Description |
|-------|------|-------------|
| ActionName | string | "Look", "Use", "Talk" |
| IconSprite | Sprite | UI icon |
| IsAvailable | bool | Greyed if unavailable |
| Handler | Action | What to execute |

---

## Grid System

For `GridBased` mode:

**GridTile component:**

| Field | Type | Description |
|-------|------|-------------|
| Position | int2 | Grid coordinates |
| MovementCost | int | Cost to enter |
| IsWalkable | bool | Can enter? |
| OccupyingEntity | Entity | Who's here |
| TerrainType | enum | Grass, Water, etc. |

---

## Tasks

### Phase 1: Interface & Data
- [ ] Create `IInteractionMode` interface
- [ ] Create `ContextAction` struct
- [ ] Create `GridTile` component

### Phase 2: Movement Implementations
- [ ] Extract `DirectControlMode`
- [ ] Create `ClickToMoveMode`
- [ ] Create `GridBasedMode`

### Phase 3: Adventure Implementations
- [ ] Create `PointAndClickMode`
- [ ] Create action context menu system
- [ ] Create interaction hotspots

### Phase 4: Card System
- [ ] Create `CardBasedMode`
- [ ] Card hand management
- [ ] Card drag-and-drop

### Phase 5: Pathfinding
- [ ] Integrate NavMesh for 3D
- [ ] Create grid pathfinder
- [ ] Path visualization

---

## Verification

- [ ] Direct control works as before
- [ ] Click-to-move paths correctly
- [ ] Point-and-click shows menu
- [ ] Grid movement respects tiles
- [ ] Cards can be played

---

## Success Criteria

- [ ] Interaction mode swappable via config
- [ ] All five modes functional
- [ ] DIG unchanged
- [ ] Click-to-move feels fluid
- [ ] Adventure game interactions work
