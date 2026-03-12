### Epic 1.6: Player Camera Controller
**Priority**: HIGH  
**Goal**: Third-person orbit camera with FPS zoom

**Tasks**:
- [X] Create `Player/Camera/PlayerCameraControlSystem.cs`
- [X] Define `PlayerCameraSettings` component:
  - [X] Orbit: `CurrentDistance`, `TargetDistance`, `MinDistance` (0), `MaxDistance` (5m), `ZoomSpeed` (10 m/s)
  - [X] Rotation: `Pitch`, `Yaw`, `LookSensitivity` (2.0), `MinPitch` (-89), `MaxPitch` (89)
  - [X] Offsets: `PivotOffset` (0, 1.6, 0), `FPSOffset` (0, 1.7, 0)
- [X] Implement look input handling (mouse delta → pitch/yaw)
- [X] Implement zoom input handling (scroll wheel → target distance)
- [X] Implement smooth distance interpolation (current → target)
- [X] Implement orbit math (calculate camera position from pitch/yaw/distance)
- [X] Implement FPS mode (distance = 0, camera at eye position)
- [X] Write calculated position/rotation to `CameraTarget` component
- [X] Add collision detection (camera doesn't clip through walls)
- [X] Test smooth transition from 3rd person to FPS
- [X] Test camera feels good during movement