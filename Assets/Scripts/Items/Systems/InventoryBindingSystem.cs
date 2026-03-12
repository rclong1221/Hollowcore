#pragma warning disable CS0162 // Unreachable code detected - intentional debug toggle
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Weapons;

namespace DIG.Items.Systems
{
    /// <summary>
    /// Binds spawned weapon entities to the ItemSetEntry buffer.
    /// 
    /// Problem: StartingInventoryAuthoring bakes prefab entity references, but at runtime
    /// in NetCode, the actual ghost entities have different indices. This system finds
    /// spawned weapons and updates the ItemSetEntry buffer with valid runtime entity references.
    /// 
    /// Strategy: Match weapons to QuickSlots by CharacterItem.SlotId (set by ItemSpawnSystem on server).
    /// This ensures correct weapon→slot mapping across server and client.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct InventoryBindingSystem : ISystem
    {
        private const bool DebugLogging = false;
        
        private bool _hasLoggedDiagnostic;
        private bool _hasLoggedWeapons;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // DEBUG: Periodic logging on client to track weapon discovery
            bool isServer = state.WorldUnmanaged.IsServer();
            bool shouldLogThisFrame = DebugLogging && !isServer && UnityEngine.Time.frameCount % 300 == 0;
            
            if (shouldLogThisFrame)
            {
                UnityEngine.Debug.Log($"[InventoryBindingSystem] CLIENT_PERIODIC_CHECK Frame={UnityEngine.Time.frameCount}");
            }
            
            // Collect all weapons with UsableAction AND CharacterItem (need SlotId for matching)
            var weapons = new NativeList<WeaponInfo>(Allocator.Temp);
            
            // Query 1: Items with UsableAction (weapons)
            foreach (var (action, charItem, entity) in 
                     SystemAPI.Query<RefRO<UsableAction>, RefRO<CharacterItem>>()
                     .WithEntityAccess())
            {
                // Only include weapons that have a valid SlotId assigned by server
                int slotId = charItem.ValueRO.SlotId;
                
                // DEBUG: Log weapon components (first time OR periodically)
                if (DebugLogging && (!_hasLoggedWeapons || shouldLogThisFrame))
                {
                    bool hasFire = state.EntityManager.HasComponent<WeaponFireComponent>(entity);
                    bool hasMelee = state.EntityManager.HasComponent<MeleeAction>(entity);
                    UnityEngine.Debug.Log($"[InventoryBindingSystem] WEAPON Entity={entity.Index} AnimID={action.ValueRO.AnimatorItemID} SlotId={slotId} HasFire={hasFire} HasMelee={hasMelee} World={state.WorldUnmanaged.Name}");
                }
                
                // Only add weapons with valid slot assignments (SlotId > 0 means assigned by server)
                if (slotId > 0)
                {
                    weapons.Add(new WeaponInfo
                    {
                        Entity = entity,
                        AnimatorItemID = action.ValueRO.AnimatorItemID,
                        SlotId = slotId
                    });
                }
            }
            
            // Query 2: Items with CharacterItem + ItemAnimationConfig but WITHOUT UsableAction (shields, off-hand items)
            foreach (var (animConfig, charItem, entity) in 
                     SystemAPI.Query<RefRO<ItemAnimationConfig>, RefRO<CharacterItem>>()
                     .WithNone<UsableAction>()
                     .WithEntityAccess())
            {
                int slotId = charItem.ValueRO.SlotId;
                
                if (DebugLogging && (!_hasLoggedWeapons || shouldLogThisFrame))
                {
                    UnityEngine.Debug.Log($"[InventoryBindingSystem] ITEM (no UsableAction) Entity={entity.Index} AnimID={animConfig.ValueRO.AnimatorItemID} SlotId={slotId} World={state.WorldUnmanaged.Name}");
                }
                
                if (slotId > 0)
                {
                    weapons.Add(new WeaponInfo
                    {
                        Entity = entity,
                        AnimatorItemID = animConfig.ValueRO.AnimatorItemID,
                        SlotId = slotId
                    });
                }
            }

            // Log available weapons once
            if (DebugLogging && !_hasLoggedWeapons && weapons.Length > 0)
            {
                var info = new FixedString512Bytes();
                info.Append((FixedString64Bytes)"Found ");
                info.Append(weapons.Length);
                info.Append((FixedString64Bytes)" weapons: ");
                for (int i = 0; i < weapons.Length; i++)
                {
                    info.Append((FixedString32Bytes)"[Slot=");
                    info.Append(weapons[i].SlotId);
                    info.Append((FixedString32Bytes)",AnimID=");
                    info.Append(weapons[i].AnimatorItemID);
                    info.Append((FixedString32Bytes)",E=");
                    info.Append(weapons[i].Entity.Index);
                    info.Append(']');
                    if (i < weapons.Length - 1) info.Append(',');
                }
                UnityEngine.Debug.Log($"[DIG.Weapons] [InventoryBindingSystem] {info}");
                _hasLoggedWeapons = true;
            }

            if (weapons.Length == 0)
            {
                weapons.Dispose();
                return;
            }

            // Collect entities that need binding
            var entitiesToUpdate = new NativeList<Entity>(Allocator.Temp);
            
            foreach (var (itemSets, entity) in 
                     SystemAPI.Query<DynamicBuffer<ItemSetEntry>>()
                     .WithAll<Simulate, ActiveSlotIndex>()
                     .WithEntityAccess())
            {
                // Check if any entries need binding OR ownership propagation (Server Only)
                for (int i = 0; i < itemSets.Length; i++)
                {
                    var entry = itemSets[i];
                    bool needsBinding = entry.ItemEntity == Entity.Null || !state.EntityManager.Exists(entry.ItemEntity);
                    
                    // On server, also ensure the weapon ghost is owned by the player for prediction
                    if (!needsBinding && isServer && SystemAPI.HasComponent<GhostOwner>(entity))
                    {
                        var playerOwner = SystemAPI.GetComponent<GhostOwner>(entity);
                        if (SystemAPI.HasComponent<GhostOwner>(entry.ItemEntity))
                        {
                            var weaponOwner = SystemAPI.GetComponent<GhostOwner>(entry.ItemEntity);
                            if (weaponOwner.NetworkId != playerOwner.NetworkId) needsBinding = true;
                        }
                        else needsBinding = true;
                    }

                    if (needsBinding)
                    {
                        entitiesToUpdate.Add(entity);
                        break;
                    }
                }
            }

            // Now update the buffers by matching weapons to QuickSlots via CharacterItem.SlotId
            bool anyBindingDone = false;
            for (int e = 0; e < entitiesToUpdate.Length; e++)
            {
                var playerEntity = entitiesToUpdate[e];
                var itemSets = state.EntityManager.GetBuffer<ItemSetEntry>(playerEntity);
                
                // Match each ItemSetEntry to a weapon by SlotId
                for (int i = 0; i < itemSets.Length; i++)
                {
                    var entry = itemSets[i];
                    
                    // Skip if already bound to a valid entity
                    if (entry.ItemEntity != Entity.Null && state.EntityManager.Exists(entry.ItemEntity))
                    {
                        // Double-check the entity still has UsableAction
                        if (state.EntityManager.HasComponent<UsableAction>(entry.ItemEntity))
                        {
                            continue;
                        }
                        // Entity exists but lost UsableAction - needs rebinding
                    }
                    
                    // Find a weapon with matching SlotId
                    Entity matchedWeapon = Entity.Null;
                    for (int w = 0; w < weapons.Length; w++)
                    {
                        if (weapons[w].SlotId == entry.QuickSlot)
                        {
                            matchedWeapon = weapons[w].Entity;
                            if (DebugLogging)
                                UnityEngine.Debug.Log($"[InventoryBindingSystem] Matched weapon Entity={matchedWeapon.Index} SlotId={weapons[w].SlotId} AnimatorItemID={weapons[w].AnimatorItemID} to QuickSlot={entry.QuickSlot}");
                            break;
                        }
                    }
                    
                    if (matchedWeapon == Entity.Null)
                    {
                        // No weapon with matching SlotId found yet - wait for server to assign
                        continue;
                    }
                    
                    // Bind the matched weapon
                    entry.ItemEntity = matchedWeapon;
                    itemSets[i] = entry;
                    anyBindingDone = true;

                    // SERVER ONLY: Propagate GhostOwner to matched weapon.
                    // This is CRITICAL for weapons to be predictable on the client.
                    if (isServer && SystemAPI.HasComponent<GhostOwner>(playerEntity))
                    {
                        var owner = SystemAPI.GetComponent<GhostOwner>(playerEntity);
                        if (owner.NetworkId != -1)
                        {
                            if (state.EntityManager.HasComponent<GhostOwner>(matchedWeapon))
                            {
                                var weaponOwner = state.EntityManager.GetComponentData<GhostOwner>(matchedWeapon);
                                if (weaponOwner.NetworkId != owner.NetworkId)
                                {
                                    state.EntityManager.SetComponentData(matchedWeapon, new GhostOwner { NetworkId = owner.NetworkId });
                                    if (DebugLogging) UnityEngine.Debug.Log($"[InventoryBindingSystem] Set GhostOwner for weapon {matchedWeapon.Index}: NetId={owner.NetworkId}");
                                }
                            }
                            else
                            {
                                state.EntityManager.AddComponentData(matchedWeapon, new GhostOwner { NetworkId = owner.NetworkId });
                                if (DebugLogging) UnityEngine.Debug.Log($"[InventoryBindingSystem] Added GhostOwner to weapon {matchedWeapon.Index}: NetId={owner.NetworkId}");
                            }
                        }
                    }
                    
                    // SERVER ONLY: Add to LinkedEntityGroup to ensure replication to client
                    if (state.WorldUnmanaged.IsServer())
                    {
                        if (state.EntityManager.HasBuffer<LinkedEntityGroup>(playerEntity))
                        {
                            var linkedGroup = state.EntityManager.GetBuffer<LinkedEntityGroup>(playerEntity);
                            bool alreadyInGroup = false;
                            for (int k = 0; k < linkedGroup.Length; k++)
                            {
                                if (linkedGroup[k].Value == matchedWeapon)
                                {
                                    alreadyInGroup = true;
                                    break;
                                }
                            }
                            
                            if (!alreadyInGroup)
                            {
                                linkedGroup.Add(new LinkedEntityGroup { Value = matchedWeapon });
                                if (DebugLogging)
                                    UnityEngine.Debug.Log($"[InventoryBindingSystem] Added Weapon {matchedWeapon.Index} to Player {playerEntity.Index} LinkedEntityGroup for replication.");
                            }
                        }
                    }
                }
            }

            if (DebugLogging && anyBindingDone && !_hasLoggedDiagnostic)
            {
                // Log the result
                for (int e = 0; e < entitiesToUpdate.Length; e++)
                {
                    var playerEntity = entitiesToUpdate[e];
                    if (state.EntityManager.HasBuffer<ItemSetEntry>(playerEntity))
                    {
                        var itemSets = state.EntityManager.GetBuffer<ItemSetEntry>(playerEntity);
                        var slots = new FixedString512Bytes();
                        for (int i = 0; i < itemSets.Length; i++)
                        {
                            slots.Append((FixedString32Bytes)"Slot");
                            slots.Append(itemSets[i].QuickSlot);
                            slots.Append('=');
                            slots.Append(itemSets[i].ItemEntity.Index);
                            if (i < itemSets.Length - 1) slots.Append(',');
                        }
                        UnityEngine.Debug.Log($"[DIG.Weapons] [InventoryBindingSystem] Bound: [{slots}]");
                        break;
                    }
                }
                _hasLoggedDiagnostic = true;
            }

            entitiesToUpdate.Dispose();
            weapons.Dispose();
        }
        
        private struct WeaponInfo
        {
            public Entity Entity;
            public int AnimatorItemID;
            public int SlotId;
        }
    }
}
