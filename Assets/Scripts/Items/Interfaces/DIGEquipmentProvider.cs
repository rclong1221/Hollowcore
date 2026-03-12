using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Weapons; // For UsableAction
using DIG.Items.Definitions;

namespace DIG.Items
{
    /// <summary>
    /// DIG's implementation of IEquipmentProvider using Unity ECS.
    /// This is the default equipment provider for the project.
    /// Can be replaced with an Asset Store inventory adapter.
    /// </summary>
    public class DIGEquipmentProvider : MonoBehaviour, IEquipmentProvider
    {
        /// <summary>
        /// Singleton instance for access from Input callbacks.
        /// </summary>
        public static DIGEquipmentProvider Instance { get; private set; }
        [Header("Slot Configuration")]
        [Tooltip("Slot definitions for each equipment slot")]
        [SerializeField] private EquipmentSlotDefinition[] _slotDefinitions;
        
        [Header("Debug")]
        [SerializeField] private bool _debugLogging = false;
        
        // Cached slot data
        private ItemInfo[] _equippedItems;
        private World _entityWorld;
        private Entity _playerEntity;

        /// <summary>
        /// The ECS World where the player entity resides.
        /// Exposed for bridges that need to read component data (like WeaponAimState).
        /// </summary>
        public World EntityWorld => _entityWorld;

        /// <summary>
        /// The Entity this provider is reading from.
        /// </summary>
        public Entity PlayerEntity => _playerEntity;

        /// <summary>
        /// Returns true if this provider's player entity is the local player (owned by this client).
        /// Used by presentation bridges to determine if they should process local input.
        /// </summary>
        public bool IsLocalPlayer
        {
            get
            {
                if (_entityWorld == null || !_entityWorld.IsCreated || _playerEntity == Entity.Null)
                    return false;

                var em = _entityWorld.EntityManager;
                if (!em.Exists(_playerEntity))
                    return false;

                // GhostOwnerIsLocal is an enableable component - must check both HasComponent and IsComponentEnabled
                return em.HasComponent<GhostOwnerIsLocal>(_playerEntity) &&
                       em.IsComponentEnabled<GhostOwnerIsLocal>(_playerEntity);
            }
        }

        // Event for equipment changes
        public event EventHandler<EquipmentChangedEventArgs> OnEquipmentChanged;
        
        /// <summary>
        /// Number of equipment slots available.
        /// </summary>
        public int SlotCount => _slotDefinitions?.Length ?? 2;
        
        /// <summary>
        /// Convenience accessor for main hand (slot 0).
        /// </summary>
        public ItemInfo MainHandItem => GetEquippedItem(0);
        
        /// <summary>
        /// Convenience accessor for off hand (slot 1).
        /// </summary>
        public ItemInfo OffHandItem => GetEquippedItem(1);
        
        private void Awake()
        {
            // Singleton assignment
            Instance = this;
            
            // Initialize slot array
            int slotCount = SlotCount > 0 ? SlotCount : 2;
            _equippedItems = new ItemInfo[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                _equippedItems[i] = ItemInfo.Empty;
            }
        }
        
        private void Start()
        {
            // Find the player entity in the ECS world
            FindPlayerEntity();
        }
        
        private bool[] _slotInputState; // Edge detection state for each slot

        private void Update()
        {
            // Initialize edge detection array if needed (lazy init)
            if (_slotInputState == null || (_slotDefinitions != null && _slotInputState.Length != _slotDefinitions.Length))
            {
                int count = _slotDefinitions != null ? _slotDefinitions.Length : 0;
                _slotInputState = new bool[count];
            }

            // Check for input modifiers and number keys
            HandleEquipmentInput();

            // Consume latched EquipSlot flags after reading.
            // EquipSlot1-9 are latched on performed (not cleared on canceled) to survive
            // same-frame performed+canceled pairs from quick key taps.
            global::Player.Systems.PlayerInputState.ConsumeEquipSlotFlags();

            // Check for off-hand use (e.g. blocking)
            HandleOffHandUseInput();
            
            // Sync with ECS ActiveEquipmentSlot
            SyncFromECS();
        }
        
        /// <summary>
        /// Manually set the player entity (e.g. from EquipmentProviderBindingSystem).
        /// Once set externally, FindPlayerEntity() will not override it.
        /// </summary>
        public void SetPlayerEntity(Entity entity, World world)
        {
            _playerEntity = entity;
            _entityWorld = world;
            _boundExternally = true; // Prevent FindPlayerEntity from overriding
            if (_debugLogging) Debug.Log($"[DIGEquipmentProvider] Manually set player entity: {entity} in world {world.Name}");
            SyncFromECS();
        }

        // Flag to track if entity was bound by EquipmentProviderBindingSystem
        private bool _boundExternally = false;

        /// <summary>
        /// Find the player entity in the ECS world.
        /// Searches all worlds for EquippedItemElement, preferring local player.
        /// NOTE: This is a fallback for single-player or testing. In multiplayer,
        /// EquipmentProviderBindingSystem binds the correct entity via SetPlayerEntity().
        /// </summary>
        private void FindPlayerEntity()
        {
            // If entity was bound externally by EquipmentProviderBindingSystem, don't override
            if (_boundExternally && _playerEntity != Entity.Null && _entityWorld != null && _entityWorld.IsCreated)
            {
                // Verify the entity still exists
                if (_entityWorld.EntityManager.Exists(_playerEntity))
                    return;
                // Entity was destroyed, allow rebinding
                _boundExternally = false;
            }

            // Search in all available worlds for entities with EquippedItemElement
            // Prefer ClientSimulation world for predicted ghosts, and LOCAL player (GhostOwnerIsLocal)
            World clientWorld = null;
            Entity clientEntity = Entity.Null;
            bool foundLocal = false;

            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;

                // Skip loading worlds and local worlds
                string worldName = world.Name;
                if (worldName.StartsWith("Loading") || worldName == "LocalWorld")
                    continue;

                try
                {
                    var em = world.EntityManager;
                    // Use EquippedItemElement as trigger
                    var query = em.CreateEntityQuery(typeof(EquippedItemElement));
                    int count = query.CalculateEntityCount();

                    if (count > 0)
                    {
                        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                        if (entities.Length > 0)
                        {
                            // Prefer ClientSimulation world (where predicted ghosts live)
                            if (worldName.Contains("Client"))
                            {
                                // Find the LOCAL player entity (the one we control)
                                Entity localPlayerEntity = Entity.Null;
                                bool foundLocalPlayer = false;
                                foreach (var entity in entities)
                                {
                                    if (em.HasComponent<GhostOwnerIsLocal>(entity) && 
                                        em.IsComponentEnabled<GhostOwnerIsLocal>(entity))
                                    {
                                        localPlayerEntity = entity;
                                        foundLocalPlayer = true;
                                        break;
                                    }
                                }
                                
                                // Fallback to first entity if no local player found (e.g., server-only mode)
                                if (localPlayerEntity == Entity.Null)
                                    localPlayerEntity = entities[0];
                                    
                                clientWorld = world;
                                clientEntity = localPlayerEntity;
                                foundLocal = foundLocalPlayer;
                                
                                if (_debugLogging)
                                    Debug.Log($"[DIGEquipmentProvider] Found player entity {localPlayerEntity.Index} in CLIENT world '{worldName}' (IsLocal={foundLocalPlayer})");
                            }
                            else if (clientWorld == null)
                            {
                                // Fallback to any world that has the entity
                                clientWorld = world;
                                clientEntity = entities[0];
                                if (_debugLogging)
                                    Debug.Log($"[DIGEquipmentProvider] Found player entity {entities[0].Index} in world '{worldName}' (fallback)");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (_debugLogging)
                        Debug.LogWarning($"[DIGEquipmentProvider] Error querying world '{worldName}': {e.Message}");
                }
            }

            if (clientWorld != null && clientEntity != Entity.Null)
            {
                _playerEntity = clientEntity;
                _entityWorld = clientWorld;
            }
            else
            {
                if (_debugLogging && Time.frameCount % 120 == 0) 
                    Debug.LogWarning("[DIGEquipmentProvider] Could not find any entity with EquippedItemElement buffer in any world!");
            }
        }
        
        /// <summary>
        /// Sync equipment state from ECS EquippedItemElement buffer.
        /// </summary>
        private void SyncFromECS()
        {
            if (_entityWorld == null || _playerEntity == Entity.Null)
            {
                FindPlayerEntity();
                return;
            }
            
            var entityManager = _entityWorld.EntityManager;
            
            if (!entityManager.Exists(_playerEntity))
            {
                _playerEntity = Entity.Null;
                return;
            }
            
            if (!entityManager.HasBuffer<EquippedItemElement>(_playerEntity))
                return;
            
            var buffer = entityManager.GetBuffer<EquippedItemElement>(_playerEntity);
            
            // Sync all slots
            for (int i = 0; i < _slotDefinitions.Length; i++)
            {
                Entity itemEntity = Entity.Null;
                if (i < buffer.Length)
                {
                    itemEntity = buffer[i].ItemEntity;
                }
                
                UpdateSlotFromEntity(i, itemEntity);
            }

            // Phase 5: Evaluate Suppression Rules
            EvaluateSuppression();
        }

        private bool[] _suppressedSlots;
        private Dictionary<string, int> _slotIdToindex;

        public bool IsSlotSuppressed(int slotIndex)
        {
            if (_suppressedSlots == null || slotIndex < 0 || slotIndex >= _suppressedSlots.Length)
                return false;
            return _suppressedSlots[slotIndex];
        }

        private void EvaluateSuppression()
        {
            if (_slotDefinitions == null) return;
            
            // Initialize arrays if needed
            if (_suppressedSlots == null || _suppressedSlots.Length != _slotDefinitions.Length)
            {
                _suppressedSlots = new bool[_slotDefinitions.Length];
                _slotIdToindex = new Dictionary<string, int>();
                for (int i = 0; i < _slotDefinitions.Length; i++)
                {
                    if (_slotDefinitions[i] != null)
                        _slotIdToindex[_slotDefinitions[i].SlotID] = i;
                }
            }

            // Reset
            Array.Clear(_suppressedSlots, 0, _suppressedSlots.Length);

            // Check rules for each slot
            for (int i = 0; i < _slotDefinitions.Length; i++)
            {
                var def = _slotDefinitions[i];
                if (def == null) continue;

                foreach (var rule in def.SuppressionRules)
                {
                    // Find watcher slot
                    if (!_slotIdToindex.TryGetValue(rule.WatchSlotID, out int watcherIndex))
                        continue; // Invalid watcher ID

                    var watcherItem = GetEquippedItem(watcherIndex);
                    bool conditionMet = false;

                    switch (rule.Condition)
                    {
                        case SuppressionCondition.HasItem:
                            conditionMet = !watcherItem.IsEmpty;
                            break;
                            
                        case SuppressionCondition.HasTwoHanded:
                            // Check Category GridType (if available) or assume based on something else?
                            // We should use WeaponCategoryDefinition
                            if (!watcherItem.IsEmpty && watcherItem.WeaponCategory != null)
                            {
                                conditionMet = watcherItem.WeaponCategory.GripType == GripType.TwoHanded;
                            }
                            else if (!watcherItem.IsEmpty)
                            {
                                // Fallback for legacy (AnimationWeaponType)?
                                // MainHand two-handed logic was typically hardcoded.
                                // If using legacy, we might not know GripType unless we map Enum -> Grip.
                                // For now, assume False if no Category.
                            }
                            break;
                            
                        case SuppressionCondition.HasCategory:
                            if (!watcherItem.IsEmpty && watcherItem.WeaponCategory != null)
                            {
                                conditionMet = watcherItem.WeaponCategory.CategoryID == rule.ConditionValue;
                            }
                            break;
                    }

                    if (conditionMet)
                    {
                        // Apply action (Only Hide/Disable supported for 'IsSuppressed' flag)
                        if (rule.Action == SuppressionAction.Hide || rule.Action == SuppressionAction.Disable)
                        {
                            _suppressedSlots[i] = true;
                            if (_debugLogging) Debug.Log($"[DIGEquipmentProvider] Slot '{def.SlotID}' suppresssed by rule on '{rule.WatchSlotID}'");
                            break; // Slot is suppressed, move to next slot
                        }
                    }
                }
            }
        }

        private float _lastDebugLogTime;

        
        /// <summary>
        /// Update a slot's ItemInfo from an entity.
        /// </summary>
        private void UpdateSlotFromEntity(int slotIndex, Entity itemEntity)
        {
            if (slotIndex >= _equippedItems.Length)
                return;

            // DEBUG: Diagnose OffHand issues
            if (slotIndex == 1 && Time.time - _lastDebugLogTime > 2.0f)
            {
                _lastDebugLogTime = Time.time;
                bool exists = _entityWorld != null && itemEntity != Entity.Null && _entityWorld.EntityManager.Exists(itemEntity);
                bool hasConfig = exists && _entityWorld.EntityManager.HasComponent<ItemAnimationConfig>(itemEntity);
                bool hasUsable = exists && _entityWorld.EntityManager.HasComponent<UsableAction>(itemEntity);
                Debug.Log($"[DIGProvider-Debug] Slot 1 Entity={itemEntity.Index} World={_entityWorld?.Name} Exists={exists} HasConfig={hasConfig} HasUsable={hasUsable}");
                
                if (exists && hasConfig)
                {
                    var c = _entityWorld.EntityManager.GetComponentData<ItemAnimationConfig>(itemEntity);
                    Debug.Log($"[DIGProvider-Debug] Config details: AnimID={c.AnimatorItemID} CategoryID={c.CategoryID}");
                }
            }

            
            var oldItem = _equippedItems[slotIndex];
            
            if (itemEntity == Entity.Null)
            {
                if (!oldItem.IsEmpty)
                {
                    _equippedItems[slotIndex] = ItemInfo.Empty;
                    FireEquipmentChanged(slotIndex, oldItem, ItemInfo.Empty);
                }
                return;
            }
            
            // Read item data from entity
            var newItem = ReadItemInfoFromEntity(itemEntity);
            
            // Check if changed
            if (oldItem.AnimatorItemID != newItem.AnimatorItemID)
            {
                _equippedItems[slotIndex] = newItem;
                FireEquipmentChanged(slotIndex, oldItem, newItem);
            }
        }
        
        /// <summary>
        /// Read ItemInfo from an ECS entity.
        /// </summary>
        private ItemInfo ReadItemInfoFromEntity(Entity itemEntity)
        {
            if (_entityWorld == null || itemEntity == Entity.Null)
                return ItemInfo.Empty;
            
            var entityManager = _entityWorld.EntityManager;
            
            if (!entityManager.Exists(itemEntity))
                return ItemInfo.Empty;
            
            var info = new ItemInfo
            {
                ItemEntity = itemEntity,
                AnimatorItemID = 0,
                CategoryID = "Gun", // Default
                MovementSetID = 0
            };
            
            // 1. Get AnimatorItemID from UsableAction (traditional source)
            if (entityManager.HasComponent<UsableAction>(itemEntity))
            {
                var usable = entityManager.GetComponentData<UsableAction>(itemEntity);
                info.AnimatorItemID = usable.AnimatorItemID;
            }
            
            // 2. Use ItemAnimationConfig if available (Overrides Type and MovementSetID)
            // This enables the new data-driven workflow (EPIC14.3)
            bool hasConfig = false;
            if (entityManager.HasComponent<ItemAnimationConfig>(itemEntity))
            {
                var config = entityManager.GetComponentData<ItemAnimationConfig>(itemEntity);
                hasConfig = true;
                
                info.CategoryID = config.CategoryID.ToString();
                info.MovementSetID = config.MovementSetID;
                
                // If Config has a specific AnimatorItemID override, use it
                // (Otherwise redundant with UsableAction.AnimatorItemID)
                if (config.AnimatorItemID != 0)
                {
                    info.AnimatorItemID = config.AnimatorItemID;
                }
            }
            
            // 3. Fallback: Determine derived stats from ID if Config is missing
            if (!hasConfig && info.AnimatorItemID != 0)
            {
                info.CategoryID = DetermineCategoryID(info.AnimatorItemID);
                info.MovementSetID = DetermineMovementSetID(info.AnimatorItemID);
            }
            
            return info;
        }
        
        /// <summary>
        /// Determine category ID from AnimatorItemID (legacy fallback).
        /// </summary>
        private string DetermineCategoryID(int animatorItemID)
        {
            // Magic: 61-65
            if (animatorItemID >= 61 && animatorItemID <= 65)
                return "Magic";
            
            // Shield: 26
            if (animatorItemID == 26)
                return "Shield";
            
            // Melee: 23=Knife, 24=Katana, 25=Sword
            if (animatorItemID >= 23 && animatorItemID <= 25)
                return "Melee";
            
            // Bow: 4
            if (animatorItemID == 4)
                return "Bow";
            
            // Default to Gun
            return "Gun";
        }
        
        /// <summary>
        /// Determine MovementSetID from AnimatorItemID.
        /// </summary>
        private int DetermineMovementSetID(int animatorItemID)
        {
            // Magic: 61-65 → MovementSetID 3 (or based on config)
            if (animatorItemID >= 61 && animatorItemID <= 65)
                return 0; // Magic still uses default for now
            
            // Melee: 23-25 → MovementSetID 1
            if (animatorItemID >= 23 && animatorItemID <= 25)
                return 1;
            
            // Bow: 4 → MovementSetID 2
            if (animatorItemID == 4)
                return 2;
            
            // Guns: default → MovementSetID 0
            return 0;
        }
        
        /// <summary>
        /// Called by Input Action callbacks (e.g., OnEquipSlot1) to handle equipment requests.
        /// Determines which slot to use based on modifier key state.
        /// </summary>
        /// <param name="quickSlot">The quick slot number (1-9)</param>
        public void HandleEquipInput(int quickSlot)
        {
            Debug.Log($"[DIGEquipmentProvider] HandleEquipInput called: quickSlot={quickSlot}, _slotDefinitions={(_slotDefinitions != null ? _slotDefinitions.Length.ToString() : "null")}");
            
            if (_slotDefinitions == null || quickSlot < 1 || quickSlot > 9)
                return;

            // Two-pass approach: First check slots that REQUIRE a modifier (more specific)
            // Then check slots with no modifier requirement
            
            // Pass 1: Slots that require a modifier (Shift, Alt, Ctrl)
            for (int i = 0; i < _slotDefinitions.Length; i++)
            {
                var slotDef = _slotDefinitions[i];
                if (slotDef == null) continue;
                if (slotDef.RequiredModifier == ModifierKey.None) continue;

                if (slotDef.IsModifierHeld())
                {
                    if (_debugLogging)
                        Debug.Log($"[DIGEquipmentProvider] HandleEquipInput: Slot '{slotDef.SlotID}' QuickSlot={quickSlot} (Modifier matched)");
                    RequestEquip(i, quickSlot);
                    return;
                }
            }

            // Pass 2: Slots with no modifier requirement
            bool anyModifierHeld = IsAnyModifierHeld();
            if (anyModifierHeld)
                return; // Don't equip to no-modifier slots if modifier is held

            for (int i = 0; i < _slotDefinitions.Length; i++)
            {
                var slotDef = _slotDefinitions[i];
                if (slotDef == null) continue;
                if (slotDef.RequiredModifier != ModifierKey.None) continue;

                if (_debugLogging)
                    Debug.Log($"[DIGEquipmentProvider] HandleEquipInput: Slot '{slotDef.SlotID}' QuickSlot={quickSlot} (No modifier)");
                RequestEquip(i, quickSlot);
                return;
            }
        }

        /// <summary>
        /// Handle equipment input using slot definitions.
        /// Processes slots with modifiers FIRST to ensure Shift+Key goes to OffHand, not MainHand.
        /// NOTE: This is now a fallback - primary input comes from HandleEquipInput() via Input Actions.
        /// </summary>
        private void HandleEquipmentInput()
        {
            if (_slotDefinitions == null)
            {
                // Debug.Log("[DIGEquipmentProvider] _slotDefinitions is null");
                return;
            }

            // Two-pass approach: First check slots that REQUIRE a modifier (more specific),
            // then check slots with no modifier requirement (less specific).
            // This ensures Shift+2 goes to OffHand (requires Shift), not MainHand (no modifier).

            // Check if any modifier is held for debug logging
            // EPIC 15.21: Use PlayerInputState instead of direct keyboard access
            bool shiftHeld = global::Player.Systems.PlayerInputState.ModShift;

            // Pass 1: Slots that require a modifier (Shift, Alt, Ctrl)
            for (int i = 0; i < _slotDefinitions.Length; i++)
            {
                var slotDef = _slotDefinitions[i];
                if (slotDef == null) continue;
                if (slotDef.RequiredModifier == 0) continue; // Skip no-modifier slots (0 = None)

                if (TryProcessSlotInput(i, slotDef))
                {
                    if (shiftHeld)
                        Debug.Log($"[DIGEquipmentProvider] OFF-HAND EQUIP: Slot '{slotDef.SlotID}' LastPressedKey={slotDef.LastPressedKey}");
                    return; // Input was consumed
                }
            }

            // Pass 2: Slots with no modifier requirement (but only if no modifier is held)
            // This prevents MainHand from catching Shift+2 when OffHand should handle it
            bool anyModifierHeld = IsAnyModifierHeld();

            for (int i = 0; i < _slotDefinitions.Length; i++)
            {
                var slotDef = _slotDefinitions[i];
                if (slotDef == null) continue;
                if (slotDef.RequiredModifier != 0) continue; // Skip modifier slots (handled above)

                // If a modifier is held, don't let no-modifier slots consume the input
                if (anyModifierHeld) continue;

                if (TryProcessSlotInput(i, slotDef))
                    return; // Input was consumed
            }
        }

        /// <summary>
        /// Check if any modifier key (Shift, Alt, Ctrl) is currently held.
        /// </summary>
        private bool IsAnyModifierHeld()
        {
            // EPIC 15.21: Use PlayerInputState Modifiers
            return global::Player.Systems.PlayerInputState.ModAlt || 
                   global::Player.Systems.PlayerInputState.ModCtrl || 
                   global::Player.Systems.PlayerInputState.ModShift;
        }

        /// <summary>
        /// Try to process input for a specific slot. Returns true if input was consumed.
        /// Implements Edge Detection using _slotInputState to prevent continuous firing.
        /// </summary>
        private bool TryProcessSlotInput(int slotIndex, EquipmentSlotDefinition slotDef)
        {
            // Check if required modifier is held
            if (!slotDef.IsModifierHeld()) 
            {
                if (slotIndex < _slotInputState.Length) _slotInputState[slotIndex] = false;
                return false;
            }

            bool isPressed = false;
            int triggerSlot = 0;

            // Check input type
            if (slotDef.UsesNumericKeys)
            {
                // Check keys 1-9
                for (int keyIndex = 1; keyIndex <= 9; keyIndex++)
                {
                    if (IsNumericKeyPressed(keyIndex))
                    {
                        isPressed = true;
                        triggerSlot = keyIndex;
                        break;
                    }
                }
            }
            else if (slotDef.IsBindingPressed())
            {
                isPressed = true;
                
                // Use LastPressedKey which is set by IsBindingPressed() for range notation like "1-9"
                // Falls back to parsing PrimaryBinding for single digit bindings
                int quickSlotIndex = slotDef.LastPressedKey;
                if (quickSlotIndex == 0 && int.TryParse(slotDef.PrimaryBinding, out int digit))
                {
                    quickSlotIndex = digit;
                }

                // Default to 1 if still not set
                if (quickSlotIndex == 0)
                {
                    quickSlotIndex = 1;
                }
                
                triggerSlot = quickSlotIndex;
            }
            
            // Edge Detection
            bool wasPressed = false;
            if (slotIndex < _slotInputState.Length) 
            {
                wasPressed = _slotInputState[slotIndex];
                _slotInputState[slotIndex] = isPressed;
            }

            if (isPressed && !wasPressed)
            {
                if (_debugLogging)
                    Debug.Log($"[DIGEquipmentProvider] Input detected for slot '{slotDef.SlotID}': TriggerSlot={triggerSlot} (Edge Trigger)");

                RequestEquip(slotIndex, triggerSlot);
                return true; // Input consumed
            }

            return false; // No edge trigger
        }
        
        /// <summary>
        /// Helper to check if a numeric key (1-9) was pressed.
        /// Uses PlayerInputState instead of hardware checks.
        /// </summary>
        private bool IsNumericKeyPressed(int digit)
        {
            // EPIC 15.21: Use PlayerInputState instead of hardware checks
            switch (digit) {
                case 1: return global::Player.Systems.PlayerInputState.EquipSlot1;
                case 2: return global::Player.Systems.PlayerInputState.EquipSlot2;
                case 3: return global::Player.Systems.PlayerInputState.EquipSlot3;
                case 4: return global::Player.Systems.PlayerInputState.EquipSlot4;
                case 5: return global::Player.Systems.PlayerInputState.EquipSlot5;
                case 6: return global::Player.Systems.PlayerInputState.EquipSlot6;
                case 7: return global::Player.Systems.PlayerInputState.EquipSlot7;
                case 8: return global::Player.Systems.PlayerInputState.EquipSlot8;
                case 9: return global::Player.Systems.PlayerInputState.EquipSlot9;
                default: return false;
            }
        }

        /// <summary>
        /// Update OffHandUseRequest based on input (Right Mouse Button).
        /// </summary>
        private void HandleOffHandUseInput()
        {
            if (_entityWorld == null || _playerEntity == Entity.Null)
                return;
                
            var em = _entityWorld.EntityManager;
            if (!em.Exists(_playerEntity) || !em.HasComponent<OffHandUseRequest>(_playerEntity))
                return;

            // EPIC 15.21: Use PlayerInputState.Aim (which maps to RMB)
            bool isRightClick = global::Player.Systems.PlayerInputState.Aim;

            // Update component
            var request = em.GetComponentData<OffHandUseRequest>(_playerEntity);
            if (request.IsPressed != isRightClick)
            {
                request.IsPressed = isRightClick;
                em.SetComponentData(_playerEntity, request);
                
                if (_debugLogging && isRightClick)
                    Debug.Log("[DIGEquipmentProvider] Off-Hand Use Input (RMB) Pressed");
            }
        }

        
        /// <summary>
        /// Request an equip via PlayerInputState (synced to server through PlayerInput).
        /// This respects the slot definitions from EPIC14.5 - the modifier key checking 
        /// has already been done in HandleEquipInput() using EquipmentSlotDefinition.IsModifierHeld().
        /// </summary>
        private void RequestEquip(int slotIndex, int quickSlot)
        {
            // Set the pending equip in PlayerInputState
            // PlayerInputSystem will read this and populate PlayerInput.EquipSlotId/EquipQuickSlot
            // This gets synced to the server via NetCode, where ItemSwitchInputSystem processes it
            global::Player.Systems.PlayerInputState.PendingEquipSlot = slotIndex;
            global::Player.Systems.PlayerInputState.PendingEquipQuickSlot = quickSlot;
            
            Debug.Log($"[DIGEquipmentProvider] RequestEquip: Slot={slotIndex} QuickSlot={quickSlot} (via PlayerInputState for server sync)");
        }
        
        /// <summary>
        /// Fire the OnEquipmentChanged event.
        /// </summary>
        private void FireEquipmentChanged(int slotIndex, ItemInfo oldItem, ItemInfo newItem)
        {
            if (_debugLogging)
                Debug.Log($"[DIGEquipmentProvider] Equipment changed: slot={slotIndex}, old={oldItem.AnimatorItemID}, new={newItem.AnimatorItemID}");
            
            OnEquipmentChanged?.Invoke(this, new EquipmentChangedEventArgs(slotIndex, oldItem, newItem));
        }
        
        #region IEquipmentProvider Implementation
        
        public ItemInfo GetEquippedItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _equippedItems.Length)
                return ItemInfo.Empty;
            
            return _equippedItems[slotIndex];
        }
        
        public bool IsSlotOccupied(int slotIndex)
        {
            return !GetEquippedItem(slotIndex).IsEmpty;
        }
        
        public void EquipItem(int slotIndex, Entity itemEntity)
        {
            // This would be called by UI or other systems
            // For now, just update local state - ECS sync handles the rest
            if (slotIndex < 0 || slotIndex >= _equippedItems.Length)
                return;
            
            var oldItem = _equippedItems[slotIndex];
            var newItem = ReadItemInfoFromEntity(itemEntity);
            
            _equippedItems[slotIndex] = newItem;
            FireEquipmentChanged(slotIndex, oldItem, newItem);
        }
        
        public void UnequipItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _equippedItems.Length)
                return;
            
            var oldItem = _equippedItems[slotIndex];
            _equippedItems[slotIndex] = ItemInfo.Empty;
            FireEquipmentChanged(slotIndex, oldItem, ItemInfo.Empty);
        }
        
        #endregion
    }
}
