# EPIC 10.16: Network Traffic Optimization

**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Dependencies**: None

---

## Problem Statement

Profiler and code analysis reveal **excessive network packet generation** (RPC flooding) in two key areas, causing performance degradation and potential disconnects:

1.  **Voxel Modification Flooding**: `VoxelBatchingSystem` claims to batch modifications but effectively sends 1 RPC per modification (looping through the batch and sending individually).
    -   *Impact*: Modifying 50 voxels sends 50 separate RPCs per client immediately.
2.  **Loot Spawn Flooding**: `LootSpawnNetworkSystem` broadcasts one RPC per spawned loot item.
    -   *Impact*: An explosion destroying 100 blocks spawns 100 loot items, triggering 100 broadcast RPCs per client.
3.  **Explosion Desync**: `VoxelExplosionSystem` uses a random seed for procedural damage that is not synced between Server and Client, leading to different crater shapes and desync.

---

## Objectives

1.  **True Voxel Batching**: Implement a packed RPC to send up to 64/128 modifications in a single command.
2.  **Loot Spawn Batching**: Group loot spawn events into efficient batches.
3.  **Deterministic Explosions**: Sync random seeds for explosions to ensure identical simulation on Client/Server, reducing the need for per-voxel sync.

---

## Tasks

### Task 10.16.1: Implement True Voxel Batching ✅ COMPLETE
- [x] Create `VoxelModificationBatch` struct implementing `IRpcCommand`.
- [x] Use `fixed int3` and `fixed byte` arrays (unsafe/pointers) or flattened fields for packing.
- [x] maximum 64 mods per batch (~1KB payload).
- [x] Update `VoxelBatchingSystem` to populate this struct and send ONCE.
- [x] Update `VoxelModificationReceiveSystem` to unpack and apply.

**Expected Impact**: reduce 50 RPCs -> 1 RPC per batch.

### Task 10.16.2: Implement Loot Spawn Batching ✅ COMPLETE
- [x] Create `LootSpawnBatch` struct implementing `IRpcCommand`.
- [x] Pack Position (FixedList512Bytes), Velocity (FixedList512Bytes), and MaterialID (FixedList128Bytes).
- [x] Update `LootSpawnServerSystem` to queue events and flush in batches.
- [x] Update `LootSpawnClientSystem` to process batches.

**Expected Impact**: reduce 100 RPCs -> 2-3 RPCs for large explosions.

### Task 10.16.3: Fix Explosion Determinism ✅ COMPLETE
- [x] Add `Seed` field to `ExplosionBroadcastRpc`.
- [x] Update `VoxelExplosionNetworkSystem` to generate and send seed from Server.
- [x] Update `VoxelExplosionSystem` to accept optional seed in `CreateCraterRequest`.
- [x] Ensure `BurstExplosionJob` uses the synced seed for RNG.

**Expected Impact**: Prevents voxel grid desync between client/server.

---

## Files to Modify

| File | Tasks |
|------|-------|
| `Voxel/Systems/Network/VoxelBatchingSystem.cs` | 10.16.1 |
| `Voxel/Systems/Network/VoxelModificationReceiveSystem.cs` | 10.16.1 |
| `Voxel/Core/VoxelNetworkMessages.cs` | 10.16.1, 10.16.2 |
| `Voxel/Systems/Network/LootSpawnNetworkSystem.cs` | 10.16.2 |
| `Voxel/Systems/Interaction/VoxelExplosionSystem.cs` | 10.16.3 |
| `Voxel/Systems/Interaction/VoxelExplosionNetworkSystem.cs` | 10.16.3 |

## Acceptance Criteria

- [ ] `VoxelBatchingSystem` sends max 1 RPC per tick per 64 incomplete mods.
- [ ] `LootSpawnServerSystem` sends max 1 RPC per tick per batch of loot.
- [x] Explosions produce identical voxel patterns on Client and Server.
- [ ] Network packet rate drops significantly during drilling/explosions.
