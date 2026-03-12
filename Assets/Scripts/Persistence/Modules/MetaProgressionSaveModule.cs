using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using DIG.Roguelite;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 23.2: Persists MetaBank (currency, lifetime stats) and MetaUnlockEntry
    /// (unlock flags) across sessions. TypeId=16.
    /// </summary>
    public class MetaProgressionSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.MetaProgression;
        public string DisplayName => "Meta Progression";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            long start = w.BaseStream.Position;

            // Find MetaBank singleton
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<MetaBank>());
            if (query.IsEmpty)
            {
                w.Write(false); // hasData marker
                return (int)(w.BaseStream.Position - start);
            }

            w.Write(true); // hasData marker

            var bankEntity = query.GetSingletonEntity();
            var bank = em.GetComponentData<MetaBank>(bankEntity);

            // Serialize MetaBank fields
            w.Write(bank.MetaCurrency);
            w.Write(bank.LifetimeMetaEarned);
            w.Write(bank.TotalRunsAttempted);
            w.Write(bank.TotalRunsWon);
            w.Write(bank.BestScore);
            w.Write(bank.BestZoneReached);
            w.Write(bank.TotalPlaytime);

            // Serialize unlock states (only UnlockId + IsUnlocked — definitions come from SO)
            var unlocks = em.GetBuffer<MetaUnlockEntry>(bankEntity);
            w.Write((short)unlocks.Length);
            for (int i = 0; i < unlocks.Length; i++)
            {
                w.Write(unlocks[i].UnlockId);
                w.Write(unlocks[i].IsUnlocked);
            }

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;

            bool hasData = r.ReadBoolean();
            if (!hasData)
                return;

            // Read MetaBank fields
            int metaCurrency = r.ReadInt32();
            int lifetimeMetaEarned = r.ReadInt32();
            int totalRunsAttempted = r.ReadInt32();
            int totalRunsWon = r.ReadInt32();
            int bestScore = r.ReadInt32();
            byte bestZoneReached = r.ReadByte();
            float totalPlaytime = r.ReadSingle();

            // Read unlock states
            short unlockCount = r.ReadInt16();
            var unlockStates = new (int id, bool unlocked)[unlockCount];
            for (int i = 0; i < unlockCount; i++)
            {
                unlockStates[i] = (r.ReadInt32(), r.ReadBoolean());
            }

            // Apply to MetaBank entity if it exists
            var query = em.CreateEntityQuery(ComponentType.ReadWrite<MetaBank>());
            if (query.IsEmpty)
                return;

            var bankEntity = query.GetSingletonEntity();

            em.SetComponentData(bankEntity, new MetaBank
            {
                MetaCurrency = metaCurrency,
                LifetimeMetaEarned = lifetimeMetaEarned,
                TotalRunsAttempted = totalRunsAttempted,
                TotalRunsWon = totalRunsWon,
                BestScore = bestScore,
                BestZoneReached = bestZoneReached,
                TotalPlaytime = totalPlaytime
            });

            // Apply unlock states — match by UnlockId (SO may have added/removed entries)
            if (em.HasBuffer<MetaUnlockEntry>(bankEntity))
            {
                // Build O(1) lookup from saved states
                var stateMap = new Dictionary<int, bool>(unlockCount);
                for (int j = 0; j < unlockStates.Length; j++)
                    stateMap[unlockStates[j].id] = unlockStates[j].unlocked;

                var unlocks = em.GetBuffer<MetaUnlockEntry>(bankEntity);
                for (int i = 0; i < unlocks.Length; i++)
                {
                    var entry = unlocks[i];
                    if (stateMap.TryGetValue(entry.UnlockId, out bool wasUnlocked))
                    {
                        entry.IsUnlocked = wasUnlocked;
                        unlocks[i] = entry;
                    }
                }
            }
        }
    }
}
