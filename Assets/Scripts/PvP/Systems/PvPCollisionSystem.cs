using Unity.Entities;
using Unity.NetCode;
using Player.Components;
using DIG.Player.Components;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Toggles CollisionGameSettings singleton for PvP mode.
    /// Enables player-vs-player damage, sets IsPvPMode on IndicatorThemeContext.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PvPMatchSystem))]
    public partial class PvPCollisionSystem : SystemBase
    {
        private EntityQuery _matchStateQuery;
        private PvPMatchPhase _lastKnownPhase;
        private bool _pvpActive;

        protected override void OnCreate()
        {
            _matchStateQuery = GetEntityQuery(ComponentType.ReadOnly<PvPMatchState>());
            RequireForUpdate<PvPConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            bool matchExists = _matchStateQuery.CalculateEntityCount() > 0;

            if (matchExists)
            {
                var state = SystemAPI.GetSingleton<PvPMatchState>();

                if (state.Phase >= PvPMatchPhase.Warmup && state.Phase <= PvPMatchPhase.Overtime && !_pvpActive)
                {
                    EnablePvPCollision(state.GameMode);
                    _pvpActive = true;
                }
                else if ((state.Phase == PvPMatchPhase.Results || state.Phase == PvPMatchPhase.Ended) && _pvpActive)
                {
                    DisablePvPCollision();
                    _pvpActive = false;
                }

                _lastKnownPhase = state.Phase;
            }
            else if (_pvpActive)
            {
                DisablePvPCollision();
                _pvpActive = false;
            }
        }

        private void EnablePvPCollision(PvPGameMode gameMode)
        {
            if (!SystemAPI.HasSingleton<CollisionGameSettings>()) return;

            var settings = SystemAPI.GetSingletonRW<CollisionGameSettings>();
            settings.ValueRW.FriendlyFireEnabled = true;
            settings.ValueRW.TeamCollisionEnabled = gameMode == PvPGameMode.FreeForAll;
        }

        private void DisablePvPCollision()
        {
            if (!SystemAPI.HasSingleton<CollisionGameSettings>()) return;

            var settings = SystemAPI.GetSingletonRW<CollisionGameSettings>();
            settings.ValueRW.FriendlyFireEnabled = false;
            settings.ValueRW.TeamCollisionEnabled = false;
        }
    }
}
