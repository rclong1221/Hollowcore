# EPIC 2.1: EVA State & Oxygen System

**Priority**: HIGH  
**Goal**: Players can enter EVA mode with oxygen management
**Dependencies**: Epic 1.4 (PlayerState), Epic 1.5 (Movement), Health component

## Components

**EVAState** (IComponentData, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `IsInEVA` | bool | Yes | True when player is outside pressurized area |
| `TimeInEVA` | float | Quantization=100 | Seconds spent in current EVA session |
| `TetheredShip` | Entity | Yes | Optional reference to connected ship (Entity.Null if untethered) |
| `EnteredEVATime` | float | No | Server timestamp when EVA started (for sync) |

**OxygenTank** (IComponentData, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `Current` | float | Quantization=100 | Current oxygen level (0-100) |
| `Max` | float | No | Maximum capacity (default: 100) |
| `DepletionRatePerSecond` | float | No | Base drain rate in EVA (default: 1.0) |
| `LeakMultiplier` | float | Quantization=100 | Multiplier from suit damage (1.0 = no leak) |
| `WarningThreshold` | float | No | Percent to trigger warning (default: 25) |
| `CriticalThreshold` | float | No | Percent to trigger critical (default: 10) |

**OxygenWarningState** (IComponentData, Client-only)
| Field | Type | Description |
|-------|------|-------------|
| `WarningTriggered` | bool | True if warning audio/UI has been shown |
| `CriticalTriggered` | bool | True if critical audio/UI has been shown |

**RadiationExposure** (IComponentData, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `Current` | float | Quantization=100 | Current radiation level (0-100+) |
| `DamageThreshold` | float | No | Level at which damage starts (default: 100) |
| `DecayRatePerSecond` | float | No | Natural decay when not exposed (default: 0.1) |
| `AccumulationRate` | float | No | Rate of gain in radioactive zones (set by zone) |

## Systems

**OxygenDepletionSystem** (SimulationSystemGroup, ServerWorld)
```
UpdateAfter: PlayerStateSystem
UpdateBefore: HealthDamageSystem
Burst: Yes, IJobEntity parallel
```
- Query: `EVAState`, `OxygenTank`, `Health` where `EVAState.IsInEVA == true`
- Responsibility:
  - Decrement `OxygenTank.Current` by `DepletionRatePerSecond * LeakMultiplier * deltaTime`
  - Clamp to 0
  - When `Current == 0`: Apply 10 HP/sec damage via `HealthDamageRequest` buffer

**OxygenWarningSystem** (PresentationSystemGroup, ClientWorld)
```
UpdateAfter: OxygenDepletionSystem (via ghost sync)
Burst: No (triggers audio/UI)
```
- Query: `OxygenTank`, `OxygenWarningState`, `LocalPlayerTag`
- Responsibility:
  - Check thresholds, trigger one-shot audio/UI warnings
  - Reset warning states when oxygen refilled above threshold

**RadiationAccumulationSystem** (SimulationSystemGroup, ServerWorld)
```
UpdateAfter: EnvironmentZoneDetectionSystem
Burst: Yes, IJobEntity parallel
```
- Query: `RadiationExposure`, `InRadiationZone` (tag or zone ref)
- Responsibility:
  - Accumulate radiation when in zone
  - Apply damage when above threshold via `HealthDamageRequest`

**RadiationDecaySystem** (SimulationSystemGroup, ServerWorld)
```
Burst: Yes, IJobEntity parallel
```
- Query: `RadiationExposure` where NOT `InRadiationZone`
- Responsibility: Decay `Current` toward 0

## Integration
- Uses existing `Health` component for damage
- EVA state transitions triggered by airlock systems (Epic 3.1)
- Oxygen refill via ship connection or O2 stations

## Acceptance Criteria
- [ ] Oxygen drains predictably in EVA
- [ ] Audio/UI warnings fire at correct thresholds (once per threshold crossing)
- [ ] Damage applies smoothly when oxygen depleted
- [ ] Radiation accumulates in zones, decays outside
- [ ] Network prediction feels smooth (no oxygen jitter)
