# EPIC 2.6: Resource Collection & Inventory

**Priority**: MEDIUM
**Goal**: Pick up resources from environment
**Dependencies**: Epic 2.3 (drill tool), Voxel system
**Status**: ✅ IMPLEMENTED

## Architecture
Use `DynamicBuffer` for inventory to support arbitrary resource types without fixed component size.

## Components

**InventoryItem** (IBufferElementData, on Player, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `ResourceType` | ResourceTypeEnum | Yes | Stone, Metal, BioMass, Crystal, TitanBone, ThermalGlass, Isotope |
| `Quantity` | int | Yes | Stack count |

**InventoryCapacity** (IComponentData, on Player)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `MaxWeight` | float | No | Maximum carry weight (default: 100 kg) |
| `CurrentWeight` | float | Quantization=100 | Calculated total weight |
| `IsOverencumbered` | bool | Yes | True if CurrentWeight > MaxWeight |

**ResourceWeights** (Singleton/BlobAsset)
| Resource | Weight per unit (kg) |
|----------|---------------------|
| Stone | 2.0 |
| Metal | 3.0 |
| BioMass | 0.5 |
| Crystal | 1.0 |
| TitanBone | 5.0 |
| ThermalGlass | 2.0 |
| Isotope | 4.0 |

**ResourceNode** (IComponentData, on world entities)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `ResourceType` | ResourceTypeEnum | Yes | What resource this yields |
| `Amount` | int | Yes | Remaining quantity |
| `RequiresDrill` | bool | No | If true, must use drill tool |
| `CollectionTime` | float | No | Seconds to fully collect (0 = instant) |
| `RespawnTime` | float | No | Seconds to respawn after depleted (0 = no respawn) |

**ResourceNodeDepleted** (IComponentData, tag)
- Added when `Amount <= 0`
- Used for respawn timing

**Interactable** (IComponentData, on resource nodes)
| Field | Type | Description |
|-------|------|-------------|
| `InteractionType` | enum | Collect, Use, Examine |
| `PromptText` | FixedString64 | "Press E to collect" |
| `Range` | float | Interaction range (default: 2m) |

**InteractionTarget** (IComponentData, on Player, Predicted)
| Field | Type | GhostField | Description |
|-------|------|------------|-------------|
| `TargetEntity` | Entity | Yes | Current interaction target |
| `CanInteract` | bool | Yes | True if in range and requirements met |

## Systems

**InteractionDetectionSystem** (SimulationSystemGroup, Predicted)
```
UpdateAfter: ToolRaycastSystem
Burst: Yes
```
- Query: `LocalTransform`, `PlayerLook`, `InteractionTarget`
- Also queries all `Interactable` entities
- Responsibility:
  - Find closest interactable in range and in view
  - Update `InteractionTarget`

**ResourceCollectionSystem** (SimulationSystemGroup, ServerWorld)
```
Burst: Yes
```
- Query: `InteractionTarget`, `PlayerInput`, `InventoryItem` buffer
- Also queries `ResourceNode` of target
- Responsibility:
  - On interact input (E key)
  - Check `RequiresDrill` vs active tool
  - Transfer resources to player inventory
  - Decrement or deplete node

**InventoryWeightSystem** (SimulationSystemGroup, Predicted)
```
Burst: Yes
ChangeFilter: InventoryItem buffer
```
- Query: `InventoryItem` buffer, `InventoryCapacity`
- Responsibility:
  - Recalculate `CurrentWeight` from buffer contents
  - Set `IsOverencumbered` flag
  - Only runs when inventory changes (via ChangeFilter)

**OverencumberedMovementSystem** (SimulationSystemGroup, Predicted)
```
UpdateBefore: PlayerMoveSystem
Burst: Yes
```
- Query: `InventoryCapacity`, `PlayerMovementSettings` where `IsOverencumbered`
- Responsibility:
  - Apply movement speed penalty (e.g., 50% speed)
  - Disable sprinting

**ResourceNodeRespawnSystem** (SimulationSystemGroup, ServerWorld)
```
Burst: Yes
```
- Query: `ResourceNode`, `ResourceNodeDepleted`
- Responsibility:
  - Track time since depletion
  - Respawn node (restore `Amount`, remove `Depleted` tag)

## Integration
- Drill tool (Epic 2.3) sends `ResourceCollectionRequest` to this system
- Voxel destruction can yield resources via this system
- Ship storage (Epic 3) allows depositing inventory

## Acceptance Criteria
- [x] Resources collect into inventory buffer
- [x] Weight calculates correctly
- [x] Overencumbered state applies movement penalty
- [x] Resource nodes deplete and respawn
- [x] Drill required for appropriate nodes
- [x] UI shows inventory contents and weight

## Implementation Details

### File Structure
```
Assets/Scripts/Runtime/Survival/Resources/
├── Components/
│   └── ResourceComponents.cs
├── Systems/
│   ├── InteractionDetectionSystem.cs
│   ├── ResourceCollectionSystem.cs
│   ├── InventoryWeightSystem.cs
│   └── ResourceNodeRespawnSystem.cs
└── Authoring/
    └── ResourceAuthoring.cs
```

### Components Implemented

**ResourceComponents.cs**
- `ResourceType` enum: Stone, Metal, BioMass, Crystal, TitanBone, ThermalGlass, Isotope
- `InteractionType` enum: None, Collect, Use, Examine
- `InventoryItem` (IBufferElementData): Player inventory storage
- `InventoryCapacity` (IComponentData): Weight tracking, overencumbered state
- `ResourceWeights` (IComponentData): Singleton with weight per resource type
- `ResourceNode` (IComponentData): World resource node state
- `ResourceNodeDepleted` (IComponentData): Tag for respawn tracking
- `Interactable` (IComponentData): Makes entity interactable
- `InteractionTarget` (IComponentData): Player's current target
- `CollectResourceRequest` (IBufferElementData): Collection requests
- `CollectionProgress` (IComponentData): Channeled collection state
- `InteractionDisplayState` (IComponentData): UI state

**InventoryWeightSystem.cs**
- `OverencumberedModifier` (IComponentData): Movement penalty state

### Systems Implemented

**InteractionDetectionSystem.cs**
- `InteractionDetectionSystem`: Finds closest interactable in range and view
- `InteractionDisplaySystem`: Updates UI state for prompts

**ResourceCollectionSystem.cs**
- `ResourceCollectionInputSystem`: Handles interact input, drill requirements
- `ResourceCollectionServerSystem`: Server-authoritative resource transfer

**InventoryWeightSystem.cs**
- `InventoryWeightSystem`: Calculates total weight, sets overencumbered flag
- `OverencumberedMovementSystem`: Applies movement penalty

**ResourceNodeRespawnSystem.cs**
- `ResourceNodeRespawnSystem`: Restores depleted nodes after timer

### Authoring Components

**ResourceAuthoring.cs**
- `ResourceNodeAuthoring`: Configure resource nodes (type, amount, collection time)
- `PlayerInventoryAuthoring`: Player capacity and starting items
- `ResourceWeightsAuthoring`: Singleton for resource weight values

### Network Architecture
- Predicted interaction detection on client
- Server-authoritative resource collection and node depletion
- GhostField sync for inventory items and weight

### Resource Weight Defaults
| Resource | Weight (kg) |
|----------|-------------|
| Stone | 2.0 |
| Metal | 3.0 |
| BioMass | 0.5 |
| Crystal | 1.0 |
| TitanBone | 5.0 |
| ThermalGlass | 2.0 |
| Isotope | 4.0 |

### Integration Points
- Tool system (Epic 2.3): Drill requirement check via ActiveTool/Tool lookup
- Voxel system (Epic 3): Can yield resources via ResourceNode creation
- Ship storage (Epic 3): Can consume InventoryItem buffer
