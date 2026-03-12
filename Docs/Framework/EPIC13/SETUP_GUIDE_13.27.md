# Setup Guide: Atlas Character Model Integration

This guide walks through replacing the Warrok character with Opsive's Atlas character to fix climb idle limb jitter.

---

## Prerequisites

- Unity Editor open with DIG project
- Assets already copied (Phase 1 complete):
  - `Assets/Art/Models/Atlas/Atlas.fbx`
  - `Assets/Art/Materials/Atlas/*.mat`
  - `Assets/Art/Textures/Atlas/*`

---

## Step 1: Verify Atlas Import

1. Open Unity and wait for asset import to complete
2. Navigate to `Assets/Art/Models/Atlas/`
3. Select `Atlas.fbx`
4. In Inspector, verify **Rig** tab shows:
   - Animation Type: **Humanoid**
   - Avatar Definition: **Create From This Model**
5. If not configured, set these values and click **Apply**

### Verify Avatar
1. Expand `Atlas.fbx` in Project window
2. You should see an Avatar asset (humanoid icon)
3. Double-click to open Avatar Configuration
4. Verify all bones are mapped (green dots)

---

## Step 2: Fix Material References

The copied materials reference textures by GUID from the Opsive package. You need to reassign them.

1. Navigate to `Assets/Art/Materials/Atlas/`
2. Select `Body.mat`
3. In Inspector, reassign textures from `Assets/Art/Textures/Atlas/`:
   - Albedo: `Body_AlbedoTransparency.png`
   - Normal Map: `Body_Normal.png`
   - Metallic: `Body_MetallicSmoothness.png`
   - Occlusion: `Body_AmbientOcclusion.png`
   - Emission: `Body_Emission.png`
4. Repeat for `Head.mat`, `Hands.mat`, `Joints.mat`

---

## Step 3: Create Atlas_Client Prefab

### 3.1 Add Model to Scene
1. Drag `Assets/Art/Models/Atlas/Atlas.fbx` into Scene Hierarchy
2. Rename the GameObject to `Atlas_Client`
3. Position at origin (0, 0, 0)

### 3.2 Configure Animator
1. Select `Atlas_Client` root object
2. Add Component → `Animator` (if not present)
3. Set Controller: `Assets/Art/Animations/Opsive/AddOns/Climbing/ClimbingDemo.controller`
4. Set Avatar: `Atlas` (from Atlas.fbx)
5. Apply Root Motion: **unchecked**
6. Culling Mode: **Always Animate**

### 3.3 Add Animation Bridge Components
Add these components to the `Atlas_Client` root:

| Component | Notes |
|-----------|-------|
| `AnimatorRigBridge` | Auto-finds Animator |
| `ClimbAnimatorBridge` | Configure after adding IK targets |
| `LandingAnimatorBridge` | For landing animations |

### 3.4 Create IK Target Transforms
Right-click `Atlas_Client` → Create Empty for each:

| Name | Position |
|------|----------|
| `LeftHandIK` | (0, 0, 0) |
| `RightHandIK` | (0, 0, 0) |
| `LeftFootIK` | (0, 0, 0) |
| `RightFootIK` | (0, 0, 0) |
| `FlashlightMount` | (0.043, 2.252, 0.911) |
| `AudioManager` | (0, 0, 0) |

### 3.5 Configure ClimbAnimatorBridge
Select `Atlas_Client`, find `ClimbAnimatorBridge` component:

1. **Enable IK**: ✅ checked
2. **Left Hand IK Target**: drag `LeftHandIK` child
3. **Right Hand IK Target**: drag `RightHandIK` child
4. **Left Foot IK Target**: drag `LeftFootIK` child
5. **Right Foot IK Target**: drag `RightFootIK` child

### 3.6 Add AudioManager Script
1. Select `AudioManager` child object
2. Add Component → `AudioManager`
3. Configure as needed (copy settings from Warrok_Client if desired)

### 3.7 Save as Prefab
1. Drag `Atlas_Client` from Hierarchy to `Assets/Prefabs/`
2. Select "Original Prefab" when prompted
3. Delete the scene instance

---

## Step 4: Create Atlas_Server Prefab

The server prefab doesn't need the visual mesh - it's for ECS/networking only.

### 4.1 Create Empty GameObject
1. Create Empty GameObject in scene
2. Name it `Atlas_Server`

### 4.2 Copy Components from Warrok_Server
Open `Assets/Prefabs/Warrok_Server.prefab` as reference.

Add these components to `Atlas_Server`:

| Component | Purpose |
|-----------|---------|
| `GhostAuthoringComponent` | NetCode ghost replication |
| `PlayerTagAuthoring` | Player entity identification |
| `FreeClimbAuthoring` | Climbing ECS components |
| `PlayerStateAuthoring` | Player state management |
| `PhysicsShapeAuthoring` | Capsule collider for physics |
| `PhysicsBodyAuthoring` | Dynamic physics body |
| *Copy all other authoring components* | Match Warrok_Server exactly |

### 4.3 Configure Physics Shape
1. Shape Type: **Capsule**
2. Match dimensions from Warrok_Server (typically radius 0.5, height 2.0)

### 4.4 Save as Prefab
1. Drag `Atlas_Server` to `Assets/Prefabs/`
2. Delete the scene instance

---

## Step 5: Update Spawn References

Find where player prefabs are referenced and update to Atlas.

### 5.1 Search for Warrok References
In Project window, search for `Warrok` to find:
- Scene objects
- Prefab references in scripts
- ScriptableObjects with prefab fields

### 5.2 Common Locations to Update

| Location | Change |
|----------|--------|
| Player Spawn System | Change prefab reference to `Atlas_Server` |
| Ghost Collection | Register `Atlas_Server` as ghost prefab |
| UI prefab spawner | Change to `Atlas_Client` |

### 5.3 Ghost Registration
If using NetCode:
1. Find your `GhostCollectionAuthoring` or equivalent
2. Add `Atlas_Server` prefab to the ghost list
3. Remove or keep `Warrok_Server` as needed

---

## Step 6: Test the Integration

### 6.1 Basic Test
1. Enter Play Mode
2. Verify Atlas character spawns correctly
3. Move around - verify locomotion works

### 6.2 Climbing Test
1. Approach a climbable surface
2. Mount the surface
3. **Stay idle on the wall for 5+ seconds**
4. Verify: **NO limb jitter**
5. Move while climbing
6. Dismount

### 6.3 IK Verification
While climbing idle:
1. Hands should be stationary on wall
2. Feet should be stationary on wall
3. No micro-movements or shaking

---

## Troubleshooting

### Atlas appears as pink (missing materials)
1. Check `Assets/Art/Materials/Atlas/` materials
2. Reassign textures from `Assets/Art/Textures/Atlas/`
3. Ensure materials use correct shader for your render pipeline

### Avatar configuration errors
1. Select `Atlas.fbx`
2. Go to Rig tab → Configure
3. Manually assign any missing bones
4. Click Apply

### Animations don't play
1. Verify Animator Controller is assigned
2. Check Animator parameters match ClimbAnimatorBridge expectations
3. Verify Avatar is assigned to Animator

### Character T-poses during climb
1. Check Avatar is from Atlas.fbx, not another model
2. Verify Humanoid configuration is correct
3. Check animation clips are Humanoid type

### Climbing still has jitter
1. Verify you're using Atlas_Client prefab (not Warrok)
2. Check OpsiveClimbingIK is on the Animator GameObject
3. Verify ClimbAnimatorBridge.EnableIK is true

### Flashlight doesn't attach
Already fixed in code - `ORG-head` added to bone name search.

---

## Verification Checklist

- [ ] Atlas.fbx imports as Humanoid
- [ ] Materials display correctly (not pink)
- [ ] Atlas_Client prefab created with all components
- [ ] Atlas_Server prefab created with ECS components
- [ ] Spawn system references updated
- [ ] Ghost collection updated (if using NetCode)
- [ ] Character spawns correctly in Play Mode
- [ ] Climbing works without limb jitter
- [ ] Dismount works correctly
- [ ] Flashlight attaches to head

---

## Quick Reference: Hierarchy Structure

### Atlas_Client
```
Atlas_Client (root)
├── Animator (component)
├── AnimatorRigBridge (component)
├── ClimbAnimatorBridge (component)
├── LandingAnimatorBridge (component)
├── [Mesh children from Atlas.fbx]
│   ├── Body
│   ├── Head
│   ├── Hands
│   └── Armature/Skeleton
├── LeftHandIK (empty)
├── RightHandIK (empty)
├── LeftFootIK (empty)
├── RightFootIK (empty)
├── FlashlightMount (empty)
└── AudioManager
    └── AudioManager (component)
```

### Atlas_Server
```
Atlas_Server (root)
├── GhostAuthoringComponent
├── PlayerTagAuthoring
├── FreeClimbAuthoring
├── PlayerStateAuthoring
├── PhysicsShapeAuthoring
├── PhysicsBodyAuthoring
└── [Other ECS authoring components]
```
