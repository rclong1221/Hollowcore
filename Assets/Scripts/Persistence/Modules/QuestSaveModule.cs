using System.IO;
using Unity.Collections;
using Unity.Entities;
using DIG.Quest;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Serializes completed quests and active quest instance entities.
    /// Graceful no-op if quest types not present in world.
    /// </summary>
    public class QuestSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Quests;
        public string DisplayName => "Quests";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            long start = w.BaseStream.Position;

            // Completed quests
            if (em.HasBuffer<CompletedQuestEntry>(e))
            {
                var completed = em.GetBuffer<CompletedQuestEntry>(e, true);
                w.Write((short)completed.Length);
                for (int i = 0; i < completed.Length; i++)
                {
                    w.Write(completed[i].QuestId);
                    w.Write(completed[i].CompletedAtTick);
                }
            }
            else
            {
                w.Write((short)0);
            }

            // Active quest instances (separate entities linked via QuestPlayerLink)
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<QuestProgress>(),
                ComponentType.ReadOnly<QuestPlayerLink>());
            var entities = query.ToEntityArray(Allocator.Temp);
            var links = query.ToComponentDataArray<QuestPlayerLink>(Allocator.Temp);
            var progresses = query.ToComponentDataArray<QuestProgress>(Allocator.Temp);

            // Count quests belonging to this player
            short activeCount = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                if (links[i].PlayerEntity == e && progresses[i].State == QuestState.Active)
                    activeCount++;
            }
            w.Write(activeCount);

            for (int i = 0; i < entities.Length; i++)
            {
                if (links[i].PlayerEntity != e || progresses[i].State != QuestState.Active)
                    continue;

                var prog = progresses[i];
                w.Write(prog.QuestId);
                w.Write((byte)prog.State);
                w.Write(prog.TimeRemaining);
                w.Write(prog.AcceptedAtTick);

                // Objectives
                if (em.HasBuffer<ObjectiveProgress>(entities[i]))
                {
                    var objectives = em.GetBuffer<ObjectiveProgress>(entities[i], true);
                    w.Write((byte)objectives.Length);
                    for (int j = 0; j < objectives.Length; j++)
                    {
                        var obj = objectives[j];
                        w.Write(obj.ObjectiveId);
                        w.Write((byte)obj.State);
                        w.Write((byte)obj.Type);
                        w.Write(obj.TargetId);
                        w.Write(obj.CurrentCount);
                        w.Write(obj.RequiredCount);
                    }
                }
                else
                {
                    w.Write((byte)0);
                }
            }

            entities.Dispose();
            links.Dispose();
            progresses.Dispose();

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;
            var e = ctx.PlayerEntity;
            var ecb = ctx.ECB;

            // Completed quests
            short completedCount = r.ReadInt16();
            if (em.HasBuffer<CompletedQuestEntry>(e))
            {
                var buffer = em.GetBuffer<CompletedQuestEntry>(e);
                buffer.Clear();
                for (int i = 0; i < completedCount; i++)
                {
                    buffer.Add(new CompletedQuestEntry
                    {
                        QuestId = r.ReadInt32(),
                        CompletedAtTick = r.ReadUInt32()
                    });
                }
            }
            else
            {
                for (int i = 0; i < completedCount; i++) { r.ReadInt32(); r.ReadUInt32(); }
            }

            // Active quests — recreate as entities
            short activeCount = r.ReadInt16();
            for (int i = 0; i < activeCount; i++)
            {
                int questId = r.ReadInt32();
                byte state = r.ReadByte();
                float timeRemaining = r.ReadSingle();
                uint acceptedTick = r.ReadUInt32();

                byte objCount = r.ReadByte();
                var objectives = new ObjectiveProgress[objCount];
                for (int j = 0; j < objCount; j++)
                {
                    objectives[j] = new ObjectiveProgress
                    {
                        ObjectiveId = r.ReadInt32(),
                        State = (ObjectiveState)r.ReadByte(),
                        Type = (ObjectiveType)r.ReadByte(),
                        TargetId = r.ReadInt32(),
                        CurrentCount = r.ReadInt32(),
                        RequiredCount = r.ReadInt32()
                    };
                }

                // Recreate quest instance entity
                var questEntity = ecb.CreateEntity();
                ecb.AddComponent(questEntity, new QuestProgress
                {
                    QuestId = questId,
                    State = (QuestState)state,
                    TimeRemaining = timeRemaining,
                    AcceptedAtTick = acceptedTick
                });
                ecb.AddComponent(questEntity, new QuestPlayerLink { PlayerEntity = e });

                var objBuffer = ecb.AddBuffer<ObjectiveProgress>(questEntity);
                for (int j = 0; j < objCount; j++)
                    objBuffer.Add(objectives[j]);
            }
        }
    }
}
