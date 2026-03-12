using System.Collections.Generic;
using UnityEngine;
using Audio.Systems;

namespace Audio.Events
{
    /// <summary>
    /// Resolves clips from AudioEventSO, enforces cooldowns and max instances,
    /// and routes playback through AudioSourcePool.
    /// Stateless per-event — all mutable state lives here, not on the SO.
    /// </summary>
    public class AudioEventPlayer
    {
        private struct ActiveInstance
        {
            public int HandleId;
            public AudioSourcePool.PooledSource Pooled;
            public AudioEventSO Event;
            public Transform AttachParent;
            public float FadeOutDuration;
            public float FadeOutTimer;
            public float FadeInDuration;
            public float FadeInTimer;
            public float TargetVolume;
            public bool FadingOut;
        }

        private readonly Dictionary<int, float> _cooldowns = new();
        private readonly Dictionary<int, int> _instanceCounts = new();
        private readonly Dictionary<int, int> _sequentialIndices = new();
        private readonly Dictionary<int, int> _lastClipIndices = new();
        private readonly Dictionary<int, List<int>> _shuffleOrders = new();
        private readonly List<ActiveInstance> _active = new(32);
        private readonly List<int> _cooldownKeysBuffer = new(16);

        private int _nextHandleId = 1;

        public int ActiveCount => _active.Count;

        public AudioEventHandle Play(AudioEventSO evt, Vector3 position)
        {
            if (!CanPlay(evt)) return AudioEventHandle.Invalid;

            var clip = ResolveClip(evt);
            if (clip == null) return AudioEventHandle.Invalid;

            var pooled = AcquireSource(evt, position);
            if (pooled.Source == null) return AudioEventHandle.Invalid;

            float vol = ConfigureSource(pooled, evt, clip);
            pooled.Source.spatialBlend = evt.SpatialBlend;

            var handle = CreateHandle(evt);
            RegisterInstance(evt, handle, pooled, null, vol);
            MarkCooldown(evt);

            pooled.Source.Play();
            return handle;
        }

        public AudioEventHandle Play2D(AudioEventSO evt)
        {
            if (!CanPlay(evt)) return AudioEventHandle.Invalid;

            var clip = ResolveClip(evt);
            if (clip == null) return AudioEventHandle.Invalid;

            var pooled = AcquireSource(evt, Vector3.zero);
            if (pooled.Source == null) return AudioEventHandle.Invalid;

            float vol = ConfigureSource(pooled, evt, clip);
            pooled.Source.spatialBlend = 0f;

            var handle = CreateHandle(evt);
            RegisterInstance(evt, handle, pooled, null, vol);
            MarkCooldown(evt);

            pooled.Source.Play();
            return handle;
        }

        public AudioEventHandle PlayAttached(AudioEventSO evt, Transform parent)
        {
            if (parent == null) return AudioEventHandle.Invalid;
            if (!CanPlay(evt)) return AudioEventHandle.Invalid;

            var clip = ResolveClip(evt);
            if (clip == null) return AudioEventHandle.Invalid;

            var pooled = AcquireSource(evt, parent.position);
            if (pooled.Source == null) return AudioEventHandle.Invalid;

            float vol = ConfigureSource(pooled, evt, clip);
            pooled.Source.spatialBlend = evt.SpatialBlend;

            var handle = CreateHandle(evt);
            RegisterInstance(evt, handle, pooled, parent, vol);
            MarkCooldown(evt);

            pooled.Source.Play();
            return handle;
        }

        public void Stop(AudioEventHandle handle, float fadeOut = 0f)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].HandleId != handle.Id) continue;

                if (fadeOut > 0f)
                {
                    var inst = _active[i];
                    inst.FadingOut = true;
                    inst.FadeOutDuration = fadeOut;
                    inst.FadeOutTimer = 0f;
                    _active[i] = inst;
                }
                else
                {
                    ReleaseInstance(i);
                }
                return;
            }
        }

        public bool IsPlaying(AudioEventHandle handle)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].HandleId == handle.Id)
                    return true;
            return false;
        }

        /// <summary>
        /// Must be called every frame to manage fades, attached transforms,
        /// and cleanup of finished instances.
        /// </summary>
        public void Tick(float deltaTime)
        {
            TickCooldowns(deltaTime);

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var inst = _active[i];

                if (inst.Pooled.Source == null)
                {
                    RemoveInstance(i);
                    continue;
                }

                // Attached transform tracking — Unity's overloaded == handles destroyed objects
                if (inst.AttachParent is not null)
                {
                    if (inst.AttachParent == null)
                    {
                        ReleaseInstance(i);
                        continue;
                    }
                    inst.Pooled.Source.transform.position = inst.AttachParent.position;
                }

                // Fade-in
                if (inst.FadeInDuration > 0f && inst.FadeInTimer < inst.FadeInDuration)
                {
                    inst.FadeInTimer += deltaTime;
                    float t = Mathf.Clamp01(inst.FadeInTimer / inst.FadeInDuration);
                    inst.Pooled.Source.volume = inst.TargetVolume * t;
                    _active[i] = inst;
                    continue;
                }

                // Fade-out
                if (inst.FadingOut)
                {
                    inst.FadeOutTimer += deltaTime;
                    float t = 1f - Mathf.Clamp01(inst.FadeOutTimer / inst.FadeOutDuration);
                    inst.Pooled.Source.volume = inst.TargetVolume * t;
                    _active[i] = inst;

                    if (inst.FadeOutTimer >= inst.FadeOutDuration)
                    {
                        ReleaseInstance(i);
                    }
                    continue;
                }

                // Non-looping finished check
                if (!inst.Pooled.Source.loop && !inst.Pooled.Source.isPlaying)
                {
                    ReleaseInstance(i);
                }
            }
        }

        private bool CanPlay(AudioEventSO evt)
        {
            if (evt == null || evt.Clips == null || evt.Clips.Length == 0)
                return false;

            int hash = evt.GetInstanceID();

            // Cooldown check
            if (evt.Cooldown > 0f && _cooldowns.TryGetValue(hash, out float remaining) && remaining > 0f)
            {
                AudioTelemetry.LogThrottled();
                return false;
            }

            // Max instances check
            if (evt.MaxInstances > 0 && _instanceCounts.TryGetValue(hash, out int count) && count >= evt.MaxInstances)
            {
                AudioTelemetry.LogThrottled();
                return false;
            }

            return true;
        }

        private AudioClip ResolveClip(AudioEventSO evt)
        {
            if (evt.Clips.Length == 0) return null;
            if (evt.Clips.Length == 1) return evt.Clips[0];

            int hash = evt.GetInstanceID();

            switch (evt.SelectionMode)
            {
                case ClipSelection.Sequential:
                {
                    _sequentialIndices.TryGetValue(hash, out int idx);
                    var clip = evt.Clips[idx % evt.Clips.Length];
                    _sequentialIndices[hash] = (idx + 1) % evt.Clips.Length;
                    return clip;
                }

                case ClipSelection.RandomNoRepeat:
                {
                    int idx = Random.Range(0, evt.Clips.Length);
                    if (_lastClipIndices.TryGetValue(hash, out int lastIdx) && idx == lastIdx && evt.Clips.Length > 1)
                        idx = (idx + 1) % evt.Clips.Length;
                    _lastClipIndices[hash] = idx;
                    return evt.Clips[idx];
                }

                case ClipSelection.Shuffle:
                {
                    if (!_shuffleOrders.TryGetValue(hash, out var order) || order.Count == 0)
                    {
                        order = new List<int>(evt.Clips.Length);
                        for (int i = 0; i < evt.Clips.Length; i++) order.Add(i);
                        ShuffleList(order);
                        _shuffleOrders[hash] = order;
                    }
                    int pick = order[order.Count - 1];
                    order.RemoveAt(order.Count - 1);
                    return evt.Clips[pick];
                }

                default: // Random
                    return evt.Clips[Random.Range(0, evt.Clips.Length)];
            }
        }

        private AudioSourcePool.PooledSource AcquireSource(AudioEventSO evt, Vector3 position)
        {
            if (AudioSourcePool.Instance == null)
            {
                AudioTelemetry.LogPlaybackFailure("AudioSourcePool not available");
                return default;
            }

            return AudioSourcePool.Instance.Acquire(evt.Bus, (byte)evt.Priority, position);
        }

        /// <summary>Returns the resolved target volume for use by RegisterInstance.</summary>
        private float ConfigureSource(AudioSourcePool.PooledSource pooled, AudioEventSO evt, AudioClip clip)
        {
            var src = pooled.Source;
            src.clip = clip;
            src.loop = evt.Loop;
            src.minDistance = evt.MinDistance;
            src.maxDistance = evt.MaxDistance;
            src.rolloffMode = evt.RolloffMode;

            if (evt.RolloffMode == AudioRolloffMode.Custom && evt.CustomRolloff != null)
                src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, evt.CustomRolloff);

            src.reverbZoneMix = evt.ReverbSend;

            float vol = evt.Volume.Evaluate();
            src.pitch = evt.Pitch.Evaluate();

            src.volume = evt.FadeIn > 0f ? 0f : vol;
            return vol;
        }

        private AudioEventHandle CreateHandle(AudioEventSO evt)
        {
            return new AudioEventHandle(_nextHandleId++, evt.GetInstanceID());
        }

        private void RegisterInstance(AudioEventSO evt, AudioEventHandle handle,
            AudioSourcePool.PooledSource pooled, Transform parent, float targetVolume)
        {
            _active.Add(new ActiveInstance
            {
                HandleId = handle.Id,
                Pooled = pooled,
                Event = evt,
                AttachParent = parent,
                FadeOutDuration = evt.FadeOut,
                FadeOutTimer = 0f,
                FadeInDuration = evt.FadeIn,
                FadeInTimer = 0f,
                TargetVolume = targetVolume,
                FadingOut = false
            });

            int hash = evt.GetInstanceID();
            _instanceCounts.TryGetValue(hash, out int c);
            _instanceCounts[hash] = c + 1;
        }

        private void ReleaseInstance(int index)
        {
            var inst = _active[index];
            if (inst.Event != null)
            {
                int hash = inst.Event.GetInstanceID();
                if (_instanceCounts.TryGetValue(hash, out int c))
                    _instanceCounts[hash] = Mathf.Max(0, c - 1);
            }

            if (inst.Pooled.Source != null && AudioSourcePool.Instance != null)
                AudioSourcePool.Instance.Release(inst.Pooled);

            RemoveInstance(index);
        }

        private void RemoveInstance(int index)
        {
            int last = _active.Count - 1;
            if (index < last)
                _active[index] = _active[last];
            _active.RemoveAt(last);
        }

        private void MarkCooldown(AudioEventSO evt)
        {
            if (evt.Cooldown > 0f)
                _cooldowns[evt.GetInstanceID()] = evt.Cooldown;
        }

        private void TickCooldowns(float deltaTime)
        {
            if (_cooldowns.Count == 0) return;

            _cooldownKeysBuffer.Clear();
            foreach (var key in _cooldowns.Keys)
                _cooldownKeysBuffer.Add(key);

            for (int i = 0; i < _cooldownKeysBuffer.Count; i++)
            {
                int key = _cooldownKeysBuffer[i];
                float remaining = _cooldowns[key] - deltaTime;
                if (remaining <= 0f)
                    _cooldowns.Remove(key);
                else
                    _cooldowns[key] = remaining;
            }
        }

        private static void ShuffleList(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public void ReleaseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                ReleaseInstance(i);

            _cooldowns.Clear();
            _instanceCounts.Clear();
        }
    }
}
