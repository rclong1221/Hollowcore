using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;
using DIG.Combat.UI;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Relay system that creates DamageVisualRpc entities OUTSIDE the prediction loop.
    ///
    /// DamageEventVisualBridgeSystem (PredictedFixedStep) queues pending RPCs to
    /// DamageVisualQueue.PendingServerRpcs. This system drains that queue and creates
    /// the actual RPC entities in SimulationSystemGroup, after prediction completes.
    ///
    /// This avoids NetCode prediction rollback interfering with RPC entity delivery.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedFixedStepSimulationSystemGroup))]
    public partial class DamageVisualRpcSendSystem : SystemBase
    {
        private DamageVisibilityServerFilter _visFilter;

        protected override void OnCreate()
        {
            DamageVisualQueue.InitializePendingRpcs();
            _visFilter = World.GetExistingSystemManaged<DamageVisibilityServerFilter>();
        }

        protected override void OnDestroy()
        {
            DamageVisualQueue.DisposePendingRpcs();
        }

        protected override void OnUpdate()
        {
            var pending = DamageVisualQueue.PendingServerRpcs;
            if (!pending.IsCreated || pending.Length == 0) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < pending.Length; i++)
            {
                var rpc = pending[i];
                _visFilter.CreateFilteredRpcs(ecb, rpc, rpc.SourceNetworkId, rpc.HitPosition);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            DamageVisualQueue.ClearPendingRpcs();
        }
    }
}
