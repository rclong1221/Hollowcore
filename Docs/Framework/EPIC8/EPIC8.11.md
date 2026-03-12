# EPIC 8.11: Procedural Generation

**Status**: 🔲 NOT STARTED  
**Priority**: LOW (Phase 2)  
**Dependencies**: EPIC 8.8 (Streaming working)
**Estimated Time**: 2+ days

---

## Goal

Generate interesting terrain instead of flat ground:
- Cave systems
- Ore veins at depth
- Biomes
- Structures

---

## This Epic Overlaps with EPIC 10

See `EPIC10/EPIC10.md` for advanced world generation.

---

## Basic Tasks

### Task 8.11.1: 3D Noise Caves

```csharp
// In GenerateVoxelDataJob:
float caveNoise = noise.snoise(worldPos * 0.05f);
if (caveNoise > 0.6f && distanceToSurface > 5f)
{
    density = VoxelConstants.DENSITY_AIR;
    material = VoxelConstants.MATERIAL_AIR;
}
```

### Task 8.11.2: Ore Vein Generation

```csharp
// Different noise seeds for different ores
float ironNoise = noise.snoise(worldPos * 0.1f + float3(1000, 0, 0));
float goldNoise = noise.snoise(worldPos * 0.08f + float3(0, 1000, 0));

if (distanceToSurface > 10f && ironNoise > 0.7f)
    material = VoxelConstants.MATERIAL_IRON_ORE;
    
if (distanceToSurface > 30f && goldNoise > 0.8f)
    material = VoxelConstants.MATERIAL_GOLD_ORE;
```

### Task 8.11.3: Multiple Noise Octaves

```csharp
float TerrainHeight(float2 xz)
{
    float height = 0;
    float amplitude = 20f;
    float frequency = 0.02f;
    
    for (int octave = 0; octave < 4; octave++)
    {
        height += noise.snoise(xz * frequency) * amplitude;
        amplitude *= 0.5f;
        frequency *= 2f;
    }
    
    return height;
}
```

---

## Integration with EPIC 10

- **EPIC 10.1**: Geology & Resources
- **EPIC 10.2**: Caves & Hollow Earth
- **EPIC 10.3**: Fluids & Hazards
- **EPIC 10.4**: Subterranean Biomes

---

## Acceptance Criteria

- [ ] Terrain has height variation
- [ ] Cave systems exist underground
- [ ] Ore veins spawn at appropriate depths
- [ ] Generation is deterministic (same seed = same world)
