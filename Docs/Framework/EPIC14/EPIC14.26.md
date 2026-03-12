# EPIC 14.26 - Object Gravity Climbing System

## Overview

This epic redesigns the climbing system to use an **Object Gravity** approach inspired by Breath of the Wild and modern climbing games. Instead of tracking a single grip point with raycasts, the character adheres to surfaces with a localized gravity that pulls them toward the nearest climbable surface.

### Key Differences from Current System

| Current System | Object Gravity System |
|----------------|----------------------|
| Single grip point tracked | Character adhered to surface via local gravity |
| `GripWorldPosition` + `GripWorldNormal` | `SurfaceGravityDirection` continuously updated |
| Raycast to find surfaces | Sweep/collision detection for adhesion |
| Movement projected onto surface plane | Movement in local surface coordinate space |
| Explicit edge detection for hang | Automatic detection when gravity conflicts |
| Manual normal tracking (can corrupt) | Always correct from physics |

---

## Goals

- [ ] Implement continuous surface gravity detection
- [ ] Character adheres to surfaces within adhesion range
- [ ] Smooth transitions between surfaces (corners, overhangs)
- [ ] Automatic ledge hang when surface gravity conflicts with world gravity
- [ ] Natural dismount when stepping onto walkable surfaces
- [ ] Support for destroyable voxels (dynamic surface changes)
- [ ] Support for curved surfaces (spheres, cylinders) without special cases
- [ ] Maintain network replication compatibility

---

## Architecture

### Core Concept: Surface Gravity

```
     World Gravity (0, -1, 0)
            │
            ▼
    ╔═══════════════╗
    ║   Character   ║ ◄── Surface Gravity (surface normal inverted)
    ╚═══════════════╝
            │
    ┌───────┴───────┐
    │   Surface     │
    └───────────────┘
```

When climbing, the character experiences **Surface Gravity** instead of World Gravity:
- `SurfaceGravity = -SurfaceNormal * GravityStrength`
- Character is "pulled" toward the surface they're climbing
- Movement is calculated in the surface's local coordinate space

### Surface Detection Sphere

Instead of discrete raycasts, use a **detection sphere** around the character:
- Casts in multiple directions to find nearest climbable surface
- Continuously updates `BestSurface` with position, normal, and entity
- Smooth transitions when surfaces change

```csharp
public struct ClimbGravityState : IComponentData
{
    // Surface adhesion
    public float3 SurfaceGravityDirection;  // Direction of pull toward surface
    public float3 SurfaceContactPoint;      // Where character touches surface
    public float3 SurfaceNormal;            // Always valid while climbing
    public Entity SurfaceEntity;            // For voxel destruction tracking
    
    // Adhesion parameters
    public float AdhesionStrength;          // How strongly pulled to surface (0-1)
    public float SurfaceDistance;           // Current distance to surface
    public bool IsAdhered;                  // Currently attached to a surface
    
    // State
    public bool IsClimbing;                 // Main climbing flag
    public bool IsTransitioning;            // Moving between surfaces
    public float TransitionProgress;        // 0-1 for smooth corner/edge traversal
    
    // Edge/Hang detection
    public bool AtLedgeTop;                 // Detected top edge
    public bool AtLedgeBottom;              // Detected bottom edge (potential hang)
    public float3 LedgeNormal;              // Direction of the ledge edge
}
```

---

## Destroyable Voxels Integration

### Challenge
When a voxel the player is climbing on is destroyed:
1. Surface disappears instantly
2. Player must not fall through geometry
3. Nearby surfaces should be detected and adhered to if available

### Solution: Surface Entity Tracking + Fallback Detection

```csharp
// In ClimbGravitySystem.OnUpdate()

// 1. Check if current surface still exists
if (SurfaceEntity != Entity.Null)
{
    bool surfaceDestroyed = !EntityManager.Exists(SurfaceEntity) ||
                            !HasChunkAt(surfaceEntity.ChunkPosition);
                            
    if (surfaceDestroyed)
    {
        // 2. Immediately search for nearby surfaces
        var nearbySurface = FindNearestClimbableSurface(position, searchRadius);
        
        if (nearbySurface.Found)
        {
            // 3A. Transition to new surface
            StartSurfaceTransition(nearbySurface);
        }
        else
        {
            // 3B. No surface available - fall to world gravity
            ExitClimbingState();
        }
    }
}
```

### Voxel Change Detection

Subscribe to voxel chunk modification events:

```csharp
public struct VoxelChunkModifiedEvent : IComponentData
{
    public int3 ChunkPosition;
    public bool WasDestruction;  // vs. was placement
}

// In ClimbGravitySystem
foreach (var (modEvent, entity) in SystemAPI.Query<VoxelChunkModifiedEvent>())
{
    if (modEvent.WasDestruction && IsPlayerNearChunk(modEvent.ChunkPosition))
    {
        // Force surface revalidation next frame
        climbState.SurfaceNeedsRevalidation = true;
    }
}
```

---

## Transition Types

### 1. Surface-to-Surface (Corner/Edge Traversal)

When moving along a surface and encountering a corner:

```
    Before          During          After
       │               │
       │    ┌────      └────┐      ────┐
       ●    │    ●          ●           │
       │    │                           │
            │                           │
    Wall    Corner      Ledge       Ceiling
```

The system detects conflicting normals and smoothly interpolates:
- Blend `SurfaceNormal` over `TransitionProgress`
- Maintain adhesion throughout
- No discrete state changes needed

### 2. Ledge Top Detection

When climbing up and no surface exists above:

```csharp
bool atLedgeTop = HasSurfaceBelow(checkHeight) && !HasSurfaceAbove(checkHeight);

if (atLedgeTop && verticalInput > 0)
{
    // Player wants to vault onto ledge
    TriggerVaultTransition();
}
```

### 3. Ledge Hang (Automatic)

When surface gravity conflicts with need for support:

```csharp
// Character is at top of wall, feet have no surface
bool feetUnsupported = !HasSurfaceAt(feetPosition, -SurfaceNormal);
bool handsSupported = HasSurfaceAt(handsPosition, -SurfaceNormal);

if (handsSupported && feetUnsupported)
{
    // Automatic hang state - surface gravity still applies
    // but character pose changes to hanging
    climbState.InHangPose = true;
}
```

---

## Movement System

### Local Coordinate Space

Movement input is transformed from world space to surface space:

```csharp
// Calculate surface-relative directions
float3 surfaceUp = SurfaceNormal;  // "Up" is away from surface
float3 surfaceRight = math.cross(surfaceUp, float3(0,1,0));
if (math.lengthsq(surfaceRight) < 0.001f)
    surfaceRight = math.cross(surfaceUp, float3(1,0,0));
surfaceRight = math.normalize(surfaceRight);
float3 surfaceForward = math.cross(surfaceRight, surfaceUp);

// Transform input to surface space
float3 moveDirection = inputHorizontal * surfaceRight + 
                       inputVertical * surfaceForward;

// Apply to velocity
velocity = moveDirection * climbSpeed;
```

### Auto-Alignment

Character rotation is driven by `SurfaceNormal`:

```csharp
// Character faces INTO the surface (forward = -normal)
quaternion targetRotation = quaternion.LookRotation(-SurfaceNormal, float3(0,1,0));
localTransform.Rotation = math.slerp(localTransform.Rotation, targetRotation, 
                                      rotationSpeed * deltaTime);
```

---

## Dismount Conditions

### Walkable Surface Detection

```csharp
// Check if feet are on walkable ground
bool feetOnGround = CastRay(feetPosition, -worldUp, groundCheckDist, out groundHit);
float groundAngle = math.degrees(math.acos(math.dot(groundHit.Normal, worldUp)));
bool isWalkable = groundAngle < MinSurfaceAngle;

// Check if player is trying to move toward ground
bool movingToGround = inputVertical < -0.5f && climbState.InHangPose == false;

// Auto-dismount when standing on walkable ground
if (feetOnGround && isWalkable && feetNearGround)
{
    TransitionToWorldGravity();
}
```

### Stamina Depletion

```csharp
if (stamina <= 0)
{
    // Release from surface - fall
    climbState.IsAdhered = false;
    TransitionToWorldGravity();
    TriggerFallAnimation();
}
```

---

## Implementation Plan

### Strategy: In-Place Replacement

**Constraint:** No file deletions. We reuse existing files by replacing their code contents.

---

### Phase 1: Component Transformation

| Existing File | Changes |
|---------------|---------|
| `FreeClimbComponents.cs` | Replace `FreeClimbState` internals with `ClimbGravityState` fields. Keep struct name for compatibility, use `[Obsolete]` on removed fields. |
| `FreeClimbSettings` | Add new parameters: `AdhesionStrength`, `SurfaceDetectionRadius`, `TransitionSpeed` |

**New fields in FreeClimbState → ClimbGravityState:**
```csharp
// REMOVE (old grip tracking)
// public float3 GripWorldPosition;
// public float3 GripWorldNormal;

// ADD (object gravity)
public float3 SurfaceGravityDirection;
public float3 SurfaceContactPoint;
public float3 SurfaceNormal;
public float AdhesionStrength;
public float SurfaceDistance;
public bool IsAdhered;
public bool SurfaceNeedsRevalidation;
```

---

### Phase 2: System Replacement

| Existing File | Replacement |
|---------------|-------------|
| `FreeClimbMovementSystem.cs` | Gut and replace with surface-space movement logic |
| `FreeHangDetectionSystem.cs` | Replace with unified edge/transition detection |
| `FreeClimbExitSystem.cs` | Simplify - use adhesion loss detection |
| `FreeClimbMountSystem.cs` | Replace mount logic with adhesion activation |
| `FreeClimbLedgeSystem.cs` | Merge vault logic into transition system |

**Systems to Modify (not gut):**
| File | Changes |
|------|---------|
| `CharacterControllerSystem.cs` | Check `IsAdhered` to override gravity direction |
| `PlayerAnimationStateSystem.cs` | Use `SurfaceNormal` for animation params |
| `FreeClimbAnimationEventSystem.cs` | Keep animation event handling, update field references |

---

### Phase 3: Voxel Integration

| File | Changes |
|------|---------|
| `FreeClimbMovementSystem.cs` | Add voxel destruction subscription |
| `FreeClimbComponents.cs` | Add `SurfaceNeedsRevalidation` flag |

---

### Phase 4: IK & Animation Cleanup

| File | Changes |
|------|---------|
| `FreeClimbIKController.cs` | Use `SurfaceNormal` instead of `GripWorldNormal` |
| `ClimbAnimatorBridge.cs` | Update parameter sources |

---

### File Mapping Summary

```
REUSE (gut & replace code):
├── FreeClimbComponents.cs      → ClimbGravityState fields
├── FreeClimbMovementSystem.cs  → Surface-space movement
├── FreeHangDetectionSystem.cs  → Edge/transition detection
├── FreeClimbExitSystem.cs      → Adhesion loss detection
├── FreeClimbMountSystem.cs     → Adhesion activation
└── FreeClimbLedgeSystem.cs     → Vault transitions

MODIFY (keep structure, update references):
├── CharacterControllerSystem.cs
├── PlayerAnimationStateSystem.cs
├── FreeClimbAnimationEventSystem.cs
├── FreeClimbIKController.cs
└── ClimbAnimatorBridge.cs

NO CHANGES:
├── FreeClimbWallJumpSystem.cs  → Wall jump still works same way
└── FreeClimbInputDismountSystem.cs → Input handling unchanged
```

---

## Verification Plan

### Automated Tests

1. **Surface Adhesion** - Spawn character near wall, verify adhesion activates
2. **Corner Traversal** - Move around 90° corners, verify smooth transition
3. **Overhang Climbing** - Climb onto ceiling, verify gravity inversion
4. **Voxel Destruction** - Destroy voxel under climber, verify fallback behavior
5. **Ledge Vault** - Climb to top, press up, verify vault triggers

### Manual Testing

1. Climb spherical/curved surfaces smoothly
2. Transition from wall to ceiling to opposite wall
3. Hang from ledge automatically when appropriate
4. Destroy blocks while climbing - observe behavior
5. Stamina drain → fall with proper animation

---

## Performance Considerations

### Current System
- 2-5 raycasts per frame per climbing player
- Explicit state management

### Object Gravity System
- Sphere sweep or multi-ray detection (6-12 rays)
- Continuous surface tracking
- May need LOD for detection radius based on camera distance

### Mitigation
- Use `PhysicsWorld.SphereCast` instead of multiple rays where possible
- Cache surface entity to skip revalidation when unchanged
- Only update adhesion when movement input is non-zero

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Performance regression | Profile early, add LOD for distant players |
| Breaking existing save data | Migration system for old `FreeClimbState` |
| Animation incompatibility | Keep animation hooks similar to current system |
| Network desync | Ensure `ClimbGravityState` is `[GhostField]` replicated |
| Voxel edge cases | Extensive testing in destruction scenarios |

---

## References

- **Breath of the Wild GDC Talk** - Object-oriented gravity for climbing
- **Mario Odyssey** - Local gravity zones for capture mechanics
- **Shadow of Mordor** - Contextual climbing with automatic edge detection
