using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Computes POI angles and distances relative to player forward direction.
    /// Writes to CompassBuffer for MapUIBridgeSystem to dispatch to CompassView.
    /// Only includes discovered POIs within CompassRange.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(FogOfWarSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class CompassSystem : SystemBase
    {
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

            // Find local player position + yaw
            float3 playerPos = float3.zero;
            float playerYawRadians = 0f;
            bool foundPlayer = false;

            foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<GhostOwnerIsLocal>())
            {
                playerPos = ltw.ValueRO.Position;
                float3 fwd = ltw.ValueRO.Forward;
                playerYawRadians = math.atan2(fwd.x, fwd.z);
                foundPlayer = true;
                break;
            }
            if (!foundPlayer) return;

            float compassRangeSq = config.CompassRange * config.CompassRange;
            managed.CompassBuffer.Clear();

            // Add discovered POIs to compass
            foreach (var (poi, ltw) in SystemAPI.Query<RefRO<PointOfInterest>, RefRO<LocalToWorld>>())
            {
                if (!poi.ValueRO.DiscoveredByPlayer) continue;

                float2 delta = ltw.ValueRO.Position.xz - playerPos.xz;
                float distSq = math.lengthsq(delta);
                if (distSq > compassRangeSq) continue;

                float dist = math.sqrt(distSq);
                float angle = math.atan2(delta.x, delta.y) - playerYawRadians;

                managed.CompassBuffer.Add(new CompassEntry
                {
                    Angle = angle,
                    Distance = dist,
                    IconType = poi.ValueRO.Type == POIType.FastTravel ? MapIconType.FastTravel : MapIconType.POI,
                    Label = poi.ValueRO.Label,
                    IsQuestWaypoint = false
                });
            }
        }
    }
}
