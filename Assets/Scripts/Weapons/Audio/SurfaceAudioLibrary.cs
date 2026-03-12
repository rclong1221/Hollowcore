using UnityEngine;
using System.Collections.Generic;

namespace DIG.Weapons.Audio
{
    /// <summary>
    /// EPIC 14.20: Library of impact sounds organized by surface material.
    /// Singleton that provides audio clips for bullet impacts, footsteps, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "SurfaceAudioLibrary", menuName = "DIG/Weapons/Surface Audio Library")]
    public class SurfaceAudioLibrary : ScriptableObject
    {
        public static SurfaceAudioLibrary Instance { get; private set; }

        [System.Serializable]
        public class SurfaceSoundSet
        {
            public SurfaceMaterialType SurfaceType;

            [Header("Bullet Impacts")]
            public AudioClip[] BulletImpactClips;

            [Header("Melee Impacts")]
            public AudioClip[] MeleeImpactClips;

            [Header("Footsteps (optional)")]
            public AudioClip[] FootstepClips;

            [Header("Settings")]
            [Range(0f, 1f)]
            public float VolumeMultiplier = 1f;

            [Range(0f, 0.2f)]
            public float PitchVariation = 0.1f;
        }

        [Header("Surface Sound Sets")]
        [SerializeField] private List<SurfaceSoundSet> surfaceSounds = new List<SurfaceSoundSet>();

        [Header("Fallback")]
        [SerializeField] private SurfaceSoundSet defaultSounds;

        // Runtime lookup
        private Dictionary<SurfaceMaterialType, SurfaceSoundSet> _soundLookup;

        public void Initialize()
        {
            Instance = this;
            BuildLookup();
        }

        private void OnEnable()
        {
            // Auto-initialize when loaded
            if (Instance == null)
            {
                Initialize();
            }
        }

        private void BuildLookup()
        {
            _soundLookup = new Dictionary<SurfaceMaterialType, SurfaceSoundSet>();

            foreach (var soundSet in surfaceSounds)
            {
                if (!_soundLookup.ContainsKey(soundSet.SurfaceType))
                {
                    _soundLookup[soundSet.SurfaceType] = soundSet;
                }
            }
        }

        /// <summary>
        /// Get a random bullet impact sound for the given surface type.
        /// </summary>
        public AudioClip GetImpactSound(SurfaceMaterialType surfaceType)
        {
            var soundSet = GetSoundSet(surfaceType);
            if (soundSet?.BulletImpactClips == null || soundSet.BulletImpactClips.Length == 0)
                return null;

            return soundSet.BulletImpactClips[Random.Range(0, soundSet.BulletImpactClips.Length)];
        }

        /// <summary>
        /// Get impact sound with volume and pitch variation.
        /// </summary>
        public (AudioClip clip, float volume, float pitch) GetImpactSoundWithVariation(SurfaceMaterialType surfaceType)
        {
            var soundSet = GetSoundSet(surfaceType);
            if (soundSet?.BulletImpactClips == null || soundSet.BulletImpactClips.Length == 0)
                return (null, 1f, 1f);

            var clip = soundSet.BulletImpactClips[Random.Range(0, soundSet.BulletImpactClips.Length)];
            float volume = soundSet.VolumeMultiplier;
            float pitch = 1f + Random.Range(-soundSet.PitchVariation, soundSet.PitchVariation);

            return (clip, volume, pitch);
        }

        /// <summary>
        /// Get a random melee impact sound for the given surface type.
        /// </summary>
        public AudioClip GetMeleeImpactSound(SurfaceMaterialType surfaceType)
        {
            var soundSet = GetSoundSet(surfaceType);
            if (soundSet?.MeleeImpactClips == null || soundSet.MeleeImpactClips.Length == 0)
                return null;

            return soundSet.MeleeImpactClips[Random.Range(0, soundSet.MeleeImpactClips.Length)];
        }

        /// <summary>
        /// Get a random footstep sound for the given surface type.
        /// </summary>
        public AudioClip GetFootstepSound(SurfaceMaterialType surfaceType)
        {
            var soundSet = GetSoundSet(surfaceType);
            if (soundSet?.FootstepClips == null || soundSet.FootstepClips.Length == 0)
                return null;

            return soundSet.FootstepClips[Random.Range(0, soundSet.FootstepClips.Length)];
        }

        private SurfaceSoundSet GetSoundSet(SurfaceMaterialType surfaceType)
        {
            if (_soundLookup == null)
                BuildLookup();

            if (_soundLookup.TryGetValue(surfaceType, out var soundSet))
                return soundSet;

            return defaultSounds;
        }
    }
}
