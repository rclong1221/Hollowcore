# EPIC 13.11: Multiplayer Flashlight System

> **Goal:** Implement a performant multiplayer flashlight system where each player's flashlight state is visible to all other players.
> **Priority:** MEDIUM (Gameplay polish / immersion)

## Problem Analysis
Current flashlight system only works for the local player:
1. `FlashlightSystem` queries `GhostOwnerIsLocal`, meaning remote player flashlights are not rendered
2. Light sources are acquired from `Camera.main`, which only applies to the local player
3. No mechanism exists to spawn/manage lights on remote player models

## Performance Considerations
Dynamic lights are expensive. Best practices for multiplayer flashlights:
1. **Use baked/fake lights for distant players** - Beyond a threshold, don't render actual lights
2. **Pool light components** - Avoid instantiation/destruction overhead
3. **Limit shadow-casting** - Only local player flashlight casts shadows
4. **Use Light Layers** - Prevent lights from affecting unnecessary geometry
5. **LOD for light quality** - Reduce range/intensity for distant players

## Implementation Plan

### 13.11.1 Component Updates
- [x] Ensure `FlashlightData.IsOn` is already a `[GhostField]` (verified - it is)
- [x] Remove the invalid `UpdateBefore(PresentationSystemGroup)` attribute

### 13.11.2 Refactor FlashlightSystem
- [x] Split into two systems:
  - `FlashlightLogicSystem` - Handles toggle/battery logic for local player (SimulationSystemGroup)
  - `FlashlightPresentationSystem` - Handles visual lights for ALL players (PresentationSystemGroup)
- [x] Local player: Continue using Camera.main flashlight (first-person view)
- [x] Remote players: Spawn/pool lights on their presentation GameObjects

### 13.11.3 Remote Player Light Management
- [x] Use `GhostPresentationGameObjectSystem` to get remote player GameObjects
- [x] Find or create a "FlashlightMount" child transform on player model (head/hand area)
- [x] Pool Light components to avoid GC/instantiation overhead
- [x] Configure remote lights: no shadows, reduced range, light layers

### 13.11.4 Performance Optimizations
- [x] Distance culling: Only spawn lights for players within 50 meters
- [x] Shadow settings: Only local player casts shadows
- [x] Light layers: Use appropriate culling mask (configurable)

### 13.11.5 Battery Recharge System
- [x] Add `RechargeRate` and `RechargeEnabled` fields to `FlashlightData`
- [x] Battery slowly recharges when flashlight is off (if enabled)
- [x] Configurable via `VisorAuthoring` component in Inspector
- [x] Debug toggle: `EnableRecharge` defaults to true for testing

## Architecture

```
FlashlightData (IComponentData, GhostField: IsOn, BatteryCurrent, BatteryMax)
    ↓
FlashlightLogicSystem (PredictedSimulationSystemGroup, ALL predicted players)
    - Toggle on input
    - Battery drain (when on)
    - Battery recharge (when off, if enabled)
    ↓
FlashlightPresentationSystem (PresentationSystemGroup, Client only)
    - Local player: Full intensity light (1000)
    - Remote players: Reduced intensity (500), distance culled
```

## Files Modified
- `Assets/Scripts/Visuals/Systems/FlashlightSystem.cs` - Refactored into two systems
- `Assets/Scripts/Visuals/Components/VisorComponents.cs` - Added RechargeRate, RechargeEnabled, marked settings as `[GhostField]`
- `Assets/Scripts/Visuals/Authoring/VisorAuthoring.cs` - Added DrainRate, RechargeRate, EnableRecharge fields

## Known Issues
- **Duplicate FlashlightData baking error**: If you see "Attempt to add duplicate component FlashlightData", there are multiple `VisorAuthoring` components on the Warrok_Server prefab. Open the prefab and search `t:VisorAuthoring` in the hierarchy to find and remove duplicates.
