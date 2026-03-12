# Setup Guide: EPIC 13.6 (Items & Inventory)

## Overview
EPIC 13.6 introduces a modular equipment and item system for weapons, tools, and consumables. This is separate from the resource inventory (`DIG.Survival.Resources`).

## Step 1: Add Equipment to Player Prefab
> [!IMPORTANT]
> **Client/Server Setup:** Add `EquipmentAuthoring` to **BOTH** prefabs.

1. Open **`Warrok_Client`** Prefab → Add `EquipmentAuthoring`.
2. Open **`Warrok_Server`** Prefab → Add `EquipmentAuthoring`.
3. Configure slot counts (default: 2 primary, 1 secondary, 2 tool, 4 consumable).

## Step 2: Create Item Prefabs

1. Create a new prefab for your item (e.g., `Pistol.prefab`).
2. Add `ItemAuthoring` component.
3. Configure:
   - **ItemTypeId:** Unique number (e.g., 1001 for Pistol).
   - **DisplayName:** Human-readable name.
   - **Category:** Weapon, Tool, Consumable, etc.
   - **EquipDuration:** Time to equip (0.5s typical).
   - **UnequipDuration:** Time to unequip (0.3s typical).

## Step 3: Create World Pickups (Optional)

1. Create a pickup prefab.
2. Add `ItemPickupAuthoring` component.
3. Configure:
   - **ItemTypeId:** Must match the item prefab.
   - **Quantity:** 1 for non-stackable.
   - **PickupRadius:** Auto-pickup distance (1.5m default).
   - **RequiresInteraction:** Toggle for manual vs auto pickup.

## Components Reference

| Component | Purpose |
|-----------|---------|
| `CharacterItem` | Runtime item state (owner, slot, equip state) |
| `ItemDefinition` | Static item data (type, category, timings) |
| `ItemSlot` | Buffer of equipment slots on character |
| `EquipRequest` | Request to equip/unequip an item |
| `ActiveEquipmentSlot` | Currently visible/active item |

## Verification

1. Enter Play Mode.
2. Add an item entity to the player (debug ECB or spawn system).
3. Set `EquipRequest.Pending = true` with the item entity.
4. **Verify:** Item transitions through Equipping → Equipped states.
5. **Verify:** Animation timers respect configured durations.
