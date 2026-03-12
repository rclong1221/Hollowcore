# EPIC 18.15: Click-to-Move & WASD Gating — Setup Guide

## Overview

EPIC 18.15 makes MOBA and ARPG paradigms fully functional. When a paradigm profile has `wasdEnabled = false`, WASD keys no longer produce movement — click-to-move becomes the sole movement input. The click-to-move pipeline uses **ECS physics raycasting** (Unity.Physics) to detect ground surfaces baked from subscenes, and routes through A* Pathfinding Project when available.

**Parent framework:** This builds on EPIC 15.20 (Input Paradigm Framework). See `Docs/EPIC15/SETUP_GUIDE_15.20.md` for the full paradigm system setup.

---

## Prerequisites

- Unity 2022.3+ with Entities, NetCode, Unity.Physics, and Input System packages
- EPIC 15.20 paradigm framework set up (ParadigmStateMachine, profiles, subsystems)
- Ground/terrain geometry baked into an **ECS subscene** (required for click-to-move raycasting)
- A* Pathfinding Project (optional — click-to-move falls back to direct movement without it)

---

## 1. Paradigm Profile Configuration

Click-to-move behavior is entirely driven by **InputParadigmProfile** ScriptableObjects. No code changes are needed to enable or disable click-to-move for a paradigm — just edit the profile asset.

### Location

`Assets/Data/Input/Profiles/`

Select any profile in the Project window to edit it in the Inspector.

### Movement Fields That Control Click-to-Move

| Field | Type | Description |
|-------|------|-------------|
| **WASD Enabled** | bool | If `false`, WASD keys produce zero movement. Only click-to-move (or path following) can move the character. |
| **Click To Move Enabled** | bool | If `true`, the ClickToMoveHandler processes mouse clicks for movement. |
| **Click To Move Button** | Enum | Which mouse button triggers click-to-move: `None`, `LeftButton`, or `RightButton`. |
| **Use Pathfinding** | bool | If `true`, clicks route through A* Pathfinding Project. If `false` (or A* is unavailable), the character moves in a straight line toward the click point. |

### Shipped Profile Settings

| Profile | WASD | Click-to-Move | Button | Pathfinding | Behavior |
|---------|------|---------------|--------|-------------|----------|
| **Profile_Shooter** | On | Off | None | Off | Standard WASD + mouse look |
| **Profile_ShooterHybrid** | On | Off | None | Off | Same as Shooter + Alt to free cursor |
| **Profile_MMO** | On | Off | None | Off | WASD + RMB orbit + A/D turn |
| **Profile_ARPG_Classic** | Off | On | LMB | On | Click-to-move only. Diablo 2/3 style. |
| **Profile_ARPG_Hybrid** | On | Off | None | Off | WASD with isometric camera. Diablo 4 / Last Epoch style. |
| **Profile_TwinStick** | On | Off | None | Off | WASD + cursor aim |
| **Profile_MOBA** | Off | On | RMB | On | RMB click-to-move. League of Legends style. |

### Creating a Custom Click-to-Move Profile

1. **Assets > Create > DIG/Input/Input Paradigm Profile**
2. Set **WASD Enabled** to `false` if click-to-move should be the only movement method
3. Set **Click To Move Enabled** to `true`
4. Set **Click To Move Button** to `LeftButton` or `RightButton`
5. Set **Use Pathfinding** to `true` if you have an A* graph in the scene
6. Assign the profile to **ParadigmStateMachine > Available Profiles**

### Validation

If **Click To Move Enabled** is `true` but **Click To Move Button** is `None`, a warning will appear in the Console on asset save:

> `[InputParadigmProfile] ProfileName: clickToMoveEnabled but no button set.`

---

## 2. ClickToMoveHandler (Runtime)

The **ClickToMoveHandler** MonoBehaviour manages click detection, raycasting, pathfinding requests, and waypoint following. It auto-creates at runtime — no manual scene placement is required.

### Auto-Creation

The handler creates itself via `[RuntimeInitializeOnLoadMethod]` before any scene loads. It persists across scene loads via `DontDestroyOnLoad`. You will see a `[ClickToMoveHandler]` GameObject in the Hierarchy at runtime.

### Optional: Manual Scene Placement

If you want to pre-configure tuning values in the editor:

1. Create an empty GameObject in your boot/persistent scene
2. Name it `[ClickToMoveHandler]`
3. Add the **ClickToMoveHandler** component
4. Configure tuning values in the Inspector (see table below)

If the handler already auto-created, the duplicate will be destroyed automatically.

### Inspector Fields

| Field | Default | Description |
|-------|---------|-------------|
| **Waypoint Reach Distance** | 0.5 | How close the character must get to an intermediate waypoint before advancing to the next one (world units) |
| **Destination Reach Distance** | 0.3 | How close the character must get to the final destination before the path is considered complete (world units) |
| **Max Raycast Distance** | 200 | Maximum distance for the click-to-ground raycast (world units). Increase if your camera is very high above the terrain. |
| **Repath Interval** | 0.2 | Minimum seconds between re-path requests while holding the click button (hold-to-move). Lower = more responsive but more A* path calculations. |
| **Log Path Events** | false | Print path request, completion, error, and arrival messages to the Console |
| **Draw Path Gizmos** | false | Draw the current path in the Scene view (cyan lines, yellow sphere = current waypoint, green sphere = destination) |

### Hold-to-Move

When the player **holds** the configured mouse button (instead of a single click), the handler continuously re-paths at the **Repath Interval** rate. The character follows the cursor in real time. Release to stop re-pathing (the character finishes walking to the last requested point).

### WASD Interruption

- If the active profile has **WASD Enabled = true** (e.g., a hypothetical hybrid mode with both WASD and click-to-move), pressing any WASD key immediately cancels the active path.
- If the active profile has **WASD Enabled = false** (MOBA, ARPG Classic), WASD keys are ignored and will NOT cancel the path.

---

## 3. A* Pathfinding Setup

Click-to-move uses **A* Pathfinding Project** for pathfinding when the profile has **Use Pathfinding = true**. If A* is not available, click-to-move falls back to direct straight-line movement.

### Scene Requirements

1. Add an **AstarPath** MonoBehaviour to a GameObject in your scene (or subscene)
2. Configure a graph:
   - **RecastGraph** is recommended for 3D terrain with obstacles
   - **GridGraph** works for flat terrain
3. **Scan** the graph in the editor (**A* > Scan** or the Scan button in the AstarPath inspector)

### Runtime Behavior

| A* State | Behavior |
|----------|----------|
| AstarPath present + graph scanned | Full pathfinding — character navigates around obstacles |
| AstarPath present + no graph | Warning in Console, falls back to direct movement |
| AstarPath not in scene | Silent fallback to direct movement |
| Path request fails (e.g., click on unreachable area) | Warning in Console, falls back to direct movement toward the click point |

### No A* Required for Testing

You can test click-to-move without A* installed. The character will move in a straight line toward the click point (ignoring obstacles). This is useful for verifying the input pipeline works before investing in navmesh setup.

---

## 4. Ground Surface Requirements

Click-to-move uses **ECS physics raycasting** (Unity.Physics) to find the ground position under the cursor. This means ground geometry must exist in the ECS physics world.

### What Works

- Terrain or mesh geometry **baked from an ECS subscene** — these automatically have `PhysicsCollider` components
- Any entity with a `PhysicsShapeAuthoring` or legacy collider that gets baked into the subscene

### What Does NOT Work

- GameObjects with legacy colliders that are **not** in a subscene (these exist only in PhysX, not Unity.Physics)
- Procedural geometry created at runtime without `PhysicsCollider` components

### Collision Filtering

The raycast currently uses a broad collision filter that hits environment, default geometry, and ships. Clicks on player or enemy physics bodies may produce unexpected move targets. If you notice the character pathing to an enemy's position instead of the ground behind them, this is a known limitation tracked for optimization.

### Verification

1. Enter Play Mode in a scene with baked ground geometry
2. Switch to MOBA or ARPG Classic paradigm
3. Click on the ground — character should move to the click point
4. If nothing happens, enable **Log Path Events** on ClickToMoveHandler and check the Console for raycast results

---

## 5. Camera Compatibility

Click-to-move converts world-space movement directions into **camera-relative input**. This works with all camera angles, including steep top-down cameras.

### Supported Camera Modes

| Camera Mode | Click-to-Move Support | Notes |
|-------------|----------------------|-------|
| **IsometricFixed** | Full | Default for ARPG Classic |
| **IsometricRotatable** | Full | Q/E rotation updates the camera-relative conversion automatically |
| **TopDownFixed** | Full | Default for MOBA. Nearly vertical camera uses yaw-based fallback. |
| **ThirdPersonFollow** | Full | Works but not a typical use case for click-to-move |

### Steep Camera Fallback

When the camera is nearly straight-down (top-down or steep isometric), the standard camera-forward vector projected onto the XZ plane approaches zero length. The system automatically falls back to the camera's **yaw rotation** to determine forward/right directions. This ensures click-to-move works at any camera angle, including 85-90 degree top-down views.

---

## 6. Testing Paradigm Transitions

Use the **ParadigmDemoUI** (if present in your scene) or switch paradigms via code to verify correct behavior across all transitions.

### Expected Behavior Matrix

| Paradigm | WASD Movement | Click-to-Move | Click Button | Path Cancels on WASD |
|----------|--------------|---------------|--------------|---------------------|
| Shooter | Yes | No | — | — |
| Shooter Hybrid | Yes | No | — | — |
| MMO | Yes | No | — | — |
| ARPG Classic | No | Yes | LMB | No (WASD ignored) |
| ARPG Hybrid | Yes | No | — | — |
| Twin-Stick | Yes | No | — | — |
| MOBA | No | Yes | RMB | No (WASD ignored) |

### Transition Test Cases

| Test | Steps | Expected Result |
|------|-------|-----------------|
| Shooter to MOBA | Switch paradigm, press WASD | Character does not move |
| Shooter to MOBA | Switch paradigm, RMB click on ground | Character paths to click point |
| MOBA hold-to-move | Hold RMB and drag | Character continuously follows cursor |
| MOBA to Shooter | Switch paradigm, press WASD | Character moves normally |
| ARPG Classic to Shooter | Switch paradigm mid-path | Path cancels, WASD resumes |
| MOBA with WASD | Press W while path-following | Path continues (WASD does not interrupt) |
| Round-trip | Shooter > MOBA > Shooter > ARPG > Shooter | WASD works every time after returning |

---

## 7. Multiplayer / Networked Behavior

Click-to-move input flows through the standard NetCode prediction pipeline. The server never needs A* or the ClickToMoveHandler.

### How It Works

1. **Client**: ClickToMoveHandler computes a camera-relative direction each frame
2. **Client**: Direction is written to `PlayerInputState.PathMoveDirection`
3. **Client**: `PlayerInputSystem` packs it into the networked `PlayerInput` component (`PathMoveX`, `PathMoveY`, `IsPathFollowing`)
4. **Server**: `PlayerInputDecodeSystem` unpacks it into `PlayerInputComponent.Move`
5. **Server**: `PlayerMovementSystem` applies movement — identical to WASD input from the server's perspective

### Server-Side WASD Gating

The server also gates WASD via the `ParadigmSettings` ECS singleton (defense-in-depth). Even if a modified client sends WASD data while in MOBA mode, the server will ignore it. This prevents desync from paradigm sync delays.

---

## 8. Debug Tools

### Console Logging

Enable **Log Path Events** on the ClickToMoveHandler Inspector to see:

| Log Tag | Message Content |
|---------|-----------------|
| `[ClickToMoveHandler] Configured:` | Profile applied — shows enabled state, button, pathfinding flag |
| `[ClickToMoveHandler] Path requested:` | Start and end positions of path request |
| `[ClickToMoveHandler] Path complete:` | Number of waypoints in computed path |
| `[ClickToMoveHandler] Path error:` | A* pathfinding error details |
| `[ClickToMoveHandler] Destination reached` | Character arrived at final waypoint |
| `[ClickToMoveHandler] Path cancelled by WASD input` | WASD interrupted the path (only when WASD is enabled) |
| `[ClickToMoveHandler] Direct move to ...` | Using direct movement (no pathfinding) |

### Scene View Gizmos

Enable **Draw Path Gizmos** on the ClickToMoveHandler Inspector:

| Gizmo | Color | Meaning |
|-------|-------|---------|
| Lines between waypoints | Cyan | The computed A* path |
| Small spheres at waypoints | Cyan | Intermediate waypoints |
| Larger sphere at end | Green | Final destination |
| Medium sphere | Yellow | Current target waypoint |

Gizmos only appear in the **Scene view** (not Game view) and only while a path is active.

### Dev Console Commands

If EPIC 18.9 (Dev Console) is set up, these existing commands are useful for testing click-to-move:

| Command | Use Case |
|---------|----------|
| `tp <x> <y> <z>` | Teleport player to test click-to-move from different positions |
| `speed <multiplier>` | Adjust movement speed to test path following at different speeds |
| `god on` | Prevent death during testing in hazardous areas |

---

## 9. Verification Checklist

After setup, verify click-to-move is working end-to-end:

1. [ ] Ground geometry is baked in an ECS subscene (not just legacy GameObjects)
2. [ ] `Profile_MOBA.asset` has `wasdEnabled: 0`, `clickToMoveEnabled: 1`, `clickToMoveButton: RightButton`
3. [ ] `Profile_ARPG_Classic.asset` has `wasdEnabled: 0`, `clickToMoveEnabled: 1`, `clickToMoveButton: LeftButton`
4. [ ] Enter Play Mode — `[ClickToMoveHandler]` appears in Hierarchy (auto-created)
5. [ ] Switch to MOBA paradigm — cursor is visible and free
6. [ ] RMB click on ground — character moves to click point
7. [ ] Hold RMB and drag — character follows cursor continuously
8. [ ] Press WASD — character does NOT move (WASD gated)
9. [ ] Switch to ARPG Classic — LMB click on ground moves character
10. [ ] Switch to Shooter — WASD moves character, clicking does nothing
11. [ ] (If A* configured) Character navigates around obstacles instead of walking through them
12. [ ] (If multiplayer) Remote clients see MOBA player moving via click-to-move, no phantom WASD movement

---

## 10. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Click-to-move does nothing (no movement) | Ground is not in ECS physics world | Ensure ground/terrain is baked from a subscene. Legacy-only colliders won't be hit. |
| Character moves with WASD in MOBA/ARPG | Profile has `wasdEnabled: true` | Check the profile asset — set `wasdEnabled` to `false` |
| Character moves toward enemies instead of ground | Raycast collision filter is too broad | Known limitation — a future optimization will filter to environment-only layers |
| Path immediately cancels after clicking | WASD key is slightly held / sticky key | If in a WASD-enabled paradigm, this is expected. In MOBA/ARPG (WASD disabled), this should not happen. |
| Character walks in wrong direction | Camera-relative conversion mismatch | Verify the active camera mode matches the profile's **Compatible Camera Modes** |
| "A* graph not configured" warning | No AstarPath in scene or graph not scanned | Add AstarPath and scan the graph, or set `usePathfinding: false` for direct movement |
| ClickToMoveHandler not in Hierarchy | Auto-init failed or destroyed by duplicate | Check Console for errors. Only one instance should exist. |
| Click registered but no movement in multiplayer | ParadigmSettings not synced to server | Verify `ParadigmSettingsSyncSystem` is running in both client and server worlds |
| Hold-to-move feels unresponsive | Repath interval too high | Lower **Repath Interval** on the ClickToMoveHandler (e.g., 0.1s). This increases A* load. |
| Path gizmos not visible | Gizmos disabled in Scene view | Click the Gizmos toggle in the Scene view toolbar. Gizmos only show during active paths. |

---

## 11. Designer Tips

- **Tuning reach distances**: If the character overshoots waypoints (runs past then doubles back), increase **Waypoint Reach Distance**. If the character stops too far from the click point, decrease **Destination Reach Distance**.
- **Hold-to-move responsiveness vs performance**: Lower **Repath Interval** values (0.05–0.1s) feel smoother but request more A* paths per second. For complex navmeshes, keep it at 0.2s or higher.
- **Creating hybrid modes**: You can create a profile with both `wasdEnabled: true` and `clickToMoveEnabled: true`. In this configuration, WASD works normally and also cancels any active click-to-move path. The ARPG Hybrid profile demonstrates the WASD-only variant (click-to-move disabled).
- **Testing without A***: Set `usePathfinding: false` on any profile to test click-to-move with direct movement. The character ignores obstacles but the full input pipeline (click > raycast > path follow > ECS movement) still exercises.

---

## Related Documentation

| Document | Description |
|----------|-------------|
| `Docs/EPIC15/SETUP_GUIDE_15.20.md` | Full Input Paradigm Framework setup (profiles, state machine, subsystems) |
| `Docs/EPIC18/EPIC18.15.md` | Technical specification and root cause analysis |
