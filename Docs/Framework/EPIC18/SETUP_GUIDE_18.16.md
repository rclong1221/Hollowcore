# EPIC 18.16: Predicted Ghost Visual Smoothing — Setup Guide

## Overview

EPIC 18.16 eliminates the choppy/shaky character movement caused by the 30Hz simulation tick rate. The local player's presentation GameObject is smoothly interpolated between tick positions, and Animator blend tree parameters use damping to prevent instant snapping. No new ECS components, no prefab changes, no network bandwidth increase.

---

## Prerequisites

- Unity 2022.3+ with Entities, NetCode packages
- `TickRateConfigSystem` configured (any tick rate — smoothing adapts automatically)
- `GhostPresentationGameObjectSystem` (built-in NetCode) syncing ghost transforms
- `PlayerAnimatorBridgeSystem` and `AnimatorRigBridge` on the player's presentation prefab

---

## 1. Transform Smoothing (Automatic)

Transform smoothing is built into `PlayerAnimatorBridgeSystem` and requires **no configuration**. It activates automatically for the local player.

### How It Works

After `GhostPresentationGameObjectSystem` snaps the presentation GameObject to the latest 30Hz tick position, the bridge system applies exponential interpolation:

```csharp
smoothed.Position = Vector3.Lerp(smoothed.Position, tickPosition, 25f * deltaTime);
smoothed.Rotation = Quaternion.Slerp(smoothed.Rotation, tickRotation, 20f * deltaTime);
```

### Tuning Smoothing Factors

The constants are in `PlayerAnimatorBridgeSystem.cs`:

```csharp
private const float PositionSmoothFactor = 25f;
private const float RotationSmoothFactor = 20f;
```

| Factor | Visual Latency | Smoothness | Best For |
|--------|---------------|------------|----------|
| **15** | ~70ms (4-5 frames) | Very smooth, noticeable trailing | Cinematic / slow-paced |
| **25** (default) | ~50ms (2-3 frames) | Smooth, minimal trailing | Third-person action (recommended) |
| **35** | ~35ms (1-2 frames) | Slightly visible stepping | Fast-paced, responsiveness priority |
| **50+** | ~20ms | Near-instant, minimal smoothing | If latency is unacceptable |

To change: edit the `const float` values and recompile.

### Scope

- **Local player only** — remote player ghosts use NetCode's built-in snapshot interpolation
- **Presentation only** — physics `LocalTransform`, netcode, and all ECS systems are unaffected
- **Frame-rate independent** — adapts to any render framerate automatically

---

## 2. Animator Parameter Damping

### Inspector Configuration

On the `AnimatorRigBridge` component (attached to the player's presentation prefab, e.g., `Atlas_Client`):

| Field | Default | Range | Description |
|-------|---------|-------|-------------|
| **Movement Damp Time** | 0.1 | 0 – 0.5 | Damping for `HorizontalMovement` / `ForwardMovement` blend tree parameters. Higher = smoother but less responsive. |
| **Speed Damp Time** | 0.15 | 0 – 0.5 | Damping for `Speed` parameter (Idle→Walk→Run→Sprint transitions). |

### Tuning Guide

| Damp Time | Feel | Use Case |
|-----------|------|----------|
| **0.0** | Instant snap (no damping) | Debug / testing only |
| **0.05** | Very snappy, minimal smoothing | FPS / twitch gameplay |
| **0.10** (default) | Good balance | Third-person action (recommended) |
| **0.15 – 0.20** | Very smooth, slight input lag | Cinematic / slow-paced |
| **0.30+** | Noticeable delay | Generally too sluggish |

These values are **live-tunable in the Inspector during Play mode** via the `[Range]` sliders.

---

## 3. Tradeoffs & Analysis

### Pros

- **Silky smooth visuals** at any render framerate, despite 30Hz simulation
- **Zero gameplay impact** — only the visual representation is smoothed, physics/netcode untouched
- **Minimal CPU cost** — one Lerp + one Slerp per frame for the local player only
- **No new components on the ghost entity** — avoids the 16KB archetype budget
- **Works regardless of tick rate** — if you later change to 20Hz or 60Hz, it adapts automatically

### Cons

- **~50ms visual latency** — the mesh trails ~2-3 frames behind the actual physics position. In a third-person game this is typically unnoticeable, but fast-twitch FPS players could feel it
- **Camera coupling** — if your camera reads from the ECS `LocalTransform` directly (not the smoothed GameObject), the camera and character mesh can desync by a few pixels during fast movement. If the camera follows the GameObject transform, this isn't an issue
- **Teleport/respawn edge case** — if the player teleports, the smoothing will briefly show the character sliding to the new position. You'd want to reset the smoothed state on teleport (snap `_smoothedTransforms[id]` to the new position)
- **Not tick-fraction aware** — a more precise approach would use `NetworkTime.ServerTickFraction` to interpolate exactly between tick positions. Our exponential Lerp is simpler but slightly less mathematically correct

### Responsiveness Tuning

If you want to **tighten responsiveness** later, increase `PositionSmoothFactor` (e.g., 35-40) for less lag at the cost of slightly more visible stepping. Decrease it (15-20) for buttery smooth at the cost of more latency. The current **25 is a good middle ground for third-person**.

---

## 4. Known Edge Cases

### Teleportation

If the player teleports (e.g., fast travel, respawn, scene transition), the smoothing will briefly interpolate from the old position to the new one, appearing as a fast slide. To fix, clear the smoothed state:

```csharp
// In a teleport handler, after setting the new position:
// Option A: Reset from outside the system (requires exposing a method)
// Option B: The system detects large position deltas and snaps automatically
```

**Recommended future enhancement:** Add a distance threshold check in the smoothing loop:

```csharp
float distance = Vector3.Distance(smoothed.Position, tickPosition);
if (distance > 5f) // Teleport threshold
    smoothed.Position = tickPosition; // Snap, don't interpolate
```

### Climbing / Ability Transitions

Climbing, vaulting, and other movement abilities write to `LocalTransform` at the same 30Hz rate. The smoothing applies identically to these — they will also appear smoother. If a specific ability needs instant position snapping (e.g., a grab/latch), it would need to reset the smoothed state.

### Remote Players

Remote player ghosts are **not affected** by this smoothing. They use NetCode's built-in ghost interpolation between server snapshots, which already provides smooth visual movement.

---

## 5. File Reference

| File | Purpose |
|------|---------|
| `Assets/Scripts/Player/Systems/PlayerAnimatorBridgeSystem.cs` | Transform smoothing system (lines 27-39 constants/state, lines 147-170 smoothing logic) |
| `Assets/Scripts/Player/Bridges/AnimatorRigBridge.cs` | Animator parameter damping (lines 115-122 fields, lines 352/453-454 damped SetFloat calls) |
| `Assets/Scripts/Systems/Network/TickRateConfigSystem.cs` | Tick rate configuration (30Hz) that causes the underlying issue |

---

## 6. Verification Checklist

- [ ] Walk forward while camera is orbited 90 degrees to the side — movement is smooth, no stepping
- [ ] Strafe left/right while camera is behind — smooth
- [ ] Sprint in all directions from side view — smooth
- [ ] Crouch movement — still smooth (was already acceptable, should be identical or better)
- [ ] Turn in place — rotation is smooth
- [ ] Start/stop moving — blend tree transitions are smooth (no arm/leg popping)
- [ ] Remote player movement — unaffected, uses NetCode interpolation
- [ ] Cap framerate to 30fps — smoothing has no negative effect (degrades gracefully)
- [ ] Uncap framerate to 144+ fps — maximum smoothness benefit
