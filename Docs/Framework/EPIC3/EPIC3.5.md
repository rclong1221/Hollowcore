# EPIC 3.5: Ship Power, Life Support, and Interior "Safe State"

**Priority**: MEDIUM  
**Goal**: Make ship interior survivable because systems are running (power + life support), and meaningfully dangerous when they fail.  
**Dependencies**: Epic 3.1 (pressurization), Survival `EnvironmentZone`, Epic 4 (damage/status effects)
**Status**: ✅ IMPLEMENTED

## Design Notes (Match EPIC7 Level of Detail)
- **Interior safety is system-driven**: "pressurized and safe" is the *output* of life support + hull integrity, not a static assumption.
- **Power allocation is deterministic**: priority-based allocation avoids "random" behavior under load.
- **Failure modes must be readable**: brownout/offline should be visible (UI/audio) and have clear survival consequences (oxygen drain, status effects).
- **Integration point**: `CurrentEnvironmentZone` should reflect interior hazard outcome; hazards/damage systems react automatically.

## Design Intent
The ship interior should be "pressurized and safe" by default, but power failures, breaches, or sabotage can flip the interior into oxygen-draining/hazard states.

## Components

**ShipPowerProducer** (IComponentData)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `MaxOutput` | float | Quantization=100 | Power produced per second |
| `CurrentOutput` | float | Quantization=100 | Current output (may be throttled) |
| `IsOnline` | bool | Yes | Is producer currently active |
| `ShipEntity` | Entity | Yes | Parent ship reference |

**ShipPowerConsumer** (IComponentData)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `RequiredPower` | float | Quantization=100 | Power needed for full operation |
| `Priority` | int | Yes | Higher stays on longer (LifeSupport=100, Engines=80, etc.) |
| `CurrentPower` | float | Quantization=100 | Assigned this tick |
| `ShipEntity` | Entity | Yes | Parent ship reference |

**ShipPowerState** (IComponentData, on ship; replicated)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `TotalProduced` | float | Quantization=100 | Sum producers |
| `TotalDemand` | float | Quantization=100 | Sum consumers |
| `TotalConsumed` | float | Quantization=100 | Actual power allocated |
| `IsBrownout` | bool | Yes | Demand > supply |

**LifeSupport** (IComponentData, on ship)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `IsOnline` | bool | Yes | Online if powered and not damaged |
| `PowerRequired` | float | No | Power needed to operate |
| `OxygenGenerationRate` | float | No | Future: refill tanks or reduce drain |
| `InteriorZoneEntity` | Entity | Yes | Zone entity representing ship interior volume |
| `ShipEntity` | Entity | Yes | Parent ship reference |
| `IsDamaged` | bool | Yes | Set by damage system |

## Systems

**ShipPowerAllocationSystem** (SimulationSystemGroup, ServerWorld)
- Aggregates producers/consumers per ship
- Sorts consumers by priority (highest first)
- Allocates power until supply exhausted
- Updates `ShipPowerState` and each consumer's `CurrentPower`

**LifeSupportSystem** (SimulationSystemGroup, ServerWorld, after ShipPowerAllocationSystem)
- Checks if life support has sufficient power (≥50% required)
- If `LifeSupport.IsOnline == false`:
  - interior zone becomes Vacuum (oxygen required)
  - temperature drops
- If online:
  - interior zone is Pressurized (no oxygen drain)

**LifeSupportInitSystem** (SimulationSystemGroup, ServerWorld, before ShipPowerAllocationSystem)
- Ensures LifeSupport entities have ShipPowerConsumer component

**PowerDebugSystem** (SimulationSystemGroup, ClientWorld)
- Press **P** to log power status and life support state
- Press **O** to toggle first power producer on/off (for testing brownout)

## File Locations

```
Assets/Scripts/Runtime/Ship/Power/
├── Components/
│   └── PowerComponents.cs          # All power-related ECS components
├── Authoring/
│   ├── PowerAuthoring.cs           # PowerProducerAuthoring, PowerConsumerAuthoring
│   └── LifeSupportAuthoring.cs     # LifeSupportAuthoring
├── Systems/
│   ├── ShipPowerAllocationSystem.cs    # Priority-based power allocation
│   ├── LifeSupportSystem.cs            # Environment zone updates
│   └── PowerDebugSystem.cs             # Debug key bindings
└── UI/
    ├── PowerHUD.cs                     # HUD showing power/life support status
    └── PowerHUDBuilder.cs              # Runtime UI creation
```

## Integration Guide

### Step 1: Add Power to Existing Ships

For ships created via the editor tool (`GameObject > DIG - Test Objects > Ships > Complete Test Ship`), power and life support are automatically added.

For existing ship prefabs, add these components:

1. **Power Producer** (child of ship):
   - Create a new child GameObject
   - Add `PowerProducerAuthoring` component
   - Set `MaxOutput` (default: 100W)
   - Ensure `StartOnline` is true

2. **Life Support** (child of ship):
   - Create a new child GameObject
   - Add `LifeSupportAuthoring` component
   - Set `PowerRequired` (default: 50W - less than producer output!)
   - Optionally link `InteriorZone` to the ship's pressurized zone

3. **Interior Zone** (child of ship):
   - Create a new child GameObject
   - Add `BoxCollider` (trigger)
   - Add `EnvironmentZoneAuthoring` with `ZoneType = Pressurized`

### Step 2: Testing Power Failure

1. Enter ship and press **P** to view power status
2. Press **O** to toggle the reactor off
3. Observe life support going offline
4. Interior zone becomes vacuum (oxygen will drain)
5. Press **O** again to restore power
6. Life support comes back online, interior becomes safe

### Step 3: Adding Audio & FX (Future)

The `PowerHUD` component has UnityEvents ready for asset integration. To add alarms or effects:

1. Select the `PowerHUD` GameObject (created by `PowerHUDBuilder` at runtime, or make a prefab).
2. Add an **AudioSource** or **ParticleSystem**.
3. In the `PowerHUD` inspector, hook up the events:
   - `OnPowerLost` → Play alarm sound
   - `OnLifeSupportLost` → Play gasping/hissing sound
   - `OnPowerRestored` → Play power-up sound
4. No code changes required!

## Priority System

| System | Priority | Notes |
|--------|----------|-------|
| Life Support | 100 | Highest - keeps crew alive |
| Engine Core | 90 | Ship mobility |
| Engines | 80 | Thrust |
| Navigation | 70 | Course plotting |
| Communications | 60 | Comms |
| Weapons | 50 | Combat |
| Shields | 40 | Defense |
| Sensors | 30 | Detection |
| Lighting | 20 | Comfort |
| Luxury | 10 | Lowest |

When power is insufficient, lower priority systems are starved first.

## Acceptance Criteria
- [x] Interior zone's hazard state matches life support state (online → safe, offline → dangerous)
- [x] Brownout behavior is deterministic and explainable (priority-based)

## Sub-Epics / Tasks

### Sub-Epic 3.5.1: Power Graph + Allocation ✅
**Tasks**:
- [x] Aggregate producers/consumers per ship
- [x] Allocate `CurrentPower` by `Priority` (highest first)
- [x] Expose debug metrics:
  - [x] total produced vs demanded
  - [x] which consumers are starved

### Sub-Epic 3.5.2: Life Support Output → Environment Zones ✅
**Tasks**:
- [x] Define how life support affects interior:
  - [x] online → `EnvironmentZoneType.Pressurized` (no oxygen drain)
  - [x] offline → `EnvironmentZoneType.Vacuum` (oxygen required, slower drain)
- [x] Ensure zone transitions are stable (no flicker from multiple systems writing)

### Sub-Epic 3.5.3: Player Feedback (Client) ✅
**Tasks**:
- [x] HUD indicator: Life Support Online/Offline
- [x] HUD Event Hooks: `OnPowerLost`, `OnLifeSupportLost`, etc. exposed for audio/FX
- [ ] Audio: alarms (Pending assets - hook into HUD events)
- [ ] Screen FX: (Pending assets - hook into HUD events)

### Sub-Epic 3.5.4: QA Checklist
**Tasks**:
- [x] Toggle power supply; life support drops and interior becomes oxygen-required
- [x] Restore power; interior returns to safe state deterministically

## Known Limitations

1. **No Audio Feedback**: No alarms or audio cues for power failure yet.
2. **Instant Transitions**: Zone changes are instant (no gradual pressure loss).
3. **No Screen FX**: No post-processing effects when interior becomes unsafe.

## Future Improvements

1. **Power Grid UI**: Visual representation of power flow
2. **Damage Integration**: Damaged systems consume more power or work less efficiently
3. **Battery/Capacitor**: Store power for brownouts
4. **Power Routing**: Player-controllable power allocation
5. **Emergency Power**: Minimum life support when main power fails
