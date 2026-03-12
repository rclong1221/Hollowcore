# Setup Guide: EPIC 13.15 (Crouch/HeightChange Parity)

## Overview

This epic brings the Crouch system to feature parity with Opsive's HeightChange ability:
- **Standup Obstruction Check** - Prevents clipping through ceilings
- **Dynamic Collider Resize** - Capsule shrinks/grows with stance
- **Height Animator Parameter** - Drives stance blend trees
- **Block Sprint While Crouched** - Optional sprint prevention
- **Test Environment** - Tunnels, vents, cover walls for testing

---

## Quick Start (For Designers)

### Step 1: Locate the Player Prefab
1. Open Unity Editor
2. Navigate to: `Assets/Prefabs/Player/`
3. Select the **Player prefab** and open in Inspector

### Step 2: Find Crouch Settings
1. Locate the **Locomotion Ability Authoring** component
2. Expand the **Crouch** and **Crouch - 13.15 Height Change** headers

### Step 3: Configure Features

#### Crouch Heights
| Field | Default | Description |
|-------|---------|-------------|
| `CrouchHeight` | 1.0 | Capsule height when crouched |
| `CrouchRadius` | 0.35 | Capsule radius when crouched |
| `CrouchCenter` | (0, 0.5, 0) | Center offset when crouched |
| `StandingHeight` | 1.8 | Original capsule height |

#### Obstruction Settings
| Field | Default | Description |
|-------|---------|-------------|
| `ColliderSpacing` | 0.02 | Skin width for overlap check |

#### Speed Control
| Field | Default | Description |
|-------|---------|-------------|
| `CrouchSpeedMultiplier` | 0.5 | Movement speed when crouched |
| `AllowSprintWhileCrouched` | false | If false, blocks sprint input |

---

## Animation Hookup Guide

### Animator Parameters (Auto-Synced)

| Parameter | Type | Values | Description |
|-----------|------|--------|-------------|
| `Height` | int | 0, 1, 2 | 0 = Standing, 1 = Crouching, 2 = Prone |
| `IsCrouching` | bool | true/false | Currently crouched |
| `IsProne` | bool | true/false | Currently prone |

### Blend Tree Setup
1. Create a blend tree with `Height` as the parameter
2. Add clips: `Stand_Idle`, `Crouch_Idle`, `Prone_Idle`
3. Set thresholds: 0, 1, 2

---

## Component Fields Reference

### CrouchAbility (Runtime State)

| Field | Type | Description |
|-------|------|-------------|
| `IsCrouching` | bool | Currently crouched (replicated) |
| `CrouchPressed` | bool | Input held state |
| `CurrentHeight` | float | Cached current capsule height |
| `OriginalHeight` | float | Stored standing height |
| `OriginalRadius` | float | Stored standing radius |
| `OriginalCenter` | float3 | Stored standing center |
| `StandupBlocked` | bool | True if ceiling prevents standing |

### CrouchSettings (Configurable)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `CrouchHeight` | float | 1.0 | Capsule height when crouched |
| `CrouchRadius` | float | 0.35 | Capsule radius when crouched |
| `CrouchCenter` | float3 | (0, 0.5, 0) | Center offset when crouched |
| `TransitionSpeed` | float | 10 | Speed of height lerp (not yet used) |
| `MovementSpeedMultiplier` | float | 0.5 | Speed modifier when crouched |
| `StandingHeight` | float | 1.8 | Original capsule height |
| `ColliderSpacing` | float | 0.02 | Skin width for overlap checks |
| `AllowSpeedChange` | bool | false | Allow sprint while crouched |

---

## Test Environment

Test objects: `GameObject > DIG - Test Objects > Traversal > Complete Test Course` (Section 16: Crouch Tests)

### Low Ceiling Tunnel (13.15.T1)
- 2m entrance → 1.2m tunnel → 1m alcove → 2m exit
- Tests standup obstruction at varying heights

### Vent Shaft System (13.15.T2)
- 1m x 1m cross-section vents
- Junction, entry grate, low/high ceiling exits
- Must stay crouched throughout

### Crouch Cover Wall (13.15.T3)
- 1m wall (crouch = fully hidden)
- 1.5m wall (standing exposes head)
- Window cutout, shooter spawn point

### Standup Trap (13.15.T4)
- Pressure plate triggers ceiling lowering
- Static layout (runtime animation deferred)

### Collider Visualization (13.15.T5)
- Height markers: 1.8m (standing), 1.0m (crouch), 0.4m (prone)
- 1.5m test ceiling (crouch only)

---

## Verification

### Standup Obstruction
1. Enter Play Mode
2. Crouch under a 1.2m ceiling
3. Release crouch input
4. ✅ Player stays crouched (cannot stand)
5. Move to 2m ceiling area
6. Release crouch
7. ✅ Player stands up normally

### Collider Resize
1. Crouch near a 1.5m ceiling
2. ✅ Player can walk under ceiling while crouched
3. Stand up in open area
4. Walk toward 1.5m ceiling
5. ✅ Player is blocked (collider is taller now)

### Sprint Block
1. Set `AllowSprintWhileCrouched = false`
2. Crouch and hold sprint
3. ✅ Player moves at crouch speed (no sprint)
4. Set `AllowSprintWhileCrouched = true`
5. ✅ Player can sprint while crouched

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Player clips through ceiling | Check `ColliderSpacing` > 0 and `StandingHeight` is correct |
| Can stand under low ceiling | Verify DOTS Physics `PhysicsWorldSingleton` exists |
| Collider not shrinking | Check `PlayerColliderHeightSystem` is running |
| Sprint works while crouched | Ensure `AllowSpeedChange = false` in CrouchSettings |
| `StandupBlocked` not updating | Raycast may be hitting player's own collider (check filter) |

---

## Files Reference

### Systems
| File | Description |
|------|-------------|
| `CrouchSystem.cs` | Stance toggle, obstruction check |
| `SprintSystem.cs` | Sprint blocking logic |
| `PlayerColliderHeightSystem.cs` | Dynamic capsule resize |
| `PlayerAnimationStateSystem.cs` | Height animator parameter |

### Components
| File | Description |
|------|-------------|
| `LocomotionComponents.cs` | CrouchAbility, CrouchSettings |
| `PlayerAnimationStateComponent.cs` | Height field |

### Authoring
| File | Description |
|------|-------------|
| `LocomotionAbilityAuthoring.cs` | Inspector configuration |

### Editor
| File | Description |
|------|-------------|
| `TraversalObjectCreator.cs` | Test environment objects |
