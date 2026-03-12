using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Adjusts per-stem volumes based on combat intensity thresholds.
    /// Base stem always 1.0; Percussion/Melody/Intensity activate at configurable thresholds.
    /// During crossfade, volumes are modulated by CrossfadeProgress.
    /// Burst-eligible: reads thresholds from MusicState.CurrentTrackThresholds (written by MusicPlaybackSystem).
    /// Telemetry is handled by MusicPlaybackSystem (managed) to keep this system pure.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MusicTransitionSystem))]
    [UpdateBefore(typeof(MusicStingerSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct MusicStemMixSystem : ISystem
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

            // Read cached thresholds from MusicState (written by MusicPlaybackSystem on track change)
            float3 thresholds = musicState.CurrentTrackThresholds;
            // Guard against zero thresholds (before first track assigned)
            if (thresholds.x <= 0f && thresholds.y <= 0f && thresholds.z <= 0f)
                thresholds = new float3(0.2f, 0.5f, 0.8f);

            float intensity = musicState.SmoothedIntensity;

            // Compute target stem volumes
            float targetBase = 1f;
            float targetPerc = intensity >= thresholds.x ? 1f : 0f;
            float targetMelody = intensity >= thresholds.y ? 1f : 0f;
            float targetIntensity = intensity >= thresholds.z ? 1f : 0f;

            // Smooth each stem
            float speed = config.StemTransitionSpeed * dt;
            float4 stemVolumes = musicState.StemVolumes;
            stemVolumes.x = math.lerp(stemVolumes.x, targetBase, speed);
            stemVolumes.y = math.lerp(stemVolumes.y, targetPerc, speed);
            stemVolumes.z = math.lerp(stemVolumes.z, targetMelody, speed);
            stemVolumes.w = math.lerp(stemVolumes.w, targetIntensity, speed);

            // During crossfade: modulate volumes
            if (musicState.CrossfadeDirection == 1)
            {
                float fade = musicState.CrossfadeProgress;
                stemVolumes *= fade;
            }

            musicState.StemVolumes = stemVolumes;
            SystemAPI.SetSingleton(musicState);
        }
    }
}
