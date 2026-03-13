# EPIC 2.2 Setup Guide: Body Persistence & Inventory

**Status:** Planned
**Requires:** EPIC 2.1 (SoulChipState, SoulChipEjectionSystem), EPIC 1.1 (ChassisState, LimbInstance), Framework CorpseLifecycle (DeathTransitionSystem, CorpseState), Framework Persistence/ (ISaveModule pipeline), Framework Items/ (inventory system)

---

## Overview

When a player dies, their body persists in the world with full inventory -- weapons, limbs, consumables, and currency. The body remains as an interactable entity across district transitions, stored in district save state. Players or teammates can return to loot the corpse, retrieving items individually or in bulk. Bodies also feed into the Scar Map (EPIC 12) as skull markers and may be claimed by district reanimation (EPIC 2.4). This system turns death into a spatial logistics problem rather than a simple respawn.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Player Prefab (Subscene) | `SoulChipAuthoring` (EPIC 2.1) | SoulId for body identification |
| Player Prefab (Subscene) | `ChassisAuthoring` (EPIC 1.1) | Chassis state snapshot at death |
| Framework | `DeathTransitionSystem` | Detects player death |
| Framework | `CorpseState` / `CorpseLifecycleSystem` | Base corpse handling |
| Framework | Items/ inventory system | Source of inventory data to snapshot |
| Framework | Persistence/ (ISaveModule) | Saving body markers across districts |

### New Setup Required

1. Register `DeadBodySaveModule` (TypeId=21) with SaveModuleRegistry
2. Create the body interaction UI prefab (inventory grid)
3. Configure CorpseLifecycle override for player bodies (infinite lifetime)
4. Create body search animation clip
5. Hook DeadBodyMarker data to Scar Map (EPIC 12)

---

## 1. DeadBodySaveModule Registration

**File:** `Assets/Scripts/SoulChip/Persistence/DeadBodySaveModule.cs`
**ISaveModule TypeId:** 21

### 1.1 Registration

Add to SaveModuleRegistry initialization:

| Field | Value | Notes |
|-------|-------|-------|
| **TypeId** | 21 | Must not conflict with existing modules (1-20 used by framework) |
| **ModuleClass** | `DeadBodySaveModule` | Implements ISaveModule |
| **SavePriority** | 5 (medium) | Saves after player state but before district cleanup |

### 1.2 Serialized Data Per Body

| Data | Source | Size Estimate |
|------|--------|---------------|
| DeadBodyState | SoulId, DistrictId, ZoneId, WorldPosition, DeathTime, IsLooted, IsReanimated | 40 bytes |
| DeadBodyInventoryEntry[] | Weapons, consumables, currency from player inventory | 12 bytes x item count |
| DeadBodyLimbEntry[] | ChassisState slots at death | 12 bytes x 6 (max) |
| DeadBodyMarker | Lightweight copy for Scar Map display | 48 bytes |

**Tuning tip:** DeadBodyInventoryEntry uses InternalBufferCapacity(16), which covers most inventories. If a player can carry more than 16 distinct item stacks, increase the capacity or switch to external buffer (InternalBufferCapacity(0)).

---

## 2. Corpse Lifecycle Override for Player Bodies

**Edit:** CorpseConfig singleton or per-entity override

Player dead bodies must NOT auto-cleanup like enemy corpses. Configure:

| Setting | Enemy Default | Player Override | Notes |
|---------|--------------|-----------------|-------|
| **CorpseLifetime** | 15s | `float.MaxValue` | Player bodies persist indefinitely |
| **FadeOutDuration** | 1.5s | N/A (no fade) | Player bodies do not dissolve |
| **MaxCorpses cap** | 30 | Excluded | Player bodies exempt from cap |

### 2.1 Implementation

`DeadBodyCreationSystem` adds a `CorpseSettingsOverride` component to the dead body entity:

| Field | Value |
|-------|-------|
| **CorpseLifetime** | -1 (infinite) |
| **FadeOutDuration** | -1 (no fade) |

This tells `CorpseLifecycleSystem` to skip cleanup for this entity.

---

## 3. Body Interaction UI

**Create:** UI prefab in `Assets/Prefabs/UI/SoulChip/BodyInventoryPanel.prefab`

### 3.1 Panel Layout

| Section | Description |
|---------|-------------|
| **Header** | "Search Body" title + player name (from SoulId lookup) |
| **Weapon Grid** | Equipped weapons with icon + name + stats |
| **Limb Grid** | Equipped limbs with slot indicator + icon + integrity bar |
| **Consumable List** | Consumable items with quantity |
| **Currency Display** | Total currency on the body |
| **Action Buttons** | "Take" per-item, "Take All" bulk action, "Close" |

### 3.2 Interaction Flow

1. Player approaches dead body (within InteractableAuthoring range)
2. Prompt appears: "Search Body [Interact]"
3. On interact: `BodyInventoryPanel` opens
4. Player selects items individually or "Take All"
5. Taking a limb: creates `LimbPickup` -> standard EPIC 1.3 equip flow
6. Taking a weapon: standard Items/ equip flow
7. Taking currency: adds to player wallet directly
8. When all items taken: `DeadBodyState.IsLooted = true`

### 3.3 Body Search Animation

| Animation | Duration | Layer | Notes |
|-----------|----------|-------|-------|
| `Search_Body_Start` | 0.8s | FullBody | Player kneels down |
| `Search_Body_Loop` | Looping | FullBody | Rummaging motion (plays while UI is open) |
| `Search_Body_End` | 0.5s | FullBody | Player stands up |

---

## 4. Dead Body Entity Setup

`DeadBodyCreationSystem` converts the existing corpse entity (from framework's `DeathTransitionSystem`) into a dead body. No new prefab needed -- components are added to the existing entity.

### 4.1 Components Added on Death

| Component | Source | Notes |
|-----------|--------|-------|
| `DeadBodyState` | Populated from player data | SoulId, DistrictId, ZoneId, WorldPosition, DeathTime |
| `DeadBodyInventoryEntry` buffer | Snapshot of player inventory | All equipped + stored items |
| `DeadBodyLimbEntry` buffer | Snapshot of ChassisState | Each occupied slot's LimbDefinitionId + integrity |
| `InteractableAuthoring` (runtime) | Added for body search | Type = BodySearch, Range = 2.5 |
| `CorpseSettingsOverride` | Infinite lifetime | Prevents auto-cleanup |

### 4.2 Inventory Snapshot Fields

| Field | Description | Source |
|-------|-------------|--------|
| **ItemDefinitionId** | ID of the weapon/consumable/currency | Items/ system |
| **Quantity** | Stack size | Items/ system |
| **SlotIndex** | Original equipment slot (-1 = general inventory) | Items/ system |

### 4.3 Chassis Snapshot Fields

| Field | Description | Source |
|-------|-------------|--------|
| **Slot** | ChassisSlot enum value | ChassisState |
| **LimbDefinitionId** | ID of the limb equipped in this slot | LimbInstance buffer |
| **IntegrityAtDeath** | Integrity percentage at time of death | LimbInstance buffer |

---

## 5. Scar Map Integration

**Data flow:** `DeadBodyCreationSystem` writes `DeadBodyMarker` to district persistence.

### 5.1 DeadBodyMarker for Map Display

| Field | Description | Map Use |
|-------|-------------|---------|
| **SoulId** | Which player died | Name label on skull icon |
| **DistrictId** | Which district | Filter by current/visited districts |
| **ZoneId** | Which zone | Position on zone map |
| **Position** | World coordinates | Skull icon placement |
| **DeathTime** | When death occurred | "Died X minutes ago" tooltip |
| **IsLooted** | Whether gear recovered | Grey skull if looted |
| **IsReanimated** | Whether district claimed body | Red skull if reanimated |
| **WeaponCount** | Items on body | Hover preview |
| **LimbCount** | Limbs on body | Hover preview |
| **CurrencyAmount** | Currency on body | Hover preview |

### 5.2 Skull Icon States

| State | Color | Tooltip |
|-------|-------|---------|
| Lootable | White | "Your body -- X weapons, Y limbs, Z currency" |
| Looted | Grey | "Looted body" |
| Reanimating | Orange | "Body being claimed by [District]..." |
| Reanimated | Red | "Reanimated -- [EnemyName] guards your gear" |

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Player Subscene | `SoulChipAuthoring` (already from 2.1) | Provides SoulId for body tagging |
| Global Config Subscene | `SaveModuleRegistryAuthoring` updated with TypeId 21 | Register DeadBodySaveModule |
| UI Canvas | `BodyInventoryPanel` prefab | Body search interaction UI |
| Scar Map UI | DeadBodyMarker data binding | EPIC 12 skull icon rendering |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| ISaveModule TypeId 21 conflicts with another module | Save/load crashes or corrupts data | Check SaveModuleRegistry for TypeId conflicts |
| Player body auto-cleaned up by CorpseLifecycleSystem | Body disappears after 15 seconds | Verify CorpseSettingsOverride is added with infinite lifetime |
| DeadBodyInventoryEntry buffer too small (InternalBufferCapacity=16) | Inventory items beyond 16 lost on death | Increase capacity or verify max inventory size fits |
| Body interaction range too small | Player cannot interact with body in cluttered areas | Increase InteractableAuthoring range to 2.5-3.0m |
| Taking a limb does not go through equip flow | Limb goes directly to inventory instead of chassis | Ensure limb retrieval creates LimbPickup entity (EPIC 1.3 flow) |
| DeadBodyMarker not written on death | Scar Map shows no skull icons | Verify DeadBodyCreationSystem calls persistence write |
| Co-op: body only interactable by owner | Teammates cannot loot body | DeadBodyInteractionSystem must allow any player to interact |
| District re-entry does not restore body entity | Body marker exists but no entity to interact with | Verify DeadBodyPersistenceSystem recreates entity from marker data |

---

## Verification

1. **Death Creates Body** -- Kill the player. Console:
   ```
   [DeadBodyCreationSystem] Created dead body for SoulId=12345 at (X, Y, Z) with 3 weapons, 5 limbs, 500 currency
   ```

2. **Body Persists** -- After respawn, return to death location. Body entity should still be there.

3. **Interaction Prompt** -- Walk near the dead body. "Search Body" prompt should appear.

4. **Inventory UI** -- Interact with body. Panel should show all weapons, limbs, consumables, currency that were equipped/carried at death.

5. **Individual Retrieval** -- Take one weapon from the body. It should enter your inventory via Items/ system. Body panel should update to show item removed.

6. **Take All** -- Press "Take All". All remaining items should transfer. DeadBodyState.IsLooted should become true.

7. **Looted Body** -- Re-approach the looted body. It should still be visible but interaction shows "Body (Looted)" with empty inventory.

8. **District Persistence** -- Exit the district and re-enter. Body entity should be recreated from save data at the same position with remaining inventory (if not fully looted).

9. **Scar Map** -- Open the map (EPIC 12). Skull icon should appear at the death location with correct color state (white=lootable, grey=looted).

10. **Co-op Looting** -- In multiplayer, have another player approach and loot the body. Any teammate should be able to interact.

11. **Entity Debugger** -- Find dead body entity. Verify `DeadBodyState` fields, `DeadBodyInventoryEntry` buffer contents, and `DeadBodyLimbEntry` buffer contents match expected values.
