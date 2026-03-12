using Unity.Entities;
using Unity.Collections;
using DIG.Combat.UI;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Cleans up combat events at end of frame to prevent accumulation.
    /// Runs in PresentationSystemGroup AFTER CombatUIBridgeSystem so the bridge
    /// can read events before they are destroyed.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CombatUIBridgeSystem))]
    public partial class CombatEventCleanupSystem : SystemBase
    {
        private EntityQuery _staleResultsQuery;
        private EntityQuery _staleDeathsQuery;

        protected override void OnCreate()
        {
            _staleResultsQuery = GetEntityQuery(
                ComponentType.ReadOnly<CombatResultEvent>()
            );
            _staleDeathsQuery = GetEntityQuery(
                ComponentType.ReadOnly<DeathEvent>()
            );
        }

        protected override void OnUpdate()
        {
            if (_staleResultsQuery.IsEmpty && _staleDeathsQuery.IsEmpty)
                return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<CombatResultEvent>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<DeathEvent>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
