# EPIC 10.2: Cave & Hollow Earth Systems

**Status**: ✅ COMPLETE  
**Priority**: CRITICAL  
**Dependencies**: EPIC 10.1 (Geology & Resources)  
**Estimated Time**: 1 week

---

## Designer Workflow

### 1. Setting Up Caves and Hollow Earth (5 minutes)

1.  **Create Assets Using Quick Setup**:
    *   Go to **DIG → Quick Setup → Generation → Create Complete Cave Setup**
    *   This creates:
        *   3 Cave Profiles (Starter, Standard, Deep)
        *   4 Hollow Earth Profiles (Mushroom, Crystal, Volcanic, Core)
        *   8 World Layer Definitions (alternating solid/hollow)
        *   1 WorldStructureConfig (automatically copied to Resources)

2.  **Enter Play Mode**: Chunks now generate with caves and hollow earth layers!

3.  **Verify Setup**:
    *   Check console for: `[ChunkGeneration] Loaded WorldStructureConfig: 8 layers...`
    *   Use **DIG → World → World Layer Editor** to visualize the structure

### Manual Setup (If Not Using Quick Setup)

1.  **Create Cave Profile**:
    *   Right-click in Project → **Create → DIG → World → Cave Profile**
    *   Configure swiss cheese, spaghetti, noodle, and cavern settings

2.  **Create Hollow Earth Profile**:
    *   Right-click → **Create → DIG → World → Hollow Earth Profile**
    *   Set height (500m-1500m), floor area, and features

3.  **Create World Layer Definitions**:
    *   Right-click → **Create → DIG → World → World Layer Definition**
    *   Set depth range, type (Solid/Hollow), and assign profiles

4.  **Create World Structure Config**:
    *   Right-click → **Create → DIG → World → World Structure Config**
    *   Add all layer definitions in depth order
    *   **Save as `Resources/WorldStructureConfig.asset`**

---

## Component Reference

### WorldStructureConfig (ScriptableObject)

Master configuration for the entire multi-layer world.

**Location**: `Assets/Scripts/Voxel/Geology/WorldStructureConfig.cs`

| Field | Type | Description |
|-------|------|-------------|
| `Layers` | `WorldLayerDefinition[]` | All layers from surface to core |
| `WorldSeed` | `uint` | Global generation seed |
| `GroundLevel` | `float` | Y coordinate of ground (usually 0) |
| `LayersAboveToLoad` | `int` | Streaming: layers above player to keep loaded |
| `LayersBelowToLoad` | `int` | Streaming: layers below player to keep loaded |
| `HorizontalViewDistance` | `float` | Chunk loading distance |

**Key Methods**:
*   `GetLayerAtDepth(float worldY)` - Returns layer at Y position
*   `GetTotalDepth()` - Total world depth in meters
*   `HollowLayerCount` / `SolidLayerCount` - Layer counts

---

### WorldLayerDefinition (ScriptableObject)

Defines a single layer in the world (solid or hollow).

**Location**: `Assets/Scripts/Voxel/Geology/WorldLayerDefinition.cs`

| Field | Type | Description |
|-------|------|-------------|
| `LayerName` | `string` | Display name for this layer |
| `LayerIndex` | `int` | Order in world structure (0 = top) |
| `Type` | `LayerType` | Solid, Hollow, or Transition |
| `TopDepth` | `float` | Y where layer starts (e.g., -400) |
| `BottomDepth` | `float` | Y where layer ends (e.g., -900) |
| `AreaWidth` | `float` | Horizontal extent (meters) |
| `AreaLength` | `float` | Horizontal extent (meters) |
| `StrataProfile` | `StrataProfile` | Rock layers (solid only) |
| `CaveProfile` | `CaveProfile` | Cave settings (solid only) |
| `HollowProfile` | `HollowEarthProfile` | Hollow settings (hollow only) |
| `TargetPlaytimeMinutes` | `float` | Expected exploration time |
| `DifficultyMultiplier` | `float` | Danger level (1.0 = normal) |

**Properties**:
*   `Thickness` - Layer height in meters
*   `AreaKm2` - Floor area in square kilometers
*   `ContainsDepth(float worldY)` - Check if Y is within layer

---

### CaveProfile (ScriptableObject)

Configures cave generation for solid layers.

**Location**: `Assets/Scripts/Voxel/Geology/CaveProfile.cs`

| Cave Type | Description | Key Settings |
|-----------|-------------|--------------|
| **Swiss Cheese** | Small random air pockets | `CheeseScale`, `CheeseThreshold` |
| **Spaghetti** | Winding narrow tunnels | `SpaghettiScale`, `SpaghettiWidth` |
| **Noodle** | Larger traversal tunnels | `NoodleScale`, `NoodleWidth` |
| **Caverns** | Large open spaces | `CavernScale`, `CavernThreshold` |
| **Vertical Shafts** | Drops and climbs | `ShaftFrequency`, `ShaftRadius` |

Each cave type has:
*   `Enable*` - Toggle on/off
*   `*Scale` - Noise scale (larger = bigger features)
*   `*MinDepth` / `*MaxDepth` - Depth range where it spawns

---

### HollowEarthProfile (ScriptableObject)

Configures a hollow earth layer - a massive underground biome.

**Location**: `Assets/Scripts/Voxel/Geology/HollowEarthProfile.cs`

| Category | Fields | Notes |
|----------|--------|-------|
| **Dimensions** | `AverageHeight` (100-2000m), `HeightVariation`, `FloorWidth`, `FloorLength` | Designer-configurable |
| **Ceiling** | `CeilingNoiseScale`, `HasStalactites`, `StalactiteDensity`, `MaxStalactiteLength` | Hanging formations |
| **Floor** | `FloorNoiseScale`, `FloorAmplitude`, `HasStalagmites`, `FloorMaterialID` | Terrain variation |
| **Pillars** | `GeneratePillars`, `PillarFrequency`, `MinPillarRadius`, `MaxPillarRadius` | Structural support |
| **Lighting** | `LightSource`, `AmbientColor`, `AmbientIntensity` | Bioluminescence, Crystal, Lava, etc. |
| **Features** | `HasUndergroundLakes`, `HasCrystalFormations`, `HasLavaFlows`, `HasFloatingIslands` | Biome-specific |

---

### CaveGenerationService (Static Service)

Converts ScriptableObjects to Burst-compatible NativeArrays.

**Location**: `Assets/Scripts/Voxel/Geology/CaveGenerationService.cs`

```csharp
// Initialize from WorldStructureConfig
CaveGenerationService.Initialize(worldStructureConfig);

// Access data in Burst jobs
NativeArray<LayerData> layers = CaveGenerationService.Layers;
NativeArray<CaveParams> caves = CaveGenerationService.CaveParamsArray;
NativeArray<HollowParams> hollows = CaveGenerationService.HollowParamsArray;

// Cleanup
CaveGenerationService.Dispose();
```

**Burst-Compatible Structs**:
*   `LayerData` - Layer boundaries and profile indices
*   `CaveParams` - Cave generation parameters
*   `HollowParams` - Hollow earth dimensions and features

---

### CaveLookup (Static, Burst-Compiled)

Static methods for cave/hollow generation in Burst jobs.

**Location**: `Assets/Scripts/Voxel/Geology/CaveGenerationService.cs`

```csharp
// Check if position is cave air
bool isCave = CaveLookup.IsCaveAir(worldPos, depth, caveParams, seed);

// Get hollow earth density (negative = air, positive = solid)
float density = CaveLookup.GetHollowDensity(worldPos, hollowParams, seed, out byte material);
```

---

## Architecture

### Generation Pipeline

```
ChunkGenerationSystem
    │
    ├─ Load WorldStructureConfig from Resources
    ├─ Initialize CaveGenerationService with layer data
    │
    └─ Schedule GenerateVoxelDataJob for each chunk
        │
        ├─ Determine layer at chunk Y position
        │
        ├─ If HOLLOW layer:
        │   └─ CaveLookup.GetHollowDensity()
        │       ├─ Floor terrain (noise)
        │       ├─ Ceiling terrain (noise)
        │       ├─ Pillars (grid-based)
        │       └─ Stalactites (noise)
        │
        └─ If SOLID layer:
            ├─ Standard terrain generation
            └─ CaveLookup.IsCaveAir()
                ├─ Swiss Cheese (3D noise)
                ├─ Spaghetti (2-axis noise)
                ├─ Noodle (2-axis noise, larger)
                └─ Caverns (3D noise, depth-scaled)
```

### Data Flow

```
ScriptableObjects                    NativeArrays (Burst-compatible)
┌─────────────────────┐             ┌─────────────────────────────┐
│ WorldStructureConfig │     →      │ CaveGenerationService       │
│ WorldLayerDefinition │  Initialize │   .Layers (LayerData[])     │
│ CaveProfile          │     →      │   .CaveParamsArray          │
│ HollowEarthProfile   │            │   .HollowParamsArray        │
└─────────────────────┘             └─────────────────────────────┘
                                              │
                                              ↓
                                    ┌─────────────────────────────┐
                                    │ GenerateVoxelDataJob        │
                                    │   [BurstCompile]            │
                                    │   IJobParallelFor           │
                                    └─────────────────────────────┘
```

---

## Editor Tools

### World Layer Editor

**Menu**: `DIG → World → World Layer Editor`

Visual cross-section of the layer structure:
*   Color-coded solid vs hollow layers
*   Depth ruler
*   One-click profile editing

### Quick Setup

**Menu**: `DIG → Quick Setup → Generation`

Automated asset creation:
*   Generates 8-layer world structure
*   Creates all necessary profiles
*   Configures default biomes

---

## Files Created

| File | Purpose |
|------|---------|
| `Geology/WorldLayerDefinition.cs` | Per-layer configuration |
| `Geology/HollowEarthProfile.cs` | Hollow earth settings |
| `Geology/CaveProfile.cs` | Cave generation settings |
| `Geology/WorldStructureConfig.cs` | Master config |
| `Geology/CaveGenerationService.cs` | Burst-compatible data |
| `Jobs/GenerateVoxelDataJob.cs` | Caves/hollow support |
| `Systems/Generation/ChunkGenerationSystem.cs` | Config loading |
| `Editor/CaveQuickSetup.cs` | Quick setup menu |
| `Editor/WorldLayerEditor.cs` | Visual layer editor |

---

## Default World Structure

| Layer | Type | Depth | Height | Playtime | Biome |
|-------|------|-------|--------|----------|-------|
| Entry Caves | Solid | 0 to -400m | 400m | 45 min | - |
| Mushroom Forest | Hollow | -400 to -900m | 500m | 45 min | Bioluminescent |
| Deep Mines | Solid | -900 to -1300m | 400m | 30 min | - |
| Crystal Cavern | Hollow | -1300 to -2100m | 800m | 60 min | Crystal Light |
| Abyssal Tunnels | Solid | -2100 to -2500m | 400m | 30 min | - |
| Volcanic Depths | Hollow | -2500 to -3500m | 1000m | 60 min | Lava Glow |
| Core Approach | Solid | -3500 to -4000m | 500m | 30 min | - |
| Ancient Core | Hollow | -4000 to -5500m | 1500m | 90 min | Artificial Sun |

**Total**: 5,500m depth, ~6.5 hours gameplay

---

## Performance

| Operation | Budget | Approach |
|-----------|--------|----------|
| Cave Detection | < 1.0ms | Burst, 3D Noise lookup |
| Hollow Density | < 1.5ms | Burst, branchless math |
| Layer Streaming | < 2.0ms | Vertical distance check |
| Memory Per Chunk | < 100KB | NativeArrays, no objects |

### Burst Compilation

All generation code is Burst-compiled:
*   `GenerateVoxelDataJob` - Main generation job
*   `CaveLookup.IsCaveAir()` - Cave detection
*   `CaveLookup.GetHollowDensity()` - Hollow earth generation

### Memory

| Data | Allocation | Lifetime |
|------|------------|----------|
| `CaveGenerationService` arrays | Persistent | Scene lifetime |
| Chunk density/material arrays | Persistent | Job completion |

---

## Developer Integration

### For Developers

1.  **Access Current Layer**:
    ```csharp
    var config = Resources.Load<WorldStructureConfig>("WorldStructureConfig");
    var layer = config.GetLayerAtDepth(playerPosition.y);
    ```

2.  **Check Layer Type**:
    ```csharp
    if (layer.Type == LayerType.Hollow)
    {
        // Player is in hollow earth
        ApplyHollowEarthEffects(layer.HollowProfile);
    }
    ```

3.  **Custom Generation**:
    The system is extensible. Add new cave types by:
    *   Adding fields to `CaveProfile`
    *   Adding to `CaveParams` struct
    *   Adding generation logic to `CaveLookup.IsCaveAir()`

### For Designers

1.  **Adjusting Cave Density**:
    *   Lower `CheeseThreshold` = more caves
    *   Higher `SpaghettiWidth` = wider tunnels

2.  **Hollow Earth Feel**:
    *   `AverageHeight` > 500m for "underground world" feel
    *   Enable `GeneratePillars` for structural interest
    *   `StalactiteDensity` 0.2-0.4 for natural look

3.  **Difficulty Progression**:
    *   Use `DifficultyMultiplier` for enemy/hazard scaling
    *   Deeper = more cave types enabled
    *   Volcanic layers = HasLavaFlows

---

---

## Acceptance Criteria

*   [x] WorldLayerDefinition ScriptableObject created
*   [x] HollowEarthProfile ScriptableObject created
*   [x] CaveProfile ScriptableObject created
*   [x] WorldStructureConfig links all layers
*   [x] CaveGenerationService converts to Burst-compatible data
*   [x] GenerateVoxelDataJob supports caves and hollow earth
*   [x] ChunkGenerationSystem loads WorldStructureConfig
*   [x] Quick Setup creates sample configuration
*   [x] World Layer Editor visualizes structure
*   [x] All generation Burst-compiled

---

## Performance Optimization Tasks

### Task 10.2.11: Cave Noise LOD
**Status**: ⬜ NOT STARTED  
**Priority**: HIGH  
**Recommendation**: ⚠️ **CAREFUL** - Use far thresholds + fog to hide LOD transitions

**Problem**: Full cave noise evaluation for all chunks regardless of distance.

**Solution**:
- Use fewer octaves for distant chunks
- Full detail only within player view distance
- 2-4x noise speedup for distant chunks

| Pros | Cons |
|------|------|
| ✅ 2-4x faster for distant chunks | ⚠️ Less accurate cave shapes at distance |
| ✅ Scales with view distance | ⚠️ LOD transition may be visible |
| ✅ Drop-in implementation | |

---

### Task 10.2.12: Hollow Layer Early-Out
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Recommendation**: ✅ **IMPLEMENT** - Pure win, no visual impact

**Implementation Notes**:
- Added pre-job check in `ChunkGenerationSystem` to intersect chunk bounds with `WorldLayers`.
- If chunk does not overlap any layer with `CaveParamsIndex >= 0` or `HollowParamsIndex >= 0`, `UseCaves` is set to false.
- Skips all cave/hollow logic for solid crust/core chunks.

**Problem**: Cave calculations run even for chunks entirely inside hollow layers.

**Solution**:
- Check if chunk Y is fully within hollow layer
- Skip all cave noise for hollow chunks
- Only carve edges where hollow meets solid

| Pros | Cons |
|------|------|
| ✅ Skip 100% of cave work for hollow | ⚠️ Edge detection needed |
| ✅ Pure win for hollow layers | ⚠️ Only helps in hollow earth zones |
| ✅ Simple bounds check | |

---

### Task 10.2.13: Chunk Density Histogram
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Recommendation**: ✅ **IMPLEMENT** - Enables other optimizations, no visual impact

**Implementation Notes**:
- Added `ChunkDensityStats` component (Solid/Air/Surface counts).
- Added `CalculateChunkStatsJob` which runs in parallel after generation.
- Integrated into `ChunkGenerationSystem`.
- Enables early-outs for Decorators and Physics (Empty chunks can skip).

**Problem**: No quick way to know if chunk has caves without full scan.

**Solution**:
- Calculate solid/air ratio during generation
- Store in `ChunkDensityStats` component
- Use for decorator early-out, physics optimization

| Pros | Cons |
|------|------|
| ✅ Enables multi-system optimizations | ⚠️ Extra component per chunk |
| ✅ One-time calculation | ⚠️ Must update on voxel modification |
| ✅ Helps decorators, physics, AI | |

---

### Task 10.2.14: Layer Boundary Caching
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: LOW  
**Recommendation**: ⏸️ **DEFER** - Only useful if 20+ world layers

**Problem**: Layer lookup scans all layers to find depth match.

**Solution**:
- Pre-compute boundaries in sorted NativeArray
- Binary search for O(log n) lookup
- Or quantized lookup table for O(1)

| Pros | Cons |
|------|------|
| ✅ O(log n) or O(1) lookup | ⚠️ Setup cost |
| ✅ Burst-compatible | ⚠️ Only useful if 20+ layers |
| ✅ Cache-friendly | |

---

## Optimization Summary & Recommendations

| Task | Name | Priority | Key Benefit | Key Risk | Keep? | Notes |
|------|------|----------|-------------|----------|-------|-------|
| **10.2.11** | Cave Noise LOD | HIGH | 2-4x noise speedup | Visual quality at distance | ✅ YES | Major win for deep worlds |
| **10.2.12** | Hollow Layer Early-Out | MEDIUM | Skip cave noise entirely | Edge detection needed | ✅ YES | Pure win for hollow layers |
| **10.2.13** | Chunk Density Histogram | MEDIUM | Multi-system benefit | Extra component | ✅ YES | Helps decorators, physics, AI |
| **10.2.14** | Layer Boundary Caching | LOW | O(log n) lookup | Memory for table | ⚠️ OPTIONAL | Only if 20+ layers |

### Recommended Actions

1. **Implement 10.2.11** - Major performance win for large view distances
2. **Implement 10.2.12** - Easy win, hollow layers are common
3. **Implement 10.2.13** - Enables other systems to early-out
4. **Skip 10.2.14** - Layer count is small, linear scan is fine

---

## Culling Optimization Tasks

### Task 10.2.15: Frustum Culling for Cave Meshes
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE (safe)

**Implementation Notes**:
- Verified `ChunkMeshingSystem` calls `mesh.RecalculateBounds()` which ensures precise bounds for culling.
- Verified usage of `MeshRenderer` (Hybrid), which benefits from Unity's automatic frustum culling.
- Default submesh bounds are also set to chunk size as fallback, but recalculation tightens them.

---

### Task 10.2.16: Occlusion Culling for Cave Chambers
**Status**: ⬜ NOT STARTED  
### Task 10.2.16: Occlusion Culling for Cave Chambers
**Status**: ✅ COMPLETE  
**Priority**: **CRITICAL**  
**Visual Impact**: ✅ NONE (safe)

**Implementation Notes**:
- Created `ChunkVisibilitySystem` (Presentation System).
- Implemented BFS Flood Fill from camera chunk to determine reachable/visible chunks.
- Uses `ChunkDensityStats.IsFull` to determine occlusion (Solid chunks block view).
- Updates `MeshRenderer.enabled` for chunks and notifies `DecoratorInstancingSystem`.
- Runs every 5 frames for performance.

---

### Task 10.2.17: Hierarchical Cave Branch Culling
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE (safe)

**Implementation Notes**:
- Integrated hierarchical culling into `ChunkVisibilitySystem`.
- Since chunks act as the parent for other features (Fluids, Decorators), occluding the chunk now automatically culls all children.
- Specifically added support for culling `FluidMeshReference` entities when the parent chunk is occluded.

---

### Task 10.2.18: LOD with Smooth Crossfade for Cave Meshes
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: MEDIUM  
**Visual Impact**: ⚠️ USE CAREFULLY

**Problem**: Distant cave walls use full-detail meshes.

**Solution**:
- Create 2-3 LOD levels per cave chunk mesh
- Use dithered crossfade (not instant swap)
- LOD transition distances: 50m, 100m, 200m

**Mitigation**: Dither/dissolve transition over 5-10m, use lighting to hide

---

### Culling Summary

| Task | Type | Visual Impact | Implement? |
|------|------|---------------|------------|
| 10.2.15 | Frustum | ✅ None | ✅ YES |
| 10.2.16 | Occlusion | ✅ None | ✅ **CRITICAL** |
| 10.2.17 | Hierarchical | ✅ None | ✅ YES |
| 10.2.18 | LOD Crossfade | ⚠️ Careful | ⚠️ With dither |

