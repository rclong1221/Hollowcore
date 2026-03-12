using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;
using DIG.Items;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Client-side system that bridges ECS weapon state to weapon-specific animators.
    /// Finds WeaponAnimatorBridge components on weapon models and calls ApplyWeaponState().
    /// 
    /// This system works alongside WeaponEquipVisualBridge:
    /// - WeaponEquipVisualBridge drives the PLAYER animator (Slot0ItemStateIndex, etc.)
    /// - WeaponAnimatorBridgeSystem drives WEAPON-SPECIFIC animators on weapon models
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class WeaponAnimatorBridgeSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _presentationSystem;
        
        protected override void OnCreate()
        {
            RequireForUpdate<EquippedItemElement>();
            _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
        }

        protected override void OnUpdate()
        {
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null)
                    return;
            }
            
            // Find all players with active weapons
            foreach (var (activeSlotIndex, equippedBuffer, entity) in 
                     SystemAPI.Query<RefRO<ActiveSlotIndex>, DynamicBuffer<EquippedItemElement>>()
                     .WithEntityAccess())
            {
                int slotIndex = activeSlotIndex.ValueRO.Value;
                if (slotIndex < 0 || slotIndex >= equippedBuffer.Length)
                    continue;

                var equippedElement = equippedBuffer[slotIndex];
                Entity weaponEntity = equippedElement.ItemEntity;
                int quickSlot = equippedElement.QuickSlot; // Not strictly needed but kept for parity info if needed

                // If weapon entity is null, we can't drive animation
                if (weaponEntity == Entity.Null) continue;

                // Build weapon animation state from ECS
                var animState = new Animation.WeaponAnimationState();

                // Shootable state
                if (SystemAPI.HasComponent<WeaponFireState>(weaponEntity))
                {
                    var fireState = SystemAPI.GetComponent<WeaponFireState>(weaponEntity);
                    animState.IsFiring = fireState.IsFiring;
                }

                if (SystemAPI.HasComponent<WeaponAmmoState>(weaponEntity))
                {
                    var ammoState = SystemAPI.GetComponent<WeaponAmmoState>(weaponEntity);
                    animState.IsReloading = ammoState.IsReloading;
                    animState.ReloadProgress = ammoState.ReloadProgress;
                    animState.AmmoCount = ammoState.AmmoCount;
                }

                // Melee state
                if (SystemAPI.HasComponent<MeleeState>(weaponEntity))
                {
                    var meleeState = SystemAPI.GetComponent<MeleeState>(weaponEntity);
                    animState.IsAttacking = meleeState.IsAttacking;
                    animState.ComboIndex = meleeState.CurrentCombo;
                }

                // Throwable state
                if (SystemAPI.HasComponent<ThrowableState>(weaponEntity))
                {
                    var throwState = SystemAPI.GetComponent<ThrowableState>(weaponEntity);
                    animState.IsCharging = throwState.IsCharging;
                    animState.ChargeProgress = throwState.ChargeProgress;
                }

                // Shield state
                if (SystemAPI.HasComponent<ShieldState>(weaponEntity))
                {
                    var shieldState = SystemAPI.GetComponent<ShieldState>(weaponEntity);
                    animState.IsBlocking = shieldState.IsBlocking;
                    animState.ParryTriggered = shieldState.ParryActive;
                }

                // Equip state
                if (SystemAPI.HasComponent<CharacterItem>(weaponEntity))
                {
                    var charItem = SystemAPI.GetComponent<CharacterItem>(weaponEntity);
                    animState.IsEquipped = charItem.State == ItemState.Equipped;
                }

                // Get presentation GameObject and find WeaponAnimatorBridge components
                var presentation = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentation != null)
                {
                    var bridges = presentation.GetComponentsInChildren<Animation.WeaponAnimatorBridge>(true);
                    foreach (var bridge in bridges)
                    {
                        if (bridge != null && bridge.isActiveAndEnabled)
                        {
                            bridge.ApplyWeaponState(animState);
                        }
                    }
                }
            }
        }
    }
}
