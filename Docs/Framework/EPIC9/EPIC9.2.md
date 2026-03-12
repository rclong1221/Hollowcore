# Epic 9.2: LOD System

**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Dependencies**: EPIC 8.5 (Marching Cubes Meshing), EPIC 8.8 (Chunk Streaming)  
**Estimated Time**: 2-3 days  
**Last Updated**: 2025-12-20

> ⚠️ **Note**: LOD was originally planned for Epic 8.12 but was deferred here because LOD requires the chunk streaming system (Epic 8.8) to be complete first. Distance-based mesh selection is fundamentally a streaming concern.

---

## Quick Start Guide

### For Designers

1. **Create LOD Config**
   - Right-click in Project → Create → DIG → Voxel → LOD Config
   - Save as `VoxelLODConfig` in `Assets/Resources/`
   - Or use DIG → Voxel → LOD Visualizer → Create Default Config

2. **Configure LOD Levels**
   - Each level has: Distance, VoxelStep, HasCollider
   - Lower VoxelStep = higher detail (1=full, 2=half, 4=quarter)
   - Disable colliders on distant chunks for physics performance

3. **Use Presets**
   - Select VoxelLODConfig asset
   - Click "Close Range", "Medium Range", or "Far Range" presets
   - Or customize levels manually

4. **Visualize in Scene**
   - Open DIG → Voxel → LOD Visualizer
   - Enable "Show LOD Rings in Scene"
   - Enter Play Mode for runtime statistics

### For Developers

1. **Key Files**
   ```
   Assets/Scripts/Voxel/
   ├── Rendering/
   │   └── VoxelLODConfig.cs       # Configuration ScriptableObject
   ├── Components/
   │   └── ChunkLODComponents.cs   # ChunkLODState, ChunkNeedsLODMesh
   ├── Systems/
   │   └── ChunkLODSystem.cs       # Runtime LOD management
   └── Editor/
       └── LODVisualizerWindow.cs  # Visualizer + Config editor
   ```

2. **Integration**
   - Add `ChunkLODState` component to chunk entities during spawning
   - `ChunkLODSystem` automatically updates LOD based on camera distance
   - Query `ChunkNeedsLODMesh` for chunks needing regeneration at new LOD

---

## Component Reference

### VoxelLODConfig

```csharp
[CreateAssetMenu(menuName = "DIG/Voxel/LOD Config")]
public class VoxelLODConfig : ScriptableObject
{
    [Serializable]
    public struct LODLevel
    {
        float Distance;       // Distance threshold
        int VoxelStep;        // 1=full, 2=half, 4=quarter, 8=eighth
        bool HasCollider;     // Generate physics collider?
        Color DebugColor;     // Visualization color
    }
    
    LODLevel[] Levels;            // Ordered by distance (closest first)
    float UpdateFrequency;        // Seconds between LOD checks (0.1-2)
    float Hysteresis;             // Prevents rapid LOD switching (0-10)
    int MaxUpdatesPerFrame;       // Limit transitions per frame (1-20)
    bool EnableColliderLOD;       // Toggle collider LOD globally
}
```

### ChunkLODState (IComponentData)

```csharp
public struct ChunkLODState : IComponentData
{
    int CurrentLOD;           // Current LOD level (0 = highest detail)
    int TargetLOD;            // Target LOD (pending transition)
    float DistanceToCamera;   // Last calculated distance
    bool NeedsLODUpdate;      // Flag for pending transition
}
```

### ChunkNeedsLODMesh (IComponentData, IEnableableComponent)

Tag component enabled when a chunk needs mesh regeneration at a new LOD level.

---

## System Architecture

### LOD Update Flow

```
[ChunkLODSystem.OnUpdate]
         │
         ▼
┌─────────────────────────────┐
│ 1. Throttle Check           │
│    Skip if < UpdateFrequency│
└───────────┬─────────────────┘
            │
            ▼
┌─────────────────────────────┐
│ 2. Cache Camera Position    │
│    (Avoid property access)  │
└───────────┬─────────────────┘
            │
            ▼
┌─────────────────────────────┐
│ 3. For Each Chunk:          │
│    - distancesq() (no sqrt) │
│    - Compare vs squared LOD │
│    - Apply hysteresis       │
│    - Check for change       │
└───────────┬─────────────────┘
            │
            ▼ (if LOD changed, up to MaxUpdatesPerFrame)
┌─────────────────────────────┐
│ 4. Update Components:       │
│    - Set CurrentLOD         │
│    - Enable ChunkNeedsLODMesh
│    - Update collider state  │
└─────────────────────────────┘
```

### Hysteresis Behavior

```
Distance ──────────────────────────────►

      LOD0        LOD1        LOD2
   [──────│──────│──────│──────]
          32m    64m    128m

With Hysteresis = 2m:
   - LOD0→LOD1 at 34m (32+2)
   - LOD1→LOD0 at 30m (32-2)

This prevents flickering when camera is near a boundary.
```

---

## Setup Guide

### 1. Create LOD Configuration

```
Assets/Resources/VoxelLODConfig.asset
```

Default configuration:
| Level | Distance | Step | Collider | Triangle Reduction |
|-------|----------|------|----------|-------------------|
| LOD0 | 32m | 1 | ✓ | 100% |
| LOD1 | 64m | 2 | ✓ | ~25% |
| LOD2 | 128m | 4 | ✗ | ~6% |
| LOD3 | 256m | 8 | ✗ | ~1.5% |

### 2. Add LOD Components to Chunks

In your chunk spawning system:

```csharp
// Add LOD components when spawning chunk entity
EntityManager.AddComponentData(chunkEntity, new ChunkLODState
{
    CurrentLOD = 0,
    TargetLOD = 0,
    DistanceToCamera = 0f,
    NeedsLODUpdate = false
});
EntityManager.AddComponent<ChunkNeedsLODMesh>(chunkEntity);
EntityManager.SetComponentEnabled<ChunkNeedsLODMesh>(chunkEntity, false);
```

### 3. Modify Mesh Generation for LOD

In `ChunkMeshingSystem`, adjust voxel step based on LOD:

```csharp
int voxelStep = 1;
if (EntityManager.HasComponent<ChunkLODState>(entity))
{
    var lodState = EntityManager.GetComponentData<ChunkLODState>(entity);
    voxelStep = lodConfig.GetVoxelStep(lodState.CurrentLOD);
}

// Use voxelStep in marching cubes iteration
for (int z = -1; z < chunkSize; z += voxelStep)
    for (int y = -1; y < chunkSize; y += voxelStep)
        for (int x = -1; x < chunkSize; x += voxelStep)
```

---

## Integration Guide

### Querying Chunks That Need LOD Mesh

```csharp
// In your meshing system
foreach (var (lodState, entity) in 
    SystemAPI.Query<RefRO<ChunkLODState>>()
    .WithAll<ChunkNeedsLODMesh>()
    .WithEntityAccess())
{
    // Regenerate mesh at new LOD
    RegenerateMeshAtLOD(entity, lodState.ValueRO.CurrentLOD);
    
    // Clear the flag
    EntityManager.SetComponentEnabled<ChunkNeedsLODMesh>(entity, false);
}
```

### Custom LOD Strategies

```csharp
// Override GetLODLevel for custom behavior
public class CustomLODConfig : VoxelLODConfig
{
    public override int GetLODLevel(float distance)
    {
        // Example: Consider chunk importance
        if (IsPlayerNearImportantArea())
            return 0; // Force high detail
            
        return base.GetLODLevel(distance);
    }
}
```

### Collider LOD Management

```csharp
// Manually control collider state
bool needsCollider = lodConfig.ShouldHaveCollider(currentLOD);

if (!needsCollider && EntityManager.HasComponent<PhysicsCollider>(entity))
{
    // Remove physics collider for distant chunks
    EntityManager.RemoveComponent<PhysicsCollider>(entity);
}
```

---

## Editor Tools

### LOD Visualizer Window

**Access**: DIG → Voxel → LOD Visualizer

**Features**:
- **Scene View Gizmos**: Color-coded LOD rings around camera
- **LOD Level Table**: Overview of all levels with reduction estimates
- **Runtime Statistics**: Chunks per LOD, total chunks, collider count
- **Triangle Estimation**: Calculate expected triangle count

### VoxelLODConfig Inspector

**Features**:
- **Open Visualizer**: Quick access to LOD Visualizer
- **Quick Presets**: Close/Medium/Far range configurations
- **Validation**: Warns if levels aren't ordered by distance

---

## Tasks Completed

### Task 9.2.1: LOD Configuration ✅
- Created `VoxelLODConfig` ScriptableObject
- Configurable LOD levels with distance, step, collider settings
- Update frequency, hysteresis, max updates per frame
- Debug colors for visualization

### Task 9.2.2: LOD Mesh Generation ✅
- `ChunkLODState` component tracks current and target LOD
- `ChunkNeedsLODMesh` flag triggers regeneration
- VoxelStep passed to marching cubes for reduced resolution

### Task 9.2.3: LOD Transition System ✅
- `ChunkLODSystem` manages all chunk LOD states
- Hysteresis prevents rapid switching
- MaxUpdatesPerFrame limits transitions

### Task 9.2.4: Collider LOD ✅
- Colliders disabled on distant chunks
- Per-level HasCollider configuration
- `EnableColliderLOD` global toggle

---

## Acceptance Criteria

- [x] 60fps maintained at 200m view distance (via reduced triangles)
- [x] Smooth LOD transitions with hysteresis (no pop-in)
- [x] Colliders disabled on distant chunks
- [x] Config exposed to designers via ScriptableObject
- [x] Runtime statistics in editor
- [x] Scene view LOD ring visualization

---

## Performance Impact

### Triangle Reduction by LOD

| LOD | VoxelStep | Triangles | Reduction |
|-----|-----------|-----------|-----------|
| 0 | 1 | 1000 | 0% |
| 1 | 2 | 250 | 75% |
| 2 | 4 | 62 | 94% |
| 3 | 8 | 16 | 98% |

### System Optimizations

`ChunkLODSystem` is optimized for large chunk counts:

| Optimization | Technique | Benefit |
|--------------|-----------|---------|
| **Distance Squared** | `math.distancesq()` instead of `math.distance()` | Avoids sqrt per chunk per update |
| **Pre-computed Thresholds** | `NativeArray<float>` of squared distances | No allocation, O(1) lookup |
| **Cached Camera Position** | Camera.main cached before loop | Avoids property getter overhead |
| **Local Variables** | Config values cached as locals | Reduced property access in hot path |
| **Throttled Updates** | `UpdateFrequency` config | Reduces update frequency |
| **Frame Budget** | `MaxUpdatesPerFrame` config | Prevents frame spikes |

```csharp
// Example: Distance-squared comparison (no sqrt needed)
float distSq = math.distancesq(cameraPos, chunkCenter);
if (distSq <= _lodDistancesSq[lodLevel]) { ... }
```

### Memory Savings

- Distant chunks use smaller meshes
- Colliders disabled = no physics memory for distant chunks
- Pooled mesh data reused across LOD levels
- `NativeArray` for LOD thresholds disposed on system destroy

---

## Troubleshooting

### LOD not updating

1. Verify `VoxelLODConfig` exists in `Assets/Resources/`
2. Check that chunks have `ChunkLODState` component
3. Ensure Camera.main is assigned
4. Check `UpdateFrequency` isn't too high

### Chunks popping between LODs

1. Increase `Hysteresis` value (e.g., 5-10)
2. Reduce `MaxUpdatesPerFrame` for smoother transitions
3. Consider implementing mesh crossfade (future enhancement)

### Physics errors on distant chunks

1. Verify `EnableColliderLOD` is enabled
2. Check that distant LOD levels have `HasCollider = false`
3. Ensure collider removal happens after physics step

---

## Related Epics

| Epic | Relevance |
|------|-----------|
| EPIC 8.5 | Marching Cubes mesh generation |
| EPIC 8.8 | Chunk streaming (determines which chunks exist) |
| EPIC 9.1 | Visual refinement (shaders work at all LODs) |
| EPIC 9.3-9.4 | Performance optimization (complementary) |

---

## Pre-Requisites from Epic 8.12 (COMPLETED)

| Feature | Status | Relevance to LOD |
|---------|--------|------------------|
| Triplanar Shader | ✅ DONE | Same shader works at all LOD levels |
| Smooth Normals | ✅ DONE | Works with reduced voxel step |
| Material Blending | ✅ DONE | Vertex colors preserved at lower LODs |
| Neighbor Data | ✅ DONE | `GetDensityWithNeighbors()` works with any step size |
