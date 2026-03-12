using System.IO;
using System.IO.Compression;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Voxel.Systems.Network;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Serializes voxel modification history to world save data.
    /// Reads from the ECS VoxelHistory singleton (NativeList of PendingModification).
    /// Supports optional GZip compression.
    /// </summary>
    public class WorldSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.World;
        public string DisplayName => "World (Voxel)";
        public int ModuleVersion => 1;

        private bool _compress;

        public WorldSaveModule(bool compress = true)
        {
            _compress = compress;
        }

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            // Read VoxelHistory singleton from the EntityManager
            var query = ctx.EntityManager.CreateEntityQuery(typeof(VoxelHistory));
            if (query.CalculateEntityCount() == 0)
            {
                w.Write(SaveBinaryConstants.CompressionNone);
                w.Write(0);
                return 5;
            }

            var historyComp = query.GetSingleton<VoxelHistory>();
            var modifications = historyComp.Value;
            if (!modifications.IsCreated || modifications.Length == 0)
            {
                w.Write(SaveBinaryConstants.CompressionNone);
                w.Write(0);
                return 5;
            }

            long start = w.BaseStream.Position;

            // Write raw record data to a temp buffer
            byte[] rawData;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(modifications.Length);
                for (int i = 0; i < modifications.Length; i++)
                {
                    var mod = modifications[i];
                    bw.Write(mod.ChunkPos.x);
                    bw.Write(mod.ChunkPos.y);
                    bw.Write(mod.ChunkPos.z);
                    bw.Write(mod.LocalPos.x);
                    bw.Write(mod.LocalPos.y);
                    bw.Write(mod.LocalPos.z);
                    bw.Write(mod.Density);
                    bw.Write(mod.Material);
                    bw.Write(mod.Tick);
                }
                rawData = ms.ToArray();
            }

            if (_compress)
            {
                w.Write(SaveBinaryConstants.CompressionGZip);
                using (var ms = new MemoryStream())
                {
                    using (var gz = new GZipStream(ms, CompressionMode.Compress, true))
                        gz.Write(rawData, 0, rawData.Length);
                    byte[] compressed = ms.ToArray();
                    w.Write(compressed.Length);
                    w.Write(compressed);
                }
            }
            else
            {
                w.Write(SaveBinaryConstants.CompressionNone);
                w.Write(rawData.Length);
                w.Write(rawData);
            }

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            byte compressionFlag = r.ReadByte();
            int dataLength = r.ReadInt32();

            if (dataLength == 0) return;

            byte[] rawData;
            if (compressionFlag == SaveBinaryConstants.CompressionGZip)
            {
                byte[] compressed = r.ReadBytes(dataLength);
                using (var ms = new MemoryStream(compressed))
                using (var gz = new GZipStream(ms, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gz.CopyTo(output);
                    rawData = output.ToArray();
                }
            }
            else
            {
                rawData = r.ReadBytes(dataLength);
            }

            // Replay modifications into VoxelHistory singleton
            var query = ctx.EntityManager.CreateEntityQuery(typeof(VoxelHistory));
            if (query.CalculateEntityCount() == 0) return;

            var entity = query.GetSingletonEntity();
            var historyComp = ctx.EntityManager.GetComponentData<VoxelHistory>(entity);
            var modifications = historyComp.Value;
            if (!modifications.IsCreated) return;

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                int recordCount = br.ReadInt32();
                for (int i = 0; i < recordCount; i++)
                {
                    var mod = new PendingModification
                    {
                        ChunkPos = new int3(br.ReadInt32(), br.ReadInt32(), br.ReadInt32()),
                        LocalPos = new int3(br.ReadInt32(), br.ReadInt32(), br.ReadInt32()),
                        Density = br.ReadByte(),
                        Material = br.ReadByte(),
                        Tick = br.ReadUInt32()
                    };
                    modifications.Add(mod);
                }
            }
        }
    }
}
