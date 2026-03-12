# EPIC19.6 - Game Profile System

**Status:** Future
**Dependencies:** EPIC19.1-19.5 (All abstractions)
**Goal:** One-click genre configuration via pre-built game profiles.

---

## Overview

With all abstraction layers in place, this EPIC creates the configuration system that ties them together. A Game Profile is a ScriptableObject that configures all game systems for a specific genre.

---

## GameProfile (ScriptableObject)

| Field | Type | Description |
|-------|------|-------------|
| ProfileName | string | Human-readable name |
| ProfileID | string | Machine identifier |
| Description | string | What this profile does |
| Icon | Sprite | For UI selection |

### System References

| Field | Type | Source |
|-------|------|--------|
| InputMode | IInputMode | EPIC 19.1 |
| TimeMode | ITimeMode | EPIC 19.2 |
| EntitySelector | IEntitySelector | EPIC 19.3 |
| TargetingSystem | ITargetingSystem | EPIC 14.7 |
| CombatResolver | ICombatResolver | EPIC 14.8 |
| VisualMode | IVisualMode | EPIC 19.4 |
| InteractionMode | IInteractionMode | EPIC 19.5 |

### Optional Overrides

| Field | Type | Description |
|-------|------|-------------|
| OverrideSlots | List | Custom slot definitions |
| OverrideCategories | List | Custom weapon categories |
| OverrideInputProfile | InputProfileDefinition | Custom inputs |

---

## Pre-Built Profiles

### ActionShooter (DIG Default)

| System | Configuration |
|--------|---------------|
| InputMode | Realtime |
| TimeMode | Realtime |
| EntitySelector | SinglePlayer |
| TargetingSystem | CameraRaycast |
| CombatResolver | PhysicsHitbox |
| VisualMode | ThirdPerson3D |
| InteractionMode | DirectControl |

### SoulsLike

| System | Configuration |
|--------|---------------|
| InputMode | Realtime |
| TimeMode | Realtime |
| EntitySelector | SinglePlayer |
| TargetingSystem | LockOnTarget |
| CombatResolver | PhysicsHitbox |
| VisualMode | ThirdPerson3D |
| InteractionMode | DirectControl |

### DiabloARPG

| System | Configuration |
|--------|---------------|
| InputMode | Realtime |
| TimeMode | Realtime |
| EntitySelector | SinglePlayer |
| TargetingSystem | ClickSelectTarget |
| CombatResolver | StatBasedDirect |
| VisualMode | Isometric3D |
| InteractionMode | ClickToMove |

### TurnBasedRPG

| System | Configuration |
|--------|---------------|
| InputMode | TurnBased |
| TimeMode | TurnBased |
| EntitySelector | PartyControl |
| TargetingSystem | ClickSelectTarget |
| CombatResolver | StatBasedRoll |
| VisualMode | TopDown3D |
| InteractionMode | GridBased |

### TacticsGame

| System | Configuration |
|--------|---------------|
| InputMode | TurnBased |
| TimeMode | ActionPoints |
| EntitySelector | PartyControl |
| TargetingSystem | ClickSelectTarget |
| CombatResolver | StatBasedRoll |
| VisualMode | Isometric3D |
| InteractionMode | GridBased |

### ClassicRTS

| System | Configuration |
|--------|---------------|
| InputMode | Realtime |
| TimeMode | Realtime |
| EntitySelector | RTSSelection |
| TargetingSystem | ClickSelectTarget |
| CombatResolver | StatBasedDirect |
| VisualMode | TopDown3D |
| InteractionMode | ClickToMove |

### PointAndClickAdventure

| System | Configuration |
|--------|---------------|
| InputMode | PauseWithCommands |
| TimeMode | HybridRealtime |
| EntitySelector | SinglePlayer |
| TargetingSystem | ClickSelectTarget |
| CombatResolver | None (no combat) |
| VisualMode | Isometric3D |
| InteractionMode | PointAndClick |

### VRAction

| System | Configuration |
|--------|---------------|
| InputMode | Realtime |
| TimeMode | Realtime |
| EntitySelector | SinglePlayer |
| TargetingSystem | CameraRaycast |
| CombatResolver | PhysicsHitbox |
| VisualMode | VRHands |
| InteractionMode | DirectControl |

### Roguelike2D

| System | Configuration |
|--------|---------------|
| InputMode | TurnBased |
| TimeMode | TurnBased |
| EntitySelector | SinglePlayer |
| TargetingSystem | ClickSelectTarget |
| CombatResolver | StatBasedRoll |
| VisualMode | TopDown2D |
| InteractionMode | GridBased |

---

## Profile Switching

**GameProfileManager:**

| Method | Description |
|--------|-------------|
| `LoadProfile(profile)` | Apply all settings |
| `GetCurrentProfile()` | Active profile |
| `ValidateProfile(profile)` | Check for missing implementations |
| `GetAvailableProfiles()` | List all profiles |

**Switching Flow:**
1. User selects profile in menu
2. System validates all implementations exist
3. All interfaces get new implementations
4. Scene reloads or hot-swaps

---

## Editor Integration

**Profile Editor Window:**
- Visual display of current profile
- Dropdown for each system
- Live preview in Play Mode
- Validation warnings

**Setup Wizard Integration:**
- "What type of game?" → selects profile
- Creates appropriate defaults

---

## Tasks

### Phase 1: Data Structure
- [ ] Create `GameProfile` ScriptableObject
- [ ] Create `GameProfileManager`
- [ ] Create profile validation

### Phase 2: Pre-Built Profiles
- [ ] Create ActionShooter profile
- [ ] Create SoulsLike profile
- [ ] Create DiabloARPG profile
- [ ] Create TurnBasedRPG profile
- [ ] Create all other profiles

### Phase 3: Switching System
- [ ] Profile loading system
- [ ] Hot-swap support (if possible)
- [ ] Scene reload fallback

### Phase 4: Editor UI
- [ ] Profile editor window
- [ ] Integration with Setup Wizard
- [ ] Profile preview

### Phase 5: Documentation
- [ ] Profile selection guide
- [ ] Per-profile setup instructions
- [ ] Customization guide

---

## Verification

- [ ] All pre-built profiles load correctly
- [ ] Switching profiles changes all systems
- [ ] Validation catches missing implementations
- [ ] Editor preview works
- [ ] Custom profiles can be created

---

## Success Criteria

- [ ] Profile switching is one-click
- [ ] All pre-built profiles functional
- [ ] Each profile feels like distinct genre
- [ ] Custom profiles easy to create
- [ ] Documentation complete
