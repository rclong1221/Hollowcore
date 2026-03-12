using System.IO;
using Unity.Entities;
using DIG.Roguelite;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 23.2: Persists the last N run history entries across sessions. TypeId=17.
    /// Run history lives on the MetaBank entity as a DynamicBuffer.
    /// </summary>
    public class RunHistorySaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.RunHistory;
        public string DisplayName => "Run History";
        public int ModuleVersion => 2;

        private const int MaxHistoryEntries = 50;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            long start = w.BaseStream.Position;

            var query = em.CreateEntityQuery(ComponentType.ReadOnly<MetaBank>());
            if (query.IsEmpty || !em.HasBuffer<RunHistoryEntry>(query.GetSingletonEntity()))
            {
                w.Write((short)0);
                return (int)(w.BaseStream.Position - start);
            }

            var bankEntity = query.GetSingletonEntity();
            var history = em.GetBuffer<RunHistoryEntry>(bankEntity);

            // Only save the most recent entries
            int count = history.Length > MaxHistoryEntries ? MaxHistoryEntries : history.Length;
            int startIndex = history.Length - count;

            w.Write((short)count);
            for (int i = startIndex; i < history.Length; i++)
            {
                var entry = history[i];
                w.Write(entry.RunId);
                w.Write(entry.Seed);
                w.Write(entry.AscensionLevel);
                w.Write((byte)entry.EndReason);
                w.Write(entry.ZonesCleared);
                w.Write(entry.Score);
                w.Write(entry.Duration);
                w.Write(entry.MetaCurrencyEarned);
                w.Write(entry.TotalKills);
                w.Write(entry.Timestamp);
            }

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;

            short count = r.ReadInt16();

            // Read all entries first
            var entries = new RunHistoryEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new RunHistoryEntry
                {
                    RunId = r.ReadUInt32(),
                    Seed = r.ReadUInt32(),
                    AscensionLevel = r.ReadByte(),
                    EndReason = (RunEndReason)r.ReadByte(),
                    ZonesCleared = r.ReadByte(),
                    Score = r.ReadInt32(),
                    Duration = r.ReadSingle(),
                    MetaCurrencyEarned = r.ReadInt32(),
                    TotalKills = blockVersion >= 2 ? r.ReadInt32() : 0,
                    Timestamp = r.ReadInt64()
                };
            }

            // Apply to MetaBank entity if it exists
            var query = em.CreateEntityQuery(ComponentType.ReadWrite<MetaBank>());
            if (query.IsEmpty)
                return;

            var bankEntity = query.GetSingletonEntity();
            if (!em.HasBuffer<RunHistoryEntry>(bankEntity))
                return;

            var history = em.GetBuffer<RunHistoryEntry>(bankEntity);
            history.Clear();
            for (int i = 0; i < entries.Length; i++)
                history.Add(entries[i]);
        }
    }
}
