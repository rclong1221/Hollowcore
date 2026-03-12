# EPIC 2.7: EVA Hazards & Survival

**Priority**: LOW  
**Goal**: Temperature, suit integrity, environmental dangers
**Dependencies**: Epic 2.1 (EVAState, Oxygen), Epic 2.3 (Welder for repair)

## Components

**BodyTemperature** (IComponentData, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `Current` | float | Quantization=10 | Current core temp in °C (normal: 37) |
| `TargetTemp` | float | No | Environment target temp |
| `ChangeRatePerSecond` | float | No | How fast temp changes toward target |
| `MinSafe` | float | No | Minimum safe temp (default: 30°C) |
| `MaxSafe` | float | No | Maximum safe temp (default: 45°C) |

**TemperatureEffects** (IComponentData, Client-only)
| Field | Type | Description |
|-------|------|-------------|
| `IsCold` | bool | Below safe range - trigger shivering VFX |
| `IsHot` | bool | Above safe range - trigger heat distortion |
| `Severity` | float | 0-1 based on how far outside range |

**SuitIntegrity** (IComponentData, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `Current` | float | Quantization=100 | Current integrity (0-100) |
| `Max` | float | No | Maximum integrity (default: 100) |
| `CrackLevel` | int | Yes | Visual crack level (0=none, 1=minor, 2=major, 3=critical) |

**SuitOxygenLeak** (IComponentData, derived from SuitIntegrity)
- Calculated: `LeakMultiplier = 1 + (1 - Current/Max) * 2` (up to 3x leak at 0 integrity)

**EnvironmentZone** (IComponentData, on zone trigger entities)
| Field | Type | Description |
|-------|------|-------------|
| `ZoneType` | enum | Normal, Hot, Cold, Radioactive, Vacuum |
| `Temperature` | float | Zone temperature in °C |
| `RadiationLevel` | float | Radiation accumulation rate |
| `OxygenAvailable` | bool | True if breathable (inside ship) |

**InEnvironmentZone** (IComponentData, on Player)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `ZoneEntity` | Entity | Yes | Current zone (Entity.Null if no zone) |
| `ZoneType` | enum | Yes | Cached zone type |
| `ZoneTemperature` | float | Yes | Cached zone temperature |

## Systems

**EnvironmentZoneDetectionSystem** (SimulationSystemGroup, Predicted)
```
Uses: Trigger events or spatial query
Burst: Partial
```
- Query: Players, `EnvironmentZone` entities
- Responsibility:
  - Detect which zone player is in (trigger overlap or spatial hash)
  - Update `InEnvironmentZone` component

**TemperatureSystem** (SimulationSystemGroup, ServerWorld)
```
UpdateAfter: EnvironmentZoneDetectionSystem
Burst: Yes
```
- Query: `BodyTemperature`, `InEnvironmentZone`, `Health`
- Responsibility:
  - Set `TargetTemp` from zone (or 20°C if no zone / inside ship)
  - Lerp `Current` toward `TargetTemp`
  - Apply damage when outside safe range (damage scales with severity)

**TemperatureEffectsSystem** (PresentationSystemGroup, ClientWorld)
```
Burst: No (triggers VFX)
```
- Query: `BodyTemperature`, `TemperatureEffects`, `LocalPlayerTag`
- Responsibility:
  - Calculate severity: `(Current - MaxSafe) / 10` or `(MinSafe - Current) / 10`
  - Trigger shivering/heat VFX based on severity

**SuitDamageSystem** (SimulationSystemGroup, ServerWorld)
```
UpdateAfter: HealthDamageSystem
Burst: Yes
```
- Query: `SuitIntegrity`, `DamageEvent` buffer
- Responsibility:
  - Reduce suit integrity when player takes damage
  - Not all damage affects suit (configure per damage type)

**SuitLeakSystem** (SimulationSystemGroup, ServerWorld)
```
UpdateBefore: OxygenDepletionSystem
Burst: Yes
```
- Query: `SuitIntegrity`, `OxygenTank`
- Responsibility:
  - Calculate `LeakMultiplier` from integrity
  - Apply to `OxygenTank.LeakMultiplier`

**SuitCrackVisualSystem** (PresentationSystemGroup, ClientWorld)
```
Burst: No (UI overlay)
```
- Query: `SuitIntegrity`, `LocalPlayerTag`
- Responsibility:
  - Update crack overlay based on `CrackLevel`
  - Thresholds: 75% = level 1, 50% = level 2, 25% = level 3

**SuitRepairSystem** (SimulationSystemGroup, ServerWorld)
```
Burst: Yes
```
- Query: `SuitIntegrity`, `ActiveTool`, `ToolUsageState`, `PlayerVelocity`
- Responsibility:
  - If active tool is Welder and targeting self (no target entity)
  - If player is stationary (velocity near zero)
  - Repair suit integrity over time
  - Consume welder durability

## Zone Detection Strategy
Option A: **Trigger Colliders** (simpler)
- Zone entities have trigger colliders
- Use Unity Physics trigger events

Option B: **Spatial Hash** (more performant for many zones)
- Use existing `SpatialHashGrid` component
- Query zones by player position

Recommendation: Start with trigger colliders, optimize later if needed.

## Acceptance Criteria
- [x] Temperature changes based on environment zone
- [x] Damage applies when outside safe temperature range
- [x] Visual effects show hot/cold status
- [x] Suit integrity reduces when taking damage
- [x] Suit damage increases oxygen leak rate
- [x] Crack visuals appear at integrity thresholds
- [x] Welder can repair suit when stationary
- [x] All systems integrate smoothly with Epic 2.1 oxygen

## Implementation Status

**Completed**: All components and systems implemented.

### Files Created

**Components** (`Assets/Scripts/Runtime/Survival/Hazards/Components/`):
- `HazardComponents.cs` - ZoneType, BodyTemperature, TemperatureEffects, SuitIntegrity, SuitCrackVisualState, EnvironmentZone, InEnvironmentZone, TemperatureDamageEvent, SuitRepairRequest, TemperatureSusceptible, HasSuit

**Systems** (`Assets/Scripts/Runtime/Survival/Hazards/Systems/`):
- `EnvironmentZoneSystem.cs` - EnvironmentZoneDetectionSystem (physics overlap for zone detection)
- `TemperatureSystem.cs` - TemperatureSystem, TemperatureDamageSystem, TemperatureDamageEventCleanupSystem
- `SuitSystems.cs` - SuitDamageEvent, SuitDamageSystem, SuitLeakSystem, SuitRepairSystem
- `HazardPresentationSystems.cs` - TemperatureEffectsSystem, SuitCrackVisualSystem, TemperatureWarningSystem, SuitDamageWarningSystem

**Authoring** (`Assets/Scripts/Runtime/Survival/Hazards/Authoring/`):
- `HazardAuthoring.cs` - EnvironmentZoneAuthoring, PlayerHazardAuthoring, EnvironmentZonePresets

### Architecture Notes

- Uses event entity pattern (TemperatureDamageEvent, SuitDamageEvent) for decoupling from Player assembly
- Zone detection uses physics point overlap queries
- Leak multiplier stored in OxygenTank.LeakMultiplier (integrates with Epic 2.1)
- Presentation systems are client-only for VFX triggers
- Warning events created on state transitions for audio feedback
