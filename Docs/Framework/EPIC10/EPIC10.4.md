# EPIC 10.4: Biome System

**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Dependencies**: EPIC 10.2 (Cave & Hollow Earth Systems)  

---

## Performance

| Operation | Budget | Approach |
|-----------|--------|----------|
| Biome Lookup | < 0.5ms | Burst, cached noise |
| Temp/Humidity Noise | < 1.0ms | Burst, 2D Simplex |
| Material Blending | < 0.5ms | Linear interpolation |

---

## Editor Tools

### Biome Map Viewer

**Menu**: `DIG → World → Biome Map Viewer`

Visualizes biome distribution:
- Temperature vs Humidity graph
- Color-coded regions
- Mouse-over inspection of biome types

---

## Files Created

| File | Purpose |
|------|---------|
| `Biomes/BiomeDefinition.cs` | Biome configuration SO |
| `Biomes/BiomeRegistry.cs` | Central biome catalog |
| `Biomes/BiomeService.cs` | Burst-compatible lookup |
| `Components/ChunkBiome.cs` | Caching component |
| `Jobs/GenerateVoxelDataJob.cs` | Biome integration |
| `Editor/BiomeMapViewer.cs` | Visual debug tool |
| `Editor/BiomeQuickSetup.cs` | Setup tool |

---

## Goal

Create a **layered biome system** that varies terrain within:
- **Solid layers** - Horizontal variation based on temperature/humidity noise (e.g., Tundra vs Desert underground strata).
- **Hollow earth layers** - Distinct underground biomes (Forest, Crystal, Volcanic) assigned per layer.
- **Integration** - Seamlessly integrates with Voxel Generation Jobs using Burst-compatible lookups.

---

## Designer Workflow

### 1. Create Biomes
1. Go to `DIG > Quick Setup > Generation > Create Biomes`.
2. This generates:
   - `Assets/Resources/Biomes/BiomeRegistry.asset`
   - Sample Hollow Biomes (Mushroom, Crystal, Magma)
   - Sample Solid Biomes (Tundra, Desert, Jungle, Taiga)

### 2. Configure Hollow Earth
1. Open your `HollowEarthProfile` (or create a new one).
2. Assign a **BiomeType** (e.g., `Hollow_Crystal`).
3. The biome's materials and fluid overrides will now be used for that layer.

### 3. Visualize Distribution
1. Open `DIG > World > Biome Map Viewer`.
2. Assign the `BiomeRegistry`.
3. Click **Generate Map** to see how solid layer biomes are distributed by Temperature (X) and Humidity (Y).

---

## Manual Setup Guide

If you prefer to create biomes from scratch without Quick Setup:

### 1. Create a Biome Definition
1. Right-click in Project view: `Create > DIG > World > Biome Definition`.
2. **Identity**: Give it a unique ID (1-255) and name.
3. **Materials**: Drag in `VoxelMaterialDefinition` assets for Surface, Subsurface, and Wall.
4. **Conditions** (for Solid Layers):
   - Set **Min/Max Temperature** (-1 to 1).
   - Set **Min/Max Humidity** (-1 to 1).
   - *Example*: Desert = High Temp (0.5 to 1), Low Humidity (-1 to -0.5).
5. **Hollow Earth**: If this is for hollow layers, conditions are ignored.

### 2. Register the Biome
1. Locate your `BiomeRegistry` asset (or create one).
2. Add your new `BiomeDefinition` to the **Biomes** list.
3. Alternatively, select the Registry and click the "Auto Populate" button (if available via inspector context) or manually add it.
   - *Note*: The `BiomeService` initializes from this registry at runtime.

### 3. Assign to World
- **Solid Layers**: Automatically appear based on their Temperature/Humidity conditions during generation.
- **Hollow Layers**: Open a `HollowEarthProfile` and assign the asset to the **Biome Type** slot.

---

## Component Reference

### Scriptable Objects

#### `BiomeDefinition`
Defines a single biome.
- **Identity**: ID, Name, Debug Color.
- **Conditions**: Min/Max Temperature & Humidity (for horizontal placement in solid layers).
- **Materials**: Surface, Subsurface, Wall material overrides.
- **Environment**: Ore multipliers, Fluid overrides, Fog/Audio settings.

#### `BiomeRegistry`
Central catalog of all biomes.
- **AutoPopulate**: Finds all `BiomeDefinition` assets in project.
- **Global Settings**: Noise scale for biome distribution.

### ECS Services

#### `BiomeService`
Static class managing Burst-compatible data.
- **Initialize(registry)**: Converts SOs to `NativeArray<BiomeParams>`.
- **AllBiomes**: Full lookup array.
- **SolidLayerBiomes**: List of biomes valid for solid layer generation.
- **Burst Functions**: `BiomeLookup.GetBiomeAt(pos, ...)`

---

## Developer Integration

### Integration with Generation
The biome system is integrated into `GenerateVoxelDataJob`.

1. **Initialization**:
   - `ChunkGenerationSystem` loads `BiomeRegistry` from `Resources`.
   - Initializes `BiomeService`.

2. **Job Execution**:
   - `GenerateVoxelDataJob` receives `SolidLayerBiomes` array.
   - For **Solid Layers**: Calculates Temperature/Humidity noise at voxel coordinate. Finds best matching biome from `SolidLayerBiomes`.
   - For **Hollow Layers**: Reads pre-determined Biome materials from `HollowParams` (baked by `CaveGenerationService`).

### Adding New Biomes
```csharp
var biome = ScriptableObject.CreateInstance<BiomeDefinition>();
biome.BiomeName = "My Custom Biome";
biome.MinTemperature = 0.5f; // Hot
biome.MaxTemperature = 1.0f;
biome.SurfaceMaterial = mySandMaterial;
```

---

## Expanded Biome Repository

*Note: While 10 variants exist for each layer type, a typical game session will procedurally select ~5 of each to form the world stack, ensuring variety across playthroughs.*

### Hollow Earth Biomes (10 Variants)

Massive open underground worlds (500m+ height).

| Biome Name | Theme | Primary Material | Features | Hazards |
|------------|-------|------------------|----------|---------|
| **Mushroom Forest** | Bioluminescent Nature | Mycelium / Dirt | Giant glowing mushrooms, spores | Poison Spores |
| **Crystal Cavern** | Geode Interior | Crystal Stone | Massive crystal pillars, reflective floors | Sharp Crystals |
| **Underground Ocean** | Subterranean Sea | Sand / Coral | Massive water body, islands, columns | Drowning, Sea Monsters |
| **Volcanic Depths** | Magma Chamber | Basalt / Obsidian | Lava rivers, ash rain, vents | Lava, Heat Damage |
| **Frozen Expanse** | Sub-zero Cavern | Ice / Packed Snow | Massive icicles, slippery terrain | Freezing, Slipping |
| **Ancient Realm** | Lost Civilization | Carved Stone | Procedural ruins, pillars, roads | Traps, Guardians |
| **Petrified Jungle** | Fossilized Nature | Stone Wood | Stone trees, hanging vines, dense canopy | Confusing Navigation |
| **Slime Caverns** | Organic/Alien | Slime Stone | Bouncy floors, sticky walls, acid pools | Acid, slowed movement |
| **Magnetic Wastes** | Gravity Anomalies | Magnetite | Floating islands, metallic spikes | Fall Damage, Disorientation |
| **Shadow Void** | Abstract/Eerie | Dark Matter | Pitch black fog, obsidian spikes, voids | Darkness, Sanity Drain |

### Solid Layer Biomes (10 Variants)

Rock composition themes for solid strata layers.

| Biome Name | Theme | Rock Type | Cave Style | Ore Richness |
|------------|-------|-----------|------------|--------------|
| **Limestone Crust** | Sedimentary | White Limestone | Swiss-cheese erosion, water pockets | Low |
| **Granite Shield** | Hard Rock | Grey Granite | Sparse, narrow cracks (hard to drill) | Medium |
| **Sandstone Strata** | Soft/Desert | Orange Sandstone | Wide, unstable tunnels, crumbling | Low (Fossils) |
| **Permafrost Layer** | Frozen Soil | Ice-veined Rock | Icy tunnels, preserved artifacts | Medium (Cryo fluids) |
| **Volcanic Veins** | Igneous | Black Basalt | Magma cracks, vertical shafts | High (Rare metals) |
| **Crystalline Matrix** | Gem-rich | Glittering Stone | Geode pockets, sharp angles | High (Gems) |
| **Fossil Beds** | Ancient Life | Bone-infused Rock | Ribcage tunnels, amber deposits | Medium (Artifacts) |
| **Metallic Strata** | Industrial | Iron-infused Stone | Smooth metallic walls, geometric caves | Very High (Iron/Cooper) |
| **Sulfur Crust** | Toxic/Chemical | Yellow Sulfur Rock | Gas pockets, acid drips | Medium (Chemicals) |
| **Abyssal Bedrock** | Deep Crushing | Dark Purple Stone | Rare large caverns, high pressure | Legendary |

---

## Performance Optimization Tasks

### Task 10.4.11: Biome Lookup Caching
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Recommendation**: ✅ **IMPLEMENT** - Pure win, no visual impact

**Implementation Notes**:
- Implemented `ChunkBiome` component.
- `ChunkGenerationSystem` samples biome at chunk center and 4 corners.
- If all samples match, marks chunk as `IsHomogeneous`.
- `GenerateColumnDataJob` skips per-column noise lookup if valid homogeneous biome is provided (saving ~2048 noise calls per chunk).
- Preserves boundary blending for non-homogeneous chunks.

---

### Task 10.4.12: Temperature/Humidity LOD
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: LOW  
**Recommendation**: ⏸️ **DEFER** - May cause biome boundary flicker

**Problem**: Full multi-octave noise for biome selection.

**Solution**:
- Use single-octave noise for distant chunks
- Biome boundaries less precise but faster
- Full precision only near player

| Pros | Cons |
|------|------|
| ✅ Faster biome detection at distance | ⚠️ Imprecise boundaries |
| ✅ Scales with view distance | ⚠️ May cause biome flicker |
| ✅ Drop-in replacement | |

---

### Task 10.4.13: Biome Boundary Blending
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: LOW  
**Recommendation**: ⏸️ **DEFER** - Complex implementation, marginal gains

**Problem**: Biome transitions cause per-voxel material blending overhead.

**Solution**:
- Pre-compute blend weights at chunk edges
- Cache blended material IDs in NativeArray
- Skip blending for interior voxels

| Pros | Cons |
|------|------|
| ✅ Skip 95%+ of blend calculations | ⚠️ Edge detection complexity |
| ✅ Only process boundary voxels | ⚠️ Cache for edge materials |
| ✅ Burst-compatible | |

---

## Optimization Summary & Recommendations

| Task | Name | Priority | Key Benefit | Key Risk | Keep? | Notes |
|------|------|----------|-------------|----------|-------|-------|
| **10.4.11** | Biome Lookup Caching | MEDIUM | One lookup per chunk | Memory for component | ✅ YES | Easy win |
| **10.4.12** | Temp/Humidity LOD | LOW | Less noise evaluation | Imprecise boundaries | ⚠️ OPTIONAL | Only if biome noise is slow |
| **10.4.13** | Boundary Blending | LOW | Skip interior voxels | Complex edge detection | ⚠️ OPTIONAL | Only if blending is bottleneck |

### Recommended Actions

1. **Implement 10.4.11** - Simple, big savings for per-voxel biome lookups
2. **Skip 10.4.12** - Biome noise is 2D only (cheap), marginal gains
3. **Skip 10.4.13** - Adds complexity, blending at edges is rare

---

## Culling Optimization Tasks

### Task 10.4.14: Frustum Culling for Biome Vegetation
**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Visual Impact**: ✅ NONE (safe)

**Implementation Notes**:
- `DecoratorInstancingSystem` manually calculates camera frustum planes each frame.
- Iterates all instances and checks `GeometryUtility.TestPlanesAABB` against their world bounds.
- Only adds visible instances to `Graphics.DrawMeshInstanced` buffer.
- Essential for GPU instancing since `DrawMeshInstanced` does not cull automatically.

---

### Task 10.4.15: Hierarchical Biome Region Culling
**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Visual Impact**: ✅ NONE (safe)

**Implementation Notes**:
- `DecoratorInstancingSystem` maintains `_occludedChunks` set (populated by `ChunkVisibilitySystem`).
- Before per-instance culling, it checks if the instance's chunk is occluded.
- Also implements `CheckChunkFrustum` to skip entire chunks if the chunk bounds are outside the frustum.

---

### Task 10.4.16: Small Object Fade for Ground Cover
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: MEDIUM  
**Visual Impact**: ⚠️ USE CAREFULLY

**Problem**: Grass and small plants rendered at extreme distances.

**Solution**:
- Fade grass alpha to 0 beyond 20m
- Small rocks/debris fade beyond 30m
- Larger plants (bushes) fade beyond 50m

**Mitigation**: Use fog to hide fade transition, keep large plants visible

---

### Task 10.4.17: LOD for Biome Vegetation
**Status**: ⏸️ DEFERRED (MVP optimization phase)  
**Priority**: MEDIUM  
**Visual Impact**: ⚠️ USE CAREFULLY

**Problem**: Full-detail plant meshes at all distances.

**Solution**:
- Billboard imposters for distant trees/large plants
- Simplified meshes for medium distance
- Use crossfade transition (not instant swap)

**Mitigation**: Dithered LOD transitions, fog helps hide

---

### Culling Summary

| Task | Type | Visual Impact | Implement? |
|------|------|---------------|------------|
| 10.4.14 | Frustum | ✅ None | ✅ YES |
| 10.4.15 | Hierarchical | ✅ None | ✅ YES |
| 10.4.16 | Small Object Fade | ⚠️ Careful | ⚠️ With fade + fog |
| 10.4.17 | LOD Crossfade | ⚠️ Careful | ⚠️ With dither |

