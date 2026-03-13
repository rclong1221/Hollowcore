# EPIC 1.3: Limb Salvage & Equipping

**Status**: Planning
**Epic**: EPIC 1 — Chassis & Limb System
**Dependencies**: EPIC 1.1 (ChassisState, LimbInstance); Framework: Interaction/, Items/, Trading/

---

## Overview

Players find replacement limbs in the world and equip them into chassis slots. Sources include environment pickups, enemy corpse drops, quest rewards, and body shop vendors. Equipping a limb into an occupied slot drops the current limb as a world pickup.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Chassis/Components/LimbPickupComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Marks an entity as a world-placed limb that can be picked up.
    /// Interactable via framework Interaction/ system.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct LimbPickup : IComponentData
    {
        /// <summary>LimbDefinitionSO ID for spawning LimbInstance on pickup.</summary>
        public int LimbDefinitionId;
        public ChassisSlot SlotType;
        public LimbRarity Rarity;
        /// <summary>Pre-rolled integrity (percentage of max, 0.5-1.0 for salvage).</summary>
        public float IntegrityPercent;
    }

    /// <summary>
    /// Request to equip a limb into a chassis slot.
    /// Created by interaction system, consumed by LimbEquipSystem.
    /// Transient entity pattern.
    /// </summary>
    public struct LimbEquipRequest : IComponentData
    {
        public Entity PlayerEntity;
        public Entity LimbPickupEntity;  // World pickup being consumed
        public ChassisSlot TargetSlot;
    }

    /// <summary>
    /// Request to drop (unequip) a limb from a chassis slot.
    /// Created by UI or swap logic.
    /// </summary>
    public struct LimbDropRequest : IComponentData
    {
        public Entity PlayerEntity;
        public ChassisSlot SourceSlot;
    }
}
```

---

## Systems

### LimbEquipSystem

```csharp
// File: Assets/Scripts/Chassis/Systems/LimbEquipSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Processes LimbEquipRequest entities.
//
// For each LimbEquipRequest:
//   1. Validate: player exists, chassis entity exists, pickup entity exists
//   2. Resolve ChassisLink → ChassisState
//   3. If target slot already has a limb:
//      a. Create LimbPickup entity at player position with current limb's data (drop it)
//      b. Destroy current limb entity
//   4. Create new limb entity from LimbPickup data:
//      a. Add LimbInstance (from LimbDefinitionId lookup)
//      b. Add LimbStatBlock (from definition)
//      c. Set CurrentIntegrity = MaxIntegrity * IntegrityPercent
//   5. Set ChassisState slot to new limb entity
//   6. Clear DestroyedSlotsMask bit for this slot
//   7. Destroy the LimbPickup world entity
//   8. Destroy the LimbEquipRequest entity
//   9. Fire chassis changed event (for stat recalculation + visual update)
```

### LimbDropSystem

```csharp
// File: Assets/Scripts/Chassis/Systems/LimbDropSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Processes LimbDropRequest entities.
//
// For each LimbDropRequest:
//   1. Resolve ChassisLink → ChassisState
//   2. Get limb entity from specified slot
//   3. If slot is empty: destroy request, skip
//   4. Read LimbInstance from limb entity
//   5. Create LimbPickup world entity at player position with limb data
//   6. Set ChassisState slot to Entity.Null
//   7. Destroy limb entity
//   8. Destroy LimbDropRequest entity
```

### LimbPickupInteractionBridge

```csharp
// File: Assets/Scripts/Chassis/Bridges/LimbPickupInteractionBridge.cs
// Managed MonoBehaviour — bridges framework Interaction/ system to LimbEquipRequest.
//
// When player interacts with a LimbPickup entity:
//   1. Show limb preview UI (stats comparison with current equipped)
//   2. On confirm: create LimbEquipRequest transient entity
//   3. UI shows: current limb stats vs pickup limb stats, slot compatibility
//
// Auto-detect target slot from LimbPickup.SlotType.
// If slot type can go in multiple slots (some limbs are ambidextrous):
//   Show slot selection UI.
```

### LimbLootDropSystem

```csharp
// File: Assets/Scripts/Chassis/Systems/LimbLootDropSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// When enemies die, chance to drop a LimbPickup based on loot table.
// Hooks into existing Loot/ pipeline — LootTableSO entries can reference LimbDefinitionSO.
//
// For each enemy death event:
//   1. Roll loot table (framework handles this)
//   2. If result includes a limb drop:
//      a. Create LimbPickup entity at enemy death position
//      b. Populate from LimbDefinitionSO
//      c. Integrity = random 0.5-1.0 (salvage quality)
```

---

## Salvage Sources

| Source | How It Works | Quality |
|---|---|---|
| Environment (crates, shelves) | Pre-placed LimbPickup entities in zone generation | Junk-Uncommon |
| Enemy corpse drops | LootTableSO rolls → LimbPickup spawn | Common-Rare |
| Quest rewards | Quest completion → LimbPickup spawn at reward point | Uncommon-Epic |
| Body shop vendor | Trading/ system, buy with district currency | Common-Epic (price scales) |
| Echo mission rewards | EPIC 5 reward → LimbPickup | Rare-Legendary |
| Boss drops | Guaranteed unique limb | Epic-Legendary |

---

## Setup Guide

1. Add LimbPickup component support to zone generation (IZoneProvider places LimbPickup entities)
2. Create LimbDefinitionSO assets for starting set: `Assets/Data/Chassis/Limbs/`
   - BasicArm_Left, BasicArm_Right, BasicLeg_Left, BasicLeg_Right, BasicTorso, BasicHead
   - Per vertical-slice district: 3-5 district-themed limbs
3. Add limb entries to enemy loot tables (LootTableSO)
4. Configure body shop vendor inventory (Trading/ NPC with LimbDefinitionSO stock)
5. Add LimbPickup interaction definition to Interaction/ system config
6. Create limb preview UI prefab for stats comparison popup

---

## Verification

- [ ] LimbPickup entities appear in world (environment, enemy drops)
- [ ] Interacting with LimbPickup shows stats comparison UI
- [ ] Confirming equip: old limb dropped, new limb equipped, stats updated
- [ ] Empty slot equip: no drop, limb equipped directly
- [ ] Destroyed slot equip: DestroyedSlotsMask cleared, limb equipped
- [ ] Body shop vendor shows available limbs with prices
- [ ] Quest reward limbs spawn at correct location
- [ ] Loot table integration: enemy death → chance of LimbPickup

---

## Validation

### LimbPickup Integrity Bounds

```csharp
// File: Assets/Editor/Chassis/LimbPickupValidator.cs
// Build-time validation for pre-placed LimbPickup entities in subscenes:
// 1. IntegrityPercent must be in range [0.1, 1.0] — warn if outside
// 2. LimbDefinitionId must resolve to a valid LimbDefinitionSO — error if orphan
// 3. SlotType must match the referenced LimbDefinitionSO.SlotType — error if mismatch
// 4. Rarity must match or be <= referenced LimbDefinitionSO.Rarity — warn if override is higher
// 5. No two LimbPickup entities within 1m of each other — warn (likely duplicate placement)
```

### Loot Table Limb Entry Validation

```csharp
// Extend existing LootTableSO validation:
// - If a loot entry references a LimbDefinitionSO, verify the SO asset exists
// - Warn if limb drop weight is > 50% of total table weight (too generous)
// - Warn if enemy has no RippableLimb but loot table has limb drops (flavor mismatch)
```

---

## Editor Tooling

### Salvage Source Map (Chassis Workstation Module)

```
// File: Assets/Editor/ChassisWorkstation/SalvageSourceModule.cs
// IWorkstationModule in the Chassis Workstation (see EPIC 1.1)
//
// Shows a district-level overview of limb availability:
// - Per district: list of all LimbPickup placements (from subscene scan)
// - Per district: loot table limb drop rates from enemies in that district
// - Per district: body shop vendor stock
// - Heat map: "limb density per zone" — highlights zones with too few/many pickups
// - Warning panel: districts with no arm/leg/head pickups flagged as potential soft-locks
```

### Limb Comparison Popup

```
// File: Assets/Editor/Chassis/LimbComparisonPopup.cs
// EditorWindow spawned from LimbPickupInteractionBridge inspector:
// - Side-by-side: current equipped limb stats vs pickup limb stats
// - Green/red delta arrows on each stat (better/worse)
// - Memory bonus comparison (current district + all districts)
// - Preview: renders both limb VisualPrefabs side by side in preview pane
```

---

## Simulation & Testing

### Limb Economy Balance Test

```
// Test: LimbEconomySimulation
// Monte Carlo: simulate 100 expedition runs (deterministic seeds 0-99)
// Per run: 5 districts, ~20 enemies per district, standard loot tables
// Measure:
//   - Average limbs found per district (target: 2-4)
//   - Average limb upgrades (replacing lower rarity with higher) per run (target: 8-12)
//   - Percentage of runs where player has empty slots entering district 3+ (target: < 5%)
//   - Distribution of rarity at run end (should be mostly Uncommon-Rare, ~10% Epic)
// Purpose: validate that salvage drop rates prevent soft-locks while maintaining upgrade tension
```

### Body Shop Economy Test

```
// Test: BodyShopPricingTest
// Verify:
//   - Common limb price affordable by district 1 earnings
//   - Epic limb price requires ~2 districts of savings
//   - Legendary never sold (boss drop only)
//   - Price scales linearly with rarity tier
```
