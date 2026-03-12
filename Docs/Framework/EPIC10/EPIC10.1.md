# EPIC 10.1: Geology & Resources

**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Dependencies**: EPIC 8.2 (Base Generation), EPIC 9.1 ✅ (Visual Refinement)  

> **Integration Note**: Use `VoxelVisualMaterial` from 9.1 for per-ore texturing. Each `OreDefinition` should link to a visual material for consistent appearance.

> **Layer System Note (EPIC 10.2)**: In the multi-layer world, each `WorldLayerDefinition` has its own `StrataProfile`. Strata apply within **solid layers only**. Hollow earth layers use `HollowEarthProfile` instead.

---

## Performance

| Operation | Budget | Approach |
|-----------|--------|----------|
| Strata Lookup | < 0.2ms | Burst, NativeArray lookup |
| Ore Placement | < 1.0ms | Burst, shared noise cache |
| Vein Warping | < 0.5ms | Domain warping (skipped for cache) |

---

## Editor Tools

### Strata Visualizer

**Menu**: `DIG → World → Strata Visualizer`

Visualizes geological layers:
- Depth-based material distribution
- Noise influence preview
- Layer blend zones

### Ore Distribution Viewer

**Menu**: `DIG → World → Ore Distribution`

Features:
- Probability graphs per depth
- Rarity tier visualization
- Spawn threshold markers

---

## Files Created

| File | Purpose |
|------|---------|
| `Geology/StrataProfile.cs` | Rock layer configuration |
| `Geology/OreDefinition.cs` | Ore spawn rules |
| `Geology/DepthValueCurve.cs` | Rarity distribution curves |
| `Geology/GeologyService.cs` | Burst-compatible lookup service |
| `Jobs/GenerateColumnDataJob.cs` | Strata generation job |
| `Jobs/GenerateOreNoiseJob.cs` | Shared noise cache job |
| `Editor/StrataVisualizer.cs` | Layer debug tool |
| `Editor/OreDistributionReview.cs` | Ore graph tool |

---

## Quick Start Guide

### Setting Up Geology (5 minutes)

1. **Create a Strata Profile**:
   - Right-click in Project → **Create → DIG → World → Strata Profile**
   - Configure rock layers by depth (Topsoil → Stone → Granite → Basalt)
   - Each layer defines: MaterialID, Depth Range, Blend Width, Noise Influence

2. **Create Ore Definitions**:
   - Right-click → **Create → DIG → World → Ore Definition**
   - Set: MaterialID, Depth Range, Rarity, Threshold, Vein Shape
   - Create one asset per ore type (Iron, Gold, Copper, etc.)

3. **Create World Generation Config**:
   - Right-click → **Create → DIG → World → World Generation Config**
   - Assign your Strata Profile and Ore Definitions
   - Save as `Resources/WorldGenerationConfig.asset`

4. **Enter Play Mode**: Chunks now generate with geological layers and ore veins!

### Visualizing Your Setup

- **DIG → World → Strata Visualizer**: See rock layers as a vertical cross-section
- **DIG → World → Ore Distribution**: View ore spawn probability graphs

---

## Component Reference

### StrataProfile (ScriptableObject)

Defines geological layers based on depth.

**Location**: `Assets/Scripts/Voxel/Geology/StrataProfile.cs`

| Field | Type | Description |
|-------|------|-------------|
| `Layers` | `Layer[]` | Array of rock layer definitions |
| `NoiseSeed` | `uint` | Seed for layer boundary noise |
| `NoiseScale` | `float` | Scale of boundary noise |

**Layer Struct**:
| Field | Description |
|-------|-------------|
| `MaterialID` | Voxel material ID from registry |
| `MinDepth` | Start depth (meters) |
| `MaxDepth` | End depth (meters) |
| `BlendWidth` | Transition zone width |
| `NoiseInfluence` | Boundary variation (0-1) |
| `DisplayName` | Editor display name |
| `DebugColor` | Visualization color |

### OreDefinition (ScriptableObject)

Defines ore vein spawn conditions and shape.

**Location**: `Assets/Scripts/Voxel/Geology/OreDefinition.cs`

| Field | Type | Description |
|-------|------|-------------|
| `MaterialID` | `byte` | Voxel material ID |
| `OreName` | `string` | Display name |
| `Rarity` | `OreRarity` | Common/Uncommon/Rare/Legendary |
| `MinDepth` | `float` | Minimum spawn depth |
| `MaxDepth` | `float` | Maximum spawn depth |
| `Threshold` | `float` | Noise threshold (0.7 = rare, 0.5 = common) |
| `NoiseScale` | `float` | Vein frequency (smaller = larger veins) |
| `DomainWarping` | `bool` | Enable organic vein shapes |
| `WarpStrength` | `float` | Warping intensity |
| `HostMaterials` | `byte[]` | Only spawn in these materials |

### DepthValueCurve (ScriptableObject)

Modifies ore spawn probability based on depth and rarity tier.

**Location**: `Assets/Scripts/Voxel/Geology/DepthValueCurve.cs`

| Field | Description |
|-------|-------------|
| `CommonOreCurve` | Peaks shallow, diminishes deep |
| `UncommonOreCurve` | Peaks at mid depths |
| `RareOreCurve` | Peaks deep |
| `LegendaryOreCurve` | Only spawns very deep |

### WorldGenerationConfig (ScriptableObject)

Master configuration linking all geology systems.

**Location**: `Assets/Scripts/Voxel/Core/WorldGenerationConfig.cs`

| Field | Description |
|-------|-------------|
| `GroundLevel` | Y coordinate of surface |
| `Seed` | World generation seed |
| `StrataProfile` | Rock layer profile |
| `DepthCurve` | Rarity probability curves |
| `OreDefinitions` | All ores that can spawn |
| `TerrainNoiseScale` | Surface variation frequency |
| `TerrainNoiseAmplitude` | Surface variation height |

---

## Setup Guide for Designers

## Designer Workflow

### 1. Create a Realistic Geology Profile

#### Step 1: Plan Your Layers

```
Surface (0-5m):    Topsoil/Dirt - Easy to dig
Shallow (5-50m):   Stone - Standard rock
Mid (50-150m):     Granite - Harder stone
Deep (150m+):      Basalt - Very hard
```

#### Step 2: Configure in StrataProfile

```yaml
Layer 1:
  MaterialID: 2 (Dirt)
  MinDepth: 0
  MaxDepth: 5
  BlendWidth: 2
  NoiseInfluence: 0.1

Layer 2:
  MaterialID: 1 (Stone)
  MinDepth: 5
  MaxDepth: 50
  BlendWidth: 3
  NoiseInfluence: 0.2
```

#### Step 3: Tune Blend Zones

- `BlendWidth: 0` = Sharp transition
- `BlendWidth: 5` = Gradual 5-meter blend
- `NoiseInfluence: 0.3` = ±3m boundary variation

### Designing Ore Distribution

#### Rarity Tiers

| Tier | Example Ores | Typical Depth | Threshold |
|------|--------------|---------------|-----------|
| Common | Coal, Iron | 10-80m | 0.55-0.65 |
| Uncommon | Copper, Tin | 30-120m | 0.65-0.75 |
| Rare | Gold, Silver | 60-180m | 0.75-0.85 |
| Legendary | Diamond, Mythril | 120m+ | 0.85-0.95 |

#### Vein Shape Tuning

- `NoiseScale: 0.05` = Large, rare deposits
- `NoiseScale: 0.15` = Small, frequent pockets
- `DomainWarping: true` = Organic, twisted shapes
- `WarpStrength: 5` = Moderate twisting

---

## Developer Integration

### Accessing Geology at Runtime

```csharp
// In a Burst job (via GeologyService):
byte material = GeologyLookup.GetStrataMaterial(
    depth, 
    worldPos, 
    in GeologyService.StrataLayers, 
    GeologyService.GetSeed()
);

byte ore = GeologyLookup.GetOreMaterial(
    worldPos, 
    depth, 
    hostMaterial, 
    in GeologyService.Ores, 
    GeologyService.GetSeed()
);
```

### Using StrataProfile from MonoBehaviour

```csharp
public class GeologyTest : MonoBehaviour
{
    public StrataProfile profile;
    
    void Test()
    {
        float depth = 75f;
        float3 pos = transform.position;
        
        byte materialId = profile.GetMaterialAtDepth(depth, pos);
        Debug.Log($"At {depth}m: Material {materialId}");
    }
}
```

### Custom Ore Logic

```csharp
public class CustomOreChecker : MonoBehaviour
{
    public OreDefinition goldOre;
    
    bool HasGold(float3 worldPos, float depth)
    {
        byte hostMaterial = 1; // Stone
        return goldOre.ShouldSpawnAt(worldPos, depth, hostMaterial, 12345);
    }
}
```

### Extending the System

To add a new ore type:
1. Create `OreDefinition` asset
2. Assign unique `MaterialID` (register in `VoxelMaterialRegistry`)
3. Add to `WorldGenerationConfig.OreDefinitions`
4. Create visual material in `VoxelVisualMaterial`

---

## Editor Tools

### World Generation Config Editor

**Menu**: DIG → World → Create World Generation Config

Custom inspector with:
- Quick Setup wizard for creating required assets
- One-click ore definition creation
- Auto-find all ores in project
- Validation panel with error checking
- Direct access to visualizer tools

### Strata Visualizer

**Menu**: DIG → World → Strata Visualizer

Features:
- Visual cross-section of rock layers
- Depth tester with noise preview
- Layer configuration table
- Direct link to edit profile

### Ore Distribution Viewer

**Menu**: DIG → World → Ore Distribution

Features:
- Probability graph for all ores
- Depth range visualization
- Quick access to ore definitions
- Create new ore button
- Legend with ore details

---

## Technical Architecture

### Generation Flow

```
ChunkGenerationSystem.OnCreate()
    ↓
LoadConfig() → Resources.Load<WorldGenerationConfig>
    ↓
InitializeGeologyService() → Creates NativeArrays for Burst
    ↓
ScheduleGeneration() per chunk
    ↓
GenerateVoxelDataJob.Execute()
    ├── GetTerrainNoise() → Surface height
    ├── GetMaterialWithGeology()
    │   ├── GeologyLookup.GetStrataMaterial() → Base rock
    │   └── GeologyLookup.GetOreMaterial() → Ore replacement
    └── Write to Densities/Materials arrays
```

### Burst Compatibility

The geology system uses a two-layer architecture:
1. **Managed Layer**: `StrataProfile`, `OreDefinition` (ScriptableObjects)
2. **Unmanaged Layer**: `GeologyService.StrataLayerData`, `GeologyService.OreData` (blittable structs in NativeArrays)

`GeologyService.Initialize()` converts managed data to blittable format for Burst jobs.

---

## Acceptance Criteria

- [x] Rock layers change with depth (StrataProfile)
- [x] Ore veins are coherent 3D shapes (domain warping + 3D noise)
- [x] Rarer ores spawn deeper (OreRarity + DepthValueCurve)
- [x] Strata visualizer shows layer distribution
- [x] Ore distribution viewer shows spawn probability
- [x] All configurable via ScriptableObjects
- [x] Burst-compatible generation jobs
- [x] WorldGenerationConfig aggregates all settings

---

## Performance Optimization Tasks

### Task 10.1.11: Ore Noise Caching
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Recommendation**: ✅ **IMPLEMENT** - Pure performance gain, no visual impact

**Implementation Notes**:
- Created `GenerateOreNoiseJob` to cache standard 3D noise (scale 0.05).
- Integrated into `ChunkGenerationSystem` pre-pass.
- Updated `GeologyLookup` to use cached noise instead of recalculating simplex noise per ore.
- Ignore domain warping for cached ores to save performance.

**Problem**: Ore noise is recalculated for each voxel, even when multiple ores share the same noise pattern.

**Solution**:
- Cache 3D noise values per chunk in shared NativeArray
- Reuse noise values across all ore types
- Reduce noise evaluations from `voxels × ore_types` to `voxels`

| Pros | Cons |
|------|------|
| ✅ Major reduction in noise calls | ⚠️ Memory for cache (~128KB/chunk) |
| ✅ Scales with ore count | ⚠️ Cache invalidation on config change |
| ✅ Simple implementation | |

---

### Task 10.1.12: Strata Lookup Optimization
**Status**: ⬜ NOT STARTED  
**Priority**: LOW  
**Recommendation**: ⏸️ **DEFER** - Only useful if 20+ strata layers

**Problem**: Strata lookup iterates through all layers to find the correct one for each voxel Y.

**Solution**:
- Pre-compute strata boundaries as Y-indexed array
- Direct array access: `strataByY[clampedY]`
- O(1) lookup instead of O(layers)

| Pros | Cons |
|------|------|
| ✅ O(1) vs O(layers) lookup | ⚠️ Setup cost on load |
| ✅ Cache-friendly access | ⚠️ Only useful if many strata layers |
| ✅ Burst-compatible | |

---

### Task 10.1.13: Burst Ore Threshold Tables
**Status**: ⬜ NOT STARTED  
**Priority**: LOW  
**Recommendation**: ⏸️ **DEFER** - Marginal gains, profile first

**Problem**: Ore spawn probability uses per-voxel calculations.

**Solution**:
- Pre-compute ore thresholds per depth in NativeArray
- Use depth-indexed lookup: `thresholds[depth][oreID]`
- Reduces per-voxel math

| Pros | Cons |
|------|------|
| ✅ Fewer calculations per voxel | ⚠️ Memory for threshold tables |
| ✅ Consistent behavior | ⚠️ Marginal gains in practice |
| ✅ Easy to profile | |

---

## Optimization Summary & Recommendations

| Task | Name | Priority | Key Benefit | Key Risk | Keep? | Notes |
|------|------|----------|-------------|----------|-------|-------|
| **10.1.11** | Ore Noise Caching | MEDIUM | Reduce noise calls | Memory for cache | ✅ YES | Worth it if many ore types |
| **10.1.12** | Strata Lookup | LOW | O(1) vs O(layers) | Extra setup | ⚠️ OPTIONAL | Only if >10 strata layers |
| **10.1.13** | Ore Threshold Tables | LOW | Less per-voxel math | Memory for tables | ⚠️ OPTIONAL | Marginal gains |

### Recommended Actions

1. **Implement 10.1.11** - If ore noise is a measured bottleneck
2. **Skip 10.1.12** - Strata layers are few (<10), linear scan is fine
3. **Skip 10.1.13** - Marginal gains, adds complexity

---

## Culling Optimization Tasks

### Task 10.1.14: Frustum Culling for Ore Indicators
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE (safe)

**Implementation Notes**:
- Verified `DecoratorInstancingSystem` implements frustum plane extraction and bounds testing.
- Optimized `DrawMeshInstanced` loop to use reusable `_drawBuffer` (Matrix4x4[]) instead of allocating arrays per frame.
- Ensures zero GC allocation during culling of ore indicators/decorators.

**Problem**: Ore sparkle/glow effects render even when offscreen.

**Solution**:
- Enable frustum culling on ore indicator renderers
- Unity handles automatically for most renderers
- Ensure ore particle systems have proper bounds

---

### Task 10.1.15: Hierarchical Chunk Culling
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE (safe)

**Implementation Notes**:
- Added `CheckChunkFrustum` using `GeometryUtility.TestPlanesAABB` on the entire chunk bounds.
- Implemented `_chunkFrustumCache` (cleared per frame) to ensure each chunk is only tested once, even if it contains 1000s of instances.
- Skips all decorator instances for chunks that are offscreen.

**Problem**: Per-object culling checks are expensive when many ore deposits exist.

**Solution**:
- Cull entire chunks before individual ore checks
- If chunk not visible, skip all ore processing for that chunk
- Integrates with voxel chunk visibility system

---

### Task 10.1.16: Distance Fade for Ore Effects
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: MEDIUM  
**Visual Impact**: ⚠️ USE CAREFULLY

**Problem**: Ore sparkle effects visible at extreme distances waste GPU.

**Solution**:
- Fade ore particle alpha to 0 beyond 50m
- Disable ore glow beyond 100m
- Use fog to hide transition naturally

**Mitigation**: Fade smoothly over 10-20m, never instant pop

---

### Culling Summary

| Task | Type | Visual Impact | Implement? |
|------|------|---------------|------------|
| 10.1.14 | Frustum | ✅ None | ✅ YES |
| 10.1.15 | Hierarchical | ✅ None | ✅ YES |
| 10.1.16 | Distance Fade | ⚠️ Careful | ⚠️ With fade |

