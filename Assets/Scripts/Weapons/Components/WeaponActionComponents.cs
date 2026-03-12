using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Combat.Resources;

namespace DIG.Weapons
{
    /// <summary>
    /// Types of usable actions.
    /// </summary>
    public enum UsableActionType : byte
    {
        None = 0,
        Shootable = 1,
        Melee = 2,
        Throwable = 3,
        Magic = 4,
        Shield = 5,
        Bow = 6,
        Channel = 7
    }

    /// <summary>
    /// Types of projectiles.
    /// </summary>
    public enum ProjectileType : byte
    {
        None = 0,
        Bullet = 1,
        Grenade = 2,
        Rocket = 3,
        Arrow = 4,
        Magic = 5
    }

    #region Base Action Components

    /// <summary>
    /// Base component for all usable item actions.
    /// Tracks use state, cooldown, and ammo.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct UsableAction : IComponentData
    {
        /// <summary>
        /// Type of action this item performs.
        /// </summary>
        [GhostField]
        public UsableActionType ActionType;

        /// <summary>
        /// Animator Item ID for Opsive animation system.
        /// Standard values: 1=AssaultRifle, 2=Pistol, 23=Knife, 24=Katana
        /// </summary>
        [GhostField]
        public int AnimatorItemID;

        /// <summary>
        /// Whether this action can currently be used.
        /// </summary>
        [GhostField]
        public bool CanUse;

        /// <summary>
        /// Whether this action is currently being used.
        /// </summary>
        [GhostField]
        public bool IsUsing;

        /// <summary>
        /// Time spent in current use.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float UseTime;

        /// <summary>
        /// Remaining cooldown before next use.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CooldownRemaining;

        /// <summary>
        /// Current ammo in clip.
        /// </summary>
        [GhostField]
        public int AmmoCount;

        /// <summary>
        /// Maximum ammo per clip.
        /// </summary>
        public int ClipSize;

        /// <summary>
        /// Reserve ammo available.
        /// </summary>
        [GhostField]
        public int ReserveAmmo;
    }

    /// <summary>
    /// Request to use an action.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct UseRequest : IComponentData
    {
        /// <summary>
        /// True when use input starts.
        /// </summary>
        [GhostField]
        public bool StartUse;

        /// <summary>
        /// True when reload input is pressed.
        /// </summary>
        [GhostField]
        public bool Reload;

        /// <summary>
        /// True when use input stops.
        /// </summary>
        [GhostField]
        public bool StopUse;

        /// <summary>
        /// World position being aimed at.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float3 AimPoint;

        /// <summary>
        /// Direction of aim.
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float3 AimDirection;
    }

    #endregion

    #region Shootable Components



    /// <summary>
    /// Core firing mechanics (Hitscan or Projectile).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeaponFireComponent : IComponentData
    {
        public float FireRate;
        public float Damage;
        public float Range;
        public bool IsAutomatic;
        public bool UseHitscan;
        public int ProjectilePrefabIndex;
    }

    /// <summary>
    /// Runtime state for firing.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeaponFireState : IComponentData
    {
        [GhostField]
        public bool IsFiring;
        
        [GhostField(Quantization = 100)]
        public float TimeSinceLastShot;
        
        /// <summary>
        /// Time remaining for fire animation. IsFiring stays true until this reaches 0.
        /// Prevents animation bridge from missing the fire state on quick clicks.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float FireAnimationTimer;
    }

    /// <summary>
    /// Runtime state for aiming (ADS).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeaponAimState : IComponentData
    {
        [GhostField]
        public bool IsAiming;
    }

    /// <summary>
    /// Recoil configuration.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeaponRecoilComponent : IComponentData
    {
        public float RecoilAmount;
        public float RecoilRecovery;
        public float2 Randomness; // x: horizontal, y: vertical
    }

    /// <summary>
    /// Runtime state for recoil.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeaponRecoilState : IComponentData
    {
        [GhostField(Quantization = 100)]
        public float CurrentRecoil;
        
        [GhostField(Quantization = 100)]
        public float2 TargetRecoilImpulse; // Accumulated impulse to apply
    }

    /// <summary>
    /// Spread configuration.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeaponSpreadComponent : IComponentData
    {
        public float BaseSpread;
        public float MaxSpread;
        public float SpreadIncrement; // Per shot
        public float SpreadRecovery; // Per second
        public float MovementMultiplier; // Spread multiplier when moving
    }

    /// <summary>
    /// Runtime state for spread.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeaponSpreadState : IComponentData
    {
        [GhostField(Quantization = 100)]
        public float CurrentSpread;
    }

    /// <summary>
    /// Ammo and Reload configuration.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeaponAmmoComponent : IComponentData
    {
        public int ClipSize;
        public float ReloadTime;
        public bool AutoReload; // Reload automatically when empty
    }

    /// <summary>
    /// Runtime state for ammo.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WeaponAmmoState : IComponentData
    {
        [GhostField]
        public int AmmoCount;
        
        [GhostField]
        public int ReserveAmmo;
        
        [GhostField]
        public bool IsReloading;
        
        [GhostField(Quantization = 100)]
        public float ReloadProgress;
    }

    // Deprecated monolithic components (Removed for refactor safety)
    // Deprecated monolithic components (Restored for compatibility with ShootableActionSystem)
    public struct ShootableAction : IComponentData
    {
        public bool IsAutomatic;
        public float FireRate;
        public float SpreadAngle;
        public float RecoilAmount;
        public bool UseHitscan;
        public float Range;
        public float ReloadTime;
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ShootableState : IComponentData
    {
        [GhostField] public bool IsFiring;
        [GhostField] public bool IsReloading;
        [GhostField] public float TimeSinceLastShot;
        [GhostField(Quantization = 100)] public float ReloadProgress;
        [GhostField(Quantization = 100)] public float CurrentSpread;
    }

    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RecoilState : IComponentData
    {
        [GhostField(Quantization = 100)] public float2 RecoilVelocity;
        [GhostField(Quantization = 100)] public float2 CurrentRecoil; // x = yaw, y = pitch
        public float RecoverySpeed;
    }

    #endregion

    #region Melee Components

    /// <summary>
    /// Configuration for melee weapons.
    /// </summary>
    public struct MeleeAction : IComponentData
    {
        /// <summary>
        /// Damage per hit.
        /// </summary>
        public float Damage;

        /// <summary>
        /// Attack range.
        /// </summary>
        public float Range;

        /// <summary>
        /// Attacks per second.
        /// </summary>
        public float AttackSpeed;

        /// <summary>
        /// Normalized time when hitbox activates.
        /// </summary>
        public float HitboxActiveStart;

        /// <summary>
        /// Normalized time when hitbox deactivates.
        /// </summary>
        public float HitboxActiveEnd;

        /// <summary>
        /// Number of combo hits.
        /// </summary>
        public int ComboCount;

        /// <summary>
        /// Window to chain combos.
        /// </summary>
        public float ComboWindow;

        // ============================================================
        // Per-Weapon Combo Overrides (EPIC 15.7)
        // ============================================================

        /// <summary>
        /// If true, uses global ComboSystemSettings. If false, uses per-weapon overrides below.
        /// </summary>
        public bool UseGlobalComboConfig;

        /// <summary>
        /// Per-weapon input mode override.
        /// </summary>
        public byte InputModeOverride;

        /// <summary>
        /// Per-weapon queue depth override.
        /// </summary>
        public int QueueDepthOverride;

        /// <summary>
        /// Per-weapon cancel policy override.
        /// </summary>
        public byte CancelPolicyOverride;

        /// <summary>
        /// Per-weapon cancel priority flags override.
        /// </summary>
        public byte CancelPriorityOverride;

        /// <summary>
        /// Per-weapon queue clear policy flags override.
        /// </summary>
        public byte QueueClearPolicyOverride;
    }

    /// <summary>
    /// Runtime state for melee weapons.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct MeleeState : IComponentData
    {
        /// <summary>
        /// Current combo index (0 = first attack).
        /// </summary>
        [GhostField]
        public int CurrentCombo;

        /// <summary>
        /// Time in current attack.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float AttackTime;

        /// <summary>
        /// Time since last attack ended.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TimeSinceAttack;

        /// <summary>
        /// Whether hitbox is currently active.
        /// </summary>
        [GhostField]
        public bool HitboxActive;

        /// <summary>
        /// Whether we've hit something this swing.
        /// </summary>
        [GhostField]
        public bool HasHitThisSwing;

        /// <summary>
        /// True if currently attacking.
        /// </summary>
        [GhostField]
        public bool IsAttacking;

        /// <summary>
        /// True if an attack input was received during the current attack (for combo chaining).
        /// Not replicated - derived from Input on both Client (Prediction) and Server.
        /// </summary>
        public bool QueuedAttack;

        /// <summary>
        /// Number of attacks currently queued (for QueueDepth > 1).
        /// </summary>
        public int QueuedAttackCount;

        /// <summary>
        /// Tracks if the current input press has been consumed (for InputPerSwing mode).
        /// Prevents held input from triggering multiple attacks.
        /// </summary>
        public bool InputConsumed;

        /// <summary>
        /// Previous frame's attack input state (for detecting new presses).
        /// </summary>
        public bool PreviousInputState;

        /// <summary>
        /// Whether the attack was canceled by another action.
        /// </summary>
        [GhostField]
        public bool WasCanceled;

        /// <summary>
        /// Normalized time in current attack (0-1).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float NormalizedTime;

        /// <summary>
        /// For RhythmBased mode: whether the last input was timed correctly.
        /// </summary>
        [GhostField]
        public bool RhythmSuccess;

        /// <summary>
        /// For RhythmBased mode: bonus multiplier earned from perfect timing.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float RhythmBonus;
    }

    /// <summary>
    /// Hitbox for melee attacks.
    /// </summary>
    public struct MeleeHitbox : IComponentData
    {
        /// <summary>
        /// Offset from owner position.
        /// </summary>
        public float3 Offset;

        /// <summary>
        /// Box dimensions.
        /// </summary>
        public float3 Size;

        /// <summary>
        /// Whether hitbox is currently checking for hits.
        /// </summary>
        public bool IsActive;
    }

    #endregion

    #region Throwable Components

    /// <summary>
    /// Configuration for throwable items.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ThrowableAction : IComponentData
    {
        /// <summary>
        /// Minimum throw force.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float MinForce;

        /// <summary>
        /// Maximum throw force.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float MaxForce;

        /// <summary>
        /// Time to charge to max force.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ChargeTime;

        /// <summary>
        /// Arc angle above aim direction.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ThrowArc;

        /// <summary>
        /// Prefab entity for projectile (baked from GameObject reference).
        /// </summary>
        [GhostField]
        public Entity ProjectilePrefab;
    }

    /// <summary>
    /// Runtime state for throwables.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ThrowableState : IComponentData
    {
        /// <summary>
        /// Charge progress (0-1).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ChargeProgress;

        /// <summary>
        /// Whether currently charging throw.
        /// </summary>
        [GhostField]
        public bool IsCharging;

        /// <summary>
        /// Direction to throw.
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float3 AimDirection;

        /// <summary>
        /// Calculated spawn position for the projectile.
        /// Set by client using socket position (hand), replicated to server for spawning.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float3 SpawnPosition;
    }

    #endregion

    #region Shield Components

    /// <summary>
    /// Configuration for shields/blocking.
    /// </summary>
    public struct ShieldAction : IComponentData
    {
        /// <summary>
        /// Damage reduction when blocking (0.7 = 70% reduction).
        /// </summary>
        public float BlockDamageReduction;

        /// <summary>
        /// Window for perfect parry in seconds.
        /// </summary>
        public float ParryWindow;

        /// <summary>
        /// Angle of coverage in degrees.
        /// </summary>
        public float BlockAngle;

        /// <summary>
        /// Stamina cost per block.
        /// </summary>
        public float StaminaCostPerBlock;
    }

    /// <summary>
    /// Runtime state for shield.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ShieldState : IComponentData
    {
        /// <summary>
        /// Whether currently blocking.
        /// </summary>
        [GhostField]
        public bool IsBlocking;

        /// <summary>
        /// Time when block started.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float BlockStartTime;

        /// <summary>
        /// Whether parry window is active.
        /// </summary>
        [GhostField]
        public bool ParryActive;

        /// <summary>
        /// When parry window ends.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ParryEndTime;

        /// <summary>
        /// Number of blocks this hold.
        /// </summary>
        [GhostField]
        public int BlocksThisHold;
    }

    #endregion

    #region Bow Components

    /// <summary>
    /// Configuration for bow weapons.
    /// </summary>
    public struct BowAction : IComponentData
    {
        /// <summary>
        /// Time to fully draw the bow (seconds).
        /// </summary>
        public float DrawTime;

        /// <summary>
        /// Damage at minimum draw.
        /// </summary>
        public float BaseDamage;

        /// <summary>
        /// Damage at full draw.
        /// </summary>
        public float MaxDamage;

        /// <summary>
        /// Arrow speed at full draw.
        /// </summary>
        public float ProjectileSpeed;

        /// <summary>
        /// Prefab index for arrow projectile.
        /// </summary>
        public int ProjectilePrefabIndex;
    }

    /// <summary>
    /// Runtime state for bow weapons.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct BowState : IComponentData
    {
        /// <summary>
        /// Whether currently drawing the bow (left-click held).
        /// </summary>
        [GhostField]
        public bool IsDrawing;

        /// <summary>
        /// Whether bow is fully drawn.
        /// </summary>
        [GhostField]
        public bool IsFullyDrawn;

        /// <summary>
        /// Current draw time progress.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float CurrentDrawTime;

        /// <summary>
        /// Draw progress normalized (0-1).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float DrawProgress;

        /// <summary>
        /// Whether aiming (right-click held).
        /// </summary>
        [GhostField]
        public bool IsAiming;

        /// <summary>
        /// Time since arrow was released (for animation).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TimeSinceRelease;

        /// <summary>
        /// Whether an arrow was just released this frame.
        /// </summary>
        [GhostField]
        public bool JustReleased;

        /// <summary>
        /// Counter for how many consecutive ticks StartUse has been false.
        /// Used for debouncing to prevent false releases from input flickering.
        /// </summary>
        [GhostField]
        public int ReleaseDebounceCounter;

        /// <summary>
        /// Time in seconds that StartUse has been false.
        /// Used for time-based debouncing (more reliable than tick counting).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ReleaseDebounceTime;

        /// <summary>
        /// Prevents multiple release events from firing.
        /// Reset to false when a new draw action begins.
        /// </summary>
        [GhostField]
        public bool HasReleasedThisAction;

        /// <summary>
        /// The network tick value when release occurred.
        /// Used to prevent duplicate release events during prediction rollback.
        /// </summary>
        [GhostField]
        public uint ReleaseTickValue;
    }

    #endregion

    #region Channel Components

    /// <summary>
    /// Configuration for channeled abilities (healing beam, drain life, etc.).
    /// </summary>
    public struct ChannelAction : IComponentData
    {
        /// <summary>
        /// How often to apply the effect (seconds between ticks).
        /// </summary>
        public float TickInterval;

        /// <summary>
        /// Resource cost per tick (mana/stamina).
        /// </summary>
        public float ResourcePerTick;

        /// <summary>
        /// Effect magnitude per tick (damage for offensive, heal for supportive).
        /// </summary>
        public float EffectPerTick;

        /// <summary>
        /// Maximum channel duration (0 = unlimited until resource depleted).
        /// </summary>
        public float MaxChannelTime;

        /// <summary>
        /// Range of the channel effect.
        /// </summary>
        public float Range;

        /// <summary>
        /// Whether this channel heals (true) or damages (false).
        /// </summary>
        public bool IsHealing;

        /// <summary>
        /// VFX prefab index for the beam/stream effect.
        /// </summary>
        public int BeamVfxIndex;

        /// <summary>
        /// EPIC 16.8: Which resource type ResourcePerTick deducts from.
        /// None = no resource drain (backward compatible).
        /// </summary>
        public ResourceType ChannelResourceType;
    }

    /// <summary>
    /// Runtime state for channeled abilities.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ChannelState : IComponentData
    {
        /// <summary>
        /// Whether currently channeling.
        /// </summary>
        [GhostField]
        public bool IsChanneling;

        /// <summary>
        /// Total time spent channeling this session.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ChannelTime;

        /// <summary>
        /// Time since last tick applied.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float TimeSinceTick;

        /// <summary>
        /// Number of ticks applied this session.
        /// </summary>
        [GhostField]
        public int TickCount;

        /// <summary>
        /// Current target entity (for targeted channels).
        /// </summary>
        [GhostField]
        public Entity CurrentTarget;

        /// <summary>
        /// Whether channel just started this frame.
        /// </summary>
        [GhostField]
        public bool JustStarted;

        /// <summary>
        /// Whether channel just ended this frame.
        /// </summary>
        [GhostField]
        public bool JustEnded;
    }

    #endregion

    #region Aim Assist

    /// <summary>
    /// Aim assist configuration.
    /// </summary>
    public struct AimAssist : IComponentData
    {
        /// <summary>
        /// Strength of aim assist (0-1).
        /// </summary>
        public float Strength;

        /// <summary>
        /// Range to detect targets.
        /// </summary>
        public float Range;

        /// <summary>
        /// Cone angle for target detection.
        /// </summary>
        public float ConeAngle;

        /// <summary>
        /// How much aim snaps to targets.
        /// </summary>
        public float Magnetism;
    }

    #endregion

    #region Projectile Components

    /// <summary>
    /// Core projectile data.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct Projectile : IComponentData
    {
        /// <summary>
        /// Damage on impact.
        /// </summary>
        [GhostField]
        public float Damage;

        /// <summary>
        /// Explosion radius (0 = no explosion).
        /// </summary>
        public float ExplosionRadius;

        /// <summary>
        /// Maximum lifetime.
        /// </summary>
        public float Lifetime;

        /// <summary>
        /// Time alive.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ElapsedTime;

        /// <summary>
        /// Type of projectile.
        /// </summary>
        [GhostField]
        public ProjectileType Type;

        /// <summary>
        /// Entity that fired this projectile.
        /// </summary>
        [GhostField]
        public Entity Owner;
    }

    /// <summary>
    /// Projectile movement physics.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ProjectileMovement : IComponentData
    {
        /// <summary>
        /// Current velocity.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float3 Velocity;

        /// <summary>
        /// Gravity multiplier.
        /// </summary>
        public float Gravity;

        /// <summary>
        /// Air drag.
        /// </summary>
        public float Drag;

        /// <summary>
        /// Whether gravity affects this projectile.
        /// </summary>
        public bool HasGravity;
    }

    /// <summary>
    /// Projectile impact behavior.
    /// </summary>
    public struct ProjectileImpact : IComponentData
    {
        /// <summary>
        /// Direct hit damage.
        /// </summary>
        public float Damage;

        /// <summary>
        /// Area damage radius.
        /// </summary>
        public float ImpactRadius;

        /// <summary>
        /// Whether to explode on impact.
        /// </summary>
        public bool ExplodeOnImpact;

        /// <summary>
        /// Whether to bounce on impact.
        /// </summary>
        public bool BounceOnImpact;

        /// <summary>
        /// Maximum bounces before destruction.
        /// </summary>
        public int MaxBounces;

        /// <summary>
        /// Current bounce count.
        /// </summary>
        public int CurrentBounces;
    }

    /// <summary>
    /// Tag for projectiles that have impacted.
    /// </summary>
    public struct ProjectileImpacted : IComponentData
    {
        public float3 ImpactPoint;
        public float3 ImpactNormal;
        public Entity HitEntity;
    }

    #endregion
}
