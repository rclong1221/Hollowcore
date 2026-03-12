using System.IO;
using Unity.Entities;
using DIG.Player;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Serializes survival stats — Hunger, Thirst, Oxygen, Sanity, Infection.
    /// Only Current values persisted (Max/rates are design-time constants from authoring).
    /// </summary>
    public class SurvivalSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Survival;
        public string DisplayName => "Survival Stats";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            w.Write(em.HasComponent<PlayerHunger>(e) ? em.GetComponentData<PlayerHunger>(e).Current : 0f);
            w.Write(em.HasComponent<PlayerThirst>(e) ? em.GetComponentData<PlayerThirst>(e).Current : 0f);
            w.Write(em.HasComponent<PlayerOxygen>(e) ? em.GetComponentData<PlayerOxygen>(e).Current : 0f);
            w.Write(em.HasComponent<PlayerSanity>(e) ? em.GetComponentData<PlayerSanity>(e).Current : 0f);
            w.Write(em.HasComponent<PlayerSanity>(e) ? em.GetComponentData<PlayerSanity>(e).DistortionIntensity : 0f);
            w.Write(em.HasComponent<PlayerInfection>(e) ? em.GetComponentData<PlayerInfection>(e).Current : 0f);

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;

            float hunger = r.ReadSingle();
            float thirst = r.ReadSingle();
            float oxygen = r.ReadSingle();
            float sanity = r.ReadSingle();
            float distortion = r.ReadSingle();
            float infection = r.ReadSingle();

            if (em.HasComponent<PlayerHunger>(e))
            {
                var h = em.GetComponentData<PlayerHunger>(e);
                h.Current = hunger;
                em.SetComponentData(e, h);
            }
            if (em.HasComponent<PlayerThirst>(e))
            {
                var t = em.GetComponentData<PlayerThirst>(e);
                t.Current = thirst;
                em.SetComponentData(e, t);
            }
            if (em.HasComponent<PlayerOxygen>(e))
            {
                var o = em.GetComponentData<PlayerOxygen>(e);
                o.Current = oxygen;
                em.SetComponentData(e, o);
            }
            if (em.HasComponent<PlayerSanity>(e))
            {
                var s = em.GetComponentData<PlayerSanity>(e);
                s.Current = sanity;
                s.DistortionIntensity = distortion;
                em.SetComponentData(e, s);
            }
            if (em.HasComponent<PlayerInfection>(e))
            {
                var inf = em.GetComponentData<PlayerInfection>(e);
                inf.Current = infection;
                em.SetComponentData(e, inf);
            }
        }
    }
}
