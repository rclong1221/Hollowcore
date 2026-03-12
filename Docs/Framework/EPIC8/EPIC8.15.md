# EPIC 8.15: Voxel System - Loot & Bug Fixes

**Status**: ✅ COMPLETE  
**Priority**: MEDIUM  
**Dependencies**: EPIC 8.6 (Collision), EPIC 8.12 (Seamless Meshing)  
**Estimated Time**: 2-3 days (cumulative)  
**Last Updated**: 2025-12-20

---

## Quick Start Guide

### For Designers

1. **Create Loot Prefabs**
   - Create a GameObject with a `Rigidbody` component
   - Add visual mesh (e.g., rock, ore chunk)
   - Save as Prefab in your project

2. **Configure Materials**
   - Open `Assets/Resources/VoxelMaterialRegistry.asset`
   - Add your `VoxelMaterialDefinition` assets to the Materials list
   - For each material, set:
     - `LootPrefab`: The prefab to spawn when mined
     - `DropChance`: 0.0 - 1.0 probability
     - `MinDropCount` / `MaxDropCount`: Range of items spawned

3. **Test in Editor**
   - Enter Play Mode (Host or Client+Server)
   - Mine voxels with left-click
   - Loot should spawn and be visible to all players

### For Developers

1. **Key Files**
   ```
   Assets/Scripts/Voxel/
   ├── Components/
   │   ├── VoxelEvents.cs          # VoxelDestroyedEvent, VoxelEventsSingleton
   │   └── ChunkComponents.cs      # ChunkPhysicsState, ObsoleteChunkCollider
   ├── Core/
   │   ├── VoxelMaterialRegistry.cs    # Material definitions with loot configs
   │   └── VoxelNetworkMessages.cs     # LootSpawnBroadcast RPC
   ├── Systems/Interaction/
   │   ├── VoxelInteractionSystem.cs   # Emits VoxelDestroyedEvents
   │   └── VoxelLootSystem.cs          # Local/offline loot spawning
   └── Systems/Network/
       └── LootSpawnNetworkSystem.cs   # Server broadcast + Client receive
   ```

2. **Run Tests**
   - Start Host mode
   - Connect a second client
   - Both players should see loot when either player mines

---

## Component Reference

### VoxelDestroyedEvent (IBufferElementData)
```csharp
public struct VoxelDestroyedEvent : IBufferElementData
{
    public float3 Position;    // World position of destroyed voxel
    public byte MaterialID;    // Material type (maps to VoxelMaterialDefinition)
    public int Amount;         // Volume destroyed (typically 1)
}
```
- **Emitted by**: `VoxelInteractionSystem` when mining completes
- **Consumed by**: `VoxelLootSystem` (local), `LootSpawnServerSystem` (networked)

### VoxelEventsSingleton (IComponentData)
```csharp
public struct VoxelEventsSingleton : IComponentData {}
```
- Tag component for reliable singleton queries
- Attached to the entity holding the `VoxelDestroyedEvent` buffer

### LootSpawnBroadcast (IRpcCommand)
```csharp
public struct LootSpawnBroadcast : IRpcCommand
{
    public float3 Position;    // Spawn position
    public float3 Velocity;    // Initial ejection velocity
    public byte MaterialID;    // Material for prefab lookup
}
```
- Sent from Server to all Clients when loot should spawn
- Cosmetic-only (no authoritative loot state)

### ChunkPhysicsState (ICleanupComponentData)
```csharp
public struct ChunkPhysicsState : ICleanupComponentData
{
    public BlobAssetReference<Collider> ColliderBlob;
}
```
- Tracks physics collider blob for proper disposal
- Prevents memory leaks on chunk unload

### ObsoleteChunkCollider (IComponentData)
```csharp
public struct ObsoleteChunkCollider : IComponentData
{
    public BlobAssetReference<Collider> Blob;
}
```
- Tags colliders scheduled for deferred disposal
- Prevents race conditions during physics updates

---

## System Architecture

### Loot Spawning Flow

```
[Mining Complete]
       │
       ▼
┌─────────────────────────┐
│ VoxelInteractionSystem  │  Emits VoxelDestroyedEvent
│ (SimulationSystemGroup) │  Adds to VoxelEventsSingleton buffer
└───────────┬─────────────┘
            │
    ┌───────┴───────┐
    │               │
    ▼               ▼
┌─────────┐   ┌─────────────────────┐
│ OFFLINE │   │     NETWORKED       │
│  MODE   │   │       MODE          │
└────┬────┘   └──────────┬──────────┘
     │                   │
     ▼                   ▼
┌────────────────┐  ┌──────────────────────┐
│ VoxelLootSystem│  │ LootSpawnServerSystem│
│ (Local Only)   │  │ (ServerSimulation)   │
│ Spawns locally │  │ Broadcasts RPC       │
└────────────────┘  └──────────┬───────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │ LootSpawnClientSystem│
                    │ (ClientSimulation)   │
                    │ Receives RPC, spawns │
                    └──────────────────────┘
```

### Physics Collider Lifecycle

```
[Chunk Re-Mesh Triggered]
         │
         ▼
┌────────────────────────────┐
│ ChunkMeshingSystem         │
│ 1. Check for existing      │
│    ChunkPhysicsState       │
│ 2. If exists, tag old blob │
│    with ObsoleteChunkCollider│
│ 3. Create new collider     │
│ 4. Update ChunkPhysicsState│
└───────────┬────────────────┘
            │
            ▼
┌────────────────────────────────────┐
│ ChunkColliderDisposalSystem        │
│ (After PhysicsSystemGroup)         │
│ 1. Query ObsoleteChunkCollider     │
│ 2. Dispose the blob safely         │
│ 3. Remove component                │
└────────────────────────────────────┘
```

---

## Setup Guide

### 1. VoxelMaterialRegistry Setup

Create the registry asset:
1. Right-click in Project → Create → DIG → Voxel → Material Registry
2. Name it `VoxelMaterialRegistry` and place in `Assets/Resources/`
3. Add `VoxelMaterialDefinition` assets to the Materials list

### 2. VoxelMaterialDefinition Setup

Create material definitions:
1. Right-click → Create → DIG → Voxel → Material Definition
2. Configure:
   - `MaterialID`: Unique byte (0=Air, 1=Dirt, 2=Stone, etc.)
   - `MaterialName`: Display name
   - `Hardness`: Mining time multiplier
   - `LootPrefab`: GameObject to spawn
   - `MinDropCount`/`MaxDropCount`: Item count range
   - `DropChance`: 0.0-1.0 probability

### 3. Loot Prefab Requirements

Each loot prefab should have:
- **Rigidbody**: For physics (ejection velocity)
- **Collider**: For ground interaction
- **Visual Mesh**: So players can see it
- Optional: Custom script for pickup behavior

### 4. Network Configuration

The system auto-configures based on world type:
- `LocalSimulation`: Uses `VoxelLootSystem` directly
- `ServerSimulation`: Uses `LootSpawnServerSystem`
- `ClientSimulation`: Uses `LootSpawnClientSystem`

No additional setup required.

---

## Integration Guide

### Adding New Material Types

```csharp
// 1. Create VoxelMaterialDefinition asset in Editor
// 2. Set MaterialID (must be unique)
// 3. Assign to VoxelMaterialRegistry

// Or programmatically:
var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
var newMat = ScriptableObject.CreateInstance<VoxelMaterialDefinition>();
newMat.MaterialID = 5;
newMat.MaterialName = "Gold Ore";
newMat.LootPrefab = goldOrePrefab;
registry.Materials.Add(newMat);
registry.Initialize(); // Rebuild lookup table
```

### Custom Loot Behavior

Override the default spawning by subscribing to events:
```csharp
// In your custom system:
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(LootSpawnClientSystem))]
public partial class CustomLootSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Query LootSpawnBroadcast before the default system processes them
        foreach (var (rpc, entity) in SystemAPI.Query<LootSpawnBroadcast>()
            .WithAll<ReceiveRpcCommandRequest>()
            .WithEntityAccess())
        {
            // Custom logic (e.g., play sound, add particle effect)
            SpawnCustomEffect(rpc.Position, rpc.MaterialID);
        }
    }
}
```

### Disabling Loot for Specific Materials

Set `DropChance = 0` on the material definition, or don't assign a `LootPrefab`.

---

## Bug Tracking

### Bug 8.15.1: Falling Through Chunk Edges

**Status**: 🔲 NOT STARTED  
**Severity**: MEDIUM  

Player occasionally falls through world at chunk boundaries. See detailed investigation steps in bug list below.

### Bug 8.15.2: Ragdoll Falls Through Floor

**Status**: 🔲 NOT STARTED  
**Severity**: LOW  

Ragdoll physics don't collide with voxel terrain properly.

### Bug 8.15.3: Loot Not Spawning Visually

**Status**: ✅ RESOLVED (2025-12-20)  
**Root Cause**: Broken script reference in `VoxelMaterialRegistry.asset`  
**Fix**: Separated `VoxelMaterialRegistry` into own file, fixed GUID

### Task 8.15.4: Network-Synced Loot

**Status**: ✅ COMPLETE (2025-12-20)  
**Solution**: Implemented via `LootSpawnBroadcast` RPC  
**Files**: `LootSpawnNetworkSystem.cs`, `VoxelNetworkMessages.cs`

---

### Bug 8.15.5: Chunk Boundary Neighbor Remesh Failure

**Status**: 🔲 NOT STARTED  
**Severity**: HIGH  
**Visual Evidence**: Hard vertical/horizontal line at chunk borders when mining

**Description**:
When digging at the edge/boundary of a chunk, only ONE chunk gets its mesh updated. The neighboring chunk that shares the boundary voxels does NOT remesh, creating a visible hard line/seam between chunks where the terrain should be seamlessly smooth.

**Expected Behavior**:
Mining at chunk boundary should trigger remesh on BOTH chunks that share boundary data. The result should be a smooth, continuous surface across chunk boundaries.

**Technical Analysis**:
1. `VoxelOperations.SetVoxel()` calls `MarkNeighbors()` which should set `ChunkNeedsRemesh` on adjacent chunks
2. `MarkNeighbors()` only triggers at local positions 0 or 31 (boundary voxels)
3. **Possible Issues**:
   - `GetChunkEntity()` lookup may be failing for neighbor chunks
   - Neighbor chunk might not exist (not loaded yet)
   - `ChunkNeedsRemesh` might be disabled/not present on neighbor
   - Marching Cubes needs neighbor voxel data padding - maybe padding fetch is stale?

**Investigation Steps**:
1. Add debug logging to `MarkNeighbors()` in `VoxelOperations.cs`
2. Verify neighbor chunk entities exist when marking
3. Check if `ChunkMeshingSystem` actually processes the remesh for neighbors
4. Verify padded density fetch (`GetPaddedVoxelData`) reads updated neighbor data

**Files to Check**:
- `Assets/Scripts/Voxel/Core/VoxelOperations.cs` - `MarkNeighbors()`, `MarkChunk()`
- `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs` - Neighbor padding logic
- `Assets/Scripts/Voxel/Components/ChunkComponents.cs` - `ChunkNeedsRemesh` definition

---

### Bug 8.15.6: Top Layer Mining Visual Desync

**Status**: 🔲 NOT STARTED  
**Severity**: MEDIUM  
**Visual Evidence**: Flat top terrain layer can be mined (player falls into hole) but mesh doesn't update

**Description**:
The topmost surface layer of terrain can be mined - physics correctly update (player can fall through), but the **visual mesh does not update** to show the hole. You can see the voxels being dug underneath through the unupdated surface mesh.

**Expected Behavior**:
Mining the top surface should visually carve a hole in the terrain. Mesh should update to show the excavated area.

**Technical Analysis**:
This is likely related to **Y-axis neighbor remeshing** specifically:
1. When mining a voxel at Y=31 (top of chunk), the chunk ABOVE should also remesh
2. If chunk above doesn't exist (above world bounds), the current chunk's mesh might not properly recalculate the surface
3. Marching Cubes isosurface extraction requires density values from ABOVE the surface voxel
4. If the chunk above has no data (or default solid), the surface normals/triangles may be wrong

**Possible Causes**:
- `MarkNeighbors()` not triggering for Y+ direction
- Chunk above world boundary doesn't exist, so no entity to mark
- `GetPaddedVoxelData()` returns incorrect values for out-of-bounds Y positions
- The mesh's top surface is generated from the chunk ABOVE looking DOWN, not current chunk looking UP

**Investigation Steps**:
1. Test mining at Y=0 (chunk bottom) vs Y=31 (chunk top) - see if behavior differs
2. Check if chunk at Y+1 exists when mining near chunk top
3. Trace `ChunkNeedsRemesh` flag propagation for vertical neighbors
4. Verify padding logic handles world bounds correctly

**Files to Check**:
- `Assets/Scripts/Voxel/Core/VoxelOperations.cs` - Y boundary handling in `MarkNeighbors()`
- `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs` - `GetPaddedVoxelData()` Y bounds
- `Assets/Scripts/Voxel/Systems/Generation/ChunkStreamingSystem.cs` - Vertical chunk loading logic

---

## Acceptance Criteria

- [ ] Bug 8.15.1: Player can walk across chunk boundaries without falling
- [ ] Bug 8.15.2: Ragdoll collides properly with voxel terrain
- [x] Bug 8.15.3: Loot items spawn and are visible on client when digging ✅
- [x] Task 8.15.4: Remote clients see loot spawning (network sync) ✅
- [ ] Bug 8.15.5: Mining at chunk boundaries remeshes BOTH adjacent chunks
- [ ] Bug 8.15.6: Top surface layer visual updates correctly when mined
- [ ] No regression in normal terrain collision
- [x] Fixes work in both single-player and multiplayer modes ✅

---

## Related Epics

| Epic | Relevance |
|------|-----------|
| EPIC 8.6 | Collision system implementation |
| EPIC 8.9 | Voxel modification networking |
| EPIC 8.12 | Seamless chunk boundary meshing |
| EPIC 8.14 | Performance (affects fix approaches) |
| EPIC 11.4 | Future: Loot table integration with inventory |

---

## Troubleshooting

### Loot not spawning at all

1. Check `VoxelMaterialRegistry` is in `Assets/Resources/`
2. Verify the asset's script reference is valid (not "Missing Script")
3. Ensure materials have `LootPrefab` assigned
4. Check console for `[VoxelLootSystem]` or `[LootSpawnClient]` logs

### Loot only visible to mining player (in multiplayer)

1. Verify server is running `LootSpawnServerSystem`
2. Check client is running `LootSpawnClientSystem`
3. Look for `LootSpawnBroadcast` RPC in network logs

### Physics crashes during mining

1. Verify `ChunkColliderDisposalSystem` is running
2. Check for `ObsoleteChunkCollider` components in Entity Debugger
3. Ensure colliders are not disposed during physics step

### Memory leaks

1. Open Voxel Debug Window (if available)
2. Monitor blob allocations during chunk load/unload
3. Verify `ChunkPhysicsState` cleanup on destroyed chunks
