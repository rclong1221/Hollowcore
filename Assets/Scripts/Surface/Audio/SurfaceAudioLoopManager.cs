using UnityEngine;

namespace DIG.Surface.Audio
{
    /// <summary>
    /// EPIC 15.24 Phase 8: Manages a pool of looping AudioSources for continuous surface audio.
    /// Supports crossfading between surfaces and volume modulation based on speed.
    /// Attach to a persistent GameObject (e.g. audio manager or player).
    /// </summary>
    public class SurfaceAudioLoopManager : MonoBehaviour
    {
        private static SurfaceAudioLoopManager _instance;
        public static SurfaceAudioLoopManager Instance => _instance;
        public static bool HasInstance => _instance != null;

        private const int PoolSize = 4;

        private struct LoopSlot
        {
            public AudioSource Source;
            public SurfaceID Surface;
            public bool Active;
            public float TargetVolume;
            public float FadeSpeed;
        }

        private LoopSlot[] _slots;
        private float _spatialBlend = 1f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _slots = new LoopSlot[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"SurfaceLoop_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = _spatialBlend;
                src.volume = 0f;

                _slots[i] = new LoopSlot
                {
                    Source = src,
                    Surface = SurfaceID.Default,
                    Active = false,
                    TargetVolume = 0f,
                    FadeSpeed = 5f
                };
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                ref var slot = ref _slots[i];
                if (slot.Source == null) continue;

                // Fade towards target
                float current = slot.Source.volume;
                float target = slot.TargetVolume;

                if (Mathf.Abs(current - target) > 0.001f)
                {
                    slot.Source.volume = Mathf.MoveTowards(current, target, slot.FadeSpeed * dt);
                }

                // Stop source when faded out
                if (!slot.Active && slot.Source.volume < 0.001f && slot.Source.isPlaying)
                {
                    slot.Source.Stop();
                    slot.Source.clip = null;
                }
            }
        }

        /// <summary>
        /// Start or update a loop for the given surface. If already playing, updates volume.
        /// </summary>
        public void StartLoop(SurfaceID surface, AudioClip clip, float volume)
        {
            if (clip == null) return;

            // Check if already playing this surface
            for (int i = 0; i < PoolSize; i++)
            {
                if (_slots[i].Active && _slots[i].Surface == surface && _slots[i].Source.clip == clip)
                {
                    _slots[i].TargetVolume = volume;
                    return;
                }
            }

            // Find a free slot (prefer inactive, then lowest volume)
            int bestSlot = -1;
            float lowestVol = float.MaxValue;
            for (int i = 0; i < PoolSize; i++)
            {
                if (!_slots[i].Active)
                {
                    bestSlot = i;
                    break;
                }
                if (_slots[i].Source.volume < lowestVol)
                {
                    lowestVol = _slots[i].Source.volume;
                    bestSlot = i;
                }
            }

            if (bestSlot < 0) return;

            ref var slot = ref _slots[bestSlot];
            slot.Source.clip = clip;
            slot.Source.volume = 0f;
            slot.Source.Play();
            slot.Surface = surface;
            slot.Active = true;
            slot.TargetVolume = volume;
            slot.FadeSpeed = 5f; // ~0.2s fade in
        }

        /// <summary>
        /// Fade out and stop a loop for the given surface.
        /// </summary>
        public void StopLoop(SurfaceID surface, float fadeOutDuration)
        {
            for (int i = 0; i < PoolSize; i++)
            {
                if (_slots[i].Active && _slots[i].Surface == surface)
                {
                    _slots[i].Active = false;
                    _slots[i].TargetVolume = 0f;
                    _slots[i].FadeSpeed = fadeOutDuration > 0.01f ? 1f / fadeOutDuration : 100f;
                }
            }
        }

        /// <summary>
        /// Update the volume of an active loop (speed-based modulation).
        /// </summary>
        public void UpdateLoopVolume(SurfaceID surface, float volume)
        {
            for (int i = 0; i < PoolSize; i++)
            {
                if (_slots[i].Active && _slots[i].Surface == surface)
                {
                    _slots[i].TargetVolume = volume;
                    return;
                }
            }
        }

        /// <summary>
        /// Stop all active loops.
        /// </summary>
        public void StopAllLoops()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                _slots[i].Active = false;
                _slots[i].TargetVolume = 0f;
                _slots[i].FadeSpeed = 10f;
            }
        }

        /// <summary>
        /// Set spatial blend for all loop sources (Phase 7 integration).
        /// </summary>
        public void SetSpatialBlend(float blend)
        {
            _spatialBlend = blend;
            if (_slots == null) return;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_slots[i].Source != null)
                    _slots[i].Source.spatialBlend = blend;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
