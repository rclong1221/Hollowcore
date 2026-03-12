# EPIC 15.6: Voxel System Completeness

## Goal
To add the critical "Missing Link" features of a voxel engine: **Persistence** (Saving/Loading worlds) and **Destruction** (Physics interactions).

---

## 1. Persistence System (Save/Load)
*   **Status:** **MISSING**. No saved game architecture found.
*   **Requirement:** Serialize the Voxel World (compressed chunks) and Player State (Inventory, Position) to disk.
*   **Architecture:**
    *   **Format:** Binary (Protobuf or Custom Bit-packing) for speed. JSON is too slow for Voxels.
    *   **Folder:** `Application.persistentDataPath/Saves/{WorldName}/`
    *   **Files:** `region_0_0.chunk`, `player.dat`.
*   **Flow:**
    *   *Save:* Async job gathers modified chunks -> Compresses -> Writes to disk.
    *   *Load:* Read header -> Stream chunks -> Build Meshes.

## 2. Physics Destruction
*   **Status:** `VoxelExplosionTester` exists, but needs proper integration.
*   **Goal:** Allow weapons (Rockets, Grenades) to destroy terrain.
*   **Logic:**
    *   On `ExplosionEvent` (ECS):
        *   Calculate Sphere of Influence.
        *   Modify Voxel Data (Air).
        *   Trigger Mesh Rebuild.
        *   Spawn "Debris" particles (use FEEL).

## 3. World Generation UI
*   **Goal:** A menu to "Create New World" (Seed input, Biome selection) and "Load World".
*   **Integration:** Must hook into the new Persistence System.

---

## Implementation Tasks
- [ ] Design Binary Save Format for Chunks.
- [ ] Implement `VoxelSaveSystem` (Async Writer).
- [ ] Create `SaveGameManager` UI (Load/Save/Delete slots).
- [ ] Hook `WeaponFireSystem` (Explosives) to Voxel Destruction API.
