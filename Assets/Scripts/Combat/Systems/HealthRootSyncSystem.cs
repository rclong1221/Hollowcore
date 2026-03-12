using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Player.Components;
using DIG.Combat.Components;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Syncs CHILD.Health → ROOT.Health each frame so ghost replication propagates
    /// actual health values to remote clients.
    ///
    /// Problem: DamageableFixupSystem copies ROOT.Health → CHILD.Health at spawn.
    /// All damage systems (SimpleDamageApplySystem, DamageApplicationSystem) then modify
    /// CHILD.Health. But ROOT is the ghost entity with [GhostField] on Health — its value
    /// never changes, so remote clients always see full health.
    ///
    /// Fix: After all damage is applied, copy CHILD.Health back to ROOT.Health.
    /// Ghost replication then sends the updated values to all clients.
    ///
    /// Runs OrderLast in SimulationSystemGroup to capture changes from both
    /// DamageSystemGroup (SimpleDamageApplySystem) and DamageApplicationSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct HealthRootSyncSystem : ISystem
    {
        private ComponentLookup<Health> _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _healthLookup = state.GetComponentLookup<Health>(false);
            state.RequireForUpdate<HasHitboxes>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _healthLookup.Update(ref state);

            foreach (var (health, link) in
                SystemAPI.Query<RefRO<Health>, RefRO<DamageableLink>>()
                .WithAll<HasHitboxes>())
            {
                var root = link.ValueRO.DamageableRoot;
                if (root == Entity.Null) continue;
                if (!_healthLookup.HasComponent(root)) continue;

                var rootHealth = _healthLookup[root];
                if (rootHealth.Current != health.ValueRO.Current ||
                    rootHealth.Max != health.ValueRO.Max)
                {
                    _healthLookup[root] = new Health
                    {
                        Current = health.ValueRO.Current,
                        Max = health.ValueRO.Max
                    };
                }
            }
        }
    }
}
