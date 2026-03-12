# Setup Guide: EPIC 13.21 (Camera & Gameplay Parity)

## Overview
This epic refactors the camera system to support modular **View Types** (Combat, First Person, Adventure) and procedural **Spring Physics** (Recoil, Shakes) for enhanced game feel.

## Player Setup (Authoring)

The camera settings are now baked via the `PlayerAuthoring` component on the Player prefab.

### 1. Camera Configuration
Locate the **PlayerAuthoring** component in the Inspector:
-   **Default View Type**: Select the starting view (Combat, Adventure, or FirstPerson).
    -   **Combat**: Over-the-shoulder TPS.
    -   **Adventure**: Free-orbit TPS.
    -   **FirstPerson**: Locked to head bone.
-   **Offsets**:
    -   `CombatPivotOffset`: Center point of orbit relative to player (default: Head height ~1.6m).
    -   `CombatCameraOffset`: Offset from pivot (e.g., Z = -2.0 puts camera 2m behind pivot).
    -   `FPSOffset`: Eye position relative to player origin.

### 2. Spring Physics (Game Feel)
Configure the dampening spring system for procedural motion:
-   **Stiffness** (0-1): How fast the spring tries to return to zero. Higher = Snappier.
-   **Damping** (0-1): How quickly velocity decays. Lower = More bounce/oscillation.
-   **Max Velocity**: Clamp for extreme forces.

## Object Fader (Occlusion)
The camera now includes a `CameraOcclusionSystem`. 
-   **Behavior**: It fires a ray from the Camera to the Player's Head.
-   **Blocking Layers**: `Environment`, `Default`, and `Ship`.
-   **Setup**: Ensure walls and large props are on the `Environment` or `Default` layer to trigger occlusion logic (currently logs to console).

## View Type Switching (Runtime)
The system supports switching views at runtime (e.g., aiming down sights).
-   **Code Usage**: Modify the `CameraViewConfig` component on the player entity.
    ```csharp
    var config = SystemAPI.GetComponent<CameraViewConfig>(playerEntity);
    config.ActiveViewType = CameraViewType.FirstPerson;
    SystemAPI.SetComponent(playerEntity, config);
    ```

## Troubleshooting
-   **Camera too jerky?** Increase `PositionSpringDamping` (closer to 1.0).
-   **Camera blocked by walls?** The Occlusion system currently only logs hits. Full transparency fading is coming in Phase 2.
-   **Head blocking view in FPS?** Ensure `FPSOffset` is slightly in front of the character mesh face.
