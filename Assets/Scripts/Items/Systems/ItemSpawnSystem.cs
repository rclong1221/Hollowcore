#pragma warning disable CS0162 // Unreachable code detected - intentional debug toggle
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Weapons;

namespace DIG.Items.Systems
{
    /// <summary>
    /// SERVER ONLY: Spawns weapon prefabs as runtime entities when a player first spawns.
    /// 
    /// Problem: StartingInventoryAuthoring bakes prefab entity references into ItemSetEntry buffer.
    /// These are prefab entities, not runtime instances. Weapons must be instantiated so:
    /// 1. They exist in the world (systems can query them)
    /// 2. They can be added to LinkedEntityGroup (for ghost replication to clients)
    /// 3. They have runtime state (UseRequest, MeleeState, etc.)
    /// 
    /// Flow:
    /// 1. Player spawns with ItemSetEntry buffer containing prefab references
    /// 2. This system instantiates each prefab as a ghost entity
    /// 3. Updates ItemSetEntry.ItemEntity with the new runtime entity
    /// 4. Adds weapons to player's LinkedEntityGroup for replication
    /// 5. Sets CharacterItem.OwnerEntity and SlotId
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InventoryBindingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ItemSpawnSystem : ISystem
    {
        // Toggle debug logging for this system. Set to true to enable logs.
        private const bool DebugEnabled = false;
        private ComponentLookup<UsableAction> _usableActionLookup;
        private ComponentLookup<CharacterItem> _charItemLookup;
        private ComponentLookup<Prefab> _prefabLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _usableActionLookup = state.GetComponentLookup<UsableAction>(true);
            _charItemLookup = state.GetComponentLookup<CharacterItem>(false);
            _prefabLookup = state.GetComponentLookup<Prefab>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);

            if (DebugEnabled)
            {
                UnityEngine.Debug.Log($"[ItemSpawnSystem] OnCreate in World={state.World.Name}");
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            _usableActionLookup.Update(ref state);
            _charItemLookup.Update(ref state);
            _prefabLookup.Update(ref state);
            _transformLookup.Update(ref state);
            
            // Collect players that need weapon spawning
            var playersToProcess = new NativeList<Entity>(Allocator.Temp);
            
            // Find players with ItemSetEntry buffer containing entities that need ownership setup
            foreach (var (itemSets, playerEntity) in 
                     SystemAPI.Query<DynamicBuffer<ItemSetEntry>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Debug: Log what we find in the buffer
                bool loggedHeader = false;
                
                // Check if any weapon entities need owner setup
                if (itemSets.Length > 0 && DebugEnabled)
                    UnityEngine.Debug.Log($"[ItemSpawnSystem] Processing Player {playerEntity.Index} with {itemSets.Length} ItemSet entries.");

                for (int i = 0; i < itemSets.Length; i++)
                {
                    var entry = itemSets[i];
                    
                    if (entry.ItemEntity == Entity.Null)
                    {
                         if (DebugEnabled) UnityEngine.Debug.LogWarning($"    [ItemSpawnSystem] NULL ItemEntity for Slot {entry.QuickSlot}!");
                         continue;
                    }

                    if (DebugEnabled)
                        UnityEngine.Debug.Log($"  > [ItemSpawnSystem] Checking ItemSet[{i}]: QuickSlot={entry.QuickSlot} ItemEntity={entry.ItemEntity.Index}");
                       // Check components on the potential prefab
                    // Note: If it's a prefab, these lookups check the prefab itself.
                    bool isPrefab = _prefabLookup.HasComponent(entry.ItemEntity);
                    bool hasGhostAuthoring = state.EntityManager.HasComponent<GhostAuthoringComponent>(entry.ItemEntity); // Runtime metadata
                    bool hasGhostInstance = state.EntityManager.HasComponent<GhostInstance>(entry.ItemEntity); // Should not exist on prefab usually? Wait, baked prefabs might have it?
                    // Actually, baked ghost prefabs have 'GhostComponent' (internal) or 'GhostInstance' (if using some setups).
                    // In NetCode 1.0+, valid ghost prefabs have 'GhostComponent'.
                    bool hasGhostComp = state.EntityManager.HasComponent<GhostInstance>(entry.ItemEntity);
                    
// [Diag Block Removed]

                    // If it's already a runtime entity, they check the runtime entity.
                    bool hasCharItem = _charItemLookup.HasComponent(entry.ItemEntity);
                    bool hasUsable = _usableActionLookup.HasComponent(entry.ItemEntity);
                    
                    // Check if owner is already set (only relevant if it's NOT a prefab and has CharacterItem)
                    bool needsOwnerSetup = false;
                    if (!isPrefab && hasCharItem)
                    {
                        var charItem = _charItemLookup[entry.ItemEntity];
                        needsOwnerSetup = charItem.OwnerEntity == Entity.Null;
                    }
                    
                    if (!loggedHeader)
                    {
                        if (DebugEnabled)
                        {
                            UnityEngine.Debug.Log($"[ItemSpawnSystem] Checking Player={playerEntity.Index}");
                        }
                        loggedHeader = true;
                    }
                    if (DebugEnabled)
                    {
                        UnityEngine.Debug.Log($"[ItemSpawnSystem]   ItemSet[{i}] Entity={entry.ItemEntity.Index} QuickSlot={entry.QuickSlot} IsPrefab={isPrefab} HasCharItem={hasCharItem} HasUsable={hasUsable} NeedsOwner={needsOwnerSetup}");
                    }
                    
                    // Process if it's a prefab OR if it needs owner setup
                    if (isPrefab || needsOwnerSetup)
                    {
                        playersToProcess.Add(playerEntity);
                        break;
                    }
                }
            }
            
            // Process each player outside the query (to allow buffer modification)
            for (int p = 0; p < playersToProcess.Length; p++)
            {
                var playerEntity = playersToProcess[p];
                var itemSets = state.EntityManager.GetBuffer<ItemSetEntry>(playerEntity);
                
                // Get LinkedEntityGroup buffer for adding weapons
                DynamicBuffer<LinkedEntityGroup> linkedGroup = default;
                bool hasLinkedGroup = state.EntityManager.HasBuffer<LinkedEntityGroup>(playerEntity);
                if (hasLinkedGroup)
                {
                    linkedGroup = state.EntityManager.GetBuffer<LinkedEntityGroup>(playerEntity);
                }
                
                // Process each weapon entry
                for (int i = 0; i < itemSets.Length; i++)
                {
                    var entry = itemSets[i];
                    
                    if (entry.ItemEntity == Entity.Null)
                    {
                        continue;
                    }
                    
                    Entity weaponEntity = entry.ItemEntity;
                    bool isPrefab = _prefabLookup.HasComponent(weaponEntity);
                                        // Check if we need to spawn a new instance
                        if (isPrefab)
                        {
                            var prefabRef = entry.ItemEntity; // Use local var to be clear
                            
                            // DEBUG: Check the PREFAB ITSELF for Ghost Component components BEFORE instantiation
                            // if (DebugEnabled && entry.QuickSlot == 4)
                            // {
                            //     bool pfHasGhostComp = state.EntityManager.HasComponent<GhostInstance>(prefabRef);
                            //     bool pfHasGhostInst = state.EntityManager.HasComponent<GhostInstance>(prefabRef);
                            //     bool pfHasGhostAuth = state.EntityManager.HasComponent<GhostAuthoringComponent>(prefabRef);
                                
                            //     // DEBUG: Changed to LogError to ensure it appears in Console even if Warnings are hidden
                            //     UnityEngine.Debug.LogError($"[ItemSpawnSystem] PREFAB DIAGNOSIS (Slot 4): PrefabEntity={prefabRef.Index} " +
                            //                                  $"HasGhostComponent={pfHasGhostComp} HasGhostInstance={pfHasGhostInst} HasGhostAuthoring={pfHasGhostAuth}");
                            // }

                            weaponEntity = state.EntityManager.Instantiate(prefabRef);
                            
                            if (DebugEnabled)
                            {
                                UnityEngine.Debug.Log($"[ItemSpawnSystem] Spawned weapon from prefab {prefabRef.Index} -> {weaponEntity.Index} for Player {playerEntity.Index} QuickSlot={entry.QuickSlot}");
                            }                      
                            // CRITICAL FIX: Spawn weapon entities hidden/disabled
                            // Weapon ECS entities don't need visible transforms - visuals are handled by
                            // MonoBehaviour side (WeaponEquipVisualBridge). Setting position far underground
                            // and scale to near-zero prevents:
                            // 1. Colliders blocking player movement
                            // 2. Visible mesh at player's feet
                            // 3. NetCode ghost position issues (position stays consistent)
                            if (_transformLookup.HasComponent(weaponEntity))
                            {
                                var weaponTransform = _transformLookup[weaponEntity];
                                // Position far underground where it can't interfere
                                // Using -5f as -1000f and even -50f might be kill zones
                                weaponTransform.Position = new float3(0, -5f, 0);
                                // Zero scale makes any remaining visuals/colliders invisible
                                weaponTransform.Scale = 0.0001f;
                                _transformLookup[weaponEntity] = weaponTransform;
                                // Verify the spawned entity
                            // SAFEGUARD: Check if the user accidentally assigned a LIVE PROJECTILE/EXPLOSIVE as the Inventory Item.
                            bool isVolatile = state.EntityManager.HasComponent<Projectile>(weaponEntity) || 
                                              state.EntityManager.HasComponent<DetonateOnTimer>(weaponEntity) ||
                                              state.EntityManager.HasComponent<DIG.Voxel.VoxelDetonationRequest>(weaponEntity);
                            
                            if (isVolatile)
                            {
                                UnityEngine.Debug.LogError($"[ItemSpawnSystem] CRITICAL SETUP ERROR: The Item assigned to QuickSlot {entry.QuickSlot} (Entity {weaponEntity.Index}) contains Projectile/Explosive components! " +
                                                           "It will destroy itself or fall through the map immediately. " +
                                                           "You must assign the 'Weapon' prefab (e.g. FragGrenadeWeaponRight), NOT the 'Projectile' prefab (e.g. FragGrenadeProjectile).");
                            }

                                if (DebugEnabled)
                                {
                                    UnityEngine.Debug.Log($"[ItemSpawnSystem] Hidden weapon {weaponEntity.Index} at underground position");
                                }
                            }
                            


                            // Update ItemSetEntry with the runtime entity
                            entry.ItemEntity = weaponEntity;
                            itemSets[i] = entry;
                            
                            // Add to LinkedEntityGroup for ghost replication
                            if (hasLinkedGroup)
                            {
                                bool hasGhost = state.EntityManager.HasComponent<GhostInstance>(weaponEntity);
                                if (!hasGhost)
                                {
                                    UnityEngine.Debug.LogError($"[ItemSpawnSystem] REPLICATION ERROR: Spawned Item {weaponEntity.Index} (Slot {entry.QuickSlot}) is MISSING GhostComponent! " +
                                                               "It will NOT exist on Client. " +
                                                               "Add 'GhostAuthoring' component to the Weapon Prefab.");
                                }
                                linkedGroup.Add(new LinkedEntityGroup { Value = weaponEntity });
                            }
                        }

                    
                    // Set owner and slot on CharacterItem (whether newly spawned or existing)
                    if (_charItemLookup.HasComponent(weaponEntity))
                    {
                        var charItem = _charItemLookup[weaponEntity];

                        // Only set SlotId for UNEQUIPPED items (SlotId == -1)
                        // Don't overwrite SlotId for items that have been equipped to MainHand (0) or OffHand (1)
                        // This was causing bugs where equipped items would have their SlotId reset
                        bool needsUpdate = false;

                        // Only set initial SlotId if item hasn't been equipped yet
                        if (charItem.SlotId == -1)
                        {
                            // Use QuickSlot for unequipped items so InventoryBindingSystem can match them
                            charItem.SlotId = entry.QuickSlot;
                            needsUpdate = true;
                        }

                        if (charItem.OwnerEntity == Entity.Null)
                        {
                            charItem.OwnerEntity = playerEntity;
                            needsUpdate = true;
                        }

                        if (needsUpdate)
                        {
                            _charItemLookup[weaponEntity] = charItem;
                            if (DebugEnabled)
                            {
                                UnityEngine.Debug.Log($"[ItemSpawnSystem] Updated Weapon={weaponEntity.Index} SlotId={charItem.SlotId} Owner={playerEntity.Index}");
                            }
                        }
                    }
                }
            }

            // DEBUG: Verify all spawned weapons still exist at end of this system
            if (DebugEnabled && playersToProcess.Length > 0)
            {
                foreach (var playerEntity in playersToProcess)
                {
                    if (!state.EntityManager.HasBuffer<ItemSetEntry>(playerEntity)) continue;
                    var itemSets = state.EntityManager.GetBuffer<ItemSetEntry>(playerEntity);
                    for (int i = 0; i < itemSets.Length; i++)
                    {
                        var entry = itemSets[i];
                        if (entry.ItemEntity != Entity.Null)
                        {
                            bool exists = state.EntityManager.Exists(entry.ItemEntity);
                            bool hasThrowable = exists && state.EntityManager.HasComponent<ThrowableAction>(entry.ItemEntity);
                            if (entry.QuickSlot == 4) // Grenade slot
                            {
                                UnityEngine.Debug.Log($"[ItemSpawnSystem] END_VERIFY QuickSlot=4 Entity={entry.ItemEntity.Index} Exists={exists} HasThrowable={hasThrowable}");
                            }
                        }
                    }
                }
            }

            playersToProcess.Dispose();
        }
    }
}
