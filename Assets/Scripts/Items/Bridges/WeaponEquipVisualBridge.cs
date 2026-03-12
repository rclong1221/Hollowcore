using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;
using DIG.Items;
using DIG.Items.Authoring;
using DIG.Weapons; // For WeaponFireState, WeaponAmmoState
using DIG.Weapons.Animation;
using DIG.Player.View;
using DIG.Player.IK; // For HandIKState
using DIG.Core.Input; // For ParadigmStateMachine
using PlayerInputState = global::Player.Systems.PlayerInputState;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Objects;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif



namespace DIG.Items.Bridges
{
    /// <summary>
    /// Bridge component that shows/hides weapon GameObjects based on IEquipmentProvider state.
    /// 
    /// Place this on the player prefab. It communicates with the DIGEquipmentProvider
    /// and shows the corresponding weapon model while hiding others.
    /// 
    /// Refactored to be agnostic of underlying inventory system (EPIC14.2).
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponEquipVisualBridge : MonoBehaviour
    {    
        [Header("References")]
        [Tooltip("Equipment Provider to read state from. Auto-found if not set.")]
        public MonoBehaviour EquipmentProviderMono; // Serialized field for inspector assignment
        private IEquipmentProvider _equipmentProvider;
        
        [Header("Weapon Models")]
        [Tooltip("Weapon model GameObjects, indexed by QuickSlot (1-indexed). Index 0 is ignored.")]
        public GameObject[] WeaponModels = new GameObject[10];
        
        [Header("Attach Points")]
        [Tooltip("Hand attach point for equipped weapons (right hand)")]
        public Transform HandAttachPoint;
        
        [Tooltip("Left hand attach point for off-hand weapons (shield, etc)")]
        public Transform LeftHandAttachPoint;
        
        [Tooltip("Back attach point for holstered weapons")]
        public Transform BackAttachPoint;

        
        [Header("Settings")]
        [Tooltip("Show holstered weapons on back")]
        public bool ShowHolsteredWeapons = true;
        
        [Header("Debug")]
        [Tooltip("Log equip/unequip events to console")]
        public bool DebugLogging = false;

        [Tooltip("Focused bow animation debugging - logs state changes and why animations may not hold")]
        public bool BowDebugLogging = false;

        [Tooltip("Attack replication debugging - logs ECS state reading and animation updates")]
        public bool AttackReplicationDebug = false;
        
        [Header("Animation")]
        [Tooltip("Player Animator to drive")]
        public Animator PlayerAnimator;
        [Tooltip("Parameter name for Item ID")]
        public string ParamSlotItemID = "Slot0ItemID";
        [Tooltip("Parameter name for Item State Index (Opsive Standard: Slot0ItemStateIndex)")]
        public string ParamSlotItemState = "Slot0ItemStateIndex";
        [Tooltip("Parameter name for Item State Change Trigger (Opsive Standard: Slot0ItemStateIndexChange)")]
        public string ParamSlotItemChange = "Slot0ItemStateIndexChange";
        [Tooltip("Parameter name for Item Substate Index (Opsive Melee: Slot0ItemSubstateIndex)")]
        public string ParamSlotItemSubstate = "Slot0ItemSubstateIndex";
        [Tooltip("Parameter name for Aiming (Opsive Standard: Aiming)")]
        public string ParamAiming = "Aiming";
        [Tooltip("Parameter name for Movement Set ID (weapon type: 0=Guns, 1=Melee, 2=Bow)")]
        public string ParamMovementSetID = "MovementSetID";
        [Tooltip("Map QuickSlot index to ItemID (e.g. Slot 1 = Rifle ID 22)")]
        // Opsive ClimbingDemo Controller Mapping:
        // Slot 1 (Rifle)    = 22
        // Slot 2 (Katana/Bow)= 24
        // Slot 3 (Knife)     = 23
        // Slot 4 (Shotgun)   = 3
        // Slot 5 (Sniper)    = 5
        // Slot 6 (Pistol)    = 1
        // Slot 7 (Rocket)    = 6
        // Slot 8 (Grenade)   = 41
        public int[] SlotItemIDs = new int[] { 0, 22, 24, 23, 3, 5, 1, 6, 41, 0 };
        
        [Tooltip("Map QuickSlot index to MovementSetID (0=Guns, 1=Melee, 2=Bow)")]
        // Slot 1 (Rifle)    = 0 (Guns)
        // Slot 2 (Katana)   = 1 (Melee)
        // Slot 3 (Knife)    = 1 (Melee)
        // Slot 4 (Shotgun)  = 0 (Guns)
        // etc.
        public int[] SlotMovementSetIDs = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0 };

        [Header("Components")]
        public WeaponAnimationEventRelay EventRelay;
        public DigOpsiveIK HandIK;
        
        // EPIC 14.17: Item VFX Authoring cache
        private ItemVFXAuthoring _currentItemVFX;
        public ItemVFXAuthoring CurrentItemVFX => _currentItemVFX;
        
        // EPIC 14.17 Phase 6: Magazine Reload Controller cache (auto-discovered)
        private MagazineReloadController _currentMagazineController;
        public MagazineReloadController CurrentMagazineController => _currentMagazineController;
        
        /// <summary>
        /// Check if the current equipped weapon has ammo in the clip.
        /// Used by animation event relay to prevent fire VFX on dry fire.
        /// </summary>
        public bool CurrentWeaponHasAmmo
        {
            get
            {
                if (_equipmentProvider == null) return true; // Fail safe
                
                var mainItem = _equipmentProvider.GetEquippedItem(0);
                Entity weaponEntity = mainItem.ItemEntity;
                
                if (weaponEntity == Entity.Null || !_entityManager.Exists(weaponEntity))
                    return true; // Fail safe
                    
                if (!_entityManager.HasComponent<WeaponAmmoState>(weaponEntity))
                    return true; // Not a shootable weapon, allow VFX
                    
                var ammoState = _entityManager.GetComponentData<WeaponAmmoState>(weaponEntity);
                return ammoState.AmmoCount > 0;
            }
        }
        
        // Track reload state for input blocking
        private bool _lastIsReloading = false;
        private bool _reloadAnimationTriggered = false;

        // State tracking
        private int _lastEquippedSlot = -1;
        private Entity _lastEquippedEntity = Entity.Null;
        private int _lastForcedSubStateItemID = -1; 
        private float _subStateForceTimer = 0f;
        private int _lastItemState = -1;
        private EntityManager _entityManager;
        private bool _isLocalPlayer = false; // Default to false - set dynamically from DIGEquipmentProvider.IsLocalPlayer
        
        // EPIC 14.17: Track previous shot time for ECS-driven VFX (all players, per-shot)
        private float _lastTimeSinceLastShot = float.MaxValue;
        
        // Off-hand state tracking
        private int _lastOffHandSlot = -1;
        private Entity _lastOffHandEntity = Entity.Null;
        private GameObject _currentOffHandModel = null;
        
        // Off-hand unequip transition tracking
        private bool _offHandUnequipping = false;
        private float _offHandUnequipTimer = 0f;
        private int _offHandUnequipItemID = 0; // The item ID being unequipped (for animation)

        // ==================== SOCKET & PARENT CACHE (Hybrid System - EPIC 14.16) ====================
        // Maps ObjectIdentifier ID to Transform for Opsive's "Category + Specific Offset" positioning (Legacy)
        private Dictionary<uint, Transform> _weaponParentCache = new Dictionary<uint, Transform>();
        
        // Maps SocketType to Transform for the new Universal Socket System
        private Dictionary<SocketAuthoring.SocketType, Transform> _socketCache = new Dictionary<SocketAuthoring.SocketType, Transform>();
        
        private bool _weaponParentCacheInitialized = false;

        /// <summary>
        /// Attempts to find an Equipment Provider on the player hierarchy.
        /// </summary>
        private void InitializeProvider()
        {
            if (_equipmentProvider == null)
            {
                if (EquipmentProviderMono != null && EquipmentProviderMono is IEquipmentProvider provider)
                {
                    _equipmentProvider = provider;
                }
                else
                {
                    // Search for any component implementing interface
                    // This is tricky in Unity < 2020 without GetComponent<Interface>
                    // So we search common candidates or rely on inspector assignment
                    var providers = GetComponentsInChildren<MonoBehaviour>().OfType<IEquipmentProvider>();
                    _equipmentProvider = providers.FirstOrDefault();
                    
                    if (_equipmentProvider != null && DebugLogging)
                    {
                        Debug.Log($"[WeaponEquipVisualBridge] Auto-found provider: {(_equipmentProvider as MonoBehaviour).name}");
                    }
                }
            }
        }

        private void InitializeEntityManager()
        {
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            }
        }

        /// <summary>
        /// Initialize both the Legacy Weapon Parent cache (ObjectIdentifier) 
        /// and the New Socket System cache (SocketAuthoring).
        /// </summary>
        private void InitializeWeaponParentCache()
        {
            if (_weaponParentCacheInitialized) return;
            _weaponParentCacheInitialized = true;

            // 1. LEGACY: Find Items containers by Searching for ObjectIdentifier components
            var identifiers = GetComponentsInChildren<ObjectIdentifier>(true);
            foreach (var identifier in identifiers)
            {
                if (identifier.ID != 0)
                {
                    _weaponParentCache[identifier.ID] = identifier.transform;
                }
            }

            // 2. NEW: Find standard Sockets by searching for SocketAuthoring components
            // This enables the "Universal Grip" system (EPIC 14.16)
            var sockets = GetComponentsInChildren<SocketAuthoring>(true);
            foreach (var socket in sockets)
            {
                if (!_socketCache.ContainsKey(socket.Type))
                {
                    _socketCache[socket.Type] = socket.transform;
                    if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Cached Socket: {socket.Type} on {socket.name}");
                }
            }

            if (DebugLogging)
            {
                Debug.Log($"[WeaponEquipVisualBridge] Cache Initialized. Legacy Parents: {_weaponParentCache.Count}, Sockets: {_socketCache.Count}");
            }
        }

        /// <summary>
        /// Hybrid Lookup System:
        /// 1. If weapon has ItemGripAuthoring AND character has matching Socket -> Use Socket (New System)
        /// 2. If no Grip or no Socket -> Fallback to Legacy ObjectIdentifier ID lookup
        /// </summary>
        private Transform FindWeaponParent(uint wieldTargetID, Transform defaultHand, GameObject weaponPrefab = null)
        {
            if (!_weaponParentCacheInitialized) InitializeWeaponParentCache();

            // --- STRATEGY 1: UNIVERSAL SOCKET (New) ---
            // If the weapon prefab has "ItemGripAuthoring", it implies it wants to use the Socket System.
            if (weaponPrefab != null)
            {
                var grip = weaponPrefab.GetComponent<ItemGripAuthoring>();
                if (grip != null)
                {
                    // For now, we assume weapons go to MainHand unless specified otherwise (TODO: Grip could specify socket type)
                    // You might expand ItemGripAuthoring to include "TargetSocket" enum
                    if (_socketCache.TryGetValue(SocketAuthoring.SocketType.MainHand, out Transform mainHandSocket))
                    {
                        if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Using Universal Socket for {weaponPrefab.name}");
                        return mainHandSocket;
                    }
                }
            }

            // --- STRATEGY 2: LEGACY PARENT (Old) ---
            if (wieldTargetID == 0) return defaultHand;

            if (_weaponParentCache.TryGetValue(wieldTargetID, out Transform parent))
            {
                return parent;
            }

            if (DebugLogging) Debug.LogWarning($"[WeaponEquipVisualBridge] Parent ID {wieldTargetID} not found, using default.");
            return defaultHand;
        }

        // ==================== MISSING FIELD RESTORATION ====================
        private const int ITEMID_DUAL_PISTOL = 2;
        private const int ITEMID_BOW = 4;
        private bool _magicCastingLockMovement = false;
        public bool CancelCastOnMove = false;

        /// <summary>
        /// Reads the ItemAnimationConfig component from the weapon entity.
        /// Returns default config if missing.
        /// </summary>
        private ItemAnimationConfig GetAnimationConfig(Entity itemEntity)
        {
            if (itemEntity == Entity.Null) return ItemAnimationConfig.Default;
            
            if (_entityManager == default) InitializeEntityManager();
            
            if (_entityManager != default && _entityManager.HasComponent<ItemAnimationConfig>(itemEntity))
            {
                return _entityManager.GetComponentData<ItemAnimationConfig>(itemEntity);
            }
            
            // Fallback for non-ECS items or missing config
            return ItemAnimationConfig.Default;
        }

        private int _hashSlotItemID;
        private int _hashSlotItemState;
        private int _hashSlotItemSubstate; // Added for Melee support
        private int _hashSlotItemChange;
        private int _hashAiming;
        private int _hashMovementSetID;
        
        // Slot1 hashes - cached for Upperbody Layer (used by bow)
        private int _hashSlot1ItemID;
        private int _hashSlot1ItemState;
        private int _hashSlot1ItemSubstate;
        private int _hashSlot1ItemChange;
        
        // ==================== SHIELD STATE TRACKING ====================
        private bool _shieldBlocking = false;
        
        // ==================== MAGIC STATE TRACKING ====================
        private int _magicSpellIndex = 0;
        private bool _magicCasting = false;
        private float _magicCastTimer = 0f;
        
        // ==================== WEAPON INPUT STATE ====================
        // Melee combo tracking
        private int _meleeComboIndex = 0;
        private float _meleeComboTimer = 0f;
        private const float COMBO_WINDOW = 0.8f;
        
        // Bow draw tracking
        private bool _bowDrawing = false;
        private float _bowDrawProgress = 0f;
        private bool _bowReleasing = false;  // True while playing release animation
        private float _bowReleaseAnimTimer = 0f; // Timer to track release animation duration
        private const float BOW_RELEASE_ANIM_DURATION = 0.4f; // How long release animation plays before returning to aim/idle
        private string _currentBowState = "Idle"; // Track current bow animation state to avoid redundant crossfades
        private bool _bowDiagnosticLogged = false; // One-time diagnostic log flag
        
        // Input state tracking (to detect press/hold/release)
        private bool _wasLeftMouseDown = false;
        private bool _wasRightMouseDown = false;
        private bool _isAiming = false;
        
        // Fire rate limiting for auto-fire
        private float _lastFireTime = 0f;
        private const float AUTO_FIRE_RATE = 0.1f; // 10 shots per second for auto weapons

        // Opsive integration - hook into animation update to apply bow params AFTER Opsive resets them
        private UltimateCharacterLocomotion _characterLocomotion;
        private bool _hookedToOpsive = false;

        /// <summary>
        /// Check if the current paradigm uses RMB for camera orbit (e.g. MMO mode).
        /// If true, RMB cannot be used for aiming.
        /// </summary>
        private bool IsOrbitMode()
        {
            if (ParadigmStateMachine.Instance != null && ParadigmStateMachine.Instance.ActiveProfile != null)
            {
                // EPIC 15.21: Check capability (OrbitMode) instead of Paradigm Enum
                return ParadigmStateMachine.Instance.ActiveProfile.cameraOrbitMode == CameraOrbitMode.ButtonHoldOrbit;
            }
            return false;
        }

        private void Start()
        {
            // DO NOT override SlotItemIDs - let the weapon prefabs provide correct AnimatorItemID
            // ClimbingDemo Controller uses these ItemIDs (from transition conditions):
            // Assault Rifle = 1, Pistol = ?, Knife = 23, Katana = 24, Bow = 4, Rocket = 6
            // The ItemID is read from CharacterItem.ItemTypeId at runtime, not from this array
            
            // FORCE correct SlotMovementSetIDs (0=Guns, 1=Melee, 2=Bow)
            // Slot 1 (Rifle) = 0, Slot 2 (Katana) = 1, Slot 3 (Knife) = 1
            SlotMovementSetIDs = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0 };
            
            // Auto-find Animator - MUST have a RuntimeAnimatorController with layers!
            FindValidAnimator();
            InitializeProvider();
            InitializeWeaponParentCache();

            if (PlayerAnimator != null && DebugLogging)
            {
                string ctrlName = PlayerAnimator.runtimeAnimatorController != null 
                    ? PlayerAnimator.runtimeAnimatorController.name 
                    : "NULL";
                Debug.Log($"[WeaponEquipVisualBridge] Found Animator on '{PlayerAnimator.gameObject.name}' Controller={ctrlName} Layers={PlayerAnimator.layerCount}");
            }
            else if (DebugLogging)
                Debug.LogWarning("[WeaponEquipVisualBridge] No valid Animator found with controller!");

            // Auto-heal stale inspector values
            if (ParamSlotItemState == "Slot0ItemState") ParamSlotItemState = "Slot0ItemStateIndex";
            if (ParamSlotItemChange == "Slot0ItemStateChange") ParamSlotItemChange = "Slot0ItemStateIndexChange"; // Just in case

            if (!string.IsNullOrEmpty(ParamSlotItemID)) _hashSlotItemID = Animator.StringToHash(ParamSlotItemID);
            if (!string.IsNullOrEmpty(ParamSlotItemState)) _hashSlotItemState = Animator.StringToHash(ParamSlotItemState);
            if (!string.IsNullOrEmpty(ParamSlotItemSubstate)) _hashSlotItemSubstate = Animator.StringToHash(ParamSlotItemSubstate);
            if (!string.IsNullOrEmpty(ParamSlotItemChange)) _hashSlotItemChange = Animator.StringToHash(ParamSlotItemChange);
            if (!string.IsNullOrEmpty(ParamAiming)) _hashAiming = Animator.StringToHash(ParamAiming);
            if (!string.IsNullOrEmpty(ParamMovementSetID)) _hashMovementSetID = Animator.StringToHash(ParamMovementSetID);
            
            // Cache Slot1 hashes for Upperbody Layer (used by bow)
            _hashSlot1ItemID = Animator.StringToHash("Slot1ItemID");
            _hashSlot1ItemState = Animator.StringToHash("Slot1ItemStateIndex");
            _hashSlot1ItemSubstate = Animator.StringToHash("Slot1ItemSubstateIndex");
            _hashSlot1ItemChange = Animator.StringToHash("Slot1ItemStateIndexChange");
            
            // Verify ALL critical parameters exist in animator
            if (DebugLogging && PlayerAnimator != null)
            {
                string[] criticalParams = { ParamSlotItemID, ParamSlotItemState, ParamSlotItemSubstate, ParamSlotItemChange, ParamAiming, ParamMovementSetID };
                foreach (var paramName in criticalParams)
                {
                    if (string.IsNullOrEmpty(paramName)) continue;
                    
                    bool found = false;
                    foreach (var param in PlayerAnimator.parameters)
                    {
                        if (param.name == paramName)
                        {
                            found = true;
                            Debug.Log($"[WEAPON_DEBUG] ANIMATOR_PARAM Found '{paramName}' type={param.type}");
                            break;
                        }
                    }
                    if (!found)
                    {
                        Debug.LogWarning($"[WEAPON_DEBUG] ANIMATOR_PARAM MISSING: '{paramName}' - animations will NOT work!");
                    }
                }
                
                // Log animator controller info for debugging missing states
                LogAnimatorControllerInfo();
            }

            if (EventRelay == null) EventRelay = GetComponent<WeaponAnimationEventRelay>();
            if (HandIK == null) HandIK = GetComponent<DigOpsiveIK>();

            // Fix references that might be pointing to prefab assets (Ghost instantiation issue)
            SanitizeReference(ref HandAttachPoint);
            SanitizeReference(ref BackAttachPoint);
            
            for (int i = 0; i < WeaponModels.Length; i++)
            {
                if (WeaponModels[i] != null)
                {
                    GameObject go = WeaponModels[i];
                    if (!go.scene.IsValid()) // Points to asset
                    {
                        var child = FindChildByName(transform, go.name);
                        if (child != null)
                        {
                            WeaponModels[i] = child.gameObject;
                            if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Repaired reference for {go.name}");
                        }
                        else
                        {
                            // Fallback: Instantiate the prefab since it's missing from hierarchy
                            // Disable the prefab first to prevent Awake() from running on Opsive components
                            bool wasActive = go.activeSelf;
                            go.SetActive(false);
                            var instance = Instantiate(go, transform);
                            go.SetActive(wasActive); // Restore prefab state

                            instance.name = go.name; // Keep name clean

                            // Remove Opsive CharacterItem components to prevent initialization errors
                            // These weapon models are visual-only and don't need Opsive's item system
                            var opsiveItems = instance.GetComponentsInChildren<Opsive.UltimateCharacterController.Items.CharacterItem>(true);
                            foreach (var opsiveItem in opsiveItems)
                            {
                                Destroy(opsiveItem);
                            }

                            WeaponModels[i] = instance;
                            if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Instantiated missing weapon model: {go.name}");
                        }
                    }
                }
                
                // Ensure hidden
                if (WeaponModels[i] != null)
                    WeaponModels[i].SetActive(false);
            }
            
            // Hook into Opsive's animation update callback
            // This is CRITICAL: Opsive's AnimationMonitorBase.UpdateDirtyAbilityAnimatorParameters() resets
            // Slot0ItemStateIndex to 0 (Idle) every frame if no Opsive ItemAbility is active.
            // By registering our callback on OnAnimationUpdate, we run AFTER Opsive resets the params,
            // allowing us to re-apply bow animation state and prevent flickering.
            HookToOpsiveAnimationUpdate();
        }

        private void OnDestroy()
        {
            UnhookFromOpsiveAnimationUpdate();
        }

        /// <summary>
        /// Hook into Opsive's OnAnimationUpdate callback.
        /// This runs AFTER Opsive's AnimationMonitorBase resets parameters to 0,
        /// allowing us to re-apply bow state and prevent flickering.
        /// </summary>
        private void HookToOpsiveAnimationUpdate()
        {
            if (_hookedToOpsive) return;

            // Find the UltimateCharacterLocomotion on this character
            _characterLocomotion = GetComponentInParent<UltimateCharacterLocomotion>();
            if (_characterLocomotion == null)
            {
                _characterLocomotion = GetComponent<UltimateCharacterLocomotion>();
            }

            if (_characterLocomotion != null)
            {
                _characterLocomotion.OnAnimationUpdate += OnOpsiveAnimationUpdate;
                _hookedToOpsive = true;
                if (DebugLogging)
                {
                    Debug.Log("[WeaponEquipVisualBridge] Hooked to Opsive OnAnimationUpdate callback - bow flickering fix enabled");
                }
            }
            else
            {
                // Fallback: Opsive not found, rely on Update/LateUpdate approach
                if (DebugLogging)
                {
                    Debug.LogWarning("[WeaponEquipVisualBridge] UltimateCharacterLocomotion not found - using fallback timing for bow animations");
                }
            }
        }

        private void UnhookFromOpsiveAnimationUpdate()
        {
            if (_hookedToOpsive && _characterLocomotion != null)
            {
                _characterLocomotion.OnAnimationUpdate -= OnOpsiveAnimationUpdate;
                _hookedToOpsive = false;
            }
        }

        /// <summary>
        /// Called by Opsive's OnAnimationUpdate callback AFTER AnimationMonitorBase.UpdateDirtyAbilityAnimatorParameters().
        /// This is the key fix: Opsive resets unused slots to 0 (Idle), so we re-apply bow state here.
        /// </summary>
        private void OnOpsiveAnimationUpdate()
        {
            if (!_isLocalPlayer || PlayerAnimator == null) return;

            // Only for bow (ItemID=4)
            int currentItemID = _hashSlotItemID != 0 ? PlayerAnimator.GetInteger(_hashSlotItemID) : 0;
            if (currentItemID != 4) return;

            // Determine correct state from FLAGS (these are maintained in Update/HandleBowInput)
            int stateIndex = 0;
            if (_bowReleasing)
                stateIndex = 4; // Attack Release
            else if (_bowDrawing)
                stateIndex = 3; // Attack Pull Back
            else if (_isAiming)
                stateIndex = 2; // Aim
            else
                stateIndex = 0; // Idle

            // CRITICAL: Re-apply the bow state AFTER Opsive reset it to 0
            // Only do this for non-idle states (idle is fine at 0)
            if (stateIndex != 0)
            {
                // Set Slot0 parameters (what Opsive just reset to 0)
                if (_hashSlotItemState != 0)
                    PlayerAnimator.SetInteger(_hashSlotItemState, stateIndex);
                if (_hashSlotItemSubstate != 0)
                    PlayerAnimator.SetInteger(_hashSlotItemSubstate, 0);

                // Also set Slot1 parameters for layers that check Slot1
                if (_hashSlot1ItemID != 0)
                    PlayerAnimator.SetInteger(_hashSlot1ItemID, 4);
                if (_hashSlot1ItemState != 0)
                    PlayerAnimator.SetInteger(_hashSlot1ItemState, stateIndex);
                if (_hashSlot1ItemSubstate != 0)
                    PlayerAnimator.SetInteger(_hashSlot1ItemSubstate, 0);

                if (BowDebugLogging && Time.frameCount % 60 == 0)
                {
                    string stateName = stateIndex switch { 2 => "Aim", 3 => "Attack Pull Back", 4 => "Attack Release", _ => "Unknown" };
                    Debug.Log($"[BOW_DEBUG] OPSIVE_CALLBACK re-applied stateIndex={stateIndex} ({stateName}) after Opsive reset");
                }
            }
        }
        
        /// <summary>
        /// Override bow animator parameters at the end of the frame.
        /// This runs after Opsive's AnimatorMonitor and ensures our values persist.
        /// Called from LateUpdate when bow is equipped.
        /// </summary>
        private void OverrideBowAnimatorParameters()
        {
            if (!_isLocalPlayer || PlayerAnimator == null) return;
            
            // Only for bow (ItemID=4)
            int currentItemID = _hashSlotItemID != 0 ? PlayerAnimator.GetInteger(_hashSlotItemID) : 0;
            if (currentItemID != 4) return;
            
            // Determine correct state from FLAGS
            int stateIndex = 0;
            if (_bowReleasing)
                stateIndex = 4; // Attack Release
            else if (_bowDrawing)
                stateIndex = 3; // Attack Pull Back
            else if (_isAiming)
                stateIndex = 2; // Aim
            else
                stateIndex = 0; // Idle
            
            // Don't override if we're in Idle state
            if (stateIndex == 0) return;
            
            // Set bow parameters - these will persist until next frame
            if (_hashSlot1ItemID != 0)
                PlayerAnimator.SetInteger(_hashSlot1ItemID, 4);
            if (_hashSlot1ItemState != 0)
                PlayerAnimator.SetInteger(_hashSlot1ItemState, stateIndex);
            if (_hashSlot1ItemSubstate != 0)
                PlayerAnimator.SetInteger(_hashSlot1ItemSubstate, 0);
            if (_hashSlotItemState != 0)
                PlayerAnimator.SetInteger(_hashSlotItemState, stateIndex);
            if (_hashSlotItemSubstate != 0)
                PlayerAnimator.SetInteger(_hashSlotItemSubstate, 0);
            
            // Log periodically during active bow states
            if (BowDebugLogging && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[BOW_DEBUG] LATE_UPDATE_OVERRIDE stateIndex={stateIndex} (frame {Time.frameCount})");
            }
        }
        
        private void SanitizeReference(ref Transform refTransform)
        {
            if (refTransform != null && !refTransform.gameObject.scene.IsValid())
            {
                var child = FindChildByName(transform, refTransform.name);
                if (child != null)
                {
                    refTransform = child;
                    if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Repaired reference for {refTransform.name}");
                }
            }
        }
        
        private Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var result = FindChildByName(child, name);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Find an Animator that has a valid RuntimeAnimatorController with layers.
        /// The issue is that some Animators exist but have no controller assigned.
        /// </summary>
        private void FindValidAnimator()
        {
            // If already assigned and valid, keep it
            if (PlayerAnimator != null && 
                PlayerAnimator.runtimeAnimatorController != null && 
                PlayerAnimator.layerCount > 0)
            {
                return;
            }
            
            // Search order: self, children, parent
            Animator[] candidates = new Animator[0];
            
            // 1. Check self
            var selfAnim = GetComponent<Animator>();
            if (IsValidAnimator(selfAnim))
            {
                PlayerAnimator = selfAnim;
                return;
            }
            
            // 2. Check children (include inactive)
            var childAnimators = GetComponentsInChildren<Animator>(true);
            foreach (var anim in childAnimators)
            {
                if (IsValidAnimator(anim))
                {
                    PlayerAnimator = anim;
                    if (DebugLogging)
                        Debug.Log($"[WeaponEquipVisualBridge] Found valid child Animator on '{anim.gameObject.name}'");
                    return;
                }
            }
            
            // 3. Check parent
            var parentAnimators = GetComponentsInParent<Animator>(true);
            foreach (var anim in parentAnimators)
            {
                if (IsValidAnimator(anim))
                {
                    PlayerAnimator = anim;
                    if (DebugLogging)
                        Debug.Log($"[WeaponEquipVisualBridge] Found valid parent Animator on '{anim.gameObject.name}'");
                    return;
                }
            }
            
            // 4. Fallback: take any Animator even if no controller (might be assigned later)
            if (selfAnim != null)
                PlayerAnimator = selfAnim;
            else if (childAnimators.Length > 0)
                PlayerAnimator = childAnimators[0];
            else if (parentAnimators.Length > 0)
                PlayerAnimator = parentAnimators[0];
                
            if (PlayerAnimator != null && DebugLogging)
            {
                Debug.LogWarning($"[WeaponEquipVisualBridge] Using Animator on '{PlayerAnimator.gameObject.name}' but controller is {(PlayerAnimator.runtimeAnimatorController != null ? "assigned" : "NULL")}!");
            }
        }
        
        private bool IsValidAnimator(Animator anim)
        {
            return anim != null && 
                   anim.runtimeAnimatorController != null && 
                   anim.layerCount > 0;
        }

        /// <summary>
        /// Update runs BEFORE the animator evaluates transitions.
        /// We use this to maintain bow Slot1 parameters so exit transitions don't fire.
        /// CRITICAL: Must use the actual state FLAGS (_bowDrawing, _isAiming, _bowReleasing)
        /// NOT _currentBowState, because that gets set in LateUpdate AFTER Update runs.
        /// </summary>
        private void Update()
        {
            if (!_isLocalPlayer || PlayerAnimator == null) return;
            
            // Only for bow (ItemID=4)
            int currentItemID = _hashSlotItemID != 0 ? PlayerAnimator.GetInteger(_hashSlotItemID) : 0;
            if (currentItemID != 4) return;
            
            // EPIC 15.21: EARLY INPUT DETECTION using PlayerInputState
            // This prevents the 1-frame delay that was causing state cycling
            bool fireHeld = global::Player.Systems.PlayerInputState.Fire;
            bool aimHeld = global::Player.Systems.PlayerInputState.Aim;
            
            bool leftPressed = fireHeld && !_wasLeftMouseDown;
            bool leftHeld = fireHeld;
            bool leftReleased = !fireHeld && _wasLeftMouseDown;
            
            bool rightPressed = aimHeld && !_wasRightMouseDown;
            bool rightHeld = aimHeld;
            bool rightReleased = !aimHeld && _wasRightMouseDown;
            
            // Update tracking
            _wasLeftMouseDown = fireHeld;
            _wasRightMouseDown = aimHeld;
            
            // Update flags immediately on input (same logic as HandleBowInput, but earlier)
            // Only handle NEW transitions here - don't override existing states
            if (!_bowReleasing && !_bowDrawing)
            {
                if (leftPressed)
                {
                    _bowDrawing = true;
                    _bowDrawProgress = 0f;
                    if (BowDebugLogging) Debug.Log("[BOW_DEBUG] UPDATE_EARLY: Start draw (leftPressed)");
                }
                else if (rightPressed && !_isAiming && !IsOrbitMode())
                {
                    _isAiming = true;
                    if (BowDebugLogging) Debug.Log("[BOW_DEBUG] UPDATE_EARLY: Start aim (rightPressed)");
                }
            }
            
            // Handle release in Update too for same-frame response
            if (_bowDrawing && leftReleased)
            {
                _bowDrawing = false;
                _bowReleasing = true;
                _bowReleaseAnimTimer = 0f;
                if (BowDebugLogging) Debug.Log("[BOW_DEBUG] UPDATE_EARLY: Fire! (leftReleased)");
            }
            
            // Handle aim release
            if (_isAiming && !_bowDrawing && rightReleased)
            {
                _isAiming = false;
                if (BowDebugLogging) Debug.Log("[BOW_DEBUG] UPDATE_EARLY: Stop aim (rightReleased)");
            }
            
            // Determine correct state from FLAGS (these persist across frames)
            // Priority: Releasing > Drawing > Aiming > Idle
            int stateIndex = 0;
            string debugState = "Idle";
            if (_bowReleasing)
            {
                stateIndex = 4; // Attack Release
                debugState = "Attack Release";
            }
            else if (_bowDrawing)
            {
                stateIndex = 3; // Attack Pull Back
                debugState = "Attack Pull Back";
            }
            else if (_isAiming)
            {
                stateIndex = 2; // Aim
                debugState = "Aim";
            }
            else
            {
                stateIndex = 0; // Idle
                debugState = "Idle";
            }
            
            // BOW DEBUG: Log what we're setting in Update (before animator evaluates)
            if (BowDebugLogging && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[BOW_DEBUG] UPDATE_PARAMS stateIndex={stateIndex} ({debugState}) | flags: drawing={_bowDrawing} aiming={_isAiming} releasing={_bowReleasing}");
            }
            
            // Set all Slot1 parameters BEFORE animator update
            if (_hashSlot1ItemID != 0)
                PlayerAnimator.SetInteger(_hashSlot1ItemID, 4);
            if (_hashSlot1ItemState != 0)
                PlayerAnimator.SetInteger(_hashSlot1ItemState, stateIndex);
            if (_hashSlot1ItemSubstate != 0)
                PlayerAnimator.SetInteger(_hashSlot1ItemSubstate, 0);
            
            // ALSO set Slot0 parameters for consistency - some transitions may check Slot0
            if (_hashSlotItemState != 0)
                PlayerAnimator.SetInteger(_hashSlotItemState, stateIndex);
            if (_hashSlotItemSubstate != 0)
                PlayerAnimator.SetInteger(_hashSlotItemSubstate, 0);
        }

        private void LateUpdate()
        {
            // ==================== LOCAL PLAYER CHECK ====================
            // Update _isLocalPlayer from the equipment provider's ECS state
            // This ensures we only process input for the player owned by this client
            if (_equipmentProvider is DIGEquipmentProvider digProvider)
            {
                _isLocalPlayer = digProvider.IsLocalPlayer;
            }

            // ==================== WEAPON INPUT HANDLING ====================
            // ONLY process input for LOCAL player - skip for server ghosts/remote players
            if (_isLocalPlayer)
            {
                HandleWeaponInput();
            }
            
            // Update melee combo timer
            if (_meleeComboTimer > 0)
            {
                _meleeComboTimer -= Time.deltaTime;
                if (_meleeComboTimer <= 0)
                {
                    _meleeComboIndex = 0; // Reset combo
                }
            }
            
            // Periodic re-check for valid animator (controller might be assigned late)
            if (Time.frameCount % 60 == 0 && !IsValidAnimator(PlayerAnimator))
            {
                FindValidAnimator();
            }
            
            // BOW DEBUG: Log upperbody layer state every 60 frames when bow equipped
            int currentItemID = PlayerAnimator != null && _hashSlotItemID != 0 ? PlayerAnimator.GetInteger(_hashSlotItemID) : 0;
            bool isBowEquipped = currentItemID == 4;
            
            // One-time bow layer diagnostic when first equipped
            if (BowDebugLogging && isBowEquipped && !_bowDiagnosticLogged && PlayerAnimator != null)
            {
                _bowDiagnosticLogged = true;
                int upperbodyIdx = GetUpperbodyLayerIndex();
                Debug.Log($"[BOW_DEBUG] ===== BOW LAYER DIAGNOSTIC =====");
                Debug.Log($"[BOW_DEBUG] Upperbody Layer Index: {upperbodyIdx}");
                Debug.Log($"[BOW_DEBUG] Total Layers: {PlayerAnimator.layerCount}");
                
                // List all layers
                for (int i = 0; i < PlayerAnimator.layerCount; i++)
                {
                    string layerName = PlayerAnimator.GetLayerName(i);
                    float layerWeight = PlayerAnimator.GetLayerWeight(i);
                    var stateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(i);
                    Debug.Log($"[BOW_DEBUG]   Layer[{i}] = '{layerName}' Weight={layerWeight:F2} CurrentHash={stateInfo.shortNameHash}");
                }
                
                // Check Bow state hashes
                Debug.Log($"[BOW_DEBUG] Bow state hashes:");
                Debug.Log($"[BOW_DEBUG]   'Bow.Idle' = {Animator.StringToHash("Bow.Idle")}");
                Debug.Log($"[BOW_DEBUG]   'Bow.Aim' = {Animator.StringToHash("Bow.Aim")}");
                Debug.Log($"[BOW_DEBUG]   'Bow.Attack Pull Back' = {Animator.StringToHash("Bow.Attack Pull Back")}");
                Debug.Log($"[BOW_DEBUG]   'Bow.Attack Release' = {Animator.StringToHash("Bow.Attack Release")}");
                Debug.Log($"[BOW_DEBUG] ================================");
            }
            
            if (BowDebugLogging && isBowEquipped && PlayerAnimator != null && Time.frameCount % 60 == 0)
            {
                // Monitor BOTH Base Layer (0) and Upperbody Layer (4) to see what's actually happening
                int upperbodyLayer = GetUpperbodyLayerIndex();
                
                // Base Layer (0) state
                var baseStateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(0);
                int stateIdx = PlayerAnimator.GetInteger(_hashSlotItemState);
                int slot1StateIdx = PlayerAnimator.GetInteger(_hashSlot1ItemState);
                
                // Upperbody Layer (4) state - THIS is where we Play() bow animations
                var upperbodyStateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(upperbodyLayer);
                float upperbodyWeight = PlayerAnimator.GetLayerWeight(upperbodyLayer);
                
                // Check what state names those hashes match on Upperbody Layer
                string upperbodyStateName = "UNKNOWN";
                if (upperbodyStateInfo.IsName("Bow.Idle")) upperbodyStateName = "Bow.Idle";
                else if (upperbodyStateInfo.IsName("Bow.Aim")) upperbodyStateName = "Bow.Aim";
                else if (upperbodyStateInfo.IsName("Bow.Attack Pull Back")) upperbodyStateName = "Bow.Attack Pull Back";
                else if (upperbodyStateInfo.IsName("Bow.Attack Release")) upperbodyStateName = "Bow.Attack Release";
                else if (upperbodyStateInfo.IsName("Idle")) upperbodyStateName = "Idle";
                
                Debug.Log($"[BOW_DEBUG] FRAME_CHECK Slot0StateIdx={stateIdx} Slot1StateIdx={slot1StateIdx} TrackedState='{_currentBowState}' | Layer4(Upperbody) Weight={upperbodyWeight:F2} State='{upperbodyStateName}' Hash={upperbodyStateInfo.shortNameHash} NormTime={upperbodyStateInfo.normalizedTime:F2}");
            }
            
            // Stuck detection for non-bow weapons only
            if (PlayerAnimator != null && !isBowEquipped)
            {
                for (int i = 0; i < PlayerAnimator.layerCount; i++)
                {
                    string layerName = PlayerAnimator.GetLayerName(i);
                    if (layerName == "Upperbody Layer")
                    {
                        var stateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(i);
                        
                        string stateName = "UNKNOWN";
                        if (stateInfo.IsName("Idle")) stateName = "Idle";
                        else if (stateInfo.IsName("Aim")) stateName = "Aim";
                        
                        // CRITICAL FIX: If we're in generic Idle/Unknown and have a weapon equipped,
                        // force transition to the correct weapon sub-state machine
                        bool isInGenericState = (stateName == "Idle" || stateName == "UNKNOWN");
                        bool hasWeaponEquipped = currentItemID > 0;
                        bool isInWeaponSubState = stateName.StartsWith("SM:");
                        
                        // Cooldown to avoid spamming CrossFade every frame
                        _subStateForceTimer -= Time.deltaTime;
                        
                        // Retry every 0.2 seconds if still stuck
                        // EXCEPTION: Skip bows (ItemID=4) - HandleBowInput controls their animation state
                        bool isBow = currentItemID == 4;
                        bool stuckCondition = isInGenericState && hasWeaponEquipped && !isInWeaponSubState && !isBow;
                        bool shouldForce = stuckCondition && _subStateForceTimer <= 0f;
                        if (shouldForce)
                        {
                            if (DebugLogging) Debug.LogWarning($"[WEAPON_DEBUG] UPPERBODY_STUCK forcing transition for ItemID={currentItemID}");
                            ForceWeaponSubStateMachine(currentItemID);
                            _lastForcedSubStateItemID = currentItemID;
                            _subStateForceTimer = 0.2f;
                        }
                        
                        if (isInWeaponSubState)
                        {
                            _lastForcedSubStateItemID = -1;
                        }
                        break;
                    }
                }
            }
            
            if (_equipmentProvider == null) return;
            
            // Read current equip state from provider
            var mainItem = _equipmentProvider.GetEquippedItem(0);
            
            // Map ItemID -> QuickSlot Index (Visual)
            int currentQuickSlot = FindQuickSlotForItemID(mainItem.AnimatorItemID);
            Entity weaponEntity = mainItem.ItemEntity;
            int animatorItemID = mainItem.AnimatorItemID;
            int movementSetID = mainItem.MovementSetID;
            
            // Check if equipped item changed
            if (currentQuickSlot != _lastEquippedSlot)
            {
                // Reset bow state usage
                if (_bowDrawing || _bowReleasing || _isAiming)
                {
                    _bowDrawing = false;
                    _bowReleasing = false;
                    _isAiming = false;
                    _currentBowState = "Idle";
                    if (DebugLogging)
                        Debug.Log("[WeaponEquipVisualBridge] Reset bow state tracking on weapon switch");
                }

                // Reset reload state tracking on weapon switch
                // FIX: Prevents input blocking when switching from reloading weapon to grenade/throwable
                if (_reloadAnimationTriggered || _lastIsReloading)
                {
                    _reloadAnimationTriggered = false;
                    _lastIsReloading = false;
                    if (DebugLogging)
                        Debug.Log("[WeaponEquipVisualBridge] Reset reload state tracking on weapon switch");
                }

                // Set MovementSetID for weapon type (MUST be set before item state for proper transitions)
                if (_hashMovementSetID != 0)
                {
                    int prevVal = PlayerAnimator.GetInteger(_hashMovementSetID);
                    PlayerAnimator.SetInteger(_hashMovementSetID, movementSetID);
                    if (DebugLogging) Debug.Log($"[ANIMATOR_PARAM] SET MovementSetID: {prevVal} -> {movementSetID}");
                }

                UpdateWeaponVisuals(currentQuickSlot, weaponEntity, animatorItemID);
                _lastEquippedSlot = currentQuickSlot;
                _lastEquippedEntity = weaponEntity;
            }
            
            // ==================== OFF-HAND VISUAL UPDATE ====================
            // Check if off-hand has changed and update visuals
            // Get QuickSlot from DIGEquipmentProvider's ECS state (ActiveEquipmentSlot.OffHandQuickSlot)
            int offHandQuickSlot = 0;
            Entity offHandEntity = Entity.Null;
            int offHandAnimatorItemID = 0;
            
            if (_equipmentProvider is DIGEquipmentProvider digProviderForOffHand && digProviderForOffHand.EntityWorld?.EntityManager != null)
            {
                var em = digProviderForOffHand.EntityWorld.EntityManager;
                var playerEntity = digProviderForOffHand.PlayerEntity;
                if (playerEntity != Entity.Null && em.Exists(playerEntity) && em.HasBuffer<EquippedItemElement>(playerEntity))
                {
                    var buffer = em.GetBuffer<EquippedItemElement>(playerEntity);
                    if (buffer.Length > 1)
                    {
                        var offHand = buffer[1];
                        offHandQuickSlot = offHand.QuickSlot;
                        offHandEntity = offHand.ItemEntity;
                        
                        // DEBUG: Log off-hand buffer state every 2 seconds
                        if (DebugLogging && Time.frameCount % 120 == 0)
                            Debug.Log($"[OFF_HAND_DEBUG] Buffer[1]: QuickSlot={offHandQuickSlot} ItemEntity={offHandEntity.Index}:{offHandEntity.Version}");
                    }
                }
            }
            
            // Get AnimatorItemID from off-hand item info
            var offHandItemInfo = _equipmentProvider.GetEquippedItem(1);
            offHandAnimatorItemID = offHandItemInfo.AnimatorItemID;
            
            // DEBUG: Log change detection
            if (offHandQuickSlot != _lastOffHandSlot || offHandEntity != _lastOffHandEntity)
            {
                Debug.Log($"[OFF_HAND_DEBUG] CHANGE DETECTED: QuickSlot {_lastOffHandSlot}->{offHandQuickSlot}, Entity {_lastOffHandEntity.Index}->{offHandEntity.Index}, AnimatorItemID={offHandAnimatorItemID}, LeftHandAttach={(LeftHandAttachPoint != null ? LeftHandAttachPoint.name : "NULL")}");
                UpdateOffHandVisuals(offHandQuickSlot, offHandEntity, offHandAnimatorItemID);
                _lastOffHandSlot = offHandQuickSlot;
                _lastOffHandEntity = offHandEntity;
            }



            // Update Dynamic State (Firing, etc) - use client-side weapon entity
            // Try to get EntityManager from DIG provider if possible
            if (_entityManager == default && _equipmentProvider is DIGEquipmentProvider digProviderForEM)
            {
                if (digProviderForEM.EntityWorld != null)
                    _entityManager = digProviderForEM.EntityWorld.EntityManager;
            }
            
            if (_entityManager != default && _entityManager.World.IsCreated)
            {
                // Periodic debug: Log what entity we're reading and its state
                if (AttackReplicationDebug && Time.frameCount % 120 == 0 && weaponEntity != Entity.Null && _entityManager.Exists(weaponEntity))
                {
                    string playerTag = _isLocalPlayer ? "LOCAL" : "REMOTE";
                    bool hasMelee = _entityManager.HasComponent<MeleeState>(weaponEntity);
                    bool hasFire = _entityManager.HasComponent<WeaponFireState>(weaponEntity);

                    string meleeInfo = "";
                    string fireInfo = "";

                    if (hasMelee)
                    {
                        var ms = _entityManager.GetComponentData<MeleeState>(weaponEntity);
                        meleeInfo = $"IsAttacking={ms.IsAttacking} Combo={ms.CurrentCombo}";
                    }
                    if (hasFire)
                    {
                        var fs = _entityManager.GetComponentData<WeaponFireState>(weaponEntity);
                        fireInfo = $"IsFiring={fs.IsFiring} AnimTimer={fs.FireAnimationTimer:F2}";
                    }

                    Debug.Log($"[ATTACK_REPLICATION] [ANIM_BRIDGE] [{playerTag}] PERIODIC_STATUS " +
                        $"WeaponEntity={weaponEntity.Index} HasMelee={hasMelee} HasFire={hasFire} " +
                        $"{meleeInfo} {fireInfo} AnimatorState={_lastItemState}");
                }

                UpdateWeaponState(weaponEntity, currentQuickSlot);
            }
            
            // ALWAYS update MovementSetID based on weapon type (every frame)
            if (_hashMovementSetID != 0 && PlayerAnimator != null)
            {
                // Trusted value from provider
                PlayerAnimator.SetInteger(_hashMovementSetID, movementSetID);
            }
            
            // CRITICAL: Override bow animator parameters at the END of LateUpdate
            // This ensures our values persist after all other systems (including Opsive) have run
            // The animator will evaluate these values on the NEXT frame's Update
            if (_isLocalPlayer && isBowEquipped)
            {
                OverrideBowAnimatorParameters();
            }
            
            // ==================== DUAL PISTOL OFF-HAND HANDLING ====================
            // Dynamically detect if BOTH hands have pistols equipped for dual-wield mode
                // Variable mainItem already defined in outer scope, rename to avoid conflict
                var mainHandItemForPistol = _equipmentProvider.GetEquippedItem(0);
                int mainHandItemID = mainHandItemForPistol.AnimatorItemID;
                bool mainHandIsPistol = IsPistolItemID(mainHandItemID, mainHandItemForPistol.ItemEntity);
                bool offHandIsPistol = false;
                
                // data-driven check from provider
                var offItem = _equipmentProvider.GetEquippedItem(1);
                if (!offItem.IsEmpty && IsPistolItemID(offItem.AnimatorItemID, offItem.ItemEntity))
                {
                    offHandIsPistol = true;
                }
                
                if (mainHandIsPistol && offHandIsPistol)
                {
                    // DUAL PISTOL MODE: Both hands have pistols
                    if (_hashSlot1ItemID != 0)
                        PlayerAnimator.SetInteger(_hashSlot1ItemID, ITEMID_DUAL_PISTOL);
                    
                    // Mirror state (Idle/Aim/Fire) from Slot0 to Slot1
                    int slot0State = (_hashSlotItemState != 0) ? PlayerAnimator.GetInteger(_hashSlotItemState) : 0;
                    if (_hashSlot1ItemState != 0)
                        PlayerAnimator.SetInteger(_hashSlot1ItemState, slot0State);
                    
                    if (DebugLogging && Time.frameCount % 120 == 0)
                        Debug.Log($"[DUAL_PISTOL] DUAL MODE: MainHand={mainHandItemID} OffHand=Pistol StateIndex={slot0State}");
                }
                else if (mainHandIsPistol)
                {
                    // SINGLE PISTOL MODE: Only main hand has pistol
                    // Clear Slot1 so dual pistol sub-state machine doesn't activate
                    if (_hashSlot1ItemID != 0)
                        PlayerAnimator.SetInteger(_hashSlot1ItemID, 0);
                    
                    if (DebugLogging && Time.frameCount % 120 == 0)
                        Debug.Log($"[DUAL_PISTOL] SINGLE MODE: MainHand={mainHandItemID} OffHand=Empty");
                }
                else
                {
                    // Check if main hand weapon is two-handed using config
                    // If IsTwoHanded, suppress off-hand visuals; otherwise allow off-hand
                    var mainHandConfig = GetAnimationConfig(_equipmentProvider?.GetEquippedItem(0).ItemEntity ?? default);
                    
                    if (mainHandConfig.IsTwoHanded)
                    {
                        // TWO-HANDED WEAPON: Clear Slot1 to suppress off-hand
                        if (_hashSlot1ItemID != 0)
                            PlayerAnimator.SetInteger(_hashSlot1ItemID, 0);
                        
                        if (DebugLogging && Time.frameCount % 120 == 0)
                            Debug.Log($"[OFF_HAND] Suppressed: MainHand={mainHandItemID} is TwoHanded");
                    }
                    else
                    {
                        // ONE-HANDED WEAPON: Check for off-hand item and set Slot1ItemID
                        var offHandSlotItem = _equipmentProvider.GetEquippedItem(1);
                        if (!offHandSlotItem.IsEmpty)
                        {
                            // Set Slot1ItemID to the off-hand item's AnimatorItemID
                            if (_hashSlot1ItemID != 0)
                                PlayerAnimator.SetInteger(_hashSlot1ItemID, offHandSlotItem.AnimatorItemID);

                            // Check for off-hand use (Blocking)
                            // Default to 0 (Idle)
                            int slot1StateIndex = 0;
                            
                            // If ECS entity has OffHandUseRequest input, check if pressed
                            if (_entityManager != default)
                            {
                                Entity pEntity = (_equipmentProvider as DIGEquipmentProvider)?.PlayerEntity ?? Entity.Null;
                                if (pEntity != Entity.Null && _entityManager.Exists(pEntity) && 
                                    _entityManager.HasComponent<OffHandUseRequest>(pEntity))
                                {
                                    var useReq = _entityManager.GetComponentData<OffHandUseRequest>(pEntity);
                                    if (useReq.IsPressed)
                                    {
                                        // For Shield (ID 26), 3 = Block
                                        // Could use switch if other off-hand items have different use states
                                        slot1StateIndex = 3; 
                                    }
                                }
                            }

                            if (_hashSlot1ItemState != 0)
                                PlayerAnimator.SetInteger(_hashSlot1ItemState, slot1StateIndex);
                            
                            if (DebugLogging && Time.frameCount % 120 == 0)
                                Debug.Log($"[OFF_HAND] Active: MainHand={mainHandItemID} OffHand={offHandSlotItem.AnimatorItemID} State={slot1StateIndex}");
                            
                            // Reset unequip state since we have an item equipped
                            _offHandUnequipping = false;
                            _offHandUnequipTimer = 0f;
                            _offHandUnequipItemID = offHandSlotItem.AnimatorItemID; // Track current ID for future unequip
                        }
                        else
                        {
                            // No off-hand equipped - handle unequip transition properly
                            
                            // Check if we need to START an unequip transition
                            if (!_offHandUnequipping && _offHandUnequipItemID > 0)
                            {
                                // We just transitioned from equipped to empty - start unequip animation
                                _offHandUnequipping = true;
                                _offHandUnequipTimer = 0.4f; // Duration for unequip animation
                                
                                // Keep the old ItemID and set state to Unequip (5)
                                if (_hashSlot1ItemID != 0)
                                    PlayerAnimator.SetInteger(_hashSlot1ItemID, _offHandUnequipItemID);
                                if (_hashSlot1ItemState != 0)
                                    PlayerAnimator.SetInteger(_hashSlot1ItemState, 5); // 5 = Unequip
                                    
                                if (DebugLogging)
                                    Debug.Log($"[OFF_HAND] Started Unequip transition for ItemID={_offHandUnequipItemID}");
                            }
                            else if (_offHandUnequipping)
                            {
                                // Currently in unequip animation - countdown timer
                                _offHandUnequipTimer -= Time.deltaTime;
                                
                                // Keep playing unequip animation
                                if (_hashSlot1ItemID != 0)
                                    PlayerAnimator.SetInteger(_hashSlot1ItemID, _offHandUnequipItemID);
                                if (_hashSlot1ItemState != 0)
                                    PlayerAnimator.SetInteger(_hashSlot1ItemState, 5); // Keep at Unequip
                                
                                if (_offHandUnequipTimer <= 0f)
                                {
                                    // Unequip animation complete - now clear
                                    _offHandUnequipping = false;
                                    _offHandUnequipItemID = 0;
                                    
                                    if (_hashSlot1ItemID != 0)
                                        PlayerAnimator.SetInteger(_hashSlot1ItemID, 0);
                                    if (_hashSlot1ItemState != 0)
                                        PlayerAnimator.SetInteger(_hashSlot1ItemState, 0);
                                        
                                    if (DebugLogging)
                                        Debug.Log($"[OFF_HAND] Unequip transition complete - cleared Slot1");
                                }
                            }
                            else
                            {
                                // Already empty and not unequipping - just ensure cleared
                                if (_hashSlot1ItemID != 0)
                                    PlayerAnimator.SetInteger(_hashSlot1ItemID, 0);
                                if (_hashSlot1ItemState != 0)
                                    PlayerAnimator.SetInteger(_hashSlot1ItemState, 0);
                            }
                        }

                        // ================================================================================
                        // LAYER WEIGHT MANAGEMENT
                        // Explicitly set arm layer weights every frame to prevent stale states.
                        // ================================================================================
                        
                        // Conditions for suppressing layers
                        bool suppressRightArm = mainHandConfig.CategoryID == "Melee" && !offHandSlotItem.IsEmpty;
                        bool suppressLeftArm = mainHandConfig.CategoryID == "Shield" && offHandSlotItem.IsEmpty;
                        
                        // Right Arm layers - suppress when Melee + OffHand present (so Sword attack plays from Layer 5)
                        int rightUpperLayer = PlayerAnimator.GetLayerIndex("Right Upperbody Layer");
                        int rightArmLayer = PlayerAnimator.GetLayerIndex("Right Arm Layer");
                        if (rightUpperLayer != -1) PlayerAnimator.SetLayerWeight(rightUpperLayer, suppressRightArm ? 0f : 1f);
                        if (rightArmLayer != -1) PlayerAnimator.SetLayerWeight(rightArmLayer, suppressRightArm ? 0f : 1f);
                        
                        // Left Arm layers - suppress when Shield in MainHand + OffHand empty (prevents ghost shield)
                        int leftUpperLayer = PlayerAnimator.GetLayerIndex("Left Upperbody Layer");
                        int leftArmLayer = PlayerAnimator.GetLayerIndex("Left Arm Layer");
                        if (leftUpperLayer != -1) PlayerAnimator.SetLayerWeight(leftUpperLayer, suppressLeftArm ? 0f : 1f);
                        if (leftArmLayer != -1) PlayerAnimator.SetLayerWeight(leftArmLayer, suppressLeftArm ? 0f : 1f);
                    }
                }
            
            // EPIC 14.17: ECS-driven VFX for remote players (Animation Events are local-only)
            if (_lastEquippedEntity != Entity.Null && _entityManager.Exists(_lastEquippedEntity))
            {
                TriggerECSBasedVFX(_lastEquippedEntity);
            }
        }
        
        /// <summary>
        /// Trigger VFX based on ECS state changes (works for ALL players including remote).
        /// Animation Events only fire locally, so we need ECS-driven VFX for network sync.
        /// Detects EACH shot by tracking when TimeSinceLastShot resets (auto weapons fire continuously).
        /// </summary>
        private void TriggerECSBasedVFX(Entity itemEntity)
        {
            if (!_entityManager.HasComponent<WeaponFireState>(itemEntity)) return;
            
            var fireState = _entityManager.GetComponentData<WeaponFireState>(itemEntity);
            
            // Per-shot detection: TimeSinceLastShot resets to 0 on each shot
            // If current value is significantly smaller than previous, a shot was fired
            bool shotFired = fireState.IsFiring && fireState.TimeSinceLastShot < _lastTimeSinceLastShot - 0.01f;
            
            if (shotFired)
            {
                if (_currentItemVFX != null)
                {
                    _currentItemVFX.PlayVFX("Fire");
                    _currentItemVFX.PlayVFX("ShellEject");
                    
                    if (DebugLogging)
                        Debug.Log($"[WeaponEquipVisualBridge] ECS-triggered Fire VFX for entity {itemEntity.Index} TimeSinceLastShot={fireState.TimeSinceLastShot:F3}");
                }
            }
            
            _lastTimeSinceLastShot = fireState.TimeSinceLastShot;
        }
        
        /// <summary>
        /// Check if an AnimatorItemID represents a pistol-type weapon.
        /// Pistols use ItemID 2, but you can expand this for other pistol variants.
        /// </summary>
        /// <summary>
        /// Check if a weapon entity is configured as a Gun (Pistol).
        /// </summary>
        private bool IsPistolItemID(int animatorItemID, Entity weaponEntity = default)
        {
            var config = GetAnimationConfig(weaponEntity);
            // Fallback to legacy ID check if config is default (for backward compatibility)
            if (config.CategoryID.IsEmpty)
            {
               // Legacy hardcoded ID 2
               return animatorItemID == 2;
            }
            // Logic: Is it a Gun? (Pistol usually implies Gun + specific ID, but here we check type)
            // For dual wield logic, we specifically want pistols, which might be separated from Rifles later.
            // For now, we trust the AnimatorItemID 2 check OR config saying it's a gun with ID 2.
            return config.CategoryID == "Gun" && config.AnimatorItemID == 2;
        }
        
        /// <summary>
        /// Check if a weapon entity is configured as Magic.
        /// </summary>
        private bool IsMagicItemID(int animatorItemID, Entity weaponEntity = default)
        {
            var config = GetAnimationConfig(weaponEntity);
            if (config.CategoryID.IsEmpty)
            {
                // Legacy hardcoded range
                return animatorItemID >= 61 && animatorItemID <= 65;
            }
            return config.CategoryID == "Magic";
        }
        
        /// <summary>
        /// Check if a weapon entity is configured as a Shield.
        /// </summary>
        private bool IsShieldItemID(int animatorItemID, Entity weaponEntity = default)
        {
            var config = GetAnimationConfig(weaponEntity);
            if (config.CategoryID.IsEmpty)
            {
                // Legacy hardcoded ID
                return animatorItemID == 26;
            }
            return config.CategoryID == "Shield";
        }
        
        
        /// <summary>
        /// Helper to find which QuickSlot index corresponds to an AnimatorItemID.
        /// This decouples the "Visual Slot Index" from the "Logical Slot Index".
        /// </summary>
        private int FindQuickSlotForItemID(int itemID)
        {
            if (itemID <= 0) return -1;
            
            for (int i = 0; i < SlotItemIDs.Length; i++)
            {
                if (SlotItemIDs[i] == itemID)
                    return i;
            }
            return -1;
        }
        

        
        /// <summary>
        /// Sets HandIKState.IsAiming on the player entity so PlayerIKBridge
        /// rotates the upper arms/hands toward the crosshair aim direction.
        /// </summary>
        private void UpdateHandIKAimState(bool isWeaponEquipped)
        {
            if (_equipmentProvider is not DIGEquipmentProvider digProvider) return;
            var playerEntity = digProvider.PlayerEntity;
            if (playerEntity == Entity.Null || !_entityManager.Exists(playerEntity)) return;
            if (!_entityManager.HasComponent<HandIKState>(playerEntity)) return;

            var handState = _entityManager.GetComponentData<HandIKState>(playerEntity);
            if (handState.IsAiming == isWeaponEquipped) return; // Already correct
            handState.IsAiming = isWeaponEquipped;
            _entityManager.SetComponentData(playerEntity, handState);
        }

        private void UpdateWeaponVisuals(int quickSlot, Entity itemEntity, int animatorItemID = 0)
        {
            // Hide previous weapon
            if (_lastEquippedSlot > 0 && _lastEquippedSlot < WeaponModels.Length)
            {
                var prevWeapon = WeaponModels[_lastEquippedSlot];
                if (prevWeapon != null)
                {
                    if (ShowHolsteredWeapons)
                    {
                        // EPIC 14.10: Get holster config from the weapon's current parent's WeaponParentConfig
                        Transform holsterTarget = BackAttachPoint;
                        Vector3 holsterPosition = Vector3.zero;
                        Quaternion holsterRotation = Quaternion.identity;

                        // Try to get WeaponParentConfig from the weapon's current parent
                        var parentConfig = prevWeapon.transform.parent?.GetComponent<WeaponParentConfig>();
                        if (parentConfig != null)
                        {
                            // Use holster target from parent config if specified
                            if (parentConfig.HolsterTarget != null)
                            {
                                holsterTarget = parentConfig.HolsterTarget;
                            }

                            // Apply holster offsets from parent config
                            holsterPosition = parentConfig.HolsterLocalPosition;
                            holsterRotation = Quaternion.Euler(parentConfig.HolsterLocalRotation);
                        }

                        if (holsterTarget != null)
                        {
                            prevWeapon.transform.SetParent(holsterTarget, false);
                            prevWeapon.transform.localPosition = holsterPosition;
                            prevWeapon.transform.localRotation = holsterRotation;
                            if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Holstered weapon to {holsterTarget.name} pos={holsterPosition}");
                        }
                        else
                        {
                            prevWeapon.SetActive(false);
                        }
                    }
                    else
                    {
                        // EPIC 14.17: Clear VFX and MagController when holstering/hiding
                        _currentItemVFX = null;
                        _currentMagazineController = null;
                        prevWeapon.SetActive(false);
                    }
                }

                if (DebugLogging)
                    Debug.Log($"[WeaponEquipVisualBridge] Unequipped weapon from slot {_lastEquippedSlot}");
            }

            // Show new weapon
            if (quickSlot > 0 && quickSlot < WeaponModels.Length)
            {
                var newWeapon = WeaponModels[quickSlot];
                if (newWeapon != null)
                {
                    newWeapon.SetActive(true);

                    // EPIC 14.17: Cache ItemVFXAuthoring for event relay
                    _currentItemVFX = newWeapon.GetComponent<ItemVFXAuthoring>();
                    _currentMagazineController = newWeapon.GetComponent<MagazineReloadController>();

                    // EPIC 14.20: Clear audio bridge cache so it re-discovers for new weapon
                    if (EventRelay != null)
                    {
                        EventRelay.ClearAudioBridgeCache();
                    }

                    Debug.Log($"[WEAPON_VFX_DEBUG] [VisualBridge] Equipped weapon '{newWeapon.name}'. Found VFX Component: {_currentItemVFX != null}, MagController: {_currentMagazineController != null}");

                    // Get WeaponAttachmentConfig for category lookup (WieldTargetID only)
                    var attachConfig = newWeapon.GetComponent<WeaponAttachmentConfig>();

                    // EPIC 14.10: Use WieldTargetID to find the correct parent transform
                    // This implements Opsive's "Category + Specific Offset" positioning algorithm
                    Transform parentTransform = HandAttachPoint;
                    WeaponParentConfig parentConfig = null;

                    if (attachConfig != null || newWeapon != null)
                    {
                        uint targetID = (attachConfig != null) ? attachConfig.WieldTargetID : 0;
                        parentTransform = FindWeaponParent(targetID, HandAttachPoint, newWeapon);
                        if (parentTransform != null && parentTransform != HandAttachPoint)
                        {
                            // Get the WeaponParentConfig from the parent transform
                            parentConfig = parentTransform.GetComponent<WeaponParentConfig>();
                            if (DebugLogging)
                            {
                                Debug.Log($"[WeaponEquipVisualBridge] Using specific parent: {parentTransform.name} (ID: {attachConfig.WieldTargetID}, HasConfig: {parentConfig != null})");
                            }
                        }
                    }

                    if (parentTransform != null)
                    {
                        newWeapon.transform.SetParent(parentTransform, false);

                        // --- STRATEGY 1: UNIVERSAL GRIP (New) ---
                        // If weapon has specific grip authoring, use that relative to the socket/parent
                        var grip = newWeapon.GetComponent<ItemGripAuthoring>();
                        if (grip != null)
                        {
                            grip.ApplyGrip(newWeapon.transform);
                            if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Applied Universal Grip for {newWeapon.name}");
                        }
                        // --- STRATEGY 2: LEGACY CONFIG (Old) ---
                        else if (parentConfig != null)
                        {
                            // Apply position/rotation/scale offsets from WeaponParentConfig (character-defined)
                            newWeapon.transform.localPosition = parentConfig.WeaponLocalPosition;
                            newWeapon.transform.localRotation = Quaternion.Euler(parentConfig.WeaponLocalRotation);
                            newWeapon.transform.localScale = parentConfig.WeaponLocalScale;
                            if (DebugLogging)
                            {
                                Debug.Log($"[WeaponEquipVisualBridge] Applied WeaponParentConfig: parent={parentTransform.name} pos={parentConfig.WeaponLocalPosition} rot={parentConfig.WeaponLocalRotation}");
                            }
                        }
                        else
                        {
                            // No config - use identity transform (socket default or zero)
                            newWeapon.transform.localPosition = Vector3.zero;
                            newWeapon.transform.localRotation = Quaternion.identity;
                            newWeapon.transform.localScale = Vector3.one;
                        }
                        LogTransform("Equipped (Parented)", newWeapon);
                    }
                    else
                    {
                        LogTransform("Equipped (No Hand)", newWeapon);
                    }

                    // EPIC 14.17: Cache VFX Authoring
                    _currentItemVFX = newWeapon.GetComponent<ItemVFXAuthoring>();

                    // EPIC 14.20: Clear audio bridge cache so it re-discovers for new weapon
                    if (EventRelay != null)
                    {
                        EventRelay.ClearAudioBridgeCache();
                    }

                    // EPIC 14.17 Phase 6: Auto-discover MagazineReloadController
                    _currentMagazineController = newWeapon.GetComponent<MagazineReloadController>();
                    if (_currentMagazineController != null && DebugLogging)
                    {
                        Debug.Log($"[WeaponEquipVisualBridge] Auto-discovered MagazineReloadController on '{newWeapon.name}'");
                    }

                    // Setup IK - get targets from ItemGripAuthoring (new) OR WeaponParentConfig (character-defined)
                    if (HandIK != null)
                    {
                        Transform leftHandTarget = null;
                        var grip = newWeapon.GetComponent<ItemGripAuthoring>();

                        // 1. UNIVERSAL GRIP IK
                        if (grip != null && grip.LeftHandIKOverride != null)
                        {
                            leftHandTarget = grip.LeftHandIKOverride;
                             if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Using Universal Grip IK Override: {leftHandTarget.name}");
                        }
                        // 2. LEGACY CONFIG
                        else if (parentConfig != null && parentConfig.LeftHandIKTarget != null)
                        {
                            leftHandTarget = parentConfig.LeftHandIKTarget;
                            if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Using WeaponParentConfig IK target: {leftHandTarget.name}");
                        }
                        // 3. FALLBACK SEARCH
                        else
                        {
                            // Fallback: Search for IK target on the weapon itself
                            leftHandTarget = FindChildByName(newWeapon.transform, "LeftHandAttach");
                            if (leftHandTarget == null) leftHandTarget = FindChildByName(newWeapon.transform, "LeftHandGrip");
                        }

                        HandIK.LeftHandIKTarget = leftHandTarget;
                        if (DebugLogging && leftHandTarget != null) Debug.Log($"[WeaponEquipVisualBridge] Set Left Hand IK to {leftHandTarget.name}");
                    }
                }
                else
                {
                    if (DebugLogging) Debug.LogWarning($"[WeaponEquipVisualBridge] Weapon model for slot {quickSlot} is NULL!");
                }
                
                if (DebugLogging)
                    Debug.Log($"[WeaponEquipVisualBridge] Equipped weapon to slot {quickSlot} (Entity: {itemEntity.Index})");

                // Activate upper-arm IK so the weapon points toward the crosshair
                UpdateHandIKAimState(true);
            }
            else if (itemEntity == Entity.Null)
            {
                if (DebugLogging)
                    Debug.Log($"[WeaponEquipVisualBridge] No weapon equipped");

                // Deactivate upper-arm aiming IK when no weapon equipped
                UpdateHandIKAimState(false);
            }
            
            // Update Animator - Use AnimatorItemID from weapon entity (baked from prefab)
            if (PlayerAnimator != null && _hashSlotItemID != 0)
            {
                int itemID = 0;

                // PREFER DYNAMIC WEAPON ID (from baked CharacterItem.ItemTypeId)
                // This ensures we use the correct ItemID from the weapon prefab's WeaponAuthoring.AnimatorItemID
                if (animatorItemID != 0)
                {
                    itemID = animatorItemID;
                }
                // Fallback to static map only if dynamic lookup failed
                else if (quickSlot >= 0 && quickSlot < SlotItemIDs.Length && SlotItemIDs[quickSlot] != 0)
                {
                    itemID = SlotItemIDs[quickSlot];
                    if (DebugLogging)
                        Debug.LogWarning($"[WEAPON_DEBUG] FALLBACK_TO_STATIC QuickSlot={quickSlot} ItemID={itemID} (animatorItemID was 0)");
                }

                // Update Aiming State (skip if input-based aiming is active)
                if (!_isAiming && itemEntity != Entity.Null && _entityManager.Exists(itemEntity) && _entityManager.HasComponent<WeaponAimState>(itemEntity))
                {
                    var aimState = _entityManager.GetComponentData<WeaponAimState>(itemEntity);
                    if (_hashAiming != 0)
                    {
                        PlayerAnimator.SetBool(_hashAiming, aimState.IsAiming);
                    }
                }
                else if (!_isAiming)
                {
                    if (_hashAiming != 0) PlayerAnimator.SetBool(_hashAiming, false);
                }

                // Update Animator
                if (_hashSlotItemID != 0)
                {
                    int prevItemID = PlayerAnimator.GetInteger(_hashSlotItemID);
                    
                    // CRITICAL: When switching weapons, we need to trigger UNEQUIP first
                    // This exits the current weapon's sub-state machine back to Entry
                    // Then Entry will transition to the new weapon's sub-state machine based on Slot0ItemID
                    if (prevItemID != itemID && prevItemID > 0 && itemID > 0 && _hashSlotItemState != 0)
                    {
                        // Step 1: Trigger unequip on the OLD weapon (while Slot0ItemID still points to old weapon)
                        PlayerAnimator.SetInteger(_hashSlotItemState, 5); // 5 = Unequip
                        if (_hashSlotItemChange != 0)
                        {
                            PlayerAnimator.SetTrigger(_hashSlotItemChange);
                        }
                        Debug.Log($"[ANIMATOR_PARAM] TRIGGER UNEQUIP: Slot0ItemStateIndex=5 for old ItemID={prevItemID}");
                    }
                    
                    // Step 2: Set the new ItemID (this determines which sub-state machine Entry will transition to)
                    PlayerAnimator.SetInteger(_hashSlotItemID, itemID);
                    Debug.Log($"[ANIMATOR_PARAM] SET Slot0ItemID: {prevItemID} -> {itemID} (QuickSlot={quickSlot})");
                    
                    // Step 3: Trigger equip animation when weapon changes
                    // Opsive uses Slot0ItemStateIndex=4 to trigger equip transitions
                    if (prevItemID != itemID && itemID > 0 && _hashSlotItemState != 0)
                    {
                        // Wait for unequip to complete before triggering equip
                        // Set StateIndex to 4 (Equip) - the animator will transition via Entry
                        PlayerAnimator.SetInteger(_hashSlotItemState, 4); // 4 = Equip
                        if (_hashSlotItemChange != 0)
                        {
                            PlayerAnimator.SetTrigger(_hashSlotItemChange);
                        }
                        Debug.Log($"[ANIMATOR_PARAM] TRIGGER EQUIP: Slot0ItemStateIndex=4 for new ItemID={itemID}");
                        
                        // CRITICAL FIX: Force CrossFade to the new weapon's sub-state machine
                        // Unity state machines don't automatically exit sub-state machines when parameters change
                        // We need to explicitly transition to the new weapon's Idle state
                        ForceWeaponSubStateMachine(itemID);
                    }
                }
                
                // CRITICAL: Set weapon layer weights
                // Opsive controllers have weapon-specific layers that need weight=1 when that weapon is equipped
                UpdateWeaponLayerWeights(itemID);
                
                // The instruction's snippet also included _hashSlotItemState, but that's handled in UpdateWeaponState.
                // Keeping the original structure for itemID and debug log.
                if (DebugLogging)
                    Debug.Log($"[WeaponEquipVisualBridge] *** ANIMATION UPDATE *** QuickSlot {quickSlot} -> WeaponEntity {itemEntity.Index} -> AnimatorItemID: {itemID}");
            }
        }
        
        /// <summary>
        /// Update off-hand weapon visuals (shields, torches, etc).
        /// Shows the weapon model on LeftHandAttachPoint.
        /// </summary>
        private void UpdateOffHandVisuals(int quickSlot, Entity itemEntity, int animatorItemID)
        {
            // Hide previous off-hand weapon
            if (_currentOffHandModel != null)
            {
                _currentOffHandModel.SetActive(false);
                _currentOffHandModel = null;
                if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Hidden off-hand weapon");
            }
            
            // Nothing to show
            if (quickSlot <= 0 || itemEntity == Entity.Null)
            {
                if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] No off-hand weapon (slot={quickSlot})");
                return;
            }
            
            // Find weapon model for this quickslot
            if (quickSlot >= WeaponModels.Length || WeaponModels[quickSlot] == null)
            {
                Debug.LogWarning($"[WeaponEquipVisualBridge] No weapon model for off-hand slot {quickSlot}");
                return;
            }
            
            // For off-hand, we need to CLONE the model (or have separate off-hand models)
            // since the same model might be used for main hand
            // For now, if main hand has same item, we don't show duplicate visually
            if (_lastEquippedSlot == quickSlot)
            {
                if (DebugLogging) Debug.Log($"[WeaponEquipVisualBridge] Off-hand same as main hand ({quickSlot}), skipping duplicate visual");
                return;
            }
            
            var offHandWeapon = WeaponModels[quickSlot];
            if (offHandWeapon == null) return;
            
            // Show weapon on left hand attach point
            offHandWeapon.SetActive(true);
            _currentOffHandModel = offHandWeapon;
            
            if (LeftHandAttachPoint != null)
            {
                offHandWeapon.transform.SetParent(LeftHandAttachPoint, false);
                offHandWeapon.transform.localPosition = Vector3.zero;
                offHandWeapon.transform.localRotation = Quaternion.identity;
                
                if (DebugLogging) 
                    Debug.Log($"[WeaponEquipVisualBridge] Equipped OFF-HAND: slot {quickSlot} to {LeftHandAttachPoint.name} (AnimatorItemID={animatorItemID})");
            }
            else
            {
                Debug.LogWarning($"[WeaponEquipVisualBridge] LeftHandAttachPoint not set! Off-hand weapon won't be parented correctly.");
            }
        }
        
        /// <summary>
        /// Map of ItemID to layer name. Add entries for each weapon type.
        /// ClimbingDemo controller ItemID mappings (from animator transition conditions):
        /// </summary>
        private static readonly Dictionary<int, string> ItemIDToLayerName = new Dictionary<int, string>
        {
            { 1, "Assault Rifle" },   // ClimbingDemo uses 1 for Assault Rifle!
            { 3, "Shotgun" },         // Shotgun
            { 5, "Sniper Rifle" },    // Sniper
            { 6, "Rocket Launcher" }, // Rocket
            { 24, "Katana" },         // Katana
            { 23, "Knife" },          // Knife
            { 4, "Bow" },             // Bow
            { 41, "Grenade" },        // Grenade
            { 20, "Trident" },        // Swimming Melee
            { 21, "Underwater Gun" }, // Swimming Ranged
        };
        
        /// <summary>
        /// Map of ItemID to sub-state machine name in UpperBody Layer.
        /// Used for CrossFade to force animator into correct weapon sub-state machine.
        /// </summary>
        private static readonly Dictionary<int, string> ItemIDToSubStateMachine = new Dictionary<int, string>
        {
            { 1, "Assault Rifle" },
            { 2, "Pistol" },
            { 3, "Shotgun" },
            { 4, "Bow" },
            { 5, "Sniper Rifle" },
            { 6, "Rocket Launcher" },
            { 22, "Sword" },
            { 23, "Knife" },
            { 24, "Katana" },
            { 41, "Frag Grenade" },
            { 20, "Trident" },
            { 21, "Underwater Gun" },
        };
        
        /// <summary>
        /// Forces the animator to transition to the correct weapon sub-state machine in the UpperBody Layer.
        /// Unity state machines don't automatically exit sub-state machines when parameters change,
        /// so we need to explicitly CrossFade to the new weapon's state.
        /// </summary>
        private void ForceWeaponSubStateMachine(int itemID)
        {
            if (PlayerAnimator == null) return;
            
            // Find the UpperBody Layer index
            int upperbodyLayerIndex = -1;
            for (int i = 0; i < PlayerAnimator.layerCount; i++)
            {
                if (PlayerAnimator.GetLayerName(i) == "Upperbody Layer")
                {
                    upperbodyLayerIndex = i;
                    break;
                }
            }
            
            if (upperbodyLayerIndex < 0)
            {
                Debug.LogWarning($"[WEAPON_DEBUG] ForceWeaponSubStateMachine: Could not find 'Upperbody Layer'");
                return;
            }
            
            // Get the sub-state machine name for this ItemID
            if (!ItemIDToSubStateMachine.TryGetValue(itemID, out string subStateName))
            {
                if (DebugLogging) Debug.LogWarning($"[WEAPON_DEBUG] ForceWeaponSubStateMachine: No sub-state machine mapped for ItemID={itemID}");
                return;
            }
            
            // Try different state path formats - just use the most likely ones
            string[] directTryPaths = new string[]
            {
                $"{subStateName} Aim",           // "Bow Aim" - FOUND IN CONTROLLER!
                $"{subStateName}.{subStateName} Aim",  // "Bow.Bow Aim" - sub-SM path notation
                $"{subStateName}",               // "Bow" (sub-state machine entry)
            };
            
            // Try CrossFade on the most likely paths
            foreach (string statePath in directTryPaths)
            {
                int stateHash = Animator.StringToHash(statePath);
                PlayerAnimator.CrossFade(stateHash, 0.1f, upperbodyLayerIndex);
            }
            
            // Also try Play() which might work differently than CrossFade
            PlayerAnimator.Play($"{subStateName} Aim", upperbodyLayerIndex, 0f);
        }
        
        /// <summary>
        /// Sets the appropriate weapon layer weight to 1 and others to 0.
        /// This is required because Opsive's layer weight management uses StateMachineBehaviors
        /// that aren't running in our ECS setup.
        /// </summary>
        private void UpdateWeaponLayerWeights(int itemID)
        {
            if (PlayerAnimator == null) return;
            
            // Find the target layer name for this item
            string targetLayerName = null;
            if (ItemIDToLayerName.TryGetValue(itemID, out var layerName))
            {
                targetLayerName = layerName;
            }
            
            // ClimbingDemo Controller Architecture:
            // - "Upperbody Layer" contains ALL weapon sub-state machines (Assault Rifle, Katana, etc.)
            // - Weapon states are accessed via Slot0ItemID parameter which drives BlendTree transitions
            // - "Upperbody Layer" weight MUST be 1 for weapon animations to play
            // - There may also be dedicated weapon layers (e.g., "Assault Rifle Layer") for overrides
            
            // Iterate through all layers and set weights
            for (int i = 0; i < PlayerAnimator.layerCount; i++)
            {
                string currentLayerName = PlayerAnimator.GetLayerName(i);
                
                // CRITICAL: Ensure "Upperbody Layer" weight is always 1 (weapons live here in ClimbingDemo)
                if (currentLayerName == "Upperbody Layer")
                {
                    float currentWeight = PlayerAnimator.GetLayerWeight(i);
                    if (currentWeight < 0.99f)
                    {
                        PlayerAnimator.SetLayerWeight(i, 1f);
                        if (DebugLogging)
                        {
                            Debug.Log($"[WEAPON_DEBUG] UPPERBODY_LAYER enabled: {currentWeight:F1} -> 1.0 (ItemID={itemID})");
                        }
                    }
                    continue;
                }
                
                // Skip base layer and other common layers
                if (i == 0 || currentLayerName == "Base Layer" || 
                    currentLayerName == "Full Body Layer" ||
                    currentLayerName == "Additive Layer")
                {
                    continue;
                }
                
                // Check if this is a dedicated weapon layer (contains weapon-related keywords)
                bool isWeaponLayer = ItemIDToLayerName.Values.Any(wl => 
                    currentLayerName.Contains(wl) || currentLayerName == wl);
                
                if (!isWeaponLayer) continue;
                
                // Set weight: 1 if this is the target weapon layer, 0 otherwise
                float targetWeight = 0f;
                if (targetLayerName != null && currentLayerName.Contains(targetLayerName))
                {
                    targetWeight = 1f;
                }
                
                float weight = PlayerAnimator.GetLayerWeight(i);
                if (Mathf.Abs(weight - targetWeight) > 0.01f)
                {
                    PlayerAnimator.SetLayerWeight(i, targetWeight);
                    if (DebugLogging)
                    {
                        Debug.Log($"[WEAPON_DEBUG] LAYER_WEIGHT '{currentLayerName}' {weight:F1} -> {targetWeight:F1} (ItemID={itemID})");
                    }
                }
            }
        }
        
        private int _lastSubstate = 0;
        private int _lastComboIndex = -1;  // Track combo index separately to detect combo changes

        /// <summary>
        /// Updates animator state based on weapon state.
        /// Mirrors Opsive's ClimbingDemo Controller Slot0ItemStateIndex system:
        ///   0 = Idle
        ///   1 = Aim (rarely used directly - prefer Aiming bool parameter)
        ///   2 = Use/Fire (requires Slot0ItemSubstateIndex=2 for guns, 2-5 for melee)
        ///   3 = Reload
        ///   4 = Equip (uses Aiming bool to differentiate Equip From Idle vs Aim)
        ///   5 = Unequip (uses Aiming bool to differentiate Unequip From Idle vs Aim)
        ///   6 = Drop
        ///   7 = Melee Attack (from Idle when Aiming=false)
        ///   8 = Melee Attack (from Aim when Aiming=true) - if controller supports separate state
        /// </summary>
        private void UpdateWeaponState(Entity itemEntity, int quickSlot)
        {
            if (PlayerAnimator == null || _hashSlotItemState == 0) return;

            // Debug: Log entity being processed
            string playerTag = _isLocalPlayer ? "LOCAL" : "REMOTE";

            int itemState = 0; // Default Idle (Opsive State 0)
            bool isAiming = false;

            // Read Aiming state
            if (itemEntity != Entity.Null && _entityManager.Exists(itemEntity) && _entityManager.HasComponent<WeaponAimState>(itemEntity))
            {
                var aimState = _entityManager.GetComponentData<WeaponAimState>(itemEntity);
                isAiming = aimState.IsAiming;
            }

            // 1. Check CharacterItem State (Equip/Unequip Priority)
            if (itemEntity != Entity.Null && _entityManager.Exists(itemEntity) && _entityManager.HasComponent<CharacterItem>(itemEntity))
            {
                var charItem = _entityManager.GetComponentData<CharacterItem>(itemEntity);

                // State: Unequipping -> 5 (Aiming bool controls From Idle vs From Aim transition)
                if (charItem.State == ItemState.Unequipping)
                {
                    itemState = 5; // Unequip - controller uses Aiming bool internally
                }
                // State: Equipping -> 4 (Aiming bool controls From Idle vs From Aim transition)
                else if (charItem.State == ItemState.Equipping)
                {
                    itemState = 4; // Equip - controller uses Aiming bool internally
                }
                // State: Dropping -> 6
                else if (charItem.State == ItemState.Dropping)
                {
                    itemState = 6;
                }
                // State: Equipped -> Check Actions
                else if (charItem.State == ItemState.Equipped)
                {
                    // Priority 1: Reload (Opsive State 3)
                    if (_entityManager.HasComponent<WeaponAmmoState>(itemEntity))
                    {
                        var ammo = _entityManager.GetComponentData<WeaponAmmoState>(itemEntity);
                        if (ammo.IsReloading)
                        {
                            itemState = 3; // Reload
                        }
                    }

                    // Priority 2: Check Melee Attack
                    if (itemState == 0 && _entityManager.HasComponent<MeleeState>(itemEntity))
                    {
                        var meleeState = _entityManager.GetComponentData<MeleeState>(itemEntity);
                        if (meleeState.IsAttacking)
                        {
                            // Melee Attack uses state 7 (Aiming bool differentiates From Idle vs From Aim)
                            itemState = 7;

                            if (AttackReplicationDebug)
                            {
                                Debug.Log($"[ATTACK_REPLICATION] [ANIM_BRIDGE] [{playerTag}] MELEE_DETECTED " +
                                    $"Entity={itemEntity.Index} IsAttacking={meleeState.IsAttacking} " +
                                    $"Combo={meleeState.CurrentCombo} -> itemState=7");
                            }
                        }
                    }

                    // Priority 2b: Check Bow State (before fire state - bows have separate handling)
                    // Bow animations are driven by HandleBowInput, so we DON'T set state here
                    bool isBow = _entityManager.HasComponent<BowState>(itemEntity);
                    // Do NOT set itemState for bows - let HandleBowInput control the animator

                    // Priority 3: Use/Fire (Opsive State 2) - SKIP for bows (they use HandleBowInput)
                    if (itemState == 0 && !isBow && _entityManager.HasComponent<WeaponFireState>(itemEntity))
                    {
                        var fireState = _entityManager.GetComponentData<WeaponFireState>(itemEntity);
                        if (fireState.IsFiring)
                        {
                            itemState = 2; // Use/Fire

                            if (AttackReplicationDebug)
                            {
                                Debug.Log($"[ATTACK_REPLICATION] [ANIM_BRIDGE] [{playerTag}] FIRE_DETECTED " +
                                    $"Entity={itemEntity.Index} IsFiring={fireState.IsFiring} " +
                                    $"AnimTimer={fireState.FireAnimationTimer:F2} -> itemState=2");
                            }
                        }
                    }

                    // Priority 4: Throwable Use (Opsive State 2)
                    if (itemState == 0 && _entityManager.HasComponent<ThrowableState>(itemEntity))
                    {
                        var throwable = _entityManager.GetComponentData<ThrowableState>(itemEntity);
                        if (throwable.IsCharging)
                        {
                            itemState = 2; // Use/Fire/Throw

                            if (AttackReplicationDebug)
                            {
                                Debug.Log($"[ATTACK_REPLICATION] [ANIM_BRIDGE] [{playerTag}] THROW_DETECTED " +
                                    $"Entity={itemEntity.Index} IsCharging={throwable.IsCharging} -> itemState=2");
                            }
                        }
                    }
                }
            }
            
            // FALLBACK: Check Fire/Melee even if CharacterItem is missing or not Equipped
            // This handles edge cases where equip state hasn't synced yet
            if (itemState == 0 && itemEntity != Entity.Null && _entityManager.Exists(itemEntity))
            {
                // Check if this is a bow - bows are handled by HandleBowInput, not ECS fire state
                bool isBowFallback = _entityManager.HasComponent<BowState>(itemEntity);
                
                // Check Fire State (Fallback) - SKIP for bows
                if (!isBowFallback && _entityManager.HasComponent<WeaponFireState>(itemEntity))
                {
                    var fireState = _entityManager.GetComponentData<WeaponFireState>(itemEntity);
                    if (fireState.IsFiring)
                    {
                        itemState = 2; // Use/Fire

                        if (AttackReplicationDebug)
                        {
                            Debug.Log($"[ATTACK_REPLICATION] [ANIM_BRIDGE] [{playerTag}] FIRE_FALLBACK " +
                                $"Entity={itemEntity.Index} IsFiring={fireState.IsFiring} -> itemState=2");
                        }
                    }
                }

                // Check Throwable State (Fallback)
                if (itemState == 0 && _entityManager.HasComponent<ThrowableState>(itemEntity))
                {
                    var throwable = _entityManager.GetComponentData<ThrowableState>(itemEntity);
                    if (throwable.IsCharging)
                    {
                        itemState = 2; // Use/Fire/Throw
                        
                        if (AttackReplicationDebug)
                        {
                            Debug.Log($"[ATTACK_REPLICATION] [ANIM_BRIDGE] [{playerTag}] THROW_FALLBACK " +
                                $"Entity={itemEntity.Index} IsCharging={throwable.IsCharging} -> itemState=2");
                        }
                    }
                }

                // Check Melee State (Fallback)
                if (itemState == 0 && _entityManager.HasComponent<MeleeState>(itemEntity))
                {
                    var meleeState = _entityManager.GetComponentData<MeleeState>(itemEntity);
                    if (meleeState.IsAttacking)
                    {
                        itemState = 7;

                        if (AttackReplicationDebug)
                        {
                            Debug.Log($"[ATTACK_REPLICATION] [ANIM_BRIDGE] [{playerTag}] MELEE_FALLBACK " +
                                $"Entity={itemEntity.Index} IsAttacking={meleeState.IsAttacking} " +
                                $"Combo={meleeState.CurrentCombo} -> itemState=7");
                        }
                    }
                }
                
                // Check Reload (Fallback)
                if (itemState == 0 && _entityManager.HasComponent<WeaponAmmoState>(itemEntity))
                {
                    var ammo = _entityManager.GetComponentData<WeaponAmmoState>(itemEntity);
                    if (ammo.IsReloading)
                    {
                        itemState = 3;
                    }
                }
            }

            // Update Aiming parameter
            // Skip if input-based aiming is active (HandleWeaponInput sets _isAiming)
            if (_hashAiming != 0 && !_isAiming)
            {
                PlayerAnimator.SetBool(_hashAiming, isAiming);
            }

            // Calculate substate for melee attacks (BEFORE state change check)
            // This allows combo detection even when staying in state 7
            int targetSubstate = 0;
            int currentComboIndex = -1;
            if (itemState == 7 && _hashSlotItemSubstate != 0 && _entityManager.HasComponent<MeleeState>(itemEntity))
            {
                var meleeState = _entityManager.GetComponentData<MeleeState>(itemEntity);
                currentComboIndex = meleeState.CurrentCombo;
                // Opsive melee substate typically matches combo index directly (0, 1, 2...)
                // Some setups use 1-based (1, 2, 3...) - try 1-based first as that's more common
                targetSubstate = 1 + currentComboIndex;
            }
            else if (itemState == 2 && _hashSlotItemSubstate != 0)
            {
                // Fire substate - use 1 as standard fire substate
                if (_entityManager.HasComponent<UsableAction>(itemEntity))
                {
                    var action = _entityManager.GetComponentData<UsableAction>(itemEntity);
                    if (action.ActionType == UsableActionType.Melee)
                        targetSubstate = UnityEngine.Random.Range(1, 4);
                    else
                        targetSubstate = 1; // Standard fire
                }
                else
                {
                    targetSubstate = 2;
                }
            }

            // Detect if we need to re-trigger for combo attacks
            // Use COMBO INDEX for more reliable detection (substate can be stale)
            bool comboRetrigger = false;
            if (itemState == 7 && _lastItemState == 7 && currentComboIndex >= 0)
            {
                // Trigger if combo index changed
                if (currentComboIndex != _lastComboIndex && _lastComboIndex >= 0)
                {
                    comboRetrigger = true;
                }
            }
            
            // Also trigger if we're starting a NEW melee attack after attack ended
            if (itemState == 7 && _lastItemState == 7 && !comboRetrigger && targetSubstate > 0)
            {
                // Check if we need to force retrigger by comparing actual animator state
                int currentAnimatorState = PlayerAnimator.GetInteger(_hashSlotItemState);
                if (currentAnimatorState != 7)
                {
                    comboRetrigger = true;
                }
            }
            
            // Check if this is a bow - bow animator state is controlled by HandleBowInput, not here
            bool isBowWeapon = itemEntity != Entity.Null && _entityManager.Exists(itemEntity) && 
                               _entityManager.HasComponent<BowState>(itemEntity);
            
            // SKIP state override for bows - HandleBowInput controls bow animator state
            if (isBowWeapon)
            {
                // Only update _lastItemState tracking, don't touch animator parameters
                _lastItemState = itemState;
                return; // Bow animation is driven by HandleBowInput
            }
            
            if (itemState != _lastItemState || comboRetrigger)
            {
                if (AttackReplicationDebug && (itemState == 7 || itemState == 2 || _lastItemState == 7 || _lastItemState == 2))
                {
                    Debug.Log($"[ATTACK_REPLICATION] [ANIM_BRIDGE] [{playerTag}] SET_ANIMATOR " +
                        $"Entity={itemEntity.Index} State={_lastItemState}->{itemState} " +
                        $"ComboRetrigger={comboRetrigger} Substate={targetSubstate}");
                }

                PlayerAnimator.SetInteger(_hashSlotItemState, itemState);
                if (_hashSlotItemChange != 0)
                {
                    PlayerAnimator.SetTrigger(_hashSlotItemChange);
                }

                // Apply substate if calculated
                if (targetSubstate > 0 && _hashSlotItemSubstate != 0)
                {
                    PlayerAnimator.SetInteger(_hashSlotItemSubstate, targetSubstate);
                    _lastSubstate = targetSubstate;
                    _lastComboIndex = currentComboIndex;  // Track combo index
                }
                else if (itemState == 0 && _hashSlotItemSubstate != 0)
                {
                     // Reset substate when returning to idle - ensures next attack triggers properly
                     PlayerAnimator.SetInteger(_hashSlotItemSubstate, 0);
                     _lastSubstate = 0;
                     _lastComboIndex = -1;  // Reset combo tracking
                     if (DebugLogging) Debug.Log($"[WEAPON_DEBUG] SUBSTATE_RESET to 0 (returned to idle)");
                }
                // FORCE ANIMATION PLAY (For Remote Players or Responsiveness)
                // If switching to Melee State (7), or retriggering combo, we need to manually Play() the animation
                // because setting parameters alone relies on transitions that might be missing or slow.
                // This logic mirrors HandleMeleeInput's direct Play() call.
                if (itemState == 7 && (_lastItemState != 7 || comboRetrigger))
                {
                    // For remote players, we trust the replicator's combo index (CurrentCombo)
                    // Note: CurrentCombo is 0-indexed (0, 1, 2) usually.
                    // HandleMeleeInput uses 1-based indexing for animation names "Attack 1", "Attack 2".
                    // So we use (CurrentCombo + 1).
                    int comboToPlay = (currentComboIndex >= 0) ? (currentComboIndex + 1) : 1;
                    
                    // Determine weapon prefix (Sword, Knife, etc)
                    int currentItemID = itemEntity != Entity.Null && _entityManager.HasComponent<CharacterItem>(itemEntity) 
                        ? _entityManager.GetComponentData<CharacterItem>(itemEntity).ItemTypeId 
                        : (SlotItemIDs.Length > quickSlot ? SlotItemIDs[quickSlot] : 0);
                        
                    string weaponPrefix = "Knife"; // Default
                    if (currentItemID == 24) weaponPrefix = "Katana";
                    else if (currentItemID == 25) weaponPrefix = "Sword";
                    
                    // Construct animation name
                    // Pattern: "[Prefix].Attack [N] Light From Idle"
                    string baseStateName = $"Attack {comboToPlay} Light From Idle";
                    string[] tryPaths = new string[]
                    {
                        $"{weaponPrefix}.{baseStateName}",
                        $"{weaponPrefix} {baseStateName}",
                        baseStateName
                    };
                    
                    int upperbodyLayer = GetUpperbodyLayerIndex();
                    bool foundState = false;
                    foreach (var path in tryPaths)
                    {
                        int hash = Animator.StringToHash(path);
                        if (PlayerAnimator.HasState(upperbodyLayer, hash))
                        {
                            PlayerAnimator.Play(hash, upperbodyLayer, 0f);
                            foundState = true;
                            if (AttackReplicationDebug)
                            {
                                Debug.Log($"[ATTACK_REPLICATION] [ANIM_BRIDGE] [{playerTag}] FORCE_PLAY API: '{path}' (Hash={hash})");
                            }
                            break;
                        }
                    }

                    // Fallback: If "Attack N" is missing (e.g. Attack 3), cycle between Attack 1 and 2
                    if (!foundState && comboToPlay > 2)
                    {
                         int fallbackCombo = ((comboToPlay - 1) % 2) + 1; // Maps 3->1, 4->2, 5->1...
                         string fallbackName = $"Attack {fallbackCombo} Light From Idle";
                         string[] fallbackPaths = new string[]
                         {
                             $"{weaponPrefix}.{fallbackName}",
                             $"{weaponPrefix} {fallbackName}",
                             fallbackName
                         };

                         foreach (var path in fallbackPaths)
                         {
                             int hash = Animator.StringToHash(path);
                             if (PlayerAnimator.HasState(upperbodyLayer, hash))
                             {
                                 PlayerAnimator.Play(hash, upperbodyLayer, 0f);
                                 if (AttackReplicationDebug)
                                 {
                                     Debug.Log($"[ATTACK_REPLICATION] [ANIM_BRIDGE] [{playerTag}] FORCE_PLAY FALLBACK: '{path}' (Hash={hash})");
                                 }
                                 break;
                             }
                         }
                    }
                }
                else if (itemState != 7 && _lastItemState == 7 && _hashSlotItemSubstate != 0)
                {
                    // Reset when leaving melee state (to any non-melee state)
                    _lastSubstate = 0;
                    _lastComboIndex = -1;  // Reset combo tracking
                }

                _lastItemState = itemState;
            }
        }
        
        private void LogTransform(string label, GameObject obj)
        {
            if (obj == null) return;
            if (DebugLogging)
                Debug.Log($"[WeaponEquipVisualBridge] {label}: '{obj.name}' Active:{obj.activeSelf} Parent:'{obj.transform.parent?.name}' Pos:{obj.transform.localPosition} Scale:{obj.transform.localScale}");
        }
        
        /// <summary>
        /// Logs animator controller layer information to help diagnose missing states.
        /// Called once at startup when DebugLogging is enabled.
        /// </summary>
        private void LogAnimatorControllerInfo()
        {
            if (PlayerAnimator == null || PlayerAnimator.runtimeAnimatorController == null) return;
            
            var controller = PlayerAnimator.runtimeAnimatorController;
            Debug.Log($"[WEAPON_DEBUG] ANIMATOR_CONTROLLER Name='{controller.name}'");
            
            // Log layer info
            int layerCount = PlayerAnimator.layerCount;
            Debug.Log($"[WEAPON_DEBUG] ANIMATOR_LAYERS Count={layerCount}");
            
            // Known weapon layer names to look for
            string[] weaponLayers = { "Assault Rifle", "Pistol", "Shotgun", "Katana", "Knife", "Bow", "Sniper" };
            string[] attackStateKeywords = { "Attack", "Fire", "Reload", "Melee", "Use" };
            
            for (int i = 0; i < layerCount; i++)
            {
                string layerName = PlayerAnimator.GetLayerName(i);
                float weight = PlayerAnimator.GetLayerWeight(i);
                
                // Check if this is a weapon layer
                bool isWeaponLayer = false;
                foreach (var wl in weaponLayers)
                {
                    if (layerName.Contains(wl))
                    {
                        isWeaponLayer = true;
                        break;
                    }
                }
                
                if (isWeaponLayer)
                {
                    Debug.Log($"[WEAPON_DEBUG] WEAPON_LAYER[{i}] '{layerName}' Weight={weight:F2}");
                    
                    // Try to get current state info
                    var stateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(i);
                    Debug.Log($"[WEAPON_DEBUG]   CurrentState Hash={stateInfo.fullPathHash} Length={stateInfo.length:F2}");
                    
                    // Check for attack states (can only do limited checking at runtime)
                    foreach (var keyword in attackStateKeywords)
                    {
                        // We can't enumerate states at runtime without AnimatorController asset access
                        // But we can check if transitions exist
                    }
                }
            }
            
            // Log a warning about ClimbingDemo missing states
            if (controller.name.Contains("Climbing"))
            {
                Debug.LogWarning($"[WEAPON_DEBUG] ANIMATOR_WARNING: Using '{controller.name}' which may be MISSING melee attack states!");
                Debug.LogWarning($"[WEAPON_DEBUG] If melee/fire animations don't play, check Docs/WEAPON_ANIMATION_FIX.md for solutions.");
                Debug.LogWarning($"[WEAPON_DEBUG] Use DIG > Animation > Animator Controller Analyzer to compare with Demo.controller");
            }
        }
        
        /// <summary>
        /// Logs current animator state and all critical parameters.
        /// Called periodically when DebugLogging is enabled.
        /// Filter: ANIMATOR_PARAM
        /// </summary>
        private void LogCurrentAnimatorState()
        {
            if (PlayerAnimator == null) return;
            
            // Get current state info for each important layer
            string baseLayerState = "?";
            string upperbodyState = "?";
            
            for (int i = 0; i < PlayerAnimator.layerCount && i < 10; i++)
            {
                string layerName = PlayerAnimator.GetLayerName(i);
                var stateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(i);
                float weight = PlayerAnimator.GetLayerWeight(i);
                
                if (layerName == "Base Layer")
                {
                    baseLayerState = $"hash={stateInfo.shortNameHash} norm={stateInfo.normalizedTime:F2}";
                }
                else if (layerName == "Upperbody Layer")
                {
                    upperbodyState = $"hash={stateInfo.shortNameHash} norm={stateInfo.normalizedTime:F2} weight={weight:F1}";
                }
            }
            
            // Get all critical parameters
            int slot0ItemID = 0;
            int slot0StateIdx = 0;
            int slot0SubstateIdx = 0;
            int movementSetID = 0;
            int abilityIndex = 0;
            bool moving = false;
            bool aiming = false;
            
            // Use TryGetInteger pattern since we might not have all params
            foreach (var param in PlayerAnimator.parameters)
            {
                switch (param.name)
                {
                    case "Slot0ItemID": slot0ItemID = PlayerAnimator.GetInteger(param.name); break;
                    case "Slot0ItemStateIndex": slot0StateIdx = PlayerAnimator.GetInteger(param.name); break;
                    case "Slot0ItemSubstateIndex": slot0SubstateIdx = PlayerAnimator.GetInteger(param.name); break;
                    case "MovementSetID": movementSetID = PlayerAnimator.GetInteger(param.name); break;
                    case "AbilityIndex": abilityIndex = PlayerAnimator.GetInteger(param.name); break;
                    case "Moving": moving = PlayerAnimator.GetBool(param.name); break;
                    case "Aiming": aiming = PlayerAnimator.GetBool(param.name); break;
                }
            }
            
            Debug.Log($"[ANIMATOR_PARAM] STATE SUMMARY | QuickSlot={_lastEquippedSlot} | " +
                $"Slot0ItemID={slot0ItemID} StateIdx={slot0StateIdx} SubstateIdx={slot0SubstateIdx} | " +
                $"MovementSetID={movementSetID} AbilityIdx={abilityIndex} Moving={moving} Aiming={aiming} | " +
                $"BaseLayer=[{baseLayerState}] Upperbody=[{upperbodyState}]");
        }
        
        /// <summary>
        /// Manually set weapon visibility for a slot.
        /// Use for initialization or testing.
        /// </summary>
        public void SetWeaponVisible(int quickSlot, bool visible)
        {
            if (quickSlot > 0 && quickSlot < WeaponModels.Length)
            {
                var weapon = WeaponModels[quickSlot];
                if (weapon != null)
                    weapon.SetActive(visible);
            }
        }
        
        /// <summary>
        /// Hide all weapons.
        /// </summary>
        public void HideAllWeapons()
        {
            for (int i = 0; i < WeaponModels.Length; i++)
            {
                if (WeaponModels[i] != null)
                    WeaponModels[i].SetActive(false);
            }
            _lastEquippedSlot = -1;
            _lastEquippedEntity = Entity.Null;
        }
        
        /// <summary>
        /// TEST: Manually trigger rifle fire animation.
        /// Call this from Inspector context menu or via code to test if animator responds.
        /// </summary>
        [ContextMenu("Test Rifle Fire Animation")]
        public void TestRifleFireAnimation()
        {
            if (PlayerAnimator == null)
            {
                Debug.LogError("[WEAPON_TEST] No PlayerAnimator assigned!");
                return;
            }
            
            Debug.Log("[WEAPON_TEST] === RIFLE FIRE TEST START ===");
            Debug.Log($"[WEAPON_TEST] Animator GO: '{PlayerAnimator.gameObject.name}' Active={PlayerAnimator.gameObject.activeInHierarchy}");
            Debug.Log($"[WEAPON_TEST] Controller: {(PlayerAnimator.runtimeAnimatorController != null ? PlayerAnimator.runtimeAnimatorController.name : "NULL - NO CONTROLLER!")}");
            
            // CRITICAL CHECK: If no controller, try to find another animator
            if (PlayerAnimator.runtimeAnimatorController == null)
            {
                Debug.LogError("[WEAPON_TEST] !!! PlayerAnimator has NO RuntimeAnimatorController !!!");
                
                // Try to find ALL animators in hierarchy
                var allAnimators = GetComponentsInChildren<Animator>(true);
                Debug.Log($"[WEAPON_TEST] Found {allAnimators.Length} Animators in hierarchy:");
                foreach (var anim in allAnimators)
                {
                    string ctrlName = anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL";
                    Debug.Log($"[WEAPON_TEST]   - '{anim.gameObject.name}' Controller={ctrlName} Layers={anim.layerCount}");
                    
                    // If we find one with a controller, use it
                    if (anim.runtimeAnimatorController != null && anim.layerCount > 0)
                    {
                        Debug.Log($"[WEAPON_TEST] SWITCHING to valid Animator on '{anim.gameObject.name}'");
                        PlayerAnimator = anim;
                        break;
                    }
                }
                
                // Also check parent
                var parentAnimators = GetComponentsInParent<Animator>(true);
                Debug.Log($"[WEAPON_TEST] Found {parentAnimators.Length} Animators in parent:");
                foreach (var anim in parentAnimators)
                {
                    string ctrlName = anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL";
                    Debug.Log($"[WEAPON_TEST]   - '{anim.gameObject.name}' Controller={ctrlName} Layers={anim.layerCount}");
                    
                    if (anim.runtimeAnimatorController != null && anim.layerCount > 0)
                    {
                        Debug.Log($"[WEAPON_TEST] SWITCHING to valid Animator on '{anim.gameObject.name}'");
                        PlayerAnimator = anim;
                        break;
                    }
                }
                
                // Still no controller?
                if (PlayerAnimator.runtimeAnimatorController == null)
                {
                    Debug.LogError("[WEAPON_TEST] FAILED: Could not find ANY Animator with a controller!");
                    return;
                }
            }
            
            // Log current state
            int currentItemID = PlayerAnimator.GetInteger("Slot0ItemID");
            int currentStateIdx = PlayerAnimator.GetInteger("Slot0ItemStateIndex");
            Debug.Log($"[WEAPON_TEST] BEFORE: ItemID={currentItemID}, StateIndex={currentStateIdx}");
            
            // Set rifle item ID (1 for Assault Rifle in ClimbingDemo - confirmed from animator transitions)
            PlayerAnimator.SetInteger("Slot0ItemID", 1);
            
            // CRITICAL: Set the Assault Rifle layer weight to 1
            UpdateWeaponLayerWeights(1);
            
            // Also try to find and enable the layer manually
            for (int i = 0; i < PlayerAnimator.layerCount; i++)
            {
                string layerName = PlayerAnimator.GetLayerName(i);
                if (layerName.Contains("Assault") || layerName.Contains("Rifle"))
                {
                    PlayerAnimator.SetLayerWeight(i, 1f);
                    Debug.Log($"[WEAPON_TEST] Enabled layer: {layerName} (index {i})");
                }
            }
            
            // Set fire state (2)
            PlayerAnimator.SetInteger("Slot0ItemStateIndex", 2);
            
            // Also set substate for fire
            PlayerAnimator.SetInteger("Slot0ItemSubstateIndex", 1);
            
            // Fire the trigger
            PlayerAnimator.SetTrigger("Slot0ItemStateIndexChange");
            
            Debug.Log("[WEAPON_TEST] SET: ItemID=22, StateIndex=2, SubstateIndex=1, Trigger=FIRED");
            
            // Log ALL layers to see what exists
            Debug.Log($"[WEAPON_TEST] === ALL LAYERS ({PlayerAnimator.layerCount}) ===");
            int upperbodyLayerIndex = -1;
            for (int i = 0; i < PlayerAnimator.layerCount; i++)
            {
                string layerName = PlayerAnimator.GetLayerName(i);
                float weight = PlayerAnimator.GetLayerWeight(i);
                var stateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(i);
                
                Debug.Log($"[WEAPON_TEST] LAYER[{i}] '{layerName}' Weight={weight:F2} StateHash={stateInfo.shortNameHash} NormalizedTime={stateInfo.normalizedTime:F2}");
                
                if (layerName == "Upperbody Layer")
                    upperbodyLayerIndex = i;
            }
            
            // CRITICAL: Check and log Upperbody Layer state specifically
            if (upperbodyLayerIndex >= 0)
            {
                var ubStateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(upperbodyLayerIndex);
                float ubWeight = PlayerAnimator.GetLayerWeight(upperbodyLayerIndex);
                Debug.Log($"[WEAPON_TEST] >>> UPPERBODY_LAYER: Index={upperbodyLayerIndex} Weight={ubWeight:F2} StateHash={ubStateInfo.shortNameHash}");
                
                // Check if we're in any fire-related states
                bool inFire = PlayerAnimator.GetCurrentAnimatorStateInfo(upperbodyLayerIndex).IsName("Use") ||
                              PlayerAnimator.GetCurrentAnimatorStateInfo(upperbodyLayerIndex).IsName("Fire") ||
                              PlayerAnimator.GetCurrentAnimatorStateInfo(upperbodyLayerIndex).IsName("Assault Rifle.Use");
                Debug.Log($"[WEAPON_TEST] >>> IN_FIRE_STATE: {inFire}");
                
                // Force the weight to 1 in case it's still 0
                if (ubWeight < 0.99f)
                {
                    PlayerAnimator.SetLayerWeight(upperbodyLayerIndex, 1f);
                    Debug.Log($"[WEAPON_TEST] >>> FORCED Upperbody Layer weight: {ubWeight:F2} -> 1.0");
                }
            }
            else
            {
                Debug.LogWarning("[WEAPON_TEST] >>> WARNING: 'Upperbody Layer' NOT FOUND! ClimbingDemo controller may not be assigned.");
            }
            
            // Don't use coroutine if inactive - just log
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(CheckAnimationStateAfterDelay(0.1f, upperbodyLayerIndex));
            }
            
            Debug.Log("[WEAPON_TEST] === RIFLE FIRE TEST END ===");
        }
        
        private System.Collections.IEnumerator CheckAnimationStateAfterDelay(float delay, int upperbodyLayerIndex)
        {
            yield return new WaitForSeconds(delay);
            
            if (PlayerAnimator != null)
            {
                int itemID = PlayerAnimator.GetInteger("Slot0ItemID");
                int stateIdx = PlayerAnimator.GetInteger("Slot0ItemStateIndex");
                int substateIdx = PlayerAnimator.GetInteger("Slot0ItemSubstateIndex");
                
                Debug.Log($"[WEAPON_TEST] +{delay}s: ItemID={itemID} StateIndex={stateIdx} SubstateIndex={substateIdx}");
                
                if (upperbodyLayerIndex >= 0)
                {
                    var stateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(upperbodyLayerIndex);
                    var nextStateInfo = PlayerAnimator.GetNextAnimatorStateInfo(upperbodyLayerIndex);
                    bool inTransition = PlayerAnimator.IsInTransition(upperbodyLayerIndex);
                    float weight = PlayerAnimator.GetLayerWeight(upperbodyLayerIndex);
                    
                    Debug.Log($"[WEAPON_TEST] +{delay}s: Upperbody CurrentState={stateInfo.shortNameHash} NextState={nextStateInfo.shortNameHash} InTransition={inTransition} Weight={weight:F2}");
                    
                    // Check specific state names
                    string[] stateNames = { "Use", "Fire", "Assault Rifle.Use", "Assault Rifle.Fire", "Idle" };
                    foreach (var name in stateNames)
                    {
                        if (stateInfo.IsName(name))
                            Debug.Log($"[WEAPON_TEST] >>> CURRENT STATE MATCHES: '{name}'");
                    }
                }
                
                // Reset after logging
                yield return new WaitForSeconds(0.4f);
                PlayerAnimator.SetInteger("Slot0ItemStateIndex", 0);
                Debug.Log("[WEAPON_TEST] RESET: StateIndex=0");
            }
        }
        
        private System.Collections.IEnumerator ResetAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (PlayerAnimator != null)
            {
                PlayerAnimator.SetInteger("Slot0ItemStateIndex", 0);
                Debug.Log("[WEAPON_TEST] RESET: StateIndex=0");
            }
        }
        
        // ==================== WEAPON INPUT HANDLING ====================
        
        /// <summary>
        /// Main input handler - routes inputs based on equipped weapon type.
        /// Called every LateUpdate.
        /// </summary>
        private void HandleWeaponInput()
        {
            if (PlayerAnimator == null) return;
            
            #if ENABLE_INPUT_SYSTEM
            // EPIC 15.21 Phase 7: Use PlayerInputState instead of direct input
            // Track input states from semantic action system
            bool leftMouseDown = PlayerInputState.Fire;
            bool rightMouseDown = PlayerInputState.Aim;
            bool leftPressed = PlayerInputState.FirePressed;
            bool leftReleased = PlayerInputState.FireReleased;
            bool rightPressed = PlayerInputState.AimPressed;
            bool rightReleased = PlayerInputState.AimReleased;
            bool reloadPressed = PlayerInputState.ReloadPressed;
            
            // Get current weapon category from MovementSetID
            int movementSetID = PlayerAnimator.GetInteger(_hashMovementSetID);
            bool isGun = (movementSetID == 0);
            bool isMelee = (movementSetID == 1);
            bool isBow = (movementSetID == 2);
            
            // Route to appropriate handler based on weapon type
            // Check for Magic by ItemID (61-65) since it uses standard MovementSetID
            var mainItem = _equipmentProvider.GetEquippedItem(0);
            int currentItemID = mainItem.AnimatorItemID;
            bool isMagic = IsMagicItemID(currentItemID, mainItem.ItemEntity);
            
            // Check for off-hand shield (Slot1ItemID)
            var offItem = _equipmentProvider.GetEquippedItem(1);
            int offHandItemID = offItem.AnimatorItemID;
            bool hasShield = IsShieldItemID(offHandItemID, offItem.ItemEntity);
            
            // Handle off-hand shield FIRST (so right-click goes to block if shield equipped)
            if (hasShield)
            {
                HandleShieldInput(leftPressed, leftMouseDown, leftReleased, rightPressed, rightMouseDown, rightReleased);
                // If blocking, don't pass right-click to main weapon
                if (_shieldBlocking)
                {
                    rightPressed = false;
                    rightMouseDown = false;
                    rightReleased = false;
                }
            }
            
            // Then handle main-hand weapon
            if (isBow)
            {
                HandleBowInput(leftPressed, leftMouseDown, leftReleased, rightPressed, rightMouseDown, rightReleased);
            }
            else if (isMagic)
            {
                HandleMagicInput(leftPressed, leftMouseDown, leftReleased, rightPressed, rightMouseDown, rightReleased);
            }
            else if (isMelee)
            {
                HandleMeleeInput(leftPressed, leftMouseDown, leftReleased, rightPressed, rightMouseDown, rightReleased);
            }
            else // Guns (default)
            {
                HandleGunInput(leftPressed, leftMouseDown, leftReleased, rightPressed, rightMouseDown, rightReleased, reloadPressed);
            }
            
            // Update tracking
            _wasLeftMouseDown = leftMouseDown;
            _wasRightMouseDown = rightMouseDown;
            
            #else
            // Legacy Input fallback
            bool leftMouseDown = Input.GetMouseButton(0);
            bool rightMouseDown = Input.GetMouseButton(1);
            bool leftPressed = Input.GetMouseButtonDown(0);
            bool leftReleased = Input.GetMouseButtonUp(0);
            bool rightPressed = Input.GetMouseButtonDown(1);
            bool rightReleased = Input.GetMouseButtonUp(1);
            bool reloadPressed = PlayerInputState.ReloadPressed;
            
            int movementSetID = PlayerAnimator.GetInteger(_hashMovementSetID);
            bool isGun = (movementSetID == 0);
            bool isMelee = (movementSetID == 1);
            bool isBow = (movementSetID == 2);
            
            if (isBow)
                HandleBowInput(leftPressed, leftMouseDown, leftReleased, rightPressed, rightMouseDown, rightReleased);
            else if (isMelee)
                HandleMeleeInput(leftPressed, leftMouseDown, leftReleased, rightPressed, rightMouseDown, rightReleased);
            else
                HandleGunInput(leftPressed, leftMouseDown, leftReleased, rightPressed, rightMouseDown, rightReleased, reloadPressed);
            
            _wasLeftMouseDown = leftMouseDown;
            _wasRightMouseDown = rightMouseDown;
            #endif
        }
        
        /// <summary>
        /// Handle gun inputs: Left click = fire, Right click = aim, R = reload
        /// Animator expects:
        ///   Idle: StateIndex < 2, Aiming = false
        ///   Aim:  StateIndex < 2, Aiming = true
        ///   Fire: StateIndex = 2, SubstateIndex = 2
        ///   Reload: StateIndex = 3
        /// </summary>
        private void HandleGunInput(bool leftPressed, bool leftHeld, bool leftReleased, 
                                     bool rightPressed, bool rightHeld, bool rightReleased, bool reloadPressed)
        {
            // ============ CHECK ECS WEAPON STATE ============
            // Get the weapon entity and check its state - MUST respect server-authoritative state
            var mainItem = _equipmentProvider.GetEquippedItem(0);
            Entity weaponEntity = mainItem.ItemEntity;
            bool isReloading = false;
            int currentAmmo = 0;
            int reserveAmmo = 0;
            
            // Check WeaponAmmoState for reload state and ammo counts
            if (weaponEntity != Entity.Null && _entityManager.Exists(weaponEntity) && 
                _entityManager.HasComponent<WeaponAmmoState>(weaponEntity))
            {
                var ammoState = _entityManager.GetComponentData<WeaponAmmoState>(weaponEntity);
                isReloading = ammoState.IsReloading;
                currentAmmo = ammoState.AmmoCount;
                reserveAmmo = ammoState.ReserveAmmo;
                
                // Rising edge detection: Trigger reload animation ONCE when state changes to Reloading
                if (isReloading && !_lastIsReloading)
                {
                    // Set reload animation state with trigger to force transition
                    SetAnimatorState(stateIndex: 3, substateIndex: 0, triggerChange: true);
                    _reloadAnimationTriggered = true;
                    Debug.Log("[WEAPON_INPUT] GUN: ECS state changed to RELOADING - triggering reload animation");
                }
                // Clear reload lock when IsReloading goes back to false (reload complete)
                else if (!isReloading && _reloadAnimationTriggered)
                {
                    _reloadAnimationTriggered = false;
                    Debug.Log("[WEAPON_INPUT] GUN: Reload complete - clearing reload lock");
                }
                
                // Update tracking for next frame
                _lastIsReloading = isReloading;
            }
            
            // BLOCK ALL FIRE INPUTS if reloading - server is authoritative!
            if (isReloading || _reloadAnimationTriggered)
            {
                if (leftPressed || leftHeld)
                {
                    if (DebugLogging) Debug.Log("[WEAPON_INPUT] GUN: FIRE BLOCKED - weapon is reloading!");
                }
                // Still allow aim toggle during reload for visual feedback
                if (rightPressed && !IsOrbitMode())
                {
                    _isAiming = true;
                    PlayerAnimator.SetBool(_hashAiming, true);
                }
                else if (rightReleased)
                {
                    _isAiming = false;
                    PlayerAnimator.SetBool(_hashAiming, false);
                }
                return; // Block all other input processing during reload
            }
            // ============ END ECS STATE CHECK ============
            
            // Debug all right-click states
            if (DebugLogging && (rightPressed || rightReleased || rightHeld))
            {
                Debug.Log($"[WEAPON_INPUT] RIGHT CLICK: pressed={rightPressed} held={rightHeld} released={rightReleased} _isAiming={_isAiming}");
            }
            
            bool hasAmmoInClip = currentAmmo > 0;
            // ============ END AMMO CHECK ============
            
            // Right click - Aim
            if (rightPressed && !IsOrbitMode())
            {
                _isAiming = true;
                PlayerAnimator.SetBool(_hashAiming, true);
                // StateIndex stays at 0 or 1 (less than 2) for Aim to trigger
                SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true);
                if (DebugLogging) Debug.Log($"[WEAPON_INPUT] GUN: Start aiming - Set Aiming=true (hash={_hashAiming})");
            }
            else if (rightReleased)
            {
                _isAiming = false;
                PlayerAnimator.SetBool(_hashAiming, false);
                SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true);
                if (DebugLogging) Debug.Log($"[WEAPON_INPUT] GUN: Stop aiming - Set Aiming=false (hash={_hashAiming})");
            }
            
            // Left click - Fire or Dry Fire
            // Fire: StateIndex = 2, SubstateIndex = 2 (with ammo)
            // Dry Fire: StateIndex = 2, SubstateIndex = 11 (no ammo - Opsive DryFireSubstate convention)
            if (leftPressed)
            {
                if (hasAmmoInClip)
                {
                    // Normal fire - has ammo
                    SetAnimatorState(stateIndex: 2, substateIndex: 2, triggerChange: true);
                    _lastFireTime = Time.time;
                    if (DebugLogging) Debug.Log("[WEAPON_INPUT] GUN: Fire (StateIndex=2, SubstateIndex=2)");
                }
                else
                {
                    // Dry fire - no ammo in clip
                    SetAnimatorState(stateIndex: 2, substateIndex: 11, triggerChange: true);
                    Debug.Log($"[WEAPON_INPUT] GUN: DRY FIRE - no ammo! (Clip={currentAmmo} Reserve={reserveAmmo})");
                }
            }
            else if (leftHeld && Time.time - _lastFireTime >= AUTO_FIRE_RATE)
            {
                // Auto-fire only works if we have ammo
                if (hasAmmoInClip)
                {
                    SetAnimatorState(stateIndex: 2, substateIndex: 2, triggerChange: true);
                    _lastFireTime = Time.time;
                    if (DebugLogging) Debug.Log("[WEAPON_INPUT] GUN: Fire (auto)");
                }
                // Don't repeat dry fire on hold - just one click
            }
            else if (leftReleased)
            {
                // Return to idle or aim based on whether still holding right click
                if (_isAiming)
                {
                    SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true);
                }
                else
                {
                    SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true);
                }
            }
            
            // R key - Reload (StateIndex = 3)
            // Only allow reload if we have reserve ammo AND clip is not already full
            if (reloadPressed)
            {
                if (reserveAmmo > 0)
                {
                    SetAnimatorState(stateIndex: 3, substateIndex: 0, triggerChange: true);
                    if (DebugLogging) Debug.Log("[WEAPON_INPUT] GUN: Reload (StateIndex=3)");
                }
                else
                {
                    Debug.Log($"[WEAPON_INPUT] GUN: RELOAD BLOCKED - no reserve ammo! (Clip={currentAmmo} Reserve={reserveAmmo})");
                }
            }
        }
        
        /// <summary>
        /// Handle magic inputs: Left click = cast spell, Right click = aim/channel
        /// Animator expects:
        ///   Idle: StateIndex = 0
        ///   Aim/Charge: StateIndex = 2, Aiming = true
        ///   Cast: StateIndex = 3 (Use)
        ///   Spell Selection: SubstateIndex (0=Fireball Light, 1=Fireball Heavy, 2=Particle Stream, 3=Heal, etc)
        /// </summary>
        private void HandleMagicInput(bool leftPressed, bool leftHeld, bool leftReleased,
                                       bool rightPressed, bool rightHeld, bool rightReleased)
        {
            var mainItem = _equipmentProvider.GetEquippedItem(0);
            var config = GetAnimationConfig(mainItem.ItemEntity);

            // Check for movement input (WASD) to handle CancelCastOnMove
            // EPIC 15.21: Use PlayerInputState.Move instead of direct keyboard access
            bool hasMovementInput = math.lengthsq(PlayerInputState.Move) > 0.01f;
            
            // Update cast timer and manage movement lock
            if (_magicCasting)
            {
                // Enable movement lock while casting
                _magicCastingLockMovement = true;
                
                // Check if movement should cancel the cast
                if (CancelCastOnMove && hasMovementInput)
                {
                    _magicCasting = false;
                    _magicCastTimer = 0f;
                    _magicCastingLockMovement = false;
                    SetAnimatorState(stateIndex: 0, substateIndex: _magicSpellIndex, triggerChange: true);
                    if (DebugLogging) Debug.Log("[WEAPON_INPUT] MAGIC: Cast CANCELLED by movement input");
                    return;
                }
                
                _magicCastTimer += Time.deltaTime;
                if (_magicCastTimer >= config.UseDuration)
                {
                    _magicCasting = false;
                    _magicCastTimer = 0f;
                    _magicCastingLockMovement = false; // Unlock movement on cast complete
                    
                    // Return to idle or aim depending on right click
                    if (_isAiming)
                        SetAnimatorState(stateIndex: 2, substateIndex: _magicSpellIndex, triggerChange: true);
                    else
                        SetAnimatorState(stateIndex: 0, substateIndex: _magicSpellIndex, triggerChange: true);
                    
                    if (DebugLogging) Debug.Log("[WEAPON_INPUT] MAGIC: Cast complete, returning to idle/aim");
                }
            }
            else
            {
                _magicCastingLockMovement = false; // Ensure unlocked when not casting
            }
            
            // Right click - Aim/Channel
            if (rightPressed)
            {
                _isAiming = true;
                PlayerAnimator.SetBool(_hashAiming, true);
                SetAnimatorState(stateIndex: 2, substateIndex: _magicSpellIndex, triggerChange: true);
                if (DebugLogging) Debug.Log($"[WEAPON_INPUT] MAGIC: Start aiming, spell={_magicSpellIndex}");
            }
            else if (rightReleased && !_magicCasting)
            {
                _isAiming = false;
                PlayerAnimator.SetBool(_hashAiming, false);
                SetAnimatorState(stateIndex: 0, substateIndex: _magicSpellIndex, triggerChange: true);
                if (DebugLogging) Debug.Log("[WEAPON_INPUT] MAGIC: Stop aiming");
            }
            
            // Left click - Cast Spell
            if (leftPressed && !_magicCasting)
            {
                _magicCasting = true;
                _magicCastTimer = 0f;
                _magicCastingLockMovement = true; // Lock movement immediately on cast start
                // StateIndex 3 = Use (triggers Fireball/Particle Stream/etc based on SubstateIndex)
                SetAnimatorState(stateIndex: 3, substateIndex: _magicSpellIndex, triggerChange: true);
                if (DebugLogging) Debug.Log($"[WEAPON_INPUT] MAGIC: Cast spell (StateIndex=3, SubstateIndex={_magicSpellIndex})");
            }
            
            // TODO: Scroll wheel to cycle spells (_magicSpellIndex 0-5)
            // This could be added via Mouse.current.scroll.ReadValue().y
        }
        
        /// <summary>
        /// Returns true if movement should be blocked due to magic casting.
        /// Other systems (CharacterController, etc.) can check this to prevent input.
        /// </summary>
        public bool IsCastingMovementLocked => _magicCastingLockMovement;
        
        /// <summary>
        /// Handle shield inputs: Right click = hold block
        /// Shield uses Slot1 (off-hand) parameters.
        /// Animator expects:
        ///   Idle: Slot1ItemStateIndex = 0
        ///   Block: Slot1ItemStateIndex = 3 (Use)
        /// </summary>
        private void HandleShieldInput(bool leftPressed, bool leftHeld, bool leftReleased,
                                        bool rightPressed, bool rightHeld, bool rightReleased)
        {
            // Right click held = Block
            if (rightPressed)
            {
                _shieldBlocking = true;
                // Set Slot1 state to Use (block)
                if (_hashSlot1ItemState != 0)
                    PlayerAnimator.SetInteger(_hashSlot1ItemState, 3);
                if (_hashSlot1ItemChange != 0)
                    PlayerAnimator.SetTrigger(_hashSlot1ItemChange);
                if (DebugLogging) Debug.Log("[WEAPON_INPUT] SHIELD: Start blocking (Slot1StateIndex=3)");
            }
            else if (rightReleased)
            {
                _shieldBlocking = false;
                // Return Slot1 to idle
                if (_hashSlot1ItemState != 0)
                    PlayerAnimator.SetInteger(_hashSlot1ItemState, 0);
                if (_hashSlot1ItemChange != 0)
                    PlayerAnimator.SetTrigger(_hashSlot1ItemChange);
                if (DebugLogging) Debug.Log("[WEAPON_INPUT] SHIELD: Stop blocking (Slot1StateIndex=0)");
            }
            
            // Left click with shield could be shield bash (future feature)
            // For now, left click passes through to main-hand weapon handling
        }
        
        /// <summary>
        /// Returns true if the character is currently blocking with a shield.
        /// </summary>
        public bool IsBlocking => _shieldBlocking;
        
        /// <summary>
        /// Handle melee inputs: Left click = attack combo, Right click = block
        /// Supports: Knife (ItemID=23, 2-hit), Katana (ItemID=24, 3-hit), Sword (ItemID=25, 2-hit)
        /// </summary>
        private void HandleMeleeInput(bool leftPressed, bool leftHeld, bool leftReleased,
                                       bool rightPressed, bool rightHeld, bool rightReleased)
        {
            var mainItem = _equipmentProvider.GetEquippedItem(0);
            var config = GetAnimationConfig(mainItem.ItemEntity);

            // Right click - Block
            // In MMO mode, RMB is steering/orbit, so we disable manual blocking via RMB
            if (rightPressed && !IsOrbitMode())
            {
                SetAnimatorState(stateIndex: 8, substateIndex: 0, triggerChange: true);
                if (DebugLogging) Debug.Log("[WEAPON_INPUT] MELEE: Start blocking");
            }
            else if (rightReleased)
            {
                SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true);
                if (DebugLogging) Debug.Log("[WEAPON_INPUT] MELEE: Stop blocking");
            }
            
            // Left click - Attack combo
            if (leftPressed)
            {
                // Get current weapon to determine prefix and combo count
                int currentItemID = _hashSlotItemID != 0 ? PlayerAnimator.GetInteger(_hashSlotItemID) : 0;
                
                // Determine weapon prefix and combo count based on ItemID
                string weaponPrefix;
                int comboCount;

                // Use Config if valid (ComboCount > 0), else fallback to switch
                if (config.ComboCount > 0)
                {
                    // For prefix, we still need mapping (or add Prefix to config later)
                    // Currently relying on switch for prefix, but config for count
                    comboCount = config.ComboCount;
                    
                    // Helper to guess prefix from weapon type/ID if we wanted fully data driven
                    // But for now, let's keep the name mapping in the switch or a dictionary
                }

                switch (currentItemID)
                {
                    case 23: // Knife
                        weaponPrefix = "Knife";
                        comboCount = config.ComboCount > 0 ? config.ComboCount : 2;
                        break;
                    case 24: // Katana
                        weaponPrefix = "Katana";
                         comboCount = config.ComboCount > 0 ? config.ComboCount : 3;
                        break;
                    case 25: // Sword
                        weaponPrefix = "Sword";
                         comboCount = config.ComboCount > 0 ? config.ComboCount : 2;
                        break;
                    default:
                        weaponPrefix = "Knife"; // Fallback
                         comboCount = config.ComboCount > 0 ? config.ComboCount : 2;
                        break;
                }
                
                // Advance combo if within window, otherwise start fresh
                if (_meleeComboTimer > 0)
                {
                    _meleeComboIndex = (_meleeComboIndex % comboCount) + 1;
                }
                else
                {
                    _meleeComboIndex = 1;
                }
                
                // Use config.UseDuration as combo window, fallback to hardcoded value if 0
                float comboWindow = config.UseDuration > 0 ? config.UseDuration : 0.8f;
                _meleeComboTimer = comboWindow;
                
                // NOTE: DO NOT call SetAnimatorState() here!
                // Setting StateIndex=2 causes transition conditions like "StateIndex >= 2" 
                // to immediately match, triggering early exit from the attack state.
                // We only use Play() to force the animation directly.
                
                // FORCE PLAY attack state on Upperbody Layer
                int upperbodyLayer = GetUpperbodyLayerIndex();
                
                // Build state name based on combo index
                string baseStateName = $"Attack {_meleeComboIndex} Light From Idle";
                
                // Try multiple path formats - Unity is picky about state name formats
                string[] pathFormats = new string[]
                {
                    $"{weaponPrefix}.{baseStateName}",          // "Knife.Attack 1 Light From Idle"
                    $"{weaponPrefix} {baseStateName}",          // "Knife Attack 1 Light From Idle"
                    baseStateName,                               // "Attack 1 Light From Idle"
                    $"{weaponPrefix}.{weaponPrefix} {baseStateName}", // Nested format
                };
                
                int successHash = 0;
                string successPath = null;
                
                foreach (string path in pathFormats)
                {
                    int hash = Animator.StringToHash(path);
                    if (PlayerAnimator.HasState(upperbodyLayer, hash))
                    {
                        successHash = hash;
                        successPath = path;
                        break;
                    }
                }
                
                if (successHash != 0)
                {
                    PlayerAnimator.Play(successHash, upperbodyLayer, 0f);
                }
                
                // Debug output
                if (DebugLogging) 
                {
                    var currentState = PlayerAnimator.GetCurrentAnimatorStateInfo(upperbodyLayer);
                    Debug.Log($"[WEAPON_INPUT] MELEE ({weaponPrefix}): Attack {_meleeComboIndex}/{comboCount} | " +
                              $"FoundPath='{successPath ?? "NONE"}' | " +
                              $"CurrentStateHash={currentState.shortNameHash}");
                }
            }
        }
        
        /// <summary>
        /// Handle bow inputs: Right click = aim, Left click = draw/release
        /// 
        /// Uses SetAnimatorState (same as guns) now that AnyState transitions exist.
        /// BowTransitionAdder added these transitions:
        ///   - Slot0ItemID=4, StateIndex=0 → Idle
        ///   - Slot0ItemID=4, StateIndex=2 → Aim  
        ///   - Slot0ItemID=4, StateIndex=3 → Attack Pull Back
        ///   - Slot0ItemID=4, StateIndex=4 → Attack Release
        /// </summary>
        private void HandleBowInput(bool leftPressed, bool leftHeld, bool leftReleased,
                                     bool rightPressed, bool rightHeld, bool rightReleased)
        {
            // BOW DEBUG: Log input events
            if (BowDebugLogging && (rightPressed || rightReleased || leftPressed || leftReleased))
            {
                Debug.Log($"[BOW_DEBUG] INPUT rightP={rightPressed} rightH={rightHeld} rightR={rightReleased} | leftP={leftPressed} leftH={leftHeld} leftR={leftReleased}");
                Debug.Log($"[BOW_DEBUG] STATE aiming={_isAiming} drawing={_bowDrawing} releasing={_bowReleasing} trackedState='{_currentBowState}'");
            }
            
            // ==================== RELEASING STATE ====================
            if (_bowReleasing)
            {
                // FORCE maintain Attack Release state while releasing
                ForceBowState("Attack Release");
                
                _bowReleaseAnimTimer += Time.deltaTime;
                
                // DEBUG: Log timer state
                if (BowDebugLogging && Time.frameCount % 10 == 0)
                {
                    Debug.Log($"[BOW_DEBUG] RELEASE_TIMER timer={_bowReleaseAnimTimer:F3} duration={BOW_RELEASE_ANIM_DURATION} deltaTime={Time.deltaTime:F4}");
                }
                
                if (_bowReleaseAnimTimer >= BOW_RELEASE_ANIM_DURATION)
                {
                    if (BowDebugLogging) Debug.Log($"[BOW_DEBUG] RELEASE_TIMER_EXPIRED timer={_bowReleaseAnimTimer:F3} >= {BOW_RELEASE_ANIM_DURATION}");
                    
                    _bowReleasing = false;
                    _bowReleaseAnimTimer = 0f;
                    
                    // After release, go to Aim if holding right-click, else Idle
                    if (rightHeld)
                    {
                        _isAiming = true;
                        PlayerAnimator.SetBool(_hashAiming, true);
                        SetAnimatorState(stateIndex: 2, substateIndex: 0, triggerChange: true); // Aim
                        if (BowDebugLogging) Debug.Log("[BOW_DEBUG] TRANSITION Release done -> Aim (StateIndex=2)");
                    }
                    else
                    {
                        _isAiming = false;
                        PlayerAnimator.SetBool(_hashAiming, false);
                        SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true); // Idle
                        if (BowDebugLogging) Debug.Log("[BOW_DEBUG] TRANSITION Release done -> Idle (StateIndex=0)");
                    }
                }
                return; // Don't process other input while releasing
            }
            
            // ==================== DRAWING STATE ====================
            if (_bowDrawing)
            {
                // Continue drawing
                _bowDrawProgress += Time.deltaTime / 1.0f;
                _bowDrawProgress = Mathf.Clamp01(_bowDrawProgress);
                
                // FORCE re-apply Attack Pull Back state every frame while holding
                // This prevents the animator from transitioning out
                ForceBowState("Attack Pull Back");
                
                // BOW DEBUG: Log that we're maintaining draw state
                if (BowDebugLogging && leftHeld && Time.frameCount % 30 == 0)
                {
                    Debug.Log($"[BOW_DEBUG] HOLDING_DRAW drawProgress={_bowDrawProgress:F2} leftHeld={leftHeld}");
                }
                
                // Release arrow when left button released
                if (leftReleased)
                {
                    _bowDrawing = false;
                    _bowReleasing = true;
                    _bowReleaseAnimTimer = 0f;
                    _bowDrawProgress = 0f;
                    SetAnimatorState(stateIndex: 4, substateIndex: 0, triggerChange: true); // Attack Release
                    if (BowDebugLogging) Debug.Log($"[BOW_DEBUG] TRANSITION Fire! -> Attack Release (StateIndex=4)");
                }
                return; // Don't process aim changes while drawing
            }
            
            // ==================== START DRAWING ====================
            if (leftPressed)
            {
                _bowDrawing = true;
                _bowDrawProgress = 0f;
                SetAnimatorState(stateIndex: 3, substateIndex: 0, triggerChange: true); // Attack Pull Back
                if (BowDebugLogging) Debug.Log("[BOW_DEBUG] TRANSITION Start draw -> Attack Pull Back (StateIndex=3)");
                return;
            }
            
            // ==================== AIM STATE ====================
            // FORCE re-apply Aim state every frame while holding right-click (before checking for transitions)
            if (_isAiming && rightHeld && !_bowDrawing)
            {
                ForceBowState("Aim");
            }
            
            // BOW DEBUG: Log that we're maintaining aim state
            if (_isAiming && rightHeld && Time.frameCount % 30 == 0 && BowDebugLogging)
            {
                Debug.Log($"[BOW_DEBUG] HOLDING_AIM rightHeld={rightHeld} _isAiming={_isAiming}");
            }
            
            if (rightPressed && !_isAiming)
            {
                _isAiming = true;
                PlayerAnimator.SetBool(_hashAiming, true);
                SetAnimatorState(stateIndex: 2, substateIndex: 0, triggerChange: true); // Aim
                if (BowDebugLogging) Debug.Log("[BOW_DEBUG] TRANSITION Start aim -> Aim (StateIndex=2)");
            }
            else if (rightReleased && _isAiming)
            {
                _isAiming = false;
                PlayerAnimator.SetBool(_hashAiming, false);
                SetAnimatorState(stateIndex: 0, substateIndex: 0, triggerChange: true); // Idle
                if (BowDebugLogging) Debug.Log("[BOW_DEBUG] TRANSITION Stop aim -> Idle (StateIndex=0)");
            }
        }
        
        /// <summary>
        /// Force bow to stay in a specific state. Called every frame while holding.
        /// Uses CrossFadeInFixedTime which can't be interrupted by other transitions during the blend.
        /// </summary>
        private void ForceBowState(string stateName)
        {
            if (PlayerAnimator == null) return;
            
            int layerIndex = GetUpperbodyLayerIndex();
            if (layerIndex < 0) return;
            
            // Map state name to state index
            int stateIndex = 0;
            switch (stateName)
            {
                case "Idle": stateIndex = 0; break;
                case "Aim": stateIndex = 2; break;
                case "Attack Pull Back": stateIndex = 3; break;
                case "Attack Release": stateIndex = 4; break;
                default: return;
            }
            
            // Keep Slot1 parameters set every frame - transitions check these
            // CRITICAL: Must set Slot1ItemID=4 so AnyState transitions match bow conditions
            if (_hashSlot1ItemID != 0)
                PlayerAnimator.SetInteger(_hashSlot1ItemID, 4);
            if (_hashSlot1ItemState != 0)
                PlayerAnimator.SetInteger(_hashSlot1ItemState, stateIndex);
            if (_hashSlot1ItemSubstate != 0)
                PlayerAnimator.SetInteger(_hashSlot1ItemSubstate, 0);
            
            // Check current state
            var currentState = PlayerAnimator.GetCurrentAnimatorStateInfo(layerIndex);
            var nextState = PlayerAnimator.GetNextAnimatorStateInfo(layerIndex);
            bool isInTransition = PlayerAnimator.IsInTransition(layerIndex);
            
            // Get the target state hash
            int targetHash = 0;
            string[] pathFormats = new string[] { $"Bow.{stateName}", $"Bow {stateName}", stateName };
            foreach (string path in pathFormats)
            {
                int hash = Animator.StringToHash(path);
                if (PlayerAnimator.HasState(layerIndex, hash))
                {
                    targetHash = hash;
                    break;
                }
            }
            
            if (targetHash == 0) return;
            
            // Check if we're already in target state OR transitioning TO it
            bool inTargetState = (currentState.fullPathHash == targetHash) || 
                                 (currentState.shortNameHash == Animator.StringToHash(stateName));
            bool transitioningToTarget = isInTransition && 
                                         (nextState.fullPathHash == targetHash || 
                                          nextState.shortNameHash == Animator.StringToHash(stateName));
            
            if (!inTargetState && !transitioningToTarget)
            {
                // Use Play() with normalized time 0 to FORCE the state immediately
                // CrossFadeInFixedTime can be ignored if another transition is already in progress
                PlayerAnimator.Play(targetHash, layerIndex, 0f);
                
                if (BowDebugLogging)
                {
                    Debug.Log($"[BOW_DEBUG] FORCE_PLAY to '{stateName}' (current hash={currentState.shortNameHash}, inTransition={isInTransition})");
                }
            }
            
            _currentBowState = stateName;
        }
        
        /// <summary>
        /// Unified method to set animator state parameters.
        /// For BOW ONLY: Also sets Slot1 parameters because Upperbody Layer checks Slot1.
        /// For other weapons: Only sets Slot0 to avoid interference.
        /// </summary>
        private void SetAnimatorState(int stateIndex, int substateIndex, bool triggerChange)
        {
            if (PlayerAnimator == null) return;
            
            // Set Slot0 parameters (standard - all weapons)
            PlayerAnimator.SetInteger(_hashSlotItemState, stateIndex);
            PlayerAnimator.SetInteger(_hashSlotItemSubstate, substateIndex);
            
            if (triggerChange && _hashSlotItemChange != 0)
            {
                PlayerAnimator.SetTrigger(_hashSlotItemChange);
            }
            
            // BOW ONLY: Also set Slot1 parameters - Upperbody Layer transitions check Slot1
            // Check if current weapon is bow (ItemID=4)
            int currentItemID = _hashSlotItemID != 0 ? PlayerAnimator.GetInteger(_hashSlotItemID) : 0;
            bool isBow = currentItemID == 4;
            
            if (isBow)
            {
                // CRITICAL: Set Slot1ItemID so AnyState transitions know it's the bow!
                if (_hashSlot1ItemID != 0)
                    PlayerAnimator.SetInteger(_hashSlot1ItemID, 4);
                if (_hashSlot1ItemState != 0)
                    PlayerAnimator.SetInteger(_hashSlot1ItemState, stateIndex);
                if (_hashSlot1ItemSubstate != 0)
                    PlayerAnimator.SetInteger(_hashSlot1ItemSubstate, substateIndex);
                if (triggerChange && _hashSlot1ItemChange != 0)
                    PlayerAnimator.SetTrigger(_hashSlot1ItemChange);
                
                // Force Play() on Upperbody Layer (4) where Bow sub-state machine lives
                string bowStateName = StateIndexToBowStateName(stateIndex);
                if (!string.IsNullOrEmpty(bowStateName))
                {
                    CrossFadeToBowState(bowStateName, GetUpperbodyLayerIndex(), 0.1f);
                }
            }
        }
        
        /// <summary>
        /// Maps StateIndex to Bow state name for CrossFade.
        /// </summary>
        private string StateIndexToBowStateName(int stateIndex)
        {
            switch (stateIndex)
            {
                case 0: return "Idle";
                case 2: return "Aim";
                case 3: return "Attack Pull Back";
                case 4: return "Attack Release";
                case 5: return "Dry Fire";
                default: return null;
            }
        }
        
        /// <summary>
        /// Gets the Upperbody Layer index (cached for performance).
        /// </summary>
        private int _cachedUpperbodyLayerIndex = -2; // -2 = not initialized
        private int GetUpperbodyLayerIndex()
        {
            if (_cachedUpperbodyLayerIndex == -2 && PlayerAnimator != null)
            {
                _cachedUpperbodyLayerIndex = -1;
                for (int i = 0; i < PlayerAnimator.layerCount; i++)
                {
                    if (PlayerAnimator.GetLayerName(i) == "Upperbody Layer")
                    {
                        _cachedUpperbodyLayerIndex = i;
                        break;
                    }
                }
            }
            return _cachedUpperbodyLayerIndex;
        }
        
        /// <summary>
        /// Direct CrossFade to Bow states since AnyState transitions are unreliable.
        /// Tries multiple state path formats to find what works in the Upperbody Layer.
        /// Uses Play() instead of CrossFade() to force state change even when other transitions are pending.
        /// </summary>
        private void CrossFadeToBowState(string stateName, int layerIndex = 4, float transitionDuration = 0.1f)
        {
            if (PlayerAnimator == null) return;
            if (layerIndex < 0) layerIndex = 4; // Default to Upperbody Layer
            
            // Skip if already in this state (prevents redundant crossfades)
            if (_currentBowState == stateName)
            {
                if (BowDebugLogging && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[BOW_DEBUG] CROSSFADE_SKIP already in '{stateName}'");
                }
                return;
            }
            
            string previousState = _currentBowState;
            _currentBowState = stateName; // Track new state
            
            // Try multiple path formats - Opsive uses different conventions
            string[] pathFormats = new string[]
            {
                $"Bow {stateName}",           // "Bow Aim" - most common Opsive format
                $"Bow.{stateName}",           // "Bow.Aim" - sub-state machine notation
                stateName,                     // "Aim" - just the state name
                $"Bow.Bow {stateName}",       // "Bow.Bow Aim" - nested format
            };
            
            int successHash = 0;
            string successPath = null;
            
            foreach (string path in pathFormats)
            {
                int hash = Animator.StringToHash(path);
                if (PlayerAnimator.HasState(layerIndex, hash))
                {
                    successHash = hash;
                    successPath = path;
                    break;
                }
            }
            
            if (successHash != 0)
            {
                PlayerAnimator.Play(successHash, layerIndex, 0f);
                if (BowDebugLogging)
                {
                    Debug.Log($"[BOW_DEBUG] PLAY_SUCCESS '{previousState}' -> '{stateName}' path='{successPath}' layer={layerIndex}");
                }
            }
            else
            {
                // Fallback: try the first format anyway
                int fallbackHash = Animator.StringToHash(pathFormats[0]);
                PlayerAnimator.Play(fallbackHash, layerIndex, 0f);
                
                if (BowDebugLogging)
                {
                    var currentStateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(layerIndex);
                    Debug.Log($"[BOW_DEBUG] PLAY_FALLBACK '{previousState}' -> '{stateName}' tried={string.Join(", ", pathFormats)} layer={layerIndex} | CurrentHash={currentStateInfo.shortNameHash}");
                }
            }
        }

        /// <summary>
        /// TEST: Log all animator layers and their states.
        /// </summary>
        [ContextMenu("Log All Animator Layers")]
        public void LogAllAnimatorLayers()
        {
            if (PlayerAnimator == null)
            {
                Debug.LogError("[WEAPON_TEST] No PlayerAnimator assigned!");
                return;
            }
            
            Debug.Log($"[WEAPON_TEST] === ANIMATOR LAYERS ({PlayerAnimator.layerCount}) ===");
            Debug.Log($"[WEAPON_TEST] Controller: {PlayerAnimator.runtimeAnimatorController?.name}");
            
            for (int i = 0; i < PlayerAnimator.layerCount; i++)
            {
                string layerName = PlayerAnimator.GetLayerName(i);
                float weight = PlayerAnimator.GetLayerWeight(i);
                var stateInfo = PlayerAnimator.GetCurrentAnimatorStateInfo(i);
                
                Debug.Log($"[WEAPON_TEST] Layer[{i}] '{layerName}' Weight={weight:F2} StateHash={stateInfo.shortNameHash} Length={stateInfo.length:F2}s");
            }
            
            // Log all parameters
            Debug.Log($"[WEAPON_TEST] === PARAMETERS ({PlayerAnimator.parameterCount}) ===");
            foreach (var param in PlayerAnimator.parameters)
            {
                string value = param.type switch
                {
                    AnimatorControllerParameterType.Int => PlayerAnimator.GetInteger(param.name).ToString(),
                    AnimatorControllerParameterType.Float => PlayerAnimator.GetFloat(param.name).ToString("F2"),
                    AnimatorControllerParameterType.Bool => PlayerAnimator.GetBool(param.name).ToString(),
                    AnimatorControllerParameterType.Trigger => "(trigger)",
                    _ => "?"
                };
                Debug.Log($"[WEAPON_TEST] Param '{param.name}' ({param.type}) = {value}");
            }
        }
        
        /// <summary>
        /// AGGRESSIVE TEST: Force rifle fire animation by setting ALL required animator parameters.
        /// Press F5 in game to trigger. Filter console by: FORCE_ANIM
        /// </summary>
        private void ForceTestRifleAnimation()
        {
            Debug.Log("===========================================");
            Debug.Log("[FORCE_ANIM] F5 PRESSED - FORCING RIFLE FIRE ANIMATION");
            Debug.Log("===========================================");
            
            // Step 1: Log BEFORE state
            Debug.Log("[FORCE_ANIM] === BEFORE STATE ===");
            foreach (var param in PlayerAnimator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger) continue;
                string value = param.type switch
                {
                    AnimatorControllerParameterType.Int => PlayerAnimator.GetInteger(param.name).ToString(),
                    AnimatorControllerParameterType.Float => PlayerAnimator.GetFloat(param.name).ToString("F2"),
                    AnimatorControllerParameterType.Bool => PlayerAnimator.GetBool(param.name).ToString(),
                    _ => "?"
                };
                Debug.Log($"[FORCE_ANIM] BEFORE: {param.name} = {value}");
            }
            
            // Step 2: Log layer states before
            for (int i = 0; i < PlayerAnimator.layerCount; i++)
            {
                string name = PlayerAnimator.GetLayerName(i);
                float weight = PlayerAnimator.GetLayerWeight(i);
                var info = PlayerAnimator.GetCurrentAnimatorStateInfo(i);
                Debug.Log($"[FORCE_ANIM] LAYER_BEFORE[{i}] '{name}' weight={weight:F2} stateHash={info.shortNameHash}");
            }
            
            // Step 3: SET ALL PARAMETERS AGGRESSIVELY
            Debug.Log("[FORCE_ANIM] === SETTING ALL PARAMETERS ===");
            
            // Core weapon parameters
            PlayerAnimator.SetInteger("Slot0ItemID", 1);           // Assault Rifle
            PlayerAnimator.SetInteger("Slot0ItemStateIndex", 2);   // 2 = Use/Fire
            PlayerAnimator.SetInteger("Slot0ItemSubstateIndex", 1); // Fire substate
            PlayerAnimator.SetInteger("MovementSetID", 0);         // 0 = Guns
            PlayerAnimator.SetBool("Aiming", false);               // Not aiming for basic fire
            
            // These might be needed for transitions
            PlayerAnimator.SetInteger("AbilityIndex", 0);
            PlayerAnimator.SetInteger("AbilityIntData", 0);
            PlayerAnimator.SetBool("Moving", false);
            
            // Fire ALL potentially relevant triggers
            if (HasParameter("Slot0ItemStateIndexChange")) PlayerAnimator.SetTrigger("Slot0ItemStateIndexChange");
            if (HasParameter("AbilityChange")) PlayerAnimator.SetTrigger("AbilityChange");
            
            Debug.Log("[FORCE_ANIM] SET: Slot0ItemID=1, StateIndex=2, SubstateIndex=1, MovementSetID=0");
            
            // Step 4: Force ALL potentially weapon-related layers to weight 1
            Debug.Log("[FORCE_ANIM] === FORCING LAYER WEIGHTS ===");
            for (int i = 0; i < PlayerAnimator.layerCount; i++)
            {
                string name = PlayerAnimator.GetLayerName(i);
                // Enable: Base Layer, Upperbody Layer, any weapon-specific layers
                if (name == "Base Layer" || 
                    name.Contains("Upperbody") || 
                    name.Contains("Upper Body") ||
                    name.Contains("Assault") || 
                    name.Contains("Rifle") ||
                    name.Contains("Arm"))
                {
                    PlayerAnimator.SetLayerWeight(i, 1f);
                    Debug.Log($"[FORCE_ANIM] ENABLED layer '{name}' -> weight=1");
                }
            }
            
            // Step 5: Log AFTER state (next frame will show if it took effect)
            Debug.Log("[FORCE_ANIM] === AFTER STATE (immediate) ===");
            Debug.Log($"[FORCE_ANIM] AFTER: Slot0ItemID={PlayerAnimator.GetInteger("Slot0ItemID")}");
            Debug.Log($"[FORCE_ANIM] AFTER: Slot0ItemStateIndex={PlayerAnimator.GetInteger("Slot0ItemStateIndex")}");
            Debug.Log($"[FORCE_ANIM] AFTER: MovementSetID={PlayerAnimator.GetInteger("MovementSetID")}");
            
            // Step 6: Schedule a delayed check
            StartCoroutine(CheckAnimatorAfterDelay());
        }
        
        private System.Collections.IEnumerator CheckAnimatorAfterDelay()
        {
            yield return new WaitForSeconds(0.1f);
            
            Debug.Log("[FORCE_ANIM] === STATE AFTER 0.1s ===");
            Debug.Log($"[FORCE_ANIM] Slot0ItemID={PlayerAnimator.GetInteger("Slot0ItemID")}");
            Debug.Log($"[FORCE_ANIM] Slot0ItemStateIndex={PlayerAnimator.GetInteger("Slot0ItemStateIndex")}");
            Debug.Log($"[FORCE_ANIM] MovementSetID={PlayerAnimator.GetInteger("MovementSetID")}");
            
            for (int i = 0; i < PlayerAnimator.layerCount; i++)
            {
                string name = PlayerAnimator.GetLayerName(i);
                float weight = PlayerAnimator.GetLayerWeight(i);
                var info = PlayerAnimator.GetCurrentAnimatorStateInfo(i);
                bool isTransitioning = PlayerAnimator.IsInTransition(i);
                Debug.Log($"[FORCE_ANIM] LAYER_AFTER[{i}] '{name}' weight={weight:F2} stateHash={info.shortNameHash} transitioning={isTransitioning}");
            }
            
            yield return new WaitForSeconds(0.5f);
            
            Debug.Log("[FORCE_ANIM] === STATE AFTER 0.6s ===");
            for (int i = 0; i < PlayerAnimator.layerCount; i++)
            {
                string name = PlayerAnimator.GetLayerName(i);
                var info = PlayerAnimator.GetCurrentAnimatorStateInfo(i);
                Debug.Log($"[FORCE_ANIM] LAYER_FINAL[{i}] '{name}' stateHash={info.shortNameHash} normalizedTime={info.normalizedTime:F2}");
            }
        }
        
        private bool HasParameter(string paramName)
        {
            foreach (var p in PlayerAnimator.parameters)
            {
                if (p.name == paramName) return true;
            }
            return false;
        }
        
        /// <summary>
        /// COMPLETE DUMP: Logs absolutely everything about the animator state.
        /// Press F6 in game to trigger. Filter console by: DUMP_ANIM
        /// </summary>
        private void DumpCompleteAnimatorState()
        {
            Debug.Log("===========================================");
            Debug.Log("[DUMP_ANIM] F6 PRESSED - COMPLETE ANIMATOR DUMP");
            Debug.Log("===========================================");
            
            var ctrl = PlayerAnimator.runtimeAnimatorController;
            Debug.Log($"[DUMP_ANIM] Controller: {ctrl?.name ?? "NULL"}");
            Debug.Log($"[DUMP_ANIM] Avatar: {PlayerAnimator.avatar?.name ?? "NULL"}");
            Debug.Log($"[DUMP_ANIM] ApplyRootMotion: {PlayerAnimator.applyRootMotion}");
            Debug.Log($"[DUMP_ANIM] UpdateMode: {PlayerAnimator.updateMode}");
            Debug.Log($"[DUMP_ANIM] CullingMode: {PlayerAnimator.cullingMode}");
            Debug.Log($"[DUMP_ANIM] Speed: {PlayerAnimator.speed}");
            Debug.Log($"[DUMP_ANIM] Enabled: {PlayerAnimator.enabled}");
            Debug.Log($"[DUMP_ANIM] GameObject Active: {PlayerAnimator.gameObject.activeInHierarchy}");
            
            Debug.Log($"[DUMP_ANIM] === ALL {PlayerAnimator.layerCount} LAYERS ===");
            for (int i = 0; i < PlayerAnimator.layerCount; i++)
            {
                string name = PlayerAnimator.GetLayerName(i);
                float weight = PlayerAnimator.GetLayerWeight(i);
                var info = PlayerAnimator.GetCurrentAnimatorStateInfo(i);
                var nextInfo = PlayerAnimator.GetNextAnimatorStateInfo(i);
                bool inTransition = PlayerAnimator.IsInTransition(i);
                
                Debug.Log($"[DUMP_ANIM] LAYER[{i}] Name='{name}' Weight={weight:F3}");
                Debug.Log($"[DUMP_ANIM]   CurrentState: hash={info.shortNameHash} fullHash={info.fullPathHash} length={info.length:F2}s normalizedTime={info.normalizedTime:F2} speed={info.speed:F2}");
                Debug.Log($"[DUMP_ANIM]   IsLooping={info.loop} IsTag('Attack')={info.IsTag("Attack")} IsTag('Use')={info.IsTag("Use")}");
                if (inTransition)
                {
                    Debug.Log($"[DUMP_ANIM]   IN TRANSITION to: hash={nextInfo.shortNameHash}");
                }
            }
            
            Debug.Log($"[DUMP_ANIM] === ALL {PlayerAnimator.parameterCount} PARAMETERS ===");
            foreach (var param in PlayerAnimator.parameters)
            {
                string value = param.type switch
                {
                    AnimatorControllerParameterType.Int => PlayerAnimator.GetInteger(param.name).ToString(),
                    AnimatorControllerParameterType.Float => PlayerAnimator.GetFloat(param.name).ToString("F3"),
                    AnimatorControllerParameterType.Bool => PlayerAnimator.GetBool(param.name).ToString(),
                    AnimatorControllerParameterType.Trigger => "(trigger)",
                    _ => "?"
                };
                Debug.Log($"[DUMP_ANIM] PARAM '{param.name}' [{param.type}] = {value}");
            }
            
            // Check for common Opsive state names
            Debug.Log($"[DUMP_ANIM] === CHECKING KNOWN STATE NAMES ===");
            string[] knownStates = { "Idle", "Use", "Fire", "Reload", "Equip", "Unequip", "Aim Idle", "Adventure Movement", "Assault Rifle" };
            for (int layer = 0; layer < Mathf.Min(PlayerAnimator.layerCount, 5); layer++)
            {
                var info = PlayerAnimator.GetCurrentAnimatorStateInfo(layer);
                foreach (var stateName in knownStates)
                {
                    if (info.IsName(stateName))
                    {
                        Debug.Log($"[DUMP_ANIM] Layer {layer} IS IN STATE: '{stateName}'");
                    }
                }
            }
        }
    }
}
