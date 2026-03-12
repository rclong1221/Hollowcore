using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Pushes map icon buffer, compass entries, fog stats, and player marker
    /// data to MapUIRegistry providers. Managed SystemBase (touches MonoBehaviour UI).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CompassSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MapUIBridgeSystem : SystemBase
    {
        private bool _rtAssigned;

        protected override void OnCreate()
        {
            RequireForUpdate<MinimapConfig>();
            RequireForUpdate<MapManagedState>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<MinimapConfig>();
            var managed = SystemAPI.ManagedAPI.GetSingleton<MapManagedState>();
            if (!managed.IsInitialized) return;

            // Push RenderTextures to UI providers on first frame (or after reconnect)
            if (!_rtAssigned)
            {
                if (MapUIRegistry.HasMinimap && managed.MinimapRenderTexture != null)
                {
                    MapUIRegistry.SetMinimapRenderTextures(
                        managed.MinimapRenderTexture, managed.FogOfWarTexture);
                    _rtAssigned = true;
                }
                if (MapUIRegistry.HasWorldMap && managed.FogOfWarTexture != null)
                {
                    MapUIRegistry.SetWorldMapFogTexture(managed.FogOfWarTexture);
                }
            }

            // Find local player for marker
            float3 playerPos = float3.zero;
            float playerYaw = 0f;
            bool foundPlayer = false;

            foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<GhostOwnerIsLocal>())
            {
                playerPos = ltw.ValueRO.Position;
                float3 fwd = ltw.ValueRO.Forward;
                playerYaw = math.atan2(fwd.x, fwd.z);
                foundPlayer = true;
                break;
            }

            if (!foundPlayer) return;

            // Push to UI providers
            MapUIRegistry.UpdateMinimapIcons(managed.IconBuffer, playerPos, playerYaw, config.Zoom);
            MapUIRegistry.UpdateCompass(managed.CompassBuffer);

            // Fog stats
            if (SystemAPI.HasSingleton<MapRevealState>())
            {
                var reveal = SystemAPI.GetSingleton<MapRevealState>();
                MapUIRegistry.UpdateFogStats(reveal.TotalRevealed, reveal.TotalPixels);
            }

            MapUIRegistry.UpdatePlayerMarker(playerPos, playerYaw);
        }
    }
}
