# EPIC 14.18 - Cinemachine Camera System Transition

## Overview

Transition from the current custom camera system to Unity's Cinemachine 3.x, enabling runtime switching between First-Person and Third-Person camera modes with smooth blending.

## Current State

### Primary Camera System
- **CameraManager.cs** - MonoBehaviour bridge between ECS and Unity Camera
- **PlayerCameraControlSystem.cs** - ECS system computing `CameraTarget` from `PlayerCameraSettings`
- **CameraOcclusionSystem.cs** - ECS-based occlusion detection
- **CameraSpringSolverSystem.cs** - Spring physics for recoil/shake

### ECS Components
- `CameraTarget` - World position/rotation for camera
- `PlayerCameraSettings` - Orbit distance, pitch/yaw, zoom, sensitivity
- `CameraViewConfig` - View types (Combat, Adventure, FirstPerson)
- `CameraShake` - Shake amplitude/frequency/decay
- `CameraSpringState` - Spring physics state

### Secondary System (Unused)
- `ICameraMode` interface with implementations (ThirdPersonFollowCamera, IsometricFixedCamera, etc.)
- `CinemachineAdapter.cs` - Partial Cinemachine integration (not functional)
- `CameraModeProvider.cs` - Singleton for active camera management

### Dependencies on Camera
- **VoxelInteractionSystem.cs** - Uses `Camera.main.ScreenPointToRay()` for mining
- **CameraDataSystem.cs** - Caches camera data for ECS optimization
- **DecoratorInstancingSystem.cs** - Frustum culling

## Target State

### Architecture
```
┌─────────────────────────────────────────────────────────────────┐
│                         Unity Scene                              │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐    ┌─────────────────┐                     │
│  │ CinemachineCamera│    │ CinemachineCamera│                    │
│  │ (Third-Person)   │    │ (First-Person)   │                    │
│  │ Priority: 10     │    │ Priority: 0      │                    │
│  └────────┬────────┘    └────────┬─────────┘                    │
│           │                      │                               │
│           └──────────┬───────────┘                               │
│                      ▼                                           │
│           ┌─────────────────────┐                                │
│           │  CinemachineBrain   │ (on Main Camera)               │
│           │  - Blending         │                                │
│           │  - Output to Camera │                                │
│           └──────────┬──────────┘                                │
│                      ▼                                           │
│           ┌─────────────────────┐                                │
│           │   Main Camera       │                                │
│           │   (Unity Camera)    │                                │
│           └──────────┬──────────┘                                │
│                      │                                           │
├──────────────────────┼───────────────────────────────────────────┤
│                      ▼            ECS World                      │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  CinemachineCameraSystem (NEW)                              │ │
│  │  - Reads PlayerCameraSettings (yaw, pitch, zoom)            │ │
│  │  - Updates Cinemachine target transform                     │ │
│  │  - Switches Priority based on CameraViewConfig              │ │
│  └─────────────────────────────────────────────────────────────┘ │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  CameraDataSystem (UPDATED)                                 │ │
│  │  - Reads from CinemachineBrain output camera                │ │
│  │  - Caches frustum planes for voxel systems                  │ │
│  └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### Camera Types

| Type | Cinemachine Body | Cinemachine Aim | Usage |
|------|------------------|-----------------|-------|
| Third-Person Orbit | Third Person Follow | Composer | Default exploration |
| First-Person | Hard Lock to Target | POV | Aiming, interiors |

### Runtime Switching
- Player can toggle FPS↔TPS via input (e.g., scroll wheel to 0 distance, or dedicated key)
- Cinemachine Brain handles smooth blending (0.5s default)
- ECS writes to shared follow target transform

## Implementation Phases

### Phase 1: Cinemachine Setup
1. Add Cinemachine package to manifest.json
2. Create camera rig prefab with:
   - Main Camera + CinemachineBrain
   - CinemachineCamera (Third-Person) with ThirdPersonFollow
   - CinemachineCamera (First-Person) with HardLockToTarget
   - Follow target transform (updated by ECS)
3. Create CinemachineCameraController.cs (MonoBehaviour bridge)

### Phase 2: ECS Integration
1. Create CinemachineCameraSystem.cs
   - Reads PlayerCameraSettings (yaw, pitch)
   - Reads CameraViewConfig (active view type)
   - Updates follow target transform position/rotation
   - Sets camera priorities for view switching
2. Update CameraDataSystem to read from Brain's output camera
3. Keep PlayerCameraControlSystem for input processing, remove camera positioning

### Phase 3: Shake Migration
1. Add CinemachineImpulseSource to player/weapons
2. Create CinemachineShakeBridge.cs to convert CameraShake component to impulses
3. Add CinemachineImpulseListener to virtual cameras

### Phase 4: Cleanup
1. Deprecate old CameraManager.cs (keep as fallback initially)
2. Remove redundant ICameraMode system files
3. Update VoxelInteractionSystem if needed (should work with Camera.main)

## Files Created

| File | Purpose | Status |
|------|---------|--------|
| `Assets/Scripts/Camera/Cinemachine/CinemachineCameraController.cs` | MonoBehaviour managing virtual cameras | ✅ Created |
| `Assets/Scripts/Camera/Cinemachine/CinemachineCameraSystem.cs` | ECS system updating Cinemachine | ✅ Created |
| `Assets/Scripts/Camera/Cinemachine/CinemachineShakeBridge.cs` | Converts ECS shake to Cinemachine impulse | ✅ Created |
| `Assets/Scripts/Camera/Cinemachine/Editor/CinemachineCameraRigSetup.cs` | Editor utility to create camera rig | ✅ Created |
| `Assets/Scripts/Camera/Cinemachine/DIG.CameraSystem.Cinemachine.asmdef` | Assembly definition | ✅ Created |
| `Assets/Scripts/Camera/Cinemachine/Editor/DIG.CameraSystem.Cinemachine.Editor.asmdef` | Editor assembly definition | ✅ Created |

## Files Modified

| File | Changes | Status |
|------|---------|--------|
| `Packages/manifest.json` | Added com.unity.cinemachine 3.1.5 | ✅ Done |
| `Assets/Scripts/Systems/Camera/CameraManager.cs` | Added Cinemachine detection & defer logic | ✅ Done |

## Files to Create (Manual in Unity Editor)

| File | Purpose |
|------|---------|
| `Assets/Prefabs/Camera/CinemachineCameraRig.prefab` | Camera rig prefab - Use menu: **DIG > Camera > Create Cinemachine Camera Rig** |

## Files to Modify

| File | Changes |
|------|---------|
| `Packages/manifest.json` | Add com.unity.cinemachine |
| `Assets/Scripts/Voxel/Systems/CameraDataSystem.cs` | Use Brain's output camera |
| `Assets/Scripts/Player/Systems/PlayerCameraControlSystem.cs` | Remove direct camera positioning |
| `Assets/Scripts/Systems/Camera/CameraManager.cs` | Add Cinemachine fallback detection |

## Files to Deprecate (Future)

| File | Reason |
|------|--------|
| `Assets/Scripts/Camera/ICameraMode.cs` | Replaced by Cinemachine |
| `Assets/Scripts/Camera/Implementations/*` | Replaced by Cinemachine virtual cameras |
| `Assets/Scripts/Camera/Adapters/CinemachineAdapter.cs` | Direct Cinemachine integration instead |
| `Assets/Scripts/Camera/CameraModeProvider.cs` | No longer needed |
| `Assets/Scripts/Camera/CameraTransitionManager.cs` | Cinemachine Brain handles blending |

## Configuration

### Third-Person Camera Settings
```
Body: Third Person Follow
- Shoulder Offset: (0.5, 0, 0) for over-shoulder
- Vertical Arm Length: 0.4
- Camera Distance: 3-15 (zoom range)
- Camera Side: 1 (right shoulder)
- Damping: (0.5, 0.5, 0.5)

Aim: Composer
- Tracked Object Offset: (0, 1.6, 0) head height
- Lookahead Time: 0
- Dead Zone: 0 (instant tracking)
```

### First-Person Camera Settings
```
Body: Hard Lock To Target
- Damping: (0, 0, 0) instant

Aim: POV
- Vertical Axis: -89 to 89 degrees
- Horizontal Axis: -180 to 180 degrees
```

### Brain Settings
```
Default Blend: EaseInOut, 0.5s
Update Method: Late Update
World Up Override: None (use Y-up)
```

## Testing Checklist

- [ ] Third-person camera follows player smoothly
- [ ] Mouse orbit works (yaw/pitch)
- [ ] Scroll wheel zoom works
- [ ] First-person mode activates at distance 0 or via toggle
- [ ] Smooth blend between FPS and TPS
- [ ] Camera collision avoids clipping through walls
- [ ] Camera shake works via Cinemachine Impulse
- [ ] Voxel mining raycast works correctly
- [ ] Frustum culling uses correct camera
- [ ] Multiplayer: only local player has active cameras

## Rollback Plan

If issues arise:
1. Set `CameraManager.CinemachineVirtualCameraObject` to null
2. CameraManager will fall back to direct camera control
3. Cinemachine cameras can be disabled without code changes

## Dependencies

- Unity Cinemachine 3.x (com.unity.cinemachine)
- Unity Input System (already installed)
- Unity.Entities (already installed)
- Unity.NetCode (already installed)

## Timeline Estimate

| Phase | Effort |
|-------|--------|
| Phase 1: Cinemachine Setup | 2-3 hours |
| Phase 2: ECS Integration | 3-4 hours |
| Phase 3: Shake Migration | 1-2 hours |
| Phase 4: Cleanup | 1-2 hours |
| Testing & Polish | 2-3 hours |
| **Total** | **9-14 hours** |

## Notes

- Cinemachine 3.x uses `CinemachineCamera` (not `CinemachineVirtualCamera` from 2.x)
- Third Person Follow is the recommended body for orbit cameras
- POV aim is for first-person mouse look
- Keep screen-center aiming for voxel interaction (no cursor projection needed)
