### Epic 1.2: Camera System (Generic)
**Priority**: CRITICAL  
**Goal**: Create reusable camera system that works for Player, Ship, and Spectator modes

**Tasks**:
- [X] Create `Systems/Camera/CameraComponents.cs` with `CameraTarget` component (Position, Rotation, FOV, NearClip, FarClip)
- [X] Create `Systems/Camera/CameraManager.cs` MonoBehaviour
- [X] Implement camera target detection (finds entity with `CameraTarget` + `GhostOwnerIsLocal`)
- [X] Implement smooth camera interpolation (position and rotation)
- [X] Add camera shake support (amplitude, frequency, decay)
- [X] Add FOV transition support (smooth zoom in/out)
- [X] Test with existing player capsule