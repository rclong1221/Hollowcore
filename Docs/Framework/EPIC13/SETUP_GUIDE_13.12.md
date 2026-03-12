# Setup Guide: EPIC 13.12 (Flashlight Bandwidth Optimization)

## Overview

This epic optimizes network bandwidth for the flashlight system by splitting `FlashlightData` into two components:
- **`FlashlightState`** - Minimal visual state, replicated to ALL clients (2 bits)
- **`FlashlightConfig`** - Full configuration, replicated only to predicted clients (~200 bits)

This reduces bandwidth usage by ~100x for flashlight data per remote player.

## No Manual Setup Required

This is a code-only optimization. No prefab changes or manual setup needed - the authoring component bakes both components automatically.

## Verification

### Single Player
1. Enter Play Mode
2. Press **F** to toggle flashlight
3. **Confirm:** Light turns on/off
4. Wait for battery to drain below 5%
5. **Confirm:** Light flickers
6. Turn off flashlight
7. **Confirm:** Battery recharges (if enabled)

### Multiplayer
1. Start a **Host**
2. Connect a **Client**
3. Toggle flashlight on Host
4. **Confirm (Client view):** Remote player's flashlight visible
5. Let Host's battery drain to <5%
6. **Confirm (Client view):** Remote player's flashlight flickers
7. Repeat for Client toggling, Host viewing

### Network Verification (Optional)
Use Unity's Network Profiler to verify bandwidth:
- Before: ~24 bytes per player per snapshot for FlashlightData
- After: ~1 bit per player per snapshot for FlashlightState

## Component Architecture

| Component | PrefabType | Recipients | Size |
|-----------|------------|------------|------|
| `FlashlightState` | All | Everyone | 2 bits |
| `FlashlightConfig` | AllPredicted | Owner only | ~200 bits |

### FlashlightState Fields
| Field | Type | Purpose |
|-------|------|---------|
| `IsOn` | bool | Light on/off state |
| `IsFlickering` | bool | Low battery flicker effect |

### FlashlightConfig Fields
| Field | Type | Purpose |
|-------|------|---------|
| `BatteryCurrent` | float | Current battery level |
| `BatteryMax` | float | Maximum battery capacity |
| `Intensity` | float | Light intensity |
| `Range` | float | Light range |
| `DrainRate` | float | Battery drain per second |
| `RechargeRate` | float | Battery recharge per second |
| `RechargeEnabled` | bool | Whether recharge is enabled |
| `LastInputFrame` | uint | Input tracking (not replicated) |

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Remote flashlight not visible | Verify `FlashlightState` has `[GhostComponent(PrefabType = GhostPrefabType.All)]` |
| Remote flicker not working | Verify `IsFlickering` is updated by `FlashlightLogicSystem` |
| Battery not syncing to owner | Verify `FlashlightConfig` has `[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]` |
| HUD shows wrong battery | Update `FlashlightHUD` to read from `FlashlightConfig` |
| Compilation errors | Ensure all dependent systems updated to query new components |
