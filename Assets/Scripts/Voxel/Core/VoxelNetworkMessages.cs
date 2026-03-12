using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Collections;

namespace DIG.Voxel.Core
{
    // Sent from Client to Server
    public struct VoxelModificationRpc : IRpcCommand
    {
        public int3 ChunkPos;
        public int3 LocalVoxelPos;
        public byte NewDensity;
        public byte NewMaterial;
        public uint RequestTick; // For verification/ordering
    }

    // Broadcast from Server to all Clients
    public struct VoxelModificationBroadcast : IRpcCommand
    {
        public int3 ChunkPos;
        public int3 LocalVoxelPos;
        public byte NewDensity;
        public byte NewMaterial;
        public uint ServerTick;
    }

    /// <summary>
    /// Broadcast from Server to all Clients when loot should spawn visually.
    /// Cosmetic-only: Each client instantiates the loot prefab locally.
    /// </summary>
    public struct LootSpawnBroadcast : IRpcCommand
    {
        public float3 Position;
        public float3 Velocity;
        public byte MaterialID;
    }

    public struct ExplosionBroadcastRpc : IRpcCommand
    {
        public float3 Center;
        public float Radius;
        public uint Seed; // Added for Task 10.16.3: Deterministic craters
    }

    // Task 10.16.1: Batched Voxel Modifications
    // Sends up to 64 modifications in a single packet (~1KB payload)
    // Significantly reduces overhead compared to 64 individual RPC headers
    public struct VoxelModificationBatchRpc : IRpcCommand
    {
        public int Count;
        public uint ServerTick;
        
        // Use FixedList (Value Types) for NetCode support.
        // 64 items * 4 bytes = 256 bytes. FixedList512Bytes fits this.
        public FixedList512Bytes<int> ChunkX;
        public FixedList512Bytes<int> ChunkY;
        public FixedList512Bytes<int> ChunkZ;
        
        public FixedList512Bytes<int> LocalX;
        public FixedList512Bytes<int> LocalY;
        public FixedList512Bytes<int> LocalZ;
        
        // 64 items * 1 byte = 64 bytes.
        public FixedList128Bytes<byte> NewDensity;
        public FixedList128Bytes<byte> NewMaterial;
    }

    // Task 10.16.2: Batched Loot Spawning
    public struct LootSpawnBatchRpc : IRpcCommand
    {
        public int Count;
        
        // 32 items * 4 bytes = 128 bytes. FixedList512Bytes is plenty.
        public FixedList512Bytes<float> PosX;
        public FixedList512Bytes<float> PosY;
        public FixedList512Bytes<float> PosZ;
        
        public FixedList512Bytes<float> VelX;
        public FixedList512Bytes<float> VelY;
        public FixedList512Bytes<float> VelZ;
        
        // 32 items * 1 byte = 32 bytes.
        public FixedList64Bytes<byte> Materials;
    }
}
