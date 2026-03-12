using System.IO;
using Unity.Entities;
using DIG.Shared;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Serializes InventoryItem buffer (resource quantities) and InventoryCapacity.
    /// </summary>
    public class InventorySaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Inventory;
        public string DisplayName => "Inventory";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            if (!em.HasBuffer<InventoryItem>(e))
            {
                w.Write((short)0);
                return (int)(w.BaseStream.Position - start);
            }

            var buffer = em.GetBuffer<InventoryItem>(e, true);
            short count = (short)System.Math.Min(buffer.Length, 255);
            w.Write(count);

            for (int i = 0; i < count; i++)
            {
                var item = buffer[i];
                w.Write((byte)item.ResourceType);
                w.Write(item.Quantity);
            }

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;

            short count = r.ReadInt16();

            if (!em.HasBuffer<InventoryItem>(e))
            {
                for (int i = 0; i < count; i++) { r.ReadByte(); r.ReadInt32(); }
                return;
            }

            var buffer = em.GetBuffer<InventoryItem>(e);
            buffer.Clear();

            float totalWeight = 0f;
            for (int i = 0; i < count; i++)
            {
                var rt = (ResourceType)r.ReadByte();
                int qty = r.ReadInt32();
                buffer.Add(new InventoryItem { ResourceType = rt, Quantity = qty });
                totalWeight += qty;
            }

            // Recompute weight
            if (em.HasComponent<InventoryCapacity>(e))
            {
                var cap = em.GetComponentData<InventoryCapacity>(e);
                cap.CurrentWeight = totalWeight;
                cap.IsOverencumbered = totalWeight > cap.MaxWeight;
                em.SetComponentData(e, cap);
            }
        }
    }
}
