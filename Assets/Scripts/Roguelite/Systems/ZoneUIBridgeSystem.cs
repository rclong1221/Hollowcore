using DIG.Roguelite;
using Unity.Entities;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Reads ZoneState and pushes to ZoneUIRegistry each frame.
    /// Client/Local only — presentation layer.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ZoneUIBridgeSystem : SystemBase
    {
        private int _lastZoneIndex = -1;
        private bool _lastCleared;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            RequireForUpdate<ZoneState>();
        }

        protected override void OnUpdate()
        {
            if (!ZoneUIRegistry.HasProvider) return;

            var zoneState = SystemAPI.GetSingleton<ZoneState>();
            var provider = ZoneUIRegistry.Provider;

            // Detect zone change
            if (zoneState.ZoneIndex != _lastZoneIndex)
            {
                var sequencer = World.GetExistingSystemManaged<ZoneSequenceResolverSystem>();
                var zoneDef = sequencer?.GetZoneAtIndex(zoneState.ZoneIndex);
                string zoneName = zoneDef != null ? zoneDef.DisplayName : $"Zone {zoneState.ZoneIndex}";

                provider.OnZoneActivated(zoneState.ZoneIndex, zoneName, zoneState.Type, zoneState.ClearMode);
                _lastZoneIndex = zoneState.ZoneIndex;
                _lastCleared = false;
            }

            // Detect clear
            if (zoneState.IsCleared && !_lastCleared)
            {
                provider.OnZoneCleared(zoneState.ZoneIndex, zoneState.TimeInZone, zoneState.EnemiesKilled);
                _lastCleared = true;
            }

            // Continuous HUD update
            provider.UpdateZoneHUD(
                zoneState.TimeInZone,
                zoneState.EnemiesAlive,
                zoneState.EnemiesKilled,
                zoneState.EnemiesSpawned,
                zoneState.SpawnBudget,
                zoneState.ExitActivated,
                zoneState.IsCleared);
        }
    }
}
