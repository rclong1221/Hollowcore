using System;
using System.IO;
using Unity.Entities;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Serializes PlayerProgression — XP, stat points, rested XP.
    /// Separated from PlayerStats for independent version lifecycle.
    /// Includes logout timestamp for rested XP accumulation on load.
    /// </summary>
    public class ProgressionSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Progression;
        public string DisplayName => "Progression";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            if (!em.HasComponent<DIG.Progression.PlayerProgression>(e))
            {
                w.Write(0); // CurrentXP
                w.Write(0); // TotalXPEarned
                w.Write(0); // UnspentStatPoints
                w.Write(0f); // RestedXP
                w.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); // LastLogoutTime
                return (int)(w.BaseStream.Position - start);
            }

            var prog = em.GetComponentData<DIG.Progression.PlayerProgression>(e);
            w.Write(prog.CurrentXP);
            w.Write(prog.TotalXPEarned);
            w.Write(prog.UnspentStatPoints);
            w.Write(prog.RestedXP);
            w.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;

            int currentXP = r.ReadInt32();
            int totalXP = r.ReadInt32();
            int statPoints = r.ReadInt32();
            float restedXP = r.ReadSingle();
            long logoutTimeMs = r.ReadInt64();

            if (!em.HasComponent<DIG.Progression.PlayerProgression>(e))
                return;

            // Calculate rested XP accumulation from offline time
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            double offlineHours = (nowMs - logoutTimeMs) / 3600000.0;

            // Read rested config from ProgressionConfigSingleton if available
            float accumRate = 500f;
            float maxDays = 3f;
            // Clamp offline hours to max days
            offlineHours = Math.Min(offlineHours, maxDays * 24.0);
            if (offlineHours > 0)
                restedXP += (float)(accumRate * offlineHours);

            em.SetComponentData(e, new DIG.Progression.PlayerProgression
            {
                CurrentXP = currentXP,
                TotalXPEarned = totalXP,
                UnspentStatPoints = statPoints,
                RestedXP = restedXP
            });
        }
    }
}
