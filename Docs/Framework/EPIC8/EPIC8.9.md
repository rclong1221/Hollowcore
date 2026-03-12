# EPIC 8.9: Basic Networking

**Status**: ✅ COMPLETED  
**Priority**: HIGH  
**Dependencies**: EPIC 8.7 (Destruction working)

---

## Overview

This epic implements a **Server-Authoritative** networking model for the Voxel Engine. Clients send requests to modify the world, the Server validates and applies them, and then broadcasts the changes to all connected clients.

## 🚀 Quick Start Guide

### 1. Multiplayer Testing
1. Open the **Project Settings** > **Multiplayer Play Mode** and set usage to 2 Players (1 Server, 1 Client) or Host/Client.
2. Enter **Play Mode**.
3. As the **Client**, click to mine a voxel.
4. Verify that the hole appears on both the Client and the Server (Host) views.

### 2. Monitoring
- Use the **Voxel Debug Window** (`DIG > Voxel > Debug Window`) on both instances to verify chunk counts match after modification.
- Console logs on Server will show `[Server] Applied modification...`.

---

## 🛠️ Architecture Details

### 1. Messaging Flow
1. **Client Input**: `VoxelInteractionSystem` detects input and creates a local `VoxelModificationRequest` entity.
2. **Client System**: `VoxelModificationClientSystem` consumes the request and sends a `VoxelModificationRpc` to the Server.
3. **Server Processing**: 
    - `VoxelModificationServerSystem` receives the RPC.
    - Validates and applies the change to the Server World using `VoxelOperations.SetVoxel`.
    - Broadcasts `VoxelModificationBroadcast` to **ALL** clients (including sender).
4. **Client Reception**: `VoxelModificationReceiveSystem` receives the broadcast and applies the change to the Client World.

### 2. Components
| Component | Description |
|-----------|-------------|
| `VoxelModificationRpc` | `IRpcCommand` sent from Client to Server. |
| `VoxelModificationBroadcast` | `IRpcCommand` sent from Server to all Clients. |
| `VoxelModificationRequest` | Local `IComponentData` used to signal a desired change. |

### 3. Graceful Fallback
- If the game is running in **Standalone** (Single Player / Offline) mode, `VoxelModificationLocalSystem` intercepts the request and applies it immediately, bypassing the network stack entirely.

---

## 💻 Integration Guide

### How to Modify Voxels (Network Safe)
Do **NOT** call `VoxelOperations.SetVoxel` directly from gameplay code (unless specific server-side logic). Instead, request a modification:

```csharp
// 1. Create a Request Entity
var request = EntityManager.CreateEntity();
EntityManager.AddComponentData(request, new VoxelModificationRequest
{
    ChunkPos = targetChunkPos,
    LocalVoxelPos = targetLocalPos,
    TargetDensity = 0, // 0 for Air
    TargetMaterial = 0 // 0 for Empty
});
// The system handles the rest (RPC or Local apply).
```

---

## ✅ Acceptance Criteria

- [x] Client A destroys voxel -> Server receives request.
- [x] Server validates and applies modification.
- [x] Client B (and A) receive broadcast and see the hole.
- [x] Single Player mode continues to work (Offline fallback).
- [ ] Late joiner sync (Deferred to later Epic/Streaming).

---

## Deferred Tasks
- **Prediction**: Currently, the client waits for the Server Broadcast to see the change (Round-trip latency). Implement Client-side Prediction + Rollback for instant feedback.
- **Initial Sync**: Late joiners currently only receive *new* broadcasts. Use Chunk Streaming or Initial Snapshot to sync existing world state.
