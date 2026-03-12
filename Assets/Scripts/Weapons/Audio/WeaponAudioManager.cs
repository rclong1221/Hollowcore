using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

namespace DIG.Weapons.Audio
{
    /// <summary>
    /// EPIC 14.20: Central manager for weapon audio playback.
    /// Uses pooled AudioSources for efficient sound playback without runtime allocation.
    /// Supports 3D spatial audio, volume control, and pitch variation.
    /// </summary>
    public class WeaponAudioManager : MonoBehaviour
    {
        public static WeaponAudioManager Instance { get; private set; }

        [Header("Pool Settings")]
        [Tooltip("Initial pool size for AudioSources")]
        [SerializeField] private int initialPoolSize = 20;

        [Tooltip("Maximum pool size")]
        [SerializeField] private int maxPoolSize = 50;

        [Header("Global Settings")]
        [Tooltip("Master volume for all weapon sounds")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 1f;

        [Tooltip("Volume multiplier for gunshots")]
        [Range(0f, 2f)]
        [SerializeField] private float gunshotVolumeMultiplier = 1f;

        [Tooltip("Volume multiplier for impacts")]
        [Range(0f, 2f)]
        [SerializeField] private float impactVolumeMultiplier = 1f;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // AudioSource pool
        private ObjectPool<AudioSource> _audioSourcePool;
        private List<AudioSource> _activeAudioSources = new List<AudioSource>();
        private Transform _poolParent;

        // Registered weapon audio configs
        private Dictionary<int, WeaponAudioConfig> _weaponConfigs = new Dictionary<int, WeaponAudioConfig>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePool();
        }

        private void InitializePool()
        {
            // Create parent for pooled audio sources
            var poolObj = new GameObject("WeaponAudioPool");
            poolObj.transform.SetParent(transform);
            _poolParent = poolObj.transform;

            _audioSourcePool = new ObjectPool<AudioSource>(
                createFunc: CreateAudioSource,
                actionOnGet: OnGetAudioSource,
                actionOnRelease: OnReleaseAudioSource,
                actionOnDestroy: OnDestroyAudioSource,
                collectionCheck: false,
                defaultCapacity: initialPoolSize,
                maxSize: maxPoolSize
            );

            // Pre-warm pool
            var prewarm = new List<AudioSource>();
            for (int i = 0; i < initialPoolSize; i++)
            {
                prewarm.Add(_audioSourcePool.Get());
            }
            foreach (var source in prewarm)
            {
                _audioSourcePool.Release(source);
            }
        }

        private AudioSource CreateAudioSource()
        {
            var go = new GameObject("PooledAudioSource");
            go.transform.SetParent(_poolParent);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f; // Default to 3D
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 100f;

            return source;
        }

        private void OnGetAudioSource(AudioSource source)
        {
            source.gameObject.SetActive(true);
            _activeAudioSources.Add(source);
        }

        private void OnReleaseAudioSource(AudioSource source)
        {
            source.Stop();
            source.clip = null;
            source.gameObject.SetActive(false);
            _activeAudioSources.Remove(source);
        }

        private void OnDestroyAudioSource(AudioSource source)
        {
            if (source != null && source.gameObject != null)
            {
                Destroy(source.gameObject);
            }
        }

        private void Update()
        {
            // Return completed audio sources to pool
            for (int i = _activeAudioSources.Count - 1; i >= 0; i--)
            {
                var source = _activeAudioSources[i];
                if (source != null && !source.isPlaying)
                {
                    _audioSourcePool.Release(source);
                }
            }
        }

        /// <summary>
        /// Register a weapon's audio config by weapon type ID.
        /// </summary>
        public void RegisterWeaponAudio(int weaponTypeId, WeaponAudioConfig config)
        {
            _weaponConfigs[weaponTypeId] = config;

            if (debugLogging)
                Debug.Log($"[WeaponAudioManager] Registered audio config for weapon {weaponTypeId}");
        }

        /// <summary>
        /// Play a weapon audio event.
        /// </summary>
        public void PlayWeaponSound(int weaponTypeId, WeaponAudioEventType eventType, Vector3 position)
        {
            if (!_weaponConfigs.TryGetValue(weaponTypeId, out var config))
            {
                if (debugLogging)
                    Debug.LogWarning($"[WeaponAudioManager] No audio config for weapon {weaponTypeId}");
                return;
            }

            AudioClip clip = null;
            float volume = masterVolume;
            float pitch = 1f;
            float spatialBlend = config.SpatialBlend;
            float minDist = config.MinDistance;
            float maxDist = config.MaxDistance;

            switch (eventType)
            {
                case WeaponAudioEventType.Fire:
                    var fireSound = config.GetFireSound();
                    clip = fireSound.clip;
                    pitch = fireSound.pitch;
                    volume *= config.FireVolume * gunshotVolumeMultiplier;
                    break;

                case WeaponAudioEventType.FireDistant:
                    clip = config.GetRandomClip(config.FireDistantClips);
                    volume *= config.FireVolume * 0.7f * gunshotVolumeMultiplier;
                    break;

                case WeaponAudioEventType.DryFire:
                    clip = config.GetRandomClip(config.DryFireClips);
                    volume *= config.DryFireVolume;
                    break;

                case WeaponAudioEventType.ReloadStart:
                    clip = config.GetRandomClip(config.ReloadStartClips);
                    volume *= config.ReloadVolume;
                    break;

                case WeaponAudioEventType.MagOut:
                    clip = config.GetRandomClip(config.MagOutClips);
                    volume *= config.ReloadVolume;
                    break;

                case WeaponAudioEventType.MagIn:
                    clip = config.GetRandomClip(config.MagInClips);
                    volume *= config.ReloadVolume;
                    break;

                case WeaponAudioEventType.BoltPull:
                    clip = config.GetRandomClip(config.BoltPullClips);
                    volume *= config.ReloadVolume;
                    break;

                case WeaponAudioEventType.ReloadComplete:
                    clip = config.GetRandomClip(config.ReloadCompleteClips);
                    volume *= config.ReloadVolume;
                    break;

                case WeaponAudioEventType.ShellBounce:
                    clip = config.GetRandomClip(config.ShellBounceClips);
                    volume *= config.ShellVolume;
                    break;

                case WeaponAudioEventType.Equip:
                    clip = config.GetRandomClip(config.EquipClips);
                    volume *= config.EquipVolume;
                    break;

                case WeaponAudioEventType.Unequip:
                    clip = config.GetRandomClip(config.UnequipClips);
                    volume *= config.EquipVolume;
                    break;

                case WeaponAudioEventType.MeleeSwing:
                    clip = config.GetRandomClip(config.MeleeSwingClips);
                    volume *= 0.6f;
                    break;

                case WeaponAudioEventType.MeleeHit:
                    clip = config.GetRandomClip(config.MeleeHitClips);
                    volume *= 0.8f;
                    break;
            }

            if (clip != null)
            {
                PlayClipAtPosition(clip, position, volume, pitch, spatialBlend, minDist, maxDist);

                if (debugLogging)
                    Debug.Log($"[WeaponAudioManager] Playing {eventType} for weapon {weaponTypeId} at {position}");
            }
        }

        /// <summary>
        /// Play an audio clip directly at a position.
        /// </summary>
        public void PlayClipAtPosition(AudioClip clip, Vector3 position, float volume = 1f,
            float pitch = 1f, float spatialBlend = 1f, float minDistance = 1f, float maxDistance = 100f)
        {
            if (clip == null) return;

            var source = _audioSourcePool.Get();
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume * masterVolume;
            source.pitch = pitch;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.Play();
        }

        /// <summary>
        /// Play impact sound based on surface material.
        /// </summary>
        public void PlayImpactSound(SurfaceMaterialType surfaceType, Vector3 position, float volume = 1f)
        {
            // Use SurfaceAudioLibrary for impact sounds
            if (SurfaceAudioLibrary.Instance != null)
            {
                var clip = SurfaceAudioLibrary.Instance.GetImpactSound(surfaceType);
                if (clip != null)
                {
                    PlayClipAtPosition(clip, position, volume * impactVolumeMultiplier);
                }
            }
        }

        /// <summary>
        /// Set master volume.
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            _audioSourcePool?.Dispose();
        }
    }

    /// <summary>
    /// EPIC 14.20: Surface material types for audio selection.
    /// </summary>
    public enum SurfaceMaterialType
    {
        Default = 0,
        Concrete,
        Metal,
        Wood,
        Dirt,
        Grass,
        Sand,
        Water,
        Glass,
        Flesh,
        Cloth,
        Plastic
    }
}
