using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;
using DIG.Combat.UI;
using DIG.Targeting.Theming;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Receives DamageVisualRpc from the server and enqueues full-fidelity damage visuals
    /// to DamageVisualQueue for CombatUIBridgeSystem to display.
    ///
    /// Runs on ClientSimulation only. On listen servers, RPCs are still consumed and destroyed
    /// (to prevent RPC age warnings) but visuals are NOT enqueued — DamageEventVisualBridgeSystem
    /// already populates DamageVisualQueue directly via the shared static queue from ServerWorld.
    ///
    /// Uses manual EntityQuery (not SystemAPI.Query) because source-gen query matching
    /// is unreliable for transient RPC entities on remote clients.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CombatUIBridgeSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class DamageVisualRpcReceiveSystem : SystemBase
    {
        private EntityQuery _rpcQuery;
        private bool _checkedForServerWorld;
        private bool _isListenServer;

        protected override void OnCreate()
        {
            _rpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<DamageVisualRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

            // Initialize DamageVisualQueue on remote clients where
            // DamageEventVisualBridgeSystem (Server|Local only) never runs.
            // Idempotent — safe no-op on listen servers.
            DamageVisualQueue.Initialize();
        }

        protected override void OnUpdate()
        {
            // Detect listen server once
            if (!_checkedForServerWorld)
            {
                _checkedForServerWorld = true;
                foreach (var world in World.All)
                {
                    if (world.Name == "ServerWorld" && world.IsCreated)
                    {
                        _isListenServer = true;
                        break;
                    }
                }
            }

            int count = _rpcQuery.CalculateEntityCount();
            if (count == 0) return;

            // On remote clients: enqueue all RPCs to DamageVisualQueue for damage number display.
            // On listen servers: skip enqueue (ServerWorld already populated the shared
            // static queue) but still destroy entities to prevent RPC age warnings.
            if (!_isListenServer)
            {
                var rpcs = _rpcQuery.ToComponentDataArray<DamageVisualRpc>(Allocator.Temp);
                for (int i = 0; i < rpcs.Length; i++)
                {
                    var data = rpcs[i];
                    DamageVisualQueue.Enqueue(new DamageVisualData
                    {
                        Damage = data.Damage,
                        HitPosition = data.HitPosition,
                        HitType = (HitType)data.HitType,
                        DamageType = (DamageType)data.DamageType,
                        Flags = (ResultFlags)data.Flags,
                        IsDOT = data.IsDOT != 0,
                        SourceNetworkId = data.SourceNetworkId
                    });
                }
                rpcs.Dispose();
            }

            // Batch-destroy all RPC entities in one structural change
            EntityManager.DestroyEntity(_rpcQuery);
        }
    }
}
