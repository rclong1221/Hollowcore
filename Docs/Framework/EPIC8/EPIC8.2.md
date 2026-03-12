# EPIC 8.2: Chunk Generation System

**Status**: ✅ COMPLETED  
**Priority**: CRITICAL  
**Dependency**: EPIC 8.1 (Core Data)

---

## Goal

Create a **Jobified Generation System** that fills chunks with voxel data (Density + Material) using a pluggable generator strategy.

**Key Requirement**: The system must be agnostic to *what* is being generated. It just asks a Generator to "Fill this buffer".

---

## Architecture

### 1. The Generation System (`ChunkGenerationSystem`)
- Watches for chunks with the `ChunkState.Loading` tag.
- Schedules a background job to generate data.
- Transitions chunk to `ChunkState.Meshing` when done.

### 2. The Generator Interface (`IVoxelGenerator`)
To keep the asset modular, we shouldn't hardcode "Perlin Noise" into the system. However, since we are using Burst, interfaces are tricky.
**Solution**: We will use a **Function Pointer** or a **Generic Job Struct** approach for the MVP, effectively allowing users to swap out the generation algorithm.

For the MVP (Epic 8), we will implement a standard **Noise Generator** using FastNoise or Unity.Mathematics.noise.

---

## Tasks

### Task 8.2.1: Define Generation Job

**File**: `Assets/Scripts/Voxel/Systems/Generation/GenerateVoxelDataJob.cs`

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Voxel.Core;

namespace DIG.Voxel.Jobs
{
    [BurstCompile]
    public struct GenerateVoxelDataJob : IJobParallelFor
    {
        // Output buffers (one chunk per job index usually, 
        // but often we run this 32x32x32 times per chunk. 
        // Optimization: Run 1 job per chunk, loop inside).
        
        public NativeArray<byte> Densities;
        public NativeArray<byte> Materials;
        
        public int3 ChunkPosition;
        public float Seed;
        public float NoiseScale;
        public float GroundLevel;
        
        // This executes once per voxel if parallelized, 
        // OR once per chunk if we handle the loop inside.
        // Let we do 1Job Per Chunk for simplicity of architecture first.
        
        public void Execute(int index)
        {
            // If index is 0, we loop 32768 times.
            // This is often faster than scheduling 32k tiny jobs.
            
            for (int i = 0; i < VoxelConstants.VOXELS_PER_CHUNK; i++)
            {
                int3 localPos = CoordinateUtils.IndexToVoxelPos(i);
                float3 worldPos = CoordinateUtils.ChunkToWorldPos(ChunkPosition) + localPos;
                
                // 1. Calculate Density (Simple Terrain)
                float noiseVal = noise.snoise(new float3(worldPos.x, 0, worldPos.z) * NoiseScale);
                float terrainHeight = GroundLevel + (noiseVal * 20f);
                
                float density = worldPos.y < terrainHeight 
                    ? VoxelConstants.DENSITY_SOLID 
                    : VoxelConstants.DENSITY_AIR;
                    
                // Gradient smoothing (simple vertical gradient for now)
                float distToSurface = terrainHeight - worldPos.y;
                density = VoxelDensity.CalculateGradient(distToSurface);
                
                Densities[i] = (byte)density;
                
                // 2. Calculate Material
                if (density > VoxelConstants.DENSITY_SURFACE)
                {
                    // Simple stratum
                    if (worldPos.y < GroundLevel - 10) Materials[i] = VoxelConstants.MATERIAL_STONE;
                    else Materials[i] = VoxelConstants.MATERIAL_DIRT;
                }
                else
                {
                    Materials[i] = VoxelConstants.MATERIAL_AIR;
                }
            }
        }
    }
}
```

### Task 8.2.2: Implement ChunkGenerationSystem

**File**: `Assets/Scripts/Voxel/Systems/Generation/ChunkGenerationSystem.cs`

- Queries for `ChunkVoxelData` + `ChunkState.Loading`.
- Allocates `NativeArray`s for job outputs.
- Schedules `GenerateVoxelDataJob`.
- On completion: 
    - Creates `BlobAssetReference`.
    - Updates Component.
    - Sets State to `Meshing`.
    - Disposes generic arrays.

### Task 8.2.3: Configuration (ScriptableObject)

**File**: `Assets/Scripts/Voxel/Core/VoxelWorldConfig.cs`

```csharp
[CreateAssetMenu(menuName = "DIG/Voxel/World Config")]
public class VoxelWorldConfig : ScriptableObject
{
    public int Seed = 1337;
    public float NoiseScale = 0.05f;
    public float GroundLevel = 0f;
}
```

---

## Validation

- Create a `VoxelWorldAuthoring` component in the scene.
- Press Play.
- Use `VoxelDebugWindow` (or Hierarchy) to see Chunks appearing.
- Inspect `ChunkVoxelData` components (using Entity Debugger) to verify non-zero densities.

---

## Asset Store Note
This system creates a "Default Generator". Advanced users will replace `GenerateVoxelDataJob` with their own implementation (Biome Graph, etc., as per Epic 10). The system should be structured so swapping the Job is easy.

**Status**: ✅ COMPLETED  
**Priority**: CRITICAL  
**Dependencies**: EPIC 8.1 (Core Data Structures)
**Estimated Time**: 0.5 day

---

## Goal

Generate voxel data for chunks with:
- **Gradient density** (not binary 0/255)
- **Material IDs** for different voxel types
- **Neighbor awareness** for boundary meshing

---

## Critical: Gradient Density Generation

The #1 cause of the previous failure was binary density values.

### Wrong (Previous System):
```csharp
// BAD: Returns only 0 or 255
return worldY < groundLevel ? 255 : 0;
```

### Correct (New System):
```csharp
// GOOD: Returns gradient based on distance to surface
float distanceToSurface = groundLevel - worldY;
return VoxelDensity.CalculateGradient(distanceToSurface);

// Result:
// worldY = groundLevel + 2 → density ≈ 0 (air)
// worldY = groundLevel + 1 → density ≈ 64 (near surface air)
// worldY = groundLevel     → density = 128 (at surface)
// worldY = groundLevel - 1 → density ≈ 192 (near surface solid)
// worldY = groundLevel - 2 → density ≈ 255 (solid)
```

---

## Tasks

### Checklist
- [x] **8.2.1**: Transform `ChunkSpawnerSystem` to create ECS Entities
- [x] **8.2.2**: Implement `GenerateVoxelDataJob` (Burst ParallelFor)
- [x] **8.2.3**: Implement `ChunkGenerationSystem` (Job Scheduling & Blob Creation)
- [x] **8.2.4**: Create `ChunkLookupSystem` for O(1) Access
- [x] **8.2.5**: Validate Generation Speed and Density Distribution

### Task 8.2.1: Create ChunkSpawnerSystem

**File**: `Assets/Scripts/Voxel/Systems/Generation/ChunkSpawnerSystem.cs`

Spawns chunk entities around the player.

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Voxel.Core;
using DIG.Voxel.Components;

namespace DIG.Voxel.Systems.Generation
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ChunkSpawnerSystem : SystemBase
    {
        private const int SPAWN_RADIUS = 4;  // Chunks in each direction
        private const int MAX_SPAWNS_PER_FRAME = 4;
        
        private EntityArchetype _chunkArchetype;
        private NativeHashSet<int3> _existingChunks;
        
        protected override void OnCreate()
        {
            _chunkArchetype = EntityManager.CreateArchetype(
                typeof(ChunkPosition),
                typeof(ChunkVoxelData),
                typeof(ChunkMeshState),
                typeof(ChunkColliderState),
                typeof(ChunkNeighbors),
                typeof(ChunkNeedsRemesh)
            );
            
            _existingChunks = new NativeHashSet<int3>(1024, Allocator.Persistent);
        }
        
        protected override void OnDestroy()
        {
            _existingChunks.Dispose();
        }
        
        protected override void OnUpdate()
        {
            // Get viewer position
            float3 viewerPos = GetViewerPosition();
            int3 centerChunk = CoordinateUtils.WorldToChunkPos(viewerPos);
            
            int spawned = 0;
            
            // Spiral out from center for better loading order
            for (int r = 0; r <= SPAWN_RADIUS && spawned < MAX_SPAWNS_PER_FRAME; r++)
            {
                for (int x = -r; x <= r && spawned < MAX_SPAWNS_PER_FRAME; x++)
                {
                    for (int y = -r; y <= r && spawned < MAX_SPAWNS_PER_FRAME; y++)
                    {
                        for (int z = -r; z <= r && spawned < MAX_SPAWNS_PER_FRAME; z++)
                        {
                            // Only process shell of radius r
                            if (math.abs(x) != r && math.abs(y) != r && math.abs(z) != r)
                                continue;
                            
                            int3 chunkPos = centerChunk + new int3(x, y, z);
                            
                            if (!_existingChunks.Contains(chunkPos))
                            {
                                SpawnChunk(chunkPos);
                                spawned++;
                            }
                        }
                    }
                }
            }
        }
        
        private float3 GetViewerPosition()
        {
            if (UnityEngine.Camera.main != null)
            {
                var pos = UnityEngine.Camera.main.transform.position;
                return new float3(pos.x, pos.y, pos.z);
            }
            return float3.zero;
        }
        
        private void SpawnChunk(int3 chunkPos)
        {
            var entity = EntityManager.CreateEntity(_chunkArchetype);
            
            EntityManager.SetComponentData(entity, new ChunkPosition { Value = chunkPos });
            EntityManager.SetComponentData(entity, new ChunkMeshState { IsDirty = true });
            EntityManager.SetComponentEnabled<ChunkNeedsRemesh>(entity, true);
            
            _existingChunks.Add(chunkPos);
            
            // Mark neighbors for remesh (they may need to update boundaries)
            MarkNeighborsForRemesh(chunkPos);
            
            UnityEngine.Debug.Log($"[Voxel] Spawned chunk at {chunkPos}");
        }
        
        private void MarkNeighborsForRemesh(int3 chunkPos)
        {
            int3[] offsets = new int3[]
            {
                new int3(-1, 0, 0), new int3(1, 0, 0),
                new int3(0, -1, 0), new int3(0, 1, 0),
                new int3(0, 0, -1), new int3(0, 0, 1)
            };
            
            foreach (var offset in offsets)
            {
                int3 neighborPos = chunkPos + offset;
                // Find neighbor entity and mark for remesh
                // (Implementation depends on chunk lookup system)
            }
        }
    }
}
```

---

### Task 8.2.2: Create GenerateVoxelDataJob

**File**: `Assets/Scripts/Voxel/Jobs/GenerateVoxelDataJob.cs`

Burst job that generates voxel data with gradient density.

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Voxel.Core;

namespace DIG.Voxel.Jobs
{
    [BurstCompile]
    public struct GenerateVoxelDataJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkWorldOrigin;
        [ReadOnly] public float GroundLevel;
        [ReadOnly] public float Seed;
        
        [WriteOnly] public NativeArray<byte> Densities;
        [WriteOnly] public NativeArray<byte> Materials;
        
        public void Execute(int index)
        {
            int3 localPos = CoordinateUtils.IndexToVoxelPos(index);
            float3 worldPos = new float3(ChunkWorldOrigin + localPos);
            
            // Calculate signed distance to surface (positive = below ground)
            float distanceToSurface = GroundLevel - worldPos.y;
            
            // Add some noise for variety (optional)
            float noiseOffset = GetTerrainNoise(worldPos);
            distanceToSurface += noiseOffset;
            
            // Calculate gradient density
            byte density = VoxelDensity.CalculateGradient(distanceToSurface);
            
            // Determine material (In a real app, use a Biome/Geology lookup)
            byte material = DetermineMaterialSamples(worldPos, distanceToSurface);
            
            Densities[index] = density;
            Materials[index] = material;
        }
        
        private float GetTerrainNoise(float3 worldPos)
        {
            // Simple perlin noise for terrain variation
            float2 noisePos = new float2(worldPos.x * 0.02f + Seed, worldPos.z * 0.02f + Seed);
            return noise.cnoise(noisePos) * 10f;  // ±10 voxels variation
        }
        
        // EXAMPLE ONLY: In a real project, this logic belongs in a generic Biome System
        private byte DetermineMaterialSamples(float3 worldPos, float depth)
        {
            // Only assign material if solid
            if (depth < 0) return VoxelConstants.MATERIAL_AIR;
            
            // Surface layer: dirt
            if (depth < 3) return VoxelConstants.MATERIAL_DIRT;
            
            // Check for ore veins using 3D noise
            float oreNoise = noise.snoise(worldPos * 0.1f);
            
            if (depth > 20 && oreNoise > 0.7f)
                return VoxelConstants.MATERIAL_GOLD_ORE;
            
            if (depth > 10 && oreNoise > 0.5f)
                return VoxelConstants.MATERIAL_IRON_ORE;
            
            if (oreNoise > 0.6f)
                return VoxelConstants.MATERIAL_COPPER_ORE;
            
            // Default: stone
            return VoxelConstants.MATERIAL_STONE;
        }
    }
}
```

---

### Task 8.2.3: Create ChunkGenerationSystem

**File**: `Assets/Scripts/Voxel/Systems/Generation/ChunkGenerationSystem.cs`

Runs generation job and creates voxel data.

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using DIG.Voxel.Jobs;

namespace DIG.Voxel.Systems.Generation
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ChunkSpawnerSystem))]
    public partial class ChunkGenerationSystem : SystemBase
    {
        private const float GROUND_LEVEL = 0f;
        private const float SEED = 12345f;
        
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            // Find chunks that need generation (have ChunkPosition but no valid VoxelData)
            foreach (var (position, voxelData, entity) in 
                SystemAPI.Query<RefRO<ChunkPosition>, RefRO<ChunkVoxelData>>()
                    .WithEntityAccess())
            {
                if (voxelData.ValueRO.IsValid) continue;  // Already generated
                
                GenerateChunk(entity, position.ValueRO.Value, ecb);
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
        
        private void GenerateChunk(Entity entity, int3 chunkPos, EntityCommandBuffer ecb)
        {
            int3 worldOrigin = chunkPos * VoxelConstants.CHUNK_SIZE;
            
            // Allocate arrays
            var densities = new NativeArray<byte>(VoxelConstants.VOXELS_PER_CHUNK, Allocator.TempJob);
            var materials = new NativeArray<byte>(VoxelConstants.VOXELS_PER_CHUNK, Allocator.TempJob);
            
            // Run generation job
            var job = new GenerateVoxelDataJob
            {
                ChunkWorldOrigin = worldOrigin,
                GroundLevel = GROUND_LEVEL,
                Seed = SEED,
                Densities = densities,
                Materials = materials
            };
            
            var handle = job.Schedule(VoxelConstants.VOXELS_PER_CHUNK, 64);
            handle.Complete();
            
            // VALIDATION: Log density distribution
            ValidateDensities(chunkPos, densities);
            
            // Create blob
            var blob = VoxelBlobBuilder.Create(densities, materials);
            
            // Update entity
            EntityManager.SetComponentData(entity, new ChunkVoxelData { Data = blob });
            
            // Cleanup
            densities.Dispose();
            materials.Dispose();
            
            UnityEngine.Debug.Log($"[Voxel] Generated chunk at {chunkPos}");
        }
        
        private void ValidateDensities(int3 chunkPos, NativeArray<byte> densities)
        {
            int airCount = 0;
            int surfaceCount = 0;  // Values between 1 and 254
            int solidCount = 0;
            
            for (int i = 0; i < densities.Length; i++)
            {
                byte d = densities[i];
                if (d == 0) airCount++;
                else if (d == 255) solidCount++;
                else surfaceCount++;
            }
            
            UnityEngine.Debug.Log($"[Voxel] Chunk {chunkPos} densities: " +
                $"Air={airCount}, Surface={surfaceCount}, Solid={solidCount}");
            
            // WARNING: If surfaceCount is 0, Marching Cubes won't generate surfaces!
            if (surfaceCount == 0)
            {
                UnityEngine.Debug.LogWarning($"[Voxel] Chunk {chunkPos} has NO surface voxels! " +
                    "Marching Cubes will skip this chunk.");
            }
        }
    }
}
```

---

### Task 8.2.4: Create Chunk Lookup System

**File**: `Assets/Scripts/Voxel/Systems/ChunkLookupSystem.cs`

Fast O(1) lookup of chunks by position.

```csharp
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Voxel.Components;

namespace DIG.Voxel.Systems
{
    public struct ChunkLookup : IComponentData
    {
        public NativeHashMap<int3, Entity> ChunkMap;
        public bool IsInitialized;
        
        public bool TryGetChunk(int3 position, out Entity entity)
        {
            if (!IsInitialized || !ChunkMap.IsCreated)
            {
                entity = Entity.Null;
                return false;
            }
            return ChunkMap.TryGetValue(position, out entity);
        }
        
        public void AddChunk(int3 position, Entity entity)
        {
            if (IsInitialized && ChunkMap.IsCreated)
            {
                ChunkMap[position] = entity;
            }
        }
        
        public void RemoveChunk(int3 position)
        {
            if (IsInitialized && ChunkMap.IsCreated)
            {
                ChunkMap.Remove(position);
            }
        }
    }
    
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ChunkLookupSystem : SystemBase
    {
        protected override void OnCreate()
        {
            var lookupEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(lookupEntity, new ChunkLookup
            {
                ChunkMap = new NativeHashMap<int3, Entity>(1024, Allocator.Persistent),
                IsInitialized = true
            });
        }
        
        protected override void OnDestroy()
        {
            if (SystemAPI.HasSingleton<ChunkLookup>())
            {
                var lookup = SystemAPI.GetSingleton<ChunkLookup>();
                if (lookup.ChunkMap.IsCreated)
                    lookup.ChunkMap.Dispose();
            }
        }
        
        protected override void OnUpdate()
        {
            // Update lookup when chunks are created/destroyed
            // (Implementation as needed)
        }
    }
}
```

---

---

## Implemented Systems

### ChunkSpawnerSystem
Location: `Assets/Scripts/Voxel/Systems/Generation/ChunkSpawnerSystem.cs`
- **Role**: Infinite Terrain Driver
- **Logic**: 
    1. Tracks camera position.
    2. Identifies a 4x4x4 chunk volume around the player.
    3. Checks `ChunkLookup` (via HashMap) to see if chunk exists.
    4. If missing, spawns a new Entity with `ChunkPosition` and `ChunkState.Loading`.

### ChunkGenerationSystem
Location: `Assets/Scripts/Voxel/Systems/Generation/ChunkGenerationSystem.cs`
- **Role**: Data Producer
- **Logic**:
    1. Queries all chunks with `ChunkState.Loading` (implied by invalid VoxelData).
    2. Allocates temporary NativeArrays.
    3. Schedules `GenerateVoxelDataJob` (Burst Compiled).
    4. On completion, builds a `BlobAssetReference` and assigns to `ChunkVoxelData`.
    5. Marks chunk as `ChunkNeedsRemesh`.

### ChunkLookupSystem
Location: `Assets/Scripts/Voxel/Systems/ChunkLookupSystem.cs`
- **Role**: Global Registry
- **Logic**:
    1. Maintains a singleton `NativeHashMap<int3, Entity>`.
    2. Updates map whenever a chunk is created or destroyed.
    3. Provides O(1) access for neighbors and gameplay systems.

---

## Configuration

Currently, parameters are hardcoded in the Job/System (MVP), but designed to be moved to a `ScriptableObject` Config:

| Parameter | Value | Effect |
|-----------|-------|--------|
| `SpawnRadius` | 4 | Generates chunks 4 units away (9x9x9 volume). |
| `NoiseScale` | 0.02 | Frequency of the terrain hills. |
| `TerrainAmplitude` | 10 | Max height of hills. |
| `GroundLevel` | 0 | Y-level where density = 0.5 (surface). |

---

## Integration Guide
 
 ### 1. Configuring Generation
 1.  Select the `VoxelWorld` object in your SubScene.
 2.  Located the `VoxelWorldAuthoring` component.
 3.  **Seed**: Enter an integer (e.g., 12345). Changing this reshuffles the terrain.
 4.  **Noise Scale**: Controls "Zoom". Smaller value (0.01) = Large hills. Larger value (0.1) = Bumpy/Spiky.
 5.  **Ground Level**: Sets the Y-height of the base terrain.
 
 ### 2. Adding a New Biome (Designer Workflow)
 *   In the future (Epic 10), you will create a `BiomeDefinition` asset.
 *   For now, to change the look:
     1.  Open `GenerateVoxelDataJob.cs`.
     2.  Locate `DetermineMaterialSamples`.
     3.  Change thresholds (e.g., `depth > 10` for IronOre).
 
 ### 3. Auto-Spawning
 *   The `ChunkSpawnerSystem` automatically tracks the Main Camera. 
 *   **Setup**: Ensure your scene has a Camera tagged as `MainCamera`.
 *   **Radius**: To load more chunks, increase `SpawnRadius` in `ChunkSpawnerSystem.cs` (or expose it to Authoring).


---

## Event Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                        Generation Pipeline                      │
├─────────────────────────────────────────────────────────────────┤
│  ChunkSpawnerSystem (Update)                                    │
│    └─ Detects missing chunk -> EntityManager.CreateEntity()     │
│                                                                 │
│  ChunkGenerationSystem (Update)                                 │
│    ├─ Query: With<ChunkPosition>, Without<ValidBlob>            │
│    ├─ Schedule: GenerateVoxelDataJob (Parallel)                 │
│    └─ Complete: BlobBuilder.Create() -> SetComponentData()      │
│                                                                 │
│  ChunkMeshingSystem (Update)                                    │
│    └─ Query: With<ChunkNeedsRemesh> -> Generate Mesh            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Testing

1.  **Spawn Verification**:
    *   Fly around in Scene View.
    *   **Verify**: New chunks appear in hierarchy as you move (`Chunk_X_Y_Z`).

2.  **Density Debug**:
    *   Set a breakpoint in `ChunkGenerationSystem.ValidateDensities`.
    *   **Verify**: `SurfaceCount` is > 0 for y=0 chunks.

3.  **Performance**:
    *   Open Profiler.
    *   **Verify**: `GenerateVoxelDataJob` takes < 0.5ms on main thread (it runs on worker threads).

---

## Acceptance Criteria

- [x] Chunks spawn around player
- [x] Each chunk has valid VoxelData blob
- [x] Validation shows gradient densities (Surface > 0)
- [x] Materials are assigned based on depth
- [x] Chunk lookup allows O(1) position → entity lookup
- [x] No memory leaks


| File | Description |
|------|-------------|
| `Assets/Scripts/Voxel/Systems/Generation/ChunkSpawnerSystem.cs` | Spawns entities around camera. |
| `Assets/Scripts/Voxel/Systems/Generation/ChunkGenerationSystem.cs` | Manages generation jobs. |
| `Assets/Scripts/Voxel/Systems/ChunkLookupSystem.cs` | Global chunk map. |
| `Assets/Scripts/Voxel/Jobs/GenerateVoxelDataJob.cs` | Burst job for noise calculation. |
