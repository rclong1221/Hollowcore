#pragma warning disable CS0162 // Unreachable code detected - intentional debug toggle
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;

namespace DIG.Items.Systems
{
    /// <summary>
    /// Reads player input and generates ItemSwitchRequest components.
    ///
    /// Input mappings:
    /// - Scroll wheel up/down: Cycle next/previous
    /// - Number keys 1-9: Quick slots
    /// - Q key: Switch to last weapon
    /// - H key: Holster/unholster
    ///
    /// This system reads from the PlayerInput component (or your input system)
    /// and translates to ItemSwitchRequest for processing by ItemSetSwitchSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(ItemSetSwitchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ItemSwitchInputSystem : ISystem
    {
        // Toggle debug logging for this system. Set to true to enable logs.
        private const bool DebugEnabled = false;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool isServer = state.WorldUnmanaged.IsServer();

            // Process entities with both input and switch request components
            // Server: Process all players (authoritative)
            // Client: Only process local player to avoid creating switch requests for remote players
            foreach (var (input, switchRequest, entity) in
                     SystemAPI.Query<RefRO<WeaponSwitchInput>, RefRW<ItemSwitchRequest>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Client-side filter: only process local player
                if (!isServer && (!SystemAPI.HasComponent<GhostOwnerIsLocal>(entity) || !SystemAPI.IsComponentEnabled<GhostOwnerIsLocal>(entity)))
                    continue;

                var inputData = input.ValueRO;
                ref var request = ref switchRequest.ValueRW;

                // Skip if no valid input this tick — let pending requests be processed by ItemSetSwitchSystem
                if (inputData.EquipSlotId < 0 || inputData.EquipQuickSlot <= 0)
                    continue;

                // New input always overrides pending requests (latest key press wins).
                // Previously: if (request.Pending) continue; — this dropped inputs during cooldown.
                // Check slot-based equip (from DIGEquipmentProvider via PlayerInputState)
                // This respects EquipmentSlotDefinition.RequiredModifier (EPIC14.5)
                if (inputData.EquipSlotId >= 0 && inputData.EquipQuickSlot > 0)
                {
                        if (inputData.EquipSlotId == 0)
                        {
                        // Main hand (slot 0)
                        request.SwitchType = ItemSwitchType.SwitchToQuickSlot;
                        request.QuickSlotNumber = inputData.EquipQuickSlot;
                        request.Pending = true;
                        if (DebugEnabled)
                            UnityEngine.Debug.Log($"[DIG.Weapons] [ItemSwitchInputSystem] Requesting MAIN HAND switch to slot {inputData.EquipQuickSlot}");
                    }
                    else if (inputData.EquipSlotId == 1)
                    {
                        // Off hand (slot 1)
                        request.SwitchType = ItemSwitchType.OffHandQuickSlot;
                        request.QuickSlotNumber = inputData.EquipQuickSlot;
                        request.Pending = true;
                        if (DebugEnabled) UnityEngine.Debug.Log($"[DIG.Weapons] [ItemSwitchInputSystem] Requesting OFF-HAND switch to slot {inputData.EquipQuickSlot}");
                    }
                    else
                    {
                        // Future slots (armor, accessory, etc) - can extend here
                        if (DebugEnabled) UnityEngine.Debug.Log($"[DIG.Weapons] [ItemSwitchInputSystem] Slot {inputData.EquipSlotId} not yet implemented");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Input component for weapon switching.
    /// Populated by WeaponSwitchInputBridgeSystem from PlayerInput.
    /// NOTE: Legacy fields (ScrollDelta, QuickSlotPressed, SwitchToLastPressed, HolsterPressed,
    /// NextWeaponPressed, PreviousWeaponPressed) have been removed. All weapon switching now
    /// goes through the data-driven slot system (EPIC14.5).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct WeaponSwitchInput : IComponentData
    {
        /// <summary>
        /// Generic slot ID for equip request (-1 = none, 0 = MainHand, 1 = OffHand, etc).
        /// Set by DIGEquipmentProvider respecting EquipmentSlotDefinition.RequiredModifier.
        /// </summary>
        [GhostField]
        public int EquipSlotId;
        
        /// <summary>
        /// Generic quick slot number for equip request (1-9), 0 if none.
        /// Used with EquipSlotId for data-driven slot selection.
        /// </summary>
        [GhostField]
        public int EquipQuickSlot;
    }

    // WeaponSwitchAuthoring moved to Items/Authoring/WeaponSwitchAuthoring.cs

    /// <summary>
    /// Bridges the gap between the main PlayerInput (networked) and the WeaponSwitchInput component.
    /// This ensures that inputs captured by PlayerInputSystem are propagated to weapon switching logic.
    /// NOTE: Legacy fields have been removed. All weapon switching now goes through EquipSlotId/EquipQuickSlot.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(ItemSwitchInputSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct WeaponSwitchInputBridgeSystem : ISystem
    {
        private const bool DebugEnabled = false;
        private bool _hasLoggedOnce;

        public void OnUpdate(ref SystemState state)
        {
            int entityCount = 0;
            bool isServer = state.WorldUnmanaged.IsServer();

            // Server: Process all players (authoritative)
            // Client: Only process local player to avoid mirroring weapon switch to remote players
            foreach (var (playerInput, switchInput, entity) in
                     SystemAPI.Query<RefRO<PlayerInput>, RefRW<WeaponSwitchInput>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Client-side filter: only process local player
                if (!isServer && (!SystemAPI.HasComponent<GhostOwnerIsLocal>(entity) || !SystemAPI.IsComponentEnabled<GhostOwnerIsLocal>(entity)))
                    continue;

                entityCount++;
                var pi = playerInput.ValueRO;
                ref var sw = ref switchInput.ValueRW;

                // Reset per frame
                sw.EquipSlotId = -1;
                sw.EquipQuickSlot = 0;

                // Map slot-based equip (from DIGEquipmentProvider via PlayerInputState)
                // This respects EquipmentSlotDefinition.RequiredModifier (EPIC14.5)
                if (pi.EquipSlotId >= 0 && pi.EquipQuickSlot > 0)
                {
                    sw.EquipSlotId = pi.EquipSlotId;
                    sw.EquipQuickSlot = pi.EquipQuickSlot;
                    if (DebugEnabled) UnityEngine.Debug.Log($"[DIG.Weapons] [Bridge] Bridged EquipSlotId={pi.EquipSlotId} EquipQuickSlot={pi.EquipQuickSlot}");
                }
            }

            // Log once if no entities found (helps debug missing components)
                if (!_hasLoggedOnce && entityCount == 0)
            {
                // Count entities with each component separately to identify issue
                int playerInputCount = 0;
                int weaponSwitchInputCount = 0;
                
                foreach (var _ in SystemAPI.Query<RefRO<PlayerInput>>().WithAll<Simulate>())
                    playerInputCount++;
                foreach (var _ in SystemAPI.Query<RefRO<WeaponSwitchInput>>().WithAll<Simulate>())
                    weaponSwitchInputCount++;
                
                if (DebugEnabled)
                    UnityEngine.Debug.LogWarning($"[DIG.Weapons] [Bridge] No entities with BOTH PlayerInput AND WeaponSwitchInput! PlayerInput count: {playerInputCount}, WeaponSwitchInput count: {weaponSwitchInputCount}");
                _hasLoggedOnce = true;
            }
            else if (entityCount > 0)
            {
                _hasLoggedOnce = true; // Reset for next session
            }
        }
    }
}
