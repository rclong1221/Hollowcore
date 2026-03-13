# EPIC 1.6: Chassis Visual & Animation Integration

**Status**: Planning
**Epic**: EPIC 1 — Chassis & Limb System
**Dependencies**: EPIC 1.1, 1.2; Framework: Animation/, Presentation/

---

## Overview

Visual representation of the modular body. Each chassis slot has a socket on the character model. Equipped limbs show their visual prefab; empty/destroyed slots show stump models with sparking VFX. Missing legs trigger crawling locomotion. Missing arms trigger one-handed weapon animations. The visual bridge reads ECS chassis state and updates managed GameObjects.

---

## Components

```csharp
// File: Assets/Scripts/Chassis/Components/ChassisVisualComponents.cs
using Unity.Entities;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Marks that chassis visuals need updating (dirty flag).
    /// Enableable — toggled by any system that changes ChassisState.
    /// </summary>
    public struct ChassisVisualDirty : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Cached hash of current chassis configuration.
    /// If hash changes, visuals need full refresh.
    /// </summary>
    public struct ChassisVisualHash : IComponentData
    {
        public int Hash;
    }
}
```

---

## Managed Bridge

```csharp
// File: Assets/Scripts/Chassis/Bridges/ChassisVisualBridge.cs
// Managed MonoBehaviour on the player's visual root GameObject.
// Runs in PresentationSystemGroup (client-side only).
//
// Socket References (set in inspector):
//   Transform HeadSocket, TorsoSocket, LeftArmSocket, RightArmSocket, LeftLegSocket, RightLegSocket
//
// Per-slot state:
//   GameObject currentLimbVisual;   // Active limb mesh instance
//   GameObject currentStumpVisual;  // Active stump mesh instance (when destroyed)
//
// OnChassisChanged():
//   For each slot:
//     1. Read ChassisState slot entity
//     2. If entity != null:
//        a. Lookup LimbInstance → LimbDefinitionId → LimbDefinitionSO → VisualPrefab
//        b. If different from current: destroy old, instantiate new at socket
//     3. If entity == null AND slot is destroyed (DestroyedSlotsMask):
//        a. Show stump prefab at socket
//        b. Enable spark/damage VFX on stump
//     4. If entity == null AND slot is NOT destroyed:
//        a. Show empty socket (no visual — slot available for equip)
//
// Animation Integration:
//   - Read ChassisPenaltyState
//   - If NoLegs: set animator locomotion layer to "Crawling"
//   - If OneLeg: set animator locomotion layer to "Limping"
//   - If OneArm: set animator upper body layer to "OneHanded"
//   - If NoArms: set animator upper body layer to "Unarmed"
//   - Normal: default animation layers
```

---

## Animation States

| Chassis State | Locomotion Layer | Upper Body Layer | Notes |
|---|---|---|---|
| Full body | Default | Default | Normal gameplay |
| Missing 1 leg | Limping | Default | Half speed, no sprint |
| Missing 2 legs | Crawling | Default (if arms) / Unarmed | 10% speed, ground level |
| Missing 1 arm | Default | OneHanded | Pistols, blades only |
| Missing 2 arms | Default | Unarmed | Kick, headbutt only |
| Missing 1 arm + 1 leg | Limping | OneHanded | Compounded penalties |

---

## VFX

- **Stump sparks**: persistent particle effect on destroyed slot sockets
- **Rip VFX**: burst effect when limb is ripped from enemy (EPIC 1.4)
- **Equip VFX**: brief click/lock effect when limb is installed
- **Destruction VFX**: explosion/shatter when limb integrity hits 0
- **Memory glow**: subtle aura on limbs with active district memory bonus (EPIC 1.5)

All VFX go through framework VFX/ pipeline (VFXRequest → VFXExecutionSystem).

---

## Setup Guide

1. Character model must have 6 socket transforms at correct bone positions
2. Create stump prefab variants per slot (arm stump, leg stump, torso damage, head — instant death so no stump)
3. Create animation controller layers: Default, Limping, Crawling, OneHanded, Unarmed
4. Add ChassisVisualBridge MonoBehaviour to player visual root
5. Configure socket references in inspector
6. Animation workstation: author transition conditions for chassis states
7. VFX: create stump spark, equip click, destruction shatter, memory glow prefabs

---

## Verification

- [ ] Equipping a limb shows correct visual mesh at socket
- [ ] Destroying a limb shows stump visual with spark VFX
- [ ] Missing both legs → crawling animation
- [ ] Missing one leg → limping animation at half speed
- [ ] Missing one arm → one-handed weapon hold animation
- [ ] Swapping limbs: old visual removed, new visual appears
- [ ] Memory glow VFX appears on limbs with active district bonus
- [ ] Visuals update correctly on remote clients (ghost replication of ChassisState)

---

## Editor Tooling

### Chassis Visual Preview (Chassis Workstation Module)

```
// File: Assets/Editor/ChassisWorkstation/ChassisVisualPreviewModule.cs
// IWorkstationModule in the Chassis Workstation (see EPIC 1.1)
//
// Features:
// 1. **Slot Socket Viewer** — displays the character model with labeled socket transforms
//    - Drag a LimbDefinitionSO onto a slot to preview the VisualPrefab at that socket
//    - Toggle stump prefab view per slot
//    - Rotate/zoom model in preview pane
//
// 2. **Animation State Preview** — dropdown to simulate chassis penalty states:
//    - Select: "Full Body", "Missing Left Leg", "Missing Both Legs", "Missing Right Arm", etc.
//    - Plays the corresponding locomotion/upper body animation in preview
//    - No play mode required — uses AnimationUtility.SampleAnimation
//
// 3. **VFX Preview** — toggle stump spark, equip click, memory glow VFX
//    - Shows particle system preview (requires play mode for accurate timing)
//
// 4. **Remote Client Preview** — checkbox to simulate interpolated ghost visual state
//    - Verifies that ChassisState ghost replication drives visuals correctly on remote
```

### Custom Inspector: ChassisVisualBridge

```
// File: Assets/Editor/Chassis/ChassisVisualBridgeEditor.cs
// Custom editor for ChassisVisualBridge MonoBehaviour:
// - Validates all 6 socket references are assigned (error if null)
// - "Auto-Find Sockets" button: searches bone hierarchy for standard socket names
// - Preview buttons: "Show All Stumps", "Show Full Body", "Show Random Loadout"
// - Socket alignment helper: draws socket axes in Scene view when selected
```

---

## Debug Visualization

### Visual State Debug Overlay

```
// Toggle: console command `chassis.visual` or key binding
//
// In-game overlay:
// 1. Per-slot label at socket world position: "[LA] BurnArm_Rare (intact)" or "[RL] STUMP"
// 2. Visual hash display: current hash + "DIRTY" indicator when ChassisVisualDirty enabled
// 3. Animation state readout: current locomotion layer + upper body layer name
// 4. Socket world positions drawn as colored spheres (matches slot debug colors)
//
// Scene view gizmos (always when ChassisVisualBridge selected):
// - Socket positions as wire spheres (2cm radius)
// - Slot labels at each socket
// - Lines connecting each socket to the chassis child entity world position
//
// Implementation: ChassisVisualDebugSystem (ClientSimulation | LocalSimulation, PresentationSystemGroup)
```
