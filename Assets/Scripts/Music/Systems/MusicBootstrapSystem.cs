using Unity.Entities;
using UnityEngine;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Loads MusicConfigSO and MusicDatabaseSO from Resources,
    /// creates MusicConfig and MusicState singletons, and self-disables.
    /// Runs once on client startup.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MusicBootstrapSystem : SystemBase
    {
        protected override void OnCreate()
        {
            // Allow first update to run unconditionally
        }

        protected override void OnUpdate()
        {
            // Load config
            var configSO = Resources.Load<MusicConfigSO>("MusicConfig");
            var databaseSO = Resources.Load<MusicDatabaseSO>("MusicDatabase");

            if (configSO == null)
            {
                Debug.LogWarning("[MusicBootstrap] MusicConfigSO not found at Resources/MusicConfig. Music system disabled.");
                Enabled = false;
                return;
            }

            if (databaseSO == null)
            {
                Debug.LogWarning("[MusicBootstrap] MusicDatabaseSO not found at Resources/MusicDatabase. Music system disabled.");
                Enabled = false;
                return;
            }

            // Create MusicConfig singleton
            var configEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(configEntity, new MusicConfig
            {
                DefaultTrackId = configSO.DefaultTrackId,
                CombatFadeSpeed = configSO.CombatFadeSpeed,
                ZoneFadeSpeed = configSO.ZoneFadeSpeed,
                StingerVolume = configSO.StingerVolume,
                StingerCooldown = configSO.StingerCooldown,
                MaxCombatIntensityRange = configSO.MaxCombatIntensityRange,
                StemTransitionSpeed = configSO.StemTransitionSpeed,
                BossOverrideFadeSpeed = configSO.BossOverrideFadeSpeed,
                IntensityWeightCombat = configSO.IntensityWeightCombat,
                IntensityWeightSearching = configSO.IntensityWeightSearching,
                IntensityWeightSuspicious = configSO.IntensityWeightSuspicious,
                IntensityWeightCurious = configSO.IntensityWeightCurious,
                MaxIntensityContributors = configSO.MaxIntensityContributors
            });

            // Create MusicState singleton
            int defaultTrack = configSO.DefaultTrackId;
            var stateEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(stateEntity, new MusicState
            {
                CurrentTrackId = defaultTrack,
                TargetTrackId = defaultTrack,
                CrossfadeProgress = 0f,
                CrossfadeDirection = 0,
                CombatIntensity = 0f,
                SmoothedIntensity = 0f,
                BossOverrideTrackId = 0,
                IsInCombat = false,
                StemVolumes = new Unity.Mathematics.float4(1f, 0f, 0f, 0f),
                CurrentZonePriority = 0,
                ZoneFadeInDuration = configSO.ZoneFadeSpeed,
                ZoneFadeOutDuration = configSO.ZoneFadeSpeed,
                StingerCooldown = 0f,
                CurrentTrackThresholds = new Unity.Mathematics.float3(0.2f, 0.5f, 0.8f)
            });

            // Create managed singleton holding SO references for MusicPlaybackSystem
            var dbEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(dbEntity, new MusicDatabaseManaged { Database = databaseSO });

            Debug.Log($"[MusicBootstrap] Music system initialized. Default track: {defaultTrack}, {databaseSO.Tracks.Count} tracks, {databaseSO.Stingers.Count} stingers.");

            // Self-disable
            Enabled = false;
        }
    }

    /// <summary>
    /// Managed component holding reference to MusicDatabaseSO for track/stinger lookups.
    /// </summary>
    public class MusicDatabaseManaged : IComponentData
    {
        public MusicDatabaseSO Database;
    }
}
