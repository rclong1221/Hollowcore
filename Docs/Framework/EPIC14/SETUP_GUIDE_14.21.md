# SETUP GUIDE: Cinemachine Camera System (EPIC 14.18)

## Quick Start

### 1. Create the Camera Rig

In Unity, go to menu:
```
DIG > Camera > Create Cinemachine Camera Rig
```

This automatically creates:
- **CinemachineCameraRig** (root object with controller + shake bridge)
  - **FollowTarget** - position follows player, rotation controls orbit
  - **FPSTarget** - position at eye level for first-person
  - **CM ThirdPerson** - virtual camera for third-person view
  - **CM FirstPerson** - virtual camera for first-person view
- Adds **CinemachineBrain** to Main Camera (if not present)

### 2. Verify Components

After creation, check these are set up:

| GameObject | Required Components |
|------------|---------------------|
| Main Camera | `CinemachineBrain` |
| CM ThirdPerson | `CinemachineCamera`, `CinemachineThirdPersonFollow`, `CinemachineDeoccluder`, `CinemachineImpulseListener` |
| CM FirstPerson | `CinemachineCamera`, `CinemachineHardLockToTarget`, `CinemachinePanTilt`, `CinemachineImpulseListener` |
| CinemachineCameraRig | `CinemachineCameraController`, `CinemachineShakeBridge` |

### 3. Enter Play Mode

The system auto-initializes:
1. Finds ClientWorld
2. Locates local player entity (with `GhostOwnerIsLocal`)
3. Reads `PlayerCameraSettings` each frame
4. Updates virtual camera parameters

---

## Configuration

### CinemachineCameraController Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Active Priority | 10 | Priority for the active camera |
| Inactive Priority | 0 | Priority for inactive cameras |
| FPS Distance Threshold | 0.5 | Distance at which camera switches to first-person |
| Min Distance | 2 | Minimum zoom distance (third-person) |
| Max Distance | 15 | Maximum zoom distance (third-person) |
| Shoulder Offset | (0.5, 0, 0) | Over-the-shoulder offset |
| Eye Offset | (0, 1.7, 0) | First-person eye position relative to player |

### Adjusting Follow Target Height

If the camera is too low:

1. Select **CinemachineCameraRig > FollowTarget**
2. Set **Local Position Y** to match your character's shoulder height (typically 1.3-1.6)

### Adjusting Third-Person Distance

Select **CM ThirdPerson** and modify `CinemachineThirdPersonFollow`:

| Property | Effect |
|----------|--------|
| Camera Distance | How far behind the character |
| Shoulder Offset | X = left/right, Y = up/down from follow point |
| Vertical Arm Length | Additional vertical offset |
| Camera Side | 0 = left shoulder, 1 = right shoulder |
| Damping | Smoothness of camera follow (higher = smoother) |

### Adjusting First-Person

Select **CM FirstPerson** and modify components:

- **CinemachineHardLockToTarget**: Locks position to FPSTarget
- **CinemachinePanTilt**: 
  - `TiltAxis.Range` = vertical look limits (default: -89° to 89°)
  - `PanAxis.Wrap` = horizontal wrapping (default: true)

---

## ECS Integration

### Required Components on Player Entity

```csharp
// Camera position/rotation settings
PlayerCameraSettings {
    Yaw, Pitch,           // Current rotation
    CurrentDistance,       // Current zoom distance
    TargetDistance,        // Target zoom distance
    MinDistance, MaxDistance,
    LookSensitivity,
    ZoomSpeed
}

// View mode configuration
CameraViewConfig {
    ActiveViewType,        // Combat, Adventure, FirstPerson
    CombatMinPitch, CombatMaxPitch
}

// Player identification
PlayerTag
GhostOwnerIsLocal         // NetCode component for local player
```

### Input Flow

```
PlayerInput.LookDelta    →  CinemachineCameraSystem  →  PlayerCameraSettings.Yaw/Pitch
PlayerInput.ZoomDelta    →  CinemachineCameraSystem  →  PlayerCameraSettings.Distance
                                    ↓
                         CinemachineCameraController (MonoBehaviour)
                                    ↓
                         Updates FollowTarget rotation + ThirdPersonFollow.Distance
                                    ↓
                         CinemachineBrain applies to Main Camera
```

---

## Camera Modes

### Third-Person (Default)

- Active when `CurrentDistance > FPSDistanceThreshold`
- Camera orbits around player using mouse
- Scroll wheel adjusts distance
- Priority: CM ThirdPerson = 10, CM FirstPerson = 0

### First-Person

- Active when `CurrentDistance <= FPSDistanceThreshold` OR `ViewType = FirstPerson`
- Camera at eye level, mouse controls look direction
- Priority: CM FirstPerson = 10, CM ThirdPerson = 0

### Switching Modes

**Via Zoom:**
Scroll mouse wheel to zoom in until distance < 0.5 (auto-switches to FPS)

**Via Code:**
```csharp
// Set view type in ECS
entityManager.SetComponentData(playerEntity, new CameraViewConfig {
    ActiveViewType = CameraViewType.FirstPerson
});
```

---

## Troubleshooting

### Camera is at floor level
→ Increase FollowTarget's Y position (1.3-1.6 for typical characters)

### Camera doesn't follow player
→ Check that player entity has `PlayerCameraSettings` and `GhostOwnerIsLocal`
→ Verify ClientWorld is running

### Mouse look not working
→ Check `PlayerInput.LookDelta` is being populated from input system
→ Verify `CinemachineCameraSystem` is updating (check logs)

### Third-person can't look up/down
→ Ensure FollowTarget rotation includes pitch: `Euler(pitch, yaw, 0)`
→ Delete and recreate camera rig with latest setup script

### First-person can't look around
→ FPS camera must have `CinemachinePanTilt` (not old POV)
→ Controller must drive `PanAxis.Value` and `TiltAxis.Value`

### Camera jitters or stutters
→ Increase Damping values on ThirdPersonFollow
→ Check for competing camera systems (disable old CameraManager)

### Camera snaps when entering/exiting FPS
→ Adjust Brain's Default Blend time (higher = smoother transitions)

---

## Removing Old Camera System

The setup automatically modifies `CameraManager.cs` to defer to Cinemachine. To fully remove the old system:

1. Disable or delete `CameraManager` component from your scene
2. Remove the `PlayerCameraControlSystem.cs` if it exists (ECS)
3. Keep `CinemachineCameraSystem.cs` for input processing

---

## Advanced: Custom Virtual Cameras

To add additional camera modes (e.g., isometric, cinematic):

1. Create new GameObject as child of CinemachineCameraRig
2. Add `CinemachineCamera` component
3. Configure appropriate follow/aim components
4. Set Priority to 0 (inactive)
5. In code, change priorities to switch:

```csharp
// Example: Switch to cinematic camera
cinematicCamera.Priority = 15;  // Higher than both TPS and FPS
thirdPersonCamera.Priority = 0;
firstPersonCamera.Priority = 0;
```

---

## Files Reference

| File | Purpose |
|------|---------|
| `Assets/Scripts/Camera/Cinemachine/CinemachineCameraController.cs` | MonoBehaviour bridge between ECS and Cinemachine |
| `Assets/Scripts/Camera/Cinemachine/CinemachineCameraSystem.cs` | ECS system processing input → camera settings |
| `Assets/Scripts/Camera/Cinemachine/CinemachineShakeBridge.cs` | Converts ECS camera shake to Cinemachine impulse |
| `Assets/Scripts/Camera/Cinemachine/Editor/CinemachineCameraRigSetup.cs` | Editor menu to create camera rig |
| `Packages/manifest.json` | Contains Cinemachine 3.1.5 dependency |

---

## Version Compatibility

- **Unity**: 2022.3+ (6000.x recommended)
- **Cinemachine**: 3.1.5 (using new API: `CinemachinePanTilt`, `CinemachineDeoccluder`)
- **Entities**: Compatible with Unity.Entities 1.x
- **NetCode**: Compatible with Unity.NetCode (uses `GhostOwnerIsLocal`)
