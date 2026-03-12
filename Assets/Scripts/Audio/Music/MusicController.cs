using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Audio.Systems;
using Audio.Config;

namespace Audio.Music
{
    /// <summary>
    /// EPIC 18.8: Drives a MusicStateMachineSO at runtime.
    /// Two-source crossfade engine, intensity layers, stinger support.
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private MusicStateMachineSO _stateMachine;

        [Header("Mixer Routing")]
        [Tooltip("AudioMixerGroup to route music sources through. If null, falls back to AudioBusConfig.")]
        [SerializeField] private AudioMixerGroup _musicMixerGroup;

        [Tooltip("Max pre-allocated intensity layer sources. Avoids runtime GameObject creation.")]
        [SerializeField] private int _maxIntensityLayers = 4;

        public MusicStateMachineSO StateMachine
        {
            get => _stateMachine;
            set
            {
                _stateMachine = value;
                if (_initialized && _stateMachine != null)
                    SetState(_stateMachine.DefaultState);
            }
        }

        public string CurrentStateId { get; private set; }
        public float CurrentIntensity { get; private set; }
        public bool IsPlaying => (_sourceA != null && _sourceA.isPlaying) || (_sourceB != null && _sourceB.isPlaying);

        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private AudioSource _stingerSource;
        private bool _aIsActive = true;

        private AudioSource ActiveSource => _aIsActive ? _sourceA : _sourceB;
        private AudioSource InactiveSource => _aIsActive ? _sourceB : _sourceA;

        private MusicState _currentState;
        private int _currentTrackIndex;
        private List<int> _shuffledOrder;
        private int _shuffleReadIndex;

        // Crossfade
        private bool _crossfading;
        private float _crossfadeDuration;
        private float _crossfadeTimer;
        private float _crossfadeFromVolume;
        private float _crossfadeToVolume;

        // Pre-allocated intensity layer pool
        private AudioSource[] _layerPool;
        private float[] _layerTargetVolumes;
        private int _activeLayerCount;

        // Fade out all — stores initial volumes for frame-rate-independent interpolation
        private bool _fadingOutAll;
        private float _fadeOutAllDuration;
        private float _fadeOutAllTimer;
        private float _fadeOutInitialA;
        private float _fadeOutInitialB;
        private float[] _fadeOutInitialLayers;

        private bool _initialized;

        private void Awake()
        {
            ResolveMixerGroup();
            CreateSources();
            PreAllocateLayerPool();
            _initialized = true;

            if (_stateMachine != null && !string.IsNullOrEmpty(_stateMachine.DefaultState))
                SetState(_stateMachine.DefaultState);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            TickCrossfade(dt);
            TickIntensityLayers(dt);
            TickFadeOutAll(dt);
            TickTrackAdvance();
        }

        /// <summary>
        /// Transition to a named music state with crossfade.
        /// </summary>
        public void SetState(string stateId)
        {
            if (_stateMachine == null) return;
            if (stateId == CurrentStateId && _currentState != null) return;

            var newState = _stateMachine.FindState(stateId);
            if (newState == null)
            {
                Debug.LogWarning($"[MusicController] State '{stateId}' not found in {_stateMachine.name}");
                return;
            }

            float fadeOut = _currentState?.CrossfadeOut > 0 ? _currentState.CrossfadeOut : _stateMachine.GlobalCrossfadeDuration;
            float fadeIn = newState.CrossfadeIn > 0 ? newState.CrossfadeIn : _stateMachine.GlobalCrossfadeDuration;
            float crossfade = Mathf.Max(fadeOut, fadeIn);

            _currentState = newState;
            CurrentStateId = stateId;
            _currentTrackIndex = 0;
            _shuffleReadIndex = 0;

            if (newState.Mode == PlaylistMode.Shuffle)
                RebuildShuffle(newState);

            StartCrossfade(newState, crossfade);
            ActivateIntensityLayers(newState);

            AudioTelemetry.TrackTransitionsThisSession++;
        }

        /// <summary>
        /// Fire a named event that may trigger a state transition.
        /// </summary>
        public void FireEvent(string eventName)
        {
            if (_stateMachine == null || string.IsNullOrEmpty(CurrentStateId)) return;

            var transition = _stateMachine.FindTransition(CurrentStateId, eventName);
            if (transition != null)
                SetState(transition.TargetStateId);
        }

        /// <summary>
        /// Set the intensity parameter (0 = calm, 1 = intense).
        /// Controls which intensity layers are active.
        /// </summary>
        public void SetIntensity(float intensity)
        {
            CurrentIntensity = Mathf.Clamp01(intensity);
            AudioTelemetry.CurrentCombatIntensity = CurrentIntensity;
            UpdateLayerTargets();
        }

        /// <summary>
        /// Play a one-shot stinger layered on top of current music.
        /// </summary>
        public void PlayStinger(AudioClip stinger, float volume = 1f)
        {
            if (stinger == null || _stingerSource == null) return;

            _stingerSource.clip = stinger;
            _stingerSource.volume = volume;
            _stingerSource.Play();

            AudioTelemetry.StingersPlayedThisSession++;
        }

        /// <summary>
        /// Fade all music to silence over the given duration.
        /// </summary>
        public void FadeOut(float duration)
        {
            if (duration <= 0f)
            {
                StopAllSources();
                return;
            }

            _fadingOutAll = true;
            _fadeOutAllDuration = duration;
            _fadeOutAllTimer = 0f;

            // Snapshot current volumes for absolute (frame-rate-independent) interpolation
            _fadeOutInitialA = _sourceA != null ? _sourceA.volume : 0f;
            _fadeOutInitialB = _sourceB != null ? _sourceB.volume : 0f;

            if (_fadeOutInitialLayers == null || _fadeOutInitialLayers.Length < _maxIntensityLayers)
                _fadeOutInitialLayers = new float[_maxIntensityLayers];

            for (int i = 0; i < _activeLayerCount && i < _layerPool.Length; i++)
                _fadeOutInitialLayers[i] = _layerPool[i] != null ? _layerPool[i].volume : 0f;
        }

        /// <summary>
        /// Immediately stop all music.
        /// </summary>
        public void StopImmediate()
        {
            StopAllSources();
            CurrentStateId = null;
            _currentState = null;
            _crossfading = false;
            _fadingOutAll = false;
        }

        // ---- Internals ----

        private void ResolveMixerGroup()
        {
            if (_musicMixerGroup != null) return;

            var pool = AudioSourcePool.Instance;
            if (pool != null && pool.BusConfig != null)
            {
                var settings = pool.BusConfig.GetSettings(AudioBusType.Music);
                _musicMixerGroup = settings.MixerGroup;
            }
        }

        private void CreateSources()
        {
            _sourceA = CreateMusicSource("MusicSource_A");
            _sourceB = CreateMusicSource("MusicSource_B");
            _stingerSource = CreateMusicSource("MusicStinger");
            _stingerSource.loop = false;
        }

        private AudioSource CreateMusicSource(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = true;
            src.volume = 0f;

            if (_musicMixerGroup != null)
                src.outputAudioMixerGroup = _musicMixerGroup;

            return src;
        }

        private void PreAllocateLayerPool()
        {
            _layerPool = new AudioSource[_maxIntensityLayers];
            _layerTargetVolumes = new float[_maxIntensityLayers];
            _fadeOutInitialLayers = new float[_maxIntensityLayers];

            for (int i = 0; i < _maxIntensityLayers; i++)
            {
                _layerPool[i] = CreateMusicSource($"MusicLayer_{i}");
                _layerPool[i].gameObject.SetActive(false);
            }
        }

        private void StartCrossfade(MusicState newState, float duration)
        {
            var clip = PickTrack(newState);
            if (clip == null) return;

            _fadingOutAll = false;

            _aIsActive = !_aIsActive;
            ActiveSource.clip = clip;
            ActiveSource.volume = 0f;
            ActiveSource.loop = newState.Mode == PlaylistMode.Single || newState.Tracks.Length <= 1;
            ActiveSource.Play();

            _crossfading = true;
            _crossfadeDuration = Mathf.Max(duration, 0.05f);
            _crossfadeTimer = 0f;
            _crossfadeFromVolume = InactiveSource.volume;
            _crossfadeToVolume = newState.Volume;
        }

        private void TickCrossfade(float dt)
        {
            if (!_crossfading) return;

            _crossfadeTimer += dt;
            float t = Mathf.Clamp01(_crossfadeTimer / _crossfadeDuration);

            ActiveSource.volume = Mathf.Lerp(0f, _crossfadeToVolume, t);
            InactiveSource.volume = Mathf.Lerp(_crossfadeFromVolume, 0f, t);

            if (t >= 1f)
            {
                _crossfading = false;
                InactiveSource.Stop();
                InactiveSource.clip = null;
            }
        }

        private void TickTrackAdvance()
        {
            if (_currentState == null) return;
            if (_currentState.Tracks.Length <= 1) return;
            if (_currentState.Mode == PlaylistMode.Single) return;

            if (ActiveSource.isPlaying || ActiveSource.loop) return;

            _currentTrackIndex++;
            var clip = PickTrack(_currentState);
            if (clip == null) return;

            ActiveSource.clip = clip;
            ActiveSource.Play();
        }

        private AudioClip PickTrack(MusicState state)
        {
            if (state.Tracks == null || state.Tracks.Length == 0) return null;

            switch (state.Mode)
            {
                case PlaylistMode.Shuffle:
                    if (_shuffledOrder == null || _shuffleReadIndex >= _shuffledOrder.Count)
                    {
                        RebuildShuffle(state);
                        _shuffleReadIndex = 0;
                    }
                    int idx = _shuffledOrder[_shuffleReadIndex++];
                    _currentTrackIndex = idx;
                    return state.Tracks[idx];

                default:
                    return state.Tracks[_currentTrackIndex % state.Tracks.Length];
            }
        }

        private void RebuildShuffle(MusicState state)
        {
            _shuffledOrder ??= new List<int>(state.Tracks.Length);
            _shuffledOrder.Clear();
            for (int i = 0; i < state.Tracks.Length; i++)
                _shuffledOrder.Add(i);
            for (int i = _shuffledOrder.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_shuffledOrder[i], _shuffledOrder[j]) = (_shuffledOrder[j], _shuffledOrder[i]);
            }
            _shuffleReadIndex = 0;
        }

        // ---- Intensity Layers (pre-allocated pool) ----

        private void ActivateIntensityLayers(MusicState state)
        {
            DeactivateIntensityLayers();

            if (state.IntensityLayers == null || state.IntensityLayers.Length == 0) return;

            _activeLayerCount = Mathf.Min(state.IntensityLayers.Length, _layerPool.Length);

            for (int i = 0; i < _activeLayerCount; i++)
            {
                var layer = state.IntensityLayers[i];
                var src = _layerPool[i];

                src.gameObject.SetActive(true);
                src.clip = layer.Clip;
                src.loop = true;
                src.volume = 0f;
                src.Play();

                if (ActiveSource.isPlaying && layer.Clip != null)
                    src.time = ActiveSource.time % layer.Clip.length;
            }

            AudioTelemetry.ActiveStemCount = _activeLayerCount;
            UpdateLayerTargets();
        }

        private void DeactivateIntensityLayers()
        {
            for (int i = 0; i < _layerPool.Length; i++)
            {
                if (_layerPool[i] == null) continue;
                _layerPool[i].Stop();
                _layerPool[i].clip = null;
                _layerPool[i].volume = 0f;
                _layerPool[i].gameObject.SetActive(false);
                _layerTargetVolumes[i] = 0f;
            }
            _activeLayerCount = 0;
            AudioTelemetry.ActiveStemCount = 0;
        }

        private void UpdateLayerTargets()
        {
            if (_currentState == null || _layerTargetVolumes == null) return;

            for (int i = 0; i < _activeLayerCount && i < _currentState.IntensityLayers.Length; i++)
            {
                var layer = _currentState.IntensityLayers[i];
                _layerTargetVolumes[i] = CurrentIntensity >= layer.ActivateThreshold
                    ? _currentState.Volume
                    : 0f;
            }
        }

        private void TickIntensityLayers(float dt)
        {
            if (_currentState == null || _activeLayerCount == 0) return;

            for (int i = 0; i < _activeLayerCount && i < _currentState.IntensityLayers.Length; i++)
            {
                if (_layerPool[i] == null || !_layerPool[i].gameObject.activeSelf) continue;

                float fadeSpeed = 1f / Mathf.Max(_currentState.IntensityLayers[i].FadeTime, 0.05f);
                _layerPool[i].volume = Mathf.MoveTowards(
                    _layerPool[i].volume,
                    _layerTargetVolumes[i],
                    fadeSpeed * dt);
            }
        }

        // ---- Fade Out All (frame-rate-independent absolute interpolation) ----

        private void TickFadeOutAll(float dt)
        {
            if (!_fadingOutAll) return;

            _fadeOutAllTimer += dt;
            float t = 1f - Mathf.Clamp01(_fadeOutAllTimer / _fadeOutAllDuration);

            if (_sourceA != null) _sourceA.volume = _fadeOutInitialA * t;
            if (_sourceB != null) _sourceB.volume = _fadeOutInitialB * t;

            for (int i = 0; i < _activeLayerCount && i < _layerPool.Length; i++)
            {
                if (_layerPool[i] != null && _layerPool[i].gameObject.activeSelf)
                    _layerPool[i].volume = _fadeOutInitialLayers[i] * t;
            }

            if (_fadeOutAllTimer >= _fadeOutAllDuration)
            {
                _fadingOutAll = false;
                StopAllSources();
            }
        }

        private void StopAllSources()
        {
            if (_sourceA != null) { _sourceA.Stop(); _sourceA.volume = 0f; }
            if (_sourceB != null) { _sourceB.Stop(); _sourceB.volume = 0f; }
            if (_stingerSource != null) { _stingerSource.Stop(); }
            DeactivateIntensityLayers();
        }
    }
}
