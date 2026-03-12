# EPIC 13.12: Flashlight Bandwidth Optimization

> **Status:** COMPLETED ✓
> **Priority:** MEDIUM (Performance optimization)
> **Dependencies:** EPIC 13.11 (Multiplayer Flashlight System)

## Problem Analysis

Current `FlashlightData` component replicates **all fields to all clients**:

```csharp
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct FlashlightData : IComponentData
{
    [GhostField] public bool IsOn;           // ✓ Remote needs
    [GhostField] public float BatteryCurrent; // ✗ Remote doesn't need
    [GhostField] public float BatteryMax;     // ✗ Remote doesn't need
    [GhostField] public float Intensity;      // ✗ Remote doesn't need
    [GhostField] public float Range;          // ✗ Remote doesn't need
    [GhostField] public float DrainRate;      // ✗ Remote doesn't need
    [GhostField] public float RechargeRate;   // ✗ Remote doesn't need
    [GhostField] public bool RechargeEnabled; // ✗ Remote doesn't need
}
```

### Bandwidth Impact

| Players | Extra Bytes/Tick | At 60Hz | Per Minute |
|---------|------------------|---------|------------|
| 10      | 240 bytes        | 14 KB/s | 864 KB     |
| 50      | 1.2 KB           | 72 KB/s | 4.3 MB     |
| 100     | 2.4 KB           | 144 KB/s | 8.6 MB    |

Remote clients only need `IsOn` (1 bit) for visual rendering, but currently receive ~200 bits per player.

## Solution

Split into two components:
1. **`FlashlightState`** (`GhostPrefabType.All`) - Minimal data for visuals (2 bits)
2. **`FlashlightConfig`** (`GhostPrefabType.AllPredicted`) - Full config for simulation

This achieves **~100x bandwidth reduction** for flashlight data to remote clients.

## Implementation Plan

### 13.12.1 Create FlashlightState Component
- [x] Create `FlashlightState` with only `IsOn` and `IsFlickering` fields
- [x] Mark as `[GhostComponent(PrefabType = GhostPrefabType.All)]`
- [x] Add `[GhostField]` to both fields

### 13.12.2 Create FlashlightConfig Component  
- [x] Create `FlashlightConfig` with battery/settings data
- [x] Mark as `[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]`
- [x] Move non-visual fields from `FlashlightData`

### 13.12.3 Update FlashlightLogicSystem
- [x] Query both `FlashlightState` and `FlashlightConfig`
- [x] Compute `IsFlickering` server-side (battery < 5%)
- [x] Update both components
- [x] Sync to legacy `FlashlightData` for backward compatibility

### 13.12.4 Update FlashlightPresentationSystem
- [x] Change query to use `FlashlightState` only
- [x] Use `IsFlickering` instead of computing from battery
- [x] Remove debug logging

### 13.12.5 Update Authoring
- [x] Update `VisorAuthoring` to bake both new components
- [x] Maintain `FlashlightData` for backward compatibility during migration

### 13.12.6 Update Dependent Systems
- [x] Update `FlashlightHUD` to use new components
- [x] Update `DarknessStressSystem` to use new components

### 13.12.7 Cleanup Legacy Code (Future)
- [ ] Remove `FlashlightData` after full migration verification
- [ ] Remove legacy sync loop from `FlashlightLogicSystem`
- [ ] Consolidate with `FlashlightTool` from Tools system if applicable

## Architecture

### Before (Current)
```
FlashlightData [All] (~200 bits per player)
    └── IsOn, BatteryCurrent, BatteryMax, Intensity, Range, DrainRate, RechargeRate, RechargeEnabled
```

### After (Optimized)
```
FlashlightState [All] (2 bits per player)    ← Remote clients receive only this
    └── IsOn, IsFlickering

FlashlightConfig [AllPredicted] (~200 bits)  ← Only owner/predicted clients receive
    └── BatteryCurrent, BatteryMax, Intensity, Range, DrainRate, RechargeRate, RechargeEnabled
```

## Files Created
- `Assets/Scripts/Visuals/Components/FlashlightComponents.cs` - New split components

## Files Modified
- `Assets/Scripts/Visuals/Systems/FlashlightSystem.cs` - Update queries
- `Assets/Scripts/Visuals/Authoring/VisorAuthoring.cs` - Bake new components
- `Assets/Scripts/Player/Systems/DarknessStressSystem.cs` - Use new components
- `Assets/Scripts/Visuals/UI/FlashlightHUD.cs` - Use new components

## Verification Checklist
- [ ] Local player flashlight toggle works
- [ ] Local player battery drain/recharge works
- [ ] Remote player flashlight visible (IsOn state replicates)
- [ ] Remote player flicker effect works (IsFlickering replicates)
- [ ] HUD displays correct battery level
- [ ] Darkness stress system reads flashlight state correctly
- [ ] No compilation errors
- [ ] NetCode serializer generates without errors
