# Setup Guide 14.5: Universal Equipment System

This guide explains how to set up and configure the equipment system using the new ScriptableObject workflow.

## Overview

The EPIC 14.5 equipment system is fully **data-driven**. All input bindings are configured via ScriptableObject assets, not code. The system flow is:

```
Player Input → DIGEquipmentProvider → PlayerInputState → PlayerInputSystem → ECS
```

Key concepts:
- **Slot Definitions** (`EquipmentSlotDefinition.asset`) define equipment slots and their input bindings
- **Weapon Prefabs** contain all item configuration (no external arrays to sync)
- **Numeric keys 1-9** are the standard equip mechanism
- **Modifier keys** (Alt, Shift, Ctrl) differentiate slots

> **Note:** Legacy weapon switching methods have been removed:
> - ❌ Scroll wheel cycling
> - ❌ Q for "switch to last weapon"  
> - ❌ H for "holster"

---

## 1. Create Definitions (Data Field)

First, define the building blocks of your equipment system.

| Menu Path | Creates | Description |
|-----------|---------|-------------|
| Create → DIG → Equipment → **Slot Definition** | `New Slot.asset` | Defines an equipment slot (e.g. MainHand, Backpack). |
| Create → DIG → Equipment → **Weapon Category** | `New Category.asset` | Defines a weapon type (e.g. Sword, Gun). |
| Create → DIG → Equipment → **Input Profile** | `New Profile.asset` | Defines specific inputs for a weapon type. |

### Recommended Setup
1. Create a folder `Assets/Content/Equipment/Definitions/`.
2. Create standard slots: `MainHand` and `OffHand`.
3. Create standard categories: `Melee`, `Gun`, `Shield`.

---

## 2. Configure the Character (The Container)

You must tell the player character which slots exist.

1. Select your **Player Prefab** (or character object).
2. Locate the **DIG Equipment Provider** component.
3. Expand **Slot Definitions**.
4. Drag your `Slot Definition` assets into the list.
   - Element 0: `MainHand`
   - Element 1: `OffHand`

> [!NOTE]
> The order here determines the internal "Slot ID". Usually 0 is Main Hand.

---

## 3. Configure Equipment Slots (Bindings)

Configure how players interact with each slot by selecting the `Slot Definition` asset itself.

1. Select `MainHand.asset` in the Project view.
2. **Settings**:
   - **Uses Numeric Keys**: Check this (Enable 1-9 keys).
   - **Required Modifier**: None.
3. Select `OffHand.asset` in the Project view.
4. **Settings**:
   - **Uses Numeric Keys**: Check this.
   - **Required Modifier**: **Alt** (or Ctrl/Shift).

> Now, pressing `1` equips to Main Hand. Pressing `Alt + 1` equips to Off Hand.

---

## 4. Configure Weapons (The Content)

Items act as information containers. Configure them on the **Prefab**.

1. Open your Weapon Prefab.
2. Locate **Item Animation Config Authoring** component.
3. **Weapon Category**: Drag your `Weapon Category` asset (e.g., `Sword.asset`) here.
   - This tells the system "This is a Sword" and applies Sword behaviors (Input Profile, Animations).
4. Locate **Item Identity** component.
5. **Default Quick Slot**: Set the number key (1-9) this item uses by default.

---

## 5. Add to Inventory (Gameplay)

Give the item to the player.

1. Select the **Player Prefab** (or SubScene entity).
2. Locate **Starting Inventory Authoring**.
3. **Weapons**: Add your configured Weapon Prefab to this list.

---

## 6. Verifying Setup

### In Editor
Use the Debugger to verify the system sees your configuration.

1. Open **DIG → Equipment → System Debugger**.
2. Enter **Play Mode**.
3. Select the `DIGEquipmentProvider` (Main Provider).
4. Verify:
   - **Slot States**: Shows your defined slots (Main Hand, Off Hand).
   - **Inventory**: Shows the items you added to Starting Inventory.
5. **Force Equip Config**: Click the numeric buttons to test equating items directly.

### Troubleshooting

**Clicking '1' does nothing:**
- Check `MainHand.asset`: Is "Uses Numeric Keys" checked?
- Check `MainHand.asset`: Is "Required Modifier" set to **None**?
- Check Weapon Prefab: Does `ItemIdentity` have a Default Quick Slot set?
- Check `StartingInventoryAuthoring`: Is the weapon in the list?
- Verify `DIGEquipmentProvider` has slot definitions assigned in the inspector.

**Alt+2 doesn't equip off-hand:**
- Check `OffHand.asset`: Is "Uses Numeric Keys" checked?
- Check `OffHand.asset`: Is "Required Modifier" set to **Alt**?
- Ensure weapon prefab has `ItemIdentity.DefaultQuickSlot = 2`.
- Verify the off-hand slot definition is in `DIGEquipmentProvider.SlotDefinitions` array.

**Character doesn't animate:**
- Check `Weapon Category`: Does it have a valid `Grip Type`?
- Check Animator: Do you have a bridge (e.g., `MecanimAnimatorBridge`) attached to the player?

---

## 7. Technical Architecture

### Input Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  DIGEquipmentProvider.HandleNumericEquip() (MonoBehaviour)      │
│    └─ Reads EquipmentSlotDefinition.RequiredModifier            │
│    └─ Calls RequestEquip(slotId, quickSlot)                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  PlayerInputState (Static Class)                                 │
│    ├─ PendingEquipSlot: int (-1=none, 0=MainHand, 1=OffHand)    │
│    └─ PendingEquipQuickSlot: int (1-9)                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  PlayerInputSystem.OnUpdate() (ECS)                              │
│    └─ Reads PlayerInputState, writes to PlayerInput component   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  ItemSwitchInputSystem → ItemSetSwitchSystem → ItemEquipSystem  │
│    └─ Processes EquipRequest and manages equip state machine    │
└─────────────────────────────────────────────────────────────────┘
```

### Key Files

| File | Purpose |
|------|---------|
| `EquipmentSlotDefinition.cs` | ScriptableObject defining slot input bindings |
| `DIGEquipmentProvider.cs` | MonoBehaviour that reads slot definitions and handles input |
| `PlayerInputState.cs` | Static class bridging MonoBehaviour → ECS |
| `PlayerInputSystem.cs` | ECS system that populates `PlayerInput` component |
| `ItemSetSwitchSystem.cs` | Processes `ItemSwitchRequest` and populates `EquipRequest` |