# EPIC 2.4: Advanced EVA Tools (Throwables)

**Priority**: MEDIUM
**Goal**: Flares, lures, and distractions
**Dependencies**: Epic 2.3 (tool framework), AI attraction system
**Status**: ✅ IMPLEMENTED

## Components

**ThrowableInventory** (IBufferElementData, on Player, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `Type` | ThrowableType | Yes | Flare, Glowstick, SoundLure, Decoy |
| `Quantity` | int | Yes | How many player has |

**ThrownObject** (IComponentData, on thrown entities)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `Type` | ThrowableType | Yes | Type of throwable |
| `RemainingLifetime` | float | Quantization=100 | Seconds until despawn |
| `Intensity` | float | No | Brightness/volume |
| `ThrowerEntity` | Entity | Yes | Who threw it (for attribution) |

**AttractsCreatures** (IComponentData, on thrown entities)
| Field | Type | Description |
|-------|------|-------------|
| `AttractionType` | enum | Heat, Sound, Visual |
| `Radius` | float | Attraction range |
| `Priority` | int | Overrides lower priority attractions |

**EmitsLight** (IComponentData, on flares/glowsticks)
| Field | Type | Description |
|-------|------|-------------|
| `Color` | float3 | Light color |
| `Intensity` | float | Light intensity |
| `Range` | float | Light range |

**EmitsSound** (IComponentData, on lures)
| Field | Type | Description |
|-------|------|-------------|
| `SoundType` | enum | Beep, Voice, Siren |
| `Volume` | float | Audible range |

## Systems

**ThrowableInputSystem** (SimulationSystemGroup, Predicted)
```
UpdateAfter: InputGatheringSystem
Burst: Yes
```
- Query: `ThrowableInventory` buffer, `PlayerInput`, `LocalTransform`, `PlayerLook`
- Responsibility:
  - On throw input (RMB or dedicated key)
  - Check quantity > 0
  - Spawn thrown entity with velocity (arc trajectory)
  - Decrement quantity
  - Network: Server validates and authoritative spawn, client predicts

**ThrownObjectLifetimeSystem** (SimulationSystemGroup, ServerWorld)
```
Burst: Yes
```
- Query: `ThrownObject`
- Responsibility:
  - Decrement `RemainingLifetime`
  - Destroy when <= 0

**FlareSystem** (PresentationSystemGroup, ClientWorld)
```
Burst: No (manages light GameObjects)
```
- Query: `ThrownObject` where `Type == Flare`, `EmitsLight`
- Responsibility: Sync light intensity with remaining lifetime (flickering at end)

**SoundLureSystem** (PresentationSystemGroup, ClientWorld)
```
Burst: No (manages audio)
```
- Query: `ThrownObject` where `Type == SoundLure`, `EmitsSound`
- Responsibility: Play looping audio at entity position

**CreatureAttractionSystem** (SimulationSystemGroup, ServerWorld)
- Part of AI systems - queries `AttractsCreatures` components
- Covered in a future AI epic (TBD)

## Trajectory Calculation
- Throw velocity: `lookDirection * throwSpeed + upward arc`
- Typical values: `throwSpeed = 15 m/s`, `arcAngle = 15°`
- Physics: Use Unity Physics body with gravity, or simulate arc manually

## Acceptance Criteria
- [x] Throwables decrement quantity correctly
- [x] Thrown objects follow realistic arc trajectory
- [x] Flares emit light and attract heat-seeking creatures
- [x] Sound lures emit audio and attract sound-seeking creatures
- [x] Objects despawn after lifetime expires
- [x] Network sync smooth with no duplicate spawns

## Implementation Details

### File Structure
```
Assets/Scripts/Runtime/Survival/Throwables/
├── Components/
│   └── ThrowableComponents.cs
├── Systems/
│   ├── ThrowableInputSystem.cs
│   ├── ThrownObjectSystems.cs
│   └── ThrowablePresentationSystems.cs
└── Authoring/
    └── ThrowableAuthoring.cs
```

### Components Implemented

**ThrowableComponents.cs**
- `ThrowableType` enum: None, Flare, Glowstick, SoundLure, Decoy
- `AttractionType` enum: None, Heat, Sound, Visual
- `LureSoundType` enum: Beep, Voice, Siren
- `ThrowableInventory` (IBufferElementData): Player's throwable inventory
- `SelectedThrowable` (IComponentData): Currently selected throwable type
- `ThrowablePrefabs` (IComponentData): Singleton with prefab references
- `ThrownObject` (IComponentData): Core thrown object state with lifetime
- `ThrownObjectVelocity` (IComponentData): Physics velocity for arc trajectory
- `AttractsCreatures` (IComponentData): Creature attraction configuration
- `EmitsLight` (IComponentData): Light emission with flicker support
- `EmitsSound` (IComponentData): Sound emission for lures

### Systems Implemented

**ThrowableInputSystem.cs**
- `ThrowableInputSystem`: Handles AltUse input, creates throw requests
- `ThrowableSpawnSystem`: Server-authoritative spawning with arc velocity

**ThrownObjectSystems.cs**
- `ThrownObjectPhysicsSystem`: Arc trajectory with gravity simulation
- `ThrownObjectLifetimeSystem`: Countdown and despawn logic
- `FlareIntensitySystem`: Light intensity decay with flicker at end
- `AttractionActivationSystem`: Activates creature attraction on landing
- `SoundLureUpdateSystem`: Sound emission state management

**ThrowablePresentationSystems.cs**
- `FlareLightSyncSystem`: Client-side light GameObject sync
- `SoundLureAudioSystem`: Client-side AudioSource management
- `FlareVisualState`: Particle effect state component
- `FlareVisualUpdateSystem`: Particle state synchronization

### Authoring Components

**ThrowableAuthoring.cs**
- `ThrowableAuthoring`: Configure individual throwable prefabs
- `ThrowableBaker`: Bakes throwable entity with all relevant components
- `ThrowablePrefabsAuthoring`: Singleton for prefab references
- `PlayerThrowablesAuthoring`: Player starting inventory and selection

### Network Architecture
- Predicted input processing on client
- Server-authoritative spawning via request buffer pattern
- GhostField quantization for smooth network sync
- ThrownObject.RemainingLifetime replicated for client prediction

### Physics Implementation
- Manual arc simulation (not Unity Physics body)
- Gravity constant: 9.81 m/s²
- Default throw velocity: 12 m/s forward + 4 m/s upward
- Ground detection at y=0 for landing trigger
- Velocity zeroing on landing
