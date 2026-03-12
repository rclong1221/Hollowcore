using Unity.Entities;
using Unity.NetCode;
using DIG.Weapons.Animation;
using DIG.Items;
using UnityEngine;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Consumes animation events from the MonoBehaviour bridge and updates ECS weapon state.
    ///
    /// This system bridges OPSIVE's animation event system (which fires from MonoBehaviour)
    /// to ECS by polling the static WeaponAnimationEvents queue.
    ///
    /// Event handling:
    /// - Fire: Trigger fire effects, update fire state
    /// - ReloadStart: Begin reload process
    /// - ReloadInsertAmmo: Actually transfer ammo from reserve to clip
    /// - ReloadComplete: End reload process, weapon ready
    /// - MeleeHitFrame: Activate hitbox for damage detection
    /// - ThrowRelease: Spawn projectile at this exact frame
    /// - EquipComplete: Mark item as fully equipped and usable
    /// - etc.
    ///
    /// Runs on both client and server to keep animation state synchronized.
    /// Server is authoritative for gameplay effects (damage, ammo).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(WeaponFireSystem))]
    public partial class WeaponAnimationEventSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Process all pending animation events
            while (WeaponAnimationEvents.TryDequeueEvent(out var weaponEvent))
            {
                ProcessWeaponEvent(weaponEvent);
            }
        }

        private void ProcessWeaponEvent(WeaponAnimationEvents.WeaponEvent weaponEvent)
        {
            bool isServer = World.Unmanaged.IsServer();

            switch (weaponEvent.Type)
            {
                // ============================================
                // SHOOTABLE EVENTS
                // ============================================

                case WeaponAnimationEvents.EventType.Fire:
                    // Animation confirms fire - trigger muzzle flash, sound, etc.
                    // The actual firing logic happens in WeaponFireSystem based on input
                    // This event is for syncing presentation with animation
                    ProcessFireEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.FireComplete:
                    // Fire animation done - for semi-auto, this resets the trigger
                    ProcessFireCompleteEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.ReloadStart:
                    // Reload animation started - lock weapon from firing
                    ProcessReloadStartEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.ReloadInsertAmmo:
                    // Magazine inserted - THIS is when ammo actually transfers
                    // Allows for reload interruption before this point
                    ProcessReloadInsertAmmoEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.ReloadComplete:
                    // Reload animation complete - weapon ready to fire
                    ProcessReloadCompleteEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.DryFire:
                    // Click with no ammo - play click sound
                    ProcessDryFireEvent(weaponEvent, isServer);
                    break;

                // ============================================
                // MELEE EVENTS
                // ============================================

                case WeaponAnimationEvents.EventType.MeleeStart:
                    // Swing started - may want to disable movement
                    ProcessMeleeStartEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.MeleeHitFrame:
                    // CRITICAL: This is the damage frame
                    // Activate hitbox, check for hits
                    ProcessMeleeHitFrameEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.MeleeComplete:
                    // Swing complete - deactivate hitbox
                    ProcessMeleeCompleteEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.MeleeCombo:
                    // Combo transition point - can chain next attack
                    ProcessMeleeComboEvent(weaponEvent, isServer);
                    break;

                // ============================================
                // THROWABLE EVENTS
                // ============================================

                case WeaponAnimationEvents.EventType.ThrowChargeStart:
                    // Started charging throw
                    ProcessThrowChargeStartEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.ThrowRelease:
                    // CRITICAL: Spawn projectile at this exact frame
                    ProcessThrowReleaseEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.ThrowComplete:
                    // Throw animation done
                    ProcessThrowCompleteEvent(weaponEvent, isServer);
                    break;

                // ============================================
                // SHIELD EVENTS
                // ============================================

                case WeaponAnimationEvents.EventType.BlockStart:
                    ProcessBlockStartEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.BlockEnd:
                    ProcessBlockEndEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.ParryWindow:
                    // Activate perfect parry window
                    ProcessParryWindowEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.ParryComplete:
                    // Parry window closed
                    ProcessParryCompleteEvent(weaponEvent, isServer);
                    break;

                // ============================================
                // EQUIP/UNEQUIP EVENTS
                // ============================================

                case WeaponAnimationEvents.EventType.EquipStart:
                    ProcessEquipStartEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.EquipComplete:
                    // Weapon fully equipped and ready
                    ProcessEquipCompleteEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.UnequipStart:
                    ProcessUnequipStartEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.UnequipComplete:
                    // Weapon fully unequipped
                    ProcessUnequipCompleteEvent(weaponEvent, isServer);
                    break;

                // ============================================
                // GENERIC EVENTS
                // ============================================

                case WeaponAnimationEvents.EventType.ItemUseStart:
                    ProcessItemUseStartEvent(weaponEvent, isServer);
                    break;

                case WeaponAnimationEvents.EventType.ItemUseComplete:
                    ProcessItemUseCompleteEvent(weaponEvent, isServer);
                    break;
            }
        }

        // ============================================
        // SHOOTABLE EVENT HANDLERS
        // ============================================

        private void ProcessFireEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            // Fire event from animation - used for effects timing
            // Actual bullet/projectile creation is handled by WeaponFireSystem
            #if UNITY_EDITOR
            Debug.Log($"[WeaponAnimEventSystem] Fire event - trigger muzzle flash/sound");
            #endif

            // TODO: Queue fire effect request for presentation system
            // FireEffectRequest could be added to a buffer for VFX system to consume
        }

        private void ProcessFireCompleteEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            // Semi-auto weapons: reset trigger state so next click can fire
            foreach (var (fireState, entity) in SystemAPI.Query<RefRW<WeaponFireState>>().WithEntityAccess())
            {
                ref var state = ref fireState.ValueRW;
                if (state.IsFiring)
                {
                    state.IsFiring = false;
                }
            }
        }

        private void ProcessReloadStartEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            // Lock weapon from firing during reload
            foreach (var (ammoState, action, entity) in
                     SystemAPI.Query<RefRW<WeaponAmmoState>, RefRW<UsableAction>>().WithEntityAccess())
            {
                ref var ammo = ref ammoState.ValueRW;
                ref var act = ref action.ValueRW;

                if (!ammo.IsReloading && ammo.AmmoCount < act.ClipSize && ammo.ReserveAmmo > 0)
                {
                    ammo.IsReloading = true;
                    ammo.ReloadProgress = 0f;
                    act.CanUse = false;

                    #if UNITY_EDITOR
                    Debug.Log($"[SHOOT_DEBUG] [WeaponAnimEventSystem] Reload started - weapon locked. Entity {entity.Index}");
                    #endif
                }
            }
        }

        private void ProcessReloadInsertAmmoEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            // Run on BOTH client and server for prediction
            // Server state is authoritative and will correct any misprediction via GhostField

            foreach (var (ammoState, ammoConfig, action, entity) in
                     SystemAPI.Query<RefRW<WeaponAmmoState>, RefRO<WeaponAmmoComponent>, RefRO<UsableAction>>().WithEntityAccess())
            {
                ref var ammo = ref ammoState.ValueRW;
                var config = ammoConfig.ValueRO;

                if (ammo.IsReloading)
                {
                    // Calculate how much ammo to add
                    int needed = config.ClipSize - ammo.AmmoCount;
                    int available = ammo.ReserveAmmo;
                    int toAdd = Mathf.Min(needed, available);

                    ammo.AmmoCount += toAdd;
                    ammo.ReserveAmmo -= toAdd;

                    #if UNITY_EDITOR
                    Debug.Log($"[WeaponAnimEventSystem] Ammo inserted: +{toAdd} (now {ammo.AmmoCount}/{config.ClipSize}, reserve: {ammo.ReserveAmmo})");
                    #endif
                }
            }
        }

        private void ProcessReloadCompleteEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
{
    // Reload finished - unlock weapon
    foreach (var (ammoState, ammoConfig, action, entity) in
             SystemAPI.Query<RefRW<WeaponAmmoState>, RefRO<WeaponAmmoComponent>, RefRW<UsableAction>>().WithEntityAccess())
    {
        ref var ammo = ref ammoState.ValueRW;
        var config = ammoConfig.ValueRO;
        ref var act = ref action.ValueRW;

        if (ammo.IsReloading)
        {
            ammo.IsReloading = false;
            ammo.ReloadProgress = 0f; // Reset to 0, not 1
            act.CanUse = true;
            #if UNITY_EDITOR
            UnityEngine.Debug.Log($"[SHOOT_DEBUG] [WeaponAnimEventSystem] ReloadComplete RECEIVED for Entity {entity.Index}. Clearing IsReloading.");
            #endif

            // Server-auth transfer: Ensure ammo is full if Insert event didn't happen
            if (isServer)
            {
                int needed = config.ClipSize - ammo.AmmoCount;
                if (needed > 0 && ammo.ReserveAmmo > 0)
                {
                    int available = UnityEngine.Mathf.Min(needed, ammo.ReserveAmmo);
                    ammo.AmmoCount += available;
                    ammo.ReserveAmmo -= available;
                    #if UNITY_EDITOR
                    UnityEngine.Debug.Log($"[WeaponAnimEventSystem] ReloadComplete transfer: +{available}");
                    #endif
                }
            }

            #if UNITY_EDITOR
            UnityEngine.Debug.Log($"[WeaponAnimEventSystem] Reload complete - weapon ready");
            #endif
        }
    }
}

        private void ProcessDryFireEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            // Play click sound - handled by presentation layer
            #if UNITY_EDITOR
            Debug.Log($"[WeaponAnimEventSystem] Dry fire - click sound");
            #endif
        }

        // ============================================
        // MELEE EVENT HANDLERS
        // ============================================

        private void ProcessMeleeStartEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (meleeState, entity) in SystemAPI.Query<RefRW<MeleeState>>().WithEntityAccess())
            {
                ref var state = ref meleeState.ValueRW;
                state.IsAttacking = true;
                state.AttackTime = 0f;
                state.HasHitThisSwing = false;

                #if UNITY_EDITOR
                Debug.Log($"[WeaponAnimEventSystem] Melee attack started");
                #endif
            }
        }

        private void ProcessMeleeHitFrameEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            // CRITICAL: Activate hitbox for damage detection
            foreach (var (meleeState, hitbox, entity) in
                     SystemAPI.Query<RefRW<MeleeState>, RefRW<MeleeHitbox>>().WithEntityAccess())
            {
                ref var state = ref meleeState.ValueRW;
                ref var box = ref hitbox.ValueRW;

                state.HitboxActive = true;
                box.IsActive = true;

                #if UNITY_EDITOR
                Debug.Log($"[WeaponAnimEventSystem] Melee hitbox ACTIVATED - damage frame");
                #endif
            }
        }

        private void ProcessMeleeCompleteEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (meleeState, hitbox, entity) in
                     SystemAPI.Query<RefRW<MeleeState>, RefRW<MeleeHitbox>>().WithEntityAccess())
            {
                ref var state = ref meleeState.ValueRW;
                ref var box = ref hitbox.ValueRW;

                state.IsAttacking = false;
                state.HitboxActive = false;
                box.IsActive = false;
                state.TimeSinceAttack = 0f;

                #if UNITY_EDITOR
                Debug.Log($"[WeaponAnimEventSystem] Melee attack complete - hitbox deactivated");
                #endif
            }
        }

        private void ProcessMeleeComboEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (meleeState, meleeAction, entity) in
                     SystemAPI.Query<RefRW<MeleeState>, RefRO<MeleeAction>>().WithEntityAccess())
            {
                ref var state = ref meleeState.ValueRW;
                var config = meleeAction.ValueRO;

                // Advance combo if not at max
                if (state.CurrentCombo < config.ComboCount - 1)
                {
                    state.CurrentCombo++;
                    state.HasHitThisSwing = false;

                    #if UNITY_EDITOR
                    Debug.Log($"[WeaponAnimEventSystem] Combo advanced to {state.CurrentCombo + 1}/{config.ComboCount}");
                    #endif
                }
            }
        }

        // ============================================
        // THROWABLE EVENT HANDLERS
        // ============================================

        private void ProcessThrowChargeStartEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (throwState, entity) in SystemAPI.Query<RefRW<ThrowableState>>().WithEntityAccess())
            {
                ref var state = ref throwState.ValueRW;
                state.IsCharging = true;
                state.ChargeProgress = 0f;

                #if UNITY_EDITOR
                Debug.Log($"[WeaponAnimEventSystem] Throw charge started");
                #endif
            }
        }

        private void ProcessThrowReleaseEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            // Server-only: Spawn projectile
            if (!isServer) return;

            foreach (var (throwState, throwAction, entity) in
                     SystemAPI.Query<RefRW<ThrowableState>, RefRO<ThrowableAction>>().WithEntityAccess())
            {
                ref var state = ref throwState.ValueRW;
                var config = throwAction.ValueRO;

                if (state.IsCharging)
                {
                    // Calculate throw force based on charge
                    float force = Mathf.Lerp(config.MinForce, config.MaxForce, state.ChargeProgress);

                    #if UNITY_EDITOR
                    Debug.Log($"[WeaponAnimEventSystem] Throw released at {state.ChargeProgress:P0} charge, force: {force}");
                    #endif

                    // Projectile spawning handled by ThrowableActionSystem on input release.
                    // Animation-driven spawn timing deferred to visual polish pass.

                    state.IsCharging = false;
                    state.ChargeProgress = 0f;
                }
            }
        }

        private void ProcessThrowCompleteEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (throwState, entity) in SystemAPI.Query<RefRW<ThrowableState>>().WithEntityAccess())
            {
                ref var state = ref throwState.ValueRW;
                state.IsCharging = false;
                state.ChargeProgress = 0f;
            }
        }

        // ============================================
        // SHIELD EVENT HANDLERS
        // ============================================

        private void ProcessBlockStartEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            double currentTime = SystemAPI.Time.ElapsedTime;

            foreach (var (shieldState, shieldAction, entity) in
                     SystemAPI.Query<RefRW<ShieldState>, RefRO<ShieldAction>>().WithEntityAccess())
            {
                ref var state = ref shieldState.ValueRW;
                var config = shieldAction.ValueRO;

                state.IsBlocking = true;
                state.BlockStartTime = (float)currentTime;
                state.BlocksThisHold = 0;

                // Activate parry window at start of block
                state.ParryActive = true;
                state.ParryEndTime = (float)currentTime + config.ParryWindow;

                #if UNITY_EDITOR
                Debug.Log($"[WeaponAnimEventSystem] Block started - parry window active for {config.ParryWindow}s");
                #endif
            }
        }

        private void ProcessBlockEndEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (shieldState, entity) in SystemAPI.Query<RefRW<ShieldState>>().WithEntityAccess())
            {
                ref var state = ref shieldState.ValueRW;
                state.IsBlocking = false;
                state.ParryActive = false;

                #if UNITY_EDITOR
                Debug.Log($"[WeaponAnimEventSystem] Block ended - blocked {state.BlocksThisHold} attacks");
                #endif
            }
        }

        private void ProcessParryWindowEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            double currentTime = SystemAPI.Time.ElapsedTime;

            foreach (var (shieldState, shieldAction, entity) in
                     SystemAPI.Query<RefRW<ShieldState>, RefRO<ShieldAction>>().WithEntityAccess())
            {
                ref var state = ref shieldState.ValueRW;
                var config = shieldAction.ValueRO;

                state.ParryActive = true;
                state.ParryEndTime = (float)currentTime + config.ParryWindow;
            }
        }

        private void ProcessParryCompleteEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (shieldState, entity) in SystemAPI.Query<RefRW<ShieldState>>().WithEntityAccess())
            {
                ref var state = ref shieldState.ValueRW;
                state.ParryActive = false;
            }
        }

        // ============================================
        // EQUIP/UNEQUIP EVENT HANDLERS
        // ============================================

        private void ProcessEquipStartEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (item, entity) in SystemAPI.Query<RefRW<CharacterItem>>().WithEntityAccess())
            {
                ref var itemRef = ref item.ValueRW;
                if (itemRef.State == ItemState.Equipping)
                {
                    // Already in equipping state from ECS side
                    #if UNITY_EDITOR
                    Debug.Log($"[WeaponAnimEventSystem] Equip animation started");
                    #endif
                }
            }
        }

        private void ProcessEquipCompleteEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (item, action, entity) in
                     SystemAPI.Query<RefRW<CharacterItem>, RefRW<UsableAction>>().WithEntityAccess())
            {
                ref var itemRef = ref item.ValueRW;
                ref var act = ref action.ValueRW;

                if (itemRef.State == ItemState.Equipping)
                {
                    itemRef.State = ItemState.Equipped;
                    act.CanUse = true;

                    #if UNITY_EDITOR
                    Debug.Log($"[WeaponAnimEventSystem] Equip complete - weapon ready to use");
                    #endif
                }
            }
        }

        private void ProcessUnequipStartEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (item, action, entity) in
                     SystemAPI.Query<RefRW<CharacterItem>, RefRW<UsableAction>>().WithEntityAccess())
            {
                ref var itemRef = ref item.ValueRW;
                ref var act = ref action.ValueRW;

                if (itemRef.State == ItemState.Equipped)
                {
                    itemRef.State = ItemState.Unequipping;
                    act.CanUse = false;

                    #if UNITY_EDITOR
                    Debug.Log($"[WeaponAnimEventSystem] Unequip started - weapon locked");
                    #endif
                }
            }
        }

        private void ProcessUnequipCompleteEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (item, entity) in SystemAPI.Query<RefRW<CharacterItem>>().WithEntityAccess())
            {
                ref var itemRef = ref item.ValueRW;

                if (itemRef.State == ItemState.Unequipping)
                {
                    itemRef.State = ItemState.Unequipped;

                    #if UNITY_EDITOR
                    Debug.Log($"[WeaponAnimEventSystem] Unequip complete");
                    #endif
                }
            }
        }

        // ============================================
        // GENERIC EVENT HANDLERS
        // ============================================

        private void ProcessItemUseStartEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (action, entity) in SystemAPI.Query<RefRW<UsableAction>>().WithEntityAccess())
            {
                ref var act = ref action.ValueRW;
                act.IsUsing = true;
                act.UseTime = 0f;
            }
        }

        private void ProcessItemUseCompleteEvent(WeaponAnimationEvents.WeaponEvent evt, bool isServer)
        {
            foreach (var (action, entity) in SystemAPI.Query<RefRW<UsableAction>>().WithEntityAccess())
            {
                ref var act = ref action.ValueRW;
                act.IsUsing = false;
            }
        }
    }
}
