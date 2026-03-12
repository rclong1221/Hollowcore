using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Items;
using DIG.Items.Authoring;
using DIG.Items.Definitions;
using DIG.Weapons; // For ComboData
using DIG.Weapons.Config;

namespace DIG.Weapons.Authoring
{
    /// <summary>
    /// Authoring-friendly combo step data.
    /// Gets converted to ComboData (IBufferElementData) during baking.
    /// </summary>
    [System.Serializable]
    public class ComboStepData
    {
        [Tooltip("Animation clip for this combo step")]
        public AnimationClip AnimationClip;
        
        [Tooltip("Animator substate index (must match Opsive animator setup)")]
        public int AnimatorSubStateIndex;
        
        [Tooltip("Duration of this combo step in seconds")]
        public float Duration = 0.5f;
        
        [Tooltip("Normalized time when input window opens (0-1)")]
        [Range(0f, 1f)]
        public float InputWindowStart = 0.5f;
        
        [Tooltip("Normalized time when input window closes (0-1)")]
        [Range(0f, 1f)]
        public float InputWindowEnd = 0.9f;
        
        [Tooltip("Damage multiplier for this combo step")]
        public float DamageMultiplier = 1f;
        
        [Tooltip("Knockback force applied on hit")]
        public float KnockbackForce = 5f;

        /// <summary>
        /// Convenience property for InputWindow duration in seconds
        /// </summary>
        public float InputWindow => (InputWindowEnd - InputWindowStart) * Duration;
    }

    /// <summary>
    /// EPIC 15.29: Authoring-friendly weapon modifier data.
    /// Gets converted to WeaponModifier (IBufferElementData) during baking.
    /// </summary>
    [System.Serializable]
    public class WeaponModifierData
    {
        [Tooltip("Type of on-hit effect")]
        public DIG.Weapons.ModifierType Type = DIG.Weapons.ModifierType.None;

        [Tooltip("Source of this modifier (for selective removal)")]
        public DIG.Weapons.ModifierSource Source = DIG.Weapons.ModifierSource.Innate;

        [Tooltip("Element for this modifier's damage")]
        public DIG.Targeting.Theming.DamageType Element = DIG.Targeting.Theming.DamageType.Physical;

        [Tooltip("Flat bonus damage (for BonusDamage type) or center damage (for Explosion)")]
        public float BonusDamage;

        [Tooltip("Proc probability per hit (0-1, 1 = guaranteed)")]
        [Range(0f, 1f)]
        public float Chance = 1f;

        [Tooltip("Duration in seconds (DOTs, debuffs, stun)")]
        public float Duration;

        [Tooltip("DPS for DOTs, % for lifesteal, speed multiplier for Slow")]
        public float Intensity;

        [Tooltip("AOE radius for Explosion/Chain/Cleave (0 = single target)")]
        public float Radius;

        [Tooltip("Knockback force")]
        public float Force;
    }

    /// <summary>
    /// Weapon type for authoring configuration.
    /// </summary>
    public enum WeaponType
    {
        None,
        Shootable,
        Melee,
        Throwable,
        Shield,
        Bow,
        Channel
    }

    /// <summary>
    /// Authoring component for weapon prefabs.
    /// Configures weapon type and all related components.
    /// </summary>
    public class WeaponAuthoring : MonoBehaviour
    {
        [Header("Weapon Type")]
        public WeaponType Type = WeaponType.Shootable;

        [Header("Configuration (Data-Driven)")]
        [Tooltip("Optional: The WeaponConfig asset that drives logic")]
        public DIG.Items.Definitions.WeaponConfig Config;

        [Header("Animation")]
        [Tooltip("Animator Item ID (must match Opsive's IDs: 1=AssaultRifle, 2=Pistol, 23=Knife, 24=Katana, etc.)")]
        public int AnimatorItemID = 1;

        [Header("Base Action")]
        public int ClipSize = 30;
        public int StartingAmmo = 30;
        public int ReserveAmmo = 90;

        [Header("Shootable Settings")]
        public float FireRate = 10f;
        public float Damage = 20f;
        public float Range = 100f;
        public float SpreadAngle = 2f;
        public float RecoilAmount = 5f;
        public float RecoilRecovery = 10f;
        public float ReloadTime = 2f;
        public bool IsAutomatic = true;
        public bool UseHitscan = true;

        [Header("Melee Settings")]
        public float MeleeDamage = 50f;
        public float MeleeRange = 2f;
        public float AttackSpeed = 2f;
        public float HitboxActiveStart = 0.2f;
        public float HitboxActiveEnd = 0.6f;
        public int ComboCount = 3;
        public float ComboWindow = 0.5f;
        public Vector3 HitboxOffset = new Vector3(0, 1, 1);
        public Vector3 HitboxSize = new Vector3(1, 1, 2);

        [Header("Combo Data (Per-Step)")]
        [Tooltip("Define each step in the combo chain with timing and damage multipliers")]
        public System.Collections.Generic.List<ComboStepData> comboData = new System.Collections.Generic.List<ComboStepData>();

        [Header("Combo System Override")]
        [Tooltip("If true, uses global ComboSystemConfig. If false, uses override values below.")]
        public bool useGlobalComboConfig = true;

        [Tooltip("Override: Input mode for this weapon")]
        public ComboInputMode inputModeOverride = ComboInputMode.InputPerSwing;

        [Tooltip("Override: How many attacks can be queued")]
        [Range(0, 5)]
        public int queueDepthOverride = 1;

        [Tooltip("Override: When attacks can be canceled")]
        public ComboCancelPolicy cancelPolicyOverride = ComboCancelPolicy.RecoveryOnly;

        [Tooltip("Override: Which actions can cancel attacks")]
        public ComboCancelPriority cancelPriorityOverride = ComboCancelPriority.Dodge;

        [Tooltip("Override: When to clear the attack queue")]
        public ComboQueueClearPolicy queueClearPolicyOverride = ComboQueueClearPolicy.Standard;

        [Header("Throwable Settings")]
        public float MinThrowForce = 10f;
        public float MaxThrowForce = 30f;
        public float ChargeTime = 1f;
        public float ThrowArc = 15f;
        [Tooltip("The projectile prefab to spawn when thrown (defines lifetime and damage)")]
        public GameObject ThrowableProjectilePrefab;

        [Header("Shield Settings")]
        public float BlockDamageReduction = 0.7f;
        public float ParryWindow = 0.15f;
        public float BlockAngle = 120f;
        public float StaminaCostPerBlock = 10f;

        [Header("Bow Settings")]
        [Tooltip("Time to fully draw the bow")]
        public float BowDrawTime = 1.0f;
        [Tooltip("Damage at minimum draw")]
        public float BowBaseDamage = 20f;
        [Tooltip("Damage at full draw")]
        public float BowMaxDamage = 80f;
        [Tooltip("Arrow speed at full draw")]
        public float BowProjectileSpeed = 50f;
        [Tooltip("Projectile prefab index for arrows")]
        public int BowProjectilePrefabIndex = 0;

        [Header("Channel Settings")]
        [Tooltip("How often to apply the effect (seconds)")]
        public float ChannelTickInterval = 0.25f;
        [Tooltip("Resource cost per tick (for future mana/stamina system)")]
        public float ChannelResourcePerTick = 5f;
        [Tooltip("Effect per tick (damage for offensive, heal for supportive)")]
        public float ChannelEffectPerTick = 10f;
        [Tooltip("Maximum channel duration (0 = unlimited)")]
        public float ChannelMaxTime = 0f;
        [Tooltip("Range of the channel effect")]
        public float ChannelRange = 15f;
        [Tooltip("Whether this channel heals (true) or damages (false)")]
        public bool ChannelIsHealing = false;
        [Tooltip("VFX prefab index for beam/stream effect")]
        public int ChannelBeamVfxIndex = 0;

        [Header("Damage Profile")]
        [Tooltip("Base element this weapon deals (Physical for most weapons)")]
        public DIG.Targeting.Theming.DamageType DamageElement = DIG.Targeting.Theming.DamageType.Physical;

        [Header("Weapon Modifiers — On-Hit Effects")]
        [Tooltip("Stackable effects that trigger on hit. Unlimited.")]
        public System.Collections.Generic.List<WeaponModifierData> weaponModifiers = new System.Collections.Generic.List<WeaponModifierData>();

        [Header("Aim Assist")]
        public bool EnableAimAssist = false;
        public float AimAssistStrength = 0.3f;
        public float AimAssistRange = 50f;
        public float AimAssistConeAngle = 20f;
        public float AimAssistMagnetism = 0.5f;

    // Note: Baking logic has been moved to WeaponBaker.cs
    }
}
