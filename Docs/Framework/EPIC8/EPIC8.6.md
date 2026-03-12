# EPIC 8.6: Marching Cubes Collision

**Status**: ✅ COMPLETED  
**Priority**: MEDIUM  
**Dependencies**: EPIC 8.5 (Marching Cubes Meshing)
**Estimated Time**: 1 day

---

## Goal

Enable physics collision for the Marching Cubes terrain using both:
1. **Unity.Physics (DOTS)**: For ECS-based character controllers and raycasts.
2. **UnityEngine.MeshCollider (Hybrid)**: For standard Rigidbody/CharacterController compatibility.

---

## Quick Start Guide

### 1. Scene Setup
1. Ensure your Voxel SubScene is configured (See Epic 8.1/8.2).
2. Play the scene.
3. Player spawns above Y=0.
4. **Verify**: Player stands on terrain surface (terrain is below Y=0).

### 2. Terrain Appearance
- **Default Material**: URP Lit shader with brown terrain color.
- **Custom Material**: See Integration Guide below.

### 3. Physics Debugger
1. Open **Window > Analysis > Physics Debugger**.
2. Enable "Collision Geometry".
3. **Verify**: Wireframe mesh appears over the terrain.

---

## Implemented Components

### PhysicsCollider (Unity.Physics)
- **Type**: `IComponentData` (Blob)
- **Location**: Added to entity via ECB when mesh is ready (NOT in archetype)
- **Purpose**: DOTS Physics raycasts and collisions
- **Note**: Only present on chunks with valid meshes (prevents null blob errors)

### MeshCollider (UnityEngine)
- **Type**: Unity Component on GameObject
- **Location**: `ChunkGameObject.Value.GetComponent<MeshCollider>()`
- **Purpose**: Standard Unity physics (CharacterController, Rigidbody)

### MeshRenderer Material
- **Shader**: `Universal Render Pipeline/Lit`
- **Material Name**: "VoxelTerrain"
- **Settings**:
  | Property | Value | Purpose |
  |----------|-------|---------|
  | `_BaseColor` | Brown (0.5, 0.4, 0.3) | Default terrain color |
  | `_Smoothness` | 0.1 | Rough, non-shiny surface |
  | `_Metallic` | 0.0 | Non-metallic terrain |
  | `_Cull` | 0 (Off) | Double-sided rendering |
  | `enableInstancing` | true | GPU Instancing for batching |

### GameObject Configuration
- **Layer**: Default (TODO: Create "Terrain" layer)
- **Static**: `true` (enables static batching)
- **Shadow Casting**: On
- **Receive Shadows**: Yes
- **Motion Vectors**: Camera-based

---

## Implemented Systems

### ChunkMeshingSystem (Updated)
Location: `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs`
- **Role**: Generates both visual mesh AND physics colliders
- **Logic**:
    1. Generates mesh vertices/indices from Marching Cubes job.
    2. Creates `Unity.Physics.MeshCollider` blob from vertices/triangles.
    3. **Adds** `PhysicsCollider` + `PhysicsWorldIndex` via ECB (only when ready).
    4. Creates GameObject with `UnityEngine.MeshCollider` + URP material.

### ChunkSpawnerSystem (Updated)
Location: `Assets/Scripts/Voxel/Systems/Generation/ChunkSpawnerSystem.cs`
- **Changes**:
    - Only includes `LocalTransform`, `LocalToWorld` (NO PhysicsCollider in archetype).
    - Sets `LocalTransform` from chunk world position.
    - Only spawns chunks where `chunkPos.y < 0` (terrain below ground).

---

## Integration Guide

### 1. For DOTS Physics (ECS Characters)
Your ECS character controller should use `PhysicsWorld.CastRay()` or similar.
Chunks are automatically registered in the default `PhysicsWorld`.

### 2. For Standard Unity Physics
The `ChunkGameObject` has a `MeshCollider` component.
Standard `Physics.Raycast()` and `CharacterController.Move()` will work.

### 3. Collision Layers
Currently all chunks use `CollisionFilter.Default`.
To customize:
1. Open `ChunkMeshingSystem.cs`.
2. Find the `MeshCollider.Create()` call.
3. Replace `CollisionFilter.Default` with your custom filter.

### 4. Custom Terrain Material (URP)
The default is a simple brown URP Lit material.
To use your own material:
1. Open `ChunkMeshingSystem.cs`.
2. Find the `Shader.Find("Universal Render Pipeline/Lit")` section.
3. Replace with:
```csharp
var mat = Resources.Load<Material>("YourTerrainMaterial");
mr.material = mat;
```
Or for triplanar shading (Epic 8.10):
```csharp
var shader = Shader.Find("Custom/TriplanarTerrain");
```

---

## Event Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                        Collision Pipeline                       │
├─────────────────────────────────────────────────────────────────┤
│  ChunkMeshingSystem.GenerateMeshData()                          │
│    ├─ Marching Cubes Job → Vertices + Indices                   │
│    ├─ new Mesh() → Visual Mesh                                  │
│    └─ MeshCollider.Create() → DOTS Physics Blob                 │
│                                                                 │
│  ChunkMeshingSystem.AssignMeshToChunk()                         │
│    ├─ Create/Update GameObject                                  │
│    └─ Assign mesh to MeshFilter + UnityEngine.MeshCollider      │
│                                                                 │
│  ECB.Playback()                                                 │
│    └─ SetComponent(PhysicsCollider) → Entity                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## Testing

1.  **Drop Test**:
    *   Create a sphere with Rigidbody at Y=50.
    *   Play scene.
    *   **Verify**: Sphere lands on terrain.

2.  **Walk Test**:
    *   Use a CharacterController prefab.
    *   Move around.
    *   **Verify**: No falling through ground.

3.  **Raycast Test**:
    ```csharp
    if (Physics.Raycast(Camera.main.transform.position, Vector3.down, out var hit, 100f))
    {
        Debug.Log($"Hit terrain at {hit.point}");
    }
    ```

4.  **DOTS Raycast Test**:
    ```csharp
    var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
    if (physicsWorld.CastRay(new RaycastInput { Start = pos, End = pos + dir * 100, Filter = CollisionFilter.Default }, out var hit))
    {
        Debug.Log($"DOTS Hit at {hit.Position}");
    }
    ```

---

## Performance Notes (MVP)

⚠️ **Current Implementation is NOT Optimized** (Epic 9 will address):

| Issue | Current | Best Practice |
|-------|---------|---------------|
| Job Completion | `Schedule().Complete()` (Blocking) | Async with double-buffering |
| Memory | Per-chunk allocation | Pooled NativeArrays |
| Mesh Upload | Main thread `SetVertices` | `MeshDataArray` (async) |
| Batch Size | 1 chunk/frame | Multiple chunks parallel |

**This is intentional for MVP**. Correctness first, optimization later.

### Current Configuration
| Setting | Value | Location |
|---------|-------|----------|
| `SPAWN_RADIUS` | 3 | `ChunkSpawnerSystem.cs` |
| `MAX_SPAWNS_PER_FRAME` | 1 | `ChunkSpawnerSystem.cs` |
| Material | Cached URP Lit | `ChunkMeshingSystem.cs` |
| Terrain Below | `chunkPos.y < 0` | `ChunkSpawnerSystem.cs` |

### Performance Tips
- **Increase `MAX_SPAWNS_PER_FRAME`** for faster loading (causes frame spikes).
- **Increase `SPAWN_RADIUS`** for larger view distance (more chunks).
- **Disable meshing on servers**: Check for headless mode in `ChunkMeshingSystem.OnCreate()`.

---

## Acceptance Criteria

- [x] Chunks have `PhysicsCollider` component (added when mesh ready)
- [x] `MeshCollider` blob is created from generated mesh
- [x] Standard `UnityEngine.MeshCollider` is also updated
- [x] Archetype includes `LocalTransform` for correct physics positioning
- [x] No structural change errors during meshing
- [x] No null blob exceptions (PhysicsCollider added only when valid)
- [x] URP material applied (no pink chunks)
- [x] Terrain spawns below Y=0 (player above ground)
- [x] Cached material for performance

---

## Files Modified

| File | Changes |
|------|---------|
| `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs` | Physics Collider, cached URP material, ECB pattern |
| `Assets/Scripts/Voxel/Systems/Generation/ChunkSpawnerSystem.cs` | Y<0 filter, reduced spawn rate |
| `Assets/Scripts/Voxel/Meshing/MarchingCubesTables.cs` | Fixed corrupted lookup table data |

## Technical Notes

### Physics Collider Disposal
When regenerating a chunk's mesh (e.g., after mining), we cannot immediately `Dispose()` the old `PhysicsCollider.Values.Blob` because the Physics engine might still be using it in the current simulation step. 
**Solution**: We use a `PhysicsColliderCleanupBuffer` to defer the disposal to the **start** of the next frame (via `ChunkMemoryCleanupSystem` running in `FixedStepSimulationSystemGroup` after Physics), ensuring thread safety and preventing `InvalidOperationException` crashes.

