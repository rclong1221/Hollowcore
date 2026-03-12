# EPIC 8.7: Voxel API & Modification

**Status**: ✅ COMPLETED  
**Priority**: HIGH  
**Dependencies**: EPIC 8.4 (Collision)
**Estimated Time**: 1 day

---

## Implementation Status

| Feature | Status | Notes |
|---------|--------|-------|
| **Voxel Materials** | ✅ DONE | `VoxelMaterialDefinition` & `VoxelMaterialRegistry` created |
| **Raycast API** | ✅ DONE | `VoxelRaycast.Cast()` with DDA algorithm |
| **Modification API** | ✅ DONE | `VoxelOperations.SetVoxel()` and `ModifySphere()` |
| **Event System** | ✅ DONE | `VoxelDestroyedEvent` buffer & `VoxelLootSystem` |
| **Interaction** | ✅ DONE | `VoxelInteractionSystem` handles mouse input & mining |

---

## Goal

Create a robust **Voxel Modification API** that allows:
1.  **Raycasting** against the voxel grid (independent of Unity Physics).
2.  **Modification** (Sphere, Cube, or Single Voxel updates).
3.  **Events** (`OnVoxelDestroyed`) for other systems to react to (e.g., Loot, VFX, Sound).

**Asset Store Philosophy**:
The core engine modifies data and meshes. It does *not* know about "Inventories" or "XP". It simply fires an event: "Hey, 50 Stone voxels were just destroyed at position X."

---

## Tasks

### Task 8.7.1: Voxel Modification API

**File**: `Assets/Scripts/Voxel/Core/VoxelOperations.cs`

A static (or singleton) API for modifying voxel data safely.

```csharp
public static class VoxelOperations
{
    // Modify a sphere of voxels
    public static void ModifySphere(
        EntityManager em, 
        float3 center, 
        float radius, 
        byte targetDensity, 
        byte targetMaterial) 
    { ... }

    // Modify a single voxel (helper)
    public static void SetVoxel(
        EntityManager em, 
        int3 worldVoxelPos, 
        byte density, 
        byte material)
    { ... }
}
```

### Task 8.7.2: Voxel Raycast System

**File**: `Assets/Scripts/Voxel/Systems/Interaction/VoxelRaycastSystem.cs`

- Implements a DDA (Digital Differential Analyzer) raycast algorithm.
- More precise than MeshCollider raycasts for high-speed digging.
- Returns `VoxelHit` struct:
    - `float3 Point`
    - `float3 Normal`
    - `int3 VoxelCoordinate`
    - `byte MaterialID`

### Task 8.7.3: The "Loot" Event System

Instead of hardcoding "Spawn Prefab", we emit an event.

**File**: `Assets/Scripts/Voxel/Components/VoxelEvents.cs`

```csharp
public struct VoxelDestroyedEvent : IBufferElementData
{
    public float3 Position;
    public byte MaterialID;
    public int Amount; // Approximate volume destroyed
}
```

### Task 8.7.4: Generic Loot Spawner (Sample Implementation)

A separate system `VoxelLootSystem` that listens for `VoxelDestroyedEvent` and spawns prefabs.

**File**: `Assets/Scripts/Voxel/Systems/Interaction/VoxelLootSystem.cs`

- Reads `VoxelDestroyedEvent` buffer.
- Looks up `MaterialID` in a `VoxelMaterialRegistry` (ScriptableObject).
- Instantiates the corresponding `LootPrefab`.
- **Note**: This system is *optional*. A user could replace it with an "Auto-Add to Inventory" system.

---

## Configuration: Material Registry

**File**: `Assets/Scripts/Voxel/Data/VoxelMaterialRegistry.cs`

ScriptableObject acting as a database:
- `List<VoxelMaterialEntry>`
    - `byte ID`
    - `string Name`
    - `GameObject DropPrefab` (Optional)
    - `GameObject ParticlePrefab` (Optional)
    - `AudioClip BreakSound` (Optional)

---


### Helper Tool: Automate Setup
We've created an Editor script to setup materials instantly.

1. **Tools > DIG > Voxel > Create Test Material Registry**:
   - Creates `Resources/VoxelMaterialRegistry`
   - Creates `Assets/Data/VoxelMaterials` (Air, Dirt, Stone, Ore)
   - Creates `Assets/Prefabs/Loot` (Physical cubes with URP materials)
   - Links everything together.

---

## Validation

- [ ] Click on stone → stone drops
- [ ] Click on iron ore → iron ore drops
- [ ] Hole appears in mesh
- [ ] Can walk through hole
- [ ] Different materials drop different items
- [ ] Hardness affects mining time/sphere when stone is broken.
- Verify mesh updates correctly.

---

## Success Criteria

1.  Can dig through terrain using mouse clicks.
2.  Loot spawns physically (falls with gravity).
3.  Performance is stable during continuous digging.

**Status**: ✅ COMPLETED (Verified)  
**Priority**: HIGH  
**Dependencies**: EPIC 8.4 (Collision working)

---

## Setup Guide

### 1. Create Material Registry
1. Right-click in Project view -> **Create > DIG > Voxel > Material Registry**.
2. Name it `VoxelMaterialRegistry`.
3. Move it to `Assets/Resources/VoxelMaterialRegistry.asset` (Crucial for `Resources.Load`).

### 2. Define Materials
1. Create Material Definitions: **Create > DIG > Voxel > Material Definition**.
2. Create:
   - `Mat_Air` (ID 0, IsMineable = false)
   - `Mat_Dirt` (ID 1, Hardness 0.5)
   - `Mat_Stone` (ID 2, Hardness 1.0)
   - `Mat_IronOre` (ID 3, Hardness 2.0)
3. Add them to the `VoxelMaterialRegistry` list.

### 3. Setup Loot
1. Create a physical prefab (Sphere/Cube with Rigidbody + Collider).
2. Assign it to the `LootPrefab` field in the Material Definition.

### 4. Enable Interaction
1. Ensure `VoxelInteractionSystem` is running (it's in `SimulationSystemGroup` automatically).
2. Ensure `VoxelLootSystem` is running.
3. In Play Mode:
   - Left Click to Mine.
   - Right Click to Debug Raycast (Visualizes target voxel).

---

## File Listing

| File | Description |
|------|-------------|
| `Core/VoxelMaterial.cs` | ScriptableObject definitions for materials |
| `Core/VoxelRaycast.cs` | DDA Raycast algorithm |
| `Core/VoxelOperations.cs` | Safe API for modifying voxel data |
| `Components/VoxelEvents.cs` | Event buffer definition |
| `Systems/Interaction/VoxelInteractionSystem.cs` | Mining logic + Event emission |
| `Systems/Interaction/VoxelLootSystem.cs` | Loot spawning logic |

