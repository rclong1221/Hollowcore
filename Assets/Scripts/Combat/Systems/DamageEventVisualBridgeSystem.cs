using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Transforms;
using DIG.Combat.UI;
using DIG.Combat.Utility;
using SurvivalDamageEvent = global::Player.Components.DamageEvent;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Server-side bridge that reads DamageEvent buffers BEFORE DamageApplySystem clears them,
    /// and enqueues visual data to DamageVisualQueue for the client-side CombatUIBridgeSystem.
    ///
    /// The inner loop is Burst-compiled via IJobEntity, writing directly to the shared NativeQueue.
    /// Managed OnUpdate handles RPC broadcast to remote clients.
    /// </summary>
    [UpdateInGroup(typeof(global::Player.Systems.DamageSystemGroup))]
    [UpdateBefore(typeof(global::Player.Systems.DamageApplySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class DamageEventVisualBridgeSystem : SystemBase
    {
        private NativeList<DamageVisualData> _rpcBatch;
        private bool _isServer;

        protected override void OnCreate()
        {
            DamageVisualQueue.Initialize();
            _rpcBatch = new NativeList<DamageVisualData>(16, Allocator.Persistent);
            _isServer = World.Name == "ServerWorld";
            if (_isServer)
            {
                DamageVisualQueue.InitializePendingRpcs();
            }
        }

        protected override void OnDestroy()
        {
            if (_rpcBatch.IsCreated)
                _rpcBatch.Dispose();
            DamageVisualQueue.Dispose();
        }

        protected override void OnUpdate()
        {
            // Complete any in-flight jobs writing to DamageEvent buffers
            CompleteDependency();

            // Burst job writes directly to the shared NativeQueue (no intermediate copy)
            var sharedQueue = DamageVisualQueue.NativeQueueDirect;

            if (_isServer)
            {
                // Server path: Burst job writes to a NativeList so we can batch RPCs,
                // then we copy to the shared queue + send RPCs in one managed pass.
                _rpcBatch.Clear();

                new VisualBridgeJobBatched
                {
                    VisualBatch = _rpcBatch,
                    CombatHints = DamageVisualQueue.CombatHintsNative
                }.Run();

                // Queue visuals to shared queue + defer RPC creation to DamageVisualRpcSendSystem
                // (RPCs created during PredictedFixedStep get caught in prediction rollback)
                if (_rpcBatch.Length > 0)
                {
                    for (int i = 0; i < _rpcBatch.Length; i++)
                    {
                        var data = _rpcBatch[i];
                        sharedQueue.Enqueue(data);

                        DamageVisualQueue.EnqueueServerRpc(new DamageVisualRpc
                        {
                            Damage = data.Damage,
                            HitPosition = data.HitPosition,
                            HitType = (byte)data.HitType,
                            DamageType = (byte)data.DamageType,
                            Flags = (byte)data.Flags,
                            IsDOT = data.IsDOT ? (byte)1 : (byte)0,
                            SourceNetworkId = data.SourceNetworkId
                        });
                    }
                }
            }
            else
            {
                // Local/listen path: Burst job writes directly to the shared NativeQueue
                new VisualBridgeJob
                {
                    VisualQueue = sharedQueue,
                    CombatHints = DamageVisualQueue.CombatHintsNative
                }.Run();
            }

            DamageVisualQueue.ClearCombatHints();
        }

        [BurstCompile]
        partial struct VisualBridgeJob : IJobEntity
        {
            public NativeQueue<DamageVisualData> VisualQueue;
            public NativeHashMap<int, CombatVisualHint> CombatHints;

            void Execute(in DynamicBuffer<SurvivalDamageEvent> damageBuffer,
                         in LocalToWorld localToWorld, Entity entity)
            {
                float3 displayPos = localToWorld.Position + new float3(0f, 0.5f, 0f);

                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    var evt = damageBuffer[i];
                    if (evt.Amount <= 0f) continue;

                    bool isDOT = (evt.SourceEntity == Entity.Null);
                    var hint = CombatVisualHint.Default;
                    if (!isDOT && CombatHints.TryGetValue(entity.Index, out var h))
                    {
                        hint = h;
                        CombatHints.Remove(entity.Index);
                    }

                    VisualQueue.Enqueue(new DamageVisualData
                    {
                        Damage = evt.Amount,
                        HitPosition = displayPos,
                        HitType = hint.HitType,
                        DamageType = DamageTypeConverter.ToTheme(evt.Type),
                        Flags = hint.Flags,
                        IsDOT = isDOT,
                        SourceNetworkId = -1 // DamageEvent pipeline — no player attribution
                    });
                }
            }
        }

        [BurstCompile]
        partial struct VisualBridgeJobBatched : IJobEntity
        {
            public NativeList<DamageVisualData> VisualBatch;
            public NativeHashMap<int, CombatVisualHint> CombatHints;

            void Execute(in DynamicBuffer<SurvivalDamageEvent> damageBuffer,
                         in LocalToWorld localToWorld, Entity entity)
            {
                float3 displayPos = localToWorld.Position + new float3(0f, 0.5f, 0f);

                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    var evt = damageBuffer[i];
                    if (evt.Amount <= 0f) continue;

                    bool isDOT = (evt.SourceEntity == Entity.Null);
                    var hint = CombatVisualHint.Default;
                    if (!isDOT && CombatHints.TryGetValue(entity.Index, out var h))
                    {
                        hint = h;
                        CombatHints.Remove(entity.Index);
                    }

                    VisualBatch.Add(new DamageVisualData
                    {
                        Damage = evt.Amount,
                        HitPosition = displayPos,
                        HitType = hint.HitType,
                        DamageType = DamageTypeConverter.ToTheme(evt.Type),
                        Flags = hint.Flags,
                        IsDOT = isDOT,
                        SourceNetworkId = -1 // DamageEvent pipeline — no player attribution
                    });
                }
            }
        }
    }
}
