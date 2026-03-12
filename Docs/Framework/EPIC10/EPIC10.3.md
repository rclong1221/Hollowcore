# EPIC 10.3: Fluids & Hazards

**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Dependencies**: EPIC 10.2 (Cave & Hollow Earth Systems)  
**Estimated Time**: 4-5 days

---

## Quick Start Guide

### Setting Up Fluids (5 minutes)

1. **Create Fluid Definitions**:
   - Go to **DIG → Quick Setup → Generation → Create Fluid Definitions**
   - Creates 5 fluid types: Water, Oil, Lava, Toxic Gas, Acid

2. **Configure Hollow Earth Layers**:
   - Open your `HollowEarthProfile` assets
   - Set `Primary Fluid Type` (Water, Lava, etc.)
   - Adjust `Fluid Coverage` (0-1)
   - Enable `Has Fluid Rivers` for lava rivers
   - Set `Lake Elevation` (height above floor)

3. **Enter Play Mode**: Hollow layers now generate with fluids!

### Manual Fluid Definition

1. Right-click in Project → **Create → DIG → World → Fluid Definition**
2. Set `FluidID` (unique, 1-255)
3. Configure properties (viscosity, damage, effects)
4. Reference in hollow profiles or fluid systems

---

## Component Reference

### FluidType (Enum)

| Value | Name | ID | Damage Type |
|-------|------|----|-------------|
| None | No fluid | 0 | - |
| Water | Water | 1 | Drowning |
| Oil | Oil | 2 | None (flammable) |
| Lava | Lava | 3 | Burning |
| ToxicGas | Toxic Gas | 4 | Toxic |
| Acid | Acid | 5 | Corrosive |

---

### FluidDefinition (ScriptableObject)

**Location**: `Assets/Scripts/Voxel/Fluids/FluidDefinition.cs`

| Category | Field | Description |
|----------|-------|-------------|
| **Identity** | `FluidID` | Unique ID (1-255) |
| | `FluidName` | Display name |
| | `Type` | FluidType enum |
| **Appearance** | `FluidColor` | Primary color with alpha |
| | `Transparency` | 0 = opaque, 1 = clear |
| | `IsEmissive` | Glows (for lava) |
| | `EmissionColor` | HDR glow color |
| **Physics** | `Viscosity` | Flow speed (water=1, lava=0.1) |
| | `Density` | Buoyancy (water=1, oil=0.8) |
| | `SpreadRate` | How fast fluid spreads |
| **Pressure** | `IsPressurized` | Erupts when released |
| | `PressureLevel` | Eruption violence |
| | `EruptionRadius` | Explosion size |
| **Damage** | `DamageType` | Drowning, Burning, Toxic, Corrosive |
| | `DamagePerSecond` | DPS when submerged |
| | `DamageStartDepth` | Depth at which damage begins |
| **Special** | `IsFlammable` | Can catch fire (oil) |
| | `CoolsToSolid` | Becomes rock (lava→obsidian) |
| | `CooledMaterialID` | Material when cooled |

---

### FluidCell (ECS Component)

Represents a fluid voxel in the chunk.

| Field | Type | Description |
|-------|------|-------------|
| `Type` | `byte` | FluidType value |
| `Level` | `byte` | Fill level (0-255) |
| `Pressure` | `byte` | For pressurized fluids |
| `Temperature` | `half` | For cooling (lava) |

**Factory Methods**:
```csharp
FluidCell.Empty       // No fluid
FluidCell.Water(255)  // Full water
FluidCell.Lava(255)   // Full lava at 1200°
```

---

### InFluidZone (ECS Component)

Added to entities submerged in fluid.

| Field | Type | Description |
|-------|------|-------------|
| `FluidType` | `byte` | Which fluid |
| `SubmersionDepth` | `float` | How deep (meters) |
| `TimeInFluid` | `float` | Duration submerged |
| `FluidSurfacePosition` | `float3` | Surface Y |
| `FluidTemperature` | `float` | Current temp |

---

### FluidService (Static Service)

Converts ScriptableObjects to Burst-compatible data.

```csharp
// Initialize from FluidDefinition array
FluidService.Initialize(fluidDefinitions);

// Access in jobs
var fluidParams = FluidService.FluidParamsArray;

// Cleanup
FluidService.Dispose();
```

---

### FluidLookup (Burst-Compiled)

Static methods for fluid generation.

```csharp
// Check if position should have fluid
bool hasFluid = FluidLookup.ShouldHaveFluid(
    worldPos, floorHeight, fluidElevation, 
    coverage, seed, out fluidType, out level);

// Check if in lava river
bool inRiver = FluidLookup.IsInLavaRiver(
    worldPos, floorHeight, riverWidth, seed, out level);

// Calculate damage
float dmg = FluidLookup.CalculateFluidDamage(
    fluidType, depth, startDepth, dps, deltaTime);
```

---

## ECS Systems

### FluidDamageSystem

Applies damage to entities in hazardous fluids.

- Drowning: Gradual damage when head submerged
- Burning: Instant damage in lava
- Toxic: Constant damage in gas
- All Burst-compiled

### FluidZoneUpdateSystem

Updates `InFluidZone` components based on entity positions.

### FluidEruptionSystem

Handles pressurized fluid eruptions when pockets are breached.

### LavaCoolingSystem

(Placeholder) Cools lava to obsidian when adjacent to water.

---

## Integration with Hollow Earth

Fluids are configured per `HollowEarthProfile`:

| Field | Use |
|-------|-----|
| `PrimaryFluidType` | Water for lakes, Lava for volcanic |
| `LakeElevation` | Height above floor (meters) |
| `FluidCoverage` | 0.3 = 30% of floor has lakes |
| `HasFluidRivers` | Enable flowing rivers |
| `RiverWidth` | River/channel width |
| `HasLavaFlows` | Enable lava (for volcanic) |

### Example Layer Configurations

| Hollow Layer | Fluid Config |
|--------------|--------------|
| Mushroom Forest | Water, 20% coverage, 15m elevation |
| Crystal Cavern | Water, 15% coverage, no rivers |
| Volcanic Depths | Lava, 25% coverage, lava rivers |
| Ancient Core | None |

---

## Damage Values (Default)

| Fluid | DPS | Start Depth | Notes |
|-------|-----|-------------|-------|
| Water | 5 | 2m | Drowning when head under |
| Lava | 50 | 0m | Instant burn on contact |
| Toxic Gas | 10 | 0m | Constant toxic damage |
| Acid | 25 | 0m | Corrosive on contact |
| Oil | 0 | - | No damage, but flammable |

---

## Burst-Compatible Data

### FluidParams Struct

```csharp
public struct FluidParams
{
    public byte FluidID;
    public byte Type;           // FluidType
    public byte DamageType;     // FluidDamageType
    public byte Flags;          // Packed booleans
    
    public float Viscosity;
    public float Density;
    public float DamagePerSecond;
    public float DamageStartDepth;
    public float PressureLevel;
}
```

**Flags format**: Bit-packed booleans
- Bit 0: IsPressurized
- Bit 1: IsFlammable
- Bit 2: IsToxic
- Bit 3: CoolsToSolid

---

## Jobs

### FluidPlacementJob

Places fluids during chunk generation based on hollow parameters.

```csharp
new FluidPlacementJob
{
    ChunkWorldOrigin = origin,
    FloorBaseY = hollow.BottomDepth,
    FluidElevation = hollow.FluidElevation,
    FluidCoverage = hollow.FluidCoverage,
    FluidType = hollow.FluidType,
    HasRivers = hollow.HasFluidRivers,
    RiverWidth = hollow.RiverWidth,
    VoxelDensities = densities,
    FluidCells = fluidGrid
}.Schedule(voxelCount, 256);
```

### FluidSimulationJob

Cellular automata-based fluid flow simulation.
- Flows down first (gravity)
- Spreads horizontally
- Viscosity controls speed

### FluidZoneDetectionJob

Detects which entities are in fluid regions.

---

## Performance

| Operation | Budget | Approach |
|-----------|--------|----------|
| Fluid placement | < 0.5ms | Burst parallel job |
| Fluid simulation | < 1ms | Burst, limited updates |
| Damage calculation | < 0.1ms | Burst IJobEntity |

---

## Editor Tools

### FluidQuickSetup

**Menu**: `DIG → Quick Setup → Generation → Create Fluid Definitions`

Creates 5 pre-configured fluid types:
- Water (ID 1)
- Oil (ID 2)
- Lava (ID 3)
- Toxic Gas (ID 4)
- Acid (ID 5)

---

## Files Created

| File | Purpose |
|------|---------|
| `Fluids/FluidType.cs` | Fluid type enum |
| `Fluids/FluidDefinition.cs` | ScriptableObject for fluid config |
| `Fluids/FluidComponents.cs` | ECS components |
| `Fluids/FluidService.cs` | Burst-compatible data service |
| `Fluids/FluidJobs.cs` | Placement and simulation jobs |
| `Fluids/FluidSystems.cs` | Damage and eruption systems |
| `Editor/FluidQuickSetup.cs` | Quick setup menu |

---

## Designer Workflow

1. **Create Fluid Definitions** (or use quick setup)
2. **Configure HollowEarthProfile** for each hollow layer:
   - Set `PrimaryFluidType`
   - Adjust `LakeElevation` and `FluidCoverage`
   - Enable `HasFluidRivers` for volcanic layers
3. **Test in Editor**: Use World Layer Editor to visualize
4. **Adjust damage values** in FluidDefinition assets

---

## Developer Integration

### Adding Fluid to a Custom System

```csharp
// Check if entity is in fluid
if (SystemAPI.HasComponent<InFluidZone>(entity))
{
    var zone = SystemAPI.GetComponent<InFluidZone>(entity);
    if (zone.FluidType == (byte)FluidType.Lava)
    {
        // React to lava
    }
}
```

### Custom Fluid Type

1. Add to `FluidType` enum (max 255)
2. Create `FluidDefinition` asset
3. Initialize `FluidService` with definition
4. Handle in `FluidDamageSystem` if needed

---

## Acceptance Criteria

- [x] FluidDefinition ScriptableObject created
- [x] FluidType enum with Water, Oil, Lava, ToxicGas, Acid
- [x] FluidCell ECS component with Level, Pressure, Temperature
- [x] InFluidZone component for entity submersion tracking
- [x] FluidService converts to Burst-compatible data
- [x] FluidPlacementJob for hollow earth fluid generation
- [x] FluidSimulationJob for cellular automata flow
- [x] FluidDamageSystem applies damage per fluid type
- [x] HollowEarthProfile has PrimaryFluidType field
- [x] CaveGenerationService includes fluid parameters
- [x] Quick Setup creates sample fluid definitions
- [x] All generation Burst-compiled

---

## Performance Optimization Tasks

### Task 10.3.11: Fluid Simulation LOD
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: HIGH  
**Recommendation**: ⏸️ **DEFER** - Only freeze very distant fluid, may affect immersion

**Problem**: Fluid simulation runs for all chunks with fluid, regardless of distance.

**Solution**:
- Skip simulation for chunks beyond view distance
- Only simulate near-player or recently modified chunks
- Freeze distant fluid state

| Pros | Cons |
|------|------|
| ✅ Massive CPU savings | ⚠️ Distant fluid appears frozen |
| ✅ Scales with view distance | ⚠️ Resume lag when approaching |
| ✅ Simple distance check | |

---

### Task 10.3.12: Active Fluid Tracking
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Recommendation**: ✅ **IMPLEMENT** - Massive savings when fluid settled, no visual impact

**Implementation Notes**:
- Created `ChunkHasActiveFluid` component.
- `ChunkGenerationSystem` adds component to chunks with initial fluid.
- `FluidSimulationSystem` only iterates query `WithAll<ChunkHasActiveFluid>`.
- Added `UpdateFluidActivityJob` to track `TimeSinceLastChange`.
- Sleep threshold set to 5.0 seconds (removes component).

---

### Task 10.3.13: Cellular Automata Batching
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: MEDIUM  
**Recommendation**: ⏸️ **DEFER** - Marginal gains, profile first

**Problem**: Each fluid cell processed individually causes job overhead.

**Solution**:
- Process 2x2x2 or 4x4x4 cell blocks per iteration
- Reduce job scheduling overhead
- Better cache locality

| Pros | Cons |
|------|------|
| ✅ Better cache utilization | ⚠️ More complex iteration |
| ✅ Fewer job boundaries | ⚠️ Marginal gains in practice |
| ✅ SIMD-friendly | |

---

### Task 10.3.14: Fluid Level Quantization
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: LOW  
**Recommendation**: ⏸️ **DEFER** - Visible stepping degrades visual fidelity

**Problem**: Full 256-level precision used for all fluids.

**Solution**:
- Use coarser levels (16 or 32) for distant chunks
- Full precision only near player
- Faster convergence for distant simulation

| Pros | Cons |
|------|------|
| ✅ Faster convergence | ⚠️ Visible stepping at distance |
| ✅ Less memory per cell | ⚠️ Precision loss for physics |
| ✅ Good for visual-only fluid | |

---

## Optimization Summary & Recommendations

| Task | Name | Priority | Key Benefit | Key Risk | Keep? | Notes |
|------|------|----------|-------------|----------|-------|-------|
| **10.3.11** | Fluid Simulation LOD | HIGH | Skip distant sim | Distant fluid frozen | ✅ YES | Essential for performance |
| **10.3.12** | Active Fluid Tracking | HIGH | Only sim active fluid | State tracking overhead | ✅ YES | Major win when fluid settles |
| **10.3.13** | CA Batching | MEDIUM | Better cache locality | Code complexity | ⚠️ OPTIONAL | Measure first |
| **10.3.14** | Level Quantization | LOW | Faster convergence | Visual precision loss | ⚠️ OPTIONAL | Only if needed |

### Recommended Actions

1. **Implement 10.3.11** - Critical for worlds with large fluid bodies
2. **Implement 10.3.12** - Most fluid is static, huge savings
3. **Skip 10.3.13** - Only if profiling shows CA is bottleneck
4. **Skip 10.3.14** - Visual fidelity matters more than marginal speed

---

## Culling Optimization Tasks

### Task 10.3.15: Frustum Culling for Fluid Surfaces
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE (safe)

**Implementation Notes**:
- Verified `FluidMeshSystem` calls `mesh.RecalculateBounds()` after mesh generation.
- Uses `MeshRenderer` (Hybrid), so Unity automatically handles frustum culling.
- Bounds are tight to the generated mesh surface.

---

### Task 10.3.16: Hierarchical Chunk Culling for Fluids
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE (safe)

**Implementation Notes**:
- Implemented in `ChunkVisibilitySystem` (Task 10.2.16/10.2.17).
- Accesses `FluidMeshReference` on the parent chunk entity.
- Disables fluid `MeshRenderer` when the parent chunk is occluded/culled.
- Eliminates fluid rendering overhead for hidden caves.

---

### Task 10.3.17: Skip Simulation for Culled Chunks
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Visual Impact**: ✅ NONE (safe)

**Implementation Notes**:
- Created `ChunkVisibility` component to act as a bridge between Presentation (Visibility) and Simulation systems.
- `ChunkVisibilitySystem` updates this component based on flood fill results.
- `FluidSimulationSystem` skips processing for chunks where `IsVisible == false`.
- Ensures physics/simulation load is strictly limited to the player's potential view area.

---

### Task 10.3.18: Distance Fade for Fluid Particles
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: LOW  
**Visual Impact**: ⚠️ USE CAREFULLY

**Problem**: Fluid particle effects (splashes, bubbles) visible at extreme distance.

**Solution**:
- Fade particle alpha beyond 30m
- Disable particle spawning beyond 50m
- Keep surface mesh, just skip effects

**Mitigation**: Fluid surface still visible, only small particles fade

---

### Culling Summary

| Task | Type | Visual Impact | Implement? |
|------|------|---------------|------------|
| 10.3.15 | Frustum | ✅ None | ✅ YES |
| 10.3.16 | Hierarchical | ✅ None | ✅ YES |
| 10.3.17 | Skip Culled Sim | ✅ None | ✅ YES |
| 10.3.18 | Particle Fade | ⚠️ Careful | ⚠️ Optional |

