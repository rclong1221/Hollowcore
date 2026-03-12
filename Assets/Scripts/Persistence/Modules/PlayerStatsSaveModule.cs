using System.IO;
using Unity.Entities;
using Unity.Transforms;
using Player.Components;
using DIG.Combat.Components;
using DIG.Combat.Resources;
using DIG.Economy;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Serializes core player stats — Health, Stamina, Shield,
    /// ResourcePool, CurrencyInventory, CharacterAttributes, position, playtime.
    /// </summary>
    public class PlayerStatsSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.PlayerStats;
        public string DisplayName => "Player Stats";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            // Health (8 bytes)
            if (em.HasComponent<Health>(e))
            {
                var h = em.GetComponentData<Health>(e);
                w.Write(h.Current);
                w.Write(h.Max);
            }
            else { w.Write(0f); w.Write(0f); }

            // PlayerStamina (16 bytes)
            if (em.HasComponent<PlayerStamina>(e))
            {
                var s = em.GetComponentData<PlayerStamina>(e);
                w.Write(s.Current);
                w.Write(s.Max);
                w.Write(s.RegenRate);
                w.Write(s.DrainRate);
            }
            else { w.Write(0f); w.Write(0f); w.Write(0f); w.Write(0f); }

            // ShieldComponent (8 bytes)
            if (em.HasComponent<ShieldComponent>(e))
            {
                var sh = em.GetComponentData<ShieldComponent>(e);
                w.Write(sh.Current);
                w.Write(sh.Max);
            }
            else { w.Write(0f); w.Write(0f); }

            // ResourcePool — Slot0 (13 bytes) + Slot1 (13 bytes) = 26 bytes
            if (em.HasComponent<ResourcePool>(e))
            {
                var rp = em.GetComponentData<ResourcePool>(e);
                WriteSlot(w, rp.Slot0);
                WriteSlot(w, rp.Slot1);
            }
            else
            {
                WriteEmptySlot(w);
                WriteEmptySlot(w);
            }

            // CurrencyInventory (12 bytes)
            if (em.HasComponent<CurrencyInventory>(e))
            {
                var c = em.GetComponentData<CurrencyInventory>(e);
                w.Write(c.Gold);
                w.Write(c.Premium);
                w.Write(c.Crafting);
            }
            else { w.Write(0); w.Write(0); w.Write(0); }

            // CharacterAttributes (20 bytes)
            if (em.HasComponent<CharacterAttributes>(e))
            {
                var a = em.GetComponentData<CharacterAttributes>(e);
                w.Write(a.Strength);
                w.Write(a.Dexterity);
                w.Write(a.Intelligence);
                w.Write(a.Vitality);
                w.Write(a.Level);
            }
            else { w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(1); }

            // Playtime (4 bytes)
            w.Write(ctx.ElapsedPlaytime);

            // Spawn position (12 bytes)
            if (em.HasComponent<LocalTransform>(e))
            {
                var pos = em.GetComponentData<LocalTransform>(e).Position;
                w.Write(pos.x);
                w.Write(pos.y);
                w.Write(pos.z);
            }
            else { w.Write(0f); w.Write(0f); w.Write(0f); }

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;

            // Health
            float hCurrent = r.ReadSingle(), hMax = r.ReadSingle();
            if (em.HasComponent<Health>(e))
                em.SetComponentData(e, new Health { Current = hCurrent, Max = hMax });

            // PlayerStamina
            float sCur = r.ReadSingle(), sMax = r.ReadSingle(), sRegen = r.ReadSingle(), sDrain = r.ReadSingle();
            if (em.HasComponent<PlayerStamina>(e))
            {
                var stamina = em.GetComponentData<PlayerStamina>(e);
                stamina.Current = sCur;
                stamina.Max = sMax;
                stamina.RegenRate = sRegen;
                stamina.DrainRate = sDrain;
                em.SetComponentData(e, stamina);
            }

            // Shield
            float shCur = r.ReadSingle(), shMax = r.ReadSingle();
            if (em.HasComponent<ShieldComponent>(e))
            {
                var shield = em.GetComponentData<ShieldComponent>(e);
                shield.Current = shCur;
                shield.Max = shMax;
                em.SetComponentData(e, shield);
            }

            // ResourcePool
            var slot0 = ReadSlot(r);
            var slot1 = ReadSlot(r);
            if (em.HasComponent<ResourcePool>(e))
            {
                var rp = em.GetComponentData<ResourcePool>(e);
                rp.Slot0.Current = slot0.Current;
                rp.Slot0.Type = slot0.Type;
                rp.Slot0.Max = slot0.Max;
                rp.Slot1.Current = slot1.Current;
                rp.Slot1.Type = slot1.Type;
                rp.Slot1.Max = slot1.Max;
                em.SetComponentData(e, rp);
            }

            // Currency
            int gold = r.ReadInt32(), premium = r.ReadInt32(), crafting = r.ReadInt32();
            if (em.HasComponent<CurrencyInventory>(e))
                em.SetComponentData(e, new CurrencyInventory { Gold = gold, Premium = premium, Crafting = crafting });

            // CharacterAttributes
            int str = r.ReadInt32(), dex = r.ReadInt32(), intel = r.ReadInt32(), vit = r.ReadInt32(), level = r.ReadInt32();
            if (em.HasComponent<CharacterAttributes>(e))
            {
                var attrs = em.GetComponentData<CharacterAttributes>(e);
                attrs.Strength = str;
                attrs.Dexterity = dex;
                attrs.Intelligence = intel;
                attrs.Vitality = vit;
                attrs.Level = level;
                em.SetComponentData(e, attrs);
            }

            // Playtime (consumed by SaveManager)
            r.ReadSingle();

            // Spawn position
            float px = r.ReadSingle(), py = r.ReadSingle(), pz = r.ReadSingle();
            if (em.HasComponent<LocalTransform>(e))
            {
                var lt = em.GetComponentData<LocalTransform>(e);
                lt.Position = new Unity.Mathematics.float3(px, py, pz);
                em.SetComponentData(e, lt);
            }
        }

        private static void WriteSlot(BinaryWriter w, ResourceSlot slot)
        {
            w.Write((byte)slot.Type);
            w.Write(slot.Current);
            w.Write(slot.Max);
            w.Write(slot.RegenRate);
        }

        private static void WriteEmptySlot(BinaryWriter w)
        {
            w.Write((byte)0);
            w.Write(0f);
            w.Write(0f);
            w.Write(0f);
        }

        private struct SlotData { public DIG.Combat.Resources.ResourceType Type; public float Current, Max; }
        private static SlotData ReadSlot(BinaryReader r)
        {
            return new SlotData
            {
                Type = (DIG.Combat.Resources.ResourceType)r.ReadByte(),
                Current = r.ReadSingle(),
                Max = r.ReadSingle()
            };
            // RegenRate read but not applied (design-time value)
        }
    }
}
