# EPIC 13.23: Inventory & Persistence Parity

## Overview
This epic specifically targets the **Meta-Systems** gap. Our current inventory is a simple resource bag. Opsive features a complex "Item Set" system (slots, holsters, equipping logic) and a robust "Saver" architecture. We also lack a centralized UI manager.

## 1. Advanced Inventory System
**Current Status**: `InventoryItem` struct is just `{ ResourceType, Quantity }`.
**Opsive Standard**:
-   **ItemCollection**: A database of all possible items.
-   **ItemSets**: Rules for what can be equipped (e.g., "Slot 0: Primary Weapon", "Slot 1: Secondary").
-   **Equip/Unequip**: Animations and logic state.
**Gap**: We cannot "equip" a specific rifle, only "have" generic items.

### Implementation Plan
-   **`ItemDefinition` (SO)**: Name, Icon, weight, prefab references.
-   **`EquippableItem` Component**:
    -   `SlotID` (0=Right Hand, 1=Left Hand).
    -   `Category` (Rifle, Pistol, Tool).
-   **`InventoryEx` Component**:
    -   Replaces basic buffer.
    -   Holds `Entity` references to instantiated items (for weapons) AND resource counts.
-   **`EquipmentSystem`**:
    -   Handles `EquipRequest`.
    -   Spawns Item Prefab in correct Bone processing (Hand).
    -   Updates Animation State (set "Rifle" layer).

## 2. Serialization (Save/Load)
**Current Status**: Non-existent.
**Opsive Standard**: `Saver` components serialize state to JSON/Binary.
**Gap**: We cannot save the game state.

### Implementation Plan
-   **`ISaveableComponent` Interface**:
    -   `Write(Writer w)`
    -   `Read(Reader r)`
-   **`SaveSystem`**:
    -   Iterates all entities with `SaveableTag`.
    -   Serializes World State + Player State + Inventory State.
    -   Writes to `Application.persistentDataPath/save.dat`.
-   **`WorldLoader`**:
    -   Deserializes and re-creates entities on Load.

## 3. UI Manager (HUD)
**Current Status**: Scattered `PromptUI` scripts.
**Opsive Standard**: Centralized UI Monitor for Health, Ammo, Crosshair, and Inventory Grid.
**Gap**: No scalable UI architecture.

### Implementation Plan
-   **`HUDManager` Singleton**:
    -   Registry for UI Widgets (HealthBar, AmmoCounter).
-   **`CrosshairMonitor`**:
    -   Dynamic spread visualization based on `ItemAction` accuracy.
-   **`DamageIndicator`**:
    -   Directional blood overlays.

## Roadmap & Priorities

### Phase 1: Equipment Architecture
1.  Define `ItemDefinition` workflow.
2.  Implement `EquipmentSystem` (Visual spawning helper).

### Phase 2: Save System Core
3.  Implement basic Binary Serialization for `LocalTransform` and `Inventory`.

### Phase 3: UI Unification
4.  Create `HUDManager` canvas structure.
5.  Link `Attributes` (from 13.22) to UI Bars.

## Success Criteria
- [ ] Player can "Equip" a specific Rifle found in the world.
- [ ] Visual model appears in hand, and Animator updates to "Rifle" pose.
- [ ] Game state (Position, Health, Inventory) persists after restarting app.
- [ ] UI shows Ammo count for equipped weapon.

## Test Environment
To verify these features, the following test objects should be added to the `TraversalObjectCreator`:

### 13.23.T1: The Armory (Loot & Equip)
- **Goal**: Verify ItemCollection, EquipmentSystem, and Persistence.
- **Setup**:
    - **Weapon Tables**: Pedestals with specific weapons (Rifle, Pistol, Axe).
    - **Ammo Crates**: interactable boxes adding resources to inventory.
    - **Save Point**: Interactive object to trigger "Game Save".
- **Success**: Use Interact -> Item attaches to hand -> Stats update -> Save/Load restores state.
