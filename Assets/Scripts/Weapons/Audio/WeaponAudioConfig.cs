using UnityEngine;
using System;

namespace DIG.Weapons.Audio
{
    /// <summary>
    /// EPIC 14.20: Scriptable object defining audio clips for a weapon.
    /// Supports multiple clip variations per event for natural sound variety.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponAudioConfig", menuName = "DIG/Weapons/Audio Config")]
    public class WeaponAudioConfig : ScriptableObject
    {
        [Header("Fire Sounds")]
        [Tooltip("Gunshot sounds - randomly selected per shot")]
        public AudioClip[] FireClips;

        [Tooltip("Volume for fire sounds")]
        [Range(0f, 1f)]
        public float FireVolume = 1f;

        [Tooltip("Pitch variation range (0.1 = +/-10%)")]
        [Range(0f, 0.3f)]
        public float FirePitchVariation = 0.05f;

        [Tooltip("Distant gunshot sounds (played at range)")]
        public AudioClip[] FireDistantClips;

        [Tooltip("Distance at which distant sound starts playing")]
        public float DistantSoundDistance = 50f;

        [Header("Dry Fire")]
        [Tooltip("Click sound when firing with no ammo")]
        public AudioClip[] DryFireClips;

        [Tooltip("Volume for dry fire")]
        [Range(0f, 1f)]
        public float DryFireVolume = 0.7f;

        [Header("Reload Sounds")]
        [Tooltip("Sound when reload starts")]
        public AudioClip[] ReloadStartClips;

        [Tooltip("Magazine out sound")]
        public AudioClip[] MagOutClips;

        [Tooltip("Magazine in sound")]
        public AudioClip[] MagInClips;

        [Tooltip("Bolt/slide pull sound")]
        public AudioClip[] BoltPullClips;

        [Tooltip("Reload complete sound")]
        public AudioClip[] ReloadCompleteClips;

        [Tooltip("Volume for reload sounds")]
        [Range(0f, 1f)]
        public float ReloadVolume = 0.8f;

        [Header("Shell Casing")]
        [Tooltip("Shell casing bounce sounds")]
        public AudioClip[] ShellBounceClips;

        [Tooltip("Volume for shell sounds")]
        [Range(0f, 1f)]
        public float ShellVolume = 0.4f;

        [Header("Equip/Unequip")]
        [Tooltip("Weapon equip/draw sound")]
        public AudioClip[] EquipClips;

        [Tooltip("Weapon unequip/holster sound")]
        public AudioClip[] UnequipClips;

        [Tooltip("Volume for equip sounds")]
        [Range(0f, 1f)]
        public float EquipVolume = 0.6f;

        [Header("Melee (if applicable)")]
        [Tooltip("Melee swing/whoosh sounds")]
        public AudioClip[] MeleeSwingClips;

        [Tooltip("Melee hit/impact sounds")]
        public AudioClip[] MeleeHitClips;

        [Tooltip("Volume for melee sounds")]
        [Range(0f, 1f)]
        public float MeleeVolume = 0.8f;

        [Header("Bow/Crossbow (if applicable)")]
        [Tooltip("Bow string draw/pull sounds")]
        public AudioClip[] BowDrawClips;

        [Tooltip("Bow string release/fire sounds")]
        public AudioClip[] BowReleaseClips;

        [Tooltip("Bow draw cancel sounds")]
        public AudioClip[] BowCancelClips;

        [Tooltip("Arrow nocking sounds")]
        public AudioClip[] ArrowNockClips;

        [Tooltip("Volume for bow sounds")]
        [Range(0f, 1f)]
        public float BowVolume = 0.7f;

        [Header("Throwable (if applicable)")]
        [Tooltip("Throw charge/wind-up sounds")]
        public AudioClip[] ThrowChargeClips;

        [Tooltip("Throw release sounds")]
        public AudioClip[] ThrowReleaseClips;

        [Tooltip("Volume for throw sounds")]
        [Range(0f, 1f)]
        public float ThrowVolume = 0.7f;

        [Header("Shield (if applicable)")]
        [Tooltip("Shield raise/block start sounds")]
        public AudioClip[] BlockStartClips;

        [Tooltip("Shield impact sounds")]
        public AudioClip[] BlockImpactClips;

        [Tooltip("Parry success sounds")]
        public AudioClip[] ParrySuccessClips;

        [Tooltip("Volume for shield sounds")]
        [Range(0f, 1f)]
        public float ShieldVolume = 0.8f;

        [Header("3D Audio Settings")]
        [Tooltip("Minimum distance for 3D sound falloff")]
        public float MinDistance = 1f;

        [Tooltip("Maximum distance for 3D sound")]
        public float MaxDistance = 100f;

        [Tooltip("Spatial blend (0 = 2D, 1 = 3D)")]
        [Range(0f, 1f)]
        public float SpatialBlend = 1f;

        /// <summary>
        /// Get a random clip from an array.
        /// </summary>
        public AudioClip GetRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
                return null;

            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        /// <summary>
        /// Get fire sound with pitch variation applied.
        /// </summary>
        public (AudioClip clip, float pitch) GetFireSound()
        {
            var clip = GetRandomClip(FireClips);
            float pitch = 1f + UnityEngine.Random.Range(-FirePitchVariation, FirePitchVariation);
            return (clip, pitch);
        }
    }

    /// <summary>
    /// EPIC 14.20: Audio event types for weapons.
    /// </summary>
    public enum WeaponAudioEventType
    {
        // Firearm events
        Fire,
        FireDistant,
        DryFire,
        ReloadStart,
        MagOut,
        MagIn,
        BoltPull,
        ReloadComplete,
        ShellBounce,
        Equip,
        Unequip,

        // Melee events
        MeleeSwing,
        MeleeHit,

        // Bow/Crossbow events
        BowDraw,
        BowRelease,
        BowCancel,
        ArrowNock,

        // Throwable events
        ThrowCharge,
        ThrowRelease,

        // Shield events
        BlockStart,
        BlockImpact,
        ParrySuccess
    }
}
