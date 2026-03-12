# EPIC14.14 - Blitz Mount System and First Person Arms Integration

**Status:** Implemented (Code Complete)
**Dependencies:** EPIC14.12 (Agility Pack), EPIC14.13 (Swimming Pack), ViewMode system
**Goal:** Integrate Blitz as a rideable mount with full ECS networking, plus first-person arms for all view modes.

> **Note:** All implementation code is documented in this EPIC. Create the files from the code blocks below, then follow the Unity setup guide (`SETUP_GUIDE_14.14.md`) for prefab configuration.

---

## Overview

This EPIC covers integrating four major features into DIG:

1. **Blitz Character** - Opsive's robot/mech character (NOT a horse) with unique locomotion
2. **Ride System** - ECS-based mount/dismount with networked state replication
3. **FirstPersonArmsDemo.controller** - Standard first-person arms for normal gameplay
4. **SwimmingFirstPersonArmsDemo.controller** - First-person arms for swimming scenarios

**Important Clarifications:**
- **Blitz is a robot/mech**, not a horse. However, the ride system works with any `Rideable` object.
- Opsive provides `Ride.cs` (player ability) and `Rideable.cs` (mount ability) which we must port to ECS.
- We disabled `AnimatorMonitor` for player locomotion; the same pattern applies to Blitz when player-controlled.

DIG already has a `ViewMode` system (`IViewModeHandler`, `CameraViewType`) that supports first-person rendering. This EPIC connects Opsive's first-person arm models and controllers to that system.

**Source Locations (OPSIVE):**
- `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Models/Characters/Blitz/Blitz.fbx`
- `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Models/Characters/Atlas/AtlasFirstPersonArms.fbx`
- `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Animator/Characters/BlitzDemo.controller`
- `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/Character/Abilities/Ride.cs` (reference)
- `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Runtime/Character/Abilities/Rideable.cs` (reference)

**Target Locations (Already Copied):**
- `Assets/Art/Animations/Opsive/Animator/Characters/BlitzDemo.controller` ✅
- `Assets/Art/Animations/Opsive/Animator/Characters/FirstPersonArmsDemo.controller` ✅
- `Assets/Art/Animations/Opsive/AddOns/Swimming/Animator/SwimmingFirstPersonArmsDemo.controller` ✅
- `Assets/Art/Animations/Opsive/Demo/Blitz/*.fbx` ✅ (animations copied)
- `Assets/Art/Animations/Opsive/Demo/Abilities/Ride/*.fbx` ✅ (ride anims copied)

**Assets Still Needed:**
- `Blitz.fbx` model (not just animations)
- `AtlasFirstPersonArms.fbx` or `RheaFirstPersonArms.fbx` models
- Materials/Textures for Blitz character
- First-person arm materials/textures

---

## Current Architecture

### ViewMode System (Already Implemented)

```
┌─────────────────────────────────────────────────────────────────┐
│ Assets/Scripts/Items/Interfaces/IViewModeHandler.cs             │
├─────────────────────────────────────────────────────────────────┤
│ public enum ViewMode                                            │
│ {                                                               │
│     ThirdPerson = 0,                                            │
│     FirstPerson = 1,           ← First Person Arms use this     │
│     FirstPersonFullBody = 2,   ← Full body visible in FP        │
│     VR = 3,                                                     │
│     Spectator = 4,                                              │
│     UI = 5                                                      │
│ }                                                               │
│                                                                 │
│ interface IViewModeHandler                                      │
│   - CurrentMode                                                 │
│   - OnViewModeChanged(newMode)                                  │
│   - RenderEquipment(slotId, itemPrefab)                         │
│   - HideEquipment(slotId)                                       │
│   - GetAttachPoint(slotId)                                      │
│   - SupportsSlot(slotId)                                        │
│   - Initialize(characterRoot)                                   │
└─────────────────────────────────────────────────────────────────┘
```

### CameraViewType (ECS Side)

```
┌─────────────────────────────────────────────────────────────────┐
│ Assets/Scripts/Player/Components/CameraViewSettings.cs          │
├─────────────────────────────────────────────────────────────────┤
│ public enum CameraViewType                                      │
│ {                                                               │
│     Combat = 0,     // Third Person (Shoulder/Orbit)            │
│     Adventure = 1,  // Third Person (Free Orbit)                │
│     FirstPerson = 2, ← Switch to this for FP Arms              │
│     TopDown = 3,                                                │
│     PointClick = 4                                              │
│ }                                                               │
└─────────────────────────────────────────────────────────────────┘
```

### Opsive First Person System (Reference)

Opsive's first-person controller uses:

```
┌─────────────────────────────────────────────────────────────────┐
│ FirstPersonObjects.cs                                           │
├─────────────────────────────────────────────────────────────────┤
│ - Manages first-person arm visibility                           │
│ - Handles arm IK and positioning                                │
│ - Syncs with third-person character state                       │
│                                                                 │
│ FirstPersonPerspectiveItem.cs                                   │
│ - First-person weapon rendering                                 │
│ - Item-specific arm positioning                                 │
│                                                                 │
│ FirstPersonBaseObject.cs                                        │
│ - Base class for FP visible objects                             │
│ - Pivot and offset management                                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Relationship to Other EPICs

| EPIC | Connection |
|------|------------|
| **14.9** | Camera system provides CameraViewType.FirstPerson support |
| **14.12** | Agility animations may need FP arm variants |
| **14.13** | Swimming uses SwimmingFirstPersonArmsDemo.controller |
| **13.21** | CameraViewConfig controls view switching |

---

## Asset Inventory

### Controllers (Already Copied ✅)

| Controller | Location | Purpose |
|------------|----------|---------|
| BlitzDemo.controller | `Assets/Art/Animations/Opsive/Animator/Characters/` | Blitz locomotion (ride mount) |
| FirstPersonArmsDemo.controller | `Assets/Art/Animations/Opsive/Animator/Characters/` | Normal FP arms |
| SwimmingFirstPersonArmsDemo.controller | `Assets/Art/Animations/Opsive/AddOns/Swimming/Animator/` | Swimming FP arms |

### Masks (Already Copied ✅)

| Mask | Location | Purpose |
|------|----------|---------|
| FirstPersonLeftArmMask.mask | `Assets/Art/Animations/Opsive/Animator/Characters/` | Left arm only |
| FirstPersonRightArmMask.mask | `Assets/Art/Animations/Opsive/Animator/Characters/` | Right arm only |
| FirstPersonUpperbodyMask.mask | `Assets/Art/Animations/Opsive/Animator/Characters/` | Upper body |

### Animations (Already Copied ✅)

| Animation Set | Location | Status |
|---------------|----------|--------|
| Blitz locomotion | `Assets/Art/Animations/Opsive/Demo/Blitz/` | ✅ 13 animations |
| Shooter items | `Assets/Art/Animations/Opsive/Demo/Items/Shooter/` | ✅ 269 animations |
| Melee items | `Assets/Art/Animations/Opsive/Demo/Items/Melee/` | ✅ Multiple weapons |

### Models (Need to Copy ❌)

| Model | Source | Target |
|-------|--------|--------|
| Blitz.fbx | `Assets/OPSIVE/.../Models/Characters/Blitz/` | `Assets/Art/Models/Characters/Blitz/` |
| AtlasFirstPersonArms.fbx | `Assets/OPSIVE/.../Models/Characters/Atlas/` | `Assets/Art/Models/Characters/FirstPersonArms/` |
| RheaFirstPersonArms.fbx | `Assets/OPSIVE/.../Models/Characters/Rhea/` | `Assets/Art/Models/Characters/FirstPersonArms/` |

### Materials/Textures (Need to Copy ❌)

| Asset Type | Source | Target |
|------------|--------|--------|
| Blitz materials | `Assets/OPSIVE/.../Materials/Characters/Blitz/` | `Assets/Art/Materials/Characters/Blitz/` |
| Blitz textures | `Assets/OPSIVE/.../Textures/Characters/Blitz/` | `Assets/Art/Textures/Characters/Blitz/` |
| Atlas materials | `Assets/OPSIVE/.../Materials/Characters/Atlas/` | `Assets/Art/Materials/Characters/Atlas/` |

---

## Weapon Animation Inventory

All weapon animations have been copied. Here's the complete inventory:

### Shooter Weapons (All ✅)

| Weapon | Count | Includes |
|--------|-------|----------|
| **AssaultRifle** | ~30 | Idle, Aim, Fire, Reload (Low/Mid/High), Equip, Unequip, Crouch, Melee, Run |
| **Bow** | ~39 | + Ride variants |
| **DualPistols** | ~31 | Standard set |
| **Pistol** | ~39 | Standard set |
| **RocketLauncher** | ~38 | Standard set |
| **Shotgun** | ~43 | + Pump action |
| **SniperRifle** | ~40 | Standard set |

### Melee Weapons (All ✅)

| Weapon | Count | Includes |
|--------|-------|----------|
| **Sword** | ~66 | Attack combos, Block, Ride variants |
| **Katana** | ~65 | Attack combos, Ride variants |
| **Knife** | ~59 | Attack combos, Ride variants |
| **Shield** | ~72 | SwordShield combo, Ride variants |
| **Body** | ~21 | Unarmed, Ride variants |

### FirstPerson Variants

Each weapon folder contains a `FirstPerson/` subfolder with FP-specific animations.

---

## Ride System (ECS Networked)

### Opsive Reference Architecture

Opsive's ride system uses two abilities that work together:

```
┌─────────────────────────────────────────────────────────────────┐
│ Opsive MonoBehaviour Ride System (Reference Only)               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Player Character                    Rideable Object (Blitz)    │
│ ├── Ride.cs (Ability)       ←───→   ├── Rideable.cs (Ability)  │
│ │   - DetectObjectAbilityBase       │   - Ability              │
│ │   - RideState enum:               │   - Tracks rider         │
│ │     Mount, Ride, Dismount         │   - Ignores collisions   │
│ │   - AbilityIntData:               │   - Parents player       │
│ │     1=MountLeft, 2=MountRight     │                          │
│ │     3=Riding, 4/5=Dismount        │                          │
│                                                                 │
│ Animation Events:                                               │
│   OnAnimatorRideMount → Player attached to mount                │
│   OnAnimatorRideDismount → Player detached from mount           │
└─────────────────────────────────────────────────────────────────┘
```

### DIG ECS Ride System (New)

```
┌─────────────────────────────────────────────────────────────────┐
│ ECS Components                                                  │
├─────────────────────────────────────────────────────────────────┤
│ RideState : IComponentData (on Player)                          │
│   [GhostField] bool IsRiding                                    │
│   [GhostField] Entity MountEntity                               │
│   [GhostField] int RidePhase (0=None,1=Mount,2=Ride,3=Dismount) │
│   [GhostField] bool MountFromLeft                               │
│   float MountProgress                                           │
│                                                                 │
│ RideableState : IComponentData (on Mount/Blitz)                 │
│   [GhostField] bool HasRider                                    │
│   [GhostField] Entity RiderEntity                               │
│   bool CanBeRidden                                              │
│   float3 MountOffsetLeft                                        │
│   float3 MountOffsetRight                                       │
│   float3 SeatOffset                                             │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│ ECS Systems                                                     │
├─────────────────────────────────────────────────────────────────┤
│ RideMountDetectionSystem                                        │
│   - Detects nearby rideables                                    │
│   - Checks CanMount conditions                                  │
│   - Starts mount on input (E key or similar)                    │
│                                                                 │
│ RideMountingSystem                                              │
│   - Handles mount animation phase                               │
│   - Parents player to mount at animation event                  │
│   - Disables player physics during ride                         │
│                                                                 │
│ RideControlSystem                                               │
│   - Redirects player input to mount when riding                 │
│   - Handles dismount input                                      │
│                                                                 │
│ RideDismountSystem                                              │
│   - Handles dismount animation                                  │
│   - Restores player physics                                     │
│   - Clears ride state                                           │
│                                                                 │
│ RideAnimationStateSystem                                        │
│   - Sets AbilityIndex = 401 (ABILITY_RIDE)                      │
│   - Sets AbilityIntData based on RidePhase                      │
└─────────────────────────────────────────────────────────────────┘
```

### Ride AbilityIndex Values

From Opsive's `Ride.cs`:

| RidePhase | AbilityIntData | Description |
|-----------|----------------|-------------|
| Mount (Left) | 1 | Mounting from left side |
| Mount (Right) | 2 | Mounting from right side |
| Ride | 3 | Actively riding |
| Dismount (Left) | 4 | Dismounting to left |
| Dismount (Right) | 5 | Dismounting to right |
| Complete | 6 | Dismount complete |

### AnimatorMonitor Considerations

**Problem:** We disabled `AnimatorMonitor` for player to use ECS-driven animation.

**Solutions:**

| Scenario | Approach |
|----------|----------|
| **Player while riding** | Continue using ECS → ClimbAnimatorBridge sets AbilityIndex=401 |
| **Blitz locomotion** | New BlitzAnimatorBridge driven by mount input |
| **Blitz AI mode** | Could use AnimatorMonitor OR ECS-driven |
| **Weapon animations** | Already handled by item animator system |

The key is that `ClimbAnimatorBridge` already handles `AbilityIndex` for all abilities. We just need to add ride ability constants and the bridge will work.

---

## Blitz Character Integration

### What is Blitz?

Blitz is Opsive's robot/mech character used in their ride system demo. It has:
- Unique locomotion animations (BlitzIdle, BlitzRun, BlitzWalk, etc.)
- Mount/dismount animations
- Turn-in-place animations

**Blitz is NOT a horse.** It's a robotic/mechanical mount. The ride system works with any `Rideable` object.

### Use Cases in DIG

1. **Rideable Vehicle/Mech** - Mount Blitz like a vehicle (primary use case)
2. **AI Companion** - Robot companion that follows player
3. **Alternative Player Character** - Robot character option

### BlitzDemo.controller Analysis

The BlitzDemo.controller likely contains:
- Locomotion blend tree (idle, walk, run)
- Turn animations (idle turn left/right, run turn left/right)
- Mount response states (when player mounts)

**Required Parameters:**
- `HorizontalMovement` (float)
- `ForwardMovement` (float)
- `Moving` (bool)
- `Speed` (float)
- `AbilityIndex` (int) - for mount-specific states
- `AbilityIntData` (int)

### Blitz Prefab Structure

```
Blitz_Client (Prefab Root)
├── Blitz (Model)
│   ├── Armature
│   │   ├── Root
│   │   │   ├── Hips
│   │   │   │   └── ... bones ...
│   │   │   └── SeatBone (player attachment point)
│   └── Mesh renderers
├── Components
│   ├── Animator (BlitzDemo.controller)
│   ├── BlitzAnimatorBridge (new - drives from ECS)
│   ├── RideableAuthoring (new - bakes RideableState)
│   ├── Collider
│   └── GhostAuthoringComponent (for networking)
├── MountPoints
│   ├── MountLeft (transform)
│   ├── MountRight (transform)
│   └── SeatPosition (transform)
```

---

## First Person Arms Integration

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ Player Character                                                │
├─────────────────────────────────────────────────────────────────┤
│ ├── ThirdPersonModel (visible in 3P, hidden in FP)              │
│ │   └── Atlas/Rhea/Custom model                                 │
│ │                                                               │
│ ├── FirstPersonArms (hidden in 3P, visible in FP)               │
│ │   ├── AtlasFirstPersonArms model                              │
│ │   ├── Animator (FirstPersonArmsDemo.controller)               │
│ │   ├── FirstPersonArmsBridge (new)                             │
│ │   └── Weapon attach points                                    │
│ │       ├── RightHand/Items/                                    │
│ │       └── LeftHand/Items/                                     │
│ │                                                               │
│ └── FirstPersonArmsSwimming (for swimming only)                 │
│     ├── Animator (SwimmingFirstPersonArmsDemo.controller)       │
│     └── FirstPersonArmsSwimmingBridge (new)                     │
└─────────────────────────────────────────────────────────────────┘
```

### First Person Arms Bridge

New MonoBehaviour to sync FP arms with player state:

```csharp
public class FirstPersonArmsBridge : MonoBehaviour
{
    [Header("References")]
    public Animator armsAnimator;
    public Transform rightHandItems;
    public Transform leftHandItems;

    [Header("Visibility")]
    public GameObject thirdPersonModel;
    public GameObject firstPersonArms;
    public GameObject swimmingFirstPersonArms;

    // Animator Parameters (same as Opsive)
    public string ParamHorizontalMovement = "HorizontalMovement";
    public string ParamForwardMovement = "ForwardMovement";
    public string ParamSpeed = "Speed";
    public string ParamMoving = "Moving";
    public string ParamAbilityIndex = "AbilityIndex";
    public string ParamAbilityChange = "AbilityChange";
    public string ParamAbilityIntData = "AbilityIntData";
    public string ParamSlotXItemID = "Slot0ItemID";

    // View mode management
    public void SetViewMode(ViewMode mode)
    {
        bool isFirstPerson = mode == ViewMode.FirstPerson;
        thirdPersonModel.SetActive(!isFirstPerson);
        firstPersonArms.SetActive(isFirstPerson && !isSwimming);
        swimmingFirstPersonArms.SetActive(isFirstPerson && isSwimming);
    }
}
```

### Swimming First Person Arms

The `SwimmingFirstPersonArmsDemo.controller` extends the normal FP arms with swimming states:

| Feature | Normal FP Arms | Swimming FP Arms |
|---------|----------------|------------------|
| Base locomotion | ✅ | ✅ |
| Weapon handling | ✅ | ❌ (arms only) |
| Swimming strokes | ❌ | ✅ |
| Surface/Underwater | ❌ | ✅ |

**Switch Logic:**
```csharp
// In FirstPersonArmsBridge or SwimmingEventListener:
void OnSwimmingStateChanged(bool isSwimming)
{
    if (currentViewMode == ViewMode.FirstPerson)
    {
        firstPersonArms.SetActive(!isSwimming);
        swimmingFirstPersonArms.SetActive(isSwimming);
    }
}
```

---

## Implementation Plan

### Phase 1: Asset Copy (Manual Unity Work)

- [x] Copy `Blitz.fbx` to `Assets/Art/Models/Characters/Blitz/`
- [x] Copy Blitz materials to `Assets/Art/Materials/Characters/Blitz/`
- [x] Copy Blitz textures to `Assets/Art/Textures/Characters/Blitz/`
- [x] Copy `AtlasFirstPersonArms.fbx` to `Assets/Art/Models/Characters/FirstPersonArms/`
- [x] Copy `RheaFirstPersonArms.fbx` to `Assets/Art/Models/Characters/FirstPersonArms/`
- [x] Copy Atlas/Rhea arm materials and textures
- [x] Verify all material references are correct after copy

### Phase 2: Ride System ECS Components

- [x] Create `RideState.cs` ECS component with GhostFields
- [x] Create `RideableState.cs` ECS component
- [x] Create `RideEvents.cs` for one-frame event flags
- [x] Add `RideAuthoring.cs` for player prefab
- [x] Add `RideableAuthoring.cs` for mount prefabs
- [x] Add ride constants to `OpsiveAnimatorConstants.cs`

### Phase 3: Ride System ECS Systems

- [x] Create `RideMountDetectionSystem.cs`
  - Detect nearby rideables using spatial queries
  - Check CanMount conditions
  - React to mount input (E key)
- [x] Create `RideMountingSystem.cs`
  - Handle mount animation phase
  - Parent player to mount transform
  - Disable player physics/collisions
- [x] Create `RideControlSystem.cs`
  - Redirect player input to mount when riding
  - Pass movement commands to mount's locomotion
  - Handle dismount input
- [x] Create `RideDismountSystem.cs`
  - Handle dismount animation
  - Restore player physics
  - Clear ride state

### Phase 4: Ride Animation Integration

- [x] Update `PlayerAnimationStateSystem.cs` to handle RideState
- [x] Add ride handling to `ClimbAnimatorBridge.cs`:
  ```csharp
  // When RideState.IsRiding:
  // AbilityIndex = 401
  // AbilityIntData = RidePhase (1-6)
  ```
- [x] Add animation event handlers:
  - `OnAnimatorRideMount()` - Player attached
  - `OnAnimatorRideDismount()` - Player detached

### Phase 5: Blitz Character Setup

- [x] Create `Blitz_Client.prefab` in `Assets/Prefabs/Characters/`
- [x] Configure Animator with BlitzDemo.controller
- [x] Set up Avatar from Blitz.fbx
- [x] Create `BlitzAnimatorBridge.cs` for ECS-driven animation
- [x] Add `RideableAuthoring` component
- [x] Configure mount points (MountLeft, MountRight, SeatPosition)
- [x] Add GhostAuthoringComponent for networking
- [x] Add Collider and physics setup
- [x] Test Blitz locomotion animations


### Phase 6: Ride Camera Integration (DEFERRED)

- [ ] Create ride camera preset (or use existing Adventure camera)
- [ ] Add camera transition when mounting/dismounting
- [ ] Configure camera follow target switching (player → mount)
- [ ] Test smooth camera transitions

---

## Code Modifications Required

### New Files

| File | Purpose |
|------|---------|
| **Ride System** | |
| `RideState.cs` | ECS component for player ride state |
| `RideableState.cs` | ECS component for mount state |
| `RideEvents.cs` | One-frame event flags for mount/dismount |
| `RideAuthoring.cs` | Baker for player ride capability |
| `RideableAuthoring.cs` | Baker for mount configuration |
| `RideMountDetectionSystem.cs` | Detect and initiate mounting |
| `RideMountingSystem.cs` | Handle mount animation phase |
| `RideControlSystem.cs` | Input redirection while riding |
| `RideDismountSystem.cs` | Handle dismount process |
| **Blitz** | |
| `BlitzAnimatorBridge.cs` | Sync Blitz animations with ECS state |

### Modified Files

| File | Changes |
|------|---------|
| `PlayerAuthoring.cs` | Add ride configuration |
| `PlayerAnimationStateSystem.cs` | Add ride state handling (AbilityIndex=401) |
| `ClimbAnimatorBridge.cs` | Add ride animation events |
| `OpsiveAnimatorConstants.cs` | Add ABILITY_RIDE and ride IntData constants |

## Verification Checklist

### Asset Copy
- [ ] Blitz.fbx exists at target location
- [ ] Blitz materials render correctly
- [ ] All texture references resolved

### Ride System (ECS)
- [ ] RideState component added to player prefab
- [ ] RideableState component added to Blitz prefab
- [ ] Mount detection works (nearby rideable highlighted/detected)
- [ ] Mount input (E key) triggers mount sequence
- [ ] Mount animation plays (left/right based on approach)
- [ ] Player attaches to mount at animation event
- [ ] Player physics disabled while riding
- [ ] Input redirects to mount during ride
- [ ] Dismount input triggers dismount sequence
- [ ] Player detaches and restores physics on dismount

### Ride Networking
- [ ] RideState replicates to all clients
- [ ] RideableState replicates correctly
- [ ] Other players see mounting animation
- [ ] Other players see player on mount
- [ ] Other players see dismounting animation
- [ ] Mount state survives reconnection

### Blitz Character
- [ ] BlitzDemo.controller assigned to Blitz prefab
- [ ] Locomotion animations play correctly
- [ ] Turn animations trigger on input
- [ ] Blitz responds to rider input while ridden
- [ ] Blitz AnimatorMonitor disabled (using BlitzAnimatorBridge)

### Ride Animations (Player)
- [ ] AbilityIndex = 401 when ride state active
- [ ] Mount left (IntData=1) plays correct animation
- [ ] Mount right (IntData=2) plays correct animation
- [ ] Ride idle (IntData=3) plays correctly
- [ ] Dismount left (IntData=4) plays correctly
- [ ] Dismount right (IntData=5) plays correctly

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Ride state desync** | **Critical** | Use GhostFields, test with latency simulation |
| **AnimatorMonitor conflicts** | High | Disable on both player and mount, use ECS bridges |
| **Mount physics collision** | High | Proper collision ignore between rider and mount |
| **Input state during mount** | Medium | Clear input on mount start, redirect during ride |
| **Camera jitter on mount** | Medium | Smooth camera transition, use spring damping |
| **Disconnect while riding** | Medium | Handle in RideDismountSystem, force dismount |

---

## Success Criteria

### Core Ride System
- [ ] Player can mount Blitz with E key (or configured key)
- [ ] Mount/dismount animations play correctly
- [ ] Player moves with mount during ride
- [ ] Player input controls mount while riding
- [ ] Dismount returns player to ground with physics
- [ ] Ride state replicates correctly (networked)
- [ ] Other players see rider on mount

### Blitz
- [ ] Blitz character prefab functional with locomotion
- [ ] Blitz responds to rider input
- [ ] Blitz animations driven by ECS (AnimatorMonitor disabled)

### Performance
- [ ] Ride system < 0.1ms per frame
- [ ] No GC allocations in hot paths

---

## Future Considerations

### Multi-Passenger Vehicles

Extend the ride system for vehicles with multiple seats:
- Driver seat (controls vehicle)
- Passenger seats (no control, can use weapons)
- Turret seats (weapon only)
- Each seat as separate Entity with RideableState

### Horse/Creature Mounts

Use same ride system with different models:
- Import horse model with rig
- Configure BlitzDemo.controller variant for quadruped
- Same ECS components work (RideState, RideableState)

### Combat While Mounted

Opsive provides ride weapon animations:
- Sword riding attacks (already copied)
- Bow riding (already copied)
- Need to integrate with equipment system
- Restrict some abilities while mounted

### AI-Controlled Mounts

When mount has no rider:
- Can use AnimatorMonitor (simpler)
- OR create AI locomotion ECS system
- Patrol, follow, idle behaviors

