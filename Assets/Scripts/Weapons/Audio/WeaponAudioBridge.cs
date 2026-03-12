using UnityEngine;

namespace DIG.Weapons.Audio
{
    /// <summary>
    /// EPIC 14.20: Bridge component that connects weapon animation events to the audio system.
    /// Attach this to weapon prefabs to enable audio playback on animation events.
    /// </summary>
    public class WeaponAudioBridge : MonoBehaviour
    {
        [Header("Audio Configuration")]
        [Tooltip("Audio config asset for this weapon")]
        [SerializeField] private WeaponAudioConfig audioConfig;

        [Tooltip("Weapon type ID for audio manager lookup (alternative to direct config)")]
        [SerializeField] private int weaponTypeId = -1;

        [Header("Audio Source (Optional)")]
        [Tooltip("Dedicated AudioSource for this weapon (if not using pooled sources)")]
        [SerializeField] private AudioSource dedicatedSource;

        [Header("Muzzle Position")]
        [Tooltip("Transform at the muzzle for 3D audio positioning")]
        [SerializeField] private Transform muzzleTransform;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        private void Start()
        {
            // Register with audio manager if we have a config
            if (audioConfig != null && weaponTypeId >= 0 && WeaponAudioManager.Instance != null)
            {
                WeaponAudioManager.Instance.RegisterWeaponAudio(weaponTypeId, audioConfig);
            }

            // Default muzzle to this transform if not set
            if (muzzleTransform == null)
            {
                muzzleTransform = transform;
            }
        }

        /// <summary>
        /// Get the position for audio playback (muzzle position).
        /// </summary>
        public Vector3 GetAudioPosition()
        {
            return muzzleTransform != null ? muzzleTransform.position : transform.position;
        }

        /// <summary>
        /// Play fire sound.
        /// </summary>
        public void PlayFireSound()
        {
            PlaySound(WeaponAudioEventType.Fire);
        }

        /// <summary>
        /// Play dry fire (click) sound.
        /// </summary>
        public void PlayDryFireSound()
        {
            PlaySound(WeaponAudioEventType.DryFire);
        }

        /// <summary>
        /// Play reload start sound.
        /// </summary>
        public void PlayReloadStartSound()
        {
            PlaySound(WeaponAudioEventType.ReloadStart);
        }

        /// <summary>
        /// Play magazine out sound.
        /// </summary>
        public void PlayMagOutSound()
        {
            PlaySound(WeaponAudioEventType.MagOut);
        }

        /// <summary>
        /// Play magazine in sound.
        /// </summary>
        public void PlayMagInSound()
        {
            PlaySound(WeaponAudioEventType.MagIn);
        }

        /// <summary>
        /// Play bolt/slide sound.
        /// </summary>
        public void PlayBoltSound()
        {
            PlaySound(WeaponAudioEventType.BoltPull);
        }

        /// <summary>
        /// Play reload complete sound.
        /// </summary>
        public void PlayReloadCompleteSound()
        {
            PlaySound(WeaponAudioEventType.ReloadComplete);
        }

        /// <summary>
        /// Play shell casing bounce sound.
        /// </summary>
        public void PlayShellBounceSound(Vector3 position)
        {
            PlaySoundAtPosition(WeaponAudioEventType.ShellBounce, position);
        }

        /// <summary>
        /// Play equip sound.
        /// </summary>
        public void PlayEquipSound()
        {
            PlaySound(WeaponAudioEventType.Equip);
        }

        /// <summary>
        /// Play unequip sound.
        /// </summary>
        public void PlayUnequipSound()
        {
            PlaySound(WeaponAudioEventType.Unequip);
        }

        /// <summary>
        /// Play any weapon audio event.
        /// </summary>
        public void PlaySound(WeaponAudioEventType eventType)
        {
            PlaySoundAtPosition(eventType, GetAudioPosition());
        }

        /// <summary>
        /// Play weapon audio at specific position.
        /// </summary>
        public void PlaySoundAtPosition(WeaponAudioEventType eventType, Vector3 position)
        {
            if (debugLogging)
                Debug.Log($"[WeaponAudioBridge] Playing {eventType} at {position}");

            // Priority 1: Use WeaponAudioManager if available
            if (WeaponAudioManager.Instance != null && weaponTypeId >= 0)
            {
                WeaponAudioManager.Instance.PlayWeaponSound(weaponTypeId, eventType, position);
                return;
            }

            // Priority 2: Use direct config with dedicated source
            if (audioConfig != null)
            {
                AudioClip clip = GetClipForEvent(eventType);
                if (clip != null)
                {
                    if (debugLogging)
                        Debug.Log($"[WeaponAudioBridge] Playing clip '{clip.name}' for {eventType}");
                    PlayClipDirect(clip, position, GetVolumeForEvent(eventType));
                }
                else if (debugLogging)
                {
                    Debug.LogWarning($"[WeaponAudioBridge] No clip found for {eventType}! Check your WeaponAudioConfig asset.");
                }
                return;
            }

            if (debugLogging)
                Debug.LogWarning($"[WeaponAudioBridge] No audio config or manager available for {eventType}");
        }

        private AudioClip GetClipForEvent(WeaponAudioEventType eventType)
        {
            if (audioConfig == null) return null;

            return eventType switch
            {
                // Firearm events
                WeaponAudioEventType.Fire => audioConfig.GetRandomClip(audioConfig.FireClips),
                WeaponAudioEventType.DryFire => audioConfig.GetRandomClip(audioConfig.DryFireClips),
                WeaponAudioEventType.ReloadStart => audioConfig.GetRandomClip(audioConfig.ReloadStartClips),
                WeaponAudioEventType.MagOut => audioConfig.GetRandomClip(audioConfig.MagOutClips),
                WeaponAudioEventType.MagIn => audioConfig.GetRandomClip(audioConfig.MagInClips),
                WeaponAudioEventType.BoltPull => audioConfig.GetRandomClip(audioConfig.BoltPullClips),
                WeaponAudioEventType.ReloadComplete => audioConfig.GetRandomClip(audioConfig.ReloadCompleteClips),
                WeaponAudioEventType.ShellBounce => audioConfig.GetRandomClip(audioConfig.ShellBounceClips),
                WeaponAudioEventType.Equip => audioConfig.GetRandomClip(audioConfig.EquipClips),
                WeaponAudioEventType.Unequip => audioConfig.GetRandomClip(audioConfig.UnequipClips),

                // Melee events
                WeaponAudioEventType.MeleeSwing => audioConfig.GetRandomClip(audioConfig.MeleeSwingClips),
                WeaponAudioEventType.MeleeHit => audioConfig.GetRandomClip(audioConfig.MeleeHitClips),

                // Bow/Crossbow events
                WeaponAudioEventType.BowDraw => audioConfig.GetRandomClip(audioConfig.BowDrawClips),
                WeaponAudioEventType.BowRelease => audioConfig.GetRandomClip(audioConfig.BowReleaseClips),
                WeaponAudioEventType.BowCancel => audioConfig.GetRandomClip(audioConfig.BowCancelClips),
                WeaponAudioEventType.ArrowNock => audioConfig.GetRandomClip(audioConfig.ArrowNockClips),

                // Throwable events
                WeaponAudioEventType.ThrowCharge => audioConfig.GetRandomClip(audioConfig.ThrowChargeClips),
                WeaponAudioEventType.ThrowRelease => audioConfig.GetRandomClip(audioConfig.ThrowReleaseClips),

                // Shield events
                WeaponAudioEventType.BlockStart => audioConfig.GetRandomClip(audioConfig.BlockStartClips),
                WeaponAudioEventType.BlockImpact => audioConfig.GetRandomClip(audioConfig.BlockImpactClips),
                WeaponAudioEventType.ParrySuccess => audioConfig.GetRandomClip(audioConfig.ParrySuccessClips),

                _ => null
            };
        }

        private float GetVolumeForEvent(WeaponAudioEventType eventType)
        {
            if (audioConfig == null) return 1f;

            return eventType switch
            {
                // Firearm
                WeaponAudioEventType.Fire or WeaponAudioEventType.FireDistant => audioConfig.FireVolume,
                WeaponAudioEventType.DryFire => audioConfig.DryFireVolume,
                WeaponAudioEventType.ReloadStart or WeaponAudioEventType.MagOut or WeaponAudioEventType.MagIn
                    or WeaponAudioEventType.BoltPull or WeaponAudioEventType.ReloadComplete => audioConfig.ReloadVolume,
                WeaponAudioEventType.ShellBounce => audioConfig.ShellVolume,
                WeaponAudioEventType.Equip or WeaponAudioEventType.Unequip => audioConfig.EquipVolume,

                // Melee
                WeaponAudioEventType.MeleeSwing or WeaponAudioEventType.MeleeHit => audioConfig.MeleeVolume,

                // Bow/Crossbow
                WeaponAudioEventType.BowDraw or WeaponAudioEventType.BowRelease
                    or WeaponAudioEventType.BowCancel or WeaponAudioEventType.ArrowNock => audioConfig.BowVolume,

                // Throwable
                WeaponAudioEventType.ThrowCharge or WeaponAudioEventType.ThrowRelease => audioConfig.ThrowVolume,

                // Shield
                WeaponAudioEventType.BlockStart or WeaponAudioEventType.BlockImpact
                    or WeaponAudioEventType.ParrySuccess => audioConfig.ShieldVolume,

                _ => 1f
            };
        }

        private void PlayClipDirect(AudioClip clip, Vector3 position, float volume)
        {
            if (dedicatedSource != null)
            {
                dedicatedSource.transform.position = position;
                dedicatedSource.PlayOneShot(clip, volume);
            }
            else if (WeaponAudioManager.Instance != null)
            {
                WeaponAudioManager.Instance.PlayClipAtPosition(clip, position, volume);
            }
            else
            {
                // Fallback to Unity's built-in
                AudioSource.PlayClipAtPoint(clip, position, volume);
            }
        }

        // Animation event methods (can be called directly from animation clips)

        /// <summary>
        /// Animation event: Fire
        /// </summary>
        public void OnFire()
        {
            PlayFireSound();
        }

        /// <summary>
        /// Animation event: Dry fire
        /// </summary>
        public void OnDryFire()
        {
            PlayDryFireSound();
        }

        /// <summary>
        /// Animation event: Reload start
        /// </summary>
        public void OnReloadStart()
        {
            PlayReloadStartSound();
        }

        /// <summary>
        /// Animation event: Magazine detach
        /// </summary>
        public void OnMagOut()
        {
            PlayMagOutSound();
        }

        /// <summary>
        /// Animation event: Magazine insert
        /// </summary>
        public void OnMagIn()
        {
            PlayMagInSound();
        }

        /// <summary>
        /// Animation event: Bolt pull
        /// </summary>
        public void OnBoltPull()
        {
            PlayBoltSound();
        }

        /// <summary>
        /// Animation event: Reload complete
        /// </summary>
        public void OnReloadComplete()
        {
            PlayReloadCompleteSound();
        }

        /// <summary>
        /// Animation event: Equip
        /// </summary>
        public void OnEquip()
        {
            PlayEquipSound();
        }

        /// <summary>
        /// Animation event: Unequip
        /// </summary>
        public void OnUnequip()
        {
            PlayUnequipSound();
        }
    }
}
