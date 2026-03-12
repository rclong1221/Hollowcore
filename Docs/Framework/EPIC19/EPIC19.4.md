# EPIC19.4 - Visual Mode Expansion

**Status:** Future
**Dependencies:** EPIC14.5 (IViewModeHandler interface)
**Goal:** Support 2D, isometric, top-down, text-only, and VR visual modes.

---

## Overview

Current system assumes 3D third-person rendering. EPIC 14.5 defined `IViewModeHandler` for FPS/TPS. This EPIC expands to non-standard visual modes.

---

## Visual Modes

| Mode | Description | Games |
|------|-------------|-------|
| `ThirdPerson3D` | Full body, behind character | DIG, Souls |
| `FirstPerson3D` | Arms only, weapon visible | Doom, COD |
| `Isometric3D` | Angled top-down, 3D models | Diablo, Path of Exile |
| `TopDown3D` | Straight-down, 3D models | Hotline Miami 3D |
| `SideScroller2D` | Side view, sprite-based | Hollow Knight |
| `TopDown2D` | Top view, sprite-based | Enter the Gungeon |
| `TextOnly` | No graphics, state only | MUDs, roguelikes |
| `VRHands` | Hand-tracked controllers | VR games |

---

## IVisualMode Interface (Extended)

Extends `IViewModeHandler` from EPIC 14.5:

| Method | Description |
|--------|-------------|
| `GetCameraRig()` | Camera setup for this mode |
| `TransformInput(input)` | Adapt input to camera orientation |
| `GetEquipmentRenderer()` | Returns renderer (3D mesh, 2D sprite, null) |
| `GetRenderingContext()` | 3D, 2D, or text |
| `OnModeEnter()` | Setup when switching to this mode |
| `OnModeExit()` | Cleanup when leaving mode |

---

## Camera Configurations

| Mode | Camera Type | Settings |
|------|-------------|----------|
| ThirdPerson3D | Follow camera | Offset behind, orbit |
| FirstPerson3D | Child of head | No body rendering |
| Isometric3D | Fixed angle | 45° or 60°, orthographic optional |
| TopDown3D | Fixed overhead | Straight down or slight angle |
| 2D modes | Orthographic | Locked Z, pixel-perfect option |
| VR | VR rig | Head-tracked, two hand cameras |

---

## Equipment Rendering by Mode

| Mode | Rendering Approach |
|------|-------------------|
| 3D modes | Mesh attached to bones |
| 2D modes | Sprite swap/overlay |
| Text only | No rendering, data only |
| VR | Mesh in hand transforms |

### 2D Equipment Rendering

Need new approach for 2D games:

| Approach | Description |
|----------|-------------|
| Sprite Swap | Change character sprite for each weapon |
| Overlay | Weapon sprite layered on character |
| Composite | Character + weapon sprites combined |

---

## Input Transformation

Different cameras need different input mapping.

| Mode | Movement Input |
|------|----------------|
| Third-Person | Forward = camera forward |
| Isometric | Forward = up-right diagonal |
| Top-Down | Forward = screen up |
| 2D Side | Right/Left only, no forward |

---

## Tasks

### Phase 1: Camera Rigs
- [ ] Create `IsometricCameraRig`
- [ ] Create `TopDownCameraRig`
- [ ] Create `OrthographicCameraRig`
- [ ] Create `VRCameraRig`

### Phase 2: 3D Mode Implementations
- [ ] Extract `ThirdPerson3DMode`
- [ ] Create `FirstPerson3DMode`
- [ ] Create `Isometric3DMode`
- [ ] Create `TopDown3DMode`

### Phase 3: 2D Support
- [ ] Create sprite-based equipment renderer
- [ ] Create `SideScroller2DMode`
- [ ] Create `TopDown2DMode`
- [ ] Handle sprite sorting

### Phase 4: Edge Cases
- [ ] Create `TextOnlyMode` (null renderer)
- [ ] Create `VRHandsMode`
- [ ] Handle mode transitions

### Phase 5: Input Mapping
- [ ] Input transformation per mode
- [ ] Aiming differences per mode

---

## Verification

- [ ] Third-person works as before
- [ ] Isometric camera correct angle
- [ ] 2D sprites render correctly
- [ ] Input feels right per mode
- [ ] Mode switching works
- [ ] VR hands track correctly

---

## Success Criteria

- [ ] Visual mode swappable via config
- [ ] All eight modes functional
- [ ] DIG unchanged
- [ ] 2D feels like native 2D game
- [ ] VR feels immersive
