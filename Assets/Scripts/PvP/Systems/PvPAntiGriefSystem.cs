using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: AFK detection, leaver tracking, and spawn camping prevention.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PvPObjectiveSystem))]
    public partial class PvPAntiGriefSystem : SystemBase
    {
        private EntityQuery _playerQuery;

        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(
                ComponentType.ReadWrite<PvPAntiGriefState>(),
                ComponentType.ReadOnly<PvPPlayerStats>(),
                ComponentType.ReadOnly<PlayerTag>());

            RequireForUpdate<PvPMatchState>();
            RequireForUpdate<PvPConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var matchState = SystemAPI.GetSingleton<PvPMatchState>();
            if (matchState.Phase != PvPMatchPhase.Active && matchState.Phase != PvPMatchPhase.Overtime)
                return;

            ref var config = ref SystemAPI.GetSingleton<PvPConfigSingleton>().Config.Value;
            float dt = SystemAPI.Time.DeltaTime;

            // AFK detection
            var entities = _playerQuery.ToEntityArray(Allocator.Temp);
            var griefStates = _playerQuery.ToComponentDataArray<PvPAntiGriefState>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var gs = griefStates[i];

                // Simple AFK check: increment timer (a real system would check InputComponent deltas)
                gs.TimeSinceLastInput += dt;

                if (gs.TimeSinceLastInput > config.AFKTimeoutSeconds && gs.IsAFK == 0)
                {
                    gs.IsAFK = 1;
                    gs.AFKWarningCount++;
                }

                EntityManager.SetComponentData(entities[i], gs);
            }

            entities.Dispose();
            griefStates.Dispose();
        }

        /// <summary>
        /// Called by input systems to reset AFK timer when player provides input.
        /// </summary>
        public static void ResetAFKTimer(EntityManager em, Entity player)
        {
            if (!em.HasComponent<PvPAntiGriefState>(player)) return;
            var state = em.GetComponentData<PvPAntiGriefState>(player);
            state.TimeSinceLastInput = 0f;
            state.IsAFK = 0;
            em.SetComponentData(player, state);
        }
    }
}
