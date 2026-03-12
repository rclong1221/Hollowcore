using UnityEngine;

namespace DIG.Weapons.Data
{
    /// <summary>
    /// Fire mode for firearms.
    /// </summary>
    public enum FireMode
    {
        SemiAutomatic,
        Automatic,
        Burst,
        BoltAction
    }

    /// <summary>
    /// Weapon category for template presets.
    /// </summary>
    public enum WeaponCategory
    {
        Pistol,
        Rifle,
        SMG,
        Shotgun,
        Sniper,
        LMG,
        Custom
    }

    /// <summary>
    /// ScriptableObject template for weapon configuration.
    /// Provides preset values for different weapon types to streamline setup.
    /// 
    /// Usage:
    /// 1. Create a template asset (Right-click > Create > DIG > Weapons > Weapon Template)
    /// 2. Configure preset values
    /// 3. Apply to weapon prefabs via WeaponAmmoAuthoring inspector button
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeaponTemplate", menuName = "DIG/Weapons/Weapon Template", order = 1)]
    public class WeaponTemplateAsset : ScriptableObject
    {
        [Header("Template Info")]
        [Tooltip("Name of this template (e.g., 'Standard Pistol', 'Assault Rifle')")]
        public string TemplateName = "New Weapon";
        
        [Tooltip("Category determines default values when creating new templates")]
        public WeaponCategory Category = WeaponCategory.Custom;

        [Header("Ammo Configuration")]
        [Tooltip("Rounds per magazine/clip")]
        [Range(1, 200)]
        public int ClipSize = 30;
        
        [Tooltip("Total reserve ammo capacity")]
        [Range(0, 999)]
        public int MaxReserveAmmo = 120;
        
        [Tooltip("Starting reserve ammo")]
        [Range(0, 999)]
        public int StartingReserveAmmo = 90;

        [Header("Fire Configuration")]
        [Tooltip("Rounds fired per second")]
        [Range(0.5f, 30f)]
        public float FireRate = 10f;
        
        [Tooltip("Firing mode")]
        public FireMode FireMode = FireMode.Automatic;
        
        [Tooltip("Rounds per burst (only for Burst mode)")]
        [Range(2, 5)]
        public int BurstCount = 3;

        [Header("Reload Configuration")]
        [Tooltip("Time to complete reload (seconds)")]
        [Range(0.5f, 5f)]
        public float ReloadTime = 2.0f;
        
        [Tooltip("Automatically reload when clip is empty")]
        public bool AutoReload = true;
        
        [Tooltip("Can interrupt reload to fire (tactical reload)")]
        public bool CanInterruptReload = false;

        [Header("Accuracy")]
        [Tooltip("Base spread angle in degrees")]
        [Range(0f, 15f)]
        public float BaseSpread = 1.5f;
        
        [Tooltip("Maximum spread angle when firing continuously")]
        [Range(0f, 20f)]
        public float MaxSpread = 5f;
        
        [Tooltip("Spread recovery rate per second")]
        [Range(1f, 50f)]
        public float SpreadRecovery = 10f;

        [Header("Recoil")]
        [Tooltip("Vertical recoil per shot (degrees)")]
        [Range(0f, 10f)]
        public float VerticalRecoil = 1.5f;
        
        [Tooltip("Horizontal recoil variance (degrees)")]
        [Range(0f, 5f)]
        public float HorizontalRecoil = 0.5f;

        [Header("Damage")]
        [Tooltip("Base damage per hit")]
        [Range(1, 500)]
        public int BaseDamage = 25;
        
        [Tooltip("Headshot damage multiplier")]
        [Range(1f, 5f)]
        public float HeadshotMultiplier = 2f;
        
        [Tooltip("Effective range in meters")]
        [Range(5f, 500f)]
        public float EffectiveRange = 50f;
        
        [Tooltip("Damage falloff at max range (0-1)")]
        [Range(0f, 1f)]
        public float DamageFalloff = 0.5f;

        [Header("VFX Presets")]
        [Tooltip("Muzzle flash particle prefab")]
        public GameObject MuzzleFlashPrefab;
        
        [Tooltip("Shell casing prefab for ejection")]
        public GameObject ShellCasingPrefab;
        
        [Tooltip("Impact effect prefab")]
        public GameObject ImpactEffectPrefab;

        [Header("Audio Presets")]
        [Tooltip("Fire sound clip")]
        public AudioClip FireSound;
        
        [Tooltip("Reload sound clip")]
        public AudioClip ReloadSound;
        
        [Tooltip("Dry fire (click) sound")]
        public AudioClip DryFireSound;
        
        [Tooltip("Magazine drop sound")]
        public AudioClip MagazineDropSound;

        /// <summary>
        /// Apply category defaults to this template.
        /// </summary>
        public void ApplyCategoryDefaults()
        {
            switch (Category)
            {
                case WeaponCategory.Pistol:
                    TemplateName = "Pistol";
                    ClipSize = 12;
                    MaxReserveAmmo = 60;
                    StartingReserveAmmo = 48;
                    FireRate = 5f;
                    FireMode = FireMode.SemiAutomatic;
                    ReloadTime = 1.5f;
                    BaseSpread = 2f;
                    MaxSpread = 6f;
                    VerticalRecoil = 2f;
                    BaseDamage = 30;
                    EffectiveRange = 25f;
                    break;

                case WeaponCategory.Rifle:
                    TemplateName = "Assault Rifle";
                    ClipSize = 30;
                    MaxReserveAmmo = 150;
                    StartingReserveAmmo = 120;
                    FireRate = 10f;
                    FireMode = FireMode.Automatic;
                    ReloadTime = 2.2f;
                    BaseSpread = 1.5f;
                    MaxSpread = 5f;
                    VerticalRecoil = 1.5f;
                    BaseDamage = 25;
                    EffectiveRange = 75f;
                    break;

                case WeaponCategory.SMG:
                    TemplateName = "SMG";
                    ClipSize = 25;
                    MaxReserveAmmo = 125;
                    StartingReserveAmmo = 100;
                    FireRate = 15f;
                    FireMode = FireMode.Automatic;
                    ReloadTime = 1.8f;
                    BaseSpread = 2.5f;
                    MaxSpread = 7f;
                    VerticalRecoil = 1f;
                    BaseDamage = 18;
                    EffectiveRange = 35f;
                    break;

                case WeaponCategory.Shotgun:
                    TemplateName = "Shotgun";
                    ClipSize = 8;
                    MaxReserveAmmo = 32;
                    StartingReserveAmmo = 24;
                    FireRate = 1.2f;
                    FireMode = FireMode.SemiAutomatic;
                    ReloadTime = 0.5f; // Per-shell reload
                    BaseSpread = 5f;
                    MaxSpread = 8f;
                    VerticalRecoil = 4f;
                    BaseDamage = 15; // Per pellet
                    EffectiveRange = 15f;
                    break;

                case WeaponCategory.Sniper:
                    TemplateName = "Sniper Rifle";
                    ClipSize = 5;
                    MaxReserveAmmo = 25;
                    StartingReserveAmmo = 20;
                    FireRate = 0.8f;
                    FireMode = FireMode.BoltAction;
                    ReloadTime = 3f;
                    BaseSpread = 0.1f;
                    MaxSpread = 0.5f;
                    VerticalRecoil = 5f;
                    BaseDamage = 100;
                    HeadshotMultiplier = 3f;
                    EffectiveRange = 200f;
                    DamageFalloff = 0.2f;
                    break;

                case WeaponCategory.LMG:
                    TemplateName = "Light Machine Gun";
                    ClipSize = 100;
                    MaxReserveAmmo = 300;
                    StartingReserveAmmo = 200;
                    FireRate = 12f;
                    FireMode = FireMode.Automatic;
                    ReloadTime = 4f;
                    BaseSpread = 3f;
                    MaxSpread = 10f;
                    VerticalRecoil = 2f;
                    BaseDamage = 22;
                    EffectiveRange = 60f;
                    break;
            }
        }

        /// <summary>
        /// Calculate time between shots based on fire rate.
        /// </summary>
        public float TimeBetweenShots => 1f / FireRate;

        /// <summary>
        /// Get a summary string for display.
        /// </summary>
        public string GetSummary()
        {
            return $"{TemplateName} ({Category})\n" +
                   $"Clip: {ClipSize} | Reserve: {MaxReserveAmmo}\n" +
                   $"Fire Rate: {FireRate} rps | Mode: {FireMode}\n" +
                   $"Reload: {ReloadTime}s | Damage: {BaseDamage}";
        }
    }
}
