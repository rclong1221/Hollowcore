using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 17.7: Serializes achievement progress buffer + cumulative stats.
    /// TypeId=14. Binary format: HasData(1 byte) + CumulativeStats(44 bytes) + ProgressCount(2) + entries(12 each).
    /// Max size: ~1,583 bytes for 128 achievements.
    /// </summary>
    public class AchievementSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Achievements;
        public string DisplayName => "Achievements";
        public int ModuleVersion => 1;

        public bool IsDirty(in SaveContext ctx)
        {
            var em = ctx.EntityManager;
            var player = ctx.PlayerEntity;

            if (!em.HasComponent<DIG.Achievement.AchievementLink>(player)) return false;

            var link = em.GetComponentData<DIG.Achievement.AchievementLink>(player);
            if (link.AchievementChild == Entity.Null) return false;
            if (!em.HasComponent<DIG.Achievement.AchievementDirtyFlags>(link.AchievementChild)) return false;

            var flags = em.GetComponentData<DIG.Achievement.AchievementDirtyFlags>(link.AchievementChild);
            return flags.Flags != 0;
        }

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var player = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            if (!em.HasComponent<DIG.Achievement.AchievementLink>(player))
            {
                w.Write((byte)0); // HasData = false
                return (int)(w.BaseStream.Position - start);
            }

            var link = em.GetComponentData<DIG.Achievement.AchievementLink>(player);
            if (link.AchievementChild == Entity.Null ||
                !em.HasComponent<DIG.Achievement.AchievementCumulativeStats>(link.AchievementChild))
            {
                w.Write((byte)0); // HasData = false
                return (int)(w.BaseStream.Position - start);
            }

            w.Write((byte)1); // HasData = true

            var childEntity = link.AchievementChild;

            // Write cumulative stats (44 bytes)
            var stats = em.GetComponentData<DIG.Achievement.AchievementCumulativeStats>(childEntity);
            w.Write(stats.TotalKills);
            w.Write(stats.TotalDeaths);
            w.Write(stats.TotalQuestsCompleted);
            w.Write(stats.TotalItemsCrafted);
            w.Write(stats.TotalNPCsInteracted);
            w.Write(stats.TotalDamageDealt);
            w.Write(stats.TotalLootCollected);
            w.Write(stats.HighestKillStreak);
            w.Write(stats.CurrentKillStreak);
            w.Write(stats.ConsecutiveLoginDays);

            // Write progress entries
            var buffer = em.GetBuffer<DIG.Achievement.AchievementProgress>(childEntity, true);
            w.Write((short)buffer.Length);

            for (int i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                w.Write(entry.AchievementId);
                w.Write(entry.CurrentValue);
                w.Write(entry.IsUnlocked ? (byte)1 : (byte)0);
                w.Write(entry.HighestTierUnlocked);
                w.Write(entry.UnlockTick);
            }

            // Clear dirty flags after save
            if (em.HasComponent<DIG.Achievement.AchievementDirtyFlags>(childEntity))
            {
                em.SetComponentData(childEntity, new DIG.Achievement.AchievementDirtyFlags { Flags = 0 });
            }

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;
            var player = ctx.PlayerEntity;

            byte hasData = r.ReadByte();
            if (hasData == 0) return; // No achievement data in this save

            // Read cumulative stats (44 bytes)
            var stats = new DIG.Achievement.AchievementCumulativeStats
            {
                TotalKills = r.ReadInt32(),
                TotalDeaths = r.ReadInt32(),
                TotalQuestsCompleted = r.ReadInt32(),
                TotalItemsCrafted = r.ReadInt32(),
                TotalNPCsInteracted = r.ReadInt32(),
                TotalDamageDealt = r.ReadInt64(),
                TotalLootCollected = r.ReadInt32(),
                HighestKillStreak = r.ReadInt32(),
                CurrentKillStreak = r.ReadInt32(),
                ConsecutiveLoginDays = r.ReadInt32()
            };

            // Read progress entries
            short progressCount = r.ReadInt16();
            var entries = new DIG.Achievement.AchievementProgress[progressCount];
            for (int i = 0; i < progressCount; i++)
            {
                entries[i] = new DIG.Achievement.AchievementProgress
                {
                    AchievementId = r.ReadUInt16(),
                    CurrentValue = r.ReadInt32(),
                    IsUnlocked = r.ReadByte() != 0,
                    HighestTierUnlocked = r.ReadByte(),
                    UnlockTick = r.ReadUInt32()
                };
            }

            // Apply to child entity
            if (!em.HasComponent<DIG.Achievement.AchievementLink>(player)) return;

            var link = em.GetComponentData<DIG.Achievement.AchievementLink>(player);
            if (link.AchievementChild == Entity.Null) return;

            var childEntity = link.AchievementChild;

            if (em.HasComponent<DIG.Achievement.AchievementCumulativeStats>(childEntity))
                em.SetComponentData(childEntity, stats);

            if (em.HasBuffer<DIG.Achievement.AchievementProgress>(childEntity))
            {
                var buffer = em.GetBuffer<DIG.Achievement.AchievementProgress>(childEntity);
                buffer.Clear();
                for (int i = 0; i < entries.Length; i++)
                    buffer.Add(entries[i]);
            }

            // Mark clean after load
            if (em.HasComponent<DIG.Achievement.AchievementDirtyFlags>(childEntity))
                em.SetComponentData(childEntity, new DIG.Achievement.AchievementDirtyFlags { Flags = 0 });
        }
    }
}
