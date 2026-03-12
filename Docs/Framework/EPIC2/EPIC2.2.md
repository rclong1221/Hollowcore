# EPIC 2.2: EVA Movement Mechanics

**Priority**: HIGH  
**Goal**: Different movement feel outside the ship (jetpack, magnetic boots)
**Dependencies**: Epic 1.5 (PlayerMovementSettings), Epic 2.1 (EVAState)

## Components

**EVAMovementModifier** (IComponentData, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `SpeedMultiplier` | float | No | Movement speed scalar in EVA (default: 0.6) |
| `JumpForceMultiplier` | float | No | Jump force scalar in EVA (default: 0.5) |
| `AirControlMultiplier` | float | No | Air control in EVA (default: 0.3) |
| `GravityOverride` | float | Quantization=100 | Custom gravity in EVA (-1 = use default) |

**JetpackState** (IComponentData, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `Fuel` | float | Quantization=100 | Current fuel (0-100) |
| `MaxFuel` | float | No | Maximum fuel capacity (default: 100) |
| `IsThrusting` | bool | Yes | True while thrust input held and fuel > 0 |
| `ThrustForce` | float | No | Vertical thrust acceleration (default: 8 m/s²) |
| `FuelConsumptionRate` | float | No | Fuel drain per second while thrusting (default: 10) |
| `FuelRegenRate` | float | No | Fuel regen per second when not thrusting (default: 2) |
| `RegenDelay` | float | No | Seconds after thrust stops before regen starts (default: 1.0) |
| `TimeSinceThrust` | float | No | Tracks time since last thrust for regen delay |

**MagneticBootState** (IComponentData, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `IsEnabled` | bool | Yes | True if boots are toggled on |
| `IsAttached` | bool | Yes | True if currently attached to metal surface |
| `AttachedNormal` | float3 | Quantization=1000 | Surface normal when attached (for gravity override) |
| `AttachForce` | float | No | Downward force when attached (default: 20) |
| `DetectRange` | float | No | Raycast distance to detect metal (default: 2m) |
| `DetachVelocityThreshold` | float | No | Speed required to break attachment (default: 5 m/s) |

## Systems

**EVAMovementModifierSystem** (SimulationSystemGroup, Predicted)
```
UpdateBefore: PlayerMoveSystem
Burst: Yes, IJobEntity parallel
```
- Query: `EVAState`, `EVAMovementModifier`, `PlayerMovementSettings`
- Responsibility:
  - When `IsInEVA`: Multiply movement settings by EVA modifiers
  - Store original values to restore when exiting EVA

**JetpackSystem** (SimulationSystemGroup, Predicted)
```
UpdateAfter: InputGatheringSystem
UpdateBefore: PlayerMoveSystem
Burst: Yes, IJobEntity parallel
```
- Query: `EVAState`, `JetpackState`, `PlayerInput`, `PlayerVelocity`
- Responsibility:
  - Check `JumpHeld` input and `Fuel > 0`
  - Apply vertical thrust to velocity
  - Drain fuel while thrusting
  - Track `TimeSinceThrust` for regen delay

**JetpackRegenSystem** (SimulationSystemGroup, Predicted)
```
UpdateAfter: JetpackSystem
Burst: Yes, IJobEntity parallel
```
- Query: `JetpackState` where `!IsThrusting`
- Responsibility:
  - Increment `TimeSinceThrust`
  - Regen fuel after delay elapsed

**MagneticBootToggleSystem** (SimulationSystemGroup, Predicted)
```
UpdateAfter: InputGatheringSystem
Burst: Yes
```
- Query: `MagneticBootState`, `PlayerInput`
- Responsibility: Toggle `IsEnabled` on input (e.g., B key)

**MagneticBootAttachSystem** (SimulationSystemGroup, Predicted)
```
UpdateAfter: MagneticBootToggleSystem
UpdateBefore: PlayerMoveSystem
Burst: Yes, IJobEntity (sequential - raycasts)
```
- Query: `EVAState`, `MagneticBootState`, `LocalTransform`
- Responsibility:
  - Raycast down to detect metal surfaces (use layer mask or `MetalSurface` tag via SurfaceMaterialMapping)
  - Set `IsAttached` and `AttachedNormal`
  - Override gravity direction to `-AttachedNormal`

**MagneticBootDetachSystem** (SimulationSystemGroup, Predicted)
```
Burst: Yes
```
- Query: `MagneticBootState`, `PlayerVelocity` where `IsAttached`
- Responsibility: Detach if velocity exceeds threshold or boots disabled

## Integration
- Modifies existing `PlayerMovementSettings` temporarily (does not replace)
- Uses `SurfaceMaterialMapping` from Epic 1.13 to detect metal surfaces
- Jetpack VFX triggered via animation bridge or event system

## Acceptance Criteria
- [ ] EVA movement feels noticeably heavier/slower
- [x] Jetpack provides smooth vertical thrust with fuel management
- [x] Magnetic boots allow walking on ship hull (arbitrary gravity)
- [ ] Transitions between attached/detached feel smooth
- [x] All systems Burst-compiled and prediction-friendly
