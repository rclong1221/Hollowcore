using Unity.Collections;
using Unity.Entities;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Destroys trade session entities in terminal state
    /// (Completed/Failed/Cancelled). 1-frame delay allows audit and UI to read final state.
    /// Uses a "marked for cleanup" approach: first frame marks, second frame destroys.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TradeAuditSystem))]
    public partial class TradeCleanupSystem : SystemBase
    {
        /// <summary>Marker component for 1-frame delay before destruction.</summary>
        private struct TradeCleanupMark : IComponentData { }

        protected override void OnCreate()
        {
            RequireForUpdate<TradeConfig>();
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 1. Destroy entities marked last frame
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<TradeCleanupMark>>()
                         .WithAll<TradeSessionTag>()
                         .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            // 2. Mark terminal sessions for cleanup next frame
            foreach (var (state, sessionEntity) in
                     SystemAPI.Query<RefRO<TradeSessionState>>()
                         .WithAll<TradeSessionTag>()
                         .WithNone<TradeCleanupMark>()
                         .WithEntityAccess())
            {
                if (state.ValueRO.State == TradeState.Completed ||
                    state.ValueRO.State == TradeState.Failed ||
                    state.ValueRO.State == TradeState.Cancelled)
                {
                    ecb.AddComponent<TradeCleanupMark>(sessionEntity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
