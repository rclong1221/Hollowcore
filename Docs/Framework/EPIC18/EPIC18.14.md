# EPIC 18.14: Camera Collision & Deoccluder System

**Status:** ⏸️ PLANNED (investigation notes captured, implementation deferred)
**Priority:** Medium (visual polish — affects spectator and gameplay camera feel)
**Dependencies:**
- EPIC 18.13: Death Camera & Post-Death Experience System (spectator follow cam, kill cam)
- `CinemachineCameraController` (existing — `Assets/Scripts/Camera/Cinemachine/CinemachineCameraController.cs`)
- `DeathFollowCam` (existing — `Assets/Scripts/DeathCamera/Cameras/DeathFollowCam.cs`)
- `DeathKillCam` (existing — `Assets/Scripts/DeathCamera/Cameras/DeathKillCam.cs`)
- `CameraModeProvider` (existing — `Assets/Scripts/Camera/CameraModeProvider.cs`)
- Unity Physics (`UnityEngine.Physics`)
- Cinemachine 3 (`Unity.Cinemachine`)

**Feature:** A unified camera collision avoidance system that prevents cameras from clipping through walls, terrain, and static geometry without the flickering/oscillation caused by naive SphereCast approaches. Covers gameplay cameras (Cinemachine TPS, isometric), death spectator follow cam, kill cam orbit, and free cam. Properly handles ghost entities, player capsule colliders, and the interaction between SmoothDamp/interpolation and collision responses.

---

## Problem

Camera collision avoidance in DIG currently has multiple issues across different camera systems:

1. **Spectator follow cam (DeathFollowCam)**: A basic SphereCast from the lookAt pivot toward the camera position pulls the camera in front of walls. This oscillates with `Vector3.SmoothDamp` — SmoothDamp pulls the camera outward toward the desired position each frame, while the SphereCast snaps it inward to the hit point, creating visible flickering/bouncing. Currently **disabled** to avoid this.

2. **Player capsule self-collision**: The SphereCast uses `LayerMask ~0` (all layers) and hits the followed player's own capsule collider (Layer 8: "Player"). As the followed player turns, the hit point shifts on the capsule surface causing zoom oscillation. Partially mitigated by excluding Layer 8 and adding a start offset, but the fundamental approach is fragile.

3. **Ghost entity colliders**: Ghost entities on remote clients may have different collider configurations than the owning player. Physics shapes baked from `PhysicsShapeAuthoring` may or may not match the managed-side `CapsuleCollider` that Unity `Physics.SphereCast` tests against.

4. **Cinemachine deoccluder**: The gameplay camera (Cinemachine 3) has its own `CinemachineDeoccluder` component for collision avoidance, but it operates independently from any custom collision logic. No shared configuration between gameplay and death camera collision behavior.

5. **Kill cam orbit**: The kill cam orbits a fixed position. If the kill happened near a wall, the orbit path intersects geometry causing the camera to clip through walls during the orbit sweep.

6. **Terrain intersection**: In isometric/top-down modes, steep terrain slopes can intersect the camera-to-target line, but the fixed-angle camera has no collision avoidance at all (relies on camera height being above all terrain).

---

## Codebase Audit

### Current Collision Systems

| System | File | Approach | Status |
|--------|------|----------|--------|
| `DeathFollowCam` (TPS) | `Assets/Scripts/DeathCamera/Cameras/DeathFollowCam.cs` | SphereCast from pivot to camera | **Disabled** — oscillates with SmoothDamp |
| `DeathFollowCam` (Iso) | Same file | None | No collision avoidance in isometric mode |
| `DeathKillCam` | `Assets/Scripts/DeathCamera/Cameras/DeathKillCam.cs` | None | Camera can orbit through walls |
| `DeathFreeCam` | `Assets/Scripts/DeathCamera/Cameras/DeathFreeCam.cs` | None | Free cam clips through everything |
| `CinemachineCameraController` | `Assets/Scripts/Camera/Cinemachine/CinemachineCameraController.cs` | Cinemachine `CinemachineDeoccluder` | Working for gameplay, not shared with death cam |
| `DeathCameraConfigSO` | `Assets/Scripts/DeathCamera/Config/DeathCameraConfigSO.cs` | Config fields: `EnableCollision`, `CollisionLayers`, `CollisionRadius` | Fields exist but collision is disabled |

### Unity Physics Layers (from TagManager.asset)

| Layer | Name | Camera Collision? |
|-------|------|-------------------|
| 0 | Default | Yes — environment geometry |
| 7 | Voxel | Yes — destructible terrain |
| 8 | Player | **No** — followed player capsule |
| 9 | Ground | Yes — terrain/walkable surfaces |
| 10 | Mantleable | Yes — climbable geometry |
| 14 | Dynamic | Maybe — physics objects |
| 15 | CameraOcclusion | Yes — purpose-built for this |
| 16 | Trigger | **No** — trigger volumes |
| 17 | Zones | **No** — zone boundaries |

### Root Cause of SphereCast Oscillation

```
Frame N:
  SmoothDamp → camera at desiredPos (8m from player)
  SphereCast hits wall at 5m → camera snaps to 5m
  Camera renders at 5m ✓

Frame N+1:
  SmoothDamp starts from 5m, target is 8m → moves to 5.8m
  SphereCast hits wall at 5m → camera snaps back to 5m
  Camera renders at 5m but was briefly at 5.8m = flicker ✗

Frame N+2:
  Same as N+1 → oscillation continues forever
```

The fundamental issue: **SmoothDamp doesn't know about the collision constraint, and the collision system doesn't know about SmoothDamp's state.** They operate independently and fight each other.

---

## Architecture: Proposed Solution

### Key Insight: Separate Desired Distance from Allowed Distance

Instead of SphereCasting the smoothed position, track a `_maxAllowedDistance` that the collision system manages independently. SmoothDamp targets the desired position but is clamped to `_maxAllowedDistance`. The max distance decreases instantly (wall detected) but increases gradually (wall cleared).

```
┌──────────────────────────────────────────────────────┐
│               Camera Collision Pipeline              │
│                                                      │
│  1. Compute desiredPos (orbit angle + followDist)    │
│  2. Raycast from pivot → desiredPos direction        │
│     → update _maxAllowedDistance (instant decrease,   │
│       gradual increase with recovery speed)          │
│  3. Clamp followDistance to _maxAllowedDistance       │
│  4. SmoothDamp with clamped target                   │
│     → no oscillation because target IS the clamped   │
│       distance, not the unclamped desired             │
│                                                      │
│  Result: camera slides in smoothly, recovers slowly  │
│  No snapping. No fighting between systems.           │
└──────────────────────────────────────────────────────┘
```

### Collision Layer Mask

```csharp
// Build once at Configure() time, reuse every frame
_cameraCollisionMask = LayerMask.GetMask("Default", "Voxel", "Ground", "Mantleable", "CameraOcclusion")
                     & _collisionLayers; // AND with designer-configured mask
```

Explicitly include only environment layers. Never include Player, Trigger, Zones, UI.

### Multi-Probe Approach (for kill cam orbit)

Single SphereCast works for follow-cam (fixed direction from pivot). For orbiting cameras that sweep through geometry, use multi-probe:

```
        probe 3 (next orbit position)
       ╱
pivot ── probe 1 (current orbit position)
       ╲
        probe 2 (slightly ahead in orbit)

If probe 1 hits: pull camera in
If probe 2/3 hit: preemptively adjust orbit radius
Result: camera smoothly avoids walls during orbit
```

---

## Tasks

### Checklist
- [ ] **18.14.1**: Create `CameraCollisionResolver` utility class
- [ ] **18.14.2**: Integrate with `DeathFollowCam` (TPS mode)
- [ ] **18.14.3**: Integrate with `DeathKillCam` (orbit mode)
- [ ] **18.14.4**: Add collision to `DeathFollowCam` (isometric/top-down mode)
- [ ] **18.14.5**: Update `DeathCameraConfigSO` with refined collision settings
- [ ] **18.14.6**: Align `CinemachineDeoccluder` settings with death camera collision
- [ ] **18.14.7**: Add `CameraOcclusion` layer to blocking geometry in key scenes
- [ ] **18.14.8**: Test all paradigms × camera modes × death phases

---

### Task 18.14.1: Create CameraCollisionResolver

**File**: `Assets/Scripts/DeathCamera/Cameras/CameraCollisionResolver.cs`

A stateful utility class (not MonoBehaviour) that manages the collision distance with asymmetric smoothing: instant pull-in, gradual recovery.

```csharp
namespace DIG.DeathCamera
{
    /// <summary>
    /// Stateful camera collision resolver with asymmetric smoothing.
    /// Pull-in is instant (wall detected), recovery is gradual (wall cleared).
    /// Avoids oscillation by tracking max allowed distance separately from desired distance.
    /// </summary>
    public class CameraCollisionResolver
    {
        private float _maxAllowedDistance;
        private float _recoverySpeed;
        private float _sphereRadius;
        private int _collisionMask;
        private bool _initialized;

        public float MaxAllowedDistance => _maxAllowedDistance;

        public void Configure(float sphereRadius, int collisionMask, float recoverySpeed = 4f)
        {
            _sphereRadius = sphereRadius;
            _collisionMask = collisionMask;
            _recoverySpeed = recoverySpeed;
        }

        /// <summary>
        /// Resolve collision for a camera at the given pivot looking toward desiredPos.
        /// Returns the clamped distance from pivot.
        /// </summary>
        public float Resolve(Vector3 pivot, Vector3 desiredPos, float desiredDistance, float deltaTime)
        {
            if (!_initialized)
            {
                _maxAllowedDistance = desiredDistance;
                _initialized = true;
            }

            Vector3 dir = (desiredPos - pivot).normalized;

            // Cast from pivot toward desired camera position
            float wallDistance = desiredDistance;
            if (Physics.SphereCast(pivot, _sphereRadius, dir, out RaycastHit hit,
                                   desiredDistance, _collisionMask))
            {
                wallDistance = hit.distance - 0.1f; // Small margin
            }

            // Asymmetric smoothing: instant decrease, gradual increase
            if (wallDistance < _maxAllowedDistance)
            {
                _maxAllowedDistance = wallDistance; // Instant pull-in
            }
            else
            {
                // Gradual recovery toward desired distance
                _maxAllowedDistance = Mathf.MoveTowards(
                    _maxAllowedDistance, desiredDistance, _recoverySpeed * deltaTime);
            }

            return Mathf.Max(_maxAllowedDistance, 0.5f); // Minimum distance
        }

        public void Reset()
        {
            _initialized = false;
        }
    }
}
```

**Acceptance**: Utility class compiles. No MonoBehaviour dependency — pure C# for easy testing.

---

### Task 18.14.2: Integrate with DeathFollowCam (TPS)

**File**: `Assets/Scripts/DeathCamera/Cameras/DeathFollowCam.cs`

Replace the disabled SphereCast block in `UpdateCameraThirdPerson()` with `CameraCollisionResolver`:

```csharp
// In Configure():
_collisionResolver = new CameraCollisionResolver();
int envMask = LayerMask.GetMask("Default", "Voxel", "Ground", "Mantleable", "CameraOcclusion");
_collisionResolver.Configure(_collisionRadius, envMask & _collisionLayers);

// In UpdateCameraThirdPerson(), before SmoothDamp:
if (_collisionEnabled)
{
    float clampedDist = _collisionResolver.Resolve(
        (Vector3)lookAtTarget, (Vector3)desiredPos, _followDistance, deltaTime);

    if (clampedDist < _followDistance)
    {
        // Recompute desiredPos at clamped distance
        float3 clampedOffset = math.normalize(offset) * clampedDist;
        desiredPos = targetPos + clampedOffset;
    }
}
// SmoothDamp targets the already-clamped position → no oscillation
```

**Key change**: Collision is resolved BEFORE SmoothDamp, not after. SmoothDamp smoothly approaches the clamped position. No fighting.

**Acceptance**: Follow cam smoothly slides closer to walls, recovers slowly when wall clears. No flickering. Test in Shooter + MMO paradigms.

---

### Task 18.14.3: Integrate with DeathKillCam (Orbit)

**File**: `Assets/Scripts/DeathCamera/Cameras/DeathKillCam.cs`

Add multi-probe collision for the orbit sweep. Before computing the orbit position, probe slightly ahead in the orbit direction:

```csharp
// Probe current orbit position
float wallDist = _collisionResolver.Resolve(killPos, orbitPos, orbitRadius, deltaTime);

// If hit, reduce orbit radius for this frame
if (wallDist < orbitRadius)
{
    orbitRadius = wallDist;
    // Recompute orbitPos at reduced radius
}
```

**Acceptance**: Kill cam orbit doesn't clip through nearby walls. Camera smoothly adjusts radius when geometry is close.

---

### Task 18.14.4: Isometric/Top-Down Collision

**File**: `Assets/Scripts/DeathCamera/Cameras/DeathFollowCam.cs`

Add a simple raycast in `UpdateCameraFixedAngle()` — isometric cameras rarely intersect geometry, but steep terrain can clip:

```csharp
// Simple raycast (not SphereCast) from target to camera
if (_collisionEnabled && Physics.Raycast(targetPos, offset.normalized, out var hit,
    math.length(offset), envMask))
{
    desiredPos = targetPos + math.normalize(offset) * (hit.distance - 0.2f);
}
```

**Acceptance**: Isometric camera doesn't clip into steep cliff faces. Rare edge case but handles gracefully.

---

### Task 18.14.5: Update DeathCameraConfigSO

**File**: `Assets/Scripts/DeathCamera/Config/DeathCameraConfigSO.cs`

Replace the current collision fields with refined settings:

```csharp
[Header("Camera Collision")]
[Tooltip("Enable collision avoidance for TPS follow cam and kill cam orbit")]
public bool EnableCollision = true;

[Tooltip("Layers to collide with (environment only — never include Player)")]
public LayerMask CollisionLayers = ~0; // Overridden at runtime to env-only

[Tooltip("SphereCast radius for collision probes")]
[Range(0.1f, 0.5f)]
public float CollisionRadius = 0.2f;

[Tooltip("How fast camera recovers to desired distance after wall clears (units/sec)")]
[Range(1f, 20f)]
public float CollisionRecoverySpeed = 4f;

[Tooltip("Minimum camera distance even when colliding")]
[Range(0.3f, 2f)]
public float MinCollisionDistance = 0.5f;
```

**Acceptance**: Config asset exposes collision tuning. `CollisionRecoverySpeed` controls how fast camera pulls back out after clearing a wall.

---

### Task 18.14.6: Align CinemachineDeoccluder Settings

**File**: No code changes — scene/prefab configuration only.

Ensure the `CinemachineDeoccluder` on the gameplay TPS camera uses the same collision layer mask as the death camera system. Document the shared convention:

- **Collide with**: Default, Voxel, Ground, Mantleable, CameraOcclusion
- **Never collide with**: Player, Trigger, Zones, UI, TransparentFX, Ignore Raycast

**Acceptance**: Gameplay camera and death camera behave consistently near walls. No jarring difference in collision behavior when transitioning between gameplay and spectator.

---

### Task 18.14.7: CameraOcclusion Layer Usage

Scene setup task. Add invisible blocking volumes on Layer 15 (`CameraOcclusion`) in areas where the camera should never enter (inside buildings, behind one-way geometry, restricted areas).

**Acceptance**: Camera collision works with purpose-built blocking geometry independent of visual mesh complexity.

---

### Task 18.14.8: Full Matrix Test

Test all combinations:

| Camera | Paradigm | Phase | Collision Expected? |
|--------|----------|-------|---------------------|
| DeathFollowCam (TPS) | Shooter | Spectator | Yes — walls, terrain |
| DeathFollowCam (TPS) | MMO | Spectator | Yes — walls, terrain |
| DeathFollowCam (Iso) | ARPG | Spectator | Yes — steep terrain only |
| DeathFollowCam (Iso) | MOBA | Spectator | Yes — steep terrain only |
| DeathKillCam | All TPS | Kill Cam | Yes — orbit near walls |
| DeathFreeCam | All | Spectator | No — free cam allows clip |
| Cinemachine TPS | Shooter/MMO | Gameplay | Yes — CinemachineDeoccluder |
| Cinemachine Iso | ARPG/MOBA | Gameplay | No — height-based avoidance |

**Acceptance**: All cells verified. No flickering in any configuration.

---

## Architecture Decisions

### Why Asymmetric Smoothing Instead of SphereCast + SmoothDamp?

| Approach | Pros | Cons | Decision |
|----------|------|------|----------|
| SphereCast then SmoothDamp (current, disabled) | Simple, one-line | Oscillates — systems fight each other every frame | **Rejected** |
| Asymmetric max-distance tracking | No oscillation, smooth pull-in and recovery, stateful | Slightly more complex, needs Reset() on target switch | **Chosen** |
| Cinemachine Deoccluder on death cam | Battle-tested, production quality | Requires Cinemachine virtual camera; death cams are raw transforms, not Cinemachine cameras | Not applicable |
| Disable collision entirely | Zero flicker guaranteed | Camera clips through walls, looks bad in tight spaces | **Current fallback** |

### Why Explicit Layer Mask Instead of Config-Only?

The `CollisionLayers` field in `DeathCameraConfigSO` defaults to `~0` (all layers). This is a footgun — designers will forget to exclude Player. The code builds the mask at runtime using `LayerMask.GetMask(...)` AND with the config mask. Environment layers are always included; character layers are always excluded. Config can only restrict further, not add dangerous layers back.

---

## Lessons Learned (from EPIC 18.13 implementation)

1. **SphereCast + SmoothDamp = oscillation**. Never snap the camera to a collision hit point and then let SmoothDamp pull it back. Resolve collision BEFORE computing the SmoothDamp target.

2. **`_collisionLayers = ~0` hits player capsules**. The followed player is a ghost entity with a `PhysicsShapeAuthoring` capsule on Layer 8 ("Player"). Always exclude the Player layer from camera collision casts.

3. **Start offset alone is insufficient**. A 0.6m offset from the pivot to skip the player capsule works at some angles but fails at steep pitch angles where the cast direction passes through more of the capsule. Layer exclusion is the correct fix; offset is defense-in-depth.

4. **Ghost entity colliders on remote clients**. Ghost prefabs replicate `PhysicsCollider` via snapshot. The managed-side Unity collider may differ from what the server sees. Camera collision should only use managed-side `Physics.SphereCast`, which tests against the managed collider — this is correct for the local visual.

5. **Kill cam orbit near walls**. When a kill happens against a wall, the orbit sweep radius must contract to avoid clipping. Single-probe collision only detects the wall when the camera reaches it; multi-probe (ahead in orbit) preemptively adjusts.

---

## Files to Create

| File | Description |
|------|-------------|
| `Assets/Scripts/DeathCamera/Cameras/CameraCollisionResolver.cs` | Stateful collision resolver with asymmetric smoothing |

## Files to Modify

| File | Description |
|------|-------------|
| `Assets/Scripts/DeathCamera/Cameras/DeathFollowCam.cs` | Integrate CameraCollisionResolver in TPS + Iso modes |
| `Assets/Scripts/DeathCamera/Cameras/DeathKillCam.cs` | Add orbit collision with CameraCollisionResolver |
| `Assets/Scripts/DeathCamera/Config/DeathCameraConfigSO.cs` | Refine collision config fields |

## Files Unchanged

| File | Reason |
|------|--------|
| `Assets/Scripts/DeathCamera/Cameras/DeathFreeCam.cs` | Free cam intentionally allows clipping (noclip-style) |
| `Assets/Scripts/Camera/Cinemachine/CinemachineCameraController.cs` | Uses Cinemachine's own CinemachineDeoccluder — no changes needed |
| `Assets/Scripts/Camera/CameraModeProvider.cs` | Collision is internal to each ICameraMode implementation |

---

## Verification

1. **TPS Follow (Shooter/MMO)**: Die → spectator follows alive player near a wall → camera smoothly slides closer → player walks away from wall → camera gradually recovers to full distance. No flickering at any point.

2. **Kill cam orbit near wall**: Die against a wall → kill cam orbit starts → camera detects wall ahead in orbit → orbit radius contracts → orbit completes without clipping → radius recovers on open side.

3. **Isometric near cliff**: Die in ARPG mode near a steep cliff → camera position adjusted to avoid terrain intersection → no visual clip.

4. **Player capsule exclusion**: In all TPS modes, camera never reacts to the followed player's capsule collider. Verified by standing in open space and rotating — no zoom changes.

5. **Target switch**: Switch followed player (Tab) → `CameraCollisionResolver.Reset()` called → new collision state computed for new player position → no stale collision distance from previous player.

6. **Performance**: `CameraCollisionResolver.Resolve()` is one `Physics.SphereCast` per frame (kill cam adds 1-2 more probes). No allocations. Negligible cost.

7. **Config parity**: `CinemachineDeoccluder` on gameplay camera and `CameraCollisionResolver` on death camera use identical layer masks. Transitioning between gameplay and spectator shows no jarring collision behavior difference.
