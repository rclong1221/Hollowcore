# EPIC 15.10: Unified Voxel Destruction System

**Priority**: HIGH
**Status**: **COMPLETE**
**Goal**: Create a unified, server-authoritative voxel destruction system that supports multiple destruction sources (tools, weapons, vehicles, placed explosives) with configurable damage shapes, subsurface destruction, tool attachments, and proper multiplayer validation.

---

## 0. Design Philosophy

### Core Gameplay Loop Support

The system must support the **strategic mining gameplay loop**:

```
Survey Terrain → Drill Test Holes → Place Charges → Detonate → Collect Resources
      │                │                  │              │            │
      ▼                ▼                  ▼              ▼            ▼
   Scanner         Hand Drill         Dynamite      Trigger      Loot System
   Tools           + Bits             C4, TNT       Remote/Fuse
                   Vehicle Drill      Shaped Charge
```

### Key Scenarios That MUST Work

1. **Surface Mining**: Player uses pickaxe/drill on visible terrain
2. **Deep Drilling**: Player drills deep hole, places dynamite at bottom, detonates for subsurface blast
3. **Precision Blasting**: Player uses shaped charges to cut specific patterns
4. **Vehicle Excavation**: Drill vehicle bores tunnels of configurable diameter
5. **Chain Reactions**: Explosion triggers nearby explosives or gas pockets
6. **Tool Customization**: Same base tool with different bits for different materials/shapes

### Architecture Principles

**Loose Coupling via Abstractions:**

```
All Destruction Sources (Tools, Weapons, Explosives, Vehicles)
         │
         │ implement
         ▼
┌─────────────────────────────────────────────────────────────┐
│              IDestructionSource (interface)                 │
│  - GetDestructionIntent() : DestructionIntent               │
└─────────────────────────────────────────────────────────────┘
         │
         │ produces
         ▼
┌─────────────────────────────────────────────────────────────┐
│              DestructionIntent (data struct)                │
│  - Shape, Damage, Position, Rotation, DamageType            │
│  - No validation concerns (raw intent)                      │
└─────────────────────────────────────────────────────────────┘
         │
         │ consumed by
         ▼
┌─────────────────────────────────────────────────────────────┐
│              DestructionMediator (system)                   │
│  - Applies tool bit modifiers                               │
│  - Creates VoxelDamageRequest                               │
│  - Single point of request creation                         │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│              VoxelDamageRequest (ECS component)             │
│  - Server-validated destruction request                     │
└─────────────────────────────────────────────────────────────┘
```

**Chain Triggerable Abstraction:**

```
┌─────────────────────────────────────────────────────────────┐
│              IChainTriggerable (interface)                  │
│  - TriggerRadius : float                                    │
│  - ChainDelay : float                                       │
│  - OnChainTrigger(sourcePosition, delay)                    │
└─────────────────────────────────────────────────────────────┘
         ▲              ▲              ▲
         │              │              │
  PlacedExplosive   GasPocket    FuelBarrel (future)
```

### Shape Rotation Behavior

All directional shapes use `TargetRotation` (quaternion) to define orientation:

| Shape | Rotation Defines | Example |
|-------|------------------|---------|
| **Point** | N/A (single voxel) | Basic pickaxe |
| **Sphere** | N/A (symmetric) | Grenade |
| **Cylinder** | Axis direction | Vertical pounder vs angled drill |
| **Cone** | Direction it points (tip → base) | Shaped charge aiming |
| **Capsule** | Line direction (start → end) | Laser beam angle |
| **Box** | Full 3D orientation | Precision cutter at angle |

```
Vertical Cone:              Angled Cone (45°):
     ▲                           ╲
    /│\                           ╲
   / │ \                           ╲▶
  /  │  \                           ╲
 /   │   \                           ╲
─────┴─────                    ───────────
```

---

## 1. Problem Statement

### Current Issues

1. **Camera-Based Raycasting** - `VoxelInteractionSystem` raycasts from `Camera.main`, enabling:
   - Multiplayer cheating (camera position can't be validated)
   - Camera clipping exploits (destroy voxels through walls)
   - Third-person view inconsistency

2. **Two Disconnected Systems** - No bridge between:
   - **Survival Tools** (`ToolRaycastSystem`, `DrillUsageSystem`) - Proper ECS, character-based, but NOT connected to voxels
   - **Voxel Interaction** (`VoxelInteractionSystem`) - Camera-based, actively used but insecure

3. **No Shape Flexibility** - Current system only supports point-based destruction. Real tools need:
   - Sphere (explosions, AOE)
   - Cylinder (pounder bit, vehicle drill)
   - Cone (flamethrower, spray)
   - Capsule (laser beam, tunnel bore)
   - Box (precision cutter)

4. **No Unified Damage Pipeline** - Each destruction source has its own logic instead of a common request/validation/execution flow.

---

## 2. Current Architecture Analysis

### Survival Tools System (The Good Foundation)

| Component | File | Status |
|-----------|------|--------|
| `ToolRaycastSystem` | `Runtime/Survival/Tools/Core/Systems/ToolRaycastSystem.cs` | Raycasts from CHARACTER position |
| `ToolUsageState` | `Runtime/Survival/Tools/Core/Components/ToolComponents.cs` | Has `TargetPoint`, `TargetEntity`, `HasTarget` |
| `DrillTool` | `Runtime/Survival/Tools/Drill/Components/DrillTool.cs` | Has `VoxelDamagePerSecond`, `Range` |
| `DrillUsageSystem` | `Runtime/Survival/Tools/Drill/Systems/DrillUsageSystem.cs` | Has TODO for voxel integration (lines 67-76) |
| `VoxelDamageRequest` | `Runtime/Survival/Tools/Drill/Systems/DrillUsageSystem.cs:85-91` | Placeholder exists but unused |

### Voxel Interaction System (The Problematic One)

| Component | File | Issue |
|-----------|------|-------|
| `VoxelInteractionSystem` | `Voxel/Systems/Interaction/VoxelInteractionSystem.cs` | Uses `Camera.main` for raycasting |
| `IVoxelTool` | `Voxel/Systems/Interaction/VoxelToolInterface.cs` | Managed code, not ECS-friendly |
| `DrillTool` (class) | `Voxel/Systems/Interaction/VoxelToolInterface.cs` | Separate from ECS `DrillTool` component |

### Data Flow Problem

```
CURRENT (Broken):

Survival Tools                          Voxel System
┌─────────────────┐                    ┌─────────────────┐
│ ToolRaycastSystem│                    │ VoxelInteraction│
│ (character pos) │       X            │ (camera pos)    │
│        │        │   NO BRIDGE        │        │        │
│        ▼        │                    │        ▼        │
│ DrillUsageSystem│                    │ VoxelModRequest │
│ (TODO comment)  │                    │ (works but bad) │
└─────────────────┘                    └─────────────────┘


TARGET (Unified):

All Destruction Sources
┌─────────────┬─────────────┬─────────────┬─────────────┐
│   Drill     │   Pickaxe   │  Explosive  │   Vehicle   │
│   Tool      │   Melee     │   Weapon    │   Drill     │
└──────┬──────┴──────┬──────┴──────┬──────┴──────┬──────┘
       │             │             │             │
       ▼             ▼             ▼             ▼
┌─────────────────────────────────────────────────────────┐
│              VoxelDamageRequest (Unified)               │
│  - Shape (Point/Sphere/Cylinder/Cone/Capsule/Box)       │
│  - SourcePosition (for validation)                      │
│  - DamageType (Mining/Explosive/Heat/Crush)             │
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│         VoxelDamageValidationSystem (Server)            │
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│         VoxelDamageProcessingSystem (Server)            │
└─────────────────────────────────────────────────────────┘
```

---

## 3. Damage Shape System

### Shape Types

| Shape | Use Case | Parameters |
|-------|----------|------------|
| **Point** | Basic pickaxe, single voxel | - |
| **Sphere** | Explosions, AOE tools | radius |
| **Cylinder** | Pounder bit, vertical drill, vehicle bore | radius, height |
| **Cone** | Flamethrower, spray tools | angle, length, tipRadius |
| **Capsule** | Laser beam, tunnel bore, path carve | radius, length |
| **Box** | Precision cutter, rectangular clearing | extents (x,y,z) |

### Damage Falloff Types

| Falloff | Description | Use Case |
|---------|-------------|----------|
| **None** | Uniform damage throughout shape | Drills, cutters |
| **Linear** | Damage decreases linearly from center | Spray tools |
| **Quadratic** | Damage = Base * (1 - (dist/radius)^2) | Realistic explosions |
| **InverseSquare** | Physical falloff model | Shockwaves |
| **Shell** | Only affects outer surface | Hollow sphere |
| **Core** | Only affects center, tapers to edge | Focused beams |

### Tool Presets

| Tool | Shape | Radius/Size | DPS | Falloff |
|------|-------|-------------|-----|---------|
| Basic Pickaxe | Point | - | 25 | None |
| Hand Drill | Sphere | 0.3m | 15 | None |
| Power Drill | Sphere | 0.5m | 30 | None |
| Mining Laser | Capsule | 0.1m x 10m | 20 | Linear (50% edge) |
| Pounder Bit | Cylinder | 0.5m x 2m | 50 | None |
| Flamethrower | Cone | 25deg x 6m | 30 | Linear (30% edge) |
| Frag Grenade | Sphere | 5m | instant | Quadratic (10% edge) |
| Drill Vehicle (small) | Cylinder | 1m x 2m | 100 | Linear (80% edge) |
| Drill Vehicle (large) | Cylinder | 1.5m x 3m | 200 | Linear (80% edge) |
| Terraformer | Sphere | 3m | 80 | None |
| Precision Cutter | Box | 2x2x0.1m | 40 | None |
| Tunnel Bore | Capsule | 2m x 5m | 150 | Shell |

---

## 4. Implementation Phases

### Phase 1: Core Components

Create the unified data structures and enums.

- [x] **1.1** Create `VoxelDamageShapeType` enum
  - File: `Assets/Scripts/Voxel/Components/VoxelDamageTypes.cs`
  - Values: Point, Sphere, Cylinder, Cone, Capsule, Box

- [x] **1.2** Create `VoxelDamageFalloff` enum
  - File: `Assets/Scripts/Voxel/Components/VoxelDamageTypes.cs`
  - Values: None, Linear, Quadratic, InverseSquare, Shell, Core

- [x] **1.3** Create `VoxelDamageType` enum (material interaction)
  - File: `Assets/Scripts/Voxel/Components/VoxelDamageTypes.cs`
  - Values: Mining, Explosive, Heat, Crush, Laser

- [x] **1.4** Create unified `VoxelDamageRequest` component
  - File: `Assets/Scripts/Voxel/Components/VoxelDamageRequest.cs`
  - Fields: SourcePosition, SourceEntity, TargetPosition, TargetRotation, ShapeType, DamageType, Falloff, Damage, EdgeMultiplier, Param1-3 (shape parameters)

- [x] **1.5** Create `VoxelDamageShapeConfig` ScriptableObject
  - File: `Assets/Scripts/Voxel/Core/VoxelDamageShapeConfig.cs`
  - Design-time configuration for shapes
  - Serializable parameters per shape type

- [x] **1.6** Create `VoxelDamageShapePresets` static class
  - File: `Assets/Scripts/Voxel/Core/VoxelDamageShapePresets.cs`
  - Pre-defined configurations for common tools

- [x] **1.7** Create `IDestructionSource` interface
  - File: `Assets/Scripts/Voxel/Interfaces/IDestructionSource.cs`
  - Method: `GetDestructionIntent()` returns `DestructionIntent`
  - Decouples tools/weapons from request creation
  - All destruction sources implement this

- [x] **1.8** Create `DestructionIntent` struct
  - File: `Assets/Scripts/Voxel/Components/DestructionIntent.cs`
  - Intermediate data before `VoxelDamageRequest`
  - Fields: ShapeType, ShapeParams, Damage, Position, Rotation, DamageType, SourceEntity
  - No validation concerns (raw intent from source)

- [x] **1.9** Create `DestructionMediatorSystem`
  - File: `Assets/Scripts/Voxel/Systems/Destruction/DestructionMediatorSystem.cs`
  - Collects `DestructionIntent` from all sources
  - Applies tool bit modifiers (centralized, not in DrillUsageSystem)
  - Creates `VoxelDamageRequest` entities
  - Single point of request creation for all sources

---

### Phase 2: Server-Side Processing Systems

Create the validation and execution pipeline.

- [x] **2.1** Create `VoxelDamageValidationSystem`
  - File: `Assets/Scripts/Voxel/Systems/Destruction/VoxelDamageValidationSystem.cs`
  - Server-only, runs before processing
  - Validates: SourceEntity at SourcePosition, range checks, rate limiting
  - Rejects invalid requests, logs suspicious activity

- [x] **2.2** Create `VoxelDamageProcessingSystem`
  - File: `Assets/Scripts/Voxel/Systems/Destruction/VoxelDamageProcessingSystem.cs`
  - Server-authoritative
  - Consumes validated `VoxelDamageRequest` entities
  - Dispatches to shape-specific processors

- [x] **2.3** Create `VoxelShapeQueryJobs` (Burst-compiled)
  - File: `Assets/Scripts/Voxel/Systems/Destruction/VoxelShapeQueryJobs.cs`
  - `QuerySphereJob` - sphere voxel intersection
  - `QueryCylinderJob` - cylinder voxel intersection
  - `QueryConeJob` - cone voxel intersection
  - `QueryCapsuleJob` - capsule voxel intersection
  - `QueryBoxJob` - box voxel intersection

- [x] **2.4** Create `VoxelHealthTrackingSystem`
  - File: `Assets/Scripts/Voxel/Systems/Destruction/VoxelHealthTrackingSystem.cs`
  - Per-voxel damage accumulation (not instant destruction)
  - Track damage per chunk in NativeHashMap
  - Voxel destroyed when health <= 0

- [x] **2.5** Create `VoxelMaterialResistance` component/lookup
  - File: `Assets/Scripts/Voxel/Components/VoxelMaterialResistance.cs`
  - Define resistance per material per damage type
  - Example: Stone resists Mining (0.5x), weak to Explosive (2x)

- [x] **2.6** Extend `VoxelMaterialDefinition` with resistance data
  - File: `Assets/Scripts/Voxel/Core/VoxelMaterial.cs` (modify existing)
  - Add `ResistanceToMining`, `ResistanceToExplosive`, `ResistanceToHeat`, `ResistanceToCrush`, `ResistanceToLaser`

---

### Phase 3: Tool Integration

Connect existing tools to the new system.

- [x] **3.1** Update `DrillUsageSystem` to emit `VoxelDamageRequest`
  - File: `Assets/Scripts/Runtime/Survival/Tools/Drill/Systems/DrillUsageSystem.cs` (modify)
  - Remove TODO comment (lines 67-76)
  - Create Point or small Sphere `VoxelDamageRequest` from `ToolUsageState.TargetPoint`
  - Include `drill.VoxelDamagePerSecond * DeltaTime` as damage

- [x] **3.2** Create `DrillToolShapeConfig` component
  - File: `Assets/Scripts/Runtime/Survival/Tools/Drill/Components/DrillToolShapeConfig.cs`
  - Optional override for drill shape (default: Point)
  - Allows power drills to use Sphere shape

- [x] **3.3** Create `MeleeVoxelDamageSystem`
  - File: `Assets/Scripts/Voxel/Systems/Destruction/MeleeVoxelDamageSystem.cs`
  - Subscribe to melee hit events
  - Check if hit position intersects voxel terrain
  - Emit Point `VoxelDamageRequest` for pickaxes/hammers

- [x] **3.4** Create `MeleeVoxelDamageConfig` component
  - File: `Assets/Scripts/Weapons/Components/MeleeVoxelDamageConfig.cs`
  - Enables voxel damage for melee weapons
  - Fields: VoxelDamage, DamageType, CanDamageVoxels (bool)

- [x] **3.5** Update `SweptMeleeAuthoring` with voxel damage option
  - File: `Assets/Scripts/Weapons/Authoring/SweptMeleeAuthoring.cs` (modify)
  - Add checkbox: "Can Damage Voxels"
  - Add VoxelDamage field

- [x] **3.6** Integrate explosions with new system
  - File: `Assets/Scripts/Voxel/Systems/Interaction/VoxelExplosionSystem.cs` (modify)
  - Convert `CreateCraterRequest` to use `VoxelDamageRequest` with Sphere shape
  - Maintain backward compatibility during transition

---

### Phase 4: Shape System Implementation

Full shape support with configurable parameters.

- [x] **4.1** Implement Sphere shape processor
  - Burst job for sphere-voxel intersection
  - Falloff calculation
  - Unit tests

- [x] **4.2** Implement Cylinder shape processor
  - Burst job for cylinder-voxel intersection
  - Orientation support (rotation quaternion)
  - Unit tests

- [x] **4.3** Implement Cone shape processor
  - Burst job for cone-voxel intersection
  - Angle and tip radius parameters
  - Unit tests

- [x] **4.4** Implement Capsule shape processor
  - Burst job for capsule-voxel intersection
  - Line segment with radius
  - Unit tests

- [x] **4.5** Implement Box shape processor
  - Burst job for OBB-voxel intersection
  - Rotation support
  - Unit tests

- [x] **4.6** Create falloff calculation utilities
  - File: `Assets/Scripts/Voxel/Core/VoxelDamageFalloffUtils.cs`
  - `CalculateFalloff(distance, maxDistance, falloffType, edgeMultiplier)`
  - Burst-compatible

- [x] **4.7** Performance optimization pass
  - Spatial hashing for large shapes
  - Chunk-level culling before voxel iteration
  - Parallel job scheduling

---

### Phase 5: Vehicle Drill Support

Support for drill vehicles with collision-based destruction.

- [x] **5.1** Create `VehicleDrillConfig` component
  - File: `Assets/Scripts/Vehicles/Components/VehicleDrillConfig.cs`
  - ShapeType (typically Cylinder)
  - Radius, Depth, DPS
  - DrillDirection (local space)

- [x] **5.2** Create `VehicleDrillSystem`
  - File: `Assets/Scripts/Vehicles/Systems/VehicleDrillSystem.cs`
  - Query drill position from vehicle transform
  - Emit Cylinder `VoxelDamageRequest` while drilling active
  - Scale damage by vehicle speed (optional)

- [x] **5.3** Create `VehicleDrillAuthoring`
  - File: `Assets/Scripts/Vehicles/Authoring/VehicleDrillAuthoring.cs`
  - Baker for drill configuration
  - Visual gizmo for drill shape preview

- [x] **5.4** Support multiple drill heads per vehicle
  - Buffer component for multiple drill configs
  - Example: Front bore + side augers

- [x] **5.5** Collision feedback system
  - Detect when drill hits indestructible material
  - Apply resistance force to vehicle physics
  - Audio/VFX feedback

---

### Phase 6: Placed Explosives System

Support for dynamite, C4, shaped charges, and other placeable explosives.

- [x] **6.1** Create `PlacedExplosive` component
  - File: `Assets/Scripts/Explosives/Components/PlacedExplosive.cs`
  - Fields: ExplosiveType, ShapeConfig, FuseTime, IsArmed, PlacedPosition, PlacedRotation
  - Supports subsurface placement (inside drilled holes)

- [x] **6.2** Create `ExplosiveTrigger` component
  - File: `Assets/Scripts/Explosives/Components/ExplosiveTrigger.cs`
  - TriggerType enum: Timer, Remote, Impact, Proximity, Chain
  - TriggerDelay, TriggerRadius (for proximity)

- [x] **6.3** Create `ExplosivePlacementSystem`
  - File: `Assets/Scripts/Explosives/Systems/ExplosivePlacementSystem.cs`
  - Handle placement input while holding explosive item
  - Validate placement position (must be on solid surface or inside void)
  - Support placement at bottom of drilled holes (subsurface)
  - Visual preview before placement

- [x] **6.4** Create `ExplosiveDetonationSystem`
  - File: `Assets/Scripts/Explosives/Systems/ExplosiveDetonationSystem.cs`
  - Process armed explosives based on trigger type
  - Emit `VoxelDamageRequest` with explosive's shape config
  - **Subsurface blast**: explosion originates FROM placed position, destroys outward
  - Handle fuse countdown, remote trigger signals

- [x] **6.5** Create explosive item definitions
  - `DynamiteConfig` - Sphere, medium radius, timer fuse
  - `C4Config` - Sphere, large radius, remote trigger
  - `ShapedChargeConfig` - Cone, directional blast
  - `MiningChargeConfig` - Cylinder, downward blast for deep holes
  - `DetCordConfig` - Capsule, line-based cutting

- [x] **6.6** Create `ExplosiveAuthoring` and baker
  - File: `Assets/Scripts/Explosives/Authoring/ExplosiveAuthoring.cs`
  - Configure explosive type, shape, trigger in editor
  - Preview blast radius in scene view

- [x] **6.7** Create remote detonator tool
  - File: `Assets/Scripts/Explosives/Components/RemoteDetonator.cs`
  - Tracks placed explosives by player
  - Trigger single, sequence, or all

- [x] **6.8** Subsurface placement validation
  - Detect if position is inside terrain (void check)
  - Allow placement on drilled hole walls/floor
  - Prevent placement in solid rock (must drill first)

---

### Phase 7: Tool Bits & Attachments

Support for interchangeable tool parts that modify destruction behavior.

- [x] **7.1** Create `ToolBit` component
  - File: `Assets/Scripts/Tools/Components/ToolBit.cs`
  - BitType enum: Standard, Diamond, Tungsten, Plasma, etc.
  - ShapeModifier (overrides base tool shape)
  - DamageMultiplier, SpeedMultiplier
  - MaterialEffectiveness (bonus vs specific materials)

- [x] **7.2** Create `ToolBitSocket` component
  - File: `Assets/Scripts/Tools/Components/ToolBitSocket.cs`
  - Reference to equipped bit entity
  - CompatibleBitTypes (what bits fit this tool)

- [x] **7.3** Create `ToolBitConfig` ScriptableObject
  - File: `Assets/Scripts/Tools/Data/ToolBitConfig.cs`
  - Design-time bit configuration
  - Shape override parameters
  - Material effectiveness table

- [x] **7.4** Create bit presets for drills
  - `StandardBit` - Point shape, balanced
  - `WideBoreBit` - Sphere shape, larger radius, slower
  - `PrecisionBit` - Point shape, faster, less durable
  - `DiamondBit` - Point shape, bonus vs hard materials
  - `PlasmaBit` - Small sphere, Heat damage type
  - `PounderBit` - Cylinder shape, downward oriented

- [x] **7.5** Create bit presets for vehicle drills
  - `TunnelBore` - Large cylinder, forward
  - `AugerBit` - Helix pattern (simplified as rotating cylinder)
  - `CuttingHead` - Cone shape, angled
  - `ExcavatorScoop` - Box shape, surface scraping

- [x] **7.6** Update `DrillUsageSystem` to read bit config
  - Check for `ToolBitSocket` component
  - Apply bit's shape override if present
  - Apply damage/speed multipliers

- [x] **7.7** Bit durability system
  - Bits degrade with use
  - Different durability vs different materials
  - Diamond bits last longer vs stone, break faster vs metal

- [x] **7.8** Bit swapping UI/interaction
  - Quick-swap in inventory
  - Visual feedback of equipped bit
  - Bit comparison tooltip

---

### Phase 8: Chain Reactions & Environmental

Support for explosions triggering other explosives and environmental hazards.

- [x] **8.1** Create `IChainTriggerable` interface
  - File: `Assets/Scripts/Explosives/Interfaces/IChainTriggerable.cs`
  - Properties: `TriggerRadius`, `ChainDelay`
  - Method: `OnChainTrigger(float3 sourcePosition, float delay)`
  - Implemented by: `PlacedExplosive`, `GasPocket`, future `FuelBarrel`

- [x] **8.2** Create `ChainReactionSystem`
  - File: `Assets/Scripts/Explosives/Systems/ChainReactionSystem.cs`
  - When explosion occurs, query ALL `IChainTriggerable` entities (not just explosives)
  - If within trigger radius, call `OnChainTrigger` with delay
  - Cascade effect with slight timing offsets
  - Max chain depth limit to prevent infinite loops

- [x] **8.3** Create `ExplosiveProximityTrigger` system
  - Explosives with Proximity trigger type
  - Detect player/entity entering radius
  - Detect other explosions entering radius (chain)

- [x] **8.4** Create `GasPocket` component
  - File: `Assets/Scripts/Voxel/Components/GasPocket.cs`
  - Implements `IChainTriggerable`
  - Hidden voxel regions containing gas
  - Ignites when exposed to Heat damage or explosion
  - Creates secondary explosion

- [x] **8.5** Create `GasPocketDetonationSystem`
  - Detect when voxel destruction exposes gas pocket
  - Trigger delayed explosion at gas location
  - Chain to nearby gas pockets via `IChainTriggerable`

- [x] **8.6** Gas pocket generation in world gen
  - Procedurally place gas pockets in certain biomes/depths
  - Scanner tool can detect nearby gas
  - Risk/reward for deep mining

- [x] **8.7** Create `StructuralIntegrity` system (optional/future)
  - Track unsupported voxel regions
  - Collapse when support removed
  - Creates avalanche/cave-in effect

- [x] **8.8** Environmental damage feedback
  - Warning indicators for gas pockets
  - Rumble/sound before collapse
  - Escape window for player

- [x] **8.9** Create `FuelBarrel` component (future extensibility)
  - Implements `IChainTriggerable`
  - Placeable/destructible world object
  - Demonstrates interface extensibility

---

### Phase 9: Editor Tooling - VoxelWorkstation

Extend VoxelToolsModule with shape configuration.

- [x] **9.1** Add Shape Designer section to `VoxelToolsModule`
  - File: `Assets/Scripts/Voxel/Editor/Modules/VoxelToolsModule.cs` (modify)
  - Shape type selector buttons
  - Context-sensitive parameter sliders

- [x] **9.2** Add shape presets UI
  - Quick-select buttons: Pickaxe, Drill, Pounder, Laser, Flamethrower, Grenade, Vehicle
  - Load preset populates all parameters

- [x] **9.3** Add damage/falloff configuration
  - DPS slider
  - Damage type dropdown
  - Falloff type dropdown
  - Edge multiplier slider (disabled when Falloff=None)

- [x] **9.4** Add scene view shape visualization
  - Draw wireframe shape at preview position
  - Color-code falloff gradient (red=high damage, green=low)
  - Show affected voxel count estimate

- [x] **9.5** Add play mode testing
  - "Test Shape" button spawns damage at scene camera
  - Real-time voxel destruction preview
  - Statistics: voxels destroyed, chunks affected, time taken

- [x] **9.6** Add material resistance configuration UI
  - Per-material resistance sliders for each damage type
  - Batch edit mode for multiple materials
  - Import/Export to JSON

---

### Phase 10: Editor Tooling - EquipmentWorkstation

New module for weapon/tool voxel interaction.

- [x] **10.1** Create `VoxelInteractionModule`
  - *Implemented via Component Inspectors (ToolAuthoring, MeleeVoxelDamageConfig)*
  - File: `Assets/Editor/EquipmentWorkstation/Modules/VoxelInteractionModule.cs` (Replaced by Inspectors)
  - New tab in Equipment Workstation

- [x] **10.2** Melee voxel damage section
  - Enable/disable voxel damage per weapon
  - Damage amount, damage type
  - Link to shape preset or custom shape

- [x] **10.3** Explosive voxel damage section
  - Crater radius, strength
  - Falloff curve preview
  - Loot spawn toggle

- [x] **10.4** Tool linking section
  - Assign `VoxelDamageShapeConfig` to tool prefabs
  - Preview shape on selected prefab

- [x] **10.5** Validation checks
  - Warn if melee weapon has voxel damage but no shape config
  - Warn if explosive has no crater settings

---

### Phase 11: Network Synchronization

Ensure multiplayer works correctly.

- [x] **11.1** Create `VoxelDamageRequestRpc`
  - Client sends request to server
  - Include all shape parameters
  - Server validates and processes

- [x] **11.2** Update `VoxelModificationServerSystem`
  - Process `VoxelDamageRequest` instead of direct modification
  - Emit `VoxelDestroyedEvent` for loot spawning
  - Broadcast results to clients

- [x] **11.3** Client-side prediction for mining
  - Show visual mining progress locally
  - Server confirms/rejects destruction
  - Rollback on rejection

- [x] **11.4** Anti-cheat validation
  - Rate limiting per player
  - Distance validation (tool range)
  - Impossible angle detection

---

### Phase 12: Migration & Cleanup

Deprecate old system and clean up. **This phase is LAST - do not delete old systems until all other phases complete.**

- [x] **12.1** Create migration path for existing saves
  - Handle old `VoxelModificationRequest` format
  - Convert to new `VoxelDamageRequest` on load

- [x] **12.2** Add deprecation warnings to old systems
  - `VoxelInteractionSystem` - log warning on use
  - `IVoxelTool` interface - mark obsolete

- [x] **12.3** Update all tools/weapons to use new system
  - Audit all destruction sources
  - Ensure none use old camera-based system

- [x] **12.4** Remove old `VoxelInteractionSystem`
  - Delete file: `Assets/Scripts/Voxel/Systems/Interaction/VoxelInteractionSystem.cs`
  - Remove camera-based mining code

- [x] **12.5** Remove old `IVoxelTool` interface and implementations
  - Delete managed `DrillTool`, `ExplosiveTool` classes
  - Keep only ECS components

- [x] **12.6** Documentation update
  - Update integration guides
  - Document new shape system
  - Create migration guide for modders

---

## 5. Implementation Files Summary

### New Files

| File | Purpose | Phase |
|------|---------|-------|
| `Voxel/Components/VoxelDamageTypes.cs` | Enums for shapes, falloff, damage types | 1 |
| `Voxel/Components/VoxelDamageRequest.cs` | Unified damage request component | 1 |
| `Voxel/Core/VoxelDamageShapeConfig.cs` | ScriptableObject for shape config | 1 |
| `Voxel/Core/VoxelDamageShapePresets.cs` | Pre-defined tool presets | 1 |
| `Voxel/Interfaces/IDestructionSource.cs` | Destruction source abstraction | 1 |
| `Voxel/Components/DestructionIntent.cs` | Raw destruction intent data | 1 |
| `Voxel/Systems/Destruction/DestructionMediatorSystem.cs` | Converts intent → request | 1 |
| `Voxel/Core/VoxelDamageFalloffUtils.cs` | Falloff calculation utilities | 4 |
| `Voxel/Systems/Destruction/VoxelDamageValidationSystem.cs` | Server validation | 2 |
| `Voxel/Systems/Destruction/VoxelDamageProcessingSystem.cs` | Server execution | 2 |
| `Voxel/Systems/Destruction/VoxelShapeQueryJobs.cs` | Burst shape queries | 2 |
| `Voxel/Systems/Destruction/VoxelHealthTrackingSystem.cs` | Per-voxel health | 2 |
| `Voxel/Systems/Destruction/MeleeVoxelDamageSystem.cs` | Melee-to-voxel bridge | 3 |
| `Voxel/Components/VoxelMaterialResistance.cs` | Material resistance data | 2 |
| `Weapons/Components/MeleeVoxelDamageConfig.cs` | Melee voxel config | 3 |
| `Vehicles/Components/VehicleDrillConfig.cs` | Vehicle drill config | 5 |
| `Vehicles/Systems/VehicleDrillSystem.cs` | Vehicle drill processing | 5 |
| `Vehicles/Authoring/VehicleDrillAuthoring.cs` | Vehicle drill authoring | 5 |
| `Explosives/Components/PlacedExplosive.cs` | Placed explosive state | 6 |
| `Explosives/Components/ExplosiveTrigger.cs` | Trigger configuration | 6 |
| `Explosives/Systems/ExplosivePlacementSystem.cs` | Placement handling | 6 |
| `Explosives/Systems/ExplosiveDetonationSystem.cs` | Detonation processing | 6 |
| `Explosives/Authoring/ExplosiveAuthoring.cs` | Editor authoring | 6 |
| `Explosives/Components/RemoteDetonator.cs` | Remote trigger tool | 6 |
| `Tools/Components/ToolBit.cs` | Bit configuration | 7 |
| `Tools/Components/ToolBitSocket.cs` | Bit socket on tools | 7 |
| `Tools/Data/ToolBitConfig.cs` | Bit ScriptableObject | 7 |
| `Explosives/Interfaces/IChainTriggerable.cs` | Chain reaction abstraction | 8 |
| `Explosives/Systems/ChainReactionSystem.cs` | Chain explosion logic | 8 |
| `Voxel/Components/GasPocket.cs` | Gas pocket hazard | 8 |
| `Editor/EquipmentWorkstation/Modules/VoxelInteractionModule.cs` | Equipment workstation module | 10 |

### Modified Files

| File | Change | Phase |
|------|--------|-------|
| `Survival/Tools/Drill/Systems/DrillUsageSystem.cs` | Implement `IDestructionSource`, emit `DestructionIntent` | 3 |
| `Survival/Tools/Drill/Components/DrillTool.cs` | Add shape config reference | 3 |
| `Weapons/Authoring/SweptMeleeAuthoring.cs` | Add voxel damage option, implement `IDestructionSource` | 3 |
| `Voxel/Systems/Interaction/VoxelExplosionSystem.cs` | Use new request system via mediator | 3 |
| `Explosives/Components/PlacedExplosive.cs` | Implement `IChainTriggerable` | 8 |
| `Voxel/Core/VoxelMaterial.cs` | Add resistance fields | 2 |
| `Voxel/Editor/Modules/VoxelToolsModule.cs` | Add shape designer UI | 9 |

### Deleted Files (Phase 12)

| File | Reason |
|------|--------|
| `Voxel/Systems/Interaction/VoxelInteractionSystem.cs` | Replaced by tool-based system |
| `Voxel/Systems/Interaction/VoxelToolInterface.cs` | Managed code, replaced by ECS |

---

## 6. Testing Checklist

### Unit Tests

- [x] Shape query jobs return correct voxels for each shape type
- [x] Shape query jobs respect rotation for directional shapes (Cone, Cylinder, Capsule, Box)
- [x] Falloff calculations produce expected damage multipliers
- [x] Material resistance modifies damage correctly
- [x] Validation rejects out-of-range requests
- [x] Validation rejects requests from wrong source position
- [x] Tool bit modifiers apply correctly to damage
- [x] Explosive trigger types fire at correct conditions
- [x] `DestructionMediator` correctly converts `DestructionIntent` to `VoxelDamageRequest`
- [x] `IChainTriggerable` implementations receive chain trigger calls

### Integration Tests

- [ ] Drill tool destroys voxels at correct rate
- [ ] Pickaxe melee hit damages voxels
- [ ] Explosive creates correct crater shape
- [ ] Vehicle drill carves tunnel at correct size
- [ ] Multiplayer: client request validated by server
- [ ] Multiplayer: destruction synced to all clients

### Gameplay Scenario Tests

- [ ] **Surface Mining**: Basic drill/pickaxe destroys surface voxels
- [ ] **Deep Hole Drilling**: Can drill vertical shaft 10+ meters deep
- [ ] **Subsurface Bomb Placement**: Can place dynamite at bottom of drilled hole
- [ ] **Subsurface Detonation**: Explosion from placed dynamite destroys voxels in sphere FROM that position
- [ ] **Shaped Charge**: Cone-shaped charge creates directional blast pattern
- [ ] **Chain Reaction**: Explosion triggers nearby armed explosives with cascade timing
- [ ] **Gas Pocket Ignition**: Breaking into gas pocket triggers secondary explosion
- [ ] **Tool Bit Swap**: Changing drill bit changes destruction shape/effectiveness
- [ ] **Vehicle Bore**: Drill vehicle creates consistent tunnel of correct diameter
- [ ] **Angled Drilling**: Cone/Cylinder shapes at 45° angle destroy voxels in correct rotated pattern
- [ ] **Remote Detonation**: Player can arm multiple charges and trigger remotely
- [ ] **Strategic Yield**: Deep blast yields more resources than surface mining same volume
- [ ] **Mixed Chain Reaction**: Explosion triggers both PlacedExplosive AND GasPocket via IChainTriggerable

### Performance Tests

- [ ] Large sphere (radius 10m) completes within frame budget
- [ ] Multiple simultaneous destruction sources don't spike
- [ ] Vehicle drill at high speed maintains performance
- [ ] No GC allocations in hot paths
- [ ] Chain reaction of 10+ explosives doesn't freeze game

### Regression Tests

- [ ] Existing explosion system still works during migration
- [ ] Loot spawns correctly from destroyed voxels
- [ ] Mining progress UI updates correctly
- [ ] Material hardness affects mining time

---

## 7. Dependencies

### Requires

- EPIC 2.6: Inventory system (for resource collection)
- EPIC 4.x: Damage pipeline (for DamageType consistency)
- EPIC 15.5: Swept melee system (for melee-voxel integration)

### Enables

- EPIC ??.?: Drill vehicles
- EPIC ??.?: Terraforming tools
- EPIC ??.?: Destructible structures

---

## 8. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Performance regression with complex shapes | Medium | High | Burst compilation, spatial culling, profiling |
| Multiplayer desync | Medium | High | Server authority, validation, reconciliation |
| Breaking existing mining | Low | High | Maintain old system until Phase 12 |
| Shape math errors | Medium | Medium | Unit tests, visual debugging |
| Chain reaction infinite loops | Low | High | Max chain depth limit, cooldown per explosive |
| Subsurface placement exploits | Medium | Medium | Validate placement position on server |
| Tool bit balance issues | Medium | Low | Designer-tunable via ScriptableObjects |

---

## 9. Success Criteria

1. **No camera-based raycasting** for voxel destruction
2. **All destruction sources** implement `IDestructionSource` interface
3. **Single point of request creation** via `DestructionMediator`
4. **Server validates** all destruction requests
5. **6+ shape types** supported with configurable parameters
6. **Shape rotation works**: Directional shapes (Cone, Cylinder) destroy at correct angles
7. **Editor tooling** for shape design and testing
8. **No performance regression** from current system
9. **Multiplayer secure** against common exploits
10. **Subsurface explosives work**: Drill hole → place dynamite → detonate creates underground blast
11. **Tool bits are swappable**: Same base tool with different bits produces different results
12. **Chain reactions function**: Multiple `IChainTriggerable` entities can trigger each other
13. **Strategic mining viable**: Deep drilling + explosives is more efficient than surface mining
14. **Loosely coupled**: Adding new destruction source only requires implementing interface

---

## 10. Future Migration Tasks

These items should be addressed after the core EPIC 15.10 phases are complete:

### 10.1 Migrate VoxelExplosionTester to Unified API

**File**: `Assets/Scripts/Voxel/Editor/VoxelExplosionTesterWindow.cs`

**Current State**: Uses legacy `CreateCraterRequest` → `VoxelExplosionSystem` path.

**Required Changes**:
- Replace `CreateCraterRequest` with unified `VoxelDamageRequest.CreateSphere()`
- Update to use `VoxelDamageProcessingSystem` instead of `VoxelExplosionSystem`
- Add support for testing different shape types (Cylinder, Cone, Box)
- Add damage type selector for testing material resistance

**Priority**: LOW (current system still functional)
