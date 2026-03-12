# EPIC 21.1: Networking Modularization

**Status**: 🔲 NOT STARTED  
**Priority**: CRITICAL  
**Estimated Effort**: 1 week  
**Dependencies**: None

---

## Goal

Split the voxel engine into two packages:
- `DIG.Voxel.Core` - Works without NetCode
- `DIG.Voxel.Networking` - Optional multiplayer addon

---

## Current Problem

```json
// Current DIG.Voxel.asmdef forces NetCode on everyone
"references": [
    "Unity.NetCode"  // ← Mandatory, even for single-player games
]
```

This is a major barrier for asset store customers who want single-player-only solutions.

---

## Tasks

### Phase 1: Abstraction Layer
- [ ] Create `IVoxelNetworkAdapter` interface
- [ ] Define network-agnostic modification events
- [ ] Create local-only implementations (default)

### Phase 2: Split Assembly Definitions
- [ ] Create `DIG.Voxel.Core.asmdef` (no NetCode reference)
- [ ] Create `DIG.Voxel.Networking.asmdef` (NetCode reference)
- [ ] Use `#if NETCODE_PRESENT` or version defines

### Phase 3: Move Network Systems
- [ ] Move `Systems/Network/` to networking package
- [ ] Move `VoxelModificationHistory.cs` to networking
- [ ] Move `VoxelNetworkMessages.cs` to networking

### Phase 4: Update Package Manifests
- [ ] Update `package.json` for core (no NetCode dependency)
- [ ] Create `package.json` for networking addon
- [ ] Define proper dependency chain

### Phase 5: Verification
- [ ] Test core package compiles without NetCode installed
- [ ] Test networking package adds multiplayer functionality
- [ ] Ensure no breaking changes for existing users

---

## Files to Move to Networking Package

| Current Location | Reason |
|-----------------|--------|
| `Systems/Network/LateJoinSyncSystem.cs` | NetCode-only |
| `Systems/Network/VoxelBatchingSystem.cs` | NetCode-only |
| `Systems/Network/VoxelModificationSystems.cs` | NetCode-only |
| `Systems/Network/LootSpawnNetworkSystem.cs` | NetCode + Game |
| `Core/VoxelModificationHistory.cs` | Only for multiplayer |
| `Core/VoxelNetworkMessages.cs` | RPC definitions |

---

## Success Criteria

- [ ] Core package compiles with only: Entities, Physics, Burst, Collections
- [ ] `#if VOXEL_NETWORKING` guards all network code
- [ ] Networking package adds ~10 KB of code only when needed
- [ ] Existing multiplayer games continue to work
- [ ] Documentation explains both configurations
