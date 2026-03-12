using Unity.Entities;
using Unity.Collections;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Server-side cleanup for CombatResultEvent and DeathEvent entities.
    ///
    /// CombatEventCleanupSystem (PresentationSystemGroup) handles client-side cleanup,
    /// but PresentationSystemGroup doesn't exist on the ServerWorld.
    /// This system prevents CRE/DeathEvent entities from accumulating indefinitely on the server.
    ///
    /// Runs after DamageApplicationSystem has processed all CREs.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    public partial class ServerCombatCleanupSystem : SystemBase
    {
        private EntityQuery _resultsQuery;
        private EntityQuery _deathsQuery;

        protected override void OnCreate()
        {
            _resultsQuery = GetEntityQuery(ComponentType.ReadOnly<CombatResultEvent>());
            _deathsQuery = GetEntityQuery(ComponentType.ReadOnly<DeathEvent>());
        }

        protected override void OnUpdate()
        {
            if (_resultsQuery.IsEmpty && _deathsQuery.IsEmpty)
                return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<CombatResultEvent>>().WithEntityAccess())
                ecb.DestroyEntity(entity);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<DeathEvent>>().WithEntityAccess())
                ecb.DestroyEntity(entity);

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
