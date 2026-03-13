# EPIC 1.6 Setup Guide: Chassis Visual & Animation Integration

**Status:** Planned
**Requires:** EPIC 1.1 (ChassisState, LimbInstance, ChassisLink), EPIC 1.2 (Limb Integrity & Degradation), Framework Animation/ system, Framework VFX/ pipeline (VFXRequest)

---

## Overview

This system provides the visual representation of the modular chassis. Each chassis slot has a socket transform on the player skeleton. Equipped limbs display their visual mesh; empty or destroyed slots show stump models with sparking VFX. Missing legs trigger crawling/limping locomotion layers, and missing arms trigger one-handed or unarmed upper body layers. A managed `ChassisVisualBridge` MonoBehaviour reads ECS chassis state and updates GameObjects on the presentation side.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Player Prefab (Subscene) | `ChassisAuthoring` (EPIC 1.1) | ChassisState + LimbInstance data source |
| Player Visual Root | `Animator` | Animation controller with layer support |
| Player Skeleton | 6 socket transforms | Attachment points for limb meshes |
| Data | LimbDefinitionSO assets (EPIC 1.1) | Each must have MeshPrefab and IconSprite |
| Framework | VFX/ pipeline | VFXRequest for stump sparks, equip click, etc. |

### New Setup Required

1. Tag 6 socket transforms on the player armature
2. Create stump prefab variants (arm, leg, torso)
3. Set up Animator Controller layers (Limping, Crawling, OneHanded, Unarmed)
4. Add `ChassisVisualBridge` MonoBehaviour to player visual root
5. Configure socket references in inspector
6. Create VFX prefabs (stump sparks, equip click, destruction shatter, memory glow)

---

## 1. Socket Transform Setup

**Location:** Player armature/skeleton hierarchy in the prefab

Tag these transforms in the player bone hierarchy. `ChassisVisualBridge` auto-finds them by name.

| Transform Name | Bone Parent | Purpose |
|----------------|-------------|---------|
| `Socket_Head` | Head bone | Head limb mesh attachment |
| `Socket_Torso` | Spine/Chest bone | Torso limb mesh attachment |
| `Socket_LeftArm` | Left upper arm bone | Left arm limb mesh attachment |
| `Socket_RightArm` | Right upper arm bone | Right arm limb mesh attachment |
| `Socket_LeftLeg` | Left upper leg bone | Left leg limb mesh attachment |
| `Socket_RightLeg` | Right upper leg bone | Right leg limb mesh attachment |

### 1.1 Socket Positioning

| Guideline | Details |
|-----------|---------|
| **Pivot** | Socket pivot should be at the joint where the limb attaches (shoulder, hip, neck base) |
| **Forward** | Socket forward (Z+) should point in the limb's natural extension direction |
| **Scale** | Socket scale must be (1,1,1) -- limb prefabs handle their own scale |

**Tuning tip:** Use the "Auto-Find Sockets" button in the ChassisVisualBridge inspector to search the bone hierarchy for standard socket names. If your skeleton uses non-standard names, either rename the bones or manually assign socket references.

---

## 2. Stump Prefab Setup

**Create:** Stump mesh prefabs per slot type
**Recommended location:** `Assets/Prefabs/Chassis/Stumps/`

| Prefab | Slot | Description |
|--------|------|-------------|
| `Stump_Arm.prefab` | LeftArm, RightArm | Severed arm socket with exposed wiring/bone |
| `Stump_Leg.prefab` | LeftLeg, RightLeg | Severed leg socket with exposed wiring/bone |
| `Stump_Torso.prefab` | Torso | Damaged torso plate with visible internals |
| (none) | Head | Head destruction = instant death, no stump needed |

### 2.1 Stump Prefab Requirements

| Component | Description |
|-----------|-------------|
| **MeshRenderer** | Stump visual mesh |
| **VFX Socket** | Empty child transform named `VFX_Attach` for stump spark particle system |

**Tuning tip:** Stumps should be visually distinct from equipped limbs. Use exposed wiring, sparking metal, and dark/charred materials. The stump spark VFX attached to `VFX_Attach` sells the "damaged" feel.

---

## 3. Animator Controller Layers

**Edit:** Player Animator Controller
**Recommended location:** `Assets/Animation/Player/PlayerAnimatorController.controller`

### 3.1 Layer Setup

| Layer | Weight | Mask | Blending | Activation Condition |
|-------|--------|------|----------|---------------------|
| **Base** (Locomotion) | 1.0 | Full body | Override | Always active |
| **Limping** | 1.0 | Full body | Override | Missing 1 leg |
| **Crawling** | 1.0 | Full body | Override | Missing 2 legs |
| **OneHanded** | 1.0 | Upper body only | Override | Missing 1 arm |
| **Unarmed** | 1.0 | Upper body only | Override | Missing 2 arms |

### 3.2 Animation Clips Required

| Clip | Layer | Loop | Notes |
|------|-------|------|-------|
| `Locomotion_Limp_Idle` | Limping | Yes | Standing idle with weight shifted |
| `Locomotion_Limp_Walk` | Limping | Yes | Half speed, asymmetric gait |
| `Locomotion_Crawl_Idle` | Crawling | Yes | On ground, arms supporting |
| `Locomotion_Crawl_Move` | Crawling | Yes | 10% speed, ground-level movement |
| `UpperBody_OneHanded_Idle` | OneHanded | Yes | Single arm holding weapon |
| `UpperBody_OneHanded_Fire` | OneHanded | No | One-handed weapon fire |
| `UpperBody_Unarmed_Idle` | Unarmed | Yes | No weapons, defensive posture |
| `UpperBody_Unarmed_Kick` | Unarmed | No | Kick attack (no arms) |
| `UpperBody_Unarmed_Headbutt` | Unarmed | No | Headbutt attack (no arms) |

### 3.3 Animator Parameters

| Parameter | Type | Set By |
|-----------|------|--------|
| `ChassisPenalty_NoLegs` | Bool | ChassisVisualBridge |
| `ChassisPenalty_OneLeg` | Bool | ChassisVisualBridge |
| `ChassisPenalty_OneArm` | Bool | ChassisVisualBridge |
| `ChassisPenalty_NoArms` | Bool | ChassisVisualBridge |

### 3.4 Chassis State to Animation Mapping

| Chassis State | Locomotion Layer | Upper Body Layer | Speed Modifier |
|---------------|-----------------|------------------|----------------|
| Full body | Base | Base | 1.0 |
| Missing 1 leg | Limping | Base | 0.5 (no sprint) |
| Missing 2 legs | Crawling | Base (if arms) / Unarmed | 0.1 |
| Missing 1 arm | Base | OneHanded | 1.0 |
| Missing 2 arms | Base | Unarmed | 1.0 |
| Missing 1 arm + 1 leg | Limping | OneHanded | 0.5 |

**Tuning tip:** Transition blend times should be ~0.2s for chassis state changes. Abrupt snapping looks jarring when a limb is destroyed mid-combat.

---

## 4. ChassisVisualBridge MonoBehaviour

**Add Component:** `ChassisVisualBridge` on the player visual root GameObject (the object with the Animator)

### 4.1 Inspector Configuration

| Field | Description | Default |
|-------|-------------|---------|
| **HeadSocket** | Transform reference for head limb | Auto-found from `Socket_Head` |
| **TorsoSocket** | Transform reference for torso limb | Auto-found from `Socket_Torso` |
| **LeftArmSocket** | Transform reference for left arm limb | Auto-found from `Socket_LeftArm` |
| **RightArmSocket** | Transform reference for right arm limb | Auto-found from `Socket_RightArm` |
| **LeftLegSocket** | Transform reference for left leg limb | Auto-found from `Socket_LeftLeg` |
| **RightLegSocket** | Transform reference for right leg limb | Auto-found from `Socket_RightLeg` |
| **ArmStumpPrefab** | Stump model for destroyed arm slots | (required) |
| **LegStumpPrefab** | Stump model for destroyed leg slots | (required) |
| **TorsoStumpPrefab** | Stump model for destroyed torso | (required) |

### 4.2 Inspector Buttons

| Button | Action |
|--------|--------|
| **Auto-Find Sockets** | Searches bone hierarchy for standard socket names |
| **Show All Stumps** | Preview all slots as destroyed (editor only) |
| **Show Full Body** | Preview all slots equipped with starting limbs |
| **Show Random Loadout** | Preview random limb configuration |

---

## 5. VFX Prefabs

**Recommended location:** `Assets/Prefabs/VFX/Chassis/`

### 5.1 Required VFX

| VFX Prefab | Trigger | Duration | VFXCategory |
|------------|---------|----------|-------------|
| `VFX_Stump_Sparks` | Slot destroyed (persistent on stump) | Looping | Combat |
| `VFX_Limb_Equip` | Limb installed into slot | 0.5s burst | Interaction |
| `VFX_Limb_Destruction` | Limb integrity hits 0 | 1.0s burst | Combat |
| `VFX_Memory_Glow` | Limb has active district memory bonus (EPIC 1.5) | Looping | Ambient |

### 5.2 VFX Integration

All VFX go through the framework VFX/ pipeline:
1. System creates `VFXRequest` transient entity with VFXTypeId + position + optional parent
2. `VFXBudgetSystem` and `VFXLODSystem` cull as needed
3. `VFXExecutionSystem` instantiates from pool via `VFXManager.SpawnVFX()`

For persistent effects (stump sparks, memory glow), the `ChassisVisualBridge` manages the lifecycle directly since they are tied to specific sockets.

---

## 6. LimbDefinitionSO Visual Fields

Ensure every `LimbDefinitionSO` has these visual fields populated (from EPIC 1.1):

| Field | Description | Validation |
|-------|-------------|------------|
| **MeshPrefab** | Visual mesh instantiated at socket | Must have `SkinnedMeshRenderer` or `MeshRenderer` |
| **MaterialOverride** | Optional faction-colored material | null = use prefab default |
| **IconSprite** | Inventory/UI icon | 128x128 recommended |

**Tuning tip:** Limb mesh prefabs should have their pivot at the attachment point (matching socket orientation). Test by dragging the mesh prefab onto a socket transform in the scene -- it should snap into the correct position and rotation with no manual offset.

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Player Prefab | 6 socket transforms on skeleton | Named `Socket_Head`, etc. |
| Player Prefab | `ChassisVisualBridge` on visual root | Configure socket refs and stump prefabs |
| Player Prefab | Animator Controller with chassis layers | Limping, Crawling, OneHanded, Unarmed |
| VFX Assets | 4 VFX prefabs in `Assets/Prefabs/VFX/Chassis/` | Register in VFXTypeDatabase if using VFX pipeline |
| Stump Assets | 3 stump prefabs in `Assets/Prefabs/Chassis/Stumps/` | Arm, Leg, Torso variants |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Socket transforms not named correctly | `Auto-Find Sockets` fails, all references null | Rename to `Socket_Head`, `Socket_Torso`, etc. |
| Socket scale not (1,1,1) | Limb meshes appear giant or tiny at socket | Reset socket transform scale to (1,1,1) |
| Missing MeshPrefab on LimbDefinitionSO | Equipped limb shows nothing at socket, no error | Check Console for validation warning, assign mesh |
| Stump prefab missing VFX_Attach child | Stump spark VFX spawns at socket origin instead of stump | Add empty child transform named `VFX_Attach` |
| Animator layers not set to Override blending | Limping and base locomotion blend together badly | Set layer blending mode to Override |
| Animator parameter names mismatch | Chassis state changes but animation does not respond | Verify parameter names match: `ChassisPenalty_NoLegs`, etc. |
| Limb mesh pivot not at attachment point | Limb appears offset from socket position | Re-export mesh with pivot at joint attachment point |
| Forgetting to add avatar mask to upper body layers | OneHanded layer overrides leg animations | Create upper body avatar mask, assign to layer |

---

## Verification

1. **Socket Detection** -- Select player prefab, check ChassisVisualBridge inspector. All 6 sockets should be populated (green checkmarks). If any are null, click "Auto-Find Sockets" or assign manually.

2. **Equip Visual** -- Enter play mode, equip a limb. The limb mesh should appear at the correct socket:
   ```
   [ChassisVisualBridge] OnChassisChanged: LeftArm → Limb_Necrospire_LeftArm_Bone (MeshPrefab instantiated)
   ```

3. **Destroy Visual** -- Reduce limb integrity to 0. Stump prefab should appear at socket with stump spark VFX.

4. **Crawling Animation** -- Destroy both leg limbs. Player should switch to Crawling locomotion at 10% speed.

5. **Limping Animation** -- Destroy one leg. Player should limp at half speed, sprint disabled.

6. **One-Handed Animation** -- Destroy one arm. Player should hold weapon one-handed.

7. **Swap Visual** -- Equip a different limb into an occupied slot. Old visual should be removed, new visual should appear.

8. **Memory Glow** -- Equip a limb with active district memory bonus. Subtle aura VFX should appear on the limb mesh.

9. **Remote Client** -- In multiplayer, verify another player's chassis visuals update correctly via ghost replication of ChassisState.

10. **Debug Overlay** -- Toggle `chassis.visual` in console. Per-slot labels at socket positions, visual hash display, animation state readout, socket world positions as colored spheres.
