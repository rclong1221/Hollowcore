# EPIC 8.12: Seamless Meshes & Solid Texturing

**Status**: ✅ COMPLETED  
**Priority**: HIGH  
**Dependencies**: EPIC 8.5 (Marching Cubes Meshing)
**Estimated Time**: 1-2 days

---

## Implementation Status

| Feature | Status | Notes |
|---------|--------|-------|
| Neighbor Data Fetching | ✅ DONE | `GetNeighborBlobs()` in ChunkMeshingSystem |
| Padded Buffer Fill | ✅ DONE | `GetDensityWithNeighbors()` with smart fallbacks |
| Boundary Cube Iteration | ✅ DONE | Marching Cubes starts at -1 for all axes |
| Triplanar Texturing | ✅ DONE | `VoxelTriplanar.shader` + `VoxelTriplanarMultiMaterial.shader` |
| Smooth Normals | ✅ DONE | `CalculateSmoothNormalsJob` using density gradient |
| Material Blending | ✅ DONE | Vertex colors + multi-material shader |
| LOD System | ❌ DEFERRED | Moved to Epic 9.2 |

---

## Goal

Make voxel terrain look **solid and natural**, not like "crumpled up paper."

Problems to solve:
1. **Chunk boundary seams** - ✅ IMPLEMENTED - `GetNeighborBlobs()` + boundary iteration
2. **UV mapping** - ✅ IMPLEMENTED - `VoxelTriplanar.shader` projects from 3 axes
3. **Smooth normals** - ✅ IMPLEMENTED - `CalculateSmoothNormalsJob` uses density gradient
4. **Material boundaries** - ✅ IMPLEMENTED - Vertex colors + `VoxelTriplanarMultiMaterial.shader`

---

## Problem 1: Chunk Boundary Seams

### The Problem:
```
Without neighbor sampling:
┌─────────┐ ┌─────────┐
│  Chunk A │ │ Chunk B │
│    ╱     │ │     ╱   │    Gap! Vertices don't align
│   ╱      │ │    ╱    │
└─────────┘ └─────────┘
```

### The Solution: Sample Neighbor Data

```csharp
// In Marching Cubes job, extend sampling 1 voxel into neighbors:

private byte GetDensitySafe(int3 pos, ref VoxelBlob blob)
{
    // Inside this chunk
    if (CoordinateUtils.IsInBounds(pos))
    {
        return blob.Densities[CoordinateUtils.VoxelPosToIndex(pos)];
    }
    
    // OUTSIDE this chunk - sample from neighbor
    
    // +X neighbor
    if (pos.x >= VoxelConstants.CHUNK_SIZE && NeighborPosX.IsCreated)
    {
        int3 neighborLocalPos = new int3(
            pos.x - VoxelConstants.CHUNK_SIZE,
            pos.y,
            pos.z
        );
        if (CoordinateUtils.IsInBounds(neighborLocalPos))
        {
            return NeighborPosX.Value.Densities[
                CoordinateUtils.VoxelPosToIndex(neighborLocalPos)];
        }
    }
    
    // Similarly for -X, +Y, -Y, +Z, -Z neighbors...
    
    // Fallback if neighbor not loaded
    return GetSmartFallback(pos);
}

private byte GetSmartFallback(int3 pos)
{
    // +Y: assume air (above ground)
    if (pos.y >= VoxelConstants.CHUNK_SIZE)
        return VoxelConstants.DENSITY_AIR;
    
    // -Y: assume solid (below ground)
    if (pos.y < 0)
        return VoxelConstants.DENSITY_SOLID;
    
    // Horizontal: assume air (creates surface at boundary)
    return VoxelConstants.DENSITY_AIR;
}
```

### Result:
```
With neighbor sampling:
┌─────────┬─────────┐
│  Chunk A│ Chunk B │
│    ╲    │    ╱    │    Seamless! Same vertices at boundary
│     ╲   │   ╱     │
└─────────┴─────────┘
```

---

## Problem 2: Texture Stretching ("Crumpled Paper")

### The Problem:
Standard UV mapping stretches textures on steep surfaces:
```
         ╱|        Texture stretched here!
        ╱ |        Rock looks like rubber
       ╱  |
      ╱   |
     ─────┘
```

### The Solution: Triplanar Mapping

Project texture from 3 axes (X, Y, Z) and blend based on surface normal:

```hlsl
Shader "DIG/VoxelTriplanarAdvanced"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Scale ("Texture Scale", Float) = 1.0
        _BlendSharpness ("Blend Sharpness", Range(1, 8)) = 4.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            
            float _Scale;
            float _BlendSharpness;
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            
            half4 TriplanarSample(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 worldNormal)
            {
                // Calculate blending weights
                float3 blending = pow(abs(worldNormal), _BlendSharpness);
                blending /= (blending.x + blending.y + blending.z + 0.001);
                
                // Sample from 3 projections
                float2 uvX = worldPos.zy * _Scale;
                float2 uvY = worldPos.xz * _Scale;
                float2 uvZ = worldPos.xy * _Scale;
                
                half4 xSample = SAMPLE_TEXTURE2D(tex, samp, uvX);
                half4 ySample = SAMPLE_TEXTURE2D(tex, samp, uvY);
                half4 zSample = SAMPLE_TEXTURE2D(tex, samp, uvZ);
                
                // Blend based on normal direction
                return xSample * blending.x + 
                       ySample * blending.y + 
                       zSample * blending.z;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = TriplanarSample(
                    TEXTURE2D_ARGS(_MainTex, sampler_MainTex),
                    IN.worldPos,
                    normalize(IN.worldNormal)
                );
                
                // Apply lighting
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(IN.worldNormal, mainLight.direction));
                color.rgb *= (NdotL * 0.5 + 0.5) * mainLight.color;
                
                return color;
            }
            ENDHLSL
        }
    }
}
```

### Result:
```
With triplanar mapping:
         ╱|        Texture looks correct!
        ╱ |        Rock looks like rock
       ╱  |
      ╱   |
     ─────┘
```

---

## Problem 3: Faceted Lighting (Flat Shading)

### The Problem:
Each triangle has its own normal → looks faceted, not smooth:
```
Without smooth normals:
    ╱╲
   ╱  ╲    Each face lit differently
  ╱    ╲   Looks like low-poly model
 ╱      ╲
```

### The Solution: Averaged Normals

Calculate normal per-vertex by averaging surrounding faces:

```csharp
// In mesh generation, compute smooth normals:

public void GenerateSmoothNormals(
    NativeArray<float3> vertices,
    NativeArray<int> indices,
    NativeArray<float3> outNormals)
{
    // Accumulate face normals per vertex
    var normalAccum = new NativeArray<float3>(vertices.Length, Allocator.Temp);
    var normalCount = new NativeArray<int>(vertices.Length, Allocator.Temp);
    
    // For each triangle
    for (int i = 0; i < indices.Length; i += 3)
    {
        int i0 = indices[i];
        int i1 = indices[i + 1];
        int i2 = indices[i + 2];
        
        float3 v0 = vertices[i0];
        float3 v1 = vertices[i1];
        float3 v2 = vertices[i2];
        
        // Calculate face normal
        float3 faceNormal = math.normalize(math.cross(v1 - v0, v2 - v0));
        
        // Add to vertex normals
        normalAccum[i0] += faceNormal;
        normalAccum[i1] += faceNormal;
        normalAccum[i2] += faceNormal;
        
        normalCount[i0]++;
        normalCount[i1]++;
        normalCount[i2]++;
    }
    
    // Average and normalize
    for (int i = 0; i < vertices.Length; i++)
    {
        if (normalCount[i] > 0)
        {
            outNormals[i] = math.normalize(normalAccum[i] / normalCount[i]);
        }
        else
        {
            outNormals[i] = new float3(0, 1, 0);  // Default up
        }
    }
    
    normalAccum.Dispose();
    normalCount.Dispose();
}
```

**Or: Use Gradient-Based Normals** (more accurate for terrain):

```csharp
// Calculate normal from density gradient
float3 CalculateNormalFromGradient(int3 pos, ref VoxelBlob blob)
{
    // Sample density gradient
    float dx = GetDensitySafe(pos + new int3(1, 0, 0), ref blob) -
               GetDensitySafe(pos + new int3(-1, 0, 0), ref blob);
    float dy = GetDensitySafe(pos + new int3(0, 1, 0), ref blob) -
               GetDensitySafe(pos + new int3(0, -1, 0), ref blob);
    float dz = GetDensitySafe(pos + new int3(0, 0, 1), ref blob) -
               GetDensitySafe(pos + new int3(0, 0, -1), ref blob);
    
    // Gradient points toward solid, so negate for surface normal
    return math.normalize(new float3(-dx, -dy, -dz));
}
```

---

## Problem 4: Material Transitions

### The Problem:
Sharp transitions between materials look artificial:
```
│ ROCK │ DIRT │    Hard edge looks fake
````

### The Solution: Texture Blending at Boundaries

```hlsl
// Multi-material triplanar shader with blending

TEXTURE2D_ARRAY(_MaterialTextures);  // All material textures in array
SAMPLER(sampler_MaterialTextures);

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float3 worldPos : TEXCOORD0;
    float3 worldNormal : TEXCOORD1;
    float2 materialBlend : TEXCOORD2;  // (material1, material2)
    float blendFactor : TEXCOORD3;      // How much of each
};

half4 frag(Varyings IN) : SV_Target
{
    float3 blending = GetTriplanarBlending(IN.worldNormal);
    
    // Sample both materials
    half4 color1 = SampleMaterialTriplanar(IN.materialBlend.x, IN.worldPos, blending);
    half4 color2 = SampleMaterialTriplanar(IN.materialBlend.y, IN.worldPos, blending);
    
    // Blend based on vertex blend factor
    half4 color = lerp(color1, color2, IN.blendFactor);
    
    return color;
}
```

In mesh generation, calculate blend factors at material boundaries:

```csharp
// Per-vertex material blending
public struct VertexMaterialData
{
    public byte Material1;
    public byte Material2;
    public float BlendFactor;  // 0 = 100% mat1, 1 = 100% mat2
}

private VertexMaterialData GetVertexMaterial(float3 vertexPos, int3 cellPos, ref VoxelBlob blob)
{
    // Sample materials at surrounding voxels
    byte[] nearbyMats = new byte[8];
    for (int i = 0; i < 8; i++)
    {
        int3 cornerPos = cellPos + (int3)MarchingCubesTables.CornerOffsets[i];
        nearbyMats[i] = GetMaterialSafe(cornerPos, ref blob);
    }
    
    // Find dominant materials and calculate blend
    // ...
    return new VertexMaterialData { ... };
}
```

---

## Problem 5: Level of Detail (LOD)

### The Problem:
Distant chunks have same mesh complexity → wasted GPU:
```
Near chunk: 10,000 triangles (needed for detail)
Far chunk: 10,000 triangles (wasted - just a few pixels on screen)
```

### The Solution: LOD Meshes

```csharp
public enum ChunkLOD
{
    LOD0 = 1,   // Full detail (1:1 voxels)
    LOD1 = 2,   // Half detail (2:1 voxels)
    LOD2 = 4,   // Quarter detail (4:1 voxels)
    LOD3 = 8    // Low detail (8:1 voxels)
}

public struct GenerateMeshJobLOD : IJob
{
    [ReadOnly] public int LODStep;  // 1, 2, 4, or 8
    
    public void Execute()
    {
        // Step through voxels at LOD interval
        for (int z = 0; z < VoxelConstants.CHUNK_SIZE; z += LODStep)
        {
            for (int y = 0; y < VoxelConstants.CHUNK_SIZE; y += LODStep)
            {
                for (int x = 0; x < VoxelConstants.CHUNK_SIZE; x += LODStep)
                {
                    // Sample density for this LOD cell
                    byte density = GetAverageDensity(x, y, z, LODStep);
                    // ... generate mesh at lower resolution
                }
            }
        }
    }
}
```

---

## Putting It All Together

### Mesh Generation Pipeline:

```
1. Generate voxel data (8.2)
   ↓
2. Sample neighbor chunks for boundaries
   ↓
3. Run Marching Cubes with neighbor data
   ↓
4. Calculate smooth normals from density gradient
   ↓
5. Calculate per-vertex material blend data
   ↓
6. Generate LOD meshes for distant chunks
   ↓
7. Apply triplanar material shader
   ↓
   RESULT: Seamless, solid-looking terrain!
```

---

## Checklist

- [x] Chunk boundaries have no visible seams (neighbor data sampling)
- [x] Both chunks generate matching vertices at boundary (iteration starts at -1)
- [ ] Textures don't stretch on vertical surfaces (needs triplanar shader)
- [ ] Lighting is smooth across the terrain (needs normal averaging)
- [ ] Material transitions blend naturally (needs vertex attributes)
- [ ] Distant chunks use lower-resolution meshes (needs LOD - Epic 9)
- [x] Terrain looks like solid rock, not paper (with neighbor data)

---

## Acceptance Criteria

- [x] No visible seams at chunk boundaries
- [ ] Rock looks like rock (no stretching) - needs triplanar
- [x] Smooth lighting (no faceting) - `CalculateSmoothNormalsJob` implemented
- [x] Ore veins blend nicely with rock - vertex colors + multi-material shader
- [ ] 60+ FPS with LOD system active - DEFERRED to Epic 9.2

---

## Implemented Components

### Shaders
Location: `Assets/Scripts/Voxel/Shaders/`

| Shader | Description | Key Features |
|--------|-------------|--------------|
| `VoxelTriplanar.shader` | Single-material triplanar | Projects texture from 3 axes based on normal, URP PBR lighting, normal maps |
| `VoxelTriplanarMultiMaterial.shader` | Multi-material triplanar | Blends 3 materials (dirt/stone/ore) based on vertex color R channel |

### Jobs
Location: `Assets/Scripts/Voxel/Meshing/`

| Job | Description | Performance |
|-----|-------------|-------------|
| `CalculateSmoothNormalsJob` | Gradient-based normal calculation | Burst-compiled, trilinear density sampling |
| `GenerateMarchingCubesMeshJob` | Marching Cubes with materials | Outputs positions, normals, colors, indices |

### System Methods
Location: `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs`

| Method | Description |
|--------|-------------|
| `GetNeighborBlobs()` | Fetches 6 adjacent chunk VoxelBlobs via ChunkLookup |
| `GetDensityWithNeighbors()` | Samples density from neighbor or uses smart fallback |
| `GetMaterialWithNeighbors()` | Samples material with edge extension fallback |

---

## Integration Guide

### 1. Using the Triplanar Shader
```csharp
// In ChunkMeshingSystem.OnCreate():
var shader = Shader.Find("DIG/VoxelTriplanar");
material.SetFloat("_Scale", 0.25f);       // Texture tiling
material.SetFloat("_BlendSharpness", 4f); // Normal blend sharpness
```

### 2. Enabling Smooth Normals
```csharp
// In ChunkMeshingSystem (already enabled by default):
private const bool USE_SMOOTH_NORMALS = true;

// After Marching Cubes job:
var smoothNormalsJob = new CalculateSmoothNormalsJob
{
    Densities = paddedDensities,
    Vertices = vertices.AsArray(),
    VertexScale = VERTEX_SCALE,
    Normals = smoothNormals
};
smoothNormalsJob.Schedule().Complete();
```

### 3. Material Blending Setup
```csharp
// Materials passed to job:
var job = new GenerateMarchingCubesMeshJob
{
    Densities = paddedDensities,
    Materials = paddedMaterials, // Material IDs (0=air, 1=dirt, 2=stone, 3+=ores)
    // ...
    Colors = colors, // Output: vertex colors for shader
};

// Shader reads vertex color R channel:
// 0.0 = dirt, 0.5 = stone, 1.0 = ore
```

---

## Event Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                     Seamless Meshing Pipeline                    │
├─────────────────────────────────────────────────────────────────┤
│  ChunkMeshingSystem.ProcessChunk()                              │
│    ├─ GetNeighborBlobs() - Fetch 6 adjacent chunks              │
│    ├─ Fill paddedDensities (34³) with neighbor data             │
│    ├─ Fill paddedMaterials (34³) for material blending          │
│    │                                                             │
│    ├─ GenerateMarchingCubesMeshJob (Burst)                      │
│    │    ├─ Iterate cubes from -1 to 31 (not 0 to 31)            │
│    │    ├─ Output: Vertices, Normals, Colors, Indices           │
│    │    └─ MaterialIdToColor() encodes material in vertex color │
│    │                                                             │
│    ├─ CalculateSmoothNormalsJob (Burst, optional)               │
│    │    └─ Calculate normals from density gradient              │
│    │                                                             │
│    └─ Create Mesh with colors for material blending             │
└─────────────────────────────────────────────────────────────────┘
```

---

## File Listing

| File | Description |
|------|-------------|
| `Assets/Scripts/Voxel/Shaders/VoxelTriplanar.shader` | Single-material triplanar shader (URP) |
| `Assets/Scripts/Voxel/Shaders/VoxelTriplanarMultiMaterial.shader` | Multi-material triplanar with blending |
| `Assets/Scripts/Voxel/Meshing/CalculateSmoothNormalsJob.cs` | Burst job for gradient normals |
| `Assets/Scripts/Voxel/Meshing/GenerateMarchingCubesMeshJob.cs` | Updated with Materials/Colors |
| `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs` | Neighbor fetching, smooth normals integration |
| `Assets/Scripts/Voxel/Systems/ChunkLookupSystem.cs` | O(1) chunk lookup for neighbor access |

---

## Shader Properties Reference

### VoxelTriplanar.shader
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `_MainTex` | Texture2D | white | Main albedo texture |
| `_NormalMap` | Texture2D | bump | Normal map (optional) |
| `_Scale` | Float | 0.25 | World-space texture scale |
| `_BlendSharpness` | Range(1,8) | 4.0 | Normal-based blend sharpness |
| `_Color` | Color | white | Tint color |
| `_Smoothness` | Range(0,1) | 0.1 | PBR smoothness |
| `_Metallic` | Range(0,1) | 0.0 | PBR metallic |

### VoxelTriplanarMultiMaterial.shader
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `_DirtTex` | Texture2D | white | Dirt/surface texture |
| `_StoneTex` | Texture2D | gray | Stone/rock texture |
| `_OreTex` | Texture2D | white | Ore/mineral texture |
| `_DirtColor` | Color | (0.6, 0.45, 0.3) | Dirt tint |
| `_StoneColor` | Color | (0.5, 0.5, 0.5) | Stone tint |
| `_OreColor` | Color | (0.8, 0.7, 0.2) | Ore tint |

---

## Testing

1. **Visual Test - Triplanar**:
   - Assign `DIG/VoxelTriplanar` shader to terrain material
   - Assign a rock texture to `_MainTex`
   - Verify texture doesn't stretch on vertical surfaces

2. **Visual Test - Smooth Normals**:
   - Toggle `USE_SMOOTH_NORMALS` in ChunkMeshingSystem
   - Compare lighting: smooth vs flat-shaded

3. **Visual Test - Material Blending**:
   - Use `DIG/VoxelTriplanarMultiMaterial` shader
   - Assign different textures to dirt/stone/ore
   - Verify smooth transitions at material boundaries

---

## Acceptance Criteria

- [x] No visible seams at chunk boundaries
- [x] Textures project correctly on all surface angles (triplanar)
- [x] Lighting is smooth across surfaces (gradient normals)
- [x] Materials blend at boundaries (vertex colors)
- [x] All jobs are Burst-compiled
- [ ] 60+ FPS with LOD (DEFERRED to Epic 9.2)
