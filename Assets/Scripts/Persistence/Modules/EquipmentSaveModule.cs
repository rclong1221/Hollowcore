using System.IO;
using Unity.Entities;
using DIG.Items;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Serializes equipped items with stats, durability, and affixes.
    /// Entity handles replaced with stable content identifiers.
    /// On load, recreates item entities via ECB.
    /// </summary>
    public class EquipmentSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Equipment;
        public string DisplayName => "Equipment";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            if (!em.HasBuffer<EquippedItemElement>(e))
            {
                w.Write((byte)0);
                return (int)(w.BaseStream.Position - start);
            }

            var equipped = em.GetBuffer<EquippedItemElement>(e, true);
            byte itemCount = 0;

            // Count valid items
            for (int i = 0; i < equipped.Length; i++)
            {
                if (equipped[i].ItemEntity != Entity.Null && em.HasComponent<CharacterItem>(equipped[i].ItemEntity))
                    itemCount++;
            }

            w.Write(itemCount);

            for (int i = 0; i < equipped.Length; i++)
            {
                var slot = equipped[i];
                if (slot.ItemEntity == Entity.Null || !em.HasComponent<CharacterItem>(slot.ItemEntity))
                    continue;

                var ci = em.GetComponentData<CharacterItem>(slot.ItemEntity);
                w.Write(ci.SlotId);
                w.Write(slot.QuickSlot);
                w.Write(ci.ItemTypeId);
                w.Write((byte)0); // ItemCategory placeholder
                w.Write((byte)ci.State);

                // ItemStatBlock (17 floats = 68 bytes)
                if (em.HasComponent<ItemStatBlock>(slot.ItemEntity))
                {
                    var sb = em.GetComponentData<ItemStatBlock>(slot.ItemEntity);
                    w.Write(sb.BaseDamage);
                    w.Write(sb.AttackSpeed);
                    w.Write(sb.CritChance);
                    w.Write(sb.CritMultiplier);
                    w.Write(sb.Armor);
                    w.Write(sb.MaxHealthBonus);
                    w.Write(sb.MovementSpeedBonus);
                    w.Write(sb.DamageResistance);
                    w.Write(sb.MaxManaBonus);
                    w.Write(sb.ManaRegenBonus);
                    w.Write(sb.MaxEnergyBonus);
                    w.Write(sb.EnergyRegenBonus);
                    w.Write(sb.MaxStaminaBonus);
                    w.Write(sb.StaminaRegenBonus);
                    w.Write(sb.XPBonusPercent);
                }
                else
                {
                    for (int f = 0; f < 15; f++) w.Write(0f);
                }

                // Durability
                bool hasDurability = em.HasComponent<DIG.Player.WeaponDurability>(slot.ItemEntity);
                w.Write(hasDurability ? (byte)1 : (byte)0);
                if (hasDurability)
                {
                    var dur = em.GetComponentData<DIG.Player.WeaponDurability>(slot.ItemEntity);
                    w.Write(dur.Current);
                    w.Write(dur.IsBroken ? (byte)1 : (byte)0);
                }

                // Affixes
                if (em.HasBuffer<ItemAffix>(slot.ItemEntity))
                {
                    var affixes = em.GetBuffer<ItemAffix>(slot.ItemEntity, true);
                    byte affixCount = (byte)System.Math.Min(affixes.Length, 4);
                    w.Write(affixCount);
                    for (int a = 0; a < affixCount; a++)
                    {
                        var affix = affixes[a];
                        w.Write(affix.AffixId);
                        w.Write((byte)affix.Slot);
                        w.Write(affix.Value);
                        w.Write(affix.Tier);
                    }
                }
                else
                {
                    w.Write((byte)0);
                }
            }

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            var ecb = ctx.ECB;

            byte itemCount = r.ReadByte();

            // Clear existing equipped items
            if (em.HasBuffer<EquippedItemElement>(e))
                em.GetBuffer<EquippedItemElement>(e).Clear();

            for (int i = 0; i < itemCount; i++)
            {
                int slotId = r.ReadInt32();
                int quickSlot = r.ReadInt32();
                int itemTypeId = r.ReadInt32();
                byte category = r.ReadByte();
                byte state = r.ReadByte();

                // ItemStatBlock
                var statBlock = new ItemStatBlock
                {
                    BaseDamage = r.ReadSingle(),
                    AttackSpeed = r.ReadSingle(),
                    CritChance = r.ReadSingle(),
                    CritMultiplier = r.ReadSingle(),
                    Armor = r.ReadSingle(),
                    MaxHealthBonus = r.ReadSingle(),
                    MovementSpeedBonus = r.ReadSingle(),
                    DamageResistance = r.ReadSingle(),
                    MaxManaBonus = r.ReadSingle(),
                    ManaRegenBonus = r.ReadSingle(),
                    MaxEnergyBonus = r.ReadSingle(),
                    EnergyRegenBonus = r.ReadSingle(),
                    MaxStaminaBonus = r.ReadSingle(),
                    StaminaRegenBonus = r.ReadSingle(),
                    XPBonusPercent = r.ReadSingle()
                };

                // Durability
                bool hasDurability = r.ReadByte() == 1;
                float durCurrent = 0f;
                bool durBroken = false;
                if (hasDurability)
                {
                    durCurrent = r.ReadSingle();
                    durBroken = r.ReadByte() == 1;
                }

                // Affixes
                byte affixCount = r.ReadByte();
                var affixes = new ItemAffix[affixCount];
                for (int a = 0; a < affixCount; a++)
                {
                    affixes[a] = new ItemAffix
                    {
                        AffixId = r.ReadInt32(),
                        Slot = (AffixSlot)r.ReadByte(),
                        Value = r.ReadSingle(),
                        Tier = r.ReadInt32()
                    };
                }

                // Create item entity via ECB
                var itemEntity = ecb.CreateEntity();
                ecb.AddComponent(itemEntity, new CharacterItem
                {
                    ItemTypeId = itemTypeId,
                    SlotId = slotId,
                    OwnerEntity = e,
                    State = (ItemState)state
                });
                ecb.AddComponent(itemEntity, statBlock);

                if (hasDurability)
                {
                    ecb.AddComponent(itemEntity, new DIG.Player.WeaponDurability
                    {
                        Current = durCurrent,
                        IsBroken = durBroken
                    });
                }

                var affixBuffer = ecb.AddBuffer<ItemAffix>(itemEntity);
                for (int a = 0; a < affixCount; a++)
                    affixBuffer.Add(affixes[a]);

                // Add to equipped buffer
                if (em.HasBuffer<EquippedItemElement>(e))
                {
                    var equipped = em.GetBuffer<EquippedItemElement>(e);
                    equipped.Add(new EquippedItemElement
                    {
                        ItemEntity = itemEntity,
                        QuickSlot = quickSlot
                    });
                }
            }
        }
    }
}
