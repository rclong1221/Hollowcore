# SETUP GUIDE: EPIC 15.20 - Input Paradigm Framework

## Overview

EPIC 15.20 implements a **paradigm-driven input system** that lets DIG switch between fundamentally different control schemes — Shooter, MMO, ARPG, MOBA, and Twin-Stick — with atomic transitions and automatic rollback. All behavior is data-driven through **InputParadigmProfile** ScriptableObjects.

---

## Quick Start

### Step 1: Generate Default Profiles

1. Go to **Tools > DIG > Input > Generate Default Paradigm Profiles**
2. This creates 7 profiles in `Assets/Data/Input/Profiles/`:

| Profile Asset | Paradigm | Key Traits |
|---------------|----------|------------|
| `Profile_Shooter.asset` | Shooter/Souls | Cursor locked, mouse orbits camera, WASD |
| `Profile_ShooterHybrid.asset` | Shooter (Hybrid) | Same as Shooter + Alt to free cursor |
| `Profile_MMO.asset` | MMO/RPG | Free cursor, RMB-hold to orbit, A/D turn |
| `Profile_ARPG_Classic.asset` | ARPG (Classic) | Click-to-move (LMB), no WASD |
| `Profile_ARPG_Hybrid.asset` | ARPG (Hybrid) | Click-to-move + WASD |
| `Profile_TwinStick.asset` | Twin-Stick | WASD move, character faces cursor |
| `Profile_MOBA.asset` | MOBA | Click-to-move (RMB), edge-pan, attack-move |

To open the folder later: **Tools > DIG > Input > Open Paradigm Profiles Folder**

### Step 2: Set Up ParadigmStateMachine

1. Find or create a persistent GameObject in your scene (e.g., `GameEntry`)
2. Add the **ParadigmStateMachine** component (or let it auto-create at runtime)
3. In the Inspector, assign:

| Field | Description |
|-------|-------------|
| **Default Profile** | The paradigm active on startup (e.g., `Profile_Shooter.asset`) |
| **Available Profiles** | Array of all profiles the player can switch between |
| **Log Transitions** | Enable to see paradigm switches in the Console |

### Step 3: Add InputParadigmState to Player Prefab

1. Open your **Player Prefab** (in the ECS subscene)
2. Add the **InputParadigmStateAuthoring** component
3. Configure:

| Field | Default | Description |
|-------|---------|-------------|
| **Default Paradigm** | Shooter | Starting paradigm for this entity |
| **Default Facing Mode** | CameraForward | Starting facing mode |

4. Bake the subscene

This creates the `InputParadigmState` ECS component, which the state machine keeps in sync.

---

## Paradigm Profile Configuration

Each **InputParadigmProfile** is a ScriptableObject you can create via **Assets > Create > DIG/Input/Input Paradigm Profile**.

Select any profile in the Project window to configure it in the Inspector:

### Identity

| Field | Type | Description |
|-------|------|-------------|
| **Paradigm** | Enum | `Shooter`, `MMO`, `ARPG`, `MOBA`, `TwinStick`, `SideScroller2D` |
| **Display Name** | string | Name shown in settings UI |
| **Description** | string (TextArea) | Description for settings UI |
| **Icon** | Sprite | Optional icon for settings UI |

### Cursor Behavior

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Cursor Free By Default** | bool | false | If true, cursor is visible and free (MMO, ARPG, MOBA, Twin-Stick) |
| **Temporary Cursor Free Key** | KeyCode | LeftAlt | Key to temporarily free cursor when locked (Shooter) |
| **Camera Orbit Mode** | Enum | AlwaysOrbit | `AlwaysOrbit` (Shooter), `ButtonHoldOrbit` (MMO), `KeyRotateOnly` (ARPG), `FollowOnly` (MOBA/Twin-Stick) |

### Movement

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **WASD Enabled** | bool | true | Enable direct WASD movement |
| **Click To Move Enabled** | bool | false | Enable click-to-move |
| **Click To Move Button** | Enum | None | `None`, `LeftButton` (ARPG), `RightButton` (MOBA) |
| **Use Pathfinding** | bool | false | If true, click-to-move routes through A* pathfinding |
| **Facing Mode** | Enum | CameraForward | `CameraForward` (Shooter), `MovementDirection` (MMO/ARPG/MOBA), `CursorDirection` (Twin-Stick), `TargetLocked`, `ManualTurn` |
| **AD Turns Character** | bool | false | If true, A/D rotate character (MMO walk). If false, A/D strafe |
| **Use Screen Relative Movement** | bool | false | If true, WASD moves in fixed screen directions (isometric). If false, relative to camera (TPS/FPS) |

### Camera

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **QE Rotation Enabled** | bool | false | Enable Q/E camera rotation (ARPG isometric) |
| **Edge Pan Enabled** | bool | false | Enable MOBA-style screen-edge camera panning |
| **Scroll Zoom Enabled** | bool | true | Enable scroll wheel zoom |

### Camera Compatibility

| Field | Type | Description |
|-------|------|-------------|
| **Compatible Camera Modes** | CameraMode[] | Which camera modes work with this paradigm. Auto-populated on save based on paradigm type |

---

## Click-to-Move Setup (ARPG / MOBA)

The **ClickToMoveHandler** MonoBehaviour manages pathing for click-to-move paradigms. It auto-creates at runtime when a click-to-move profile is active, but you can also place it in the scene manually for tuning.

### Scene Setup

1. Add `ClickToMoveHandler` to a persistent GameObject (or let it auto-create)
2. Configure in the Inspector:

| Field | Default | Description |
|-------|---------|-------------|
| **Waypoint Reach Distance** | 0.5 | Distance at which intermediate waypoints are considered reached |
| **Destination Reach Distance** | 0.3 | Distance at which the final destination is considered reached |
| **Max Raycast Distance** | 200 | Maximum raycast distance for ground detection |
| **Ground Layers** | Everything | LayerMask for walkable ground surfaces |
| **Repath Interval** | 0.2 | Seconds between repath requests while holding click (hold-to-move) |
| **Log Path Events** | false | Console logging for path calculations |
| **Draw Path Gizmos** | false | Draw path in Scene view (cyan lines, yellow = current waypoint, green = destination) |

### A* Pathfinding Integration

If the profile has **Use Pathfinding = true**, the handler routes through A* Pathfinding Project:

1. Ensure `AstarPath` MonoBehaviour exists in the scene
2. Configure a `RecastGraph` or `GridGraph` for your terrain
3. The handler automatically uses `ABPath.Construct()` when A* is available
4. Falls back to direct movement if no A* graph is configured

### Behavior by Profile

| Profile | Click Button | Pathfinding | Hold-to-Move |
|---------|-------------|-------------|--------------|
| ARPG Classic | LMB | Yes (if A* present) | Yes (LMB hold continuously repaths) |
| ARPG Hybrid | LMB | Yes (if A* present) | Yes |
| MOBA | RMB | Yes (if A* present) | Yes (RMB hold continuously repaths) |

### WASD Interruption

If the profile has **WASD Enabled = true** (ARPG Hybrid), pressing any WASD key immediately cancels the active path and switches to direct movement.

---

## MOBA Paradigm Setup (Phase 4a)

The MOBA paradigm adds attack-move, edge-pan camera, and camera lock toggle on top of click-to-move.

### Action Map: Combat_MOBA

The `Combat_MOBA` action map is already configured in `DIGInputActions.inputactions`. It enables automatically when the MOBA paradigm is active.

| Action | Default Binding | Description |
|--------|----------------|-------------|
| **AttackMove** | `A` | Enter attack-move mode (press A, then click) |
| **Stop** | `S` | Cancel all movement and actions |
| **HoldPosition** | `H` | Stop moving but attack enemies in range |
| **CameraLockToggle** | `Y` | Toggle camera lock on/off |
| **AttackAtCursor** | `LMB` | Attack target under cursor / confirm attack-move click |

To rebind these keys, open `DIGInputActions.inputactions` and edit the `Combat_MOBA` map bindings.

### AttackMoveHandler

Add `AttackMoveHandler` to a persistent GameObject (or let it auto-create). It only activates when the MOBA profile is active.

| Field | Default | Description |
|-------|---------|-------------|
| **Acquisition Range** | 10 | Range (world units) to scan for hostile entities while attack-moving |
| **Scan Interval** | 0.25 | Seconds between enemy proximity scans |
| **Log Events** | false | Console logging for state machine transitions |

**State machine flow:**
1. Press `A` to enter attack-move mode (cursor should change — visual feedback deferred)
2. Click a location to path there while scanning for enemies
3. If a hostile entity enters acquisition range, movement pauses and the player engages
4. Press `S` to cancel everything, `H` to hold position (attack in range only)

### TeamComponent (Hostility Detection)

AttackMoveHandler detects enemies via `TeamComponent` on ECS entities.

- **TeamId = 0**: Neutral (never hostile)
- **TeamId 1+**: Hostile to all other non-zero, non-matching teams

To make an entity targetable by attack-move, add a `TeamComponent` to it via code or a custom authoring component. No built-in authoring component exists yet — add `TeamComponent` directly via `EntityManager.AddComponentData()` or create a baker for your NPC/enemy prefab.

### Edge-Pan Camera

Edge-pan is configured through the **CameraConfig** ScriptableObject attached to the camera system.

1. Select your CameraConfig asset (created via **Assets > Create > DIG/Camera/Camera Config**)
2. Find the **Edge Pan (MOBA)** section:

| Field | Range | Default | Description |
|-------|-------|---------|-------------|
| **Edge Pan Speed** | 1–50 | 15 | Camera pan speed in units/second |
| **Edge Pan Margin** | 5–100 | 30 | Screen-edge margin in pixels that triggers panning |
| **Edge Pan Max Offset** | 5–50 | 25 | Maximum distance camera can pan from the player |

Edge-pan activates when:
- The MOBA profile is active (profile has `edgePanEnabled = true`)
- Camera lock is OFF (press `Y` to toggle)
- Move cursor to screen edges to pan

### Camera Lock Toggle

- Default state: **Locked** (camera follows player, no edge-pan)
- Press `Y` to unlock: camera stays put, edge-pan activates
- Press `Y` again to re-lock: camera snaps back to player, pan offset resets

### Top-Down Camera Settings

The MOBA paradigm uses the TopDownFixed camera mode. Configure in CameraConfig:

| Field | Range | Default | Description |
|-------|-------|---------|-------------|
| **Top Down Angle** | 60–90 | 85 | Camera pitch (90 = straight down, 85 = slight tilt for depth) |
| **Top Down Height** | — | 20 | Height above the character |

---

## Twin-Stick Paradigm Setup (Phase 4b)

The Twin-Stick paradigm uses WASD for movement while the character always faces the cursor.

### How It Works

- **Movement**: WASD moves in fixed screen directions (screen-relative)
- **Facing**: Character always faces cursor when attacking; faces movement direction when walking
- **Animation**: While walking (not attacking), the character always appears to move forward regardless of WASD direction. While attacking, proper strafe/backpedal animations play based on movement relative to facing
- **Weapon Aim**: `TargetData.AimDirection` is automatically set from the character's current rotation, feeding the weapon fire system

### Profile Settings

The `Profile_TwinStick.asset` is pre-configured with:
- `wasdEnabled = true`
- `clickToMoveEnabled = false`
- `facingMode = CursorDirection`
- `useScreenRelativeMovement = true`
- `cameraOrbitMode = FollowOnly`

No additional scene components are needed beyond the standard paradigm setup.

---

## NPC Pathfinding Setup (Phase 4c)

Scaffolding for A*-driven NPC pathfinding. The actual AI decision-making (target selection, aggro, patrol) is deferred to the combat system EPIC. This provides the pathfinding infrastructure.

### NPCPathfindingBridge (Scene Setup)

1. Add `NPCPathfindingBridge` to a persistent GameObject
2. Configure:

| Field | Default | Description |
|-------|---------|-------------|
| **Waypoint Reach Distance** | 0.5 | Distance at which NPC waypoints are considered reached |
| **Log Events** | false | Console logging for path requests and completions |

This singleton manages all NPC path requests through A*.

### NPCPathfindingAuthoring (Prefab Setup)

1. Open your **NPC Prefab** (in the ECS subscene)
2. Add the **NPCPathfindingAuthoring** component
3. Configure per-NPC settings:

| Field | Default | Description |
|-------|---------|-------------|
| **Repath Interval** | 1.0 | How often the NPC recalculates its path (seconds) |
| **Stopping Distance** | 0.5 | Distance at which NPC considers itself at destination |
| **Max Speed** | 5.0 | Maximum movement speed for this NPC |

4. Bake the subscene

This creates an `NPCPathfindingTag` component on the entity. The `NPCPathfindingBridge` picks up any entity with this tag.

### Prerequisites

- `AstarPath` MonoBehaviour must exist in the scene with a configured graph
- NPC entities need a `TeamComponent` if they should be targetable by attack-move
- AI systems (not yet implemented) call `NPCPathfindingBridge.Instance.RequestPath()` to start pathing

---

## Scene Architecture Checklist

Ensure these components exist in your scene for full paradigm support:

### Persistent GameObjects

| Component | Required For | Auto-Creates? |
|-----------|-------------|---------------|
| **ParadigmStateMachine** | All paradigms | Yes |
| **ParadigmInputManager** | All paradigms (action map switching) | Yes |
| **CursorController** | All paradigms (cursor lock/free) | Yes |
| **CameraOrbitController** | All paradigms (orbit mode) | Yes |
| **MovementRouter** | All paradigms (WASD/click routing) | Yes |
| **FacingController** | All paradigms (character facing) | Yes |
| **ClickToMoveHandler** | ARPG, MOBA | Yes (when profile enables it) |
| **AttackMoveHandler** | MOBA | Yes (when MOBA profile active) |
| **NPCPathfindingBridge** | NPC pathfinding | No — add manually |
| **AstarPath** | Pathfinding (ARPG, MOBA, NPC) | No — add manually and configure graph |

### Player Prefab (ECS Subscene)

| Authoring Component | Purpose |
|---------------------|---------|
| **InputParadigmStateAuthoring** | Syncs paradigm state to ECS |

### NPC Prefab (ECS Subscene)

| Authoring Component | Purpose |
|---------------------|---------|
| **NPCPathfindingAuthoring** | Tags NPC for A* pathfinding |

---

## Input Action Maps

The paradigm system manages these action maps (in `DIGInputActions.inputactions`):

| Map | Active When | Key Actions |
|-----|-------------|-------------|
| **Core** | Always | Movement, Jump, Interact, Look, Sprint |
| **Combat_Shooter** | Shooter paradigm | Attack, Block, Aim, LockOn, Dodge |
| **Combat_MMO** | MMO paradigm | Attack, Block, Interact, Tab-Target |
| **Combat_ARPG** | ARPG paradigm | AttackAtCursor, UseSkill, Interact |
| **Combat_MOBA** | MOBA paradigm | AttackMove, Stop, HoldPosition, CameraLockToggle, AttackAtCursor |
| **UI** | Always (menus) | Navigation, Submit, Cancel |

Only one Combat map is active at a time. Switching paradigms automatically enables the correct map.

---

## Subsystem Registration Order

Subsystems configure themselves in this order during paradigm transitions. If you create a custom `IParadigmConfigurable`, choose an order that fits:

| Order | Subsystem | Responsibility |
|-------|-----------|---------------|
| 0 | CursorController | Cursor lock/free state |
| 10 | CameraOrbitController | Camera orbit mode, edge-pan flag |
| 100 | MovementRouter | WASD enable, click-to-move routing |
| 110 | ClickToMoveHandler | Click-to-move + pathfinding |
| 115 | AttackMoveHandler | Attack-move state machine (MOBA) |
| 200 | FacingController | Character facing mode |

---

## Troubleshooting

### Paradigm Not Switching
1. Check console for compatibility errors from subsystems
2. Verify the target camera mode is in the profile's **Compatible Camera Modes** array
3. Right-click `ParadigmStateMachine` > **Log Registered Subsystems** to verify all subsystems registered

### Cursor Stuck (Locked or Free)
1. Check `CursorController.IsCursorFree` at runtime
2. Verify `ParadigmStateMachine` has the correct active profile
3. Check if a UI menu is open (`MenuState.IsAnyMenuOpen()` overrides cursor to free)

### A/D Not Turning (MMO)
1. Verify the MMO profile has `adTurnsCharacter = true`
2. Confirm `InputParadigmState.ADTurnsCharacter` is synced on the entity
3. RMB must NOT be held (holding RMB forces strafe mode)

### Click-to-Move Not Working
1. Verify the profile has `clickToMoveEnabled = true` and correct `clickToMoveButton`
2. Check that `ClickToMoveHandler` exists (inspect Console for creation logs)
3. Verify **Ground Layers** mask includes your terrain layer
4. If using pathfinding, ensure `AstarPath` is in the scene with a scanned graph

### Edge-Pan Not Working (MOBA)
1. Verify the MOBA profile has `edgePanEnabled = true`
2. Camera must be **unlocked** (press `Y` to toggle)
3. Check CameraConfig has non-zero **Edge Pan Speed** and **Edge Pan Margin**
4. Verify `CameraOrbitController.Instance.IsEdgePanEnabled` is true at runtime

### Attack-Move Not Triggering
1. Verify you're in the MOBA paradigm (Combat_MOBA map must be active)
2. Press `A` first, then click — check Console logs with `Log Events` enabled
3. Hostile entities need a `TeamComponent` with a different non-zero TeamId from the player

### Twin-Stick Animations Wrong
1. Ensure the profile has `useScreenRelativeMovement = true`
2. The "always forward" animation relies on `PlayerMovementSystem` being the sole rotation authority in screen-relative mode
3. `PlayerFacingSystem` must NOT modify rotation when screen-relative is active

### Path Gizmos Not Showing
1. Enable **Draw Path Gizmos** on `ClickToMoveHandler`
2. Gizmos are only visible in the Scene view, not Game view
3. Must have an active path (click to move first)

---

## What's Implemented

- [x] State Machine Coordinator with atomic transitions and rollback
- [x] All subsystem controllers (Cursor, Camera Orbit, Movement, Facing)
- [x] ECS state sync (`InputParadigmState`)
- [x] Profile generator tool (7 default profiles)
- [x] Shooter paradigm (camera-relative, always orbit)
- [x] MMO paradigm (RMB orbit, A/D turn/strafe)
- [x] ARPG paradigm (click-to-move with LMB, pathfinding)
- [x] MOBA paradigm (click-to-move RMB, attack-move, edge-pan, camera lock)
- [x] Twin-Stick paradigm (WASD + cursor aim, weapon fire direction)
- [x] Click-to-move with A* pathfinding bridge
- [x] NPC pathfinding scaffolding
- [x] Combat_MOBA action map
- [x] TeamComponent for hostility detection

## What's Not Implemented

- [ ] Vehicle/Mount mode overlay (Phase 5)
- [ ] Build/Placement mode overlay (Phase 5)
- [ ] Visual movement target indicator (click destination decal/VFX)
- [ ] Attack-move cursor visual feedback
- [ ] TeamComponent authoring component (currently added via code only)
- [ ] Full AI integration for NPC pathfinding (deferred to combat EPIC)

---

## Related EPICs

| EPIC | Relationship |
|------|--------------|
| 15.18 | Tier 1 implementation (ShooterDirect, HybridToggle) |
| 15.21 | Input Action Layer & Keybind UI |
| 14.9 | CursorAimTargeting, camera utilities |
| 15.16 | Target lock integration |

---

## File Structure

```
Assets/Scripts/Core/Input/Paradigm/
├── InputParadigm.cs                    # Enums (paradigm, facing, orbit, etc.)
├── InputParadigmProfile.cs             # ScriptableObject (profile data)
├── ParadigmStateMachine.cs             # Coordinator singleton
│
├── Interfaces/
│   ├── IInputParadigmProvider.cs
│   ├── IParadigmConfigurable.cs
│   ├── ICursorController.cs
│   ├── ICameraOrbitController.cs
│   ├── IMovementRouter.cs
│   └── IFacingController.cs
│
├── Subsystems/
│   ├── CursorController.cs             # Order 0
│   ├── CameraOrbitController.cs        # Order 10
│   ├── MovementRouter.cs               # Order 100
│   └── FacingController.cs             # Order 200
│
├── Pathfinding/
│   ├── ClickToMoveHandler.cs           # Order 110, click-to-move + A* bridge
│   ├── AttackMoveHandler.cs            # Order 115, MOBA attack-move
│   ├── NPCPathfindingAuthoring.cs      # ECS baker for NPC prefabs
│   └── NPCPathfindingBridge.cs         # Managed NPC path manager
│
├── Components/
│   └── InputParadigmState.cs           # ECS component
│
├── Authoring/
│   └── InputParadigmStateAuthoring.cs  # ECS baker for player prefab
│
└── Editor/
    └── ParadigmProfileGenerator.cs     # Profile creation tool

Assets/Scripts/Shared/
└── TeamComponent.cs                    # ECS team/faction component

Assets/Scripts/Camera/
├── CameraConfig.cs                     # Edge-pan, top-down settings
└── Implementations/
    └── TopDownFixedCamera.cs           # Edge-pan + camera lock logic

Assets/Settings/Input/
└── DIGInputActions.inputactions        # Contains Combat_MOBA action map
```
