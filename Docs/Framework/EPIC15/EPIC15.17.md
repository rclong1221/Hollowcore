# EPIC 15.17: Vision / Line-of-Sight System

**Status:** ✅ Implemented
**Dependencies:** None (Greenfield Implementation)
**Feature:** AI Vision and Senses

## Overview
We lack a generalized system for AI to "See" things. Currently, AI just knows where the player is (cheating) or uses simple distance checks. We need a proper cone-of-vision and raycast occlusion system to drive Aggro and Stealth mechanics.

## Architecture

The system follows a modular, data-driven design with three layers:

```
┌─────────────────────────────────────────────────┐
│  Consumers (Aggro, Stealth UI, AI Behavior)     │
│  Read SeenTargetElement buffer                  │
├─────────────────────────────────────────────────┤
│  Systems Layer                                  │
│  VisionDetectionSystem → VisionDecaySystem      │
├─────────────────────────────────────────────────┤
│  Core Layer (Swappable)                         │
│  VisionQueryUtility  │  VisionLayers            │
├─────────────────────────────────────────────────┤
│  Data Layer                                     │
│  VisionSensor  │  SeenTargetElement             │
│  Detectable    │  VisionSettings                │
└─────────────────────────────────────────────────┘
```

**Extensibility:** Replace `VisionQueryUtility` to swap detection algorithms without touching systems or components. Collision filters centralized in `VisionLayers` for single-source-of-truth.

## Implemented Components

### ✅ 1. `VisionSensor` Component
Authoring component for AI agents defining their eyes.

```csharp
// Assets/Scripts/Vision/Components/VisionSensor.cs
public struct VisionSensor : IComponentData
{
    public float ViewDistance;       // e.g., 20m
    public float ViewAngle;          // Half-angle, e.g., 45° (90° cone)
    public float EyeHeight;          // e.g., 1.6m
    public float UpdateInterval;     // Per-entity throttle (perf)
    public float TimeSinceLastUpdate; // Internal accumulator
}
```

Collision filters are centralized in `VisionLayers` (not per-component) for single-source-of-truth.

### ✅ 2. `SeenTargetElement` Buffer
A dynamic buffer to store what the AI currently perceives.

```csharp
// Assets/Scripts/Vision/Components/SeenTargetElement.cs
[InternalBufferCapacity(8)]
public struct SeenTargetElement : IBufferElementData
{
    public Entity Entity;
    public float3 LastKnownPosition;
    public float TimeSinceLastSeen;   // 0 = visible right now
    public bool IsVisibleNow;
}
```

### ✅ 3. `Detectable` Component
Marker on entities that can be seen. Implements `IEnableableComponent` for runtime toggling.

```csharp
// Assets/Scripts/Vision/Components/Detectable.cs
public struct Detectable : IComponentData, IEnableableComponent
{
    public float DetectionHeightOffset;  // Raycast target point
    public float StealthMultiplier;      // 1.0 = normal, 0.5 = crouching, 0 = invisible
}
```

### ✅ 4. `VisionSettings` Singleton
Global configuration for the vision system.

```csharp
// Assets/Scripts/Vision/Components/VisionSettings.cs
public struct VisionSettings : IComponentData
{
    public float GlobalUpdateInterval;    // Default 0.2s
    public float MemoryDuration;          // Default 5.0s
    public int MaxRaycastsPerFrame;       // Default 64
    public bool EnableStealthModifiers;   // Default true
}
```

### ✅ 5. `VisionDetectionSystem`
Server-authoritative 3-phase detection pipeline:

1. **Broad Phase:** `CollisionSpatialQueryUtility.OverlapSphere` — find candidates within `ViewDistance`
2. **Cone Check:** `VisionQueryUtility.IsInCone` — discard targets outside FOV
3. **Occlusion Check:** `VisionQueryUtility.HasLineOfSight` — raycast through `OcclusionFilter`
4. **Buffer Update:** Add/update `SeenTargetElement` entries

```
WorldSystemFilter: ServerSimulation | LocalSimulation
UpdateInGroup: SimulationSystemGroup
```

### ✅ 6. `VisionDecaySystem`
Lightweight per-frame system that increments `TimeSinceLastSeen` and prunes expired entries.

```
UpdateAfter: VisionDetectionSystem
```

### ✅ 7. `VisionQueryUtility` (Core — Swappable)
Pure static methods for detection math:
- `IsInCone()` — dot product cone check with squared distance early-out
- `HasLineOfSight()` — occlusion raycast via Unity Physics
- `CanSee()` — combined: distance → cone → LOS with stealth multiplier

### ✅ 8. `VisionLayers`
Centralized collision filter factories:
- `DetectableFilter` — what sensors look FOR (Player layer)
- `OcclusionFilter` — what BLOCKS vision (Default, Environment, Ship)

### ✅ 9. Stealth Integration (Groundwork)
- `Detectable.StealthMultiplier` reduces effective `ViewDistance` per-target
- `VisionSettings.EnableStealthModifiers` master toggle
- Other systems modify `StealthMultiplier` at runtime (crouch → 0.5, prone → 0.3, etc.)

### ✅ 10. Authoring Components
- `VisionSensorAuthoring` — bakes VisionSensor + SeenTargetElement buffer
- `DetectableAuthoring` — bakes Detectable with configurable stealth/offset
- `VisionSettingsAuthoring` — bakes global VisionSettings singleton

### ✅ 11. Debug Tester
`VisionDebugTester` MonoBehaviour:
- Draws vision cones as gizmos in Scene view
- Draws lines to seen targets (green = visible, yellow = remembered)
- Shows stats in Inspector (sensor count, seen targets, visible now)
- Runtime overrides for update interval and stealth multiplier

## File Manifest

```
Assets/Scripts/Vision/
├── Components/
│   ├── VisionSensor.cs
│   ├── SeenTargetElement.cs
│   ├── Detectable.cs
│   └── VisionSettings.cs
├── Core/
│   ├── VisionQueryUtility.cs
│   └── VisionLayers.cs
├── Systems/
│   ├── VisionDetectionSystem.cs
│   └── VisionDecaySystem.cs
├── Authoring/
│   ├── VisionSensorAuthoring.cs
│   ├── DetectableAuthoring.cs
│   └── VisionSettingsAuthoring.cs
└── Debug/
    └── VisionDebugTester.cs
```

## Integration Points
- **Aggro System (EPIC 15.19):** Aggro only starts if `IsVisibleNow` becomes true (or hearing ranges).
- **UI:** Stealth meter can look at "How many enemies have Player in their `SeenTargetElement` buffer".
- **Targeting System:** Can use `VisionQueryUtility` helpers for LOS checks instead of inline raycasts.

## Verification Plan
1. Place AI with `VisionSensorAuthoring` (ViewDistance=20, ViewAngle=45).
2. Place Player with `DetectableAuthoring` behind wall. Buffer should be empty.
3. Player walks out. Buffer should populate with Player Entity, `IsVisibleNow = true`.
4. Player walks behind wall. `IsVisibleNow` becomes false, `LastKnownPosition` updates.
5. Wait `MemoryDuration` seconds. Entry is pruned from buffer.
6. Test stealth: Set `StealthMultiplier = 0.5` — effective detection range should halve.
7. Use `VisionDebugTester` to visualize cones and verify behavior in Scene view.
