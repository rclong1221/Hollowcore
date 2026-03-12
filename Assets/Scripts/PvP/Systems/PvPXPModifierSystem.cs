using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Tags KillCredited entities with PvPKillMarker when the kill
    /// was a PvP kill. Runs before XPAwardSystem so it can check the marker
    /// and apply PvPKillXPMultiplier.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DIG.Progression.XPAwardSystem))]
    public partial class PvPXPModifierSystem : SystemBase
    {
        private EntityQuery _killQuery;

        protected override void OnCreate()
        {
            _killQuery = GetEntityQuery(
                ComponentType.ReadOnly<KillCredited>(),
                ComponentType.ReadWrite<PvPKillMarker>(),
                ComponentType.ReadOnly<PlayerTag>());
            RequireForUpdate<PvPMatchState>();
        }

        protected override void OnUpdate()
        {
            var matchState = SystemAPI.GetSingleton<PvPMatchState>();
            if (matchState.Phase != PvPMatchPhase.Active && matchState.Phase != PvPMatchPhase.Overtime)
                return;

            var playerTagLookup = GetComponentLookup<PlayerTag>(true);
            var entities = _killQuery.ToEntityArray(Allocator.Temp);
            var kills = _killQuery.ToComponentDataArray<KillCredited>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                // Tag as PvP kill if victim has PlayerTag
                if (kills[i].Victim != Entity.Null && playerTagLookup.HasComponent(kills[i].Victim))
                {
                    EntityManager.SetComponentEnabled<PvPKillMarker>(entities[i], true);
                }
            }

            entities.Dispose();
            kills.Dispose();
        }
    }
}
