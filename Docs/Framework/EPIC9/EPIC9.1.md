# Epic 9.1: Visual Refinement

**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Dependencies**: EPIC 8.5 (Marching Cubes), EPIC 8.12 (Seamless Texturing)  
**Estimated Time**: 3-4 days  
**Last Updated**: 2025-12-20

---

## Quick Start Guide

### For Designers

1. **Create Visual Materials**
   - Right-click in Project → Create → DIG → Voxel → Visual Material
   - Drop textures onto the editor (auto-assigns by suffix: `_albedo`, `_normal`, `_height`, `_detail`)
   - Adjust Smoothness, Metallic, and Tint as needed
   - Click "Generate Preview Icon" for quick identification

2. **Build Texture Arrays**
   - Open DIG → Voxel → Texture Array Builder
   - Select a folder containing your textures
   - Click "Scan Folder" to categorize textures
   - Click "Build Texture Arrays" to create arrays for each category
   - Arrays are saved to `Assets/Resources/VoxelTextures/`

3. **Apply Enhanced Shader**
   - Create a material using `DIG/VoxelTriplanarEnhanced` shader
   - Assign the generated texture arrays
   - Optionally add detail textures for close-up viewing

### For Developers

1. **Key Files**
   ```
   Assets/Scripts/Voxel/
   ├── Rendering/
   │   ├── VoxelVisualMaterial.cs   # Per-material visual config
   │   └── VoxelTextureConfig.cs    # Existing texture array config
   ├── Shaders/
   │   ├── VoxelTriplanarEnhanced.shader  # NEW: Detail + Normal + AO
   │   └── VoxelTriplanarMultiMaterial.shader
   └── Editor/
       ├── MaterialVisualEditor.cs  # NEW: Drag-drop texture setup
       └── TextureArrayBuilder.cs   # NEW: Array builder window
   ```

2. **Shader Features**
   - Triplanar mapping with Texture2DArray support
   - Detail textures with distance-based fading
   - Normal mapping with RNM blending
   - Height-based ambient occlusion
   - Proper SSAO support (DepthNormals pass)

---

## Component Reference

### VoxelVisualMaterial

```csharp
[CreateAssetMenu(menuName = "DIG/Voxel/Visual Material")]
public class VoxelVisualMaterial : ScriptableObject
{
    // Identification
    byte MaterialID;           // Must match VoxelMaterialDefinition
    string DisplayName;
    
    // Base Textures
    Texture2D Albedo;          // Required: Main color
    Texture2D Normal;          // Optional: Surface detail
    Texture2D HeightMap;       // Optional: For AO/parallax
    
    // Surface Properties
    float Smoothness;          // 0-1, default 0.3
    float Metallic;            // 0-1, default 0
    Color Tint;                // Color multiplier
    
    // Detail Textures (close-up)
    Texture2D DetailAlbedo;    // Tiled at higher frequency
    Texture2D DetailNormal;
    float DetailStrength;      // 0-1, default 0.3
    float DetailScale;         // 1-20, default 8
    
    // Ambient Occlusion
    float AOStrength;          // 0-1, default 0.5
}
```

### VoxelTriplanarEnhanced Shader Properties

| Property | Type | Description |
|----------|------|-------------|
| `_AlbedoArray` | Texture2DArray | Material albedo textures (index = material ID) |
| `_NormalArray` | Texture2DArray | Material normal maps |
| `_HeightArray` | Texture2DArray | Height maps for AO approximation |
| `_DetailTex` | Texture2D | Detail albedo (tiled at `_DetailScale`) |
| `_DetailNormal` | Texture2D | Detail normal map |
| `_DetailScale` | Float | Detail texture tiling (1-20) |
| `_DetailStrength` | Float | Detail blend amount (0-1) |
| `_DetailFadeDistance` | Float | Distance at which details fade (5-50) |
| `_Scale` | Float | Base texture scale (world units) |
| `_BlendSharpness` | Float | Triplanar blend sharpness (1-16) |
| `_NormalStrength` | Float | Normal map intensity (0-2) |
| `_AOStrength` | Float | Ambient occlusion strength (0-1) |
| `_Color1-4` | Color | Per-material tint colors |

---

## Editor Tools

### Material Visual Editor

**Access**: Select any `VoxelVisualMaterial` asset in the Inspector

**Features**:
- **Drag-Drop Texture Assignment**: Drop textures on the drop zone, auto-assigned by suffix
  - `_albedo`, `_diffuse`, `_color`, `_c` → Albedo
  - `_normal`, `_nrm`, `_n` → Normal
  - `_height`, `_displacement`, `_bump`, `_h` → Height
  - `_detail` → Detail Albedo
- **Texture Preview Grid**: Visual overview of all assigned textures
- **Generate Preview Icon**: Creates 64x64 thumbnail for quick identification

### Texture Array Builder

**Access**: DIG → Voxel → Texture Array Builder

**Features**:
- **Folder Scanning**: Scans folders for textures and categorizes by suffix
- **Batch Building**: Creates Texture2DArray for each category
- **Size Options**: 128, 256, 512, 1024, 2048
- **Mipmap Generation**: Configurable mipmap generation
- **Output Path**: Customizable save location

**Supported Categories**:
- `albedo`, `normal`, `height`, `ao`, `roughness`, `metallic`, `detail`

---

## Setup Guide

### 1. Prepare Textures

Organize textures with naming convention:
```
Textures/
├── Dirt_albedo.png
├── Dirt_normal.png
├── Dirt_height.png
├── Stone_albedo.png
├── Stone_normal.png
├── Stone_height.png
└── ...
```

### 2. Build Texture Arrays

1. Open DIG → Voxel → Texture Array Builder
2. Drag your textures folder to "Source Folder"
3. Set texture size to match your source textures
4. Click "Scan Folder"
5. Click "Build Texture Arrays"

### 3. Create Material

1. Create a new Material
2. Set shader to `DIG/VoxelTriplanarEnhanced`
3. Assign texture arrays:
   - `Albedo Array` → ALBEDO_Array.asset
   - `Normal Array` → NORMAL_Array.asset
   - `Height Array` → HEIGHT_Array.asset

### 4. Apply to Chunks

Update `ChunkMeshingSystem` to use the new material, or assign via `VoxelRenderingConfig`.

### 5. Enable SSAO

In URP Asset:
1. Enable "Screen Space Ambient Occlusion"
2. Adjust intensity for caves (0.5-1.0 recommended)
3. The shader's DepthNormals pass ensures SSAO works correctly

---

## Integration Guide

### Using with Existing VoxelMaterialDefinition

`VoxelVisualMaterial.MaterialID` should match `VoxelMaterialDefinition.MaterialID`:

```csharp
// Link visual to gameplay material
var gameplayMat = registry.GetMaterial(materialID);
var visualMat = visualRegistry.GetVisual(materialID);
```

### Custom Shader Integration

To use different shaders based on material:

```csharp
Material GetMaterialShader(byte materialID)
{
    var visual = GetVisualMaterial(materialID);
    if (visual.Metallic > 0.5f)
        return metallicShader;
    if (visual.HasDetailTextures)
        return detailedShader;
    return basicShader;
}
```

### Runtime Texture Array Updates

```csharp
// Add new texture at runtime
public void AddTextureToArray(Texture2D newTex, int index, Texture2DArray array)
{
    Graphics.CopyTexture(newTex, 0, 0, array, index, 0);
    array.Apply();
}
```

---

## Tasks Completed

### Task 9.1.1: Enhanced Triplanar Shader ✅
- Created `VoxelTriplanarEnhanced.shader`
- Texture2DArray support for multiple materials
- Detail textures with distance-based fading
- Normal mapping with RNM blending
- Height-based ambient occlusion
- Proper depth/normals passes for SSAO

### Task 9.1.2: Per-Material Visual Properties ✅
- Created `VoxelVisualMaterial` ScriptableObject
- Full texture support (Albedo, Normal, Height, Detail)
- Surface properties (Smoothness, Metallic, Tint)
- Detail texture configuration
- Preview icon generation

### Task 9.1.3: Gradient-Based Normals System ✅
- Normal maps sampled via Texture2DArray
- RNM blending for detail normals
- Configurable normal strength

### Task 9.1.4: SSAO Setup ✅
- Shader includes DepthOnly and DepthNormals passes
- Compatible with URP SSAO feature
- Documentation for URP configuration

---

## Acceptance Criteria

- [x] Terrain has smooth, organic appearance
- [x] Lighting reacts correctly to surface angle (normal mapping)
- [x] Close-up detail visible without blurriness (detail textures)
- [x] Caves have proper AO (dark corners) - height-based + SSAO compatible
- [x] Texture array builder works with drag-drop
- [x] Material editor auto-detects texture types

---

## Troubleshooting

### Textures appear wrong material

- Verify `MaterialID` matches between `VoxelMaterialDefinition` and `VoxelVisualMaterial`
- Check that vertex colors correctly encode material ID
- Ensure texture array indices match material IDs

### Detail textures not visible

- Check `_DetailFadeDistance` - may be too low
- Verify detail textures are assigned
- Increase `_DetailStrength`

### SSAO not working

- Ensure URP Asset has SSAO enabled
- Verify shader is using `DIG/VoxelTriplanarEnhanced`
- Check that DepthNormals pass is included

### Texture array build fails

- Ensure all source textures are the same size
- Check texture import settings (Read/Write enabled)
- Verify folder path is valid

---

## Related Epics

| Epic | Relevance |
|------|-----------|
| EPIC 8.5 | Marching Cubes mesh generation |
| EPIC 8.10 | Original texture array system |
| EPIC 8.12 | Seamless texturing |
| EPIC 9.2 | LOD system (uses these visual materials) |
