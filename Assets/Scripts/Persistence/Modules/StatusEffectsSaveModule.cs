using System.IO;
using Unity.Entities;
using Player.Components;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Serializes active StatusEffect buffer entries.
    /// Player quitting mid-bleed resumes with bleed on load.
    /// </summary>
    public class StatusEffectsSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.StatusEffects;
        public string DisplayName => "Status Effects";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            if (!em.HasBuffer<StatusEffect>(e))
            {
                w.Write((byte)0);
                return (int)(w.BaseStream.Position - start);
            }

            var buffer = em.GetBuffer<StatusEffect>(e, true);
            byte count = (byte)System.Math.Min(buffer.Length, 8);
            w.Write(count);

            for (int i = 0; i < count; i++)
            {
                var fx = buffer[i];
                w.Write((byte)fx.Type);
                w.Write(fx.Severity);
                w.Write(fx.TimeRemaining);
                w.Write(fx.TickTimer);
            }

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;

            byte count = r.ReadByte();

            if (!em.HasBuffer<StatusEffect>(e))
            {
                // Skip the data
                for (int i = 0; i < count; i++)
                {
                    r.ReadByte();
                    r.ReadSingle();
                    r.ReadSingle();
                    r.ReadSingle();
                }
                return;
            }

            var buffer = em.GetBuffer<StatusEffect>(e);
            buffer.Clear();

            for (int i = 0; i < count; i++)
            {
                buffer.Add(new StatusEffect
                {
                    Type = (StatusEffectType)r.ReadByte(),
                    Severity = r.ReadSingle(),
                    TimeRemaining = r.ReadSingle(),
                    TickTimer = r.ReadSingle()
                });
            }
        }
    }
}
