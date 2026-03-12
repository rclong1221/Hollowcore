using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Audio.Config;

namespace Audio.Systems
{
    /// <summary>
    /// Managed audio source pool with priority-based eviction and bus routing.
    /// Replaces the inline 8-source pool in AudioManager with a configurable,
    /// priority-aware pool. Each pooled source has AudioLowPassFilter + AudioHighPassFilter
    /// for occlusion processing.
    /// EPIC 15.27 Phase 1.
    /// </summary>
    public class AudioSourcePool : MonoBehaviour
    {
        public static AudioSourcePool Instance { get; private set; }

        [Header("Pool Settings")]
        [Tooltip("Maximum number of pooled AudioSources")]
        [Range(16, 64)]
        public int PoolSize = 32;

        [Header("Bus Config")]
        [Tooltip("Audio bus configuration asset")]
        public AudioBusConfig BusConfig;

        // Telemetry
        public int ActiveCount => _active.Count;
        public int PeakCount { get; private set; }
        public int EvictionsThisFrame { get; private set; }

        private readonly List<PooledSource> _available = new List<PooledSource>();
        private readonly List<PooledSource> _active = new List<PooledSource>();

        public struct PooledSource
        {
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public AudioHighPassFilter HighPass;
            public AudioBusType Bus;
            public byte Priority;
            public float Distance; // cached distance to listener for eviction scoring
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

        private void InitializePool()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"PooledAudioSrc_{i}");
                go.transform.SetParent(transform, false);

                var src = go.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.playOnAwake = false;

                var lpf = go.AddComponent<AudioLowPassFilter>();
                lpf.cutoffFrequency = 22000f;

                var hpf = go.AddComponent<AudioHighPassFilter>();
                hpf.cutoffFrequency = 10f;

                _available.Add(new PooledSource
                {
                    Source = src,
                    LowPass = lpf,
                    HighPass = hpf,
                    Bus = AudioBusType.Combat,
                    Priority = 0,
                    Distance = 0f
                });
            }
        }

        /// <summary>
        /// Acquire a pooled AudioSource configured for the given bus.
        /// If pool is exhausted, evicts the lowest-priority furthest source.
        /// </summary>
        public PooledSource Acquire(AudioBusType bus, byte priority = 50, Vector3 position = default)
        {
            PooledSource result;

            if (_available.Count > 0)
            {
                result = _available[_available.Count - 1];
                _available.RemoveAt(_available.Count - 1);
            }
            else
            {
                // Priority eviction: steal lowest-scoring active source
                result = EvictLowestPriority(priority);
                if (result.Source == null)
                {
                    // All active sources are higher priority — create a temporary overflow source
                    var go = new GameObject("AudioOverflowSrc");
                    go.transform.SetParent(transform, false);
                    var src = go.AddComponent<AudioSource>();
                    src.spatialBlend = 1f;
                    src.playOnAwake = false;
                    var lpf = go.AddComponent<AudioLowPassFilter>();
                    lpf.cutoffFrequency = 22000f;
                    var hpf = go.AddComponent<AudioHighPassFilter>();
                    hpf.cutoffFrequency = 10f;

                    result = new PooledSource { Source = src, LowPass = lpf, HighPass = hpf };
                    AudioTelemetry.LogPlaybackFailure("Pool exhausted, overflow created");
                }
                EvictionsThisFrame++;
            }

            // Configure for bus
            result.Bus = bus;
            result.Priority = priority;
            result.Source.transform.position = position;

            if (BusConfig != null)
            {
                var settings = BusConfig.GetSettings(bus);
                result.Source.outputAudioMixerGroup = settings.MixerGroup;
                result.Source.spatialBlend = settings.DefaultSpatialBlend;
                result.Source.maxDistance = settings.DefaultMaxDistance > 0 ? settings.DefaultMaxDistance : 50f;
                result.Source.rolloffMode = settings.RolloffMode == 1 ? AudioRolloffMode.Linear : AudioRolloffMode.Logarithmic;
            }

            // Reset filters
            result.LowPass.cutoffFrequency = 22000f;
            result.HighPass.cutoffFrequency = 10f;

            _active.Add(result);
            if (_active.Count > PeakCount) PeakCount = _active.Count;

            return result;
        }

        /// <summary>
        /// Return a source to the pool. Stops playback and resets state.
        /// </summary>
        public void Release(PooledSource source)
        {
            if (source.Source == null) return;

            source.Source.Stop();
            source.Source.clip = null;
            source.Source.outputAudioMixerGroup = null;
            source.Source.volume = 1f;
            source.Source.pitch = 1f;
            source.Source.loop = false;
            source.Priority = 0;
            source.Distance = 0f;

            _active.Remove(source);

            // Only return to available if it's a pool-managed source (not overflow)
            if (source.Source.gameObject.name.StartsWith("PooledAudioSrc"))
            {
                _available.Add(source);
            }
            else
            {
                Destroy(source.Source.gameObject);
            }
        }

        /// <summary>
        /// Clean up finished one-shot sources. Call each frame from AudioSourcePoolSystem.
        /// </summary>
        public void CleanupFinished()
        {
            EvictionsThisFrame = 0;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var ps = _active[i];
                if (ps.Source == null || (!ps.Source.isPlaying && !ps.Source.loop))
                {
                    if (ps.Source != null)
                    {
                        ps.Source.Stop();
                        ps.Source.clip = null;
                        ps.Source.outputAudioMixerGroup = null;
                        ps.Source.volume = 1f;
                        ps.Source.pitch = 1f;
                        ps.Source.loop = false;
                    }
                    _active.RemoveAt(i);

                    if (ps.Source != null && ps.Source.gameObject.name.StartsWith("PooledAudioSrc"))
                    {
                        _available.Add(ps);
                    }
                    else if (ps.Source != null)
                    {
                        Destroy(ps.Source.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// Update cached distances for all active sources (for priority scoring).
        /// </summary>
        public void UpdateDistances(Vector3 listenerPos)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var ps = _active[i];
                ps.Distance = Vector3.Distance(ps.Source.transform.position, listenerPos);
                _active[i] = ps;
            }
        }

        /// <summary>Get active source count for a specific bus.</summary>
        public int GetActiveCountForBus(AudioBusType bus)
        {
            int count = 0;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Bus == bus) count++;
            return count;
        }

        private PooledSource EvictLowestPriority(byte incomingPriority)
        {
            if (_active.Count == 0)
                return default;

            int lowestIdx = -1;
            float lowestScore = float.MaxValue;

            for (int i = 0; i < _active.Count; i++)
            {
                var ps = _active[i];
                // Score = priority * inverse distance. Lower score = more likely to evict
                float score = ps.Priority * (1f / (1f + ps.Distance * 0.1f));
                if (score < lowestScore)
                {
                    lowestScore = score;
                    lowestIdx = i;
                }
            }

            if (lowestIdx < 0) return default;

            // Only evict if incoming priority is higher than the lowest active
            if (_active[lowestIdx].Priority > incomingPriority)
                return default; // Don't evict higher priority

            var evicted = _active[lowestIdx];
            evicted.Source.Stop();
            evicted.Source.clip = null;
            _active.RemoveAt(lowestIdx);
            return evicted;
        }
    }
}
