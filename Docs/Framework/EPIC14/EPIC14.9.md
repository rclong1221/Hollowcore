# EPIC14.9 - Isometric Camera and Input Support

**Status:** Complete (All Phases Done)
**Dependencies:** EPIC14.7 (Targeting needs camera context)
**Goal:** Add isometric/top-down camera support for ARPG games while keeping third-person for DIG.

---

## Overview

DIG uses a third-person follow camera. An ARPG roguelite like Ravenswatch uses an isometric or top-down angled camera. This EPIC adds camera abstraction so both games share the same gameplay infrastructure with different views.

---

## Relationship to Other EPICs

| EPIC | Connection |
|------|------------|
| **14.5** | Camera config becomes a ScriptableObject |
| **14.7** | Targeting input must transform for camera angle |
| **19.4** | Full visual mode abstraction (2D, VR, etc.) expands this |

---

## Camera Modes Needed

| Mode | Game | Description |
|------|------|-------------|
| **ThirdPersonFollow** | DIG | Behind-character, orbit, follow |
| **IsometricFixed** | ARPG | Fixed 45-60° angle, follows character |
| **TopDownFixed** | ARPG variant | Straight-down or slight angle |
| **IsometricRotatable** | Optional | Isometric with Q/E rotation |

---

## ICameraMode Interface

| Method | Description |
|--------|-------------|
| `Initialize(config)` | Setup camera rig |
| `Update(deltaTime)` | Per-frame update |
| `GetCameraTransform()` | Current camera position/rotation |
| `GetAimPlane()` | Plane for cursor projection |
| `TransformMovementInput(input)` | Convert WASD to world direction |
| `TransformAimInput(cursorPos)` | Convert cursor to world aim point |
| `SetTarget(entity)` | What to follow |
| `SetZoom(level)` | Camera distance/zoom |
| `Shake(intensity, duration)` | Screen shake effect |

---

## CameraConfig (ScriptableObject)

### Common Settings

| Field | Type | Description |
|-------|------|-------------|
| CameraMode | enum | ThirdPerson, Isometric, TopDown |
| FollowSmoothing | float | How quickly camera follows |
| ZoomMin | float | Minimum zoom/distance |
| ZoomMax | float | Maximum zoom/distance |
| DefaultZoom | float | Starting zoom level |
| ZoomSpeed | float | Scroll wheel sensitivity |

### Third-Person Settings

| Field | Type | Description |
|-------|------|-------------|
| FollowOffset | float3 | Offset from character |
| LookAtOffset | float3 | Where camera points |
| OrbitSpeed | float | Mouse look speed |
| MaxPitchUp | float | Look up limit |
| MaxPitchDown | float | Look down limit |
| CollisionLayers | LayerMask | What camera avoids |

### Isometric Settings

| Field | Type | Description |
|-------|------|-------------|
| IsometricAngle | float | Camera pitch (45-60°) |
| IsometricRotation | float | Camera yaw (45° for diamond) |
| IsometricHeight | float | Height above character |
| UseOrthographic | bool | Orthographic vs perspective |
| OrthoSize | float | Orthographic camera size |
| FollowDeadzone | float | Movement before camera follows |

### Top-Down Settings

| Field | Type | Description |
|-------|------|-------------|
| TopDownAngle | float | Near 90° = straight down |
| TopDownHeight | float | Height above character |

---

## Implementations

### ThirdPersonFollowCamera

Current DIG behavior. Orbit camera behind character.

| Feature | Behavior |
|---------|----------|
| Position | Behind + above character |
| Control | Mouse moves camera orbit |
| Movement | Forward = camera forward |
| Aiming | Screen center = aim direction |
| Collision | Camera avoids walls |

### IsometricFixedCamera

ARPG camera. Fixed angle, follows character.

| Feature | Behavior |
|---------|----------|
| Position | Above character at fixed angle |
| Control | No mouse orbit (fixed) |
| Movement | W = up-right, A = up-left (for 45° rotation) |
| Aiming | Cursor position projected to ground |
| Zoom | Scroll to adjust height/zoom |

**Movement Input Transformation:**

For 45° rotated isometric (diamond view):
```
Input → World Direction:
W (up)    → (+X, 0, +Z) normalized
S (down)  → (-X, 0, -Z) normalized
A (left)  → (-X, 0, +Z) normalized
D (right) → (+X, 0, -Z) normalized
```

For straight isometric:
```
Input → World Direction:
W (up)    → (0, 0, +Z)
S (down)  → (0, 0, -Z)
A (left)  → (-X, 0, 0)
D (right) → (+X, 0, 0)
```

### TopDownFixedCamera

Simplified version. Looking straight down.

| Feature | Behavior |
|---------|----------|
| Position | Directly above character |
| Control | No orbit |
| Movement | Standard world-aligned |
| Aiming | Cursor = world XZ position |

### IsometricRotatableCamera

Optional. Isometric with Q/E to rotate view.

| Feature | Behavior |
|---------|----------|
| Rotation | Q/E rotates in 45° increments |
| Movement | Transforms based on current rotation |
| Transition | Smooth rotation animation |

---

## Cursor-to-World Projection

For isometric cameras, cursor position must project to game world.

### Projection Methods

| Method | Description |
|--------|-------------|
| **GroundPlane** | Raycast cursor to Y=0 plane |
| **TerrainHit** | Raycast to terrain collider |
| **FixedHeight** | Project to character Y height |
| **SmartHeight** | Use NavMesh or terrain height |

### Implementation

```
1. Get cursor screen position
2. Create ray from camera through cursor
3. Intersect ray with ground plane (or terrain)
4. Return world position
```

This world position becomes:
- Movement target (if click-to-move)
- Aim direction (character → cursor world pos)
- Targeting point (for ground-target abilities)

---

## Integration Points

| System | How Camera Affects It |
|--------|----------------------|
| `CharacterControllerSystem` | Uses transformed movement input |
| `DIGEquipmentProvider` | May need camera-relative input |
| `TargetingSystem` (14.7) | Uses cursor-to-world projection |
| `CinemachineIntegration` | If using Cinemachine |
| `MinimapSystem` | May share camera data |

---

## Third-Party Asset Integration (The Adapter Pattern)

To ensure maximum modularity, the `ICameraMode` interface acts as an **Adapter** between DIG's game logic and whatever camera solution you choose (Vanilla Unity, Cinemachine, Rewired, ProCamera2D, etc.).

If you want to use a different camera asset:
1. **Create an Adapter:** Write a class that implements `ICameraMode`.
2. **Delegate:** In the implementation, simply call the API of your third-party asset.
3. **Plug & Play:** Assign this adapter in the config. The rest of the game (Input, Targeting, Movement) never knows the difference.

### Example: CinemachineAdapter
Instead of writing camera logic in `Update`, the adapter just configures Cinemachine:
```csharp
public class CinemachineAdapter : ICameraMode {
    private CinemachineVirtualCamera _vcam;

    public void Initialize(CameraConfig config) {
        _vcam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
        _vcam.m_Lens.FieldOfView = config.FieldOfView;
    }

    public void LinkToSubject(Entity subject, Transform visualParams) {
        _vcam.Follow = visualParams;
        _vcam.LookAt = visualParams;
    }

    public void Update(float deltaTime) {
        // Cinemachine handles movement, we just handle bridge logic if needed
    }
}
```

---

## Cinemachine Integration (Optional)

If using Cinemachine, you can provide an implementation of `ICameraMode` that wraps Cinemachine calls.

| Virtual Camera | Use Case |
|----------------|----------|
| `ThirdPersonVCam` | Behind-shoulder follow |
| `IsometricVCam` | Fixed angle, transposer follow |
| `TopDownVCam` | Overhead follow |

Switching between cameras via CinemachineBrain is handled transparently by the Adapter.

---

## Tasks

### Phase 1: Interface & Data
- [x] Create `ICameraMode` interface
- [x] Create `CameraConfig` ScriptableObject
- [x] Create `CameraMode` enum
- [x] Create input transformation utilities

### Phase 2: Implementations
- [x] Extract `ThirdPersonFollowCamera` from current code
- [x] Create `IsometricFixedCamera`
- [x] Create `TopDownFixedCamera`
- [x] Create `IsometricRotatableCamera` (optional)

### Phase 3: Input Transformation
- [x] Implement movement input remapping per camera
- [x] Implement cursor-to-world projection
- [x] Integrate with `DIGEquipmentProvider` input handling
- [x] Integrate with targeting system (14.7)

### Phase 4: Integration
- [x] Add camera selection to player prefab
- [x] Create DIG camera config (ThirdPerson)
- [x] Create ARPG camera config (Isometric)
- [x] Add to Setup Wizard templates

### Phase 5: Polish
- [x] Smooth camera transitions
- [x] Screen shake support
- [x] Zoom controls
- [x] Cinemachine integration (optional)

---

## Verification Checklist

### ThirdPerson (DIG)
- [ ] Camera follows behind character
- [ ] Mouse orbit works
- [ ] Collision avoidance works
- [ ] Movement is camera-relative

### Isometric (ARPG)
- [ ] Camera is at correct angle
- [ ] WASD movement feels natural (world-aligned or transformed)
- [ ] Cursor projects to world ground correctly
- [ ] Zoom works with scroll wheel

### Input Transformation
- [ ] WASD produces correct world directions
- [ ] Cursor aim hits correct world position
- [ ] Targeting (14.7) works with isometric view

### Switching
- [ ] Can change camera mode in config
- [ ] Game plays correctly with either mode

---

## Success Criteria

- [ ] Camera mode swappable via config only
- [ ] All three modes functional
- [ ] DIG third-person unchanged
- [ ] ARPG isometric feels like Ravenswatch/Hades
- [ ] Input transformation is seamless
- [ ] Cursor aiming works in isometric view
- [ ] No code changes to switch camera styles
