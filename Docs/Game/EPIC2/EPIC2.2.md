# EPIC 2.2: Body Persistence & Inventory

**Status**: Planning
**Epic**: EPIC 2 — Soul Chip, Death & Revival
**Dependencies**: EPIC 2.1 (SoulChip); EPIC 1.1 (ChassisState); Framework: CorpseLifecycle, Persistence/

---

## Overview

When a player dies, their body stays in the world with full inventory — weapons, limbs, consumables, currency. The body persists across district transitions as a marker in district save state. Players can return to loot their own corpse. Bodies are interactable: approach → UI shows inventory → retrieve items individually or bulk.

---

## Component Definitions

```csharp
// File: Assets/Scripts/SoulChip/Components/DeadBodyComponents.cs
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

namespace Hollowcore.SoulChip
{
    /// <summary>
    /// Full snapshot of a dead player's state. On the dead body entity.
    /// Extends framework's CorpseState (which handles ragdoll/fade/cleanup).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct DeadBodyState : IComponentData
    {
        public int SoulId;            // Which player this was
        public int DistrictId;        // Where the body is
        public int ZoneId;            // Specific zone within district
        public float3 WorldPosition;  // Exact position
        public double DeathTime;      // When death occurred (elapsed time)
        public bool IsLooted;         // Whether gear has been recovered
        public bool IsReanimated;     // Whether district has claimed this body (EPIC 2.4)
    }

    /// <summary>
    /// Inventory snapshot stored on dead body. Buffer of serialized item references.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct DeadBodyInventoryEntry : IBufferElementData
    {
        public int ItemDefinitionId;  // Weapon, consumable, currency, etc.
        public int Quantity;
        public int SlotIndex;         // Original equipment slot (-1 = inventory)
    }

    /// <summary>
    /// Chassis snapshot stored on dead body. One entry per equipped limb at death.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct DeadBodyLimbEntry : IBufferElementData
    {
        public ChassisSlot Slot;
        public int LimbDefinitionId;
        public float IntegrityAtDeath;
    }

    /// <summary>
    /// Marker for Scar Map (EPIC 12). Written to district save state.
    /// Lightweight — just enough for the map UI to show skull + hover preview.
    /// </summary>
    public struct DeadBodyMarker
    {
        public int SoulId;
        public int DistrictId;
        public int ZoneId;
        public float3 Position;
        public double DeathTime;
        public bool IsLooted;
        public bool IsReanimated;
        public int WeaponCount;     // For hover preview
        public int LimbCount;       // For hover preview
        public int CurrencyAmount;  // For hover preview
    }
}
```

---

## Systems

### DeadBodyCreationSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/DeadBodyCreationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: SoulChipEjectionSystem
//
// On player death (after soul chip ejects):
//   1. The existing corpse entity from framework's DeathTransitionSystem becomes the dead body
//   2. Add DeadBodyState: populate SoulId, DistrictId, ZoneId, WorldPosition, DeathTime
//   3. Snapshot inventory → DeadBodyInventoryEntry buffer:
//      - All equipped weapons (from EquippedItemElement buffer)
//      - All inventory items
//      - Currency amounts
//   4. Snapshot chassis → DeadBodyLimbEntry buffer:
//      - For each occupied ChassisState slot: record LimbDefinitionId + integrity
//   5. Write DeadBodyMarker to district persistence (for Scar Map)
//   6. Do NOT apply CorpseLifecycle cleanup timers — dead player bodies persist indefinitely
//      (Override CorpseConfig for player bodies: CorpseLifetime = float.MaxValue)
```

### DeadBodyInteractionSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/DeadBodyInteractionSystem.cs
// Managed system in PresentationSystemGroup (client-side UI interaction).
//
// When player approaches a dead body (own or teammate's):
//   1. Show interaction prompt: "Search Body"
//   2. On interact: open body inventory UI
//      - Shows: weapons, limbs, consumables, currency
//      - Player can take individual items or "Take All"
//      - Taking a limb: creates LimbPickup → goes through EPIC 1.3 equip flow
//      - Taking a weapon: goes through existing Items/ equip flow
//      - Taking currency: adds to player wallet
//   3. When all items taken: set DeadBodyState.IsLooted = true
//   4. Looted bodies remain in world (for reanimation or visual reference)
```

### DeadBodyPersistenceSystem

```csharp
// File: Assets/Scripts/SoulChip/Persistence/DeadBodySaveModule.cs
// ISaveModule TypeId: 21
//
// Serializes all dead body markers for the expedition.
// On district exit: save all DeadBodyMarkers in current district
// On district re-entry: restore DeadBody entities from markers
//   - If body was in this district: recreate full entity with inventory
//   - Inventory stored as DeadBodyInventoryEntry + DeadBodyLimbEntry
//
// Cross-district bodies: stored as lightweight markers only
//   - Full entity recreated when player re-enters the district
```

---

## Setup Guide

1. Modify CorpseLifecycle config: player bodies get `CorpseLifetime = float.MaxValue` (never auto-cleanup)
2. Add `DeadBodyState`, `DeadBodyInventoryEntry` buffer, `DeadBodyLimbEntry` buffer to dead body entity
3. Register DeadBodySaveModule (TypeId=21) with SaveModuleRegistry
4. Create body interaction UI: inventory grid showing weapons, limbs, consumables, currency
5. Create body search animation: player kneeling/rummaging
6. Scar Map bridge: DeadBodyMarkers feed skull icons on map (EPIC 12)

---

## Verification

- [ ] Player death creates dead body with full inventory snapshot
- [ ] Body visible at death location after respawn
- [ ] Interacting with body shows inventory preview
- [ ] Can retrieve individual items from body
- [ ] Can "Take All" to bulk recover
- [ ] Body persists across district transitions (marker in save data)
- [ ] Re-entering district with dead body: body entity recreated with inventory
- [ ] Looted body stays in world but marked IsLooted
- [ ] Scar Map shows skull icon at body location
- [ ] Co-op: any teammate can loot any player's body

---

## Validation

```csharp
// File: Assets/Editor/Validation/DeadBodyValidation.cs
// Build-time validation scanning all dead body save data:
//
// Rules:
// - DeadBodyState.SoulId must be > 0
// - DeadBodyState.DistrictId must match a valid district in ExpeditionGraphState
// - DeadBodyState.ZoneId must exist within the district's zone list
// - DeadBodyInventoryEntry.ItemDefinitionId must reference a valid item definition
// - DeadBodyInventoryEntry.Quantity must be > 0
// - DeadBodyLimbEntry.Slot must be a valid ChassisSlot enum value
// - DeadBodyLimbEntry.IntegrityAtDeath must be in [0.0, 1.0]
// - DeadBodyMarker must mirror DeadBodyState fields (SoulId, DistrictId, ZoneId, Position)
// - ISaveModule TypeId 21 must not conflict with other registered modules
```

---

## Debug Visualization

**Dead Body Overlay** (toggle via debug menu):
- In-world gizmo: skull icon at each DeadBodyState.WorldPosition
- Color coding: white = lootable, grey = looted, red = reanimating, orange = reanimated
- Hover info (editor scene view): inventory item count, limb count, currency total
- Scar Map debug layer: all DeadBodyMarkers rendered regardless of district presence

**Activation**: Debug menu toggle `Death/Bodies/ShowAllBodies`
