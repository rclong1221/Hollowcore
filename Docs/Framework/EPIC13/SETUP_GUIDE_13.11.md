# Setup Guide: Epic 13.11 (Multiplayer Flashlight)

## Overview
This epic implements a multiplayer flashlight system where each player's flashlight is visible to all other players. The flashlight is attached to the player model (not the camera), so it always points in the direction the player is facing.

## Step 1: Add Flashlight Mount to Player Model
Add a mount point to the player presentation prefab where the flashlight will be attached.

1. Open the **Player Presentation Prefab** (the visual model used by `GhostPresentationGameObjectSystem`).
2. Create an empty child GameObject named **`FlashlightMount`**.
3. Position it at the player's **head or helmet** area.
4. Rotate it to face **forward** (the direction the player looks).

> [!NOTE]
> If `FlashlightMount` is not found, the system falls back to searching for a "Head" bone, then the root transform.

## Step 2: Verification

### Single Player
1. Enter Play Mode.
2. Press **F** to toggle flashlight.
3. **Confirm:** Light appears from player's head, pointing forward.
4. Rotate the player - **Confirm:** Light rotates with player, not camera.
5. **Confirm:** Light flickers when battery is low (<5%).
6. **Confirm:** Light turns off when battery depletes.
7. Turn off flashlight - **Confirm:** Battery recharges slowly (if `EnableRecharge` is checked).

### Multiplayer
1. Start a **Host**.
2. Connect a **Client**.
3. Toggle flashlight on Host.
4. **Confirm (Client view):** You can see the Host's flashlight on their character.
5. Toggle flashlight on Client.
6. **Confirm (Host view):** You can see the Client's flashlight.
7. Walk far apart (>50m).
8. **Confirm:** Distant flashlights are culled for performance.

## Performance Notes

| Feature | Local Player | Remote Players |
|---------|--------------|----------------|
| Shadows | None | None |
| Intensity | 1000 | 500 |
| Range | 100m | 50m |
| Spot Angle | 10°/20° | 10°/20° |
| Distance Cull | Never | Beyond 50m |

## Configuration (VisorAuthoring)

| Field | Default | Description |
|-------|---------|-------------|
| Max Battery Seconds | 3600 | Total battery capacity (1 hour) |
| Drain Rate | 1.0 | Seconds drained per second while on |
| Recharge Rate | 0.5 | Seconds recharged per second while off |
| Enable Recharge | ✓ | Debug toggle - enables battery recharge |

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Light in wrong position | Add `FlashlightMount` child to player prefab at head |
| Light not appearing | Verify player has `FlashlightData` component |
| Flickering on/off rapidly | Check input system - F key should use `wasPressedThisFrame` |
| Light doesn't rotate with player | Ensure mount is child of animated skeleton, not root |
| Battery not recharging | Check `Enable Recharge` is checked in VisorAuthoring |
| Constant light always on | Disable/remove `Main Camera > Flashlight` if present |
| Duplicate FlashlightData baking error | Remove duplicate `VisorAuthoring` component from prefab (search `t:VisorAuthoring` in hierarchy) |
