using Unity.Entities;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Reads MusicState each frame and pushes changes to MusicUIRegistry providers.
    /// Follows CombatUIBridgeSystem pattern.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MusicPlaybackSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MusicUIBridgeSystem : SystemBase
    {
        private int _lastReportedTrackId;
        private float _lastReportedIntensity;

        protected override void OnCreate()
        {
            RequireForUpdate<MusicState>();
            RequireForUpdate<MusicDatabaseManaged>();
        }

        protected override void OnUpdate()
        {
            if (!MusicUIRegistry.HasProvider) return;

            var musicState = SystemAPI.GetSingleton<MusicState>();
            var dbManaged = SystemAPI.ManagedAPI.GetSingleton<MusicDatabaseManaged>();

            // Track change notification
            if (musicState.CurrentTrackId != _lastReportedTrackId)
            {
                _lastReportedTrackId = musicState.CurrentTrackId;

                string trackName = "Unknown";
                var category = MusicTrackCategory.Exploration;

                if (dbManaged.Database != null)
                {
                    var track = dbManaged.Database.GetTrack(musicState.CurrentTrackId);
                    if (track != null)
                    {
                        trackName = track.TrackName;
                        category = track.Category;
                    }
                }

                MusicUIRegistry.Provider.OnTrackChanged(trackName, category);
            }

            // Intensity change notification (throttled to avoid spam)
            float intensityDelta = musicState.SmoothedIntensity - _lastReportedIntensity;
            if (intensityDelta > 0.05f || intensityDelta < -0.05f)
            {
                _lastReportedIntensity = musicState.SmoothedIntensity;
                MusicUIRegistry.Provider.OnCombatIntensityChanged(musicState.SmoothedIntensity);
            }
        }
    }
}
