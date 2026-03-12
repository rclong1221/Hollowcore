using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Processes LoadRequest entities. Reads binary file, validates magic + CRC32,
    /// dispatches module blocks to ISaveModule.Deserialize().
    /// Unknown TypeIds are skipped (forward compatibility). Missing modules silently skipped.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SaveSystem))]
    public partial class LoadSystem : SystemBase
    {
        private EntityQuery _requestQuery;
        private EntityQuery _playerQuery;

        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(ComponentType.ReadOnly<LoadRequest>());
            _playerQuery = GetEntityQuery(
                ComponentType.ReadOnly<global::PlayerTag>(),
                ComponentType.ReadOnly<SaveStateLink>());
            RequireForUpdate<SaveManagerSingleton>();
        }

        protected override void OnUpdate()
        {
            if (_requestQuery.CalculateEntityCount() == 0) return;

            var manager = SystemAPI.ManagedAPI.GetSingleton<SaveManagerSingleton>();
            if (!manager.IsInitialized) return;

            var requests = _requestQuery.ToEntityArray(Allocator.Temp);
            var requestData = _requestQuery.ToComponentDataArray<LoadRequest>(Allocator.Temp);

            for (int r = 0; r < requests.Length; r++)
            {
                ProcessLoadRequest(manager, requestData[r]);
                EntityManager.DestroyEntity(requests[r]);
            }

            requests.Dispose();
            requestData.Dispose();
        }

        private void ProcessLoadRequest(SaveManagerSingleton manager, LoadRequest req)
        {
            var players = _playerQuery.ToEntityArray(Allocator.Temp);

            for (int p = 0; p < players.Length; p++)
            {
                var playerEntity = players[p];
                var link = EntityManager.GetComponentData<SaveStateLink>(playerEntity);

                string playerId = "local";
                if (link.SaveChildEntity != Entity.Null && EntityManager.HasComponent<PlayerSaveId>(link.SaveChildEntity))
                {
                    var saveId = EntityManager.GetComponentData<PlayerSaveId>(link.SaveChildEntity);
                    if (saveId.PlayerId.Length > 0)
                        playerId = saveId.PlayerId.ToString();
                }

                if (req.TargetPlayerId.Length > 0 && playerId != req.TargetPlayerId.ToString())
                    continue;

                string fileName = $"{playerId}_slot{req.SlotIndex}.dig";
                string filePath = Path.Combine(manager.SaveDirectory, fileName);

                try
                {
                    byte[] data = SaveFileReader.ReadFile(filePath);
                    if (data == null)
                    {
                        CreateLoadComplete(req.SlotIndex, false, "File not found");
                        continue;
                    }

                    if (!SaveFileReader.ValidateMagic(data, SaveBinaryConstants.MagicPlayer))
                    {
                        CreateLoadComplete(req.SlotIndex, false, "Invalid magic bytes");
                        continue;
                    }

                    if (!SaveFileReader.ValidateCRC32(data))
                    {
                        CreateLoadComplete(req.SlotIndex, false, "CRC32 checksum mismatch — file may be corrupted");
                        continue;
                    }

                    DeserializePlayer(manager, playerEntity, data);
                    CreateLoadComplete(req.SlotIndex, true, default);

                    Debug.Log($"[LoadSystem] Loaded save for {playerId} from slot {req.SlotIndex}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LoadSystem] Failed to load {filePath}: {e.Message}");
                    CreateLoadComplete(req.SlotIndex, false, e.Message);
                }
            }

            players.Dispose();
        }

        private void DeserializePlayer(SaveManagerSingleton manager, Entity playerEntity, byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);

            // Skip header: magic(4) + version(4) + timestamp(8) + name(64) + crc(4)
            r.ReadUInt32(); // magic
            int formatVersion = r.ReadInt32();
            r.ReadInt64();  // timestamp
            r.ReadBytes(SaveBinaryConstants.PlayerNameFieldSize); // name
            r.ReadUInt32(); // crc

            short moduleCount = r.ReadInt16();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var loadCtx = new LoadContext(EntityManager, playerEntity, ecb, formatVersion);

            for (int i = 0; i < moduleCount; i++)
            {
                int typeId = r.ReadInt32();
                short moduleVersion = r.ReadInt16();
                int dataLength = r.ReadInt32();
                long dataStart = ms.Position;

                if (manager.ModuleByTypeId.TryGetValue(typeId, out var module))
                {
                    module.Deserialize(loadCtx, r, moduleVersion);

                    // Ensure we consumed exactly the right number of bytes
                    long expected = dataStart + dataLength;
                    if (ms.Position != expected)
                    {
                        Debug.LogWarning($"[LoadSystem] Module {module.DisplayName} read {ms.Position - dataStart} bytes but block is {dataLength} bytes. Seeking to correct position.");
                        ms.Position = expected;
                    }
                }
                else
                {
                    // Unknown module — skip forward (forward compatibility)
                    Debug.LogWarning($"[LoadSystem] Unknown module TypeId={typeId}, skipping {dataLength} bytes.");
                    ms.Position = dataStart + dataLength;
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            // Verify EOF marker
            if (ms.Position + 4 <= ms.Length)
            {
                uint eof = r.ReadUInt32();
                if (eof != SaveBinaryConstants.EOFMarker)
                    Debug.LogWarning("[LoadSystem] EOF marker missing or invalid.");
            }
        }

        private void CreateLoadComplete(int slotIndex, bool success, string error)
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new LoadComplete
            {
                SlotIndex = slotIndex,
                Success = success,
                ErrorMessage = string.IsNullOrEmpty(error) ? default : new FixedString128Bytes(error.Length > 120 ? error.Substring(0, 120) : error)
            });
        }
    }
}
