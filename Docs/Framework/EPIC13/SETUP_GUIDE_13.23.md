# Setup Guide: Epic 13.23 - Inventory & Persistence Parity

## Overview
This guide covers the implementation of the advanced Inventory System (Equipping logic), Save/Load Persistence, and the unified HUD Manager.

## Step 1: Equipment System (Inventory)
1.  **Create Item Definitions**:
    -   Create ScriptableObjects for your weapons/tools (e.g., `RifleItemDef`, `PistolItemDef`).
    -   Assign visual prefabs (the actual gun model to spawn in hand).
2.  **Setup Equip Logic**:
    -   Add `EquipmentSystem` to a new `InventorySystemGroup`.
    -   Ensure `EquipmentSystem` listens for `EquipRequest` components on the player entity.
3.  **Visuals**:
    -   The system must attach the specific item prefab to the correct bone (e.g., `RightHand`).
    -   Sync the `Animator` layer weight (e.g., "Rifle" layer = 1).

## Step 2: Persistence (Save/Load)
1.  **Tag Saveables**:
    -   Add `SaveableTag` to any entity you want to persist (Player, Inventory, World Chests).
2.  **Implement Serialization**:
    -   Ensure components implement the `ISaveableComponent` interface (if using custom serialization) or use the reflection-based serializer.
3.  **Triggering Save**:
    -   Call `SaveSystem.TriggerSave()` via a UI button or Interactable Save Point.
    -   Verify file creation at `Application.persistentDataPath/save.dat`.

## Step 3: HUD Integration
1.  **Canvas Setup**:
    -   Add the `HUDManager` prefab to your scene (under `DIG_UI`).
    -   Link the `HealthBar` widget to the `AttributeData` (Health) index.
2.  **Crosshair**:
    -   Ensure `CrosshairMonitor` is referencing the local player's `WeaponSpreadComponent`.

## Troubleshooting
-   **"Item not appearing in hand"**: Check that the `ItemDefinition` prefab has a `LocalTransform` reset to (0,0,0) and the Bone Ref in `EquipmentSystem` is correct.
-   **"Save file corrupt"**: Ensure all `GhostComponent` types are registered if using NetCode serialization rules.
