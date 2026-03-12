using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Processes SaveRequest entities. Orchestrates serialization via ISaveModules,
    /// writes header + module blocks + EOF, computes CRC32, hands bytes to SaveFileWriter.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SaveSystem : SystemBase
    {
        private EntityQuery _requestQuery;
        private EntityQuery _playerQuery;

        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(ComponentType.ReadOnly<SaveRequest>());
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

            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint serverTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;

            var requests = _requestQuery.ToEntityArray(Allocator.Temp);
            var requestData = _requestQuery.ToComponentDataArray<SaveRequest>(Allocator.Temp);

            for (int r = 0; r < requests.Length; r++)
            {
                var req = requestData[r];
                ProcessSaveRequest(manager, req, serverTick);
                EntityManager.DestroyEntity(requests[r]);
            }

            requests.Dispose();
            requestData.Dispose();

            manager.TimeSinceLastSave = 0f;
        }

        private void ProcessSaveRequest(SaveManagerSingleton manager, SaveRequest req, uint serverTick)
        {
            var players = _playerQuery.ToEntityArray(Allocator.Temp);

            for (int p = 0; p < players.Length; p++)
            {
                var playerEntity = players[p];
                var link = EntityManager.GetComponentData<SaveStateLink>(playerEntity);

                // Get player ID
                string playerId = "local";
                if (link.SaveChildEntity != Entity.Null && EntityManager.HasComponent<PlayerSaveId>(link.SaveChildEntity))
                {
                    var saveId = EntityManager.GetComponentData<PlayerSaveId>(link.SaveChildEntity);
                    if (saveId.PlayerId.Length > 0)
                        playerId = saveId.PlayerId.ToString();
                }

                // Filter by target player if specified
                if (req.TargetPlayerId.Length > 0 && playerId != req.TargetPlayerId.ToString())
                    continue;

                try
                {
                    byte[] fileData = SerializePlayer(manager, playerEntity, playerId, req.SlotIndex, serverTick);
                    SaveFileReader.PatchCRC32(fileData);

                    string fileName = $"{playerId}_slot{req.SlotIndex}.dig";
                    string filePath = Path.Combine(manager.SaveDirectory, fileName);
                    string metaPath = Path.ChangeExtension(filePath, ".json");

                    var metadata = BuildMetadata(manager, playerEntity, playerId, req.SlotIndex);
                    string metaJson = JsonUtility.ToJson(metadata, true);

                    SaveFileWriter.EnqueueWrite(filePath, fileData, metaJson, metaPath);

                    // Create SaveComplete entity
                    var completeEntity = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(completeEntity, new SaveComplete
                    {
                        SlotIndex = req.SlotIndex,
                        Success = true,
                        TriggerSource = req.TriggerSource
                    });

                    // Clear dirty flags
                    if (link.SaveChildEntity != Entity.Null && EntityManager.HasComponent<SaveDirtyFlags>(link.SaveChildEntity))
                        EntityManager.SetComponentData(link.SaveChildEntity, new SaveDirtyFlags());
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] Failed to save player {playerId}: {e.Message}");
                    var errorEntity = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(errorEntity, new SaveComplete
                    {
                        SlotIndex = req.SlotIndex,
                        Success = false,
                        ErrorMessage = new FixedString128Bytes(e.Message.Length > 120 ? e.Message.Substring(0, 120) : e.Message),
                        TriggerSource = req.TriggerSource
                    });
                }
            }

            players.Dispose();
        }

        private byte[] SerializePlayer(SaveManagerSingleton manager, Entity playerEntity, string playerId, int slotIndex, uint serverTick)
        {
            using var ms = new MemoryStream(4096);
            using var w = new BinaryWriter(ms);

            // Header
            w.Write(SaveBinaryConstants.MagicPlayer);       // 4 bytes
            w.Write(manager.Config.SaveFormatVersion);       // 4 bytes
            w.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); // 8 bytes

            // Player name (64 bytes, null-padded)
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(playerId);
            byte[] namePadded = new byte[SaveBinaryConstants.PlayerNameFieldSize];
            Buffer.BlockCopy(nameBytes, 0, namePadded, 0, Math.Min(nameBytes.Length, namePadded.Length));
            w.Write(namePadded);

            // CRC32 placeholder (patched after)
            w.Write((uint)0);

            // Module count placeholder
            long moduleCountPos = ms.Position;
            w.Write((short)0);

            // Serialize modules
            var ctx = new SaveContext(EntityManager, playerEntity, manager.Config.SaveFormatVersion, manager.ElapsedPlaytime, serverTick);
            short moduleCount = 0;

            foreach (var module in manager.RegisteredModules)
            {
                if (module.TypeId == SaveModuleTypeIds.World) continue; // World saved separately

                long blockStart = ms.Position;
                w.Write(module.TypeId);           // 4 bytes
                w.Write((short)module.ModuleVersion); // 2 bytes
                long dataLenPos = ms.Position;
                w.Write(0);                       // DataLength placeholder (4 bytes)

                long dataStart = ms.Position;
                int written = module.Serialize(ctx, w);

                // Patch data length
                long dataEnd = ms.Position;
                ms.Position = dataLenPos;
                w.Write((int)(dataEnd - dataStart));
                ms.Position = dataEnd;

                moduleCount++;
            }

            // Patch module count
            long endPos = ms.Position;
            ms.Position = moduleCountPos;
            w.Write(moduleCount);
            ms.Position = endPos;

            // EOF marker
            w.Write(SaveBinaryConstants.EOFMarker);

            return ms.ToArray();
        }

        private SaveMetadata BuildMetadata(SaveManagerSingleton manager, Entity playerEntity, string playerId, int slotIndex)
        {
            int level = 1;
            if (EntityManager.HasComponent<DIG.Combat.Components.CharacterAttributes>(playerEntity))
                level = EntityManager.GetComponentData<DIG.Combat.Components.CharacterAttributes>(playerEntity).Level;

            var moduleNames = new List<string>();
            foreach (var m in manager.RegisteredModules)
            {
                if (m.TypeId != SaveModuleTypeIds.World)
                    moduleNames.Add(m.DisplayName);
            }

            return new SaveMetadata
            {
                SlotIndex = slotIndex,
                PlayerName = playerId,
                CharacterLevel = level,
                PlaytimeSeconds = manager.ElapsedPlaytime,
                SaveTimestampUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                GameVersion = Application.version,
                SaveFormatVersion = manager.Config.SaveFormatVersion,
                ModuleCount = moduleNames.Count,
                ModuleNames = moduleNames.ToArray()
            };
        }
    }
}
