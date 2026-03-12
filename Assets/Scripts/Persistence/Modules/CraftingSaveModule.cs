using System.IO;
using Unity.Entities;
using DIG.Crafting;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Serializes known recipe IDs from the crafting knowledge child entity.
    /// Graceful skip if CraftingKnowledgeLink is absent.
    /// </summary>
    public class CraftingSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Crafting;
        public string DisplayName => "Crafting Knowledge";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            if (!em.HasComponent<CraftingKnowledgeLink>(e))
            {
                w.Write((short)0);
                return (int)(w.BaseStream.Position - start);
            }

            var link = em.GetComponentData<CraftingKnowledgeLink>(e);
            if (link.KnowledgeEntity == Entity.Null || !em.HasBuffer<KnownRecipeElement>(link.KnowledgeEntity))
            {
                w.Write((short)0);
                return (int)(w.BaseStream.Position - start);
            }

            var known = em.GetBuffer<KnownRecipeElement>(link.KnowledgeEntity, true);
            w.Write((short)known.Length);
            for (int i = 0; i < known.Length; i++)
                w.Write(known[i].RecipeId);

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;

            short count = r.ReadInt16();

            if (!em.HasComponent<CraftingKnowledgeLink>(e))
            {
                for (int i = 0; i < count; i++) r.ReadInt32();
                return;
            }

            var link = em.GetComponentData<CraftingKnowledgeLink>(e);
            if (link.KnowledgeEntity == Entity.Null || !em.HasBuffer<KnownRecipeElement>(link.KnowledgeEntity))
            {
                for (int i = 0; i < count; i++) r.ReadInt32();
                return;
            }

            var buffer = em.GetBuffer<KnownRecipeElement>(link.KnowledgeEntity);
            buffer.Clear();
            for (int i = 0; i < count; i++)
                buffer.Add(new KnownRecipeElement { RecipeId = r.ReadInt32() });
        }
    }
}
