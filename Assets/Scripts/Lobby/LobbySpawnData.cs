using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Transient ECS singleton bridging lobby → game.
    /// Created in ServerWorld by LobbyToGameTransition.
    /// Consumed by GoInGameServerSystem and SaveIdAssignmentSystem.
    /// Destroyed after all players spawn.
    /// Zero bytes on player entity — lives on its own entity.
    ///
    /// Spawn data is indexed by SLOT INDEX (not NetworkId).
    /// Players are assigned slots in connection order via NextSlotIndex.
    /// </summary>
    public struct LobbySpawnData : IComponentData
    {
        /// <summary>Spawn positions from MapDefinitionSO, indexed by slot.</summary>
        public FixedList128Bytes<float3> SpawnPositions;

        /// <summary>Spawn rotations from MapDefinitionSO, indexed by slot.</summary>
        public FixedList128Bytes<quaternion> SpawnRotations;

        /// <summary>Total number of expected players.</summary>
        public int PlayerCount;

        /// <summary>Next slot to assign to a connecting player. Incremented by GoInGameServerSystem.</summary>
        public int NextSlotIndex;

        /// <summary>Number of players that have spawned so far.</summary>
        public int SpawnedCount;

        /// <summary>
        /// Persistent PlayerId data, indexed by slot.
        /// Layout: [slotIndex:1 byte][idLength:1 byte][idChars:N bytes] repeated.
        /// </summary>
        public FixedList512Bytes<byte> PersistentIdData;

        /// <summary>Number of persistent ID entries.</summary>
        public int PersistentIdCount;

        /// <summary>
        /// Claims the next available slot for a spawning player.
        /// Returns the slot index and increments the counter.
        /// Returns -1 if all slots are taken.
        /// </summary>
        public int ClaimNextSlot()
        {
            if (NextSlotIndex >= PlayerCount)
                return -1;
            return NextSlotIndex++;
        }

        /// <summary>
        /// Returns persistent PlayerId for a given slot index.
        /// </summary>
        public FixedString64Bytes GetPersistentIdForSlot(int slotIndex)
        {
            int offset = 0;
            for (int i = 0; i < PersistentIdCount; i++)
            {
                if (offset + 2 > PersistentIdData.Length) break;

                int storedSlot = PersistentIdData[offset];
                int idLen = PersistentIdData[offset + 1];
                offset += 2;

                if (storedSlot == slotIndex)
                {
                    var result = new FixedString64Bytes();
                    for (int c = 0; c < idLen && offset + c < PersistentIdData.Length; c++)
                        result.Append((char)PersistentIdData[offset + c]);
                    return result;
                }

                offset += idLen;
            }

            return default;
        }

        /// <summary>
        /// Adds a SlotIndex → PlayerId mapping to PersistentIdData.
        /// Called during transition setup.
        /// </summary>
        public void AddPersistentId(int slotIndex, string playerId)
        {
            int idLen = math.min(playerId.Length, 32);
            int bytesNeeded = 2 + idLen; // 1 byte slot + 1 byte length + N bytes id
            if (PersistentIdData.Length + bytesNeeded > PersistentIdData.Capacity)
                return;

            PersistentIdData.Add((byte)slotIndex);
            PersistentIdData.Add((byte)idLen);
            for (int i = 0; i < idLen; i++)
                PersistentIdData.Add((byte)playerId[i]);

            PersistentIdCount++;
        }
    }
}
