# EPIC 10: Multi-Layer Voxel World Generation

**Status**: 🔲 IN PROGRESS  
**Goal**: Create a massive multi-layer underground world with hollow earth layers and procedural caves.

**Philosophy**:
The world consists of **6-8 alternating layers** of solid rock (with caves) and hollow earth (massive underground biomes). Players spend 30-60 minutes exploring each layer before descending deeper.

**Key Features:**
- **Multi-Layer Structure**: Solid → Hollow → Solid → Hollow (6-8 times)
- **Hollow Earth Layers**: 500m-1500m tall underground biomes
- **Designer-Configurable**: All dimensions via ScriptableObjects
- **High-Performance DOTS**: Burst-compiled jobs, blob assets, layer streaming
- **Total Depth**: ~7,500m with ~9-12 hours of gameplay

**Priority**: HIGH  
**Dependencies**: EPIC 8 (Core Voxel), EPIC 9.1 ✅ (Visual Refinement), EPIC 9.2 ✅ (LOD System)  
**Estimated Duration**: 4-6 weeks

> **Note**: Epic 9.1 and 9.2 are now complete. Generation can leverage:
> - `VoxelVisualMaterial` for per-material texturing (9.1)
> - `VoxelLODConfig` for LOD-aware generation (9.2)

---

## World Structure Overview

```
Surface (Ground Level = 0)
│
├─ Solid Layer 1:    0m to -400m          (400m thick)
│   └─ Caves: Swiss cheese, spaghetti, noodle tunnels
│   └─ Content: Basic ores, introduction to mining
│
├─ HOLLOW EARTH 1:   -400m to -900m       (500m tall)
│   └─ Biome: Mushroom Forest
│   └─ Floor Area: 2km × 2km
│   └─ Playtime: ~45 minutes
│
├─ Solid Layer 2:    -900m to -1300m      (400m thick)
│   └─ Harder rock, better ores
│
├─ HOLLOW EARTH 2:   -1300m to -2100m     (800m tall)
│   └─ Biome: Crystal Cavern
│   └─ Floor Area: 3km × 3km
│   └─ Playtime: ~60 minutes
│
├─ Solid Layer 3:    -2100m to -2500m     (400m thick)
│
├─ HOLLOW EARTH 3:   -2500m to -3100m     (600m tall)
│   └─ Biome: Underground Ocean (with islands)
│
├─ Solid Layer 4:    -3100m to -3500m     (400m thick)
│
├─ HOLLOW EARTH 4:   -3500m to -4500m     (1000m tall)
│   └─ Biome: Volcanic Depths (lava rivers)
│
├─ Solid Layer 5:    -4500m to -4900m     (400m thick)
│
├─ HOLLOW EARTH 5:   -4900m to -5600m     (700m tall)
│   └─ Biome: Frozen Expanse
│
├─ Solid Layer 6:    -5600m to -6000m     (400m thick)
│
└─ THE CORE:         -6000m to -7500m     (1500m tall)
    └─ Biome: Ancient Realm (Final zone)
    └─ Floor Area: 5km × 5km
    └─ Playtime: ~90 minutes
```

**Total Depth**: ~7,500m  
**Total Playtime**: 9-12 hours

---

## Architecture: Layered Generation Pipeline

```
┌─────────────────────────────────────────────────────────────┐
│                    GENERATION PIPELINE                       │
├─────────────────────────────────────────────────────────────┤
│  Pass 0: Layer Detection (Which layer type are we in?)      │
│  Pass 1: Base Terrain / Hollow Structure                    │
│  Pass 2: Stratigraphy (Materials by Depth) [Solid only]     │
│  Pass 3: Caves (3D Noise Carving) [Solid only]              │
│  Pass 4: Floor/Ceiling/Pillars [Hollow only]                │
│  Pass 5: Resources (Ore Veins)                              │
│  Pass 6: Fluids (Water/Lava Lakes)                          │
│  Pass 7: Decorators (Crystals, Structures, Vegetation)      │
└─────────────────────────────────────────────────────────────┘

All passes are Burst-compiled parallel jobs.
```

---

## Sub-Epics

| Sub-Epic | Topic | Priority | Status | Depends On |
|----------|-------|----------|--------|------------|
| [10.1](EPIC10.1.md) | Geology & Resources | HIGH | ✅ COMPLETE | 8.2 |
| [10.2](EPIC10.2.md) | Cave & Hollow Earth Systems | CRITICAL | 🔲 | 10.1 |
| [10.3](EPIC10.3.md) | Fluids & Hazards | MEDIUM | 🔲 | 10.2 |
| [10.4](EPIC10.4.md) | Biome System | MEDIUM | 🔲 | 10.2 |
| [10.5](EPIC10.5.md) | Decorators & Structures | LOW | 🔲 | 10.4 |
| [10.6](EPIC10.6.md) | Generation Tooling | HIGH | 🔲 | 10.1-10.5 |
| [10.7](EPIC10.7.md) | Generation Performance | CRITICAL | 🔲 | 10.2 |
| [10.10](EPIC10.10.md) | GPU-Driven Rendering | HIGH | 🔴 NOT STARTED | 10.9 |
| [10.11](EPIC10.11.md) | ChunkPhysics Optimization | CRITICAL | ✅ COMPLETE | 10.9, 8.3 |
| [10.13](EPIC10.13.md) | Rendering Optimization | HIGH | 🔴 NOT STARTED | 10.11 |
| [10.14](EPIC10.14.md) | Memory Optimization | CRITICAL | ✅ COMPLETE | 10.7 |
| [10.15](EPIC10.15.md) | Player Performance | CRITICAL | ✅ COMPLETE | 10.7 |
| [10.16](EPIC10.16.md) | Network Traffic | HIGH | ✅ COMPLETE | 10.11 |


---

## Core ScriptableObjects

### WorldStructureConfig (Master)
Defines all layers, their depths, and configurations.

```csharp
[CreateAssetMenu(menuName = "DIG/World/World Structure Config")]
public class WorldStructureConfig : ScriptableObject
{
    public WorldLayerDefinition[] Layers;  // All 12 layers
    public uint WorldSeed;
    public float GroundLevel = 0f;
    
    // Streaming settings
    public int LayersAboveToLoad = 1;
    public int LayersBelowToLoad = 1;
}
```

### WorldLayerDefinition (Per-Layer)
```csharp
[CreateAssetMenu(menuName = "DIG/World/World Layer Definition")]
public class WorldLayerDefinition : ScriptableObject
{
    public string LayerName;
    public LayerType Type;  // Solid or Hollow
    public float TopDepth;
    public float BottomDepth;
    public float AreaWidth;
    public float AreaLength;
    public float TargetPlaytimeMinutes;
    
    // For Solid layers
    public StrataProfile StrataProfile;
    public CaveProfile CaveProfile;
    
    // For Hollow layers
    public HollowEarthProfile HollowProfile;
}
```

### HollowEarthProfile
```csharp
[CreateAssetMenu(menuName = "DIG/World/Hollow Earth Profile")]
public class HollowEarthProfile : ScriptableObject
{
    public float AverageHeight = 500f;      // 500m-1500m
    public float HeightVariation = 50f;
    public float FloorWidth = 2000f;        // 2km-5km
    public float FloorLength = 2000f;
    
    // Floor/Ceiling
    public float FloorNoiseScale;
    public float CeilingNoiseScale;
    public bool HasStalactites;
    
    // Pillars
    public bool GeneratePillars;
    public float PillarFrequency;
    
    // Features
    public bool HasUndergroundLakes;
    public bool HasCrystalFormations;
    public bool HasLavaFlows;
    
    // Biome
    public BiomeDefinition Biome;
}
```

---

## Performance Architecture

### DOTS Components
All generation uses Unity DOTS with Burst compilation:

| Component | Type | Purpose |
|-----------|------|---------|
| `GenerateTerrainJob` | IJobParallelFor | Per-voxel density + material |
| `CaveCarveJob` | IJobParallelFor | Cave noise sampling |
| `HollowEarthJob` | IJobParallelFor | Floor/ceiling/pillar density |
| `OreSpawnJob` | IJobParallelFor | Ore vein placement |
| `FluidFillJob` | IJob | Lake/river generation |

### Blob Assets
Configuration converted to Burst-compatible blob assets:

| Blob | Source ScriptableObject |
|------|-------------------------|
| `StrataBlob` | StrataProfile |
| `CaveParamsBlob` | CaveProfile |
| `HollowEarthBlob` | HollowEarthProfile |
| `BiomeDataBlob` | BiomeDefinition |

### Layer Streaming
Only 2-3 layers loaded at once:

```csharp
// Player at -1500m (in Hollow 2)
// Loaded: Solid 2 (-900 to -1300m)
//         Hollow 2 (-1300m to -2100m) ← Current
//         Solid 3 (-2100m to -2500m)
// Unloaded: Everything else
```

---

## Editor Tools

| Tool | Purpose | Epic |
|------|---------|------|
| World Layer Editor | Visual cross-section of all layers | 10.6 |
| Hollow Earth Previewer | Floor/ceiling heightmap preview | 10.6 |
| Cave Slice Viewer | Horizontal cave view at any depth | 10.2 |
| Generation Benchmark | Measure Burst job performance | 10.6 |
| World Structure Validator | Check configuration errors | 10.6 |
| Biome Map Viewer | Visualize biome distribution | 10.4 |

---

## Performance Budgets

| Metric | Target | Approach |
|--------|--------|----------|
| Chunk generation | < 5ms | Burst + parallel jobs |
| LOD chunk generation | < 0.5ms | Reduced sampling |
| Frame budget (total) | < 12ms | Budget manager |
| Layers loaded | 2-3 | Layer streaming |
| Memory per layer | < 500MB | Aggressive unloading |

---

## Success Criteria

- [ ] 6+ hollow earth layers reachable by player
- [ ] Each hollow is 500m-1500m tall (designer configurable)
- [ ] Each hollow has distinct biome/theme
- [ ] 30-60 min gameplay per layer
- [ ] Layer streaming prevents memory issues
- [ ] < 5ms per chunk generation (Burst-compiled)
- [ ] All dimensions configurable via ScriptableObjects
- [ ] Same seed produces identical world
- [ ] All editor tools work without Play Mode

---

## Dependencies

| Depends On | Provides To |
|------------|-------------|
| Epic 8 (Core Voxel) | Base chunk system |
| Epic 9.1 ✅ (Visuals) | Material textures, `VoxelVisualMaterial` |
| Epic 9.2 ✅ (LOD) | LOD-aware generation, `VoxelLODConfig` |
| — | Epic 11 (Resources/Loot) |
