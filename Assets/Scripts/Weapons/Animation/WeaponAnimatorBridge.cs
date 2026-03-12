using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Weapons.Animation
{
    /// <summary>
    /// Bridge component that drives weapon animator parameters from ECS state.
    ///
    /// Place this on the weapon model's GameObject alongside the Animator.
    /// The WeaponAnimatorBridgeSystem reads ECS weapon state and calls
    /// ApplyWeaponState() each frame to update animator parameters.
    ///
    /// This is the opposite direction from OpsiveWeaponAnimationEventRelay:
    /// - OpsiveWeaponAnimationEventRelay: Animation Events → ECS
    /// - WeaponAnimatorBridge: ECS State → Animator Parameters
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class WeaponAnimatorBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;

        [Header("Shootable Parameters")]
        [SerializeField] private string paramIsFiring = "IsFiring";
        [SerializeField] private string paramIsReloading = "IsReloading";
        [SerializeField] private string paramReloadProgress = "ReloadProgress";
        [SerializeField] private string paramFireTrigger = "Fire";
        [SerializeField] private string paramReloadTrigger = "Reload";
        [SerializeField] private string paramAmmoCount = "AmmoCount";
        [SerializeField] private string paramIsEmpty = "IsEmpty";

        [Header("Melee Parameters")]
        [SerializeField] private string paramIsAttacking = "IsAttacking";
        [SerializeField] private string paramAttackTrigger = "Attack";
        [SerializeField] private string paramComboIndex = "ComboIndex";

        [Header("Throwable Parameters")]
        [SerializeField] private string paramIsCharging = "IsCharging";
        [SerializeField] private string paramChargeProgress = "ChargeProgress";
        [SerializeField] private string paramThrowTrigger = "Throw";

        [Header("Shield Parameters")]
        [SerializeField] private string paramIsBlocking = "IsBlocking";
        [SerializeField] private string paramBlockTrigger = "Block";
        [SerializeField] private string paramParryTrigger = "Parry";

        [Header("Common Parameters")]
        [SerializeField] private string paramIsEquipped = "IsEquipped";
        [SerializeField] private string paramEquipTrigger = "Equip";
        [SerializeField] private string paramUnequipTrigger = "Unequip";

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // Cached parameter hashes
        private int h_IsFiring;
        private int h_IsReloading;
        private int h_ReloadProgress;
        private int h_FireTrigger;
        private int h_ReloadTrigger;
        private int h_AmmoCount;
        private int h_IsEmpty;

        private int h_IsAttacking;
        private int h_AttackTrigger;
        private int h_ComboIndex;

        private int h_IsCharging;
        private int h_ChargeProgress;
        private int h_ThrowTrigger;

        private int h_IsBlocking;
        private int h_BlockTrigger;
        private int h_ParryTrigger;

        private int h_IsEquipped;
        private int h_EquipTrigger;
        private int h_UnequipTrigger;

        // State tracking for trigger detection
        private bool _wasFiring;
        private bool _wasReloading;
        private bool _wasAttacking;
        private bool _wasCharging;
        private bool _wasBlocking;
        private bool _wasEquipped;
        private int _lastComboIndex;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            CacheParameterHashes();
        }

        private void OnValidate()
        {
            CacheParameterHashes();
        }

        private void CacheParameterHashes()
        {
            // Cache all parameter hashes for performance
            h_IsFiring = !string.IsNullOrEmpty(paramIsFiring) ? Animator.StringToHash(paramIsFiring) : 0;
            h_IsReloading = !string.IsNullOrEmpty(paramIsReloading) ? Animator.StringToHash(paramIsReloading) : 0;
            h_ReloadProgress = !string.IsNullOrEmpty(paramReloadProgress) ? Animator.StringToHash(paramReloadProgress) : 0;
            h_FireTrigger = !string.IsNullOrEmpty(paramFireTrigger) ? Animator.StringToHash(paramFireTrigger) : 0;
            h_ReloadTrigger = !string.IsNullOrEmpty(paramReloadTrigger) ? Animator.StringToHash(paramReloadTrigger) : 0;
            h_AmmoCount = !string.IsNullOrEmpty(paramAmmoCount) ? Animator.StringToHash(paramAmmoCount) : 0;
            h_IsEmpty = !string.IsNullOrEmpty(paramIsEmpty) ? Animator.StringToHash(paramIsEmpty) : 0;

            h_IsAttacking = !string.IsNullOrEmpty(paramIsAttacking) ? Animator.StringToHash(paramIsAttacking) : 0;
            h_AttackTrigger = !string.IsNullOrEmpty(paramAttackTrigger) ? Animator.StringToHash(paramAttackTrigger) : 0;
            h_ComboIndex = !string.IsNullOrEmpty(paramComboIndex) ? Animator.StringToHash(paramComboIndex) : 0;

            h_IsCharging = !string.IsNullOrEmpty(paramIsCharging) ? Animator.StringToHash(paramIsCharging) : 0;
            h_ChargeProgress = !string.IsNullOrEmpty(paramChargeProgress) ? Animator.StringToHash(paramChargeProgress) : 0;
            h_ThrowTrigger = !string.IsNullOrEmpty(paramThrowTrigger) ? Animator.StringToHash(paramThrowTrigger) : 0;

            h_IsBlocking = !string.IsNullOrEmpty(paramIsBlocking) ? Animator.StringToHash(paramIsBlocking) : 0;
            h_BlockTrigger = !string.IsNullOrEmpty(paramBlockTrigger) ? Animator.StringToHash(paramBlockTrigger) : 0;
            h_ParryTrigger = !string.IsNullOrEmpty(paramParryTrigger) ? Animator.StringToHash(paramParryTrigger) : 0;

            h_IsEquipped = !string.IsNullOrEmpty(paramIsEquipped) ? Animator.StringToHash(paramIsEquipped) : 0;
            h_EquipTrigger = !string.IsNullOrEmpty(paramEquipTrigger) ? Animator.StringToHash(paramEquipTrigger) : 0;
            h_UnequipTrigger = !string.IsNullOrEmpty(paramUnequipTrigger) ? Animator.StringToHash(paramUnequipTrigger) : 0;
        }

        /// <summary>
        /// Apply weapon state from ECS to animator.
        /// Called by WeaponAnimatorBridgeSystem each frame.
        /// </summary>
        public void ApplyWeaponState(WeaponAnimationState state)
        {
            if (animator == null) return;

            // ====================================================
            // SHOOTABLE ANIMATIONS
            // ====================================================

            // Firing state
            if (h_IsFiring != 0)
                animator.SetBool(h_IsFiring, state.IsFiring);

            // Fire trigger (on rising edge)
            if (h_FireTrigger != 0 && state.IsFiring && !_wasFiring)
            {
                animator.SetTrigger(h_FireTrigger);
                if (debugLogging) Debug.Log("[WeaponAnimBridge] Fire trigger set");
            }
            _wasFiring = state.IsFiring;

            // Reload state
            if (h_IsReloading != 0)
                animator.SetBool(h_IsReloading, state.IsReloading);

            // Reload trigger (on rising edge)
            if (h_ReloadTrigger != 0 && state.IsReloading && !_wasReloading)
            {
                animator.SetTrigger(h_ReloadTrigger);
                if (debugLogging) Debug.Log("[WeaponAnimBridge] Reload trigger set");
            }
            _wasReloading = state.IsReloading;

            // Reload progress
            if (h_ReloadProgress != 0)
                animator.SetFloat(h_ReloadProgress, state.ReloadProgress);

            // Ammo count
            if (h_AmmoCount != 0)
                animator.SetInteger(h_AmmoCount, state.AmmoCount);

            // Empty state
            if (h_IsEmpty != 0)
                animator.SetBool(h_IsEmpty, state.AmmoCount <= 0);

            // ====================================================
            // MELEE ANIMATIONS
            // ====================================================

            if (h_IsAttacking != 0)
                animator.SetBool(h_IsAttacking, state.IsAttacking);

            // Attack trigger (on rising edge)
            if (h_AttackTrigger != 0 && state.IsAttacking && !_wasAttacking)
            {
                animator.SetTrigger(h_AttackTrigger);
                if (debugLogging) Debug.Log("[WeaponAnimBridge] Attack trigger set");
            }
            _wasAttacking = state.IsAttacking;

            // Combo index
            if (h_ComboIndex != 0)
            {
                animator.SetInteger(h_ComboIndex, state.ComboIndex);

                // Trigger combo transition on index change
                if (state.ComboIndex != _lastComboIndex && state.IsAttacking)
                {
                    if (debugLogging) Debug.Log($"[WeaponAnimBridge] Combo changed: {_lastComboIndex} -> {state.ComboIndex}");
                }
                _lastComboIndex = state.ComboIndex;
            }

            // ====================================================
            // THROWABLE ANIMATIONS
            // ====================================================

            if (h_IsCharging != 0)
                animator.SetBool(h_IsCharging, state.IsCharging);

            if (h_ChargeProgress != 0)
                animator.SetFloat(h_ChargeProgress, state.ChargeProgress);

            // Throw trigger (on falling edge of charging - release)
            if (h_ThrowTrigger != 0 && !state.IsCharging && _wasCharging)
            {
                animator.SetTrigger(h_ThrowTrigger);
                if (debugLogging) Debug.Log("[WeaponAnimBridge] Throw trigger set");
            }
            _wasCharging = state.IsCharging;

            // ====================================================
            // SHIELD ANIMATIONS
            // ====================================================

            if (h_IsBlocking != 0)
                animator.SetBool(h_IsBlocking, state.IsBlocking);

            // Block trigger (on rising edge)
            if (h_BlockTrigger != 0 && state.IsBlocking && !_wasBlocking)
            {
                animator.SetTrigger(h_BlockTrigger);
                if (debugLogging) Debug.Log("[WeaponAnimBridge] Block trigger set");
            }
            _wasBlocking = state.IsBlocking;

            // Parry trigger
            if (h_ParryTrigger != 0 && state.ParryTriggered)
            {
                animator.SetTrigger(h_ParryTrigger);
                if (debugLogging) Debug.Log("[WeaponAnimBridge] Parry trigger set");
            }

            // ====================================================
            // EQUIP ANIMATIONS
            // ====================================================

            if (h_IsEquipped != 0)
                animator.SetBool(h_IsEquipped, state.IsEquipped);

            // Equip trigger (on rising edge)
            if (h_EquipTrigger != 0 && state.IsEquipped && !_wasEquipped)
            {
                animator.SetTrigger(h_EquipTrigger);
                if (debugLogging) Debug.Log("[WeaponAnimBridge] Equip trigger set");
            }
            // Unequip trigger (on falling edge)
            else if (h_UnequipTrigger != 0 && !state.IsEquipped && _wasEquipped)
            {
                animator.SetTrigger(h_UnequipTrigger);
                if (debugLogging) Debug.Log("[WeaponAnimBridge] Unequip trigger set");
            }
            _wasEquipped = state.IsEquipped;
        }

        /// <summary>
        /// Force reset all animator state.
        /// Call when weapon is switched or dropped.
        /// </summary>
        public void ResetState()
        {
            _wasFiring = false;
            _wasReloading = false;
            _wasAttacking = false;
            _wasCharging = false;
            _wasBlocking = false;
            _wasEquipped = false;
            _lastComboIndex = 0;

            if (animator != null)
            {
                // Reset all triggers
                if (h_FireTrigger != 0) animator.ResetTrigger(h_FireTrigger);
                if (h_ReloadTrigger != 0) animator.ResetTrigger(h_ReloadTrigger);
                if (h_AttackTrigger != 0) animator.ResetTrigger(h_AttackTrigger);
                if (h_ThrowTrigger != 0) animator.ResetTrigger(h_ThrowTrigger);
                if (h_BlockTrigger != 0) animator.ResetTrigger(h_BlockTrigger);
                if (h_ParryTrigger != 0) animator.ResetTrigger(h_ParryTrigger);
                if (h_EquipTrigger != 0) animator.ResetTrigger(h_EquipTrigger);
                if (h_UnequipTrigger != 0) animator.ResetTrigger(h_UnequipTrigger);
            }
        }
    }

    /// <summary>
    /// State data passed from ECS to weapon animator bridge.
    /// </summary>
    public struct WeaponAnimationState
    {
        // Shootable
        public bool IsFiring;
        public bool IsReloading;
        public float ReloadProgress;
        public int AmmoCount;

        // Melee
        public bool IsAttacking;
        public int ComboIndex;

        // Throwable
        public bool IsCharging;
        public float ChargeProgress;

        // Shield
        public bool IsBlocking;
        public bool ParryTriggered;

        // Common
        public bool IsEquipped;
    }
}
