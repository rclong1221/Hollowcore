using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Audio.Config;
using Audio.Systems;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Managed system that drives AudioSource playback for music stems and stingers.
    /// The only system that touches Unity's AudioSource API.
    /// Manages 4 stem sources (looping) + 1 stinger source (one-shot) on a persistent GameObject.
    /// Also handles all telemetry writes (consolidated from ISystem structs to keep them Burst-eligible).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MusicStingerSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MusicPlaybackSystem : SystemBase
    {
        // Static bridge for stinger dispatch from MusicStingerSystem
        public struct StingerPlayback
        {
            public AudioClip Clip;
            public float Volume;
            public float DuckDB;
            public float DuckDuration;
        }
        public static StingerPlayback? PendingStinger;

        private GameObject _musicPlayer;
        private AudioSource _baseSrc;
        private AudioSource _percSrc;
        private AudioSource _melodySrc;
        private AudioSource _intensitySrc;
        private AudioSource _stingerSrc;

        private int _lastTrackId;
        private bool _playingIntro;
        private float _stingerDuckTimer;
        private float _stingerDuckDB;
        private float _cachedDuckLinear = 1f; // Cached dB-to-linear conversion

        // Crossfade: second set of stem sources for outgoing track
        private AudioSource _xfadeBaseSrc;
        private AudioSource _xfadePercSrc;
        private AudioSource _xfadeMelodySrc;
        private AudioSource _xfadeIntensitySrc;
        private int _xfadeOutgoingTrackId;  // Track being faded OUT
        private int _crossfadeTargetTrackId; // Track being faded TO (prevents re-trigger)

        // Cached AudioMixerGroup for Music bus
        private UnityEngine.Audio.AudioMixerGroup _musicMixerGroup;
        private bool _mixerGroupResolved;

        protected override void OnCreate()
        {
            RequireForUpdate<MusicState>();
            RequireForUpdate<MusicDatabaseManaged>();
        }

        protected override void OnUpdate()
        {
            EnsureAudioSources();
            if (_musicPlayer == null) return;

            var musicState = SystemAPI.GetSingleton<MusicState>();
            var dbManaged = SystemAPI.ManagedAPI.GetSingleton<MusicDatabaseManaged>();
            if (dbManaged.Database == null) return;

            float dt = SystemAPI.Time.DeltaTime;

            // Handle track changes (crossfade completed — CurrentTrackId was updated by MusicTransitionSystem)
            if (musicState.CurrentTrackId != _lastTrackId && musicState.CrossfadeDirection == 0)
            {
                AssignTrack(musicState.CurrentTrackId, dbManaged.Database);
                UpdateTrackThresholds(ref musicState, dbManaged.Database, musicState.CurrentTrackId);

                // Telemetry: track transition completed (moved from MusicTransitionSystem for Burst)
                if (_lastTrackId != 0)
                    AudioTelemetry.TrackTransitionsThisSession++;
            }
            // Handle crossfade start — only trigger once per target change
            else if (musicState.CrossfadeDirection == 1 && musicState.TargetTrackId != _crossfadeTargetTrackId)
            {
                StartCrossfade(musicState.TargetTrackId, dbManaged.Database);
                UpdateTrackThresholds(ref musicState, dbManaged.Database, musicState.TargetTrackId);
            }

            // Update stem volumes
            var track = dbManaged.Database.GetTrack(musicState.CurrentTrackId);
            float baseVolume = track != null ? track.BaseVolume : 1f;

            // Apply stinger duck (cached dB-to-linear)
            float duckMultiplier = 1f;
            if (_stingerDuckTimer > 0f)
            {
                _stingerDuckTimer -= dt;
                if (_stingerDuckTimer <= 0f)
                {
                    _cachedDuckLinear = 1f;
                }
                duckMultiplier = _cachedDuckLinear;
            }

            // Main stems
            ApplyStemVolumes(musicState.StemVolumes, baseVolume * duckMultiplier);

            // Crossfade outgoing stems
            if (musicState.CrossfadeDirection == 1)
            {
                float outFade = 1f - musicState.CrossfadeProgress;
                var xfadeTrack = dbManaged.Database.GetTrack(_xfadeOutgoingTrackId);
                float xfBaseVol = xfadeTrack != null ? xfadeTrack.BaseVolume : 1f;
                float xfDuck = xfBaseVol * duckMultiplier * outFade;

                if (_xfadeBaseSrc != null) _xfadeBaseSrc.volume = xfDuck;
                if (_xfadePercSrc != null) _xfadePercSrc.volume = xfDuck * musicState.StemVolumes.y;
                if (_xfadeMelodySrc != null) _xfadeMelodySrc.volume = xfDuck * musicState.StemVolumes.z;
                if (_xfadeIntensitySrc != null) _xfadeIntensitySrc.volume = xfDuck * musicState.StemVolumes.w;
            }
            else if (_xfadeOutgoingTrackId != 0)
            {
                // Crossfade complete — stop outgoing
                StopCrossfadeSources();
                _xfadeOutgoingTrackId = 0;
                _crossfadeTargetTrackId = 0;
            }

            // Handle loop points
            if (track != null)
            {
                HandleLoopPoints(_baseSrc, track);
                HandleLoopPoints(_percSrc, track);
                HandleLoopPoints(_melodySrc, track);
                HandleLoopPoints(_intensitySrc, track);
            }

            // Handle intro→loop transition
            if (_playingIntro && track != null && _baseSrc != null && !_baseSrc.isPlaying)
            {
                _playingIntro = false;
                AssignStemClips(track);
                PlayAllStems();
            }

            // Handle stinger
            if (PendingStinger.HasValue)
            {
                var stinger = PendingStinger.Value;
                PendingStinger = null;

                if (_stingerSrc != null)
                {
                    _stingerSrc.clip = stinger.Clip;
                    _stingerSrc.volume = stinger.Volume;
                    _stingerSrc.loop = false;
                    _stingerSrc.Play();
                    _stingerDuckDB = stinger.DuckDB;
                    _stingerDuckTimer = stinger.DuckDuration;
                    _cachedDuckLinear = Mathf.Pow(10f, stinger.DuckDB / 20f);
                }
            }

            // Tick stinger cooldown on state
            if (musicState.StingerCooldown > 0f)
            {
                musicState.StingerCooldown -= dt;
                SystemAPI.SetSingleton(musicState);
            }

            // Telemetry (consolidated here — all managed, no Burst impact)
            AudioTelemetry.CurrentTrackId = musicState.CurrentTrackId;
            AudioTelemetry.CurrentCombatIntensity = musicState.SmoothedIntensity;

            int activeStemCount = 1; // base always active
            if (musicState.StemVolumes.y > 0.01f) activeStemCount++;
            if (musicState.StemVolumes.z > 0.01f) activeStemCount++;
            if (musicState.StemVolumes.w > 0.01f) activeStemCount++;
            AudioTelemetry.ActiveStemCount = activeStemCount;
        }

        private void UpdateTrackThresholds(ref MusicState musicState, MusicDatabaseSO database, int trackId)
        {
            var track = database.GetTrack(trackId);
            if (track != null)
                musicState.CurrentTrackThresholds = track.CombatIntensityThresholds;
            else
                musicState.CurrentTrackThresholds = new float3(0.2f, 0.5f, 0.8f);
            SystemAPI.SetSingleton(musicState);
        }

        private void EnsureAudioSources()
        {
            if (_musicPlayer != null) return;

            // Resolve mixer group once
            if (!_mixerGroupResolved)
            {
                _mixerGroupResolved = true;
                var audioManager = Object.FindAnyObjectByType<AudioManager>();
                if (audioManager != null && audioManager.MasterMixer != null)
                {
                    var groups = audioManager.MasterMixer.FindMatchingGroups("Music");
                    if (groups != null && groups.Length > 0)
                        _musicMixerGroup = groups[0];
                }
            }

            _musicPlayer = new GameObject("MusicPlayer");
            Object.DontDestroyOnLoad(_musicPlayer);

            _baseSrc = CreateStemSource("BaseStem");
            _percSrc = CreateStemSource("PercussionStem");
            _melodySrc = CreateStemSource("MelodyStem");
            _intensitySrc = CreateStemSource("IntensityStem");
            _stingerSrc = CreateStemSource("Stinger");
            _stingerSrc.loop = false;

            // Crossfade sources
            _xfadeBaseSrc = CreateStemSource("XF_BaseStem");
            _xfadePercSrc = CreateStemSource("XF_PercussionStem");
            _xfadeMelodySrc = CreateStemSource("XF_MelodyStem");
            _xfadeIntensitySrc = CreateStemSource("XF_IntensityStem");
        }

        private AudioSource CreateStemSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_musicPlayer.transform, false);
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 0f; // 2D music
            src.playOnAwake = false;
            src.loop = true;
            src.priority = 0; // highest priority

            if (_musicMixerGroup != null)
                src.outputAudioMixerGroup = _musicMixerGroup;

            return src;
        }

        private void AssignTrack(int trackId, MusicDatabaseSO database)
        {
            var track = database.GetTrack(trackId);
            _lastTrackId = trackId;

            if (track == null)
            {
                StopAllStems();
                return;
            }

            // Handle intro
            if (track.IntroClip != null)
            {
                _playingIntro = true;
                if (_baseSrc != null)
                {
                    _baseSrc.clip = track.IntroClip;
                    _baseSrc.loop = false;
                    _baseSrc.Play();
                }
                // Other stems wait for intro to finish
                StopStem(_percSrc);
                StopStem(_melodySrc);
                StopStem(_intensitySrc);
            }
            else
            {
                _playingIntro = false;
                AssignStemClips(track);
                PlayAllStems();
            }
        }

        private void AssignStemClips(MusicTrackSO track)
        {
            AssignClip(_baseSrc, track.BaseStem);
            AssignClip(_percSrc, track.PercussionStem);
            AssignClip(_melodySrc, track.MelodyStem);
            AssignClip(_intensitySrc, track.IntensityStem);
        }

        private void AssignClip(AudioSource src, AudioClip clip)
        {
            if (src == null) return;
            src.clip = clip;
            src.loop = true;
        }

        private void PlayAllStems()
        {
            // Sync all stems to play at the same time
            if (_baseSrc != null && _baseSrc.clip != null) { _baseSrc.timeSamples = 0; _baseSrc.Play(); }
            if (_percSrc != null && _percSrc.clip != null) { _percSrc.timeSamples = 0; _percSrc.Play(); }
            if (_melodySrc != null && _melodySrc.clip != null) { _melodySrc.timeSamples = 0; _melodySrc.Play(); }
            if (_intensitySrc != null && _intensitySrc.clip != null) { _intensitySrc.timeSamples = 0; _intensitySrc.Play(); }
        }

        private void StartCrossfade(int newTrackId, MusicDatabaseSO database)
        {
            // Move current stems to crossfade sources
            SwapToXfade(_baseSrc, _xfadeBaseSrc);
            SwapToXfade(_percSrc, _xfadePercSrc);
            SwapToXfade(_melodySrc, _xfadeMelodySrc);
            SwapToXfade(_intensitySrc, _xfadeIntensitySrc);
            _xfadeOutgoingTrackId = _lastTrackId;
            _crossfadeTargetTrackId = newTrackId;

            // Assign new track to main stems
            AssignTrack(newTrackId, database);
        }

        private void SwapToXfade(AudioSource main, AudioSource xfade)
        {
            if (main == null || xfade == null) return;
            xfade.clip = main.clip;
            xfade.volume = main.volume;
            xfade.timeSamples = main.timeSamples;
            xfade.loop = main.loop;
            if (xfade.clip != null) xfade.Play();
            main.Stop();
        }

        private void StopCrossfadeSources()
        {
            StopStem(_xfadeBaseSrc);
            StopStem(_xfadePercSrc);
            StopStem(_xfadeMelodySrc);
            StopStem(_xfadeIntensitySrc);
        }

        private void ApplyStemVolumes(float4 stemVols, float masterVolume)
        {
            if (_baseSrc != null) _baseSrc.volume = stemVols.x * masterVolume;
            if (_percSrc != null) _percSrc.volume = stemVols.y * masterVolume;
            if (_melodySrc != null) _melodySrc.volume = stemVols.z * masterVolume;
            if (_intensitySrc != null) _intensitySrc.volume = stemVols.w * masterVolume;
        }

        private void HandleLoopPoints(AudioSource src, MusicTrackSO track)
        {
            if (src == null || !src.isPlaying || src.clip == null) return;
            if (track.LoopEndSample <= 0) return;

            if (src.timeSamples >= track.LoopEndSample)
                src.timeSamples = track.LoopStartSample;
        }

        private void StopAllStems()
        {
            StopStem(_baseSrc);
            StopStem(_percSrc);
            StopStem(_melodySrc);
            StopStem(_intensitySrc);
        }

        private void StopStem(AudioSource src)
        {
            if (src != null) src.Stop();
        }

        protected override void OnDestroy()
        {
            if (_musicPlayer != null)
                Object.Destroy(_musicPlayer);
        }
    }
}
