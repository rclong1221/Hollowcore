using Unity.Entities;
using Unity.NetCode;
using Player.Components;
using DIG.Combat.Components;
using DIG.Combat.Resources;
using DIG.Economy;
using DIG.Player;
using DIG.Shared;
using DIG.Items;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Monitors component changes via per-component change filters and sets
    /// only the relevant dirty flags on save state child entities. Each module has one or
    /// more EntityQueries with SetChangedVersionFilter — IsEmpty is O(1) per query
    /// (chunk version comparison). When no components changed, early-outs immediately.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class SaveDirtyTrackingSystem : SystemBase
    {
        // PlayerStats (TypeId=1): Health, CharacterAttributes, PlayerStamina, ShieldComponent, ResourcePool, CurrencyInventory
        EntityQuery _healthQ;
        EntityQuery _attrsQ;
        EntityQuery _staminaQ;
        EntityQuery _shieldQ;
        EntityQuery _resourceQ;
        EntityQuery _currencyQ;

        // Inventory (TypeId=2)
        EntityQuery _inventoryQ;

        // Equipment (TypeId=3)
        EntityQuery _equipmentQ;

        // StatusEffects (TypeId=8)
        EntityQuery _statusQ;

        // Survival (TypeId=9): PlayerHunger, PlayerThirst, PlayerOxygen, PlayerSanity, PlayerInfection
        EntityQuery _hungerQ;
        EntityQuery _thirstQ;
        EntityQuery _oxygenQ;
        EntityQuery _sanityQ;
        EntityQuery _infectionQ;

        // Progression (TypeId=10)
        EntityQuery _progressionQ;

        protected override void OnCreate()
        {
            RequireForUpdate<SaveManagerSingleton>();

            // PlayerStats
            _healthQ = GetEntityQuery(ComponentType.ReadOnly<Health>(), ComponentType.ReadOnly<SaveStateLink>());
            _healthQ.SetChangedVersionFilter(ComponentType.ReadOnly<Health>());

            _attrsQ = GetEntityQuery(ComponentType.ReadOnly<CharacterAttributes>(), ComponentType.ReadOnly<SaveStateLink>());
            _attrsQ.SetChangedVersionFilter(ComponentType.ReadOnly<CharacterAttributes>());

            _staminaQ = GetEntityQuery(ComponentType.ReadOnly<PlayerStamina>(), ComponentType.ReadOnly<SaveStateLink>());
            _staminaQ.SetChangedVersionFilter(ComponentType.ReadOnly<PlayerStamina>());

            _shieldQ = GetEntityQuery(ComponentType.ReadOnly<ShieldComponent>(), ComponentType.ReadOnly<SaveStateLink>());
            _shieldQ.SetChangedVersionFilter(ComponentType.ReadOnly<ShieldComponent>());

            _resourceQ = GetEntityQuery(ComponentType.ReadOnly<ResourcePool>(), ComponentType.ReadOnly<SaveStateLink>());
            _resourceQ.SetChangedVersionFilter(ComponentType.ReadOnly<ResourcePool>());

            _currencyQ = GetEntityQuery(ComponentType.ReadOnly<CurrencyInventory>(), ComponentType.ReadOnly<SaveStateLink>());
            _currencyQ.SetChangedVersionFilter(ComponentType.ReadOnly<CurrencyInventory>());

            // Inventory
            _inventoryQ = GetEntityQuery(ComponentType.ReadOnly<InventoryItem>(), ComponentType.ReadOnly<SaveStateLink>());
            _inventoryQ.SetChangedVersionFilter(ComponentType.ReadOnly<InventoryItem>());

            // Equipment
            _equipmentQ = GetEntityQuery(ComponentType.ReadOnly<EquippedItemElement>(), ComponentType.ReadOnly<SaveStateLink>());
            _equipmentQ.SetChangedVersionFilter(ComponentType.ReadOnly<EquippedItemElement>());

            // StatusEffects
            _statusQ = GetEntityQuery(ComponentType.ReadOnly<StatusEffect>(), ComponentType.ReadOnly<SaveStateLink>());
            _statusQ.SetChangedVersionFilter(ComponentType.ReadOnly<StatusEffect>());

            // Survival
            _hungerQ = GetEntityQuery(ComponentType.ReadOnly<PlayerHunger>(), ComponentType.ReadOnly<SaveStateLink>());
            _hungerQ.SetChangedVersionFilter(ComponentType.ReadOnly<PlayerHunger>());

            _thirstQ = GetEntityQuery(ComponentType.ReadOnly<PlayerThirst>(), ComponentType.ReadOnly<SaveStateLink>());
            _thirstQ.SetChangedVersionFilter(ComponentType.ReadOnly<PlayerThirst>());

            _oxygenQ = GetEntityQuery(ComponentType.ReadOnly<PlayerOxygen>(), ComponentType.ReadOnly<SaveStateLink>());
            _oxygenQ.SetChangedVersionFilter(ComponentType.ReadOnly<PlayerOxygen>());

            _sanityQ = GetEntityQuery(ComponentType.ReadOnly<PlayerSanity>(), ComponentType.ReadOnly<SaveStateLink>());
            _sanityQ.SetChangedVersionFilter(ComponentType.ReadOnly<PlayerSanity>());

            _infectionQ = GetEntityQuery(ComponentType.ReadOnly<PlayerInfection>(), ComponentType.ReadOnly<SaveStateLink>());
            _infectionQ.SetChangedVersionFilter(ComponentType.ReadOnly<PlayerInfection>());

            // Progression
            _progressionQ = GetEntityQuery(ComponentType.ReadOnly<DIG.Progression.PlayerProgression>(), ComponentType.ReadOnly<SaveStateLink>());
            _progressionQ.SetChangedVersionFilter(ComponentType.ReadOnly<DIG.Progression.PlayerProgression>());
        }

        protected override void OnUpdate()
        {
            uint dirtyMask = 0;

            // PlayerStats (TypeId=1)
            if (!_healthQ.IsEmpty || !_attrsQ.IsEmpty || !_staminaQ.IsEmpty ||
                !_shieldQ.IsEmpty || !_resourceQ.IsEmpty || !_currencyQ.IsEmpty)
                dirtyMask |= (1u << (SaveModuleTypeIds.PlayerStats - 1));

            // Inventory (TypeId=2)
            if (!_inventoryQ.IsEmpty)
                dirtyMask |= (1u << (SaveModuleTypeIds.Inventory - 1));

            // Equipment (TypeId=3)
            if (!_equipmentQ.IsEmpty)
                dirtyMask |= (1u << (SaveModuleTypeIds.Equipment - 1));

            // StatusEffects (TypeId=8)
            if (!_statusQ.IsEmpty)
                dirtyMask |= (1u << (SaveModuleTypeIds.StatusEffects - 1));

            // Survival (TypeId=9)
            if (!_hungerQ.IsEmpty || !_thirstQ.IsEmpty || !_oxygenQ.IsEmpty ||
                !_sanityQ.IsEmpty || !_infectionQ.IsEmpty)
                dirtyMask |= (1u << (SaveModuleTypeIds.Survival - 1));

            // Progression (TypeId=10)
            if (!_progressionQ.IsEmpty)
                dirtyMask |= (1u << (SaveModuleTypeIds.Progression - 1));

            if (dirtyMask == 0) return;

            // Apply accumulated dirty mask to all player save entities
            foreach (var link in SystemAPI.Query<RefRO<SaveStateLink>>())
            {
                if (link.ValueRO.SaveChildEntity == Entity.Null) continue;
                if (!EntityManager.HasComponent<SaveDirtyFlags>(link.ValueRO.SaveChildEntity)) continue;

                var flags = EntityManager.GetComponentData<SaveDirtyFlags>(link.ValueRO.SaveChildEntity);
                flags.Flags |= dirtyMask;
                EntityManager.SetComponentData(link.ValueRO.SaveChildEntity, flags);
            }
        }
    }
}
