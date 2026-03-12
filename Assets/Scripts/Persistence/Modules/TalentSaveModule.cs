using System.IO;
using Unity.Entities;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 17.1: Serializes talent tree allocations and state.
    /// TypeId=11, never changes. Stores TalentState + TalentAllocation buffer + TalentTreeProgress.
    /// </summary>
    public class TalentSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Talents;
        public string DisplayName => "Talents";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            if (!em.HasComponent<DIG.SkillTree.TalentLink>(e))
            {
                WriteEmpty(w);
                return (int)(w.BaseStream.Position - start);
            }

            var link = em.GetComponentData<DIG.SkillTree.TalentLink>(e);
            if (link.TalentChild == Entity.Null || !em.HasComponent<DIG.SkillTree.TalentState>(link.TalentChild))
            {
                WriteEmpty(w);
                return (int)(w.BaseStream.Position - start);
            }

            // Write state
            var state = em.GetComponentData<DIG.SkillTree.TalentState>(link.TalentChild);
            w.Write(state.TotalTalentPoints);
            w.Write(state.SpentTalentPoints);
            w.Write(state.RespecCount);

            // Write allocations
            var allocBuffer = em.GetBuffer<DIG.SkillTree.TalentAllocation>(link.TalentChild, true);
            w.Write((short)allocBuffer.Length);
            for (int i = 0; i < allocBuffer.Length; i++)
            {
                w.Write(allocBuffer[i].TreeId);
                w.Write(allocBuffer[i].NodeId);
            }

            // Write tree progress
            var treeProgBuffer = em.GetBuffer<DIG.SkillTree.TalentTreeProgress>(link.TalentChild, true);
            w.Write((byte)treeProgBuffer.Length);
            for (int i = 0; i < treeProgBuffer.Length; i++)
            {
                w.Write(treeProgBuffer[i].TreeId);
                w.Write(treeProgBuffer[i].PointsSpent);
                w.Write(treeProgBuffer[i].HighestTier);
            }

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;

            int totalPoints = r.ReadInt32();
            int spentPoints = r.ReadInt32();
            byte respecCount = r.ReadByte();

            short allocCount = r.ReadInt16();
            var allocs = new DIG.SkillTree.TalentAllocation[allocCount];
            for (int i = 0; i < allocCount; i++)
            {
                allocs[i] = new DIG.SkillTree.TalentAllocation
                {
                    TreeId = r.ReadUInt16(),
                    NodeId = r.ReadUInt16()
                };
            }

            byte treeProgCount = r.ReadByte();
            var treeProgs = new DIG.SkillTree.TalentTreeProgress[treeProgCount];
            for (int i = 0; i < treeProgCount; i++)
            {
                treeProgs[i] = new DIG.SkillTree.TalentTreeProgress
                {
                    TreeId = r.ReadUInt16(),
                    PointsSpent = r.ReadUInt16(),
                    HighestTier = r.ReadUInt16()
                };
            }

            // Apply to entity
            if (!em.HasComponent<DIG.SkillTree.TalentLink>(e)) return;
            var link = em.GetComponentData<DIG.SkillTree.TalentLink>(e);
            if (link.TalentChild == Entity.Null) return;

            if (em.HasComponent<DIG.SkillTree.TalentState>(link.TalentChild))
            {
                em.SetComponentData(link.TalentChild, new DIG.SkillTree.TalentState
                {
                    TotalTalentPoints = totalPoints,
                    SpentTalentPoints = spentPoints,
                    RespecCount = respecCount
                });
            }

            if (em.HasBuffer<DIG.SkillTree.TalentAllocation>(link.TalentChild))
            {
                var buffer = em.GetBuffer<DIG.SkillTree.TalentAllocation>(link.TalentChild);
                buffer.Clear();
                foreach (var alloc in allocs)
                    buffer.Add(alloc);
            }

            if (em.HasBuffer<DIG.SkillTree.TalentTreeProgress>(link.TalentChild))
            {
                var buffer = em.GetBuffer<DIG.SkillTree.TalentTreeProgress>(link.TalentChild);
                buffer.Clear();
                foreach (var prog in treeProgs)
                    buffer.Add(prog);
            }
        }

        private static void WriteEmpty(BinaryWriter w)
        {
            w.Write(0);       // TotalTalentPoints
            w.Write(0);       // SpentTalentPoints
            w.Write((byte)0); // RespecCount
            w.Write((short)0); // allocCount
            w.Write((byte)0); // treeProgCount
        }
    }
}
