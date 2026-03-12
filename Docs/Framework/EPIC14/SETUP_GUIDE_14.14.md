# EPIC 14.14 - Blitz Mount System and First Person Arms Setup Guide

## Overview

This guide covers setting up the Blitz rideable mount and first-person arms in the Unity Editor.

**All code has been implemented.** This guide covers Unity Editor setup only.

---

## Phase Status

| Phase | Content | Status |
|-------|---------|--------|
| Phase 1 | Asset Copy | ⏳ Manual Copy Required |
| Phase 2-4 | Ride System Code | ✅ Complete |
| Phase 5 | Blitz Character Setup | ⏳ Unity Setup Required |
| Phase 6 | Ride Camera | ⏳ Unity Setup Required |
| Phase 7-8 | First Person Arms | ⏳ Unity Setup Required |
| Phase 9-10 | Player Integration | ⏳ Unity Setup Required |
| Phase 11 | Weapon Sync | ⏳ Unity Setup Required |
| Phase 12 | Network Testing | ⏳ Testing Required |

---

## Part 1: Asset Copy

### Already Copied ✅

| Asset Type | Location |
|------------|----------|
| BlitzDemo.controller | `Assets/Art/Animations/Opsive/Animator/Characters/` |
| FirstPersonArmsDemo.controller | `Assets/Art/Animations/Opsive/Animator/Characters/` |
| SwimmingFirstPersonArmsDemo.controller | `Assets/Art/Animations/Opsive/AddOns/Swimming/Animator/` |
| Blitz Animations | `Assets/Art/Animations/Opsive/Demo/Blitz/` |
| Ride Animations | `Assets/Art/Animations/Opsive/Demo/Abilities/Ride/` |
| FP Arm Masks | `Assets/Art/Animations/Opsive/Animator/Characters/` |

### Copy These in Unity

In Unity Project window, copy the following:

#### Blitz Model

| From | To |
|------|-----|
| `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Models/Characters/Blitz/Blitz.fbx` | `Assets/Art/Models/Characters/Blitz/` |

#### Blitz Materials

| From | To |
|------|-----|
| `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Materials/Characters/Blitz/` (all .mat files) | `Assets/Art/Materials/Characters/Blitz/` |

Files: `Accessories.mat`, `Blitz.mat`, `Mane.mat`

#### Blitz Textures

| From | To |
|------|-----|
| `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Textures/Characters/Blitz/` (all files) | `Assets/Art/Textures/Characters/Blitz/` |

#### First Person Arms Models

| From | To |
|------|-----|
| `Assets/OPSIVE/.../Models/Characters/Atlas/AtlasFirstPersonArms.fbx` | `Assets/Art/Models/Characters/FirstPersonArms/` |
| `Assets/OPSIVE/.../Models/Characters/Atlas/AtlasFirstPersonLeftArm.fbx` | `Assets/Art/Models/Characters/FirstPersonArms/` |
| `Assets/OPSIVE/.../Models/Characters/Atlas/AtlasFirstPersonRightArm.fbx` | `Assets/Art/Models/Characters/FirstPersonArms/` |
| `Assets/OPSIVE/.../Models/Characters/Rhea/RheaFirstPersonArms.fbx` | `Assets/Art/Models/Characters/FirstPersonArms/` |

#### Atlas Materials (for FP Arms)

| From | To |
|------|-----|
| `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Materials/Characters/Atlas/` (all .mat files) | `Assets/Art/Materials/Characters/Atlas/` |

Files: `Body.mat`, `Hands.mat`, `Head.mat`, `Joints.mat`

#### Atlas Textures (for FP Arms)

| From | To |
|------|-----|
| `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Textures/Characters/Atlas/` (all files) | `Assets/Art/Textures/Characters/Atlas/` |

---

## Part 2: Blitz Character Setup

### Option A: Use Editor Tool (Recommended)

1. In Unity menu: **DIG → Create Blitz Prefabs**
2. Adjust settings if needed
3. Click **Create Blitz Prefabs**
4. Done! Both `Blitz_Client` and `Blitz_Server` are created with all components.

### Option B: Manual Setup

#### Step 1: Create Blitz Prefab

1. In Project window, navigate to `Assets/Prefabs/Characters/`
2. Create empty GameObject, name it `Blitz_Client`
3. Drag `Blitz.fbx` model as child
4. Apply Blitz materials to mesh renderers

### Step 2: Configure Animator

1. Select Blitz model child object
2. In Inspector, find Animator component
3. Set **Controller**: `BlitzDemo.controller`
4. Set **Avatar**: Select avatar from Blitz.fbx

### Step 3: Add Required Components

Add these components to the **root** `Blitz_Client` object:

| Component | Location |
|-----------|----------|
| Ghost Authoring Component | Unity.NetCode |
| Rideable Authoring | Player.Authoring |
| Blitz Animator Bridge | Player.Bridges |

### Step 4: Configure Rideable Authoring

| Field | Value |
|-------|-------|
| Can Be Ridden | ✓ |
| Interaction Radius | 2.0 |
| Mount Offset Left | (-1, 0, 0) |
| Mount Offset Right | (1, 0, 0) |
| Seat Offset | (0, 1.5, 0) |

### Step 5: Configure Blitz Animator Bridge

| Field | Value |
|-------|-------|
| Blitz Animator | Drag the Animator from Blitz model child |
| Locomotion Speed Multiplier | 1.0 |

### Step 6: Create Mount Point Transforms (Optional)

Create these empty child GameObjects under Blitz root:

| Name | Position | Purpose |
|------|----------|---------|
| MountLeft | (-1, 0, 0.5) | Where player stands to mount from left |
| MountRight | (1, 0, 0.5) | Where player stands to mount from right |
| SeatPosition | (0, 1.5, 0) | Where player sits when mounted |

### Step 7: Add Physics

| Component | Settings |
|-----------|----------|
| Rigidbody | Mass: 500, Drag: 1, Use Gravity: ✓ |
| Capsule Collider | Height: 2, Radius: 0.5, Center: (0, 1, 0) |

### Step 8: Save Prefab

1. Drag `Blitz_Client` to `Assets/Prefabs/Characters/`
2. Create Server variant: Duplicate, rename to `Blitz_Server`
3. On Server variant, disable mesh renderers (visual-only on client)
4. **Important:** Ensure both prefabs have `Rideable Authoring` and `Ghost Authoring Component`

---

## Part 3: First Person Arms Setup

### Step 1: Create FP Arms Prefab

1. Create folder `Assets/Prefabs/FirstPerson/`
2. Drag `AtlasFirstPersonArms.fbx` into scene
3. Rename to `FirstPersonArms`

### Step 2: Configure Animator

| Field | Value |
|-------|-------|
| Controller | FirstPersonArmsDemo.controller |
| Avatar | From AtlasFirstPersonArms.fbx |
| Apply Root Motion | ✗ |
| Update Mode | Normal |
| Culling Mode | Always Animate |

### Step 3: Add First Person Arms Bridge

Add component: **First Person Arms Bridge** (`Player.Bridges`)

| Field | Value |
|-------|-------|
| First Person Arms | (Leave empty - set by player) |
| Swimming First Person Arms | (Leave empty - set by player) |
| Third Person Model | (Leave empty - set by player) |
| Arms Animator | Drag Animator from this object |
| Right Hand Items | See Step 4 |
| Left Hand Items | See Step 4 |

### Step 4: Create Weapon Attachment Points

Navigate through the armature hierarchy to find hand bones, then create empty child GameObjects:

```
FirstPersonArms
└── Armature
    └── ...
        └── RightHand
            └── Items  ← Create this (empty GameObject)
        └── LeftHand
            └── Items  ← Create this (empty GameObject)
```

Assign these to the bridge:
- Right Hand Items → RightHand/Items
- Left Hand Items → LeftHand/Items

### Step 5: Save as Prefab

Drag to `Assets/Prefabs/FirstPerson/FirstPersonArms.prefab`

---

## Part 4: Swimming First Person Arms

### Step 1: Duplicate FP Arms Prefab

1. Duplicate `FirstPersonArms.prefab`
2. Rename to `SwimmingFirstPersonArms.prefab`

### Step 2: Change Controller

| Field | Value |
|-------|-------|
| Controller | SwimmingFirstPersonArmsDemo.controller |

### Step 3: Replace Bridge Component

1. Remove **First Person Arms Bridge**
2. Add **First Person Arms Swimming Bridge** (`Player.Bridges`)

| Field | Value |
|-------|-------|
| Swimming Arms Animator | Drag Animator from this object |
| Right Hand Items | RightHand/Items transform |
| Left Hand Items | LeftHand/Items transform |

---

## Part 5: Player Prefab Integration

### Step 1: Open Player Prefab

Open `Assets/Prefabs/Atlas_Server.prefab`

### Step 2: Add Ride Authoring

Add component to the **Server** player prefab:
- `Assets/Prefabs/Atlas_Server.prefab`

Component: **Ride Authoring** (`Player.Authoring`)

| Field | Value |
|-------|-------|
| Detection Range | 3.0 |

**Note:** The player gets `RideState` and `RideConfig` components baked when the prefab is spawned through ECS.

---

## Part 5.5: Blitz Spawner Setup (CRITICAL)

**Blitz must be spawned through ECS, not placed directly in the scene.**

### Step 1: Open your SubScene

Open the same SubScene that contains `PlayerSpawnerAuthoring` (where player prefab is registered).

### Step 2: Create Blitz Spawn Point

1. Create empty GameObject in the SubScene
2. Name it `BlitzSpawnPoint`
3. Position it where you want Blitz to spawn

### Step 3: Add Blitz Spawner Authoring

Add component: **Blitz Spawner** (`Player.Authoring`)

| Field | Value |
|-------|-------|
| Blitz Prefab | Drag `Blitz_Server.prefab` here |
| Spawn On Start | ✓ |

### Step 4: Remove Direct Blitz

If you have Blitz placed directly in the scene (not SubScene), **delete it**. It won't work because it doesn't get baked to ECS.

### Why This Is Necessary

- ECS components are baked when prefabs are referenced in a SubScene
- GameObjects placed in regular scenes don't get ECS components
- The ride system uses ECS queries to find rideables
- Without the spawner, Blitz has no `RideableState` component and can't be detected

### Step 3: Add Arms as Children

1. Drag `FirstPersonArms` prefab as child of player root
2. Drag `SwimmingFirstPersonArms` prefab as child of player root
3. Position both at (0, 0, 0) local

### Step 4: Add First Person View Handler

Add component to player root: **First Person View Handler** (`DIG.Items.Bridges`)

### Step 5: Configure First Person Arms Bridge

On the **FirstPersonArmsBridge** component (on player or FP arms):

| Field | Reference |
|-------|-----------|
| Third Person Model | Player's visual model (Atlas mesh root) |
| First Person Arms | The FirstPersonArms child object |
| Swimming First Person Arms | The SwimmingFirstPersonArms child object |
| Arms Animator | FirstPersonArms Animator |
| Swimming Arms Animator | SwimmingFirstPersonArms Animator |

### Step 6: Initial Visibility State

| Object | Active State |
|--------|--------------|
| Third Person Model | ✓ Active |
| FirstPersonArms | ✗ Inactive |
| SwimmingFirstPersonArms | ✗ Inactive |

---

## Part 6: Controls

### Mount System Controls

| Action | Key | Description |
|--------|-----|-------------|
| Mount | **T** | When near a rideable, press T to mount |
| Dismount | **T** | When riding, press T to dismount |
| Control Mount | **WASD** | Move the mount (redirects player movement) |
| Look | **Mouse** | Look around while mounted |

**Note:** The 'T' key is the project's Interact key, defined in `PlayerInputSystem.cs`.

---

---

## Part 7: Testing

### Test 1: Blitz Locomotion

1. Place Blitz prefab in scene
2. Enter Play Mode
3. Verify Blitz idle animation plays

### Test 2: Mount/Dismount

1. Place Player and Blitz in scene
2. Walk player near Blitz (within 2-3 units)
3. Press **T** (Interact key) to mount
4. Verify player attaches to seat
5. Press **T** again to dismount
6. Verify player returns to ground

### Test 3: First Person Toggle

1. Play as player character
2. Switch to FirstPerson camera view type
3. Verify third person model hides
4. Verify first person arms appear

### Test 4: Swimming Arms

1. Enter water while in first person mode
2. Verify normal arms swap to swimming arms
3. Exit water
4. Verify swimming arms swap back to normal

### Test 5: Multiplayer Sync

1. Start host + client
2. Have one player mount Blitz
3. Verify other player sees rider on mount

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Blitz not animating | Verify Animator Controller is assigned and Avatar is set |
| Pressing T doesn't mount | See checklist below |
| Blitz spins wildly | Rigidbody needs FreezeRotation constraint (fixed in editor tool) |
| Can't mount Blitz | Check Rideable Authoring → Can Be Ridden = true, Interaction Radius > 0 |
| "[RideDetection] Found 0 rideable(s)" in console | Blitz not in ECS - use BlitzSpawnerAuthoring in SubScene |
| "[RideInput] No nearby rideable found" in console | Walk closer to Blitz (within InteractionRadius) |

### T Key Not Mounting Checklist

1. ✓ Player prefab has `RideAuthoring` component
2. ✓ Blitz_Server prefab has `RideableAuthoring` component  
3. ✓ BlitzSpawnerAuthoring is in SubScene (not regular scene)
4. ✓ BlitzSpawnerAuthoring has Blitz_Server prefab assigned
5. ✓ Blitz is NOT placed directly in regular scene
6. ✓ Player is within InteractionRadius (default 2 units) of Blitz
7. ✓ Console shows "[RideDetection] Found 1 rideable(s)" when near Blitz
| Player floats after mount | Adjust Seat Offset Y value |
| FP Arms not showing | Check view mode is FirstPerson, arms objects are assigned |
| Arms in wrong position | Adjust FirstPersonArms local position on player |
| Swimming arms not switching | Verify SetSwimming() is being called |
| Weapon not in FP hands | Check Items attachment point transforms exist |
| Other players don't see mount | Verify Ghost Authoring on both player and Blitz prefabs |
| RideableAuthoring not found | Search for "Rideable Authoring" in Add Component |
| BlitzAnimatorBridge not found | Search for "Blitz Animator Bridge" in Add Component |

---

## Inspector Reference

### Rideable Authoring

| Field | Type | Description |
|-------|------|-------------|
| Can Be Ridden | bool | Enable/disable mounting |
| Interaction Radius | float | How close player must be to mount |
| Mount Offset Left | Vector3 | Position for left-side mount approach |
| Mount Offset Right | Vector3 | Position for right-side mount approach |
| Seat Offset | Vector3 | Final seated position relative to mount |
| Seat Transform | Transform | (Optional) Transform for seat position |
| Mount Left Transform | Transform | (Optional) Transform for left mount point |
| Mount Right Transform | Transform | (Optional) Transform for right mount point |
| Move Speed | float | Forward movement speed (default: 8) |
| Turn Speed | float | Turn speed in degrees/sec (default: 120) |

### Ride Authoring (on Player)

| Field | Type | Description |
|-------|------|-------------|
| Detection Range | float | Range to detect nearby rideables (default: 3) |

### Blitz Animator Bridge

| Field | Type | Description |
|-------|------|-------------|
| Blitz Animator | Animator | Reference to Blitz's Animator component |
| Locomotion Speed Multiplier | float | Speed multiplier for locomotion anims |

### First Person Arms Bridge

| Field | Type | Description |
|-------|------|-------------|
| First Person Arms | GameObject | Arms to show in FP mode |
| Swimming First Person Arms | GameObject | Arms for swimming |
| Third Person Model | GameObject | Model to hide in FP mode |
| Arms Animator | Animator | Normal FP arms animator |
| Swimming Arms Animator | Animator | Swimming FP arms animator |
| Right Hand Items | Transform | Weapon attachment point |
| Left Hand Items | Transform | Off-hand attachment point |

### First Person Arms Swimming Bridge

| Field | Type | Description |
|-------|------|-------------|
| Swimming Arms Animator | Animator | Swimming arms animator |
| Right Hand Items | Transform | Right hand attachment |
| Left Hand Items | Transform | Left hand attachment |

### First Person View Handler

| Field | Type | Description |
|-------|------|-------------|
| Arms Bridge | FirstPersonArmsBridge | Reference to FP arms bridge |
| Character Root | Transform | Root of character (auto-detected) |

---

## Animation Integration

The mount system uses the existing Opsive **Ride** animator layer (visible in the animator controller). The animations are driven automatically:

| Phase | AbilityIndex | AbilityIntData | Animation State |
|-------|--------------|----------------|-----------------|
| Mount from Left | 12 | 1 | Mount Left |
| Mount from Right | 12 | 2 | Mount Right |
| Riding | 12 | 3 | Idle / Movement |
| Dismount Left | 12 | 4 | Dismount Left |
| Dismount Right | 12 | 5 | Dismount Right |

**How it works:**
1. `RideInputSystem` (server-only) detects 'T' key and starts mounting (sets `RidePhase = Mounting`)
2. `PlayerAnimationStateSystem` detects ride state and sets `AbilityIndex=12` + `AbilityIntData`
3. `ClimbAnimatorBridge.ApplyAnimationState()` writes these to the Animator
4. The Animator's "Ride" layer plays the appropriate animation based on AbilityIndex/IntData

**Shader Warning:** You may see a warning about `Universal Render Pipeline/Lit` not supporting skinning with Rukhanka. This means Blitz's GPU skinning may not render correctly. See [Rukhanka shader docs](https://docs.rukhanka.com/shaders_with_deformations) for compatible shaders.

---

## Horse Movement

When mounted, the player's WASD input is forwarded to the horse:
- **W/S** = Move forward/backward
- **A/D** = Turn left/right

This follows Opsive's pattern from `Ride.cs`:
```csharp
// Player input forwarded to mount
m_Rideable.CharacterLocomotionHandler.OverriddenForwardMovement = playerInput.y;
m_Rideable.CharacterLocomotionHandler.OverriddenHorizontalMovement = playerInput.x;
```

Our ECS equivalent:
1. `RideControlSystem` reads `PlayerInput.MoveInput` from rider
2. Writes to mount's `MountMovementInput` component
3. `MountMovementSystem` moves the mount based on that input

---

## Server-Only Architecture

All ride state changes happen on the **SERVER ONLY**:

| System | World Filter | Purpose |
|--------|--------------|---------|
| `RideMountDetectionSystem` | Server | Detect nearby mounts |
| `RideInputSystem` | Server | Process T key, modify RideState |
| `RideMountingSystem` | Server | Progress mount animation |
| `RideControlSystem` | Server | Forward input to mount, attach player |
| `RideDismountSystem` | Server | Progress dismount animation |
| `MountMovementSystem` | Server | Move mount based on input |

**Client receives state via GhostFields:**
- `RideState.RidePhase`, `IsRiding`, `MountEntity`, etc.
- `MountMovementInput.ForwardInput`, `HorizontalInput`

This prevents client/server desync issues that occurred when both were modifying state.

---

## Notes

- **Blitz is a horse mount** from Opsive's demo. The ride system works with any Rideable entity.
- The same ride system can be used for other horses, vehicles, or mounts by creating new prefabs with RideableAuthoring.
- First Person Arms are independent from the ride system and can be tested separately.
- **Mount/Dismount uses the 'T' key** (Interact input). This is already wired in `RideInputSystem.cs`.
- **Mount animations** are automatically triggered via the Opsive Ride layer (AbilityIndex=12).
- **Horse movement** works with WASD when mounted - input is forwarded to the mount entity.
