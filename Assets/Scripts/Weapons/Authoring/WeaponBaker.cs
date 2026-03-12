using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Items;
using DIG.Items.Authoring;
using DIG.Items.Definitions;
using DIG.Weapons;

namespace DIG.Weapons.Authoring
{
    /// <summary>
    /// Advanced Baker for Weapons.
    /// Handles baking of logic components and creating optimized entities for Client/Server.
    /// (EPIC 14.16 Phase 3)
    /// </summary>
    public class WeaponBaker : Baker<WeaponAuthoring>
    {
        public override void Bake(WeaponAuthoring authoring)
        {
            // Check if there are multiple WeaponAuthoring on the same GameObject
            // If so, only bake from the FIRST one to avoid duplicates
            var allWeaponAuthorings = authoring.GetComponents<WeaponAuthoring>();
            if (allWeaponAuthorings.Length > 1)
            {
                Debug.LogWarning($"[WEAPON_BAKER] WARNING: {authoring.name} has {allWeaponAuthorings.Length} WeaponAuthoring components! Remove duplicates from the prefab.");
                // Find first one - only that should bake
                if (allWeaponAuthorings[0] != authoring)
                {
                    Debug.Log($"[WEAPON_BAKER] Skipping duplicate WeaponAuthoring on {authoring.name}");
                    return;
                }
            }

            // Prevent duplicate baking if a PARENT GameObject has WeaponAuthoring
            Transform parent = authoring.transform.parent;
            while (parent != null)
            {
                if (parent.GetComponent<WeaponAuthoring>() != null)
                {
                    Debug.Log($"[WEAPON_BAKER] Skipping {authoring.name} - parent has WeaponAuthoring");
                    return;
                }
                parent = parent.parent;
            }

            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Debug: Log what type is being baked
            Debug.Log($"[WEAPON_BAKER] Baking {authoring.name} Type={authoring.Type}");

            // 1. Bake Core Action Components
            BakeCoreAction(entity, authoring);

            // 2. Bake Weapon-Type Specifics
            BakeTypeSpecifics(entity, authoring);

            // 2.5. EPIC 15.29: Bake Damage Profile (base element)
            AddComponent(entity, new DamageProfile
            {
                Element = authoring.DamageElement
            });

            // 2.6. EPIC 15.29: Bake Weapon Modifiers (on-hit effects)
            if (authoring.weaponModifiers != null && authoring.weaponModifiers.Count > 0)
            {
                var modBuffer = AddBuffer<WeaponModifier>(entity);
                foreach (var mod in authoring.weaponModifiers)
                {
                    modBuffer.Add(new WeaponModifier
                    {
                        Type = mod.Type,
                        Source = mod.Source,
                        Element = mod.Element,
                        BonusDamage = mod.BonusDamage,
                        Chance = mod.Chance,
                        Duration = mod.Duration,
                        Intensity = mod.Intensity,
                        Radius = mod.Radius,
                        Force = mod.Force
                    });
                }
            }

            // 3. Bake Aim Assist
            if (authoring.EnableAimAssist)
            {
                AddComponent(entity, new AimAssist
                {
                    Strength = authoring.AimAssistStrength,
                    Range = authoring.AimAssistRange,
                    ConeAngle = authoring.AimAssistConeAngle,
                    Magnetism = authoring.AimAssistMagnetism
                });
            }

            // 4. Bake Data-Driven Configuration (Combo Data)
            if (authoring.Config != null)
            {
                BakeWeaponConfig(entity, authoring.Config);
            }
        }

        private void BakeCoreAction(Entity entity, WeaponAuthoring authoring)
        {
            var actionType = authoring.Type switch
            {
                WeaponType.Shootable => UsableActionType.Shootable,
                WeaponType.Melee => UsableActionType.Melee,
                WeaponType.Throwable => UsableActionType.Throwable,
                WeaponType.Shield => UsableActionType.Shield,
                WeaponType.Bow => UsableActionType.Bow,
                WeaponType.Channel => UsableActionType.Channel,
                _ => UsableActionType.None
            };

            AddComponent(entity, new UsableAction
            {
                ActionType = actionType,
                AnimatorItemID = authoring.AnimatorItemID,
                CanUse = true,
                IsUsing = false,
                UseTime = 0f,
                CooldownRemaining = 0f,
                AmmoCount = authoring.StartingAmmo,
                ClipSize = authoring.ClipSize,
                ReserveAmmo = authoring.ReserveAmmo
            });

            AddComponent(entity, new UseRequest());
            AddComponent(entity, new WeaponAimState());

            // Character item - required for slot/equip tracking
            // Use GetComponents logic to avoid duplication if ItemAuthoring exists
            var itemAuthoring = GetComponent<ItemAuthoring>(); 
            if (itemAuthoring == null)
            {
                AddComponent(entity, new CharacterItem
                {
                    ItemTypeId = authoring.AnimatorItemID,
                    SlotId = -1,
                    OwnerEntity = Entity.Null,
                    State = ItemState.Unequipped,
                    StateTime = 0f
                });
            }
        }

        private void BakeTypeSpecifics(Entity entity, WeaponAuthoring authoring)
        {
            switch (authoring.Type)
            {
                case WeaponType.Shootable:
                    BakeShootable(entity, authoring);
                    break;
                case WeaponType.Melee:
                    BakeMelee(entity, authoring);
                    break;
                case WeaponType.Throwable:
                    BakeThrowable(entity, authoring);
                    break;
                case WeaponType.Shield:
                    BakeShield(entity, authoring);
                    break;
                case WeaponType.Bow:
                    BakeBow(entity, authoring);
                    break;
                case WeaponType.Channel:
                    BakeChannel(entity, authoring);
                    break;
            }
        }

        private void BakeShootable(Entity entity, WeaponAuthoring authoring)
        {
            AddComponent(entity, new WeaponFireComponent
            {
                FireRate = authoring.FireRate,
                Damage = authoring.Damage,
                Range = authoring.Range,
                IsAutomatic = authoring.IsAutomatic,
                UseHitscan = authoring.UseHitscan
            });
            AddComponent(entity, new WeaponFireState());

            AddComponent(entity, new WeaponRecoilComponent
            {
                RecoilAmount = authoring.RecoilAmount,
                RecoilRecovery = authoring.RecoilRecovery,
                Randomness = new float2(0.2f, 0.2f)
            });
            AddComponent(entity, new WeaponRecoilState());

            AddComponent(entity, new WeaponSpreadComponent
            {
                BaseSpread = authoring.SpreadAngle,
                MaxSpread = authoring.SpreadAngle * 2.5f,
                SpreadIncrement = 0.5f,
                SpreadRecovery = 5.0f,
                MovementMultiplier = 1.5f
            });
            AddComponent(entity, new WeaponSpreadState());

            AddComponent(entity, new WeaponAmmoComponent
            {
                ClipSize = authoring.ClipSize,
                ReloadTime = authoring.ReloadTime,
                AutoReload = true
            });
            AddComponent(entity, new WeaponAmmoState
            {
                AmmoCount = authoring.StartingAmmo,
                ReserveAmmo = authoring.ReserveAmmo
            });
        }

        private void BakeMelee(Entity entity, WeaponAuthoring authoring)
        {
            AddComponent(entity, new MeleeAction
            {
                Damage = authoring.MeleeDamage,
                Range = authoring.MeleeRange,
                AttackSpeed = authoring.AttackSpeed,
                HitboxActiveStart = authoring.HitboxActiveStart,
                HitboxActiveEnd = authoring.HitboxActiveEnd,
                ComboCount = authoring.ComboCount,
                ComboWindow = authoring.ComboWindow
            });
            AddComponent(entity, new MeleeState());
            AddComponent(entity, new MeleeHitbox
            {
                Offset = authoring.HitboxOffset,
                Size = authoring.HitboxSize
            });

            // Unified processing
            AddComponent(entity, new WeaponFireComponent
            {
                FireRate = authoring.AttackSpeed,
                Damage = authoring.MeleeDamage,
                Range = authoring.MeleeRange,
                IsAutomatic = false,
                UseHitscan = false
            });
            AddComponent(entity, new WeaponFireState());

            // Bake inline combo data if provided (fallback when no WeaponConfig)
            if (authoring.Config == null && authoring.comboData != null && authoring.comboData.Count > 0)
            {
                var comboBuffer = AddBuffer<ComboData>(entity);
                foreach (var step in authoring.comboData)
                {
                    comboBuffer.Add(new ComboData
                    {
                        AnimatorSubStateIndex = step.AnimatorSubStateIndex,
                        Duration = step.Duration,
                        InputWindowStart = step.InputWindowStart,
                        InputWindowEnd = step.InputWindowEnd,
                        DamageMultiplier = step.DamageMultiplier,
                        KnockbackForce = step.KnockbackForce
                    });
                }
            }
        }

        private void BakeThrowable(Entity entity, WeaponAuthoring authoring)
        {
            // Get prefab entity if assigned
            Entity prefabEntity = Entity.Null;
            if (authoring.ThrowableProjectilePrefab != null)
            {
                prefabEntity = GetEntity(authoring.ThrowableProjectilePrefab, TransformUsageFlags.Dynamic);
            }

            // EPIC 15.13: ProjectileLifetime and ProjectileDamage are now on the projectile prefab
            AddComponent(entity, new ThrowableAction
            {
                MinForce = authoring.MinThrowForce,
                MaxForce = authoring.MaxThrowForce,
                ChargeTime = authoring.ChargeTime,
                ThrowArc = authoring.ThrowArc,
                ProjectilePrefab = prefabEntity
            });
            AddComponent(entity, new ThrowableState());
        }

        private void BakeShield(Entity entity, WeaponAuthoring authoring)
        {
            AddComponent(entity, new ShieldAction
            {
                BlockDamageReduction = authoring.BlockDamageReduction,
                ParryWindow = authoring.ParryWindow,
                BlockAngle = authoring.BlockAngle,
                StaminaCostPerBlock = authoring.StaminaCostPerBlock
            });
            AddComponent(entity, new ShieldState());
        }

        private void BakeBow(Entity entity, WeaponAuthoring authoring)
        {
            AddComponent(entity, new BowAction
            {
                DrawTime = authoring.BowDrawTime,
                BaseDamage = authoring.BowBaseDamage,
                MaxDamage = authoring.BowMaxDamage,
                ProjectileSpeed = authoring.BowProjectileSpeed,
                ProjectilePrefabIndex = authoring.BowProjectilePrefabIndex
            });
            AddComponent(entity, new BowState());
        }

        private void BakeChannel(Entity entity, WeaponAuthoring authoring)
        {
            AddComponent(entity, new ChannelAction
            {
                TickInterval = authoring.ChannelTickInterval,
                ResourcePerTick = authoring.ChannelResourcePerTick,
                EffectPerTick = authoring.ChannelEffectPerTick,
                MaxChannelTime = authoring.ChannelMaxTime,
                Range = authoring.ChannelRange,
                IsHealing = authoring.ChannelIsHealing,
                BeamVfxIndex = authoring.ChannelBeamVfxIndex
            });
            AddComponent(entity, new ChannelState());
        }

        private void BakeWeaponConfig(Entity entity, WeaponConfig config)
        {
            var comboBuffer = AddBuffer<ComboData>(entity);
            foreach (var step in config.ComboChain)
            {
                comboBuffer.Add(new ComboData
                {
                    AnimatorSubStateIndex = step.AnimatorSubStateIndex,
                    Duration = step.Duration,
                    InputWindowStart = step.InputWindowStart,
                    InputWindowEnd = step.InputWindowEnd,
                    DamageMultiplier = step.DamageMultiplier,
                    KnockbackForce = step.KnockbackForce
                });
            }
        }
    }
}
