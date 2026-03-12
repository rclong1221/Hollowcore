# EPIC 5.3: Environmental Atmosphere

**Priority**: LOW  
**Status**: **IMPLEMENTED** (See Assets/Scripts/Visuals/ & Player/Components/)  
**Goal**: Use volumetric lighting, dynamic shadows, and "darkness" mechanics to create tension and resource pressure.
**Dependencies**: Epic 3.1 (Environment Zones), Epic 5.2 (Flashlight)

## Design Notes
1.  **Volumetric Fog**:
    *   **Implementation**: `AtmosphereManager` (MonoBehaviour) toggles scene objects acting as Fog Volumes.
    *   **Logic**: `AtmosphereSystem` reads local player `CurrentEnvironmentZone` and updates `AtmosphereManager`.
2.  **The Dark (Stress Mechanic)**:
    *   **Logic**: Defined by `DarknessZoneConfig` (IsDark, StressMultiplier).
    *   **System**: `DarknessStressSystem` checks if Player is in Dark Zone without Flashlight.
    *   **Feedback**: High stress triggers Heartbeat audio (via `VitalAudioSystem`).
    *   **Counter**: Flashlight ON pauses stress gain and recovers.
3.  **Shadows**:
    *   **Dynamic**: Controlled by Unity Light settings on the Flashlight.

## Implemented Components
- `PlayerStressState`: Tracks `CurrentStress` (0-100).
- `EnvironmentZone`: Added `IsDark` and `StressMultiplier` fields.
- `CurrentEnvironmentZone`: Updated to cache darkness/stress values.
- `ZoneBounds`: **NEW** - Stores zone shape for simple bounds checking without physics.

## Implemented Systems
- `DarknessStressSystem`: Logic for gaining stress in dark zones.
- `AtmosphereSystem`: Presentation system for toggling Fog.
- `EnvironmentZoneDetectionSystem`: **REWRITTEN** - Uses simple bounds checks instead of physics.
- `VitalAudioSystem`: Updated to play heartbeat when Stress > 50%.

---

## Critical Fix: Physics-Free Zone Detection

### Problem
Environment zone trigger colliders were causing severe physics issues:
1. **Floating**: Players would float upward when inside zone triggers instead of falling with gravity
2. **Invisible Walls**: Players would be blocked or pushed by zone boundaries
3. **Inconsistent Behavior**: Trigger events would sometimes fire, sometimes not

### Root Cause
Unity Physics simulation was generating contacts between the player's dynamic physics body and the zone's trigger collider. Even with `CollisionResponsePolicy.RaiseTriggerEvents`, the physics solver was applying separation forces. Various attempts to fix via collision filters failed because:
- Bidirectional filter matching is complex and error-prone
- The player's `PhysicsCollider` with `PhysicsVelocity` and `PhysicsMass` made it a dynamic body
- Unity Physics treated overlapping bodies as needing separation

### Solution: Remove Physics Entirely

The definitive fix was to **completely remove physics colliders from environment zones** and use simple AABB/distance checks instead.

#### New Components

**`ZoneBounds`** (`EnvironmentZone.cs`):
```csharp
public struct ZoneBounds : IComponentData
{
    public ZoneShapeType Shape;  // Box, Sphere, or Capsule
    public float3 Center;         // World position
    public float3 HalfExtents;    // For Box shape
    public float Radius;          // For Sphere/Capsule
    public float HalfHeight;      // For Capsule
    
    public bool ContainsPoint(float3 point)
    {
        // Simple AABB/distance checks - no physics!
    }
}
```

#### Modified Baker

**`EnvironmentZoneBaker`** now:
1. Does NOT create any `PhysicsCollider` or trigger entities
2. Adds `ZoneBounds` component with pre-calculated world bounds
3. Adds `EnvironmentZone` component with zone properties
4. Uses `TransformUsageFlags.Renderable` (no physics)

#### Simplified Detection System

**`EnvironmentZoneDetectionSystem`** now:
1. Queries all zone entities with `ZoneBounds` components
2. For each player (entity with `EnvironmentSensitive`), checks if their position is inside any zone
3. Uses simple `ZoneBounds.ContainsPoint()` - no physics queries
4. Updates `CurrentEnvironmentZone` with the highest-priority matching zone

### Benefits
- **Zero physics interaction**: Zones cannot cause floating, blocking, or any physics artifacts
- **Simpler code**: No complex collision filter configuration needed
- **Better performance**: Simple math instead of physics trigger events
- **Reliable**: Always works regardless of player physics configuration

### Files Modified
- `Assets/Scripts/Runtime/Survival/Environment/Components/EnvironmentZone.cs` - Added `ZoneBounds` component
- `Assets/Scripts/Runtime/Survival/Authoring/EnvironmentZoneAuthoring.cs` - Removed physics, adds `ZoneBounds`
- `Assets/Scripts/Runtime/Survival/Environment/Systems/EnvironmentZoneDetectionSystem.cs` - Simple bounds checks

---

## Integration Guide

### 1. Player Setup
1.  Add `StressAuthoring` component to the **Main Ghost Player Prefab**.
    - Configure Max Stress (e.g., 100).
    - Configure Stress Rate (e.g., 5/sec).
    - Configure Recovery Rate (e.g. 10/sec).

### 2. Scene Setup (Atmosphere)
1.  Create an empty GameObject named **AtmosphereManager** in the scene.
2.  Add `AtmosphereManager` script.
3.  Create distinct Fog GameObjects (e.g., "SpaceFog", "CaveFog", "ShipFog") using Unity's Volume or Particle System tools. Disable them by default.
4.  In `AtmosphereManager` inspector, expand **Profiles**:
    - Element 0: Zone Type = Vacuum. Fog Volume = "SpaceFog".
    - Element 1: Zone Type = Pressurized. Fog Volume = "ShipFog".
    - Etc.

### 3. Environment Zones
1.  Create or find your Environment Zone objects in the scene.
2.  Add `EnvironmentZoneAuthoring` component:
    - **Zone Shape**: Choose Box, Sphere, or Capsule
    - **Box Size / Radius / Height**: Define the zone bounds
    - **Center**: Offset from transform position
    - **Zone Type**: Pressurized, Vacuum, Toxic, etc.
    - **Is Dark**: Whether this zone causes stress
    - **Stress Multiplier**: How fast stress builds

**Note**: NO Unity Collider components are needed. The authoring creates simple bounds data, not physics colliders.

## Testing
1.  Enter a Dark Zone (e.g. Vacuum) with Flashlight OFF.
2.  Wait ~10 seconds.
3.  Hear heartbeat audio start playing.
4.  Turn Flashlight ON (`F`).
5.  Heartbeat should fade as stress recovers.
7.  **Test Object**: Use menu `GameObject > DIG - Test Objects > Traversal > Radiation Chamber`.
8.  **Verify**: Entering the green zone applies radiation (check debug logs / UI) and triggers toxic/radioactive effects if configured.
