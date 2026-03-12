using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: 64-byte file header for .digreplay format.
    /// All values little-endian.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ReplayFileHeader
    {
        public const uint MagicValue = 0x52474944; // "DIGR"
        public const ushort CurrentVersion = 1;

        public uint Magic;
        public ushort FormatVersion;
        public ushort TickRate;
        public long StartTimestampUnix;
        public float DurationSeconds;
        public uint TotalFrames;
        public ushort PeakEntityCount;
        public byte PlayerCount;
        public uint MapHash;
        public uint CRC32;
        // Padding handled in serialization to reach 64 bytes
    }

    /// <summary>
    /// EPIC 18.10: Per-frame header written before entity data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ReplayFrameHeader
    {
        public uint ServerTick;
        public ReplayFrameType FrameType;
        public ushort EntityCount;
        public ushort EventCount;
        public int DataSizeBytes;
    }

    /// <summary>
    /// EPIC 18.10: Header for a single entity's snapshot within a frame.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntitySnapshotHeader
    {
        public ushort EntityId;
        public byte PrefabTypeId;
        public ushort DataSizeBytes;
    }

    /// <summary>
    /// EPIC 18.10: Fixed-size component data per entity per frame.
    /// 49 bytes: position(12) + rotation(16) + velocity(12) + healthCurrent(4) + healthMax(4) + deathPhase(1).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntityComponentData
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 Velocity;
        public float HealthCurrent;
        public float HealthMax;
        public byte DeathPhase;
    }

    /// <summary>
    /// EPIC 18.10: Event recorded at a specific tick.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ReplayEventData
    {
        public ReplayEventType EventType;
        public uint Tick;
        public ushort SourceEntityId;
        public ushort TargetEntityId;
        public float3 Position;
        public float Value;
    }

    /// <summary>
    /// EPIC 18.10: Player info entry for the file header's player table.
    /// </summary>
    public struct ReplayPlayerInfo
    {
        public int NetworkId;
        public ushort GhostId;
        public byte TeamId;
    }

    /// <summary>
    /// EPIC 18.10: Deserialized frame (in-memory representation for playback).
    /// </summary>
    public class ReplayFrame
    {
        public uint Tick;
        public ReplayFrameType FrameType;
        public Dictionary<ushort, EntityComponentData> EntityData;
        public List<ReplayEventData> Events;
    }
}
