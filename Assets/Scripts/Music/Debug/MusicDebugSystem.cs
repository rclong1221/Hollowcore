using Unity.Entities;
using UnityEngine;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Optional debug overlay showing music state in development builds.
    /// Displays current track, intensity, active stems, zone, and transition state.
    /// Only active when DEBUG_LOG_AUDIO is defined or in editor.
    /// Uses a companion MonoBehaviour for OnGUI rendering (SystemBase has no OnGUI lifecycle).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MusicUIBridgeSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MusicDebugSystem : SystemBase
    {
        private MusicDebugOverlay _overlay;

        protected override void OnCreate()
        {
            RequireForUpdate<MusicState>();
            #if !UNITY_EDITOR && !DEBUG_LOG_AUDIO
            Enabled = false;
            #endif
        }

        protected override void OnUpdate()
        {
            // Toggle with F8 key
            if (Input.GetKeyDown(KeyCode.F8))
            {
                EnsureOverlay();
                _overlay.ShowOverlay = !_overlay.ShowOverlay;
            }

            // Update overlay data each frame when visible
            if (_overlay != null && _overlay.ShowOverlay)
            {
                var musicState = SystemAPI.GetSingleton<MusicState>();

                string trackName = "Unknown";
                if (SystemAPI.ManagedAPI.HasSingleton<MusicDatabaseManaged>())
                {
                    var dbManaged = SystemAPI.ManagedAPI.GetSingleton<MusicDatabaseManaged>();
                    var track = dbManaged.Database?.GetTrack(musicState.CurrentTrackId);
                    if (track != null) trackName = track.TrackName;
                }

                _overlay.TrackName = trackName;
                _overlay.CurrentTrackId = musicState.CurrentTrackId;
                _overlay.TargetTrackId = musicState.TargetTrackId;
                _overlay.SmoothedIntensity = musicState.SmoothedIntensity;
                _overlay.IsInCombat = musicState.IsInCombat;
                _overlay.StemX = musicState.StemVolumes.x;
                _overlay.StemY = musicState.StemVolumes.y;
                _overlay.StemZ = musicState.StemVolumes.z;
                _overlay.StemW = musicState.StemVolumes.w;
                _overlay.CrossfadeProgress = musicState.CrossfadeProgress;
                _overlay.CrossfadeDirection = musicState.CrossfadeDirection;
                _overlay.BossOverrideTrackId = musicState.BossOverrideTrackId;
                _overlay.StingerCooldown = musicState.StingerCooldown;
            }
        }

        private void EnsureOverlay()
        {
            if (_overlay != null) return;
            var go = new GameObject("MusicDebugOverlay");
            Object.DontDestroyOnLoad(go);
            _overlay = go.AddComponent<MusicDebugOverlay>();
        }

        protected override void OnDestroy()
        {
            if (_overlay != null)
                Object.Destroy(_overlay.gameObject);
        }
    }

    /// <summary>
    /// Companion MonoBehaviour providing OnGUI rendering for music debug overlay.
    /// Data is written by MusicDebugSystem each frame.
    /// </summary>
    internal class MusicDebugOverlay : MonoBehaviour
    {
        public bool ShowOverlay;
        public string TrackName;
        public int CurrentTrackId;
        public int TargetTrackId;
        public float SmoothedIntensity;
        public bool IsInCombat;
        public float StemX, StemY, StemZ, StemW;
        public float CrossfadeProgress;
        public byte CrossfadeDirection;
        public int BossOverrideTrackId;
        public float StingerCooldown;

        private void OnGUI()
        {
            if (!ShowOverlay) return;

            float x = 10f, y = 10f, w = 300f, h = 20f;

            GUI.Box(new Rect(x - 5, y - 5, w + 10, 200), "");
            GUI.Label(new Rect(x, y, w, h), $"<b>Music Debug (F8)</b>"); y += h;
            GUI.Label(new Rect(x, y, w, h), $"Track: {TrackName} (ID:{CurrentTrackId})"); y += h;
            GUI.Label(new Rect(x, y, w, h), $"Target: {TargetTrackId}"); y += h;
            GUI.Label(new Rect(x, y, w, h), $"Combat Intensity: {SmoothedIntensity:F2}"); y += h;
            GUI.Label(new Rect(x, y, w, h), $"In Combat: {IsInCombat}"); y += h;
            GUI.Label(new Rect(x, y, w, h), $"Stems: B={StemX:F2} P={StemY:F2} M={StemZ:F2} I={StemW:F2}"); y += h;
            GUI.Label(new Rect(x, y, w, h), $"Crossfade: {CrossfadeProgress:F2} Dir={CrossfadeDirection}"); y += h;
            GUI.Label(new Rect(x, y, w, h), $"Boss Override: {BossOverrideTrackId}"); y += h;
            GUI.Label(new Rect(x, y, w, h), $"Stinger CD: {StingerCooldown:F1}s"); y += h;
        }
    }
}
