# EPIC 2.5: Explosives & Charges

**Priority**: MEDIUM
**Goal**: Blow open passages, break resources
**Dependencies**: Epic 2.4 (throwable framework), Voxel system, Physics system
**Status**: ✅ IMPLEMENTED

## Components

**ExplosiveInventory** (IBufferElementData, on Player, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `Type` | ExplosiveType | Yes | MicroCharge, CuttingCharge, BreachingCharge |
| `Quantity` | int | Yes | Count |

**PlacedExplosive** (IComponentData, on placed explosive entities)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `Type` | ExplosiveType | Yes | Type determines blast params |
| `FuseTimeRemaining` | float | Quantization=100 | Seconds until detonation |
| `IsArmed` | bool | Yes | True when countdown started |
| `PlacerEntity` | Entity | Yes | Who placed it |

**ExplosiveStats** (IComponentData, on placed explosive entities)
| Field | Type | Description |
|-------|------|-------------|
| `BlastRadius` | float | Damage sphere radius |
| `BlastDamage` | float | Max damage at center |
| `VoxelDamageRadius` | float | Radius for voxel destruction |
| `PhysicsForce` | float | Impulse force on physics bodies |
| `FalloffExponent` | float | Damage falloff curve (1 = linear, 2 = quadratic) |

**ExplosiveTypeDefaults** (BlobAsset or ScriptableObject)
| Type | FuseTime | BlastRadius | BlastDamage | VoxelRadius | Force |
|------|----------|-------------|-------------|-------------|-------|
| MicroCharge | 3s | 2m | 50 | 1m | 500 |
| CuttingCharge | 5s | 4m | 100 | 3m | 1000 |
| BreachingCharge | 5s | 6m | 200 | 5m | 2000 |

## Systems

**ExplosivePlacementSystem** (SimulationSystemGroup, Predicted)
```
UpdateAfter: InputGatheringSystem
Burst: Yes (entity spawn via ECB)
```
- Query: `ExplosiveInventory`, `PlayerInput`, `ToolRaycastResult`
- Responsibility:
  - On place input when explosive selected
  - Raycast to find surface
  - Spawn explosive entity at hit point, attached to surface
  - Decrement inventory
  - Server authoritative for actual placement

**ExplosiveArmingSystem** (SimulationSystemGroup, ServerWorld)
```
Burst: Yes
```
- Query: `PlacedExplosive` where `!IsArmed`
- Responsibility:
  - Arm after short delay (0.5s) to prevent instant detonation
  - Start fuse countdown

**ExplosiveFuseSystem** (SimulationSystemGroup, ServerWorld)
```
Burst: Yes
```
- Query: `PlacedExplosive` where `IsArmed`
- Responsibility:
  - Decrement `FuseTimeRemaining`
  - When <= 0: Add `DetonationRequest` component

**ExplosiveDetonationSystem** (SimulationSystemGroup, ServerWorld)
```
UpdateAfter: ExplosiveFuseSystem
Burst: Partial (sphere overlap may need main thread)
```
- Query: `PlacedExplosive`, `ExplosiveStats`, `DetonationRequest`
- Responsibility:
  1. Sphere overlap query for entities with `Health`
  2. Apply damage with distance falloff
  3. Send `VoxelDamageRequest` to voxel system (sphere removal)
  4. Apply physics impulse to nearby rigidbodies
  5. Spawn explosion VFX (via presentation event)
  6. Destroy explosive entity

**ExplosionVFXSystem** (PresentationSystemGroup, ClientWorld)
- Listens for detonation events
- Spawns particle effects and plays audio

## Physics Integration
- Use `PhysicsWorld.OverlapSphere` or `CollisionWorld.OverlapAabb` + distance check
- Apply impulse via `PhysicsVelocity` modification or `PhysicsWorld.ApplyImpulse`

## Acceptance Criteria
- [x] Explosives place on surfaces correctly
- [x] Fuse countdown visible (audio beeps or visual)
- [x] Explosion damages entities with falloff
- [x] Explosion removes voxels in radius
- [x] Explosion applies physics force
- [x] VFX and audio feel impactful
- [x] Network authoritative (no client-side detonation)

## Implementation Details

### File Structure
```
Assets/Scripts/Runtime/Survival/Explosives/
├── Components/
│   └── ExplosiveComponents.cs
├── Systems/
│   ├── ExplosivePlacementSystem.cs
│   ├── ExplosiveFuseSystems.cs
│   ├── ExplosiveDetonationSystem.cs
│   └── ExplosivePresentationSystems.cs
└── Authoring/
    └── ExplosiveAuthoring.cs
```

### Components Implemented

**ExplosiveComponents.cs**
- `ExplosiveType` enum: None, MicroCharge, CuttingCharge, BreachingCharge
- `ExplosiveInventory` (IBufferElementData): Player's explosive storage
- `SelectedExplosive` (IComponentData): Currently selected explosive type
- `PlacedExplosive` (IComponentData): Core placed explosive state with fuse timer
- `ExplosiveStats` (IComponentData): Blast parameters (radius, damage, force, falloff)
- `DetonationRequest` (IComponentData): Tag triggering explosion processing
- `PlaceExplosiveRequest` (IBufferElementData): Server-authoritative placement requests
- `ExplosivePrefabs` (IComponentData): Singleton with prefab references
- `ExplosionEvent` (IComponentData): Event for VFX/audio triggering
- `ExplosiveVisualState` (IComponentData): Beep/light state for presentation
- `VoxelDamageRequest` (IComponentData): Request for voxel system (Epic 3)
- `LatestExplosionDisplay` (IComponentData): Singleton for hybrid presentation

### Systems Implemented

**ExplosivePlacementSystem.cs**
- `ExplosivePlacementInputSystem`: Raycast placement, creates requests
- `ExplosiveSpawnSystem`: Server-authoritative spawning, inventory management

**ExplosiveFuseSystems.cs**
- `ExplosiveArmingSystem`: 0.5s delay before arming
- `ExplosiveFuseSystem`: Countdown timer, adds DetonationRequest
- `ExplosiveBeepSystem`: Client-side beep interval calculation

**ExplosiveDetonationSystem.cs**
- `ExplosiveDetonationSystem`: Damage with falloff, physics impulse, VoxelDamageRequest
- `ExplosionEventCleanupSystem`: Removes old explosion events
- `VoxelDamageRequestCleanupSystem`: Removes processed voxel requests

**ExplosivePresentationSystems.cs**
- `ExplosionVFXSystem`: Processes explosion events for VFX
- `ExplosiveWarningDisplaySystem`: Syncs visual state to presentation
- `ExplosionDisplayUpdateSystem`: Updates singleton for hybrid layer

### Authoring Components

**ExplosiveAuthoring.cs**
- `ExplosiveAuthoring`: Configure explosive prefabs (fuse, blast, damage)
- `ExplosiveBaker`: Bakes explosive entity with all components
- `ExplosivePrefabsAuthoring`: Singleton for prefab references
- `PlayerExplosivesAuthoring`: Player starting inventory
- `ExplosionDisplayAuthoring`: Singleton for explosion display tracking

### Network Architecture
- Predicted input processing for placement
- Server-authoritative spawning via PlaceExplosiveRequest buffer
- Server-only detonation logic (WorldSystemFilterFlags.ServerSimulation)
- GhostField quantization for fuse timer sync
- ExplosionEvent replicated for client VFX

### Physics Implementation
- Sphere overlap using `PhysicsWorld.CalculateDistance`
- Damage falloff: `damage * (1 - pow(distance/radius, exponent))`
- Physics impulse applied to PhysicsVelocity components
- Direction calculated from explosion center to entity

### Explosive Type Defaults
| Type | FuseTime | BlastRadius | Damage | VoxelRadius | Force |
|------|----------|-------------|--------|-------------|-------|
| MicroCharge | 3s | 2m | 50 | 1m | 500 |
| CuttingCharge | 5s | 4m | 100 | 3m | 1000 |
| BreachingCharge | 5s | 6m | 200 | 5m | 2000 |

### Voxel System Integration
- `VoxelDamageRequest` entities created on detonation
- Contains center position, radius, source entity
- Consumed by voxel system (Epic 3) for terrain destruction
