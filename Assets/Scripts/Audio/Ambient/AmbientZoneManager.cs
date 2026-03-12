using System.Collections.Generic;
using UnityEngine;

namespace Audio.Ambient
{
    /// <summary>
    /// EPIC 18.8: Manages ambient soundscape transitions.
    /// Tracks active zones, resolves highest-priority soundscape,
    /// crossfades between soundscapes, drives per-layer AudioSources.
    /// Uses a pre-allocated AudioSource pool to avoid runtime GameObject creation.
    /// </summary>
    public class AmbientZoneManager : MonoBehaviour
    {
        public static AmbientZoneManager Instance { get; private set; }

        [Header("Config")]
        [Tooltip("Global time of day (0-24) for day/night ambient blending. Set externally by weather/time system.")]
        [Range(0f, 24f)]
        public float TimeOfDay = 12f;

        [Tooltip("Max pre-allocated ambient layer sources. Covers current + fading-out layers.")]
        [SerializeField] private int _poolSize = 12;

        public AmbientSoundscapeSO ActiveSoundscape { get; private set; }

        private readonly List<AmbientZoneAuthoring> _activeZones = new();
        private readonly List<LayerInstance> _currentLayers = new();
        private readonly List<LayerInstance> _fadingOutLayers = new();

        private float _crossfadeTimer;
        private float _crossfadeDuration;
        private bool _crossfading;

        // Pre-allocated AudioSource pool
        private AudioSource[] _sourcePool;
        private bool[] _sourceInUse;

        private struct LayerInstance
        {
            public int PoolIndex;
            public AmbientLayer Config;
            public float TargetVolume;
            public float CurrentVolume;
            public float VarianceTimer;
            public float VarianceOffset;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializePool();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            TickCrossfade(dt);
            TickActiveLayers(dt);
            TickFadingOutLayers(dt);
        }

        public void OnZoneEnter(AmbientZoneAuthoring zone)
        {
            if (zone == null || zone.Soundscape == null) return;
            if (_activeZones.Contains(zone)) return;

            _activeZones.Add(zone);
            ResolveHighestPriority();
        }

        public void OnZoneExit(AmbientZoneAuthoring zone)
        {
            _activeZones.Remove(zone);
            ResolveHighestPriority();
        }

        /// <summary>
        /// Force-set a soundscape without zone triggers (e.g., from a script or cutscene).
        /// </summary>
        public void SetSoundscape(AmbientSoundscapeSO soundscape)
        {
            if (soundscape == ActiveSoundscape) return;
            TransitionTo(soundscape);
        }

        /// <summary>
        /// Immediately stop all ambient audio.
        /// </summary>
        public void StopAll()
        {
            for (int i = _currentLayers.Count - 1; i >= 0; i--)
                ReleaseLayer(_currentLayers, i);

            for (int i = _fadingOutLayers.Count - 1; i >= 0; i--)
                ReleaseLayer(_fadingOutLayers, i);

            ActiveSoundscape = null;
            _crossfading = false;
        }

        // ---- Pool Management ----

        private void InitializePool()
        {
            _sourcePool = new AudioSource[_poolSize];
            _sourceInUse = new bool[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"AmbientSrc_{i}");
                go.transform.SetParent(transform, false);
                go.SetActive(false);

                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.volume = 0f;

                _sourcePool[i] = src;
                _sourceInUse[i] = false;
            }
        }

        private int AcquirePooledSource()
        {
            for (int i = 0; i < _sourcePool.Length; i++)
            {
                if (!_sourceInUse[i])
                {
                    _sourceInUse[i] = true;
                    _sourcePool[i].gameObject.SetActive(true);
                    return i;
                }
            }
            return -1;
        }

        private void ReturnPooledSource(int index)
        {
            if (index < 0 || index >= _sourcePool.Length) return;

            var src = _sourcePool[index];
            if (src != null)
            {
                src.Stop();
                src.clip = null;
                src.volume = 0f;
                src.gameObject.SetActive(false);
            }
            _sourceInUse[index] = false;
        }

        // ---- Zone Resolution ----

        private void ResolveHighestPriority()
        {
            AmbientSoundscapeSO best = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < _activeZones.Count; i++)
            {
                var zone = _activeZones[i];
                if (zone == null || zone.Soundscape == null) continue;
                if (zone.Priority > bestPriority)
                {
                    bestPriority = zone.Priority;
                    best = zone.Soundscape;
                }
            }

            if (best == ActiveSoundscape) return;

            TransitionTo(best);
        }

        private void TransitionTo(AmbientSoundscapeSO newSoundscape)
        {
            // Move current layers to fading-out list
            for (int i = 0; i < _currentLayers.Count; i++)
            {
                var layer = _currentLayers[i];
                layer.TargetVolume = 0f;
                _fadingOutLayers.Add(layer);
            }
            _currentLayers.Clear();

            ActiveSoundscape = newSoundscape;

            if (newSoundscape == null) return;

            _crossfadeDuration = newSoundscape.CrossfadeDuration;
            _crossfadeTimer = 0f;
            _crossfading = true;

            for (int i = 0; i < newSoundscape.Layers.Length; i++)
            {
                var layerConfig = newSoundscape.Layers[i];
                var clip = PickClip(layerConfig);
                if (clip == null) continue;

                int poolIdx = AcquirePooledSource();
                if (poolIdx < 0)
                {
                    Debug.LogWarning("[AmbientZoneManager] Ambient source pool exhausted. Increase pool size.");
                    break;
                }

                var src = _sourcePool[poolIdx];
                src.clip = clip;
                src.spatialBlend = layerConfig.Is3D ? 1f : 0f;
                src.volume = 0f;
                src.Play();

                _currentLayers.Add(new LayerInstance
                {
                    PoolIndex = poolIdx,
                    Config = layerConfig,
                    TargetVolume = ComputeLayerVolume(layerConfig),
                    CurrentVolume = 0f,
                    VarianceTimer = 0f,
                    VarianceOffset = 0f
                });
            }
        }

        // ---- Tick ----

        private void TickCrossfade(float dt)
        {
            if (!_crossfading) return;

            _crossfadeTimer += dt;
            if (_crossfadeTimer >= _crossfadeDuration)
                _crossfading = false;
        }

        private void TickActiveLayers(float dt)
        {
            float crossfadeT = _crossfading ? Mathf.Clamp01(_crossfadeTimer / _crossfadeDuration) : 1f;
            float fadeSpeed = _crossfadeDuration > 0f ? 1f / _crossfadeDuration : 10f;

            for (int i = 0; i < _currentLayers.Count; i++)
            {
                var layer = _currentLayers[i];
                var src = _sourcePool[layer.PoolIndex];
                if (src == null) continue;

                layer.TargetVolume = ComputeLayerVolume(layer.Config);

                if (layer.Config.VolumeVariance > 0f)
                {
                    layer.VarianceTimer += dt;
                    if (layer.VarianceTimer >= 1f)
                    {
                        layer.VarianceTimer = 0f;
                        layer.VarianceOffset = Random.Range(-layer.Config.VolumeVariance, layer.Config.VolumeVariance);
                    }
                }

                float target = Mathf.Clamp01(layer.TargetVolume + layer.VarianceOffset) * crossfadeT;
                layer.CurrentVolume = Mathf.MoveTowards(layer.CurrentVolume, target, fadeSpeed * dt);
                src.volume = layer.CurrentVolume;

                _currentLayers[i] = layer;
            }
        }

        private void TickFadingOutLayers(float dt)
        {
            float fadeSpeed = _crossfadeDuration > 0f ? 1f / _crossfadeDuration : 10f;

            for (int i = _fadingOutLayers.Count - 1; i >= 0; i--)
            {
                var layer = _fadingOutLayers[i];
                var src = _sourcePool[layer.PoolIndex];

                if (src == null || !_sourceInUse[layer.PoolIndex])
                {
                    SwapRemove(_fadingOutLayers, i);
                    continue;
                }

                layer.CurrentVolume = Mathf.MoveTowards(layer.CurrentVolume, 0f, fadeSpeed * dt);
                src.volume = layer.CurrentVolume;
                _fadingOutLayers[i] = layer;

                if (layer.CurrentVolume <= 0.001f)
                    ReleaseLayer(_fadingOutLayers, i);
            }
        }

        // ---- Helpers ----

        private float ComputeLayerVolume(AmbientLayer config)
        {
            float vol = config.Volume;
            vol *= config.DayBlend.Evaluate(TimeOfDay);
            return Mathf.Clamp01(vol);
        }

        private static AudioClip PickClip(AmbientLayer layer)
        {
            if (layer.Clips == null || layer.Clips.Length == 0) return null;
            return layer.Clips[Random.Range(0, layer.Clips.Length)];
        }

        private void ReleaseLayer(List<LayerInstance> list, int index)
        {
            ReturnPooledSource(list[index].PoolIndex);
            SwapRemove(list, index);
        }

        private static void SwapRemove<T>(List<T> list, int index)
        {
            int last = list.Count - 1;
            if (index < last)
                list[index] = list[last];
            list.RemoveAt(last);
        }
    }
}
