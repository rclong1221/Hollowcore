using UnityEngine;
using Unity.Entities;
using DIG.Items.Bridges;
using DIG.Weapons.Audio;

namespace DIG.Weapons.Animation
{
    /// <summary>
    /// Relay component for weapon animation events.
    ///
    /// Place this component on the SAME GameObject as the Animator component
    /// (typically the character model or weapon model with animations).
    /// It receives animation events from animation clips and forwards them
    /// to the ECS WeaponAnimationEvents queue for processing by WeaponAnimationEventSystem.
    ///
    /// Unity Animation Events only call methods on components attached to the same
    /// GameObject as the Animator. This relay bridges that gap to ECS.
    ///
    /// Workflow:
    /// 1. Attach this to your character/weapon model with Animator
    /// 2. Animation clips fire events (e.g., "OnItemUse", "OnReloadComplete")
    /// 3. This relay catches events via ExecuteEvent(string) or specific methods
    /// 4. Events are queued to WeaponAnimationEvents static class
    /// 5. WeaponAnimationEventSystem processes queue and updates ECS components
    ///
    /// EPIC 14.20: Added audio integration via WeaponAudioBridge
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class WeaponAnimationEventRelay : MonoBehaviour
    {
        [Header("References")]

        [Header("Debug")]
        [Tooltip("Enable debug logging for weapon animation events")]
        [SerializeField] private bool debugLogging = false;

        private Animator _animator;
        private WeaponEquipVisualBridge _visualBridge;
        private Transform _leftHandBone;
        private bool _hasWarnedMissingBridge = false;

        // EPIC 14.20: Audio bridge reference
        private WeaponAudioBridge _audioBridge;

        /// <summary>
        /// EPIC 14.20: Clear cached audio bridge when weapon changes.
        /// Called by WeaponEquipVisualBridge when equipping a new weapon.
        /// </summary>
        public void ClearAudioBridgeCache()
        {
            _audioBridge = null;
            if (debugLogging)
                Debug.Log("[WEAPON_AUDIO_DEBUG] [Relay] Audio bridge cache cleared");
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            
            // EPIC 14.17 Phase 6: Auto-discover WeaponEquipVisualBridge
            AutoDiscoverVisualBridge();

            if (_animator == null)
            {
                Debug.LogWarning($"[WeaponAnimEventRelay] No Animator found on {gameObject.name}");
            }
            else
            {
                _leftHandBone = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
        }
        
        /// <summary>
        /// Auto-discover the WeaponEquipVisualBridge on the character hierarchy.
        /// Searches parent, then root, then scene as fallback.
        /// </summary>
        private void AutoDiscoverVisualBridge()
        {
            // 1. Try parent hierarchy first (most common case)
            _visualBridge = GetComponentInParent<WeaponEquipVisualBridge>();
            
            if (_visualBridge != null)
            {
                if (debugLogging)
                    Debug.Log($"[WeaponAnimEventRelay] Auto-discovered VisualBridge in parent: {_visualBridge.gameObject.name}");
                return;
            }
            
            // 2. Try finding on root object
            var root = transform.root;
            _visualBridge = root.GetComponentInChildren<WeaponEquipVisualBridge>();
            
            if (_visualBridge != null)
            {
                if (debugLogging)
                    Debug.Log($"[WeaponAnimEventRelay] Auto-discovered VisualBridge on root hierarchy: {_visualBridge.gameObject.name}");
                return;
            }
            
            // 3. Deferred discovery - may be spawned later
            if (!_hasWarnedMissingBridge)
            {
                Debug.LogWarning($"[WeaponAnimEventRelay] No WeaponEquipVisualBridge found for {gameObject.name}. VFX events will be skipped. " +
                                 $"Bridge will be searched again on first event.");
                _hasWarnedMissingBridge = true;
            }
        }
        
        /// <summary>
        /// Ensure we have a valid bridge reference. Called before triggering events.
        /// </summary>
        private bool EnsureVisualBridge()
        {
            if (_visualBridge != null) return true;
            
            // Try to discover again (character may have spawned late)
            AutoDiscoverVisualBridge();
            
            return _visualBridge != null;
        }
        
        // Helper to trigger VFX on active weapon
        private void TriggerVFX(string vfxID)
        {
            // EPIC 14.17 Phase 6: Use auto-discovery
            if (!EnsureVisualBridge())
            {
                if (debugLogging)
                    Debug.Log($"[WEAPON_VFX_DEBUG] [Relay] SKIPPED '{vfxID}': No VisualBridge available");
                return;
            }

            if (_visualBridge.CurrentItemVFX != null)
            {
                if (debugLogging)
                    Debug.Log($"[WEAPON_VFX_DEBUG] [Relay] Triggering VFX: '{vfxID}'");
                _visualBridge.CurrentItemVFX.PlayVFX(vfxID);
            }
            else if (debugLogging)
            {
                Debug.Log($"[WEAPON_VFX_DEBUG] [Relay] No VFX component on current weapon for '{vfxID}'");
            }
        }

        // EPIC 14.20: Helper to trigger audio on active weapon
        private void TriggerAudio(WeaponAudioEventType audioEvent)
        {
            // Try to get audio bridge from current weapon (search hierarchy)
            if (_audioBridge == null && _visualBridge != null && _visualBridge.CurrentItemVFX != null)
            {
                // First try on same GameObject
                _audioBridge = _visualBridge.CurrentItemVFX.GetComponent<WeaponAudioBridge>();

                // If not found, search parent hierarchy (WeaponAudioBridge may be on weapon root)
                if (_audioBridge == null)
                {
                    _audioBridge = _visualBridge.CurrentItemVFX.GetComponentInParent<WeaponAudioBridge>();
                }

                // If still not found, search children
                if (_audioBridge == null)
                {
                    _audioBridge = _visualBridge.CurrentItemVFX.GetComponentInChildren<WeaponAudioBridge>();
                }

                if (debugLogging && _audioBridge != null)
                {
                    Debug.Log($"[WEAPON_AUDIO_DEBUG] [Relay] Found AudioBridge on: {_audioBridge.gameObject.name}");
                }
            }

            if (_audioBridge != null)
            {
                if (debugLogging)
                {
                    Debug.Log($"[WEAPON_AUDIO_DEBUG] [Relay] Playing audio event: {audioEvent}");
                }
                _audioBridge.PlaySound(audioEvent);
            }
            else if (debugLogging)
            {
                Debug.Log($"[WEAPON_AUDIO_DEBUG] [Relay] No AudioBridge found for event '{audioEvent}'");
            }
        }

        // Helper to bridge legacy events to new Visual Action Controller
        private void ExecuteVisualAction(string action)
        {
            // EPIC 14.17 Phase 6: Use auto-discovery
            if (!EnsureVisualBridge() || _visualBridge.CurrentItemVFX == null)
                return;
                
            var vac = _visualBridge.CurrentItemVFX.GetComponent<DIG.Items.Authoring.WeaponVisualActionController>();
            if (vac != null)
            {
                // Ensure the LeftHand target is set relative to the character
                if (_leftHandBone != null)
                    vac.SetReparentTarget("LeftHand", _leftHandBone);

                vac.ExecuteAction(action);
            }
        }

        // ====================================================
        // MAIN OPSIVE EVENT DISPATCHER
        // OPSIVE clips call ExecuteEvent(string) with event name as parameter
        // ====================================================

        /// <summary>
        /// Main event dispatcher called by OPSIVE animation clips.
        /// Routes to specific handlers based on event name.
        /// </summary>
        public void ExecuteEvent(string eventName)
        {
            // Forward "VisualAction" styled events if validation is needed later, 
            // but for now ExecuteEvent handles the dispatch.
            // ... strict logic is inside the switch
            
            if (debugLogging && !eventName.Contains(":")) 
                Debug.Log($"[WeaponAnimationEventRelay] ExecuteEvent: {eventName}");

            ProcessEvent(eventName);
        }

        /// <summary>
        /// Alias method for Visual Action events.
        /// This allows Animation Events to call "VisualAction" with a parameter like "ShowPart:Magazine".
        /// </summary>
        public void VisualAction(string action)
        {
            if (debugLogging) Debug.Log($"[WEAPON_VFX_DEBUG] [Relay] VisualAction called with: '{action}'");
            ExecuteEvent(action);
        }

        private void ProcessEvent(string eventName)
        {
            Debug.Log($"[WEAPON_VFX_DEBUG] [Relay] ExecuteEvent received: {eventName}");
            
            if (debugLogging)
                Debug.Log($"[OpsiveWeaponAnimEventRelay] ExecuteEvent received: {eventName}");

            switch (eventName)
            {
                // Shootable events
                case "OnAnimatorItemFire":
                case "OnItemFire":
                case "Fire":
                    OnAnimatorItemFire();
                    break;

                case "OnAnimatorItemFireComplete":
                case "OnItemFireComplete":
                case "FireComplete":
                    OnAnimatorItemFireComplete();
                    break;

                case "OnAnimatorReloadStart":
                case "OnReloadStart":
                case "ReloadStart":
                    OnAnimatorReloadStart();
                    break;

                case "OnAnimatorItemReloadComplete":  // <-- OPSIVE's actual event name!
                case "OnAnimatorReloadComplete":
                case "OnReloadComplete":
                case "ReloadComplete":
                    OnAnimatorReloadComplete();
                    break;

                case "OnAnimatorReloadInsertAmmo":
                case "OnReloadInsertAmmo":
                case "InsertAmmo":
                    OnAnimatorReloadInsertAmmo();
                    break;

                case "OnAnimatorShellEject":
                case "OnShellEject":
                case "ShellEject":
                    OnAnimatorShellEject();
                    break;
                    
                case "OnAnimatorDryFire":
                case "OnDryFire":
                case "DryFire":
                    OnAnimatorDryFire();
                    break;

                // EPIC 14.20: Bolt/Slide pull event
                case "OnAnimatorBoltPull":
                case "OnBoltPull":
                case "BoltPull":
                case "SlidePull":
                case "ChargingHandle":
                    OnAnimatorBoltPull();
                    break;

                // Magazine reload clip events (Opsive-style)
                case "OnAnimatorItemReloadDetachClip":
                case "OnReloadDetachClip":
                case "ReloadDetachClip":
                case "DetachClip":
                    OnAnimatorItemReloadDetachClip();
                    break;

                case "OnAnimatorItemReloadDropClip":
                case "OnReloadDropClip":
                case "ReloadDropClip":
                case "DropClip":
                    OnAnimatorItemReloadDropClip();
                    break;

                case "OnAnimatorItemReloadAttachClip":
                case "OnReloadAttachClip":
                case "ReloadAttachClip":
                case "AttachClip":
                    OnAnimatorItemReloadAttachClip();
                    break;

                case "OnAnimatorItemReactivateClip":
                case "OnReactivateClip":
                case "ReactivateClip":
                    OnAnimatorItemReactivateClip();
                    break;

                // Melee events
                case "OnAnimatorMeleeStart":
                case "OnMeleeStart":
                case "MeleeStart":
                    OnAnimatorMeleeStart();
                    break;

                case "OnAnimatorMeleeHitFrame":
                case "OnMeleeHitFrame":
                case "MeleeHit":
                case "HitFrame":
                    OnAnimatorMeleeHitFrame();
                    break;

                case "OnAnimatorMeleeComplete":
                case "OnMeleeComplete":
                case "MeleeComplete":
                    OnAnimatorMeleeComplete();
                    break;

                case "OnAnimatorMeleeCombo":
                case "OnMeleeCombo":
                case "Combo":
                    OnAnimatorMeleeCombo();
                    break;

                // Throwable events
                case "OnAnimatorThrowChargeStart":
                case "OnThrowChargeStart":
                case "ChargeStart":
                    OnAnimatorThrowChargeStart();
                    break;

                case "OnAnimatorThrowRelease":
                case "OnThrowRelease":
                case "ThrowRelease":
                case "Release":
                    OnAnimatorThrowRelease();
                    break;

                case "OnAnimatorThrowComplete":
                case "OnThrowComplete":
                case "ThrowComplete":
                    OnAnimatorThrowComplete();
                    break;

                // Bow/Crossbow events
                case "OnAnimatorBowDraw":
                case "OnBowDraw":
                case "BowDraw":
                case "DrawBow":
                    OnAnimatorBowDraw();
                    break;

                case "OnAnimatorBowRelease":
                case "OnBowRelease":
                case "BowRelease":
                case "BowFire":
                    OnAnimatorBowRelease();
                    break;

                case "OnAnimatorBowCancel":
                case "OnBowCancel":
                case "BowCancel":
                    OnAnimatorBowCancel();
                    break;

                case "OnAnimatorArrowNock":
                case "OnArrowNock":
                case "ArrowNock":
                case "NockArrow":
                    OnAnimatorArrowNock();
                    break;

                // Shield events
                case "OnAnimatorBlockStart":
                case "OnBlockStart":
                case "BlockStart":
                    OnAnimatorBlockStart();
                    break;

                case "OnAnimatorBlockEnd":
                case "OnBlockEnd":
                case "BlockEnd":
                    OnAnimatorBlockEnd();
                    break;

                case "OnAnimatorParryWindow":
                case "OnParryWindow":
                case "ParryWindow":
                    OnAnimatorParryWindow();
                    break;

                case "OnAnimatorParryComplete":
                case "OnParryComplete":
                case "ParryComplete":
                    OnAnimatorParryComplete();
                    break;

                case "OnAnimatorBlockImpact":
                case "OnBlockImpact":
                case "BlockImpact":
                case "ShieldHit":
                    OnAnimatorBlockImpact();
                    break;

                case "OnAnimatorParrySuccess":
                case "OnParrySuccess":
                case "ParrySuccess":
                    OnAnimatorParrySuccess();
                    break;

                // Equip/Unequip events
                case "OnAnimatorEquipStart":
                case "OnEquipStart":
                case "EquipStart":
                    OnAnimatorEquipStart();
                    break;

                case "OnAnimatorEquipComplete":
                case "OnEquipComplete":
                case "EquipComplete":
                    OnAnimatorEquipComplete();
                    break;

                case "OnAnimatorUnequipStart":
                case "OnUnequipStart":
                case "UnequipStart":
                    OnAnimatorUnequipStart();
                    break;

                case "OnAnimatorUnequipComplete":
                case "OnUnequipComplete":
                case "UnequipComplete":
                    OnAnimatorUnequipComplete();
                    break;

                // Generic item use
                case "OnAnimatorItemUseStart":
                case "OnItemUseStart":
                case "UseStart":
                    OnAnimatorItemUseStart();
                    break;
                    
                // OPSIVE: This event often means "Fire" (Use Action)
                case "OnAnimatorItemUse":
                case "OnItemUse":
                    OnAnimatorItemFire();
                    break;

                case "OnAnimatorItemUseComplete":
                case "OnItemUseComplete":
                case "UseComplete":
                    OnAnimatorItemUseComplete();
                    break;

                default:
                    // Try data-driven event bindings on the weapon
                    if (TryTriggerWeaponEvent(eventName))
                        return;
                    
                    if (debugLogging)
                        Debug.Log($"[OpsiveWeaponAnimEventRelay] Unhandled event: {eventName}");
                    break;
            }
        }
        
        /// <summary>
        /// Try to trigger an event via the weapon's visual action controller or specialized controllers.
        /// Returns true if the event was handled.
        /// </summary>
        private bool TryTriggerWeaponEvent(string eventName)
        {
            if (_visualBridge == null || _visualBridge.CurrentItemVFX == null)
                return false;
            
            var weaponRoot = _visualBridge.CurrentItemVFX.gameObject;
            
            // 1. Try generic visual actions (ShowPart:X, HidePart:X, Spawn:X, etc.)
            var visualController = weaponRoot.GetComponent<DIG.Items.Authoring.WeaponVisualActionController>();
            if (visualController != null && eventName.Contains(":"))
            {
                if (visualController.ExecuteAction(eventName))
                    return true;
            }
            
            // 2. Try specialized reload controller for Opsive-style events
            var reloadController = weaponRoot.GetComponent<DIG.Items.Authoring.MagazineReloadController>();
            if (reloadController != null)
            {
                switch (eventName)
                {
                    case "OnAnimatorItemReloadDetachClip":
                    case "DetachClip":
                        reloadController.DetachMagazine();
                        return true;
                        
                    case "OnAnimatorItemReloadDropClip":
                    case "DropClip":
                        reloadController.DropMagazine();
                        return true;
                        
                    case "OnAnimatorItemReactivateClip":
                    case "ReactivateClip":
                        reloadController.ShowFreshMagazine();
                        return true;
                        
                    case "OnAnimatorItemReloadAttachClip":
                    case "AttachClip":
                        reloadController.AttachMagazine();
                        return true;
                        
                    case "OnAnimatorReloadStart":
                    case "ReloadStart":
                        reloadController.StartReload();
                        return true;
                        
                    case "OnAnimatorReloadComplete":
                    case "ReloadComplete":
                        reloadController.CompleteReload();
                        return true;
                }
            }
            
            return false;
        }

        // ====================================================
        // SHOOTABLE ANIMATION EVENTS
        // ====================================================

        /// <summary>
        /// Called when fire animation triggers.
        /// Only triggers VFX if weapon has ammo (prevents muzzle flash/shell eject on dry fire).
        /// EPIC 14.20: Also triggers audio via WeaponAudioBridge.
        /// </summary>
        public void OnAnimatorItemFire()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorItemFire");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.Fire);

            // CRITICAL: Only trigger fire VFX if weapon actually has ammo
            // This prevents muzzle flash and shell ejection during dry fire
            bool hasAmmo = _visualBridge != null && _visualBridge.CurrentWeaponHasAmmo;

            if (hasAmmo)
            {
                TriggerVFX("Fire");
                // Also trigger shell eject on fire (fallback if animation event missing)
                TriggerVFX("ShellEject");
                // EPIC 14.20: Trigger fire audio
                TriggerAudio(WeaponAudioEventType.Fire);
            }
            else
            {
                // EPIC 14.20: No ammo - trigger dry fire audio instead
                if (debugLogging)
                    Debug.Log("[WeaponAnimEventRelay] Fire VFX BLOCKED - no ammo (triggering dry fire audio)");
                TriggerAudio(WeaponAudioEventType.DryFire);
            }
        }

        /// <summary>
        /// Called when fire animation completes (for semi-auto weapons).
        /// </summary>
        public void OnAnimatorItemFireComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorItemFireComplete");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.FireComplete);
        }

        /// <summary>
        /// Called when reload animation starts.
        /// EPIC 14.20: Also triggers reload start audio.
        /// </summary>
        public void OnAnimatorReloadStart()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorReloadStart");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.ReloadStart);
            TriggerVFX("Reload");
            // EPIC 14.20: Trigger reload start audio
            TriggerAudio(WeaponAudioEventType.ReloadStart);
        }

        /// <summary>
        /// Called when magazine is inserted / ammo is loaded (mid-reload).
        /// This is when ammo count should actually update.
        /// </summary>
        public void OnAnimatorReloadInsertAmmo()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorReloadInsertAmmo");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.ReloadInsertAmmo);
            TriggerVFX("ReloadInsert");
        }

        /// <summary>
        /// Called when reload animation completes.
        /// EPIC 14.20: Also triggers reload complete audio.
        /// </summary>
        public void OnAnimatorReloadComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorReloadComplete");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.ReloadComplete);
            TriggerVFX("ReloadComplete");
            // EPIC 14.20: Trigger reload complete audio
            TriggerAudio(WeaponAudioEventType.ReloadComplete);
        }

        /// <summary>
        /// Called when attempting to fire with no ammo.
        /// EPIC 14.20: Also triggers dry fire audio.
        /// </summary>
        public void OnAnimatorDryFire()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorDryFire");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.DryFire);
            TriggerVFX("DryFire");
            // EPIC 14.20: Trigger dry fire audio
            TriggerAudio(WeaponAudioEventType.DryFire);
        }

        /// <summary>
        /// EPIC 14.20: Called when bolt/slide/charging handle is pulled.
        /// Typically happens at the end of reload or when chambering a round.
        /// </summary>
        public void OnAnimatorBoltPull()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorBoltPull");

            // EPIC 14.20: Trigger bolt pull audio
            TriggerAudio(WeaponAudioEventType.BoltPull);
            TriggerVFX("BoltPull");
        }

        /// <summary>
        /// Called when shell ejection should happen.
        /// Only triggers if weapon has ammo (prevents shell eject on dry fire).
        /// </summary>
        public void OnAnimatorShellEject()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorShellEject");

            // CRITICAL: Only trigger shell eject if weapon actually has ammo
            // This prevents shell ejection during dry fire
            bool hasAmmo = _visualBridge != null && _visualBridge.CurrentWeaponHasAmmo;
            
            if (hasAmmo)
            {
                TriggerVFX("ShellEject");
            }
            else
            {
                Debug.Log("[WeaponAnimEventRelay] ShellEject VFX BLOCKED - no ammo (dry fire)");
            }
        }

        // ====================================================
        // MAGAZINE RELOAD CLIP ANIMATION EVENTS
        // ====================================================

        /// <summary>
        /// Called when magazine should be detached from weapon and attached to hand.
        /// Animation event: OnAnimatorItemReloadDetachClip
        /// </summary>
        public void OnAnimatorItemReloadDetachClip()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorItemReloadDetachClip");

            // Bridge to new Visual Action System
            ExecuteVisualAction("Reparent:TPMagazine:LeftHand");
            ExecuteVisualAction("Reparent:FPMagazine:LeftHand");

            var reloadController = GetMagazineReloadController();
            if (reloadController != null)
            {
                reloadController.DetachMagazine();
            }
        }

        /// <summary>
        /// Called when old magazine should be dropped as physics object.
        /// Animation event: OnAnimatorItemReloadDropClip
        /// EPIC 14.20: Also triggers mag out audio.
        /// </summary>
        public void OnAnimatorItemReloadDropClip()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorItemReloadDropClip");

            // Bridge to new Visual Action System
            ExecuteVisualAction("Spawn:DropMag");
            ExecuteVisualAction("HidePart:TPMagazine");
            ExecuteVisualAction("HidePart:FPMagazine");

            var reloadController = GetMagazineReloadController();
            if (reloadController != null)
            {
                reloadController.DropMagazine();
            }

            TriggerVFX("ReloadMagDrop");
            // EPIC 14.20: Trigger mag out audio
            TriggerAudio(WeaponAudioEventType.MagOut);
        }

        /// <summary>
        /// Called when magazine should be re-attached to weapon from hand.
        /// Animation event: OnAnimatorItemReloadAttachClip
        /// EPIC 14.20: Also triggers mag in audio.
        /// </summary>
        public void OnAnimatorItemReloadAttachClip()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorItemReloadAttachClip");

            // Bridge to new Visual Action System
            ExecuteVisualAction("Restore:TPMagazine");
            ExecuteVisualAction("Restore:FPMagazine");

            var reloadController = GetMagazineReloadController();
            if (reloadController != null)
            {
                reloadController.AttachMagazine();
            }

            TriggerVFX("ReloadMagInsert");
            // EPIC 14.20: Trigger mag in audio
            TriggerAudio(WeaponAudioEventType.MagIn);
        }

        /// <summary>
        /// Called when fresh magazine should be shown (after drop, before attach).
        /// Animation event: OnAnimatorItemReactivateClip
        /// </summary>
        public void OnAnimatorItemReactivateClip()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorItemReactivateClip");

            // Bridge to new Visual Action System
            ExecuteVisualAction("ShowPart:TPMagazine");
            ExecuteVisualAction("ShowPart:FPMagazine");

            var reloadController = GetMagazineReloadController();
            if (reloadController != null)
            {
                reloadController.ShowFreshMagazine();
            }
        }

        /// <summary>
        /// Helper to get MagazineReloadController from current weapon.
        /// </summary>
        private DIG.Items.Authoring.MagazineReloadController GetMagazineReloadController()
        {
            if (_visualBridge != null && _visualBridge.CurrentItemVFX != null)
            {
                return _visualBridge.CurrentItemVFX.GetComponent<DIG.Items.Authoring.MagazineReloadController>();
            }
            return null;
        }

        // ====================================================
        // MELEE ANIMATION EVENTS
        // ====================================================

        /// <summary>
        /// Called when melee swing animation starts.
        /// EPIC 14.20: Also triggers melee swing audio.
        /// </summary>
        public void OnAnimatorMeleeStart()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorMeleeStart");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.MeleeStart);
            // EPIC 14.20: Trigger melee swing audio
            TriggerAudio(WeaponAudioEventType.MeleeSwing);
            TriggerVFX("MeleeSwing");
        }

        /// <summary>
        /// Called at the exact frame when melee hitbox should be active.
        /// This is the damage window frame.
        /// EPIC 14.20: Also triggers melee hit audio.
        /// </summary>
        public void OnAnimatorMeleeHitFrame()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorMeleeHitFrame");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.MeleeHitFrame);
            // EPIC 14.20: Trigger melee hit audio (on impact confirmation)
            TriggerAudio(WeaponAudioEventType.MeleeHit);
            TriggerVFX("MeleeHit");
        }

        /// <summary>
        /// Called when melee swing animation completes.
        /// </summary>
        public void OnAnimatorMeleeComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorMeleeComplete");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.MeleeComplete);
        }

        /// <summary>
        /// Called at combo transition point (can chain next attack).
        /// </summary>
        public void OnAnimatorMeleeCombo()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorMeleeCombo");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.MeleeCombo);
        }

        // ====================================================
        // THROWABLE ANIMATION EVENTS
        // ====================================================

        /// <summary>
        /// Called when throw charge animation starts.
        /// EPIC 14.20: Also triggers throw charge audio.
        /// </summary>
        public void OnAnimatorThrowChargeStart()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorThrowChargeStart");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.ThrowChargeStart);
            // EPIC 14.20: Trigger throw charge audio
            TriggerAudio(WeaponAudioEventType.ThrowCharge);
        }

        /// <summary>
        /// Called at the exact frame when throw is released.
        /// Projectile should spawn at this moment.
        /// EPIC 14.20: Also triggers throw release audio.
        /// </summary>
        public void OnAnimatorThrowRelease()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorThrowRelease");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.ThrowRelease);
            TriggerVFX("ThrowRelease");
            // EPIC 14.20: Trigger throw release audio
            TriggerAudio(WeaponAudioEventType.ThrowRelease);
        }

        /// <summary>
        /// Called when throw animation completes.
        /// </summary>
        public void OnAnimatorThrowComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorThrowComplete");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.ThrowComplete);
        }

        // ====================================================
        // BOW/CROSSBOW ANIMATION EVENTS
        // ====================================================

        /// <summary>
        /// Called when bow draw starts.
        /// EPIC 14.20: Also triggers bow draw audio.
        /// </summary>
        public void OnAnimatorBowDraw()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorBowDraw");

            // EPIC 14.20: Trigger bow draw audio
            TriggerAudio(WeaponAudioEventType.BowDraw);
            TriggerVFX("BowDraw");
        }

        /// <summary>
        /// Called when bow is released (arrow fires).
        /// EPIC 14.20: Also triggers bow release audio.
        /// </summary>
        public void OnAnimatorBowRelease()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorBowRelease");

            // EPIC 14.20: Trigger bow release audio
            TriggerAudio(WeaponAudioEventType.BowRelease);
            TriggerVFX("BowRelease");
            TriggerVFX("ArrowFire");
        }

        /// <summary>
        /// Called when bow draw is cancelled.
        /// EPIC 14.20: Also triggers bow cancel audio.
        /// </summary>
        public void OnAnimatorBowCancel()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorBowCancel");

            // EPIC 14.20: Trigger bow cancel audio
            TriggerAudio(WeaponAudioEventType.BowCancel);
        }

        /// <summary>
        /// Called when arrow is nocked (reload for bow).
        /// EPIC 14.20: Also triggers arrow nock audio.
        /// </summary>
        public void OnAnimatorArrowNock()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorArrowNock");

            // EPIC 14.20: Trigger arrow nock audio
            TriggerAudio(WeaponAudioEventType.ArrowNock);
            TriggerVFX("ArrowNock");
        }

        // ====================================================
        // SHIELD ANIMATION EVENTS
        // ====================================================

        /// <summary>
        /// Called when block animation starts (shield raised).
        /// EPIC 14.20: Also triggers block start audio.
        /// </summary>
        public void OnAnimatorBlockStart()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorBlockStart");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.BlockStart);
            // EPIC 14.20: Trigger block start audio
            TriggerAudio(WeaponAudioEventType.BlockStart);
        }

        /// <summary>
        /// Called when block animation ends (shield lowered).
        /// </summary>
        public void OnAnimatorBlockEnd()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorBlockEnd");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.BlockEnd);
        }

        /// <summary>
        /// Called during the perfect parry window.
        /// </summary>
        public void OnAnimatorParryWindow()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorParryWindow");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.ParryWindow);
        }

        /// <summary>
        /// Called when parry window closes.
        /// </summary>
        public void OnAnimatorParryComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorParryComplete");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.ParryComplete);
        }

        /// <summary>
        /// Called when shield blocks an attack.
        /// EPIC 14.20: Also triggers block impact audio.
        /// </summary>
        public void OnAnimatorBlockImpact()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorBlockImpact");

            // EPIC 14.20: Trigger block impact audio
            TriggerAudio(WeaponAudioEventType.BlockImpact);
            TriggerVFX("BlockImpact");
        }

        /// <summary>
        /// Called when a parry is successful.
        /// EPIC 14.20: Also triggers parry success audio.
        /// </summary>
        public void OnAnimatorParrySuccess()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorParrySuccess");

            // EPIC 14.20: Trigger parry success audio
            TriggerAudio(WeaponAudioEventType.ParrySuccess);
            TriggerVFX("ParrySuccess");
        }

        // ====================================================
        // EQUIP/UNEQUIP ANIMATION EVENTS
        // ====================================================

        /// <summary>
        /// Called when equip animation starts.
        /// EPIC 14.20: Also triggers equip audio.
        /// </summary>
        public void OnAnimatorEquipStart()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorEquipStart");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.EquipStart);
            // EPIC 14.20: Trigger equip audio
            TriggerAudio(WeaponAudioEventType.Equip);
        }

        /// <summary>
        /// Called when equip animation completes.
        /// Weapon is now fully equipped and usable.
        /// </summary>
        public void OnAnimatorEquipComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorEquipComplete");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.EquipComplete);
        }

        /// <summary>
        /// Called when unequip animation starts.
        /// EPIC 14.20: Also triggers unequip audio.
        /// </summary>
        public void OnAnimatorUnequipStart()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorUnequipStart");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.UnequipStart);
            // EPIC 14.20: Trigger unequip audio
            TriggerAudio(WeaponAudioEventType.Unequip);
        }

        /// <summary>
        /// Called when unequip animation completes.
        /// Weapon is now fully unequipped.
        /// </summary>
        public void OnAnimatorUnequipComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorUnequipComplete");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.UnequipComplete);
        }

        // ====================================================
        // GENERIC ITEM USE EVENTS
        // ====================================================

        /// <summary>
        /// Called when generic item use animation starts.
        /// </summary>
        public void OnAnimatorItemUseStart()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorItemUseStart");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.ItemUseStart);
        }

        /// <summary>
        /// Called when generic item use animation completes.
        /// </summary>
        public void OnAnimatorItemUseComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveWeaponAnimEventRelay] OnAnimatorItemUseComplete");

            WeaponAnimationEvents.QueueEvent(WeaponAnimationEvents.EventType.ItemUseComplete);
        }

        // ====================================================
        // OPSIVE SPECIFIC METHODS
        // These match OPSIVE's CharacterItemAction animation event names
        // ====================================================

        /// <summary>
        /// OPSIVE: Called by ShootableAction animations.
        /// </summary>
        public void OnItemUse()
        {
            OnAnimatorItemFire();
        }

        /// <summary>
        /// OPSIVE: Called by ShootableAction for reload.
        /// </summary>
        public void OnItemReload()
        {
            OnAnimatorReloadStart();
        }

        /// <summary>
        /// OPSIVE: Called by MeleeAction animations.
        /// </summary>
        public void OnMeleeAttack()
        {
            OnAnimatorMeleeStart();
        }

        /// <summary>
        /// OPSIVE: Called by MeleeAction at impact frame.
        /// </summary>
        public void OnMeleeImpact()
        {
            OnAnimatorMeleeHitFrame();
        }

        /// <summary>
        /// OPSIVE: Called by ThrowableAction animations.
        /// </summary>
        public void OnThrow()
        {
            OnAnimatorThrowRelease();
        }
    }
}
