# EPIC19.3 - Entity Selection Abstraction

**Status:** Future
**Dependencies:** EPIC14.5 (Universal architecture)
**Goal:** Control single character, party, or RTS-style multi-unit selection.

---

## Overview

Current system assumes one player entity that is always controlled. This EPIC abstracts entity selection for games where you control multiple characters or units.

---

## Selection Modes

| Mode | Description | Games |
|------|-------------|-------|
| `SinglePlayer` | One entity, always controlled | DIG, Shooters |
| `PartyControl` | Cycle through party, AI for others | Final Fantasy, Mass Effect |
| `RTSSelection` | Box select, control groups | StarCraft, Age of Empires |
| `PossessionSwitch` | Swap control between entities | GTA V, The Quiet Man |

---

## IEntitySelector Interface

| Method | Description |
|--------|-------------|
| `GetActiveEntity()` | Currently controlled entity |
| `GetControlledEntities()` | All entities under player control |
| `GetSelectedEntities()` | Currently selected subset |
| `SetActiveEntity(entity)` | Switch control target |
| `SelectEntities(list)` | Set selection |
| `AddToSelection(entity)` | Add to current selection |
| `ClearSelection()` | Deselect all |
| `IssueCommand(command)` | Order to selected entities |
| `CycleActive(direction)` | Next/previous party member |

---

## Implementations

### SinglePlayerSelector
- One entity, always active
- No selection UI needed
- Already implemented (current behavior)

### PartyControl

| Feature | Description |
|---------|-------------|
| Party List | Up to N controllable characters |
| Active | One is player-controlled |
| AI Companions | Others follow AI behavior |
| Switch | Tab/bumpers to cycle |
| Commands | Can issue orders to companions |

### RTSSelection

| Feature | Description |
|---------|-------------|
| Box Select | Click-drag to select area |
| Click Select | Click individual unit |
| Shift-Add | Add to selection |
| Control Groups | Ctrl+# to save, # to recall |
| Commands | Right-click to order movement/attack |

### PossessionSwitch

| Feature | Description |
|---------|-------------|
| Possess | Take control of entity |
| Release | Return to default character |
| Criteria | Which entities can be possessed |
| Transition | Camera/control blend |

---

## Party AI System

For `PartyControl` mode, non-active party members need AI.

**CompanionAI behaviors:**

| Behavior | Description |
|----------|-------------|
| `FollowActive` | Stay near active character |
| `DefendSelf` | Attack if threatened |
| `SupportActive` | Heal/buff active character |
| `AggressiveEngage` | Actively seek enemies |
| `HoldPosition` | Stay in place |

---

## RTS Command System

For `RTSSelection` mode, need command types.

**RTSCommand enum:**

| Command | Description |
|---------|-------------|
| `Move` | Go to position |
| `AttackMove` | Move, attack enemies on path |
| `Attack` | Attack specific target |
| `Stop` | Halt current action |
| `Hold` | Stop and defend position |
| `Patrol` | Loop between points |

---

## Tasks

### Phase 1: Interface & Data
- [ ] Create `IEntitySelector` interface
- [ ] Create selection component
- [ ] Create control group data

### Phase 2: Implementations
- [ ] Extract `SinglePlayerSelector`
- [ ] Create `PartyControlSelector`
- [ ] Create `RTSSelectionSelector`
- [ ] Create `PossessionSwitchSelector`

### Phase 3: Party System
- [ ] Party roster management
- [ ] Companion AI behaviors
- [ ] Party commands

### Phase 4: RTS System
- [ ] Box selection input
- [ ] Control groups
- [ ] Command queue

### Phase 5: UI
- [ ] Party portraits
- [ ] Selection indicators
- [ ] Control group display
- [ ] Command feedback

---

## Verification

- [ ] Single player works as before
- [ ] Party cycling works
- [ ] Companion AI follows/fights
- [ ] RTS box select works
- [ ] Control groups save/recall
- [ ] Commands execute correctly

---

## Success Criteria

- [ ] Selection mode swappable via config
- [ ] All four modes functional
- [ ] DIG unchanged
- [ ] Party feels like an RPG
- [ ] RTS feels responsive
