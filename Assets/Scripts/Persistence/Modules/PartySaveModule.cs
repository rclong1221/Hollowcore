using System.IO;
using Unity.Entities;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 17.2: Serializes party preferences. TypeId=12.
    /// Party membership is session-scoped — does NOT recreate party entities on load.
    /// Only persists preferred loot mode and last party member IDs for reconnection.
    /// </summary>
    public class PartySaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Party;
        public string DisplayName => "Party";
        public int ModuleVersion => 1;

        // Managed cache for preferred loot mode (restored on party formation)
        public static DIG.Party.LootMode PreferredLootMode { get; private set; } = DIG.Party.LootMode.FreeForAll;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            if (!em.HasComponent<DIG.Party.PartyLink>(e))
            {
                WriteEmpty(w);
                return (int)(w.BaseStream.Position - start);
            }

            var link = em.GetComponentData<DIG.Party.PartyLink>(e);
            bool inParty = link.PartyEntity != Entity.Null &&
                           em.Exists(link.PartyEntity) &&
                           em.HasComponent<DIG.Party.PartyState>(link.PartyEntity);

            w.Write(inParty ? (byte)1 : (byte)0);

            if (inParty)
            {
                var state = em.GetComponentData<DIG.Party.PartyState>(link.PartyEntity);
                w.Write((byte)state.LootMode);

                // Write member count + entity indices (for reconnect matching)
                var members = em.GetBuffer<DIG.Party.PartyMemberElement>(link.PartyEntity, true);
                w.Write((byte)members.Length);
                for (int i = 0; i < members.Length; i++)
                    w.Write(members[i].PlayerEntity.Index);
            }
            else
            {
                w.Write((byte)PreferredLootMode);
                w.Write((byte)0); // no members
            }

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            byte inParty = r.ReadByte();
            byte lootMode = r.ReadByte();
            byte memberCount = r.ReadByte();

            // Read member indices (for future reconnect feature)
            for (int i = 0; i < memberCount; i++)
                r.ReadInt32();

            // Cache preferred loot mode for next party formation
            if (lootMode <= (byte)DIG.Party.LootMode.MasterLoot)
                PreferredLootMode = (DIG.Party.LootMode)lootMode;
        }

        public bool IsDirty(in SaveContext ctx)
        {
            // Only dirty when PartyLink changes (join/leave) — very rare
            var em = ctx.EntityManager;
            if (!em.HasComponent<DIG.Party.PartyLink>(ctx.PlayerEntity))
                return false;
            var link = em.GetComponentData<DIG.Party.PartyLink>(ctx.PlayerEntity);
            return link.PartyEntity != Entity.Null;
        }

        private static void WriteEmpty(BinaryWriter w)
        {
            w.Write((byte)0); // not in party
            w.Write((byte)0); // default loot mode
            w.Write((byte)0); // no members
        }
    }
}
