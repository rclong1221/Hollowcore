using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Processes MusicStingerRequest transient entities.
    /// Selects highest-priority request, respects cooldown, dispatches to MusicPlaybackSystem.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MusicStemMixSystem))]
    [UpdateBefore(typeof(MusicPlaybackSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MusicStingerSystem : SystemBase
    {
        private EntityQuery _requestQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<MusicState>();
            RequireForUpdate<MusicConfig>();
            _requestQuery = GetEntityQuery(ComponentType.ReadOnly<MusicStingerRequest>());
        }

        protected override void OnUpdate()
        {
            if (_requestQuery.IsEmpty) return;

            var musicState = SystemAPI.GetSingleton<MusicState>();
            var config = SystemAPI.GetSingleton<MusicConfig>();

            var requests = _requestQuery.ToComponentDataArray<MusicStingerRequest>(Allocator.Temp);
            var entities = _requestQuery.ToEntityArray(Allocator.Temp);

            // Find highest priority request
            int bestIdx = -1;
            byte bestPriority = 0;
            bool bestAllowOverlap = false;

            for (int i = 0; i < requests.Length; i++)
            {
                if (requests[i].Priority > bestPriority || bestIdx == -1)
                {
                    bestIdx = i;
                    bestPriority = requests[i].Priority;
                    bestAllowOverlap = requests[i].AllowOverlap;
                }
            }

            // Dispatch best request if cooldown allows
            if (bestIdx >= 0)
            {
                bool canPlay = musicState.StingerCooldown <= 0f || bestAllowOverlap;

                if (canPlay)
                {
                    // Resolve stinger definition
                    var dbManaged = SystemAPI.ManagedAPI.GetSingleton<MusicDatabaseManaged>();
                    var stingerDef = dbManaged.Database?.GetStinger(requests[bestIdx].StingerId);

                    if (stingerDef != null && stingerDef.Clip != null)
                    {
                        // Dispatch to MusicPlaybackSystem via static pending stinger
                        MusicPlaybackSystem.PendingStinger = new MusicPlaybackSystem.StingerPlayback
                        {
                            Clip = stingerDef.Clip,
                            Volume = config.StingerVolume * requests[bestIdx].VolumeScale,
                            DuckDB = stingerDef.DuckMusicDB,
                            DuckDuration = stingerDef.DuckDuration > 0f ? stingerDef.DuckDuration : stingerDef.Clip.length
                        };

                        musicState.StingerCooldown = config.StingerCooldown;
                        Audio.Systems.AudioTelemetry.StingersPlayedThisSession++;
                    }
                }
            }

            // Destroy all request entities
            for (int i = 0; i < entities.Length; i++)
                EntityManager.DestroyEntity(entities[i]);

            requests.Dispose();
            entities.Dispose();

            SystemAPI.SetSingleton(musicState);
        }
    }
}
