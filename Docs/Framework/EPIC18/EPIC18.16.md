# EPIC 18.16: Predicted Ghost Visual Smoothing

**Status:** IMPLEMENTED
**Priority:** High (visual polish — directly affects perceived game quality)
**Dependencies:**
- `TickRateConfigSystem` (existing — `Assets/Scripts/Systems/Network/TickRateConfigSystem.cs`, sets 30Hz simulation tick rate)
- `PlayerAnimatorBridgeSystem` (existing — `Assets/Scripts/Player/Systems/PlayerAnimatorBridgeSystem.cs`, presentation-layer animation bridge)
- `GhostPresentationGameObjectSystem` (Unity NetCode built-in — syncs ghost entity LocalTransform to presentation GameObject)
- `AnimatorRigBridge` (existing — `Assets/Scripts/Player/Bridges/AnimatorRigBridge.cs`, drives Animator parameters from ECS state)

**Feature:** Eliminate choppy/shaky character movement caused by the 30Hz simulation tick rate being lower than the render frame rate. The local player's presentation GameObject transform is now smoothly interpolated between tick positions, and Animator blend tree parameters are damped to prevent instant snapping.

---

## Problem

The server simulation runs at **30Hz** (`TickRateConfigSystem` sets `SimulationTickRate = 30`) with `MaxSimulationStepsPerFrame = 4`. The `PredictedFixedStepSimulationSystemGroup` — where player physics, movement, and animation state derivation all run — executes at this 30Hz tick rate.

The rendering pipeline runs at the display frame rate (typically 60-144+ FPS). Between simulation ticks, the player entity's `LocalTransform` does not change. The `GhostPresentationGameObjectSystem` copies this transform to the presentation GameObject every render frame, but the position value is identical for 2+ consecutive frames.

### Visual Result

- **From behind the character**: Movement appears smooth because depth-axis position changes (toward/away from camera) are perceptually hard to detect at 30Hz
- **From the side**: Movement appears **choppy and shaky** because lateral screen-space position changes at 30Hz are immediately visible — the character visibly "steps" across the screen
- **While crouching**: Less noticeable because crouch movement speed is slower, making 30Hz position steps smaller
- **The camera**: Appears smooth if it has its own interpolation/smoothing, creating a jarring disconnect where the camera is smooth but the character mesh stutters

### Root Cause Chain

```
1. TickRateConfigSystem sets SimulationTickRate = 30Hz
   ↓
2. PredictedFixedStepSimulationSystemGroup runs at 30Hz
   ↓
3. PlayerMovementSystem updates LocalTransform at 30Hz
   ↓
4. PlayerAnimationStateSystem updates PlayerAnimationState at 30Hz
   ↓
5. PresentationSystemGroup runs every render frame (60+ FPS)
   ↓
6. GhostPresentationGameObjectSystem copies LocalTransform → GameObject
   (same value for 2+ frames between ticks)
   ↓
7. PlayerAnimatorBridgeSystem reads PlayerAnimationState
   (same values for 2+ frames between ticks)
   ↓
8. Character visually moves at 30Hz despite rendering at 60+ FPS
```

### Additional Issue: Animator Parameter Snapping

The `AnimatorRigBridge` was setting blend tree parameters (`HorizontalMovement`, `ForwardMovement`, `Speed`) using `Animator.SetFloat(hash, value)` **without dampTime**. When Opsive's `AnimatorMonitor` was disabled to give ECS control of animation, the damping it provided was also lost. Raw keyboard input (0 or 1, no analog) snapped the blend tree position instantly, causing visible pose jumps in the Animator.

---

## Solution

### 1. Transform Visual Smoothing (PlayerAnimatorBridgeSystem)

Added exponential interpolation of the local player's presentation GameObject transform between 30Hz tick positions. After `GhostPresentationGameObjectSystem` snaps the transform to the latest tick value, the bridge system smooths it:

```csharp
[UpdateAfter(typeof(GhostPresentationGameObjectSystem))]
public partial class PlayerAnimatorBridgeSystem : SystemBase
{
    private const float PositionSmoothFactor = 25f;
    private const float RotationSmoothFactor = 20f;

    // In OnUpdate, for local player only:
    smoothed.Position = Vector3.Lerp(smoothed.Position, tickPosition, PositionSmoothFactor * deltaTime);
    smoothed.Rotation = Quaternion.Slerp(smoothed.Rotation, tickRotation, RotationSmoothFactor * deltaTime);
    presTransform.position = smoothed.Position;
    presTransform.rotation = smoothed.Rotation;
}
```

At 60fps, `PositionSmoothFactor * deltaTime = 25 * 0.016 = 0.4`, meaning the visual position moves 40% toward the tick position each render frame. This converges in ~3 frames (~50ms), eliminating visible 30Hz stepping while adding minimal visual latency.

**Key design decisions:**
- Only applies to the **local player** (remote ghosts use NetCode's built-in interpolation)
- Operates on the **presentation GameObject only** — physics, netcode, and ECS `LocalTransform` are untouched
- Uses `[UpdateAfter(typeof(GhostPresentationGameObjectSystem))]` to ensure we smooth the already-snapped transform
- Per-instance `Dictionary<int, SmoothedTransform>` keyed by GameObject instance ID
- First frame snaps to current position (no smoothing on spawn)

### 2. Animator Parameter Damping (AnimatorRigBridge)

Added `dampTime` to all movement blend tree `SetFloat` calls:

```csharp
// Before (instant snap):
animator.SetFloat(h_HorizontalMovement, horiz);

// After (smooth interpolation):
animator.SetFloat(h_HorizontalMovement, horiz, movementDampTime, deltaTime);
```

Two tunable serialized fields exposed in the Inspector:
- `movementDampTime` (default 0.1s) — for `HorizontalMovement` / `ForwardMovement`
- `speedDampTime` (default 0.15s) — for `Speed` (Idle/Walk/Run/Sprint transitions)

The `LateUpdate` re-apply (which prevents Opsive AnimatorMonitor from overwriting values) also uses damping to maintain consistency.

---

## Architecture

```
PredictedFixedStepSimulationSystemGroup (30Hz)
│
├── PlayerMovementSystem → writes LocalTransform
├── PlayerAnimationStateSystem → writes PlayerAnimationState
│
PresentationSystemGroup (every render frame)
│
├── GhostPresentationGameObjectSystem → snaps GameObject to LocalTransform (30Hz values)
│
├── PlayerAnimatorBridgeSystem [UpdateAfter GhostPresentation]
│   ├── Reads PlayerAnimationState → forwards to AnimatorRigBridge (with dampTime)
│   └── LOCAL PLAYER ONLY: Smooths presentation.transform.position/rotation
│       via exponential Lerp between current smoothed value and tick position
│
└── AnimatorRigBridge.LateUpdate()
    └── Re-applies cached movement values with dampTime (prevents Opsive overwrite)
```

---

## Files Modified

| File | Change | Lines (est.) |
|------|--------|-------------|
| `Assets/Scripts/Player/Systems/PlayerAnimatorBridgeSystem.cs` | Added `SmoothedTransform` struct, `_smoothedTransforms` dictionary, smoothing constants, `[UpdateAfter]` attribute, per-frame position/rotation Lerp for local player | ~30 |
| `Assets/Scripts/Player/Bridges/AnimatorRigBridge.cs` | Added `movementDampTime` / `speedDampTime` serialized fields, changed all movement `SetFloat()` calls to use dampTime overload, updated `LateUpdate` re-apply to also use dampTime | ~15 |

**Total:** ~45 lines changed across 2 files

---

## Performance

- **CPU cost**: One `Vector3.Lerp` + one `Quaternion.Slerp` + one `Dictionary.TryGetValue` per frame for the local player only. Negligible.
- **Memory**: One `Dictionary<int, SmoothedTransform>` entry (~40 bytes) per player instance. Negligible.
- **No new ECS components**: Avoids the player entity's 16KB archetype budget. Smoothing state lives in the managed system's dictionary.
- **No new ghost data**: Zero additional network bandwidth.

---

## Testing

- **Side-view walk test**: Orbit camera 90 degrees to the character's side. Walk forward, backward, strafe. Movement should appear smooth at any direction.
- **Sprint test**: Sprint in all directions while viewing from the side. No visible stepping.
- **Crouch comparison**: Crouching should look identical before/after (was already smooth due to slower speed).
- **Teleport test**: If teleportation is implemented, verify the smoothing doesn't cause a visible slide to the new position. If it does, reset `_smoothedTransforms` entry on teleport.
- **Frame rate test**: Cap framerate to 30fps (matches tick rate) — smoothing should have no effect. Uncap to 144+ fps — should be silky smooth.
- **Remote client test**: Remote player ghosts should be unaffected (smoothing only applies to local player).
