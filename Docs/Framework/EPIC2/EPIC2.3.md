# EPIC 2.3: Basic EVA Tools

**Priority**: HIGH
**Status**: ✅ IMPLEMENTED (Core Systems)
**Goal**: Essential tools for survival and repair
**Dependencies**: Epic 2.1 (EVAState), Voxel system (for drill), Ship health (for welder)

## Architecture: Entity-per-Tool
Each tool is its own entity with tool-specific components. Player has a buffer of owned tools and a reference to the active tool.

## Components

**ToolOwnership** (IBufferElementData, on Player)
| Field | Type | Description |
|-------|------|-------------|
| `ToolEntity` | Entity | Reference to owned tool entity |
| `SlotIndex` | int | Hotbar slot (0-4) |

**ActiveTool** (IComponentData, on Player, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `ToolEntity` | Entity | Yes | Currently equipped tool (Entity.Null = empty hands) |
| `SlotIndex` | int | Yes | Current slot index |

**Tool** (IComponentData, tag on tool entities)
| Field | Type | Description |
|-------|------|-------------|
| `ToolType` | ToolTypeEnum | Welder, Drill, Sprayer, Flashlight, Geiger |
| `DisplayName` | FixedString32 | For UI |

**ToolDurability** (IComponentData, on tool entities, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `Current` | float | Quantization=100 | Current durability/ammo (0-100) |
| `Max` | float | No | Maximum capacity |
| `DegradeRatePerSecond` | float | No | Durability loss while in use |
| `IsDepleted` | bool | Yes | True when Current <= 0 |

**ToolUsageState** (IComponentData, on tool entities, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `IsInUse` | bool | Yes | True while use input held |
| `UseTimer` | float | No | Accumulated use time (for charged actions) |
| `TargetPoint` | float3 | Quantization=100 | Raycast hit point |
| `TargetEntity` | Entity | Yes | Entity being affected (if any) |

**Tool-Specific Components** (on tool entities)

| Component | Fields | Description |
|-----------|--------|-------------|
| `WelderTool` | `HealPerSecond`, `DamagePerSecond`, `Range` | Repairs ship, damages creatures |
| `DrillTool` | `VoxelDamagePerSecond`, `Range`, `ResourceMultiplier` | Mines voxels |
| `SprayerTool` | `FoamPrefab`, `AmmoPerShot`, `Range` | Sprays blocking foam |
| `FlashlightTool` | `LightEntity`, `IsOn`, `BatteryDrainPerSecond` | Toggleable light |
| `GeigerTool` | `ScanRadius`, `UpdateInterval` | Displays radiation |

## Systems

**ToolSwitchingSystem** (SimulationSystemGroup, Predicted)
```
UpdateAfter: InputGatheringSystem
Burst: Yes
```
- Query: `ActiveTool`, `ToolOwnership` buffer, `PlayerInput`
- Responsibility:
  - Read scroll wheel or number key input
  - Update `ActiveTool.ToolEntity` and `SlotIndex`
  - Reset previous tool's `ToolUsageState.IsInUse`

**ToolRaycastSystem** (SimulationSystemGroup, Predicted)
```
UpdateAfter: ToolSwitchingSystem
UpdateBefore: Tool usage systems
Burst: Yes, uses CollisionWorld
```
- Query: `ActiveTool`, `LocalTransform`, `PlayerLook`
- Responsibility:
  - Perform raycast from player eye position in look direction
  - Store hit info in `ToolUsageState` of active tool

**WelderUsageSystem** (SimulationSystemGroup, ServerWorld)
```
UpdateAfter: ToolRaycastSystem
Burst: Yes
```
- Query: `WelderTool`, `ToolUsageState`, `ToolDurability` where `IsInUse && !IsDepleted`
- Responsibility:
  - If target has `ShipHullHealth`: Heal it
  - If target has `CreatureHealth`: Damage it
  - Degrade durability

**DrillUsageSystem** (SimulationSystemGroup, ServerWorld)
```
UpdateAfter: ToolRaycastSystem
Burst: Yes (voxel modifications via command buffer)
```
- Query: `DrillTool`, `ToolUsageState`, `ToolDurability` where `IsInUse && !IsDepleted`
- Responsibility:
  - Send `VoxelDamageRequest` to voxel system
  - Collect resources via `ResourceCollectionRequest`
  - Degrade durability

**SprayerUsageSystem** (SimulationSystemGroup, ServerWorld)
```
Burst: Yes (entity spawning via ECB)
```
- Query: `SprayerTool`, `ToolUsageState`, `ToolDurability` where use triggered
- Responsibility:
  - Spawn foam entity at target point
  - Decrement ammo

**FlashlightToggleSystem** (SimulationSystemGroup, Predicted)
```
Burst: Yes
```
- Query: `FlashlightTool`, `ToolUsageState`, `ToolDurability` where use triggered
- Responsibility:
  - Toggle `IsOn` state
  - Light entity enabled/disabled (presentation layer handles actual light)

**FlashlightDrainSystem** (SimulationSystemGroup, ServerWorld)
```
Burst: Yes
```
- Query: `FlashlightTool`, `ToolDurability` where `IsOn`
- Responsibility: Drain battery over time

**GeigerDisplaySystem** (PresentationSystemGroup, ClientWorld)
```
Burst: No (UI updates)
```
- Query: `GeigerTool`, `ToolUsageState`, `ActiveTool` on local player
- Responsibility: Display radiation level in HUD when Geiger is active tool

**ToolDurabilityReplenishSystem** (SimulationSystemGroup, ServerWorld)
- Handles tool recharging at stations/ship

## Integration
- Raycast uses Unity Physics `CollisionWorld`
- Voxel damage integrates with voxel system via request buffer
- Resource collection integrates with Epic 2.6 inventory
- Ship healing integrates with Epic 3 ship health

## Acceptance Criteria
- [x] Tools switch smoothly with no prediction desync
- [x] Welder heals ship, damages creatures (WeldRepairable integration ready, creature damage TODO)
- [ ] Drill removes voxels and collects resources (awaiting voxel system integration)
- [x] Sprayer creates blocking foam entities
- [x] Flashlight toggles with battery drain
- [x] Geiger shows radiation in HUD
- [x] Durability depletes and prevents use at 0

## Implementation Files

### Components
- `Runtime/Survival/Tools/Core/Components/ToolComponents.cs` - Core components (Tool, ToolDurability, ToolUsageState, ActiveTool, ToolOwnership, ToolOwner)
- `Runtime/Survival/Tools/Drill/Components/DrillTool.cs` - Drill-specific component
- `Runtime/Survival/Tools/Welder/Components/WelderTool.cs` - Welder-specific component + WeldRepairable
- `Runtime/Survival/Tools/Sprayer/Components/SprayerTool.cs` - Sprayer-specific component + FoamEntity
- `Runtime/Survival/Tools/Flashlight/Components/FlashlightTool.cs` - Flashlight-specific component + ToggleableLight
- `Runtime/Survival/Tools/Geiger/Components/GeigerTool.cs` - Geiger-specific component + GeigerDisplayState

### Systems
- `Runtime/Survival/Tools/Core/Systems/ToolSwitchingSystem.cs` - Handles tool slot switching via scroll wheel
- `Runtime/Survival/Tools/Core/Systems/ToolRaycastSystem.cs` - Performs raycasts and updates usage state
- `Runtime/Survival/Tools/Core/Systems/ToolDurabilityReplenishSystem.cs` - Recharges tools in pressurized zones
- `Runtime/Survival/Tools/Core/Systems/ToolSpawnSystem.cs` - Spawns tool entities for players
- `Runtime/Survival/Tools/Drill/Systems/DrillUsageSystem.cs` - Drill usage logic
- `Runtime/Survival/Tools/Welder/Systems/WelderUsageSystem.cs` - Welder usage logic
- `Runtime/Survival/Tools/Sprayer/Systems/SprayerUsageSystem.cs` - Sprayer usage + FoamDecaySystem
- `Runtime/Survival/Tools/Flashlight/Systems/FlashlightSystems.cs` - Toggle, drain, and light sync systems
- `Runtime/Survival/Tools/Geiger/Systems/GeigerSystems.cs` - Geiger update and display systems

### Authoring
- `Runtime/Survival/Tools/Authoring/ToolAuthoring.cs` - Baker for tool prefabs
- `Runtime/Survival/Tools/Authoring/PlayerToolsAuthoring.cs` - Baker for player tool capability

## Remaining Integration Points

1. **Voxel System Integration** (Drill)
   - DrillUsageSystem has placeholder for VoxelDamageRequest
   - Needs integration when voxel system is implemented

2. **Creature Damage** (Welder)
   - WelderUsageSystem has TODO for creature health damage
   - Needs integration when creature system is implemented

3. **Ship Health** (Welder)
   - WeldRepairable component is ready
   - Ship entities need WeldRepairable component added

4. **Resource Collection** (Drill)
   - ResourceCollectionRequest struct defined
   - Needs Epic 2.6 inventory integration
