using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Handles crossfading between music tracks.
    /// When TargetTrackId differs from CurrentTrackId, drives CrossfadeProgress from 0 to 1.
    /// Burst-eligible: telemetry writes moved to MusicPlaybackSystem (managed).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(MusicStemMixSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct MusicTransitionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MusicState>();
            state.RequireForUpdate<MusicConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var musicState = SystemAPI.GetSingleton<MusicState>();
            var config = SystemAPI.GetSingleton<MusicConfig>();
            float dt = SystemAPI.Time.DeltaTime;

            // Initiate crossfade if target differs from current
            if (musicState.TargetTrackId != musicState.CurrentTrackId && musicState.CrossfadeDirection == 0)
            {
                musicState.CrossfadeDirection = 1;
                musicState.CrossfadeProgress = 0f;
            }

            // Drive crossfade
            if (musicState.CrossfadeDirection == 1)
            {
                float fadeSpeed = musicState.BossOverrideTrackId != 0
                    ? config.BossOverrideFadeSpeed
                    : musicState.ZoneFadeInDuration;

                if (fadeSpeed <= 0f) fadeSpeed = config.ZoneFadeSpeed;

                musicState.CrossfadeProgress = math.min(1f, musicState.CrossfadeProgress + dt / fadeSpeed);

                if (musicState.CrossfadeProgress >= 1f)
                {
                    musicState.CurrentTrackId = musicState.TargetTrackId;
                    musicState.CrossfadeDirection = 0;
                    musicState.CrossfadeProgress = 0f;
                }
            }

            SystemAPI.SetSingleton(musicState);
        }
    }
}
