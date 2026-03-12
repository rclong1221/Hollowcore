# EPIC 15.20: Input Paradigm Framework

**Status:** Task Planning (PR [#393](https://github.com/rclong1221/DIG/pull/393)), A* Architecture (PR [#394](https://github.com/rclong1221/DIG/pull/394))
**Priority:** Low (Future Architecture)
**Dependencies:**
- ✅ EPIC 15.18 Tier 1 (HybridToggle) - Complete
- ✅ Screen-Relative Movement (Isometric WASD) - Complete
- ✅ Cursor-Free Mode (CursorController) - Complete
- ✅ Auto Camera Mode Switching - Complete
- ✅ EPIC 15.21 Input Action Layer - Complete (PR [#392](https://github.com/rclong1221/DIG/pull/392))
- ⏸️ EPIC 15.18 Tier 2 (TacticalCursor) - Deferred
- ⚠️ Click-to-Move - Not implemented
- ⚠️ Navmesh Pathfinding - Included (A* v5.4.6) — **Requires bridge layer** (PhysX↔Unity Physics mismatch, managed↔ECS boundary, no NetCode support)

**Feature:** Comprehensive Input/Camera/Movement Paradigm System

---

## Overview

DIG currently supports Shooter/Souls-style input (EPIC 15.18 Tier 1). This epic defines a complete framework for supporting **all major real-time action game input paradigms**, enabling the game to switch between fundamentally different control schemes based on gameplay context, camera mode, or player preference.

### The Core Question

Every action game must answer three fundamental questions:

1. **What does the mouse control?** (Camera rotation, cursor position, aim direction)
2. **How does the player move?** (WASD direct, click-to-move, or both)
3. **What does the character face?** (Camera direction, movement direction, cursor/target)

The combination of answers defines the **input paradigm**.

---

## Paradigm Definitions

### Core Combat Paradigms (5)

These are mutually exclusive base paradigms. Only one can be active at a time.

| # | Paradigm | Camera | Mouse Role | Move Input | Facing |
|---|----------|--------|------------|------------|--------|
| 1 | **Shooter/Souls** | TPS/FPS Orbit | Camera Control | WASD | Camera |
| 2 | **MMO/RPG** | TPS (RMB Orbit) | Free Cursor | WASD | Move Dir / Camera |
| 3 | **ARPG** | Isometric Fixed | Free Cursor | Click | Destination |
| 4 | **MOBA** | Top-Down | Free Cursor | Click (RMB) | Destination |
| 5 | **Twin-Stick** | Isometric/Follow | Aim Direction | WASD | Cursor |

### Alternate Dimension Paradigm (1)

| # | Paradigm | Camera | Mouse Role | Move Input | Facing |
|---|----------|--------|------------|------------|--------|
| 6 | **2D Side-Scroller** | Side-View | None or Aim | A/D + Jump | Move Dir / Cursor |

### Context Mode Overlays (2)

These temporarily modify any base paradigm while active.

| # | Mode | Modifies | Purpose |
|---|------|----------|---------|
| 7 | **Vehicle/Mount** | Movement + Camera | Different physics, momentum, no strafe |
| 8 | **Build/Placement** | Cursor + Actions | Ghost object positioning |

---

## Paradigm 1: Shooter/Souls

**Games:** Dark Souls, Elden Ring, Monster Hunter, Resident Evil 4+, TPS Shooters

**Current Implementation:** ✅ Complete (ShooterDirect + HybridToggle)

### Characteristics
- Mouse delta always rotates camera
- Cursor locked and hidden
- Crosshair at screen center
- WASD movement relative to camera facing
- Character always faces camera forward direction
- A/D = strafe (always)
- Lock-on is explicit toggle (Tab/MMB)
- Dodge/roll with I-frames

### Control Mapping

| Control | Action |
|---------|--------|
| **W** | Move forward (camera direction) |
| **S** | Move backward |
| **A** | Strafe left |
| **D** | Strafe right |
| **Mouse** | Camera orbit (always) |
| **LMB** | Attack / Shoot (at crosshair) |
| **RMB** | Block / Aim / Secondary |
| **Space** | Dodge / Roll / Jump |
| **Shift** | Sprint |
| **Tab / MMB** | Lock-on toggle |
| **Alt (hold)** | Free cursor temporarily (HybridToggle) |

### Character Facing

| Situation | Faces |
|-----------|-------|
| Idle | Camera direction |
| Moving | Camera direction |
| Attacking | Camera direction |
| Lock-on | Locked target |

### Compatible Cameras
- ThirdPersonFollow (orbit)
- FirstPerson (orbit)
- ShoulderCam (orbit)

---

## Paradigm 2: MMO/RPG

**Games:** World of Warcraft, Final Fantasy XIV, Elder Scrolls Online, Guild Wars 2

**Current Implementation:** ✅ Complete (cursor-free, movement-direction facing, RMB orbit, A/D toggle all functional)

### Characteristics
- Cursor free by default (can click UI, targets)
- RMB hold → camera orbit, cursor hides
- LMB hold → character turn (yaw)
- Both buttons → auto-run forward
- Click-to-select targets (not click-to-move)
- Tab-target cycling
- A/D switch between TURN and STRAFE based on RMB state

### Control Mapping

| Control | Default | RMB Held | LMB Held | Both Held |
|---------|---------|----------|----------|-----------|
| **W** | Forward (char dir) | Forward (cam dir) | Forward | Auto-run |
| **S** | Backward | Backpedal | Backpedal | - |
| **A** | Turn left | Strafe left | Turn left | Strafe left |
| **D** | Turn right | Strafe right | Turn right | Strafe right |
| **Mouse** | Move cursor | Camera orbit | Char turn | Camera orbit |
| **Cursor** | Visible | Hidden | Hidden | Hidden |
| **LMB Click** | Select target | - | - | - |
| **RMB Click** | Context menu | - | - | - |

### Character Facing

| Situation | Faces |
|-----------|-------|
| Idle | Last movement direction |
| Moving (no buttons) | Movement direction |
| Moving (RMB held) | Camera direction |
| Moving (LMB held) | Turns with mouse yaw |
| Attacking | Target (if tab-targeted) |

### Compatible Cameras
- ThirdPersonFollow (orbit via RMB)
- Orbital cameras with toggle control

### Implementation Requirements

| Feature | Status |
|---------|--------|
| Cursor free by default | ✅ Have (CursorController) |
| RMB → camera orbit + strafe mode | ✅ Have |
| LMB → character turn | ⏸️ Blocked on [EPIC 15.21](EPIC15.21.md) |
| Both buttons → auto-run | ⏸️ Blocked on [EPIC 15.21](EPIC15.21.md) |
| A/D turn vs strafe toggle | ✅ Have |
| Movement-direction facing | ✅ Have (screen-relative mode) |

---

## Paradigm 3: ARPG (Diablo-style)

**Games:** Diablo 2/3/4, Path of Exile, Grim Dawn, Last Epoch, Torchlight

**Current Implementation:** ⚠️ Partial (WASD hybrid with screen-relative movement; click-to-move not implemented)

### Characteristics
- Fixed isometric camera
- Cursor always visible
- LMB click on ground = move to location (pathfinding)
- LMB click on enemy = attack (move into range first)
- Hold LMB = continuous move-to-cursor
- RMB / 1-4 = abilities aimed at cursor
- WASD = optional direct movement (Diablo 4 style)
- Shift = stand still (attack in place)
- Q/E = camera rotation (if rotatable)

### Control Mapping

| Control | Classic (D2/D3/PoE) | Hybrid (D4/Last Epoch) |
|---------|---------------------|------------------------|
| **LMB Click (ground)** | Move to location | Move to location |
| **LMB Click (enemy)** | Attack (auto-approach) | Attack (auto-approach) |
| **LMB Hold** | Continuous move | Continuous move |
| **RMB** | Skill slot | Skill slot |
| **1-4 / QWER** | Skill slots | Skill slots |
| **W** | - | Move forward (cam) |
| **A** | - | Move left |
| **S** | - | Move backward |
| **D** | - | Move right |
| **Shift** | Stand still | Stand still |
| **Space** | - | Dodge / Evade |
| **Scroll** | - | Zoom (some games) |

### Character Facing

| Situation | Faces |
|-----------|-------|
| Idle | Last action direction |
| Moving | Move destination |
| Attacking | Target |
| Casting | Cursor (for ground-target abilities) |

### Camera Control

| Control | Action |
|---------|--------|
| **Q/E** | Rotate camera (if rotatable) |
| **Scroll** | Zoom in/out |
| **MMB drag** | Rotate camera (some games) |
| **Camera** | Fixed angle, follows player |

### Compatible Cameras
- IsometricFixed
- IsometricRotatable
- TopDownFixed (higher angle)

### Implementation Requirements

| Feature | Status |
|---------|--------|
| Isometric cameras | ✅ Have |
| Cursor-to-world projection | ✅ Have |
| Q/E camera rotation | ✅ Have |
| Click-to-move | ❌ Need |
| Navmesh pathfinding | ❌ Need |
| Hold-to-move-continuously | ❌ Need |
| Move-to-target-then-attack | ❌ Need |
| WASD direct movement | ✅ Have (screen-relative) |
| Ground indicator | ❌ Need |
| Shift = stand still | ❌ Need |
| Character faces move direction | ✅ Have (EPIC 15.20) |
| Cursor visible | ✅ Have (CursorController) |

---

## Paradigm 4: MOBA

**Games:** League of Legends, Dota 2, Heroes of the Storm

**Current Implementation:** ❌ Not Implemented

### Characteristics
- Fixed top-down camera (higher angle than ARPG)
- Cursor always visible
- RMB click = move (opposite of ARPG's LMB)
- LMB = select (units, UI)
- QWER = abilities aimed at cursor
- A+click = attack-move (attack enemies en route)
- Edge-pan or drag-scroll camera
- Spacebar = snap camera to champion
- Y = toggle camera lock

### Control Mapping

| Control | Action |
|---------|--------|
| **RMB Click (ground)** | Move to location |
| **RMB Click (enemy)** | Attack-move to enemy |
| **LMB Click** | Select / UI |
| **A + Click** | Attack-move (attack anything on path) |
| **S** | Stop / Hold position |
| **H** | Hold (stop + maintain position) |
| **Q/W/E/R** | Abilities |
| **Space** | Center camera on champion |
| **Y** | Toggle camera lock |
| **F1-F5** | Select allied champions |
| **Edge of screen** | Pan camera |
| **MMB drag** | Drag-pan camera |

### Character Facing

| Situation | Faces |
|-----------|-------|
| Idle | Last action direction |
| Moving | Move destination |
| Attacking | Target |
| Casting | Varies (target or cursor) |

### Compatible Cameras
- TopDownFixed
- TopDownUnlocked (with edge-pan)

### Implementation Requirements

| Feature | Status |
|---------|--------|
| Top-down camera | ✅ Have |
| Click-to-move (RMB) | ❌ Need |
| Attack-move command | ❌ Need |
| Edge-pan camera | ❌ Need |
| Camera lock toggle | ❌ Need |
| Spacebar snap-to-player | ❌ Need |
| Stop command | ❌ Need |
| Unit selection | ❌ Need (if pets/minions) |

---

## Paradigm 5: Twin-Stick / WASD+Aim

**Games:** Hades, Shape of Dreams, Enter the Gungeon, Vampire Survivors, Risk of Rain 2

**Current Implementation:** ⚠️ Partial (WASD screen-relative works; cursor-aim attacks not implemented)

### Characteristics
- Move and aim are completely independent axes
- WASD = movement (world or camera relative)
- Mouse = aim direction (cursor stays on screen)
- LMB = primary attack toward cursor
- RMB / 1-9 = abilities toward cursor
- Character always faces cursor position
- No click-to-move
- Fixed isometric or auto-follow camera

### Control Mapping

| Control | Action |
|---------|--------|
| **W** | Move forward/up (world direction) |
| **S** | Move backward/down |
| **A** | Move left |
| **D** | Move right |
| **Mouse** | Aim direction (cursor visible) |
| **LMB** | Primary attack toward cursor |
| **RMB** | Secondary / Dash / Special |
| **1-9 / QERF** | Abilities (toward cursor) |
| **Space** | Dash / Dodge |
| **Shift** | Varies |

### Character Facing

| Situation | Faces |
|-----------|-------|
| Idle | Cursor direction |
| Moving | Cursor direction (or blend) |
| Attacking | Cursor direction |
| Always | Cursor direction |

### Variants

| Subtype | Examples | Difference |
|---------|----------|------------|
| **Pure twin-stick** | Binding of Isaac, Gungeon | Constant fire option |
| **Action roguelike** | Hades, Shape of Dreams | Ability combos, dash |
| **Horde survivor** | Vampire Survivors | Auto-aim, WASD only |
| **3D twin-stick** | Risk of Rain 2, Remnant | Over-shoulder but cursor aim |

### Compatible Cameras
- IsometricFixed (auto-follow)
- IsometricRotatable
- FollowCam with fixed angle

### Implementation Requirements

| Feature | Status |
|---------|--------|
| WASD direct movement | ✅ Have (screen-relative) |
| Isometric cameras | ✅ Have |
| Cursor-to-world projection | ✅ Have |
| Cursor always visible | ✅ Have (CursorController) |
| Character faces move direction | ✅ Have (default) |
| Character faces cursor (optional) | ⏸️ Deferred (Hotline Miami style) |
| Attacks aim at cursor | ❌ Need |
| Aim indicator/reticle | ❌ Need |

---

## Paradigm 6: 2D Side-Scroller

**Games:** Hollow Knight, Dead Cells, Celeste, Terraria, Metroidvanias

**Current Implementation:** ❌ Not Implemented (3D game, but included for completeness)

### Characteristics
- Movement locked to 2D plane
- A/D = move left/right
- W/S = look up/down, climb, or aim vertical
- Space = jump
- Mouse = none (platformer) or 360° aim (2D shooter)
- Camera follows player horizontally with vertical tracking

### Control Mapping

| Control | Pure Platformer | 2D Twin-Stick |
|---------|-----------------|---------------|
| **A** | Move left | Move left |
| **D** | Move right | Move right |
| **W** | Look up / Climb | Aim up |
| **S** | Crouch / Drop | Aim down |
| **Space** | Jump | Jump |
| **Mouse** | - | Aim 360° |
| **LMB** | - | Attack toward cursor |
| **RMB** | - | Secondary ability |

### Character Facing

| Subtype | Faces |
|---------|-------|
| Platformer | Last move direction (left/right) |
| 2D Twin-Stick | Cursor direction (360°) |

---

## Context Mode 7: Vehicle/Mount

**Modifies:** Any base paradigm
**Current Implementation:** ❌ Not Implemented

### Characteristics
- Replaces character movement with vehicle physics
- Forward/backward = accelerate/brake
- A/D = turn or roll (not strafe)
- Momentum and inertia affect movement
- May limit camera angles
- Sprint becomes boost

### Control Mapping Changes

| Control | On Foot | Vehicle/Mount |
|---------|---------|---------------|
| **W** | Move forward | Accelerate |
| **S** | Move backward | Brake / Reverse |
| **A** | Strafe left | Turn left / Roll left |
| **D** | Strafe right | Turn right / Roll right |
| **Space** | Jump / Dodge | Boost / Brake / Jump |
| **Shift** | Sprint | Turbo / Afterburner |
| **Strafe** | Yes | Usually no |
| **Momentum** | Instant stop | Inertia-based |

### Vehicle Subtypes

| Type | Examples | Handling |
|------|----------|----------|
| **Ground mount** | Horse, Motorcycle | Forward + Turn |
| **Ground vehicle** | Car, Tank | Forward + Turn + Reverse |
| **Flying free** | Helicopter, Jetpack | 6DOF (pitch/yaw/roll) |
| **Flying arcade** | Plane, Dragon | Forward only, pitch/yaw |
| **Mech/Walker** | Titanfall, Armored Core | Hybrid (may strafe) |
| **Water** | Boat, Submarine | Similar to flying |

---

## Context Mode 8: Build/Placement

**Modifies:** Any base paradigm
**Current Implementation:** ❌ Not Implemented

### Characteristics
- Cursor controls ghost object position
- LMB = place / confirm
- RMB = cancel / rotate
- May grid-snap positioning
- Movement usually still works
- Exit mode to return to combat

### Control Mapping Changes

| Control | Normal | Build Mode |
|---------|--------|------------|
| **Mouse** | Paradigm default | Position ghost object |
| **LMB** | Attack | Place / Confirm |
| **RMB** | Secondary | Cancel / Rotate 90° |
| **R / Scroll** | Reload / Zoom | Rotate piece |
| **Q/E** | Abilities | Cycle building type |
| **WASD** | Move | Move (usually same) |
| **Cursor** | Paradigm default | Visible + placement preview |
| **Tab / B** | - | Exit build mode |

---

## Comparison Matrices

### Mouse Role by Paradigm

| Paradigm | LMB Tap | LMB Hold | RMB Tap | RMB Hold | Move |
|----------|---------|----------|---------|----------|------|
| **Shooter** | Attack | Auto-fire | Block/Aim | ADS | Camera |
| **MMO** | Select | Turn char | Context | Orbit | Cursor |
| **ARPG** | Move/Attack | Move cont. | Ability | - | Cursor |
| **MOBA** | Select | Drag-select | Move | - | Cursor |
| **Twin-Stick** | Attack | Auto-fire | Special | - | Aim dir |
| **2D Platformer** | - | - | - | - | None |
| **2D Twin-Stick** | Attack | Auto-fire | Special | - | Aim dir |

### WASD by Paradigm

| Key | Shooter | MMO (default) | MMO (RMB) | ARPG | MOBA | Twin-Stick |
|-----|---------|---------------|-----------|------|------|------------|
| **W** | Forward (cam) | Forward (char) | Forward (cam) | Optional | - | Up |
| **S** | Backward | Backward | Backpedal | Optional | Stop | Down |
| **A** | Strafe L | Turn L | Strafe L | Optional | - | Left |
| **D** | Strafe R | Turn R | Strafe R | Optional | - | Right |
| **Used?** | Primary | Primary | Primary | Optional | No | Primary |

### Character Facing by Paradigm

| Situation | Shooter | MMO | ARPG | MOBA | Twin-Stick |
|-----------|---------|-----|------|------|------------|
| **Idle** | Camera | Last move | Last action | Last move | Cursor |
| **Moving** | Camera | Move dir | Destination | Destination | Cursor |
| **Attacking** | Camera | Target | Target | Target | Cursor |
| **Special** | Lock-on: Target | RMB: Camera | - | - | - |

### Movement Type by Paradigm

| | Shooter | MMO | ARPG | MOBA | Twin-Stick |
|-|---------|-----|------|------|------------|
| **Primary** | WASD | WASD | Click | Click | WASD |
| **Pathfinding** | No | No | Yes | Yes | No |
| **Click-to-move** | No | No | Yes | Yes | No |
| **Direct input** | Yes | Yes | Optional | No | Yes |

### Component Requirements

| Component | Shooter | MMO | ARPG | MOBA | Twin-Stick |
|-----------|:-------:|:---:|:----:|:----:|:----------:|
| WASD Direct Movement | ✅ | ✅ | ⚪ | ❌ | ✅ |
| Click-to-Move | ❌ | ❌ | ✅ | ✅ | ❌ |
| Navmesh Pathfinding | ❌ | ❌ | ✅ | ✅ | ❌ |
| Mouse Camera Orbit | ✅ | ⚪¹ | ❌ | ❌ | ❌ |
| RMB Camera Orbit | ❌ | ✅ | ❌ | ❌ | ❌ |
| Mouse = Aim Direction | ❌ | ❌ | ❌ | ❌ | ✅ |
| Cursor Free by Default | ❌ | ✅ | ✅ | ✅ | ✅ |
| Q/E Camera Rotation | ❌ | ❌ | ✅ | ❌ | ❌ |
| Edge-Pan Camera | ❌ | ❌ | ❌ | ✅ | ❌ |
| Lock-On System | ✅ | ⚪ | ❌ | ❌ | ❌ |
| Tab-Target | ⚪ | ✅ | ❌ | ❌ | ❌ |
| A/D Turn vs Strafe | ❌ | ✅ | ❌ | ❌ | ❌ |
| Movement-Dir Facing | ❌ | ✅ | ✅ | ✅ | ❌ |
| Cursor-Dir Facing | ❌ | ❌ | ❌ | ❌ | ✅ |
| Ground Indicator | ❌ | ❌ | ✅ | ✅ | ⚪ |
| Attack-Move Command | ❌ | ❌ | ⚪ | ✅ | ❌ |

✅ = Required | ⚪ = Optional/Partial | ❌ = Not Used
¹ Only when RMB held

---

## Architecture

### Input Context Stack

```
┌─────────────────────────────────────────────────────────────────┐
│                    Input Context Stack                          │
├─────────────────────────────────────────────────────────────────┤
│  TOP: UI Menu (pauses/overlays everything)                      │
├─────────────────────────────────────────────────────────────────┤
│  Mode Overlay: Build Mode | Vehicle Mode | None                 │
├─────────────────────────────────────────────────────────────────┤
│  Base Paradigm: Shooter | MMO | ARPG | MOBA | TwinStick | 2D    │
├─────────────────────────────────────────────────────────────────┤
│  World: Gameplay Active                                         │
└─────────────────────────────────────────────────────────────────┘
```

### Proposed Input Scheme Enum

```csharp
public enum InputScheme : byte
{
    // Tier 1 - Implemented
    ShooterDirect = 0,    // Mouse = camera (always)
    HybridToggle = 1,     // Mouse = camera, Alt = free cursor
    
    // Tier 2 - Defined
    TacticalCursor = 2,   // Mouse = cursor, click-to-move (ARPG/MOBA)
    
    // Tier 3 - Proposed
    MMOCursor = 3,        // Mouse = cursor, RMB = camera
    TwinStickAim = 4,     // Mouse = aim direction, WASD = move
    SideScroller2D = 5,   // A/D move, optional mouse aim
}

public enum InputModeOverlay : byte
{
    None = 0,
    VehicleMount = 1,
    BuildPlacement = 2,
}
```

### Movement Facing Mode

```csharp
public enum MovementFacingMode : byte
{
    CameraForward,      // Always face camera direction (Shooter)
    MovementDirection,  // Face movement direction (MMO, ARPG)
    CursorDirection,    // Always face cursor (Twin-Stick)
    TargetLocked,       // Face locked target (Souls lock-on)
    ManualTurn,         // Only turn via explicit input
}
```

### Input Scheme Profile (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "DIG/Input/Input Scheme Profile")]
public class InputSchemeProfile : ScriptableObject
{
    [Header("Identity")]
    public InputScheme schemeType;
    public string displayName;
    
    [Header("Cursor Behavior")]
    public bool cursorFreeByDefault;
    public KeyCode temporaryCursorFreeKey;   // Alt for Shooter
    public MouseButton cameraOrbitButton;     // RMB for MMO, None for Shooter
    
    [Header("Mouse Button Actions")]
    public MouseButtonAction leftButton;
    public MouseButtonAction rightButton;
    public MouseButtonAction middleButton;
    
    [Header("Movement")]
    public bool wasdEnabled;
    public bool clickToMoveEnabled;
    public MouseButton clickToMoveButton;     // LMB for ARPG, RMB for MOBA
    public MovementFacingMode facingMode;
    public bool adIsTurnByDefault;            // MMO: A/D turn, RMB makes strafe
    
    [Header("Camera")]
    public CameraControlMode cameraMode;
    public bool qeRotationEnabled;
    public bool edgePanEnabled;
    public bool scrollZoomEnabled;
    
    [Header("Compatible Cameras")]
    public CameraType[] compatibleCameraTypes;
}
```

---

## Current Implementation Status

| Paradigm | Scheme | Status |
|----------|--------|--------|
| Shooter/Souls | ShooterDirect | ✅ Complete |
| Shooter + Cursor | HybridToggle | ✅ Complete |
| ARPG | TacticalCursor | ⏸️ Defined, not implemented |
| MOBA | TacticalCursor + config | ❌ Not implemented |
| MMO/RPG | MMOCursor | ✅ Complete |
| Twin-Stick | TwinStickAim | ❌ Not implemented |
| 2D Side-Scroller | SideScroller2D | ❌ Not implemented |
| Vehicle/Mount | VehicleOverlay | ❌ Not implemented |
| Build/Placement | BuildOverlay | ❌ Not implemented |

### What Exists

| Component | Status |
|-----------|--------|
| InputSchemeManager | ✅ Have |
| CursorHoverSystem | ✅ Have |
| CursorClickTargetSystem | ✅ Have |
| CursorAimTargeting | ✅ Have |
| ThirdPersonFollowCamera | ✅ Have |
| IsometricFixedCamera | ✅ Have |
| IsometricRotatableCamera | ✅ Have |
| TopDownFixedCamera | ✅ Have |
| Q/E camera rotation | ✅ Have |
| Lock-on system | ✅ Have |

### What's Needed

| Component | For Paradigms |
|-----------|---------------|
| Click-to-Move System | ARPG, MOBA |
| Navmesh Player Pathfinding | ARPG, MOBA |
| RMB-to-Orbit Camera | MMO | ✅ Have |
| A/D Turn vs Strafe Toggle | MMO | ✅ Have |
| Movement-Direction Facing | MMO, ARPG, MOBA |
| Cursor-Direction Facing | Twin-Stick |
| Attacks Aim at Cursor | Twin-Stick |
| Edge-Pan Camera | MOBA |
| Camera Lock Toggle | MOBA |
| Attack-Move Command | MOBA |
| Ground Indicator Prefab | ARPG, MOBA |
| Vehicle Physics Controller | Vehicle mode |
| Build Ghost Placement | Build mode |

---

## Implementation Tasks

### Phase 1: Core Framework (Completed) ✅
- [x] **Framework Architecture**
  - [x] Create `IInputParadigmProvider` interface
  - [x] Implement `ParadigmStateMachine` for atomic transitions
  - [x] Implement `ParadigmInputManager` for Input System map switching
  - [x] Create `InputParadigmProfile` ScriptableObject definition
- [x] **Basic Integration**
  - [x] Integrate with `CameraModeProvider`
  - [x] Sync state to ECS (`InputParadigmState` component)
  - [x] Update `CursorController` to listen for paradigm changes

### Phase 2: Shooter & MMO Paradigms (Tier 1 & 3) (Completed) ✅
- [x] **Shooter/Souls Paradigm**
  - [x] Implement `ShooterDirect` profile (Camera-relative move, always-orbit)
  - [x] Implement `HybridToggle` profile (Alt key cursor toggle)
- [x] **MMO/RPG Paradigm**
  - [x] Implement `MMOCursor` profile (RMB orbit, click select)
  - [x] Implement RMB-hold logic for camera orbit + strafing
  - [x] Implement A/D toggling between Turn (standard) and Strafe (RMB held)
  - [x] Ensure character faces movement direction (when not strafing)

### Phase 3: Isometric Foundation — Click-to-Move & Pathfinding (Tier 2) 🚧

> **Architecture Decision: Bridge/Middleware Layer**
>
> The A* Pathfinding Project (v5.4.6, `com.arongranberg.astar`) is already installed. However,
> it **does not work out of the box** with our DOTS/ECS architecture. There are three
> fundamental incompatibilities that require a bridge layer approach:
>
> | Incompatibility | DIG | A* Package |
> |-----------------|-----|------------|
> | **Physics Engine** | `com.unity.physics` 1.4.4 (ECS) | `UnityEngine.Physics` (PhysX) |
> | **Graph & Path Requests** | Burst/ISystem pipeline | Managed C# (`AstarPath` MonoBehaviour, `Seeker`, `ABPath`) |
> | **Networking** | NetCode prediction in `PredictedFixedStepSimulationSystemGroup` | No NetCode awareness, runs in own `AIMovementSystemGroup` |
>
> **Impact:** A*'s graph scanning uses PhysX raycasts that cannot see our Unity Physics
> colliders. Its `FollowerEntity` movement systems would bypass our `CharacterControllerSystem`
> and desync from NetCode prediction. Path requests go through managed code incompatible with
> Burst-compiled systems.
>
> **Solution:** Use A* **only for path calculation** (graph + algorithm). All movement stays
> in our existing ECS pipeline. A thin managed bridge marshals path results into ECS
> `DynamicBuffer<PathWaypoint>` components that our Burst-compiled systems consume.
> `FollowerEntity` and its 18 ECS systems are **not used** for player movement.

#### 3a. A* Graph Configuration (Managed Side)

- [ ] **Static PhysX Collider Mirror for Graph Scanning**
  - [ ] Create a dedicated PhysX physics scene (or layer) mirroring terrain/obstacle geometry
  - [ ] A*'s `RecastGraph` scans this PhysX representation to build the navmesh
  - [ ] Document the sync strategy: static geometry baked at level load, dynamic obstacles
        update the graph via `GraphUpdateObject` when ECS collision geometry changes
  - [ ] Verify graph scanning produces valid navmesh over game terrain
- [ ] **Configure `AstarPath` MonoBehaviour**
  - [ ] Add `AstarPath` component to a persistent scene GameObject
  - [ ] Configure `RecastGraph` settings (cell size, agent radius, walkable slope, tile size)
  - [ ] Test navmesh visualization in Scene view against actual terrain
- [ ] **Graph Update Strategy for Dynamic Obstacles**
  - [ ] Define which ECS obstacle types need PhysX mirror colliders
  - [ ] Create `NavmeshObstacleSyncSystem` — when ECS obstacles spawn/move/despawn,
        update their PhysX mirror collider and trigger `AstarPath.UpdateGraphs(bounds)`

#### 3b. Path Request Bridge (Managed ↔ ECS Boundary)

- [ ] **Create `PathRequestService`** (MonoBehaviour)
  - [ ] Exposes `RequestPath(float3 start, float3 end, Entity requester)` API
  - [ ] Internally calls `ABPath.Construct()` + `AstarPath.StartPath()` (A*'s managed API)
  - [ ] On path completion callback, converts `List<Vector3>` waypoints to
        `NativeArray<float3>` and writes to a `NativeQueue<PathResult>` for ECS consumption
  - [ ] Handles path failure (no path, partial path) with status codes
- [ ] **Create `PathResult` struct** (unmanaged, Burst-compatible)
  - [ ] Fields: `Entity requester`, `NativeArray<float3> waypoints`, `PathStatus status`
- [ ] **Create `PathWaypoint` IBufferElementData**
  - [ ] Fields: `float3 Position`
  - [ ] Attached as `DynamicBuffer<PathWaypoint>` on player entities
- [ ] **Create `PathFollowState` IComponentData**
  - [ ] Fields: `int CurrentWaypointIndex`, `float3 CurrentTarget`, `bool IsFollowingPath`,
        `float StopDistance`, `float RepathInterval`, `float TimeSinceLastRepath`

#### 3c. Click-to-Move System (ECS Side)

```
Architecture Flow:

[Input] RMB/LMB Click (paradigm-dependent)
    → [ECS]     ClickToMoveSystem: Raycast screen→world via CollisionWorld.CastRay()
    → [Managed] PathRequestService.RequestPath(playerPos, clickPos, entity)
    → [Managed] A* calculates path (PhysX graph, managed ABPath)
    → [Managed] PathRequestService writes result to NativeQueue<PathResult>
    → [ECS]     PathResultSystem: Dequeue results, populate DynamicBuffer<PathWaypoint>
    → [ECS]     PathFollowSystem: Read buffer, set MovementControl per frame (Burst)
    → [ECS]     PlayerMovementSystem: Apply movement (Burst, NetCode predicted)
    → [ECS]     CharacterControllerSystem: Resolve collisions (Unity Physics)
```

- [ ] **Create `ClickToMoveSystem`** (ISystem, Burst-compatible)
  - [ ] Runs in `PredictedFixedStepSimulationSystemGroup`
  - [ ] Reads `ParadigmSettings` singleton — only active when `IsClickToMoveEnabled`
  - [ ] Reads `ClickToMoveButton` from paradigm to determine LMB (ARPG) vs RMB (MOBA)
  - [ ] On click: perform `CollisionWorld.CastRay()` from cursor screen position through
        camera into world (using our Unity Physics, not PhysX)
  - [ ] Pass hit point to `PathRequestService` via managed bridge call
  - [ ] Set `PathFollowState.IsFollowingPath = true` on the entity
- [ ] **Create `PathResultSystem`** (ISystem)
  - [ ] Runs in `InitializationSystemGroup` (before movement systems)
  - [ ] Dequeues from `NativeQueue<PathResult>` (populated by managed `PathRequestService`)
  - [ ] Writes waypoints into `DynamicBuffer<PathWaypoint>` on the requester entity
  - [ ] Sets `PathFollowState.CurrentWaypointIndex = 0`
- [ ] **Create `PathFollowSystem`** (ISystem, BurstCompile)
  - [ ] Runs in `PredictedFixedStepSimulationSystemGroup` (before `PlayerMovementSystem`)
  - [ ] Reads `DynamicBuffer<PathWaypoint>` and `PathFollowState`
  - [ ] Calculates direction to current waypoint, writes to `PlayerInputComponent`
        (Horizontal/Vertical) so existing movement pipeline handles it
  - [ ] Advances `CurrentWaypointIndex` when within `StopDistance` of current waypoint
  - [ ] Clears path when final waypoint reached or interrupted by new WASD input
  - [ ] Handles repath timer: re-requests path at `RepathInterval` for moving targets
- [ ] **WASD Interruption**
  - [ ] If player presses WASD while following a path, immediately cancel path follow
  - [ ] Set `PathFollowState.IsFollowingPath = false`, clear waypoint buffer

#### 3d. ARPG Paradigm (Diablo-style)

- [ ] **Create `ARPGIsometric` profile** (InputParadigmProfile ScriptableObject)
  - [ ] `clickToMoveButton = LeftButton`
  - [ ] `usePathfinding = true`
  - [ ] `cursorVisible = true`
  - [ ] `cameraOrbitMode = KeyRotateOnly` (Q/E)
  - [ ] `facingMode = MovementDirection`
  - [ ] `isWASDEnabled = false`
- [ ] **Implement "Hold to Move" logic**
  - [ ] While LMB held, continuously update path destination to cursor world position
  - [ ] Repath at fixed interval (e.g., 200ms) while held, not every frame
- [ ] **Implement "Shift to Hold Position" logic**
  - [ ] Shift modifier cancels current path and prevents new movement commands
  - [ ] Allows attack-in-place / ability use without moving
- [ ] **Visuals: movement target indicator**
  - [ ] Spawn decal/VFX at click destination (on navmesh hit point)
  - [ ] Despawn on arrival or path cancellation

### Phase 4: Advanced Isometric, MOBA & Twin-Stick

#### 4a. MOBA Paradigm

- [ ] **Create `MOBATopDown` profile** (InputParadigmProfile ScriptableObject)
  - [ ] `clickToMoveButton = RightButton`
  - [ ] `usePathfinding = true`
  - [ ] `cursorVisible = true`
  - [ ] `cameraOrbitMode = FollowOnly`
  - [ ] `facingMode = MovementDirection`
  - [ ] `isWASDEnabled = false`
- [ ] **Attack-Move Logic**
  - [ ] Implement A-Click (Attack-Move Click) input handling via `Combat_MOBA` action map
  - [ ] Create `AttackMoveSystem` (ISystem, Burst) — when A-Click issued:
    1. Request path to click location via `PathRequestService`
    2. While following path, run `AggroScanSystem` query each tick
    3. If hostile entity found within aggro radius along path, stop and engage
  - [ ] Create `AggroScanSystem` (ISystem, Burst)
    - [ ] `CollisionWorld.OverlapAabb()` or sphere query centered on agent
    - [ ] Filter for hostile entities within configurable aggro range
    - [ ] Return nearest valid target
  - [ ] Stop-to-Attack behavior: pause path follow → face target → attack → resume path
- [ ] **Camera Controls**
  - [ ] Implement Edge-Panning: detect cursor at screen edges, translate camera
  - [ ] Implement Camera Lock toggle (Space/Y): lock camera to player vs free-pan

#### 4b. Twin-Stick Paradigm

- [ ] **Create `TwinStickAim` profile** (InputParadigmProfile ScriptableObject)
  - [ ] `isWASDEnabled = true`
  - [ ] `clickToMoveButton = None` (no click-to-move)
  - [ ] `usePathfinding = false`
  - [ ] `cursorVisible = true`
  - [ ] `facingMode = CursorDirection`
  - [ ] `cameraOrbitMode = FollowOnly`
- [ ] **Decouple facing from movement**
  - [ ] Character always faces cursor world position regardless of movement direction
  - [ ] `PlayerFacingSystem` already supports `CursorDirection` mode — verify integration
- [ ] **Bind attack direction to cursor**
  - [ ] Attacks fire toward cursor position, not movement direction
  - [ ] Integrate with existing weapon/ability aiming systems

#### 4c. NPC Pathfinding (Future Consideration)

> **Note:** For server-authoritative NPC pathfinding, evaluate using A*'s `FollowerEntity`
> in ECS subscene mode on the server. Since NPCs don't require client-side prediction
> (clients just interpolate ghost positions), `FollowerEntity`'s lack of NetCode awareness
> is acceptable. Server runs `FollowerEntity` systems → position syncs to clients as ghost
> transforms. This avoids building a custom NPC path-follow pipeline.
>
> **Prerequisites:** Requires the PhysX mirror collider scene from Phase 3a to already be
> in place. `FollowerEntity` uses PhysX raycasts for ground detection and movement.

- [ ] Evaluate `FollowerEntity` (subscene/pure ECS) for server-side NPC agents
- [ ] If adopted: create `NPCPathfindingAuthoring` baker for NPC prefabs
- [ ] If rejected: extend `PathRequestService` + `PathFollowSystem` for NPC entities

### Phase 5: Context Overlays (Tier 4)
- [ ] **Vehicle System**
  - [ ] Create `VehicleOverlay` mode
  - [ ] Implement momentum-based physics controller replacement
  - [ ] Disable standard strafe/crouch inputs
- [ ] **Build Mode**
  - [ ] Create `BuildOverlay` mode
  - [ ] Implement Ghost Object mouse tracking
  - [ ] Implement Grid Snapping logic

---

## Related EPICs

| EPIC | Relationship |
|------|--------------|
| 15.18 | Parent - Tier 1 implementation (ShooterDirect, HybridToggle) |
| 14.9 | CursorAimTargeting, camera utilities |
| 15.16 | Target lock integration |
| 15.20 Phase 3 | Click-to-move via A* bridge layer (PathRequestService → ECS PathFollowSystem) |
| TBD | Vehicle/mount system |
| TBD | Building/placement system |

---

## Implementation Architecture

This section describes how to build the paradigm system following the codebase's established patterns and SOLID principles.

### Design Principles Applied

| Principle | Application |
|-----------|-------------|
| **Single Responsibility** | Separate classes for state, configuration, cursor control, movement routing |
| **Open/Closed** | New paradigms added via ScriptableObject profiles, no code changes |
| **Liskov Substitution** | All providers implement interfaces, swappable implementations |
| **Interface Segregation** | Small focused interfaces for each concern |
| **Dependency Inversion** | Systems depend on interfaces, not concrete implementations |

### Decoupled Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              UI LAYER                                       │
│                         (Settings Menu, HUD)                                │
│                                                                             │
│   InputSettingsView subscribes to IInputParadigmProvider.OnParadigmChanged  │
│   Calls IInputParadigmProvider.TrySetParadigm() on user selection           │
│   NO direct reference to InputParadigmManager - only interface              │
│                                                                             │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │ Interface
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PROVIDER INTERFACE                                  │
│                                                                             │
│   IInputParadigmProvider                                                    │
│   ├── ActiveProfile : InputParadigmProfile (read-only)                      │
│   ├── AvailableProfiles : IReadOnlyList<InputParadigmProfile>               │
│   ├── OnParadigmChanged : event Action<InputParadigmProfile>                │
│   ├── OnModeOverlayChanged : event Action<InputModeOverlay>                 │
│   ├── TrySetParadigm(profile) : bool                                        │
│   └── SetModeOverlay(overlay) : void                                        │
│                                                                             │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │ Implements
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    InputParadigmManager (MonoBehaviour)                     │
│                         Implements IInputParadigmProvider                   │
│                                                                             │
│   ONLY responsibilities:                                                    │
│   - Store active profile                                                    │
│   - Validate paradigm ↔ camera compatibility                                │
│   - Broadcast OnParadigmChanged event                                       │
│   - Sync minimal state to ECS                                               │
│                                                                             │
│   Does NOT:                                                                 │
│   - Configure cursor (that's ICursorController's job)                       │
│   - Configure camera (listeners handle themselves)                          │
│   - Configure movement (listeners handle themselves)                        │
│                                                                             │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │ OnParadigmChanged event
                     ┌──────────────┼──────────────┬──────────────┐
                     ▼              ▼              ▼              ▼
              ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐
              │ ICursor    │ │ ICamera    │ │ IMovement  │ │ IFacing    │
              │ Controller │ │ Controller │ │ Router     │ │ Controller │
              │            │ │            │ │            │ │            │
              │ Subscribes │ │ Subscribes │ │ Subscribes │ │ Subscribes │
              │ & self-    │ │ & self-    │ │ & self-    │ │ & self-    │
              │ configures │ │ configures │ │ configures │ │ configures │
              └────────────┘ └────────────┘ └────────────┘ └────────────┘
```

### Core Interfaces

#### IInputParadigmProvider

The main interface that UI and systems consume. Follows the same pattern as `IHealthBarSettingsProvider`.

```csharp
// Assets/Scripts/Core/Input/Interfaces/IInputParadigmProvider.cs
namespace DIG.Core.Input
{
    /// <summary>
    /// Provider interface for input paradigm settings.
    /// Decouples UI and gameplay systems from concrete implementation.
    /// 
    /// Pattern: Same as IHealthBarSettingsProvider in Combat.UI
    /// </summary>
    public interface IInputParadigmProvider
    {
        /// <summary>Current active paradigm profile.</summary>
        InputParadigmProfile ActiveProfile { get; }
        
        /// <summary>All paradigms available for selection.</summary>
        IReadOnlyList<InputParadigmProfile> AvailableProfiles { get; }
        
        /// <summary>Current mode overlay (Vehicle, Build, or None).</summary>
        InputModeOverlay ActiveModeOverlay { get; }
        
        /// <summary>Fired when paradigm changes. Listeners self-configure.</summary>
        event System.Action<InputParadigmProfile> OnParadigmChanged;
        
        /// <summary>Fired when mode overlay changes.</summary>
        event System.Action<InputModeOverlay> OnModeOverlayChanged;
        
        /// <summary>
        /// Attempt to switch paradigm. Returns false if incompatible with current camera.
        /// </summary>
        bool TrySetParadigm(InputParadigmProfile profile);
        
        /// <summary>
        /// Attempt to switch by paradigm type (finds matching profile).
        /// </summary>
        bool TrySetParadigm(InputParadigm paradigm);
        
        /// <summary>
        /// Activate or deactivate a mode overlay.
        /// </summary>
        void SetModeOverlay(InputModeOverlay overlay);
        
        /// <summary>
        /// Check if a paradigm is compatible with current camera.
        /// </summary>
        bool IsParadigmCompatible(InputParadigmProfile profile);
    }
}
```

#### ICursorController

Handles cursor visibility and lock state. Separates concern from paradigm manager.

```csharp
// Assets/Scripts/Core/Input/Interfaces/ICursorController.cs
namespace DIG.Core.Input
{
    /// <summary>
    /// Controls cursor visibility and lock state.
    /// Listens to paradigm changes and self-configures.
    /// </summary>
    public interface ICursorController
    {
        bool IsCursorVisible { get; }
        bool IsCursorLocked { get; }
        bool IsCursorFreeByDefault { get; }
        
        /// <summary>
        /// Temporarily override cursor state (e.g., Alt-to-free in Shooter).
        /// </summary>
        void SetTemporaryFree(bool free);
    }
}
```

#### IMovementRouter

Routes movement input based on paradigm (WASD vs click-to-move).

```csharp
// Assets/Scripts/Core/Input/Interfaces/IMovementRouter.cs
namespace DIG.Core.Input
{
    /// <summary>
    /// Routes movement input based on active paradigm.
    /// Decides whether WASD, click-to-move, or both are active.
    /// </summary>
    public interface IMovementRouter
    {
        bool IsWASDEnabled { get; }
        bool IsClickToMoveEnabled { get; }
        MouseButton ClickToMoveButton { get; }
        bool UsePathfinding { get; }
    }
}
```

#### IFacingController

Controls character facing based on paradigm.

```csharp
// Assets/Scripts/Core/Input/Interfaces/IFacingController.cs
namespace DIG.Core.Input
{
    /// <summary>
    /// Controls character rotation/facing.
    /// Listens to paradigm changes and applies correct facing mode.
    /// </summary>
    public interface IFacingController
    {
        MovementFacingMode CurrentFacingMode { get; }
    }
}
```

### Self-Configuring Listeners

The key architectural insight: **listeners subscribe to events and configure themselves**, rather than having a god-object configure them.

```csharp
// Example: CursorController listens and self-configures
public class CursorController : MonoBehaviour, ICursorController
{
    private IInputParadigmProvider _paradigmProvider;
    
    private void OnEnable()
    {
        // Find provider via ServiceLocator or DI, not singleton
        _paradigmProvider = ServiceLocator.Get<IInputParadigmProvider>();
        _paradigmProvider.OnParadigmChanged += OnParadigmChanged;
        
        // Initialize from current state
        OnParadigmChanged(_paradigmProvider.ActiveProfile);
    }
    
    private void OnDisable()
    {
        if (_paradigmProvider != null)
            _paradigmProvider.OnParadigmChanged -= OnParadigmChanged;
    }
    
    private void OnParadigmChanged(InputParadigmProfile profile)
    {
        // Self-configure based on new paradigm
        IsCursorFreeByDefault = profile.cursorFreeByDefault;
        _cameraOrbitButton = profile.cameraOrbitButton;
        UpdateCursorState();
    }
}
```

### ServiceLocator vs Singletons

The codebase currently uses singletons. For better testability, we can add a simple ServiceLocator that wraps them:

```csharp
// Assets/Scripts/Core/Services/ServiceLocator.cs
namespace DIG.Core
{
    /// <summary>
    /// Simple service locator for decoupling.
    /// Wraps existing singletons behind interfaces.
    /// Can be replaced with proper DI container later.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();
        
        public static void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }
        
        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;
            return null;
        }
        
        // Bootstrap method called on game start
        public static void Initialize()
        {
            // Register existing singletons behind interfaces
            Register<IInputParadigmProvider>(InputParadigmManager.Instance);
            Register<ICursorController>(CursorController.Instance);
            // etc.
        }
    }
}
```

### System Integration Map (Decoupled)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PARADIGM MANAGER                                    │
│                                                                             │
│   InputParadigmManager : MonoBehaviour, IInputParadigmProvider              │
│                                                                             │
│   Responsibilities (SRP):                                                   │
│   ✓ Store active profile                                                    │
│   ✓ Validate paradigm ↔ camera compatibility                                │
│   ✓ Fire OnParadigmChanged event                                            │
│   ✓ Sync InputParadigmState to ECS                                          │
│                                                                             │
│   NOT responsible for (delegated):                                          │
│   ✗ Cursor lock/visibility     → CursorController listens                   │
│   ✗ Camera orbit enabling      → CameraController listens                   │
│   ✗ Movement routing           → MovementRouter listens                     │
│   ✗ Character facing           → PlayerFacingSystem (ECS) reads state       │
│                                                                             │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    │ OnParadigmChanged event (Observer Pattern)
                                    │
         ┌──────────────────────────┼──────────────────────────┐
         ▼                          ▼                          ▼
┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐
│  CursorController   │  │   CameraController  │  │   MovementRouter    │
│  : ICursorController│  │                     │  │  : IMovementRouter  │
│                     │  │  Wraps existing     │  │                     │
│  - Listens to event │  │  CameraModeProvider │  │  - Listens to event │
│  - Reads profile.   │  │                     │  │  - Reads profile.   │
│    cursorFreeBy     │  │  - Listens to event │  │    clickToMove      │
│    Default          │  │  - Reads profile.   │  │    Enabled          │
│  - Reads profile.   │  │    mouseOrbitEnabled│  │  - Reads profile.   │
│    cameraOrbitBtn   │  │  - Reads profile.   │  │    wasdEnabled      │
│  - Self-configures  │  │    edgePanEnabled   │  │  - Self-configures  │
│                     │  │  - Self-configures  │  │                     │
└─────────────────────┘  └─────────────────────┘  └─────────────────────┘
         │                          │                          │
         │                          │                          │
         ▼                          ▼                          ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              ECS LAYER                                      │
│                                                                             │
│   InputParadigmState : IComponentData (on player entity)                    │
│   - Read-only from ECS perspective (written by MonoBehaviour manager)       │
│   - ECS systems query this instead of calling MonoBehaviour singletons      │
│                                                                             │
│   ┌─────────────────────┐  ┌─────────────────────┐  ┌──────────────────┐   │
│   │ PlayerFacingSystem  │  │ ClickToMoveSystem   │  │ CursorAimSystem  │   │
│   │                     │  │                     │  │ (existing)       │   │
│   │ Reads:              │  │ Reads:              │  │                  │   │
│   │ - paradigmState.    │  │ - paradigmState.    │  │ Reads:           │   │
│   │   FacingMode        │  │   IsClickToMove     │  │ - paradigmState. │   │
│   │ - paradigmState.    │  │   Enabled           │  │   CursorWorld    │   │
│   │   CursorWorldPos    │  │ - paradigmState.    │  │   Position       │   │
│   │                     │  │   ClickMoveButton   │  │                  │   │
│   └─────────────────────┘  └─────────────────────┘  └──────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### InputParadigmManager (Cleaned Up)

Focused single responsibility:

```csharp
// Assets/Scripts/Core/Input/InputParadigmManager.cs
namespace DIG.Core.Input
{
    /// <summary>
    /// Central manager for input paradigms.
    /// Stores active paradigm and broadcasts changes.
    /// Listeners self-configure via OnParadigmChanged event.
    /// 
    /// Follows same pattern as HealthBarSettingsManager.
    /// </summary>
    public class InputParadigmManager : MonoBehaviour, IInputParadigmProvider
    {
        public static InputParadigmManager Instance { get; private set; }
        
        [Header("Configuration")]
        [SerializeField] private InputParadigmProfile _defaultProfile;
        [SerializeField] private InputParadigmProfile[] _availableProfiles;
        
        [Header("Runtime State (Read-Only)")]
        [SerializeField, ReadOnly] private InputParadigmProfile _activeProfile;
        [SerializeField, ReadOnly] private InputModeOverlay _activeModeOverlay;
        
        // IInputParadigmProvider implementation
        public InputParadigmProfile ActiveProfile => _activeProfile;
        public IReadOnlyList<InputParadigmProfile> AvailableProfiles => _availableProfiles;
        public InputModeOverlay ActiveModeOverlay => _activeModeOverlay;
        
        public event System.Action<InputParadigmProfile> OnParadigmChanged;
        public event System.Action<InputModeOverlay> OnModeOverlayChanged;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Register with ServiceLocator for interface-based access
            ServiceLocator.Register<IInputParadigmProvider>(this);
        }
        
        private void Start()
        {
            // Apply default paradigm (triggers listeners)
            TrySetParadigm(_defaultProfile);
        }
        
        public bool TrySetParadigm(InputParadigmProfile profile)
        {
            if (profile == null) return false;
            
            // Validate camera compatibility
            if (!IsParadigmCompatible(profile))
            {
                Debug.LogWarning($"[InputParadigm] {profile.displayName} incompatible with current camera");
                return false;
            }
            
            var previousProfile = _activeProfile;
            _activeProfile = profile;
            
            // Sync to ECS (minimal bridge)
            SyncToECS();
            
            // Notify listeners - they self-configure
            OnParadigmChanged?.Invoke(profile);
            
            Debug.Log($"[InputParadigm] Switched: {previousProfile?.displayName ?? "None"} → {profile.displayName}");
            return true;
        }
        
        public bool TrySetParadigm(InputParadigm paradigm)
        {
            var profile = System.Array.Find(_availableProfiles, p => p.paradigm == paradigm);
            if (profile == null)
            {
                Debug.LogWarning($"[InputParadigm] No profile found for {paradigm}");
                return false;
            }
            return TrySetParadigm(profile);
        }
        
        public void SetModeOverlay(InputModeOverlay overlay)
        {
            if (_activeModeOverlay == overlay) return;
            
            _activeModeOverlay = overlay;
            SyncToECS();
            OnModeOverlayChanged?.Invoke(overlay);
        }
        
        public bool IsParadigmCompatible(InputParadigmProfile profile)
        {
            // Get current camera type from existing CameraModeProvider
            var cameraProvider = CameraModeProvider.Instance;
            if (cameraProvider == null) return true; // No camera = allow anything
            
            var currentCameraType = cameraProvider.ActiveCameraType;
            return profile.IsCompatibleWith(currentCameraType);
        }
        
        private void SyncToECS()
        {
            // Find local player and update InputParadigmState component
            // This is the ONLY bridge point to ECS
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<InputParadigmState>(),
                ComponentType.ReadOnly<PlayerTag>()
            );
            
            if (query.IsEmpty) return;
            
            var entity = query.GetSingletonEntity();
            em.SetComponentData(entity, new InputParadigmState
            {
                ActiveParadigm = _activeProfile.paradigm,
                FacingMode = _activeProfile.facingMode,
                IsClickToMoveEnabled = _activeProfile.clickToMoveEnabled,
                ClickToMoveButton = _activeProfile.clickToMoveButton,
            });
        }
    }
}
```

### UI Integration (Decoupled)

Settings UI depends only on the interface, not the concrete manager:

```csharp
// Assets/Scripts/UI/Settings/InputSettingsView.cs
namespace DIG.UI.Settings
{
    /// <summary>
    /// Settings panel for input paradigm selection.
    /// Depends only on IInputParadigmProvider interface.
    /// </summary>
    public class InputSettingsView : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Dropdown _paradigmDropdown;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Image _paradigmIcon;
        
        private IInputParadigmProvider _provider;
        
        private void OnEnable()
        {
            // Get provider via ServiceLocator (interface-based)
            _provider = ServiceLocator.Get<IInputParadigmProvider>();
            if (_provider == null)
            {
                Debug.LogError("[InputSettingsView] No IInputParadigmProvider registered");
                return;
            }
            
            // Subscribe to changes
            _provider.OnParadigmChanged += OnParadigmChanged;
            
            // Populate dropdown
            PopulateDropdown();
            
            // Set initial selection
            OnParadigmChanged(_provider.ActiveProfile);
        }
        
        private void OnDisable()
        {
            if (_provider != null)
                _provider.OnParadigmChanged -= OnParadigmChanged;
        }
        
        private void PopulateDropdown()
        {
            _paradigmDropdown.ClearOptions();
            
            var options = _provider.AvailableProfiles
                .Select(p => new TMP_Dropdown.OptionData(p.displayName, p.icon))
                .ToList();
            
            _paradigmDropdown.AddOptions(options);
        }
        
        // Called by UI when dropdown changes
        public void OnDropdownValueChanged(int index)
        {
            var selectedProfile = _provider.AvailableProfiles[index];
            
            if (!_provider.TrySetParadigm(selectedProfile))
            {
                // Revert dropdown to current
                int currentIndex = _provider.AvailableProfiles
                    .ToList()
                    .IndexOf(_provider.ActiveProfile);
                _paradigmDropdown.SetValueWithoutNotify(currentIndex);
                
                // Show incompatibility message
                ShowIncompatibleMessage(selectedProfile);
            }
        }
        
        private void OnParadigmChanged(InputParadigmProfile profile)
        {
            // Update UI to reflect new paradigm
            _descriptionText.text = profile.description;
            _paradigmIcon.sprite = profile.icon;
            
            int index = _provider.AvailableProfiles.ToList().IndexOf(profile);
            _paradigmDropdown.SetValueWithoutNotify(index);
        }
        
        private void ShowIncompatibleMessage(InputParadigmProfile profile)
        {
            // Show toast/popup explaining incompatibility
            Debug.Log($"Cannot use {profile.displayName} with current camera mode");
        }
    }
}
```

### Testing Support

With interfaces, we can easily mock for tests:

```csharp
// Tests/EditMode/Input/MockInputParadigmProvider.cs
public class MockInputParadigmProvider : IInputParadigmProvider
{
    public InputParadigmProfile ActiveProfile { get; set; }
    public IReadOnlyList<InputParadigmProfile> AvailableProfiles { get; set; }
    public InputModeOverlay ActiveModeOverlay { get; set; }
    
    public event System.Action<InputParadigmProfile> OnParadigmChanged;
    public event System.Action<InputModeOverlay> OnModeOverlayChanged;
    
    public bool TrySetParadigm(InputParadigmProfile profile)
    {
        ActiveProfile = profile;
        OnParadigmChanged?.Invoke(profile);
        return true;
    }
    
    // ... etc
}

// In tests:
[Test]
public void CursorController_WhenParadigmChangesToMMO_SetsCursorFreeByDefault()
{
    var mockProvider = new MockInputParadigmProvider();
    var cursorController = new CursorController(mockProvider);
    
    var mmoProfile = ScriptableObject.CreateInstance<InputParadigmProfile>();
    mmoProfile.cursorFreeByDefault = true;
    
    mockProvider.TrySetParadigm(mmoProfile);
    
    Assert.IsTrue(cursorController.IsCursorFreeByDefault);
}
```

### Existing System Modifications (Minimal)

Existing systems need minimal changes - just subscribe to the event:

#### InputSchemeManager (Existing - Modify)

```csharp
// Add to existing InputSchemeManager.cs
private void OnEnable()
{
    var provider = ServiceLocator.Get<IInputParadigmProvider>();
    if (provider != null)
    {
        provider.OnParadigmChanged += OnParadigmChanged;
    }
}

private void OnDisable()
{
    var provider = ServiceLocator.Get<IInputParadigmProvider>();
    if (provider != null)
    {
        provider.OnParadigmChanged -= OnParadigmChanged;
    }
}

private void OnParadigmChanged(InputParadigmProfile profile)
{
    // Map paradigm to scheme
    InputScheme scheme = profile.paradigm switch
    {
        InputParadigm.Shooter => InputScheme.ShooterDirect,
        InputParadigm.MMO => InputScheme.MMOCursor,
        InputParadigm.ARPG => InputScheme.TacticalCursor,
        InputParadigm.MOBA => InputScheme.TacticalCursor,
        InputParadigm.TwinStick => InputScheme.TwinStickAim,
        _ => InputScheme.ShooterDirect
    };
    
    SetScheme(scheme);
}
```

### Preset Profiles (ScriptableObject Data)

Create once, configure in Inspector, no code changes needed to add new paradigms:

```
Assets/Data/Input/Profiles/
├── Profile_Shooter.asset        // paradigm=Shooter, cursorFree=false
├── Profile_ShooterHybrid.asset  // paradigm=Shooter, cursorFree=false, altKey=LeftAlt
├── Profile_MMO.asset            // paradigm=MMO, cursorFree=true, orbitBtn=RMB
├── Profile_ARPG_Classic.asset   // paradigm=ARPG, clickToMove=LMB, wasd=false
├── Profile_ARPG_Hybrid.asset    // paradigm=ARPG, clickToMove=LMB, wasd=true (D4 style)
├── Profile_TwinStick.asset      // paradigm=TwinStick, facing=Cursor
└── Profile_MOBA.asset           // paradigm=MOBA, clickToMove=RMB
```

### Summary: Before vs After

| Aspect | Before (Draft) | After (Revised) |
|--------|----------------|-----------------|
| **Dependencies** | Direct singleton calls | Interface-based via ServiceLocator |
| **Coupling** | Manager configures everything | Listeners self-configure via events |
| **Testability** | Requires running game | Mockable interfaces |
| **New Paradigms** | Modify code | Add ScriptableObject profile |
| **UI Separation** | UI calls manager directly | UI depends on interface only |
| **ECS Bridge** | Mixed in manager | Single sync point, ECS reads state |
| **SRP** | Manager does 5+ things | Manager does 1 thing (state + events) |

This now follows the patterns established by `IHealthBarSettingsProvider`, `HealthBarSettingsManager`, and the ViewModel pattern used throughout the codebase.

---

## State Machine Coordinator Architecture

The event-based approach above is simple but has potential failure modes during complex transitions. This section describes a **State Machine Coordinator** pattern that provides bulletproof transitions with rollback support.

### Why State Machines?

Event-based approach failure modes:

| Failure Mode | Description | Impact |
|--------------|-------------|--------|
| **Partial Configuration** | Listener A succeeds, B throws, C never runs | Half-configured system |
| **Race Conditions** | Listeners execute in non-deterministic order | Order-dependent bugs |
| **Reentrancy** | Transition triggers during transition | Corrupted state |
| **No Rollback** | No way to undo if late-stage fails | Stuck in bad state |
| **Hidden Dependencies** | CursorController requires CameraController first | Silent failures |

State Machine approach solves all of these.

### Event-Based vs State Machine Comparison

| Aspect | Event-Based (Observer) | State Machine Coordinator |
|--------|------------------------|---------------------------|
| **Transition atomic?** | No (each listener independent) | Yes (all-or-nothing) |
| **Rollback support?** | No | Yes (snapshot-based) |
| **Order guaranteed?** | No | Yes (explicit) |
| **Reentrancy safe?** | No | Yes (Transitioning state) |
| **Pre-validation?** | Limited (just camera check) | Full (all subsystems) |
| **Debuggable?** | Trace events manually | State diagram + logs |
| **Complexity** | Simple | Moderate |
| **Best for** | Simple systems | Complex orchestration |

### State Machine Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     ParadigmStateMachine (Coordinator)                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   States:                                                                   │
│   ┌─────────────┐    RequestTransition()    ┌───────────────────┐          │
│   │             │ ─────────────────────────▶│                   │          │
│   │   Stable    │                           │   Transitioning   │          │
│   │ (Shooter,   │◀───────────────────────── │   (In-Progress)   │          │
│   │  MMO, ARPG, │    Success / Rollback     │                   │          │
│   │  MOBA, etc) │                           └───────────────────┘          │
│   └─────────────┘                                                          │
│         │                                                                  │
│         │ CanTransitionTo(targetParadigm)                                  │
│         ▼                                                                  │
│   ┌─────────────────────────────────────────────────────────────────────┐  │
│   │                         Guard Conditions                            │  │
│   │  ✓ Camera compatibility check                                       │  │
│   │  ✓ All subsystems report CanConfigure(profile) = true               │  │
│   │  ✓ No transition already in progress                                │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Core Interface: IParadigmConfigurable

Every subsystem that needs to respond to paradigm changes implements this interface:

```csharp
// Assets/Scripts/Core/Input/Interfaces/IParadigmConfigurable.cs
namespace DIG.Core.Input
{
    /// <summary>
    /// Interface for subsystems that configure themselves during paradigm transitions.
    /// Supports pre-validation, snapshot capture, configuration, and rollback.
    /// 
    /// Order of operations during transition:
    /// 1. CanConfigure() - Check if this subsystem can handle the new paradigm
    /// 2. CaptureSnapshot() - Save current state for potential rollback
    /// 3. Configure() - Apply the new paradigm configuration
    /// 4. Rollback() - Restore from snapshot if later step fails
    /// </summary>
    public interface IParadigmConfigurable
    {
        /// <summary>Configuration priority. Lower = configured first.</summary>
        int ConfigurationOrder { get; }
        
        /// <summary>Human-readable name for logging.</summary>
        string SubsystemName { get; }
        
        /// <summary>
        /// Pre-validate: Can this subsystem configure for the given profile?
        /// Called BEFORE any configuration starts.
        /// Return false with error message to abort entire transition.
        /// </summary>
        bool CanConfigure(InputParadigmProfile profile, out string errorReason);
        
        /// <summary>
        /// Capture current state for potential rollback.
        /// Called BEFORE Configure() is invoked.
        /// </summary>
        IConfigSnapshot CaptureSnapshot();
        
        /// <summary>
        /// Apply configuration for the new paradigm.
        /// May throw on error (coordinator will rollback).
        /// </summary>
        void Configure(InputParadigmProfile profile);
        
        /// <summary>
        /// Rollback to the provided snapshot.
        /// Called if a later subsystem fails during Configure().
        /// </summary>
        void Rollback(IConfigSnapshot snapshot);
    }
    
    /// <summary>
    /// Marker interface for configuration snapshots.
    /// Each subsystem defines its own snapshot class.
    /// </summary>
    public interface IConfigSnapshot { }
}
```

### ParadigmStateMachine Implementation

The coordinator that orchestrates all transitions:

```csharp
// Assets/Scripts/Core/Input/ParadigmStateMachine.cs
namespace DIG.Core.Input
{
    public enum ParadigmState
    {
        Stable,
        Transitioning
    }
    
    /// <summary>
    /// State machine coordinator for paradigm transitions.
    /// Ensures atomic transitions with rollback support.
    /// </summary>
    public class ParadigmStateMachine : MonoBehaviour, IInputParadigmProvider
    {
        public static ParadigmStateMachine Instance { get; private set; }
        
        [Header("Configuration")]
        [SerializeField] private InputParadigmProfile _defaultProfile;
        [SerializeField] private InputParadigmProfile[] _availableProfiles;
        
        [Header("Runtime State")]
        [SerializeField, ReadOnly] private ParadigmState _state = ParadigmState.Stable;
        [SerializeField, ReadOnly] private InputParadigmProfile _activeProfile;
        [SerializeField, ReadOnly] private InputModeOverlay _activeModeOverlay;
        
        // Registered configurable subsystems (sorted by ConfigurationOrder)
        private readonly SortedList<int, IParadigmConfigurable> _configurables = new();
        
        // IInputParadigmProvider implementation
        public InputParadigmProfile ActiveProfile => _activeProfile;
        public IReadOnlyList<InputParadigmProfile> AvailableProfiles => _availableProfiles;
        public InputModeOverlay ActiveModeOverlay => _activeModeOverlay;
        
        public event System.Action<InputParadigmProfile> OnParadigmChanged;
        public event System.Action<InputModeOverlay> OnModeOverlayChanged;
        
        // Additional state machine events
        public event System.Action<InputParadigmProfile, InputParadigmProfile> OnTransitionStarted;
        public event System.Action<InputParadigmProfile, bool> OnTransitionCompleted; // bool = success
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ServiceLocator.Register<IInputParadigmProvider>(this);
        }
        
        /// <summary>
        /// Register a configurable subsystem.
        /// Called by subsystems in their Awake/OnEnable.
        /// </summary>
        public void RegisterConfigurable(IParadigmConfigurable configurable)
        {
            _configurables[configurable.ConfigurationOrder] = configurable;
            Debug.Log($"[ParadigmSM] Registered: {configurable.SubsystemName} (order: {configurable.ConfigurationOrder})");
        }
        
        public void UnregisterConfigurable(IParadigmConfigurable configurable)
        {
            _configurables.Remove(configurable.ConfigurationOrder);
        }
        
        public bool TrySetParadigm(InputParadigmProfile profile)
        {
            if (profile == null) return false;
            
            // Guard: Reject if already transitioning
            if (_state == ParadigmState.Transitioning)
            {
                Debug.LogWarning("[ParadigmSM] Cannot transition - already in progress");
                return false;
            }
            
            // Guard: Check camera compatibility
            if (!IsParadigmCompatible(profile))
            {
                Debug.LogWarning($"[ParadigmSM] {profile.displayName} incompatible with current camera");
                return false;
            }
            
            // Guard: Pre-validate all subsystems
            foreach (var kvp in _configurables)
            {
                var configurable = kvp.Value;
                if (!configurable.CanConfigure(profile, out string error))
                {
                    Debug.LogWarning($"[ParadigmSM] {configurable.SubsystemName} rejected transition: {error}");
                    return false;
                }
            }
            
            // All guards passed - begin transition
            return ExecuteTransition(profile);
        }
        
        public bool TrySetParadigm(InputParadigm paradigm)
        {
            var profile = System.Array.Find(_availableProfiles, p => p.paradigm == paradigm);
            return profile != null && TrySetParadigm(profile);
        }
        
        private bool ExecuteTransition(InputParadigmProfile targetProfile)
        {
            var previousProfile = _activeProfile;
            _state = ParadigmState.Transitioning;
            
            OnTransitionStarted?.Invoke(previousProfile, targetProfile);
            Debug.Log($"[ParadigmSM] Transition started: {previousProfile?.displayName ?? "None"} → {targetProfile.displayName}");
            
            // Capture snapshots from all subsystems (in order)
            var snapshots = new Dictionary<IParadigmConfigurable, IConfigSnapshot>();
            foreach (var kvp in _configurables)
            {
                var configurable = kvp.Value;
                snapshots[configurable] = configurable.CaptureSnapshot();
            }
            
            // Configure all subsystems (in order)
            var configuredSubsystems = new List<IParadigmConfigurable>();
            bool success = true;
            IParadigmConfigurable failedSubsystem = null;
            
            foreach (var kvp in _configurables)
            {
                var configurable = kvp.Value;
                try
                {
                    configurable.Configure(targetProfile);
                    configuredSubsystems.Add(configurable);
                    Debug.Log($"[ParadigmSM] Configured: {configurable.SubsystemName}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ParadigmSM] {configurable.SubsystemName} failed: {ex.Message}");
                    failedSubsystem = configurable;
                    success = false;
                    break;
                }
            }
            
            if (!success)
            {
                // Rollback in reverse order
                Debug.LogWarning($"[ParadigmSM] Rolling back due to failure in {failedSubsystem.SubsystemName}");
                
                for (int i = configuredSubsystems.Count - 1; i >= 0; i--)
                {
                    var configurable = configuredSubsystems[i];
                    try
                    {
                        configurable.Rollback(snapshots[configurable]);
                        Debug.Log($"[ParadigmSM] Rolled back: {configurable.SubsystemName}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[ParadigmSM] Rollback failed for {configurable.SubsystemName}: {ex.Message}");
                        // Continue rolling back other subsystems anyway
                    }
                }
                
                _state = ParadigmState.Stable;
                OnTransitionCompleted?.Invoke(previousProfile, false);
                return false;
            }
            
            // Success - update state
            _activeProfile = targetProfile;
            _state = ParadigmState.Stable;
            
            // Sync to ECS
            SyncToECS();
            
            // Notify observers (for UI updates, etc.)
            OnParadigmChanged?.Invoke(targetProfile);
            OnTransitionCompleted?.Invoke(targetProfile, true);
            
            Debug.Log($"[ParadigmSM] Transition complete: {targetProfile.displayName}");
            return true;
        }
        
        public void SetModeOverlay(InputModeOverlay overlay)
        {
            if (_activeModeOverlay == overlay) return;
            if (_state == ParadigmState.Transitioning)
            {
                Debug.LogWarning("[ParadigmSM] Cannot change overlay during transition");
                return;
            }
            
            _activeModeOverlay = overlay;
            SyncToECS();
            OnModeOverlayChanged?.Invoke(overlay);
        }
        
        public bool IsParadigmCompatible(InputParadigmProfile profile)
        {
            var cameraProvider = CameraModeProvider.Instance;
            if (cameraProvider == null) return true;
            
            var currentCameraType = cameraProvider.ActiveCameraType;
            return profile.IsCompatibleWith(currentCameraType);
        }
        
        private void SyncToECS()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<InputParadigmState>(),
                ComponentType.ReadOnly<PlayerTag>()
            );
            
            if (query.IsEmpty) return;
            
            var entity = query.GetSingletonEntity();
            em.SetComponentData(entity, new InputParadigmState
            {
                ActiveParadigm = _activeProfile.paradigm,
                FacingMode = _activeProfile.facingMode,
                IsClickToMoveEnabled = _activeProfile.clickToMoveEnabled,
                ClickToMoveButton = _activeProfile.clickToMoveButton,
            });
        }
    }
}
```

### Example: CursorController as IParadigmConfigurable

```csharp
// Assets/Scripts/Core/Input/CursorController.cs
namespace DIG.Core.Input
{
    public class CursorController : MonoBehaviour, ICursorController, IParadigmConfigurable
    {
        // IParadigmConfigurable implementation
        public int ConfigurationOrder => 100;  // Configure after camera (order 50)
        public string SubsystemName => "CursorController";
        
        // Current state
        private bool _cursorFreeByDefault;
        private MouseButton _cameraOrbitButton;
        private CursorLockMode _lockMode;
        
        // Snapshot class
        private class CursorSnapshot : IConfigSnapshot
        {
            public bool CursorFreeByDefault;
            public MouseButton CameraOrbitButton;
            public CursorLockMode LockMode;
            public bool CursorVisible;
        }
        
        private void Awake()
        {
            ParadigmStateMachine.Instance?.RegisterConfigurable(this);
        }
        
        private void OnDestroy()
        {
            ParadigmStateMachine.Instance?.UnregisterConfigurable(this);
        }
        
        public bool CanConfigure(InputParadigmProfile profile, out string errorReason)
        {
            // Cursor controller can always configure
            errorReason = null;
            return true;
        }
        
        public IConfigSnapshot CaptureSnapshot()
        {
            return new CursorSnapshot
            {
                CursorFreeByDefault = _cursorFreeByDefault,
                CameraOrbitButton = _cameraOrbitButton,
                LockMode = _lockMode,
                CursorVisible = Cursor.visible
            };
        }
        
        public void Configure(InputParadigmProfile profile)
        {
            _cursorFreeByDefault = profile.cursorFreeByDefault;
            _cameraOrbitButton = profile.cameraOrbitButton;
            
            // Apply cursor state
            if (_cursorFreeByDefault)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                _lockMode = CursorLockMode.None;
            }
            else
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                _lockMode = CursorLockMode.Locked;
            }
        }
        
        public void Rollback(IConfigSnapshot snapshot)
        {
            var s = (CursorSnapshot)snapshot;
            _cursorFreeByDefault = s.CursorFreeByDefault;
            _cameraOrbitButton = s.CameraOrbitButton;
            _lockMode = s.LockMode;
            
            Cursor.visible = s.CursorVisible;
            Cursor.lockState = s.LockMode;
        }
        
        // ... rest of ICursorController implementation
    }
}
```

### Example: CameraController with Nested State Machine

For complex subsystems, they can have their own internal state machine:

```csharp
// Assets/Scripts/Core/Camera/CameraController.cs
namespace DIG.Core.Camera
{
    public enum CameraModeState
    {
        Orbit,          // Shooter: always orbit
        RMBOrbit,       // MMO: RMB to orbit
        Fixed,          // ARPG/MOBA: no orbit
        Transitioning   // Blending between modes
    }
    
    public class CameraController : MonoBehaviour, IParadigmConfigurable
    {
        public int ConfigurationOrder => 50;  // Configure before cursor
        public string SubsystemName => "CameraController";
        
        [SerializeField, ReadOnly] private CameraModeState _cameraState;
        
        private class CameraSnapshot : IConfigSnapshot
        {
            public CameraModeState State;
            public bool OrbitEnabled;
            public float CurrentYaw;
            public float CurrentPitch;
        }
        
        public bool CanConfigure(InputParadigmProfile profile, out string errorReason)
        {
            // Check if current camera type is in the profile's compatible list
            var currentCamera = CameraModeProvider.Instance?.ActiveCameraType ?? CameraType.ThirdPersonFollow;
            
            if (!profile.IsCompatibleWith(currentCamera))
            {
                errorReason = $"Camera type {currentCamera} not compatible with {profile.paradigm}";
                return false;
            }
            
            errorReason = null;
            return true;
        }
        
        public IConfigSnapshot CaptureSnapshot()
        {
            return new CameraSnapshot
            {
                State = _cameraState,
                OrbitEnabled = _orbitEnabled,
                CurrentYaw = _currentYaw,
                CurrentPitch = _currentPitch
            };
        }
        
        public void Configure(InputParadigmProfile profile)
        {
            switch (profile.paradigm)
            {
                case InputParadigm.Shooter:
                    _cameraState = CameraModeState.Orbit;
                    EnableContinuousOrbit(true);
                    break;
                    
                case InputParadigm.MMO:
                    _cameraState = CameraModeState.RMBOrbit;
                    EnableContinuousOrbit(false);
                    SetOrbitButton(MouseButton.Right);
                    break;
                    
                case InputParadigm.ARPG:
                case InputParadigm.MOBA:
                    _cameraState = CameraModeState.Fixed;
                    EnableContinuousOrbit(false);
                    break;
                    
                case InputParadigm.TwinStick:
                    _cameraState = CameraModeState.Fixed;
                    EnableContinuousOrbit(false);
                    break;
            }
        }
        
        public void Rollback(IConfigSnapshot snapshot)
        {
            var s = (CameraSnapshot)snapshot;
            _cameraState = s.State;
            _orbitEnabled = s.OrbitEnabled;
            _currentYaw = s.CurrentYaw;
            _currentPitch = s.CurrentPitch;
            
            // Restore camera state
            ApplySnapshot(s);
        }
        
        // ... camera-specific methods
    }
}
```

### Configuration Order Constants

Define ordering in one place for clarity:

```csharp
// Assets/Scripts/Core/Input/ConfigurationOrder.cs
namespace DIG.Core.Input
{
    /// <summary>
    /// Constants for IParadigmConfigurable ordering.
    /// Lower values configure first.
    /// </summary>
    public static class ConfigurationOrder
    {
        public const int Camera = 50;           // Camera must be first (others depend on it)
        public const int Cursor = 100;          // Cursor after camera
        public const int Movement = 150;        // Movement after cursor
        public const int Facing = 200;          // Facing after movement
        public const int UI = 300;              // UI last
        public const int Analytics = 400;       // Analytics/logging last
    }
}
```

### Transition Sequence Diagram

```
┌────────────────────────────────────────────────────────────────────────────────┐
│                    Full Transition: Shooter → MMO                              │
├────────────────────────────────────────────────────────────────────────────────┤
│                                                                                │
│   User Action: Click "MMO" in settings dropdown                                │
│                        │                                                       │
│                        ▼                                                       │
│   ┌──────────────────────────────────────────────────────────────────────────┐│
│   │ PHASE 1: GUARD CONDITIONS                                                ││
│   │                                                                          ││
│   │ 1. Check: _state != Transitioning                           ✓           ││
│   │ 2. Check: IsParadigmCompatible(MMO) - camera supports MMO   ✓           ││
│   │ 3. For each IParadigmConfigurable:                                       ││
│   │    a. CameraController.CanConfigure(MMO)                    ✓           ││
│   │    b. CursorController.CanConfigure(MMO)                    ✓           ││
│   │    c. MovementRouter.CanConfigure(MMO)                      ✓           ││
│   │    d. FacingController.CanConfigure(MMO)                    ✓           ││
│   │                                                                          ││
│   │ All guards passed → proceed                                              ││
│   └──────────────────────────────────────────────────────────────────────────┘│
│                        │                                                       │
│                        ▼                                                       │
│   ┌──────────────────────────────────────────────────────────────────────────┐│
│   │ PHASE 2: CAPTURE SNAPSHOTS                                               ││
│   │                                                                          ││
│   │ _state = Transitioning                                                   ││
│   │                                                                          ││
│   │ 1. CameraController.CaptureSnapshot()     → CameraSnapshot               ││
│   │ 2. CursorController.CaptureSnapshot()     → CursorSnapshot               ││
│   │ 3. MovementRouter.CaptureSnapshot()       → MovementSnapshot             ││
│   │ 4. FacingController.CaptureSnapshot()     → FacingSnapshot               ││
│   │                                                                          ││
│   │ All snapshots stored in dictionary                                       ││
│   └──────────────────────────────────────────────────────────────────────────┘│
│                        │                                                       │
│                        ▼                                                       │
│   S┌──────────────────────────────────────────────────────────────────────────┐│
│   │ PHASE 3: CONFIGURE (In Order)                                            ││
│   │                                                                          ││
│   │ 1. CameraController.Configure(MMO)                                       ││
│   │    - _cameraState = RMBOrbit                                             ││
│   │    - EnableContinuousOrbit(false)                                        ││
│   │    - SetOrbitButton(RMB)                                      ✓          ││
│   │                                                                          ││
│   │ 2. CursorController.Configure(MMO)                                       ││
│   │    - _cursorFreeByDefault = true                                         ││
│   │    - Cursor.visible = true                                               ││
│   │    - Cursor.lockState = None                                  ✓          ││
│   │                                                                          ││
│   │ 3. MovementRouter.Configure(MMO)                                         ││
│   │    - _wasdEnabled = true                                                 ││
│   │    - _clickToMoveEnabled = false                                         ││
│   │    - _adTurnByDefault = true                                  ✓          ││
│   │                                                                          ││
│   │ 4. FacingController.Configure(MMO)                                       ││
│   │    - _facingMode = MovementDirection                          ✓          ││
│   │                                                                          ││
│   │ All configurations succeeded!                                            ││
│   └──────────────────────────────────────────────────────────────────────────┘│
│                        │                                                       │
│                        ▼                                                       │
│   ┌──────────────────────────────────────────────────────────────────────────┐│
│   │ PHASE 4: FINALIZE                                                        ││
│   │                                                                          ││
│   │ _activeProfile = MMO                                                     ││
│   │ _state = Stable                                                          ││
│   │ SyncToECS()                                                              ││
│   │ OnParadigmChanged?.Invoke(MMO)                                           ││
│   │ OnTransitionCompleted?.Invoke(MMO, success: true)                        ││
│   │                                                                          ││
│   │ ✅ TRANSITION COMPLETE                                                   ││
│   └──────────────────────────────────────────────────────────────────────────┘│
│                                                                                │
└────────────────────────────────────────────────────────────────────────────────┘
```

### Rollback Sequence Diagram

```
┌────────────────────────────────────────────────────────────────────────────────┐
│                    Failed Transition with Rollback                             │
├────────────────────────────────────────────────────────────────────────────────┤
│                                                                                │
│   PHASE 3: CONFIGURE                                                           │
│                                                                                │
│   1. CameraController.Configure(ARPG)                             ✓           │
│   2. CursorController.Configure(ARPG)                             ✓           │
│   3. MovementRouter.Configure(ARPG)                               ✗ THROWS!   │
│      → Exception: "Navmesh agent not found"                                    │
│                                                                                │
│   ┌──────────────────────────────────────────────────────────────────────────┐│
│   │ ROLLBACK SEQUENCE (Reverse Order)                                        ││
│   │                                                                          ││
│   │ 1. CursorController.Rollback(cursorSnapshot)                             ││
│   │    - Restore cursor visibility                                           ││
│   │    - Restore lock mode                                        ✓          ││
│   │                                                                          ││
│   │ 2. CameraController.Rollback(cameraSnapshot)                             ││
│   │    - Restore orbit mode                                                  ││
│   │    - Restore yaw/pitch                                        ✓          ││
│   │                                                                          ││
│   │ (MovementRouter was never successfully configured, so skip)              ││
│   │ (FacingController was never reached, so skip)                            ││
│   │                                                                          ││
│   │ _state = Stable                                                          ││
│   │ OnTransitionCompleted?.Invoke(Shooter, success: false)                   ││
│   │                                                                          ││
│   │ ⚠️ TRANSITION FAILED - System restored to previous state                 ││
│   └──────────────────────────────────────────────────────────────────────────┘│
│                                                                                │
└────────────────────────────────────────────────────────────────────────────────┘
```

### Benefits Summary

| Benefit | Description |
|---------|-------------|
| **Atomic Transitions** | Either all subsystems configure or none do |
| **Explicit Order** | ConfigurationOrder ensures deterministic sequence |
| **Reentrancy Safe** | Transitioning state rejects concurrent transitions |
| **Pre-Validation** | CanConfigure checks catch issues before any changes |
| **Rollback Support** | Snapshot + Rollback enables recovery from failures |
| **Debuggable** | State, order, and phase are all explicit and logged |
| **Extensible** | New subsystems just implement IParadigmConfigurable |
| **Testable** | Each subsystem's Configure/Rollback is unit-testable |

### When to Use Which Pattern

| Scenario | Recommendation |
|----------|----------------|
| Simple 2-3 subsystems, all always succeed | Event-Based (simpler) |
| Many subsystems with dependencies | State Machine Coordinator |
| Failure recovery required | State Machine Coordinator |
| Order-sensitive configuration | State Machine Coordinator |
| Need transition animations/blending | State Machine + Transitioning state |
| Maximum debuggability | State Machine Coordinator |

---

## Testing Checklist

When implementing new paradigms:

- [ ] Mouse controls match paradigm spec
- [ ] WASD behavior correct (move, turn, or disabled)
- [ ] Character facing matches expected mode
- [ ] Camera responds correctly to controls
- [ ] Cursor visibility matches paradigm
- [ ] Abilities/attacks aim correctly
- [ ] Smooth transition when switching paradigms
- [ ] Mode overlays correctly modify base behavior
- [ ] Settings UI allows paradigm selection
- [ ] Controller/gamepad equivalent works
