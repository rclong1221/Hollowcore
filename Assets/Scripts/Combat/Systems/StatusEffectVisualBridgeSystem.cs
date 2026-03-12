using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Burst;
using DIG.Combat.UI;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// EPIC 15.30: Detects when new StatusEffects appear on entities and enqueues
    /// visual events to StatusVisualQueue for "BLEEDING!", "BURNING!" floating text.
    ///
    /// Design: Burst-compiled IJobEntity handles bitmask delta computation and writes
    /// to a NativeQueue. Managed drain loop moves results to the static StatusVisualQueue.
    /// Same pattern as DamageEventVisualBridgeSystem.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(global::Player.Systems.StatusEffectSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class StatusEffectVisualBridgeSystem : SystemBase
    {
        private NativeHashMap<Entity, int> _previousEffectMasks;
        private NativeQueue<StatusAppliedVisual> _nativeQueue;
        private int _cleanupCounter;
        private const int CleanupInterval = 60;

        protected override void OnCreate()
        {
            _previousEffectMasks = new NativeHashMap<Entity, int>(64, Allocator.Persistent);
            _nativeQueue = new NativeQueue<StatusAppliedVisual>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_previousEffectMasks.IsCreated)
                _previousEffectMasks.Dispose();
            if (_nativeQueue.IsCreated)
                _nativeQueue.Dispose();
        }

        protected override void OnUpdate()
        {
            // Burst-compiled job computes bitmask deltas and writes to NativeQueue
            new StatusVisualJob
            {
                PreviousEffectMasks = _previousEffectMasks,
                OutputQueue = _nativeQueue
            }.Run();

            // Drain Burst-written native queue to managed static queue
            while (_nativeQueue.TryDequeue(out var visual))
            {
                StatusVisualQueue.Enqueue(visual);
            }

            // Periodic cleanup of destroyed entities
            if (++_cleanupCounter >= CleanupInterval)
            {
                _cleanupCounter = 0;
                var entitiesToRemove = new NativeList<Entity>(Allocator.Temp);
                foreach (var kvp in _previousEffectMasks)
                {
                    if (!EntityManager.Exists(kvp.Key))
                        entitiesToRemove.Add(kvp.Key);
                }
                for (int i = 0; i < entitiesToRemove.Length; i++)
                    _previousEffectMasks.Remove(entitiesToRemove[i]);
                entitiesToRemove.Dispose();
            }
        }

        [BurstCompile]
        partial struct StatusVisualJob : IJobEntity
        {
            public NativeHashMap<Entity, int> PreviousEffectMasks;
            public NativeQueue<StatusAppliedVisual> OutputQueue;

            void Execute(in DynamicBuffer<global::Player.Components.StatusEffect> effects,
                         in LocalToWorld localToWorld, Entity entity)
            {
                int currentMask = 0;
                for (int i = 0; i < effects.Length; i++)
                    currentMask |= (1 << (int)effects[i].Type);

                int prevMask = PreviousEffectMasks.TryGetValue(entity, out var m) ? m : 0;
                int newBits = currentMask & ~prevMask;

                if (newBits != 0)
                {
                    float3 pos = localToWorld.Position + new float3(0, 1.5f, 0);
                    for (int bit = 0; bit < 12; bit++)
                    {
                        if ((newBits & (1 << bit)) != 0)
                        {
                            OutputQueue.Enqueue(new StatusAppliedVisual
                            {
                                Type = (global::Player.Components.StatusEffectType)bit,
                                Position = pos
                            });
                        }
                    }
                }

                PreviousEffectMasks[entity] = currentMask;
            }
        }
    }
}
