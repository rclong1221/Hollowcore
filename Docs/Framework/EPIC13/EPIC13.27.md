# EPIC 13.27: Atlas Character Model Integration

> **Status:** IN PROGRESS
> **Priority:** HIGH
> **Dependencies:** EPIC 13.25 (Opsive Climbing Animations), EPIC 13.26 (Animator Controller)
> **Setup Guide:** [SETUP_GUIDE_13.27.md](SETUP_GUIDE_13.27.md)

## Overview

Replace the Warrok character model (Mixamo rig) with Opsive's Atlas character model to eliminate limb jitter during climb idle. The root cause is a rig mismatch: Opsive climbing animations use `ORG-` prefixed bones while Warrok uses `mixamorig:` prefixed bones.

---

## Problem Analysis

### Root Cause
The climbing animations from Opsive were designed for the Atlas character with `ORG-` bone naming convention:
- `ORG-hips`, `ORG-spine`, `ORG-hand.L`, etc.

The Warrok character uses Mixamo's bone naming convention:
- `mixamorig:Hips`, `mixamorig:Spine`, `mixamorig:LeftHand`, etc.

While Unity's Humanoid retargeting handles the major bones correctly, subtle differences in bone lengths and finger positioning cause:
1. **Limb positioning errors** - hands/feet end up slightly off target
2. **IK fighting** - the IK system constantly tries to correct retargeted positions
3. **Animation restart jitter** - the battle between animation and IK causes visible limb jitter

### Evidence
- Opsive's FreeClimbIdle.fbx uses only 2 frames (essentially a static pose)
- Their IK system clears position maps every frame for fresh calculations
- The animations work perfectly with their Atlas model but jitter with Warrok

---

## Sub-Tasks

### 13.27.1 Copy Atlas Assets
**Status:** ✅ COMPLETE

Copy all Atlas character assets from Opsive package to project.

#### Files Copied

| Source | Destination |
|--------|-------------|
| `OPSIVE/.../Models/Characters/Atlas/Atlas.fbx` | `Assets/Art/Models/Atlas/Atlas.fbx` |
| `OPSIVE/.../Materials/Characters/Atlas/*.mat` | `Assets/Art/Materials/Atlas/` |
| `OPSIVE/.../Textures/Characters/Atlas/*` | `Assets/Art/Textures/Atlas/` |

#### Assets Copied
- **Model**: `Atlas.fbx` (4.5 MB) - Humanoid rig with `ORG-` prefix bones
- **Materials**: `Body.mat`, `Head.mat`, `Hands.mat`, `Joints.mat`
- **Textures**: Full PBR texture set (Albedo, Normal, MetallicSmoothness, AmbientOcclusion, Emission)

---

### 13.27.2 Configure FBX Import Settings
**Status:** ✅ COMPLETE

The Atlas.fbx.meta file was copied with correct Humanoid configuration:
- `animationType: 3` (Humanoid)
- `avatarSetup: 1` (Create From This Model)

No manual configuration required - settings preserved from Opsive package.

---

### 13.27.3 Create Atlas_Client Prefab
**Status:** NOT STARTED

Create the client-side presentation prefab with Atlas model.

#### Required Components
| Component | Purpose |
|-----------|---------|
| `Animator` | Animation playback (assign ClimbingDemo.controller) |
| `AnimatorRigBridge` | Bridges ECS state to Animator |
| `ClimbAnimatorBridge` | Climbing-specific animation control |
| `OpsiveClimbingIK` | IK for climbing (auto-added to Animator GO) |
| `LandingAnimatorBridge` | Landing animations |

#### Required Child Objects
- `LeftHandIK` - IK target transform
- `RightHandIK` - IK target transform
- `LeftFootIK` - IK target transform
- `RightFootIK` - IK target transform
- `FlashlightMount` - Flashlight attachment point
- `AudioManager` - Audio source management

#### Acceptance Criteria
- [ ] Atlas model displays correctly in scene
- [ ] Animator component configured with correct controller
- [ ] All IK targets created and assigned
- [ ] ClimbAnimatorBridge references IK targets

---

### 13.27.4 Create Atlas_Server Prefab
**Status:** NOT STARTED

Create the server-side ECS prefab (no visual mesh required).

#### Required Components
Copy from `Warrok_Server.prefab`:
- `GhostAuthoringComponent`
- `PlayerTagAuthoring`
- `FreeClimbAuthoring`
- `PlayerStateAuthoring`
- `PhysicsShape` / `PhysicsBody`
- All other ECS authoring components

#### Acceptance Criteria
- [ ] Server prefab contains all ECS authoring components
- [ ] No SkinnedMeshRenderer (server is headless)
- [ ] Ghost configuration matches Warrok_Server

---

### 13.27.5 Update Spawn References
**Status:** NOT STARTED

Update game systems to spawn Atlas prefabs instead of Warrok.

#### Files to Update
| File | Change |
|------|--------|
| Player spawn system | Reference Atlas_Server for spawning |
| Ghost collection | Register Atlas ghost prefabs |
| Scene references | Replace any Warrok prefab instances |

---

### 13.27.6 Update Bone Name References
**Status:** ✅ COMPLETE

Update code that references bone names by string.

#### Changes Made

**FlashlightSystem.cs:321**
```csharp
// Before
string[] headNames = { "Head", "head", "Bip01 Head", "mixamorig:Head", "Bone_Head" };

// After
string[] headNames = { "Head", "head", "Bip01 Head", "mixamorig:Head", "ORG-head", "Bone_Head" };
```

#### No Changes Required
Most code uses Unity's `HumanBodyBones` enum which abstracts bone names:
- `OpsiveClimbingIK.cs` - Uses `HumanBodyBones.LeftUpperArm`, etc.
- IK systems - Use `AvatarIKGoal` enum

---

## Files Modified

| File | Changes |
|------|---------|
| `Assets/Scripts/Visuals/Systems/FlashlightSystem.cs` | Added `ORG-head` to bone name array |

## Files Created

| File | Purpose |
|------|---------|
| `Assets/Art/Models/Atlas/Atlas.fbx` | Atlas character model |
| `Assets/Art/Models/Atlas/Atlas.fbx.meta` | Import settings (Humanoid) |
| `Assets/Art/Materials/Atlas/Body.mat` | Body material |
| `Assets/Art/Materials/Atlas/Head.mat` | Head material |
| `Assets/Art/Materials/Atlas/Hands.mat` | Hands material |
| `Assets/Art/Materials/Atlas/Joints.mat` | Joints material |
| `Assets/Art/Textures/Atlas/*` | PBR texture set |

---

## Verification Plan

1. **Import Check**
   - Open Unity, verify Atlas.fbx imports as Humanoid
   - Check Avatar is created correctly

2. **Prefab Creation**
   - Create Atlas_Client with all components
   - Create Atlas_Server with ECS components

3. **Climbing Test**
   - Spawn as Atlas character
   - Mount a climbing surface
   - **Idle on wall** - verify NO limb jitter
   - Move while climbing - verify smooth animation
   - Dismount - verify clean transition

4. **Comparison Test**
   - Record video of Warrok climbing (with jitter)
   - Record video of Atlas climbing (without jitter)
   - Confirm fix

---

## Technical Notes

### Why Duplicate-and-Swap Doesn't Work
Duplicating Warrok_Client and swapping the mesh doesn't work because:
1. Prefab contains bone transform references by internal Unity fileID
2. Old references point to `mixamorig:` bones that no longer exist
3. New `ORG-` bones have no references pointing to them
4. The Avatar must be created from the Atlas FBX, not inherited

### Humanoid Abstraction
Unity's Humanoid system maps bone names to abstract slots:
- `HumanBodyBones.LeftHand` maps to whatever the Avatar defines as left hand
- Works regardless of actual bone name (`mixamorig:LeftHand` or `ORG-hand.L`)
- IK goals (`AvatarIKGoal.LeftHand`) work through this abstraction

This is why most code doesn't need changes - only string-based bone lookups do.

---

## Rollback Plan

If Atlas model causes issues:
1. Delete `Assets/Art/Models/Atlas/` folder
2. Delete `Assets/Art/Materials/Atlas/` folder
3. Delete `Assets/Art/Textures/Atlas/` folder
4. Revert FlashlightSystem.cs change
5. Continue using Warrok with jitter (or investigate IK smoothing fix)
