using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Crafting;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12 + 16.13: Watches CraftQueueElement for Complete state transitions,
    /// emits QuestEvent(Craft, recipeId) for the crafting player.
    /// Uses a NativeHashSet to track already-emitted completions and avoid duplicates
    /// (outputs persist until collected, but quest events should fire once).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(QuestEvaluationSystemGroup))]
    public partial class CraftQuestEventEmitterSystem : SystemBase
    {
        private EntityQuery _stationQuery;
        private NativeHashSet<long> _emittedSet;

        protected override void OnCreate()
        {
            _stationQuery = GetEntityQuery(
                ComponentType.ReadOnly<CraftingStation>(),
                ComponentType.ReadOnly<CraftOutputElement>());
            _emittedSet = new NativeHashSet<long>(64, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_emittedSet.IsCreated) _emittedSet.Dispose();
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = _stationQuery.ToEntityArray(Allocator.Temp);
            var activeKeys = new NativeHashSet<long>(64, Allocator.Temp);

            for (int s = 0; s < entities.Length; s++)
            {
                var outputs = EntityManager.GetBuffer<CraftOutputElement>(entities[s], true);
                for (int o = 0; o < outputs.Length; o++)
                {
                    var output = outputs[o];
                    if (output.ForPlayer == Entity.Null) continue;

                    // Unique key: station entity index + output index + recipe
                    long key = ((long)entities[s].Index << 32) | ((uint)output.RecipeId ^ ((uint)o << 16));
                    activeKeys.Add(key);

                    if (_emittedSet.Contains(key)) continue;
                    _emittedSet.Add(key);

                    var eventEntity = ecb.CreateEntity();
                    ecb.AddComponent(eventEntity, new QuestEvent
                    {
                        EventType = ObjectiveType.Craft,
                        TargetId = output.RecipeId,
                        Count = 1,
                        SourcePlayer = output.ForPlayer
                    });
                    ecb.AddComponent<QuestEventTag>(eventEntity);
                }
            }

            // Clean stale entries (collected outputs)
            var toRemove = new NativeList<long>(16, Allocator.Temp);
            foreach (var key in _emittedSet)
            {
                if (!activeKeys.Contains(key))
                    toRemove.Add(key);
            }
            for (int i = 0; i < toRemove.Length; i++)
                _emittedSet.Remove(toRemove[i]);

            activeKeys.Dispose();
            toRemove.Dispose();
            entities.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
