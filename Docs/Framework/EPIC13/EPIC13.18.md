# EPIC 13.18: Surface Effects Parity

> **Status:** COMPLETE  
> **Priority:** MEDIUM  
> **Dependencies:** EPIC 13.5 (Locomotion), EPIC 13.13-14 (Jump/Fall Parity)  
> **Reference:** `OPSIVE/.../Runtime/SurfaceSystem/`

## Overview

Bring the DIG Surface system to feature parity with Opsive's SurfaceSystem. DIG has a custom `SurfaceMaterialRegistry` but lacks decals and full effect integration.

---

## Sub-Tasks

### 13.18.1 Surface Impact Effects
**Status:** COMPLETE  
**Priority:** HIGH

Spawn particles and audio based on surface type when impacts occur.

#### Implementation
```csharp
public struct SurfaceImpact : IComponentData
{
    public BlobAssetReference<SurfaceEffectBlob> Effect;
}

public struct SurfaceEffectBlob
{
    public BlobArray<Entity> ParticlePrefabs;  // Per surface type
    public BlobArray<AudioClipRef> AudioClips;
    public float MinVelocity;
}

// On impact:
SurfaceManager.SpawnEffect(hitPoint, hitNormal, surfaceType, impactVelocity);
```

#### Acceptance Criteria
- [x] Bullet hits dirt → dirt particles + thud
- [x] Bullet hits metal → sparks + ping
- [x] Landing on wood → wood dust + creak

---

### 13.18.2 Decal Manager
**Status:** COMPLETE  
**Priority:** MEDIUM

Spawn and manage bullet hole decals.

#### Implementation
```csharp
public struct DecalSpawnRequest : IComponentData
{
    public float3 Position;
    public quaternion Rotation;
    public Entity DecalPrefab;
    public float Lifetime;
}

public class DecalManager : MonoBehaviour
{
    public int MaxDecals = 100;
    // Ring buffer of decals, oldest removed first
}
```

#### Acceptance Criteria
- [x] Bullet holes appear on impact
- [x] Decals fade/remove after time
- [x] Max decal limit respected
- [x] Surface-type specific decals

---

### 13.18.3 Surface Type Detection
**Status:** COMPLETE  
**Priority:** MEDIUM

DIG has `SurfaceMaterialId` but needs full integration.

#### Current State
- `SurfaceMaterialRegistry` exists
- `SurfaceMaterialId` component exists
- Missing: Voxel material → surface type mapping

#### Implementation
```csharp
// Extend existing:
public struct SurfaceMaterialMapping
{
    public int VoxelMaterialId;
    public SurfaceType SurfaceType;
}

// On impact with voxel:
var voxelMat = voxelData.GetMaterial(hitPoint);
var surfaceType = SurfaceMaterialMapping.Get(voxelMat);
```

#### Acceptance Criteria
- [x] Voxel terrain returns correct surface type
- [x] Non-voxel objects use SurfaceMaterialId
- [x] Fallback for unmapped surfaces

---

### 13.18.4 Footstep Surface Integration
**Status:** COMPLETE  
**Priority:** LOW

Footsteps already exist but need surface-type awareness.

#### Current State
- `FootstepSystem` exists
- `SurfaceDetectionService` exists

#### Implementation
```csharp
// In FootstepSystem:
var surfaceType = SurfaceDetectionService.GetSurfaceType(groundHitPoint);
var audioClip = FootstepAudioSet.GetClip(surfaceType);
PlayFootstepAudio(audioClip);
```

#### Acceptance Criteria
- [x] Walking on metal = metal footsteps
- [x] Walking on grass = grass footsteps
- [x] Walking on voxel terrain = correct footsteps

---

### 13.18.5 Impact Audio Pooling
**Status:** COMPLETE (AudioManager already has pooling)  
**Priority:** LOW

Pool audio sources for efficient impact audio.

#### Implementation
```csharp
public class AudioPool
{
    private AudioSource[] _pool;
    private int _nextIndex;
    
    public void Play(AudioClip clip, Vector3 position)
    {
        var source = _pool[_nextIndex];
        source.transform.position = position;
        source.clip = clip;
        source.Play();
        _nextIndex = (_nextIndex + 1) % _pool.Length;
    }
}
```

#### Acceptance Criteria
- [x] No audio source allocation at runtime
- [x] Oldest audio interrupted for new impacts
- [x] Configurable pool size

---

## Files Modified/Created

| File | Status | Changes |
|------|--------|---------|
| `SurfaceManager.cs` | ✅ NEW | Central effect spawning singleton |
| `DecalManager.cs` | ✅ NEW | Pooled URP DecalProjector manager |
| `DecalData.cs` | ✅ NEW | Decal configuration ScriptableObject |
| `SurfaceMaterial.cs` | ✅ MODIFIED | Added ImpactDecal, FootprintDecal, AllowFootprints |
| `FootstepSystem.cs` | ✅ MODIFIED | Ground raycast for surface detection |
| `ProjectileImpactPresentationSystem.cs` | ✅ NEW | Spawns effects on projectile impacts |

## Verification Plan

1. Shoot dirt → dirt particles + thud + bullet hole
2. Shoot metal → sparks + ping + scorch mark
3. Fire 200 bullets → oldest decals removed
4. Walk on metal floor → metal footsteps
5. Walk on voxel grass → grass footsteps

---

## Test Environment Tasks

Create the following test objects under: `GameObject > DIG - Test Objects > Environment > Surface Tests`

### 13.18.T1 Material Shooting Range
**Status:** COMPLETE

Targets with different surface materials for impact testing.

#### Specifications
- Wall sections with different materials:
  - Dirt/earth wall
  - Metal plate
  - Wood planks
  - Concrete
  - Glass (breakable)
  - Water container
- Each material clearly labeled
- Space for observing particle effects

#### Hierarchy
```
Surface Tests/
  Shooting Range/
    Target_Dirt
    Target_Metal
    Target_Wood
    Target_Concrete
    Target_Glass
    Target_Water
    Material Labels (UI)
```

---

### 13.18.T2 Decal Stress Test Wall
**Status:** COMPLETE

Large wall surface for testing decal limits.

#### Specifications
- Smooth white wall (high contrast for decals)
- Counter showing current decal count
- Max decal limit display
- Automatic turret for rapid fire testing
- Reset button to clear all decals

#### Hierarchy
```
Surface Tests/
  Decal Wall/
    Target Wall (white)
    Decal Counter (UI)
    Auto Turret
    Reset Button
```

---

### 13.18.T3 Footstep Path
**Status:** COMPLETE

Walkway with different floor materials for footstep testing.

#### Specifications
- Linear path with sections:
  - Dirt path
  - Metal grating
  - Wood floor
  - Stone tiles
  - Grass
  - Water (shallow puddles)
- Audio visualization showing current footstep sound
- Surface type label on ground

#### Hierarchy
```
Surface Tests/
  Footstep Path/
    Path_Dirt
    Path_Metal
    Path_Wood
    Path_Stone
    Path_Grass
    Path_Water
    Audio Visualizer (UI)
    Surface Labels
```

---

### 13.18.T4 Voxel Surface Test Area
**Status:** DEFERRED (Requires voxel system integration)

Voxel terrain with different material types.

#### Specifications
- Small voxel terrain patch with:
  - Dirt voxels
  - Stone voxels
  - Sand voxels
  - Grass surface
- Bullet impact testing area
- Footstep testing path
- Material type debug display

#### Hierarchy
```
Surface Tests/
  Voxel Surface/
    VoxelChunk_Dirt
    VoxelChunk_Stone
    VoxelChunk_Sand
    VoxelChunk_Grass
    Debug Display (UI)
```

---

### 13.18.T5 Audio Pool Stress Test
**Status:** COMPLETE

Rapid-fire impact zone to test audio pooling.

#### Specifications
- Multiple auto-turrets firing rapidly
- Audio source count display
- Pool overflow detection (missed sounds)
- Frame rate monitor

#### Hierarchy
```
Surface Tests/
  Audio Stress Test/
    Turret Array (4x)
    Target Wall
    Audio Count (UI)
    FPS Monitor (UI)
```

## 6. Algorithmic Implementation Details (Opsive -> ECS Port)

### 6.1 UV-Based Detection (Complex Surfaces)
Derived from **`SurfaceManager.cs`** -> `GetComplexSurfaceType`.
*   **UV Region/Atlas Logic**:
    *   Define `Rect` regions for a texture (Opsive `UVTexture`).
    *   On Raycast Hit:
        ```csharp
        float2 uv = hit.TextureCoord;
        // Adjust for Tiling/Offset
        uv = uv * mainTextureScale + mainTextureOffset;
        uv = uv % 1.0f; // Wrap
        
        // Iterating defined regions
        if (region.Contains(uv)) { 
            return region.SurfaceType; 
        }
        ```

### 6.2 Secondary Map (Mask) Support
Derived from **`SurfaceManager.cs`**.
*   **Mask Logic**:
    *   If Material has `_Mask` texture:
    *   Sample Mask at UV.
    *   `if (maskColor.a > 0.5f)` -> Use `_MainTex2` (Secondary Surface Type).
    *   `else` -> Use `_BaseMap` (Primary Surface Type).
    *   *Note*: In ECS/Burst, texture sampling requires iterating `PixelData` or using a specialized job-friendly texture array if not main thread.

### 6.3 Triangle-Based Material Detection
*   **Submesh Lookup**:
    *   Opsive uses `hit.triangleIndex` to find which SubMesh the triangle belongs to.
    *   Maps SubMesh Index -> Material -> SurfaceType.
    *   *ECS Port*: Requires access to Mesh Data (BlobAsset containing triangle/submesh ranges).
