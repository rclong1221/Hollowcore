# EPIC 14.9 - Isometric Camera and Input Support Setup Guide

## Overview

The Camera System provides a unified interface for different camera modes (third-person, isometric, top-down) that can be swapped via configuration. This enables DIG to support both TPS games and ARPG/isometric games with the same gameplay infrastructure.

---

## Phase Status

| Phase | Content | Status |
|-------|---------|--------|
| Phase 1 | Interface & Data | ✅ Complete |
| Phase 2 | Implementations | ✅ Complete |
| Phase 3 | Input Transformation | ✅ Complete |
| Phase 4 | Integration | ✅ Complete |
| Phase 5 | Polish | ✅ Complete |

---

## Files Created

### Phase 1: Interface & Data
| File | Purpose |
|------|---------|
| `Assets/Scripts/Camera/CameraMode.cs` | Enum for camera modes (ThirdPersonFollow, IsometricFixed, TopDownFixed, IsometricRotatable) |
| `Assets/Scripts/Camera/ICameraMode.cs` | Interface for camera mode implementations |
| `Assets/Scripts/Camera/CameraConfig.cs` | ScriptableObject for camera configuration |
| `Assets/Scripts/Camera/CameraInputUtility.cs` | Static utilities for input transformation and cursor projection |

### Phase 2: Implementations
| File | Purpose |
|------|---------|
| `Assets/Scripts/Camera/Implementations/CameraModeBase.cs` | Abstract base class with common camera functionality |
| `Assets/Scripts/Camera/Implementations/ThirdPersonFollowCamera.cs` | DIG-style orbit camera behind character |
| `Assets/Scripts/Camera/Implementations/IsometricFixedCamera.cs` | ARPG-style fixed angle camera |
| `Assets/Scripts/Camera/Implementations/TopDownFixedCamera.cs` | Near-vertical top-down camera |
| `Assets/Scripts/Camera/Implementations/IsometricRotatableCamera.cs` | Isometric with Q/E rotation support |

### Phase 3: Input Transformation
| File | Purpose |
|------|---------|
| `Assets/Scripts/Camera/CameraModeProvider.cs` | Singleton provider for centralized camera access |
| `Assets/Scripts/Camera/CameraInputBridge.cs` | Bridge for movement input transformation |
| `Assets/Scripts/Targeting/CameraAwareTargetingBase.cs` | Base class for camera-integrated targeting |
| `Assets/Scripts/Targeting/Implementations/CursorAimTargeting.cs` | Updated to use ICameraMode for cursor projection |

### Phase 4: Integration
| File | Purpose |
|------|---------|
| `Assets/Scripts/Camera/Authoring/CameraSystemAuthoring.cs` | Authoring component for player prefabs with ECS Baker |
| `Assets/Scripts/Camera/Editor/CameraConfigPresetCreator.cs` | Editor utility with menu items for preset creation |
| `Assets/Scripts/Camera/Editor/CameraSetupWizard.cs` | Multi-step wizard for camera system setup |

### Phase 5: Polish
| File | Purpose |
|------|---------|
| `Assets/Scripts/Camera/CameraTransitionManager.cs` | Smooth transitions between camera modes and configurations |
| `Assets/Scripts/Camera/CameraShakeEffect.cs` | Advanced screen shake with trauma system and directional shakes |
| `Assets/Scripts/Camera/CameraZoomController.cs` | Zoom input handling with smooth interpolation and snap points |
| `Assets/Scripts/Camera/Adapters/CinemachineAdapter.cs` | Optional Cinemachine integration adapter |

---

## Camera Modes

| Mode | Best For | Description |
|------|----------|-------------|
| ThirdPersonFollow | DIG/TPS | Behind-character orbit camera with mouse look |
| IsometricFixed | ARPG | Fixed 45-60° angle, follows character, cursor aiming |
| TopDownFixed | Twin-stick | Near-vertical camera, world-aligned movement |
| IsometricRotatable | Strategy | Isometric with Q/E rotation in 45° increments |

---

## Quick Setup (Phase 1)

### 1. Create Camera Config

1. **Right-click** in Project → `Create > DIG > Camera > Camera Config`
2. Save to `Assets/Data/Camera/` folder (create if needed)
3. Configure mode and settings:

| Field | Description |
|-------|-------------|
| `Camera Mode` | ThirdPersonFollow, IsometricFixed, TopDownFixed, IsometricRotatable |
| `Follow Smoothing` | How quickly camera follows target (higher = snappier) |
| `Zoom Min/Max` | Distance/height range for zoom |
| `Default Zoom` | Starting zoom level (0-1) |

**Or use presets programmatically:**
```csharp
var config = CameraConfig.CreateDIGPreset();       // Third-person (DIG)
var config = CameraConfig.CreateARPGPreset();      // Isometric (ARPG)
var config = CameraConfig.CreateTopDownPreset();   // Top-down
var config = CameraConfig.CreateRotatableIsometricPreset(); // Rotatable isometric
```

---

## CameraConfig Settings Reference

### Common Settings

| Field | Type | Description |
|-------|------|-------------|
| Camera Mode | enum | Which camera mode to use |
| Follow Smoothing | float | Camera follow speed (0-50) |
| Zoom Min | float | Minimum zoom/distance |
| Zoom Max | float | Maximum zoom/distance |
| Default Zoom | float | Starting zoom level (0-1) |
| Zoom Speed | float | Scroll wheel sensitivity |
| Field Of View | float | FOV in degrees (perspective only) |

### Third-Person Settings

| Field | Type | Description |
|-------|------|-------------|
| Follow Offset | Vector3 | Offset from character pivot to orbit center |
| Look At Offset | Vector3 | Where camera looks relative to character |
| Orbit Sensitivity | float | Mouse look speed |
| Max Pitch Up/Down | float | Vertical look limits (degrees) |
| Default Yaw/Pitch | float | Starting camera angles |
| Enable Collision | bool | Camera avoids walls |
| Collision Layers | LayerMask | What camera collides with |
| Collision Radius | float | Spherecast radius |
| FPS Offset | Vector3 | Position when distance = 0 |

### Isometric Settings

| Field | Type | Description |
|-------|------|-------------|
| Isometric Angle | float | Camera pitch (30-75°) |
| Isometric Rotation | float | Camera yaw (45° = diamond view) |
| Isometric Height | float | Height above character |
| Use Orthographic | bool | Orthographic vs perspective projection |
| Ortho Size | float | Orthographic camera size |
| Follow Deadzone | float | Movement before camera follows |
| Cursor Projection | enum | How cursor maps to world |
| Cursor Projection Height | float | Fixed Y for cursor projection |
| Terrain Layers | LayerMask | Layers for terrain raycast |

### Top-Down Settings

| Field | Type | Description |
|-------|------|-------------|
| Top Down Angle | float | Camera pitch (60-90°) |
| Top Down Height | float | Height above character |

### Rotatable Isometric Settings

| Field | Type | Description |
|-------|------|-------------|
| Rotation Increment | float | Degrees per Q/E press |
| Rotation Duration | float | Animation time (seconds) |

### Screen Shake Settings

| Field | Type | Description |
|-------|------|-------------|
| Shake Multiplier | float | Global shake intensity scale |
| Shake Decay | float | Amplitude reduction per second |
| Shake Frequency | float | Oscillations per second |

---

## Cursor Projection Methods

For isometric/top-down cameras, cursor position must be projected to world space.

| Method | Description | Best For |
|--------|-------------|----------|
| GroundPlane | Project to Y=0 plane | Flat terrain |
| TerrainHit | Raycast to terrain collider | Uneven terrain |
| FixedHeight | Project to character Y height | Flying/floating characters |
| SmartHeight | Terrain with fallback to character height | General use |

---

## Input Transformation Utilities

The `CameraInputUtility` static class provides methods for transforming input based on camera mode.

### Movement Input

| Method | Description |
|--------|-------------|
| `TransformThirdPersonInput(float2 input, float cameraYaw)` | Camera-relative movement |
| `TransformIsometricInput(float2 input, float isoRotation)` | Isometric-transformed movement |
| `TransformTopDownInput(float2 input)` | World-aligned movement |
| `TransformMovementInput(float2, CameraMode, float)` | Auto-dispatch based on mode |

**Isometric Movement Mapping (45° rotation):**
```
Input → World Direction:
W (up)    → (+X, 0, +Z) - screen up-right
S (down)  → (-X, 0, -Z) - screen down-left
A (left)  → (-X, 0, +Z) - screen up-left
D (right) → (+X, 0, -Z) - screen down-right
```

### Cursor Projection

| Method | Description |
|--------|-------------|
| `ProjectCursorToGroundPlane(float2, Camera, float)` | Project to Y plane |
| `ProjectCursorToTerrain(float2, Camera, LayerMask)` | Raycast to terrain |
| `ProjectCursorToFixedHeight(float2, Camera, float)` | Project to fixed Y |
| `ProjectCursor(float2, Camera, CursorProjectionMethod, CameraConfig, float)` | Auto-dispatch |

### Aim Direction

| Method | Description |
|--------|-------------|
| `CalculateAimDirection(float3 from, float3 to, bool flattenY)` | Direction between points |
| `GetThirdPersonAimDirection(Camera)` | Camera forward direction |
| `GetIsometricAimDirection(float3 charPos, float3 cursorWorld)` | Character to cursor (XZ plane) |

### Rotation Helpers

| Method | Description |
|--------|-------------|
| `SnapRotation(float current, float increment)` | Snap to nearest increment |
| `StepRotation(float current, float increment, int dir)` | Step by increment |
| `LerpRotation(float from, float to, float t)` | Interpolate with wraparound |

---

## ICameraMode Interface

The `ICameraMode` interface defines what camera implementations must provide:

| Method | Description |
|--------|-------------|
| `Initialize(CameraConfig)` | Setup camera with config |
| `UpdateCamera(float deltaTime)` | Per-frame update |
| `GetCameraTransform()` | Get camera Transform |
| `GetAimPlane()` | Get plane for cursor projection |
| `TransformMovementInput(float2)` | Convert WASD to world direction |
| `TransformAimInput(float2)` | Convert cursor to world aim point |
| `SetTarget(Entity, Transform)` | Set follow target |
| `SetZoom(float)` | Set zoom level (0-1) |
| `GetZoom()` | Get current zoom level |
| `Shake(float, float)` | Trigger screen shake |
| `HandleRotationInput(float2)` | Handle orbit/rotation input |

| Property | Description |
|----------|-------------|
| `Mode` | Current camera mode enum |
| `SupportsOrbitRotation` | True if mouse orbit works |
| `UsesCursorAiming` | True if cursor = aim direction |

---

## Integration with Targeting (EPIC 14.7)

The camera system affects targeting behavior:

| Camera Mode | Targeting Mode | Behavior |
|-------------|----------------|----------|
| ThirdPersonFollow | CameraRaycast | Aim at screen center |
| IsometricFixed | CursorAim | Aim toward cursor world position |
| TopDownFixed | CursorAim | Aim toward cursor world position |

The camera implementations integrate with `ITargetingSystem`:
- `TransformAimInput()` provides cursor-to-world projection
- `GetAimPlane()` provides the plane for cursor raycasting
- `TransformMovementInput()` provides camera-relative movement

---

## Using Camera Implementations (Phase 2)

### Basic Setup

1. Add one of the camera mode components to your Camera GameObject:
   - `ThirdPersonFollowCamera` - For DIG/TPS games
   - `IsometricFixedCamera` - For ARPG games
   - `TopDownFixedCamera` - For twin-stick/roguelikes
   - `IsometricRotatableCamera` - For strategy games

2. Create a `CameraConfig` asset and assign it.

3. Initialize in your game code:

```csharp
using DIG.CameraSystem;
using DIG.CameraSystem.Implementations;

public class GameCameraController : MonoBehaviour
{
    [SerializeField] private CameraConfig _config;
    [SerializeField] private ThirdPersonFollowCamera _camera;

    void Start()
    {
        _camera.Initialize(_config);
    }

    void LateUpdate()
    {
        _camera.UpdateCamera(Time.deltaTime);
    }
}
```

### ThirdPersonFollowCamera

DIG-style orbit camera with mouse look:

```csharp
var camera = GetComponent<ThirdPersonFollowCamera>();

// Set target to follow
camera.SetTarget(playerEntity, playerTransform);

// Handle mouse orbit input
camera.HandleRotationInput(new float2(mouseDeltaX, mouseDeltaY));

// Get camera-relative movement direction
float3 moveDir = camera.TransformMovementInput(new float2(horizontal, vertical));

// Zoom control (scroll wheel)
camera.AdjustZoom(Input.mouseScrollDelta.y);

// Access angles
float yaw = camera.Yaw;
float pitch = camera.Pitch;
```

### IsometricFixedCamera

ARPG-style fixed angle camera:

```csharp
var camera = GetComponent<IsometricFixedCamera>();

// Set target to follow
camera.SetTarget(playerEntity, playerTransform);

// Get isometric movement direction (transformed for camera angle)
float3 moveDir = camera.TransformMovementInput(new float2(horizontal, vertical));

// Get cursor world position for aiming
float3 aimPoint = camera.GetCursorWorldPosition();
float3 aimDir = camera.GetAimDirection();

// Zoom control
camera.AdjustZoom(Input.mouseScrollDelta.y);

// Snap to target (after teleport/respawn)
camera.SnapToTarget();
```

### TopDownFixedCamera

Near-vertical camera for twin-stick games:

```csharp
var camera = GetComponent<TopDownFixedCamera>();

// Set target to follow
camera.SetTarget(playerEntity, playerTransform);

// Get world-aligned movement (no transformation)
float3 moveDir = camera.TransformMovementInput(new float2(horizontal, vertical));

// Get cursor world position
float3 aimPoint = camera.GetCursorWorldPosition();
float3 aimDir = camera.GetAimDirection();
```

### IsometricRotatableCamera

Isometric with Q/E rotation:

```csharp
var camera = GetComponent<IsometricRotatableCamera>();

// Rotate camera with Q/E
if (Input.GetKeyDown(KeyCode.Q)) camera.RotateLeft();
if (Input.GetKeyDown(KeyCode.E)) camera.RotateRight();

// Movement is automatically transformed based on current rotation
float3 moveDir = camera.TransformMovementInput(new float2(horizontal, vertical));

// Check rotation state
if (camera.IsRotating)
{
    // Camera is animating rotation
}
float currentRotation = camera.Rotation;

// Set rotation directly (snaps instantly)
camera.SetRotation(45f);
```

### Common Features

All camera modes share these features:

```csharp
// Zoom control
camera.SetZoom(0.5f);  // 0 = closest, 1 = farthest
float zoom = camera.GetZoom();

// Screen shake
camera.Shake(0.5f, 0.3f);  // intensity, duration

// Check camera capabilities
if (camera.SupportsOrbitRotation) { /* mouse look supported */ }
if (camera.UsesCursorAiming) { /* cursor = aim direction */ }

// Get camera transform
Transform camTransform = camera.GetCameraTransform();

// Get aim plane for raycasting
Plane aimPlane = camera.GetAimPlane();
```

---

## Input Transformation (Phase 3)

### CameraModeProvider

Singleton that provides centralized access to the active camera:

```csharp
using DIG.CameraSystem;

// Set the active camera
CameraModeProvider.Instance.SetActiveCamera(myCamera);

// Access from anywhere
ICameraMode camera = CameraModeProvider.Instance.ActiveCamera;
CameraMode mode = CameraModeProvider.Instance.CurrentMode;

// Check camera capabilities
if (CameraModeProvider.Instance.UsesCursorAiming)
{
    // Isometric/top-down camera
}

// Get camera rotation for input transformation
float yaw = CameraModeProvider.Instance.GetCameraRotation();

// Auto-detect camera in scene
CameraModeProvider.Instance.AutoDetectCamera();
```

### CameraInputBridge

Transforms raw WASD input to world-space movement direction:

```csharp
using DIG.CameraSystem;

// Static method (uses singleton)
float2 rawInput = new float2(horizontal, vertical);
float3 worldDir = CameraInputBridge.Transform(rawInput);

// With explicit camera yaw (from PlayerCameraSettings)
float3 worldDir = CameraInputBridge.Transform(rawInput, cameraYaw, isCameraYawValid);

// Instance method
var bridge = GetComponent<CameraInputBridge>();
float3 worldDir = bridge.TransformMovementInput(rawInput);

// Manual mode (when not using CameraModeProvider)
bridge.SetManualMode(CameraMode.IsometricFixed);
bridge.SetManualRotation(45f);
```

### CameraAwareTargetingBase

Base class for targeting implementations that need camera context:

```csharp
using DIG.Targeting;

public class MyTargeting : CameraAwareTargetingBase
{
    public override TargetingMode Mode => TargetingMode.CursorAim;

    protected override void PerformTargeting()
    {
        // Project cursor using camera system
        float3 cursorWorld = GetCursorWorldPosition();

        // Calculate aim based on camera mode
        float3 aimDir = CalculateCameraAwareAimDirection(_characterPosition);

        // Get aim plane
        Plane aimPlane = GetAimPlane();

        // Transform movement (for reference)
        float3 moveDir = TransformMovementInput(rawInput);
    }
}
```

### Updated CursorAimTargeting

The `CursorAimTargeting` class now inherits from `CameraAwareTargetingBase`:

- Uses `ICameraMode.TransformAimInput()` for cursor projection when available
- Falls back to legacy ground plane raycast if no camera system
- Toggle via `_useCameraSystem` field in Inspector

```csharp
// CursorAimTargeting automatically uses camera system
var targeting = GetComponent<CursorAimTargeting>();

// Set Use Camera System = true in Inspector
// It will use ICameraMode.TransformAimInput() for cursor projection

// Or disable for legacy behavior
// Set Use Camera System = false
```

---

## Third-Party Camera Integration

The `ICameraMode` interface acts as an adapter for any camera solution:

### Using Cinemachine

```csharp
public class CinemachineAdapter : MonoBehaviour, ICameraMode
{
    [SerializeField] private CinemachineVirtualCamera _vcam;

    public CameraMode Mode => CameraMode.ThirdPersonFollow;

    public void Initialize(CameraConfig config)
    {
        _vcam.m_Lens.FieldOfView = config.FieldOfView;
        // Configure Cinemachine body/aim based on config
    }

    public void SetTarget(Entity entity, Transform visualTransform)
    {
        _vcam.Follow = visualTransform;
        _vcam.LookAt = visualTransform;
    }

    public void UpdateCamera(float deltaTime)
    {
        // Cinemachine handles updates automatically
    }

    // ... implement remaining interface methods
}
```

### Using Other Assets

1. Create a class implementing `ICameraMode`
2. Delegate interface calls to your asset's API
3. Assign the adapter as the active camera mode
4. The rest of DIG (input, targeting, movement) works unchanged

---

## Preset Configurations

### DIG Preset (Third-Person)

```csharp
var config = CameraConfig.CreateDIGPreset();
// Mode: ThirdPersonFollow
// Zoom: 0-20m (default ~8m)
// Pitch: 25° default, 80° up / 60° down limits
// Orbit sensitivity: 0.15
// Collision: enabled
```

### ARPG Preset (Isometric)

```csharp
var config = CameraConfig.CreateARPGPreset();
// Mode: IsometricFixed
// Angle: 50° pitch, 45° rotation (diamond view)
// Height: 15m
// Zoom: 10-25m
// Cursor projection: GroundPlane
```

### Top-Down Preset

```csharp
var config = CameraConfig.CreateTopDownPreset();
// Mode: TopDownFixed
// Angle: 85° (near vertical)
// Height: 20m
// Zoom: 8-30m
```

---

## Namespace

All camera system types are in the `DIG.CameraSystem` namespace:

```csharp
using DIG.CameraSystem;
```

Types available:
- `CameraMode` - enum
- `CursorProjectionMethod` - enum
- `ICameraMode` - interface
- `CameraConfig` - ScriptableObject
- `CameraInputUtility` - static utility class

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `CameraConfig` not in Create menu | Ensure scripts compiled (check Console for errors) |
| `CameraMode` enum not found | Add `using DIG.CameraSystem;` |
| Cursor projection returns wrong position | Check `CursorProjection` setting matches terrain setup |
| Movement feels wrong in isometric | Verify `IsometricRotation` matches visual camera angle |
| Zoom not working | Check `ZoomMin` < `ZoomMax` |

---

## Phase 5: Polish Features

### Camera Transitions

Use `CameraTransitionManager` for smooth transitions between camera modes:

```csharp
using DIG.CameraSystem;

// Transition to a new camera mode
var newCamera = GetComponent<IsometricFixedCamera>();
CameraTransitionManager.Instance.TransitionToCamera(newCamera, 0.5f, () =>
{
    Debug.Log("Transition complete!");
});

// Transition to new config on current camera
CameraTransitionManager.Instance.TransitionToConfig(newConfig, 0.3f);

// Smooth zoom transition
CameraTransitionManager.Instance.TransitionZoom(0.7f, 0.25f);

// Cancel/skip transition
CameraTransitionManager.Instance.CancelTransition();

// Custom easing curves
CameraTransitionManager.Instance.SetTransitionCurve(
    CameraTransitionManager.CreateEaseOutCurve()
);

// Check transition state
if (CameraTransitionManager.Instance.IsTransitioning)
{
    float progress = CameraTransitionManager.Instance.TransitionProgress;
}
```

### Screen Shake

Use `CameraShakeEffect` for advanced screen shake:

```csharp
using DIG.CameraSystem;

// Simple shake
CameraShakeEffect.TriggerShake(0.5f, 0.3f);  // intensity, duration

// Directional shake (from explosion)
CameraShakeEffect.TriggerDirectionalShake(0.7f, 0.4f, explosionDirection);

// Shake from world position
CameraShakeEffect.Instance.ShakeFromPosition(0.8f, 0.5f, explosionPosition);

// Preset shakes
CameraShakeEffect.Instance.ShakeLight();    // Footsteps, light hits
CameraShakeEffect.Instance.ShakeMedium();   // Melee hits, small explosions
CameraShakeEffect.Instance.ShakeHeavy();    // Big explosions, boss attacks
CameraShakeEffect.Instance.ShakeExtreme();  // Death, massive impacts

// Trauma system (accumulating shake)
CameraShakeEffect.TriggerTrauma(0.3f);  // Adds to existing trauma
CameraShakeEffect.Instance.SetTrauma(0.5f);  // Set directly
CameraShakeEffect.Instance.ResetTrauma();

// Continuous rumble (call every frame)
CameraShakeEffect.Instance.Rumble(0.2f);

// Check shake state
if (CameraShakeEffect.Instance.IsShaking)
{
    float intensity = CameraShakeEffect.Instance.CurrentShakeIntensity;
}
```

### Zoom Controls

Use `CameraZoomController` for zoom input handling:

```csharp
using DIG.CameraSystem;

// Basic zoom control
CameraZoomController.DoZoomIn(0.1f);
CameraZoomController.DoZoomOut(0.1f);
CameraZoomController.DoZoomTo(0.5f, instant: false);

// Instance methods
var zoomController = CameraZoomController.Instance;
zoomController.ZoomTo(0.3f);
zoomController.ZoomToMin();  // Closest
zoomController.ZoomToMax();  // Farthest
zoomController.ResetZoom();

// Snap points (for preset zoom levels)
zoomController.CycleZoomUp();
zoomController.CycleZoomDown();

// Configure at runtime
zoomController.EnableScrollZoom = true;
zoomController.ScrollSensitivity = 1.5f;

// Check zoom state
float currentZoom = zoomController.CurrentZoom;
float targetZoom = zoomController.TargetZoom;
if (zoomController.IsZooming)
{
    // Zoom is animating
}
```

### Cinemachine Integration

Use `CinemachineAdapter` to integrate with Cinemachine:

**Setup:**
1. Add `CINEMACHINE_ENABLED` to Project Settings > Scripting Define Symbols
2. Add `CinemachineAdapter` component to a GameObject
3. Assign your Cinemachine virtual camera
4. Register with CameraModeProvider

```csharp
using DIG.CameraSystem.Adapters;

// Setup adapter
var adapter = GetComponent<CinemachineAdapter>();
adapter.Initialize(cameraConfig);
adapter.SetTarget(playerEntity, playerTransform);

// Register with provider
CameraModeProvider.Instance.SetActiveCamera(adapter);

#if CINEMACHINE_ENABLED
// Switch virtual cameras with blending
adapter.SwitchToCamera(newVirtualCamera, blendTime: 0.5f);

// Trigger Cinemachine impulse
adapter.TriggerImpulse(1.0f);

// Access underlying virtual camera
var vcam = adapter.VirtualCamera;
#endif
```

**Features:**
- Full ICameraMode implementation
- Automatic Cinemachine body/aim configuration
- Camera blending via CinemachineBrain
- Cinemachine Impulse integration for shake
- Fallback shake for non-Impulse setups

---

## Complete Setup Example

```csharp
using DIG.CameraSystem;
using DIG.CameraSystem.Implementations;
using UnityEngine;

public class GameCameraSetup : MonoBehaviour
{
    [SerializeField] private CameraConfig _config;
    [SerializeField] private Transform _playerTransform;

    private ICameraMode _camera;

    void Start()
    {
        // Create camera based on config
        _camera = CreateCameraForMode(_config.CameraMode);
        _camera.Initialize(_config);
        _camera.SetTarget(default, _playerTransform);

        // Register with provider
        CameraModeProvider.Instance.SetActiveCamera(_camera);

        // Optional: Setup zoom controller
        var zoom = gameObject.AddComponent<CameraZoomController>();

        // Optional: Setup shake effect
        var shake = gameObject.AddComponent<CameraShakeEffect>();
    }

    private ICameraMode CreateCameraForMode(CameraMode mode)
    {
        var cam = Camera.main;

        switch (mode)
        {
            case CameraMode.ThirdPersonFollow:
                return cam.gameObject.AddComponent<ThirdPersonFollowCamera>();
            case CameraMode.IsometricFixed:
                return cam.gameObject.AddComponent<IsometricFixedCamera>();
            case CameraMode.TopDownFixed:
                return cam.gameObject.AddComponent<TopDownFixedCamera>();
            case CameraMode.IsometricRotatable:
                return cam.gameObject.AddComponent<IsometricRotatableCamera>();
            default:
                return cam.gameObject.AddComponent<ThirdPersonFollowCamera>();
        }
    }

    void LateUpdate()
    {
        _camera.UpdateCamera(Time.deltaTime);
    }
}
```

---

## All Phases Complete

EPIC 14.9 is now fully implemented with:
- 4 camera mode implementations (Phase 2)
- Input transformation and targeting integration (Phase 3)
- Player prefab authoring and setup wizard (Phase 4)
- Camera transitions, shake, zoom, and Cinemachine support (Phase 5)
