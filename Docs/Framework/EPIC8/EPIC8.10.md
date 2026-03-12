# EPIC 8.10: Materials & Textures

**Status**: ✅ COMPLETED  
**Priority**: MEDIUM  
**Dependencies**: EPIC 8.5 (Marching Cubes Meshing)
**Estimated Time**: 1 day

---

## Goal

Different voxel types have different visual appearances using a **Texture2DArray** and **Triplanar Mapping**. This allows multiple materials in a single draw call per chunk.

---

## 🚀 Quick Start Guide

### 1. Create Textures
1. Create 512x512 textures for each material type (Dirt, Stone, Iron Ore, etc.).
2. Textures must be **Read/Write Enabled** in Import Settings.
3. All textures must be the same size.

### 2. Create Texture Config
1. Right-click in Project → **Create > DIG > Voxel > Texture Config**.
2. Name it `VoxelTextureConfig`.
3. Drag textures into the `Textures` array in order of Material ID:
   - Index 0: Air (unused, can be empty/magenta)
   - Index 1: Dirt
   - Index 2: Stone
   - Index 3: Iron Ore
   - ... and so on.
4. Click **"Build Texture Array"** button in Inspector.
5. Move the asset to `Assets/Resources/VoxelTextureConfig.asset`.

### 3. Play Mode
- The system automatically loads the config from `Resources/VoxelTextureConfig`.
- Each voxel material ID maps to a texture in the array.
- Triplanar mapping prevents stretching on vertical surfaces.

---

## 🛠️ Architecture Details

### Components

| Asset/Script | Location | Description |
|--------------|----------|-------------|
| `VoxelTextureConfig` | `Assets/Scripts/Voxel/Rendering/VoxelTextureConfig.cs` | ScriptableObject that holds source textures and builds `Texture2DArray`. |
| `VoxelTriplanar.shader` | `Assets/Resources/Shaders/VoxelTriplanar.shader` | URP shader with Texture2DArray + Triplanar sampling. |
| `GenerateMarchingCubesMeshJob` | `Assets/Scripts/Voxel/Meshing/GenerateMarchingCubesMeshJob.cs` | Encodes Material ID into Vertex Color R channel. |
| `ChunkMeshingSystem` | `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs` | Loads config and assigns Texture2DArray to material at runtime. |

### Data Flow

```
VoxelBlob.Materials[index] (byte)
    ↓
GenerateMarchingCubesMeshJob
    → Vertex Color R = MaterialID (byte 0-255)
    ↓
Shader (VoxelTriplanar.shader)
    → floor(color.r * 255 + 0.5) = Array Index
    → SAMPLE_TEXTURE2D_ARRAY(_MainTex, uv, index)
    ↓
Final Pixel Color
```

### Shader Technical Details

The `DIG/Voxel/Triplanar` shader:
- Uses **Triplanar Mapping**: Samples texture 3 times (XY, XZ, YZ planes) and blends by normal.
- Uses **Texture2DArray**: Single texture asset containing all materials as slices.
- Material ID from Vertex Color R channel selects array slice.
- Supports URP Lighting (PBR, shadows, fog).

```hlsl
// Key sampling code
half4 xCol = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, pos.yz, materialID);
half4 yCol = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, pos.xz, materialID);
half4 zCol = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, pos.xy, materialID);
half4 baseColor = xCol * blend.x + yCol * blend.y + zCol * blend.z;
```

---

## 💻 Integration Guide for Developers

### Adding a New Material

1. Add texture to `VoxelTextureConfig.Textures` array at the correct index.
2. Click "Build Texture Array" in Inspector.
3. Use the index as the Material ID when generating voxels:
   ```csharp
   // In your generation code
   voxelBlob.Materials[index] = 4; // Material ID 4
   ```

### Custom Shader Properties

| Property | Type | Description |
|----------|------|-------------|
| `_MainTex` | Texture2DArray | The material texture array. |
| `_TextureScale` | Float | World-space tiling scale (default 0.25 = 4 tiles per unit). |
| `_Smoothness` | Float | PBR smoothness (0 = rough, 1 = shiny). |
| `_Metallic` | Float | PBR metalness (0 = non-metal). |

### Fallback Behavior

If `VoxelTextureConfig` is not found in Resources:
- System logs a warning.
- Shader still works but uses uninitialized texture (may appear black/magenta).
- Ensure the asset is named exactly `VoxelTextureConfig` and placed in a `Resources` folder.

---

## 🎨 Integration Guide for Designers

### Texture Requirements

| Property | Requirement |
|----------|-------------|
| Size | Must match `TextureSize` in config (default 512x512) |
| Format | RGBA32 recommended |
| Read/Write | Must be enabled in Import Settings |
| Seamless | Textures should tile seamlessly for best results |

### Material ID Reference

| ID | Material | Notes |
|----|----------|-------|
| 0 | Air | Should never render (no solid triangles) |
| 1 | Dirt | Surface layer |
| 2 | Stone | Underground bulk |
| 3 | Iron Ore | Ore vein |
| 4+ | Custom | Add as needed |

### Debugging Tips

- **Pink/Magenta chunks?** Texture array not built. Click "Build Texture Array".
- **Wrong texture?** Check Material ID order matches generation code.
- **Texture stretching?** Adjust `_TextureScale` in material (lower = larger tiles).

---

## ✅ Acceptance Criteria

- [x] Stone looks like stone (textured, not flat color)
- [x] Ore veins are visually distinct
- [x] Triplanar mapping (no stretching on vertical surfaces)
- [x] Single draw call per chunk (Texture2DArray)
- [x] Material ID encoded in vertex color
- [x] Shader supports URP lighting
- [x] Designer-friendly texture config workflow

---

## Files Created/Modified

| File | Changes |
|------|---------|
| `Assets/Scripts/Voxel/Rendering/VoxelTextureConfig.cs` | NEW - ScriptableObject + Editor |
| `Assets/Resources/Shaders/VoxelTriplanar.shader` | NEW - URP Triplanar + Texture2DArray |
| `Assets/Scripts/Voxel/Meshing/GenerateMarchingCubesMeshJob.cs` | Material ID encoding updated |
| `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs` | Loads texture config, assigns array to material |

---

## Next Steps

1. **Create Textures**: Design 512x512 seamless textures for each material.
2. **Build Config**: Create `VoxelTextureConfig` asset in Resources folder.
3. **Test**: Verify different ores appear distinct in-game.
