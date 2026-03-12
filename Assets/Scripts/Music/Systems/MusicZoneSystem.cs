using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using Unity.Physics;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Detects local player overlap with MusicZone trigger volumes.
    /// Sets MusicState.TargetTrackId to the highest-priority overlapping zone.
    /// Falls back to MusicConfig.DefaultTrackId when no zone overlaps.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MusicZoneSystem : SystemBase
    {
        private EntityQuery _zoneQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<MusicState>();
            RequireForUpdate<MusicConfig>();
            _zoneQuery = GetEntityQuery(
                ComponentType.ReadOnly<MusicZone>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<PhysicsCollider>()
            );
        }

        protected override void OnUpdate()
        {
            // Find local player position
            float3 playerPos = float3.zero;
            bool foundPlayer = false;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<GhostOwnerIsLocal>())
            {
                playerPos = transform.ValueRO.Position;
                foundPlayer = true;
                break;
            }
            if (!foundPlayer) return;

            var musicState = SystemAPI.GetSingleton<MusicState>();
            var config = SystemAPI.GetSingleton<MusicConfig>();

            // Boss override takes priority over zone music
            if (musicState.BossOverrideTrackId != 0) return;

            int bestTrackId = 0;
            int bestPriority = int.MinValue;
            float bestFadeIn = 0f;
            float bestFadeOut = 0f;

            // Batched reads — avoids per-entity random access via EntityManager
            var zones = _zoneQuery.ToComponentDataArray<MusicZone>(Unity.Collections.Allocator.Temp);
            var zoneTransforms = _zoneQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
            var colliders = _zoneQuery.ToComponentDataArray<PhysicsCollider>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < zones.Length; i++)
            {
                if (!colliders[i].IsValid) continue;

                // Point-in-AABB test against zone collider bounds
                var aabb = colliders[i].Value.Value.CalculateAabb(new RigidTransform(zoneTransforms[i].Rotation, zoneTransforms[i].Position));
                if (playerPos.x >= aabb.Min.x && playerPos.x <= aabb.Max.x &&
                    playerPos.y >= aabb.Min.y && playerPos.y <= aabb.Max.y &&
                    playerPos.z >= aabb.Min.z && playerPos.z <= aabb.Max.z)
                {
                    if (zones[i].Priority > bestPriority)
                    {
                        bestPriority = zones[i].Priority;
                        bestTrackId = zones[i].TrackId;
                        bestFadeIn = zones[i].FadeInDuration;
                        bestFadeOut = zones[i].FadeOutDuration;
                    }
                }
            }

            zones.Dispose();
            zoneTransforms.Dispose();
            colliders.Dispose();

            // Determine target track
            int targetTrack;
            float fadeIn;
            float fadeOut;

            if (bestTrackId != 0)
            {
                targetTrack = bestTrackId;
                fadeIn = bestFadeIn > 0f ? bestFadeIn : config.ZoneFadeSpeed;
                fadeOut = bestFadeOut > 0f ? bestFadeOut : config.ZoneFadeSpeed;
            }
            else
            {
                // No zone overlap — use default
                targetTrack = config.DefaultTrackId;
                fadeIn = config.ZoneFadeSpeed;
                fadeOut = config.ZoneFadeSpeed;
            }

            if (targetTrack != musicState.TargetTrackId || bestPriority != musicState.CurrentZonePriority)
            {
                musicState.TargetTrackId = targetTrack;
                musicState.CurrentZonePriority = bestTrackId != 0 ? bestPriority : 0;
                musicState.ZoneFadeInDuration = fadeIn;
                musicState.ZoneFadeOutDuration = fadeOut;
                SystemAPI.SetSingleton(musicState);
            }
        }
    }
}
