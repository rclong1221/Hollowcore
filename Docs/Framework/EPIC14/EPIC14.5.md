# EPIC14.5 - Universal Equipment System Architecture

**Status:** Implemented (Phases 1-8)
**Dependencies:** EPIC14.4 (Content expansion reveals pain points to address)
**Goal:** Refactor the equipment system to be fully data-driven, extensible, and adaptable to any game genre without code modifications.

---

## Overview

EPIC 14.5 transforms the equipment system from a "good action-game system" into a **universal, asset-store-quality architecture**. After this refactor, the system will support:

- Any number of equipment slots (2 for shooters, 20+ for RPGs)
- Any weapon category without enum modifications
- Any animation backend (Opsive, Animancer, custom)
- Any input configuration per slot
- Any rendering mode (third-person, first-person, VR)

The core principle: **Every decision point becomes a ScriptableObject or Interface.**

---

## Design Principle: Prefab as Single Source of Truth

> [!IMPORTANT]
> **The weapon prefab is the ONLY place weapon configuration should exist.**

### The Problem (Pre-14.5)
Configuring a weapon currently requires editing multiple disconnected locations:
1. **Weapon Prefab**: `WeaponAuthoring`, `ItemAnimationConfigAuthoring` with IDs, ComboCount, etc.
2. **StartingInventoryAuthoring**: Array where you re-specify QuickSlot and reference the prefab.
3. **DIGEquipmentProvider**: Slot config arrays that must align with inventory indices.

This scattering leads to desync bugs (e.g., "Why is my Shield empty?") when arrays are misaligned.

### The Solution
**All configuration lives on the Weapon Prefab. Period.**

| Data | Where It Lives | Why |
|------|----------------|-----|
| AnimatorItemID | Weapon Prefab (`ItemAnimationConfig`) | The weapon knows its own ID |
| MovementSetID | Weapon Prefab (`ItemAnimationConfig`) | The weapon defines its movement animation |
| GripType | Weapon Prefab (`WeaponCategoryRef`) | The weapon knows if it's one/two-handed |
| ComboCount | Weapon Prefab (`ItemAnimationConfig`) | The weapon knows its combos |
| DefaultQuickSlot | Weapon Prefab (`ItemIdentity`) | The weapon knows its default hotbar slot |
| Slot Compatibility | `WeaponCategoryDefinition` SO | Category defines which slots accept this weapon |

### The New Workflow

**Adding a weapon to inventory:**
1. Drag the weapon prefab into `StartingInventoryAuthoring.Weapons` array.
2. **Done.** The system reads everything else from the prefab.

**No more:**
- Matching QuickSlot indices to array positions
- Remembering AnimatorItemIDs
- Syncing MovementSetIDs
- Managing slot compatibility arrays

### Implementation Summary

Weapon prefab components (baked to ECS):

| Component | Purpose |
|-----------|---------|
| `ItemIdentity` | ID, name, default QuickSlot, icon |
| `WeaponCategoryRef` | Reference to `WeaponCategoryDefinition` SO |
| `ItemAnimationConfig` | AnimatorItemID, MovementSetID, ComboCount, UseDuration |

`StartingInventoryAuthoring` becomes simply:
```csharp
public List<GameObject> Weapons; // Just prefab references
```

The baker reads each prefab's `ItemIdentity.DefaultQuickSlot` to auto-populate the buffer.

---

## Architecture Comparison


### Before (EPIC 14.3)

| Component | Implementation | Limitation |
|-----------|---------------|------------|
| Slot Count | Hardcoded 2 (Main, Off) | Can't add armor, accessories |
| Weapon Types | `AnimationWeaponType` enum | Code change for new types |
| Two-Handed Logic | `IsTwoHanded` boolean | No versatile weapons |
| Animator | Direct Opsive calls | Locked to one animation system |
| Input | Hardcoded in `DIGEquipmentProvider` | Inflexible bindings |
| Rendering | Assumed third-person | No first-person support |

### After (EPIC 14.5)

| Component | Implementation | Flexibility |
|-----------|---------------|-------------|
| Slot Count | Array of `EquipmentSlotDefinition` | Unlimited slots via assets |
| Weapon Types | `WeaponCategoryDefinition` SO | New types via assets only |
| Grip System | `GripType` with slot rules | One-handed, two-handed, versatile |
| Animator | `IAnimatorBridge` interface | Swap animation backends |
| Input | `InputProfileDefinition` SO | Per-category input mapping |
| Rendering | `IViewModeHandler` interface | FPS, TPS, VR, spectator |

---

## Core Components

### 1. EquipmentSlotDefinition (ScriptableObject)

Defines a single equipment slot. The system supports any number of these.

| Field | Type | Description |
|-------|------|-------------|
| SlotID | string | Unique identifier ("MainHand", "Helmet") |
| DisplayName | string | UI-friendly name |
| SlotIndex | int | For legacy compatibility and array indexing |
| AttachmentBone | HumanBodyBones | Where item renders on character |
| FallbackAttachPath | string | Custom bone path if HumanBodyBones insufficient |
| AllowedCategories | List | Which weapon categories can occupy this slot |
| InputAction | InputActionReference | Input to equip/use items in this slot |
| AnimatorParamPrefix | string | Animator parameter prefix ("Slot0", "Armor") |
| RenderMode | enum | AlwaysVisible, OnlyWhenEquipped, Holstered |
| SuppressionRules | List | Rules for hiding this slot based on other slots |
| Priority | int | For resolving conflicts |

**Suppression Rule Structure:**

| Field | Type | Description |
|-------|------|-------------|
| WatchSlotID | string | Which slot to monitor |
| Condition | enum | HasItem, HasTwoHanded, HasCategory, etc. |
| ConditionValue | string | Category name or other value |
| Action | enum | Hide, Disable, Override |

---

### 2. WeaponCategoryDefinition (ScriptableObject)

Replaces the `AnimationWeaponType` enum entirely.

| Field | Type | Description |
|-------|------|-------------|
| CategoryID | string | Unique identifier ("Sword", "AssaultRifle") |
| DisplayName | string | UI-friendly name |
| ParentCategory | WeaponCategoryDefinition | For inheritance (Katana → Sword → Melee) |
| DefaultMovementSetID | int | Animator movement set |
| GripType | enum | OneHanded, TwoHanded, Versatile |
| UseStyle | enum | SingleUse, ComboChain, HoldChannel, Toggle |
| DefaultComboCount | int | Number of attacks in chain |
| CanDualWield | bool | Can be held in both hands |
| AllowedOffHandCategories | List | What can be held with this |
| InputProfile | InputProfileDefinition | Input bindings for this category |
| AnimatorSubstateMachine | string | Name of Animator sub-state machine |
| DefaultUseDuration | float | How long use action takes |
| LockMovementOnUse | bool | Freeze character during use |
| DefaultEquipDuration | float | Equip animation length |
| DefaultUnequipDuration | float | Unequip animation length |
| CustomData | Dictionary | Extensible key-value pairs |

**GripType Enum:**

| Value | Meaning | Off-Hand Behavior |
|-------|---------|-------------------|
| OneHanded | Always uses one hand | Off-hand fully available |
| TwoHanded | Always uses both hands | Off-hand always suppressed |
| Versatile | Can be one or two-handed | Context-dependent suppression |

---

### 3. InputProfileDefinition (ScriptableObject)

Defines input bindings for a weapon category or slot.

| Field | Type | Description |
|-------|------|-------------|
| ProfileID | string | Unique identifier |
| PrimaryAction | InputActionReference | Main use (LMB, trigger) |
| SecondaryAction | InputActionReference | Alt use (RMB, aim) |
| ReloadAction | InputActionReference | Reload/reset |
| SpecialAction | InputActionReference | Unique ability |
| ModifierKey | InputActionReference | Modifier for combos |
| ScrollBehavior | enum | CycleSubstate, CycleWeapon, Zoom |
| HoldBehaviors | List | What happens on hold vs tap |
| CancelAction | InputActionReference | Cancel current action |

**HoldBehavior Structure:**

| Field | Type | Description |
|-------|------|-------------|
| InputAction | InputActionReference | Which input |
| TapAction | enum | What happens on tap |
| HoldAction | enum | What happens on hold |
| HoldDuration | float | How long is "hold" |

---

### 4. IAnimatorBridge (Interface)

Abstracts animation system communication.

| Method | Description |
|--------|-------------|
| SetEquippedItem(slotId, itemData) | Notify animator of equipped item |
| TriggerAction(slotId, actionType, substateIndex) | Start an action animation |
| SetMovementSet(movementSetId) | Change movement animation set |
| GetCurrentState(slotId) | Query current animation state |
| IsAnimationComplete(slotId, actionType) | Check if action finished |
| CancelAction(slotId) | Interrupt current animation |
| SetAimActive(isAiming) | Toggle aim state |
| SetBlocking(isBlocking) | Toggle block state |

**Implementations Required:**

| Implementation | Target | Status |
|----------------|--------|--------|
| OpsiveAnimatorBridge | Current Opsive UCC integration | ✅ Complete - `MecanimAnimatorBridge.cs` |
| GenericMecanimBridge | Standard Unity Animator | ✅ Complete - same file |
| AnimancerBridge | Animancer plugin support | ⬜ Not started |
| FirstPersonArmsBridge | First-person arm rendering | ⬜ Not started |
| TimelineBridge | Cutscene/QTE animations | ⬜ Not started |

> **Cross-Reference:** See EPIC 15.7 Section 7 for detailed analysis of Opsive melee algorithms and how DIG's ECS implementation compares/differs.

---

### 5. IViewModeHandler (Interface)

Abstracts camera-mode-specific rendering.

| Method | Description |
|--------|-------------|
| OnViewModeChanged(mode) | React to camera mode switch |
| RenderEquipment(slotId, itemEntity) | Render item appropriately |
| HideEquipment(slotId) | Stop rendering item |
| GetAttachPoint(slotId) | Get bone/transform for slot |
| SupportsSlot(slotId) | Check if view mode uses this slot |

**View Modes:**

| Mode | Description | Slot Behavior |
|------|-------------|---------------|
| ThirdPerson | Full body visible | All slots render |
| FirstPerson | Only arms visible | MainHand/OffHand only |
| FirstPersonFullBody | Mirrors + body | All slots render |
| VR | Hand-tracked | Per-hand slots |
| Spectator | Free camera | All slots render |
| UI | Inventory screen | None render |

---

### 6. Item Component Architecture

Items become composable entities with optional components.

**Required Components:**

| Component | Description |
|-----------|-------------|
| ItemIdentity | ID, name, icon, description |
| WeaponCategoryRef | Reference to WeaponCategoryDefinition |
| AnimationProfile | ItemID, substates, visual overrides |

**Optional Components:**

| Component | Description | Use Case |
|-----------|-------------|----------|
| WeaponDurability | Current/max durability, degrade rate | Survival games |
| WeaponAmmo | Clip size, reserve, reload time | Shooters |
| WeaponMods | Buffer of attached modifications | Tarkov-style |
| WeaponEnchantment | Magic effects, elemental damage | RPGs |
| WeaponSkin | Visual override mesh/material | Cosmetics |
| WeaponCharges | Limited uses before depletion | Consumables |
| WeaponLevel | XP, level, stat scaling | Progression RPGs |
| WeaponSocket | Gems/runes attached | Diablo-style |

---

## Data Flow

### Equip Process

1. **Input:** Player presses `1` (or `Alt+2` for off-hand)
2. **DIGEquipmentProvider.HandleNumericEquip():** 
   - Reads `EquipmentSlotDefinition` for each configured slot
   - Checks `UsesNumericKeys` and `RequiredModifier` against current input
   - If match found, calls `RequestEquip(slotId, quickSlot)`
3. **PlayerInputState (static class):** Stores pending equip:
   - `PendingEquipSlot` (0=MainHand, 1=OffHand, -1=none)
   - `PendingEquipQuickSlot` (1-9)
4. **PlayerInputSystem (ECS):** Reads from `PlayerInputState`, writes to `PlayerInput`:
   - `PlayerInput.EquipSlotId` = slot target
   - `PlayerInput.EquipQuickSlot` = numeric key pressed
5. **ItemSwitchInputSystem:** Bridges `PlayerInput` to `ItemSwitchRequest`
6. **ItemSetSwitchSystem:** Finds item in buffer by QuickSlot, populates `EquipRequest`
7. **ItemEquipSystem:** Manages unequip → equip state machine
8. **Equipment Changed Event:** Fired to all listeners
9. **IAnimatorBridge:** `SetEquippedItem()` called
10. **IViewModeHandler:** `RenderEquipment()` called

> **Note:** All legacy weapon switching methods have been removed:
> - ❌ Scroll wheel cycling
> - ❌ Q for "switch to last weapon"
> - ❌ H for "holster"
> 
> The system now exclusively uses the data-driven slot definitions for equip input.

### Use Process

1. **Input:** Player presses LMB
2. **InputProfileDefinition:** Maps to "PrimaryAction"
3. **WeaponUseRequest:** ECS component set
4. **WeaponUseSystem:** Reads `WeaponCategoryDefinition.UseStyle`, processes action
5. **IAnimatorBridge:** `TriggerAction()` called
6. **Animation Events:** Fire damage/effects
7. **Combo Logic:** If ComboChain, track combo state
8. **Cooldown:** Enforce `UseDuration`

### ItemSwitchType Enum (Simplified)

The `ItemSwitchType` enum has been simplified to only support data-driven slot switching:

| Value | Description |
|-------|-------------|
| `None` | No switch requested |
| `SwitchToQuickSlot` | Switch main-hand to item on QuickSlot N |
| `OffHandQuickSlot` | Switch off-hand to item on QuickSlot N |

**Removed legacy values:**
- ~~`CycleNext`~~ - Scroll wheel next (removed)
- ~~`CyclePrevious`~~ - Scroll wheel previous (removed)
- ~~`SwitchToLast`~~ - Q key last weapon (removed)
- ~~`Holster`~~ - H key holster (removed)
- ~~`SwitchToSet`~~ - Named set switching (removed)

---

## Editor Tools (Required for Architecture)

These tools are essential for working with the new ScriptableObject-based architecture.

### 1. ScriptableObject Creation Menus

Right-click context menus for all new asset types.

| Menu Path | Creates |
|-----------|---------|
| Create > DIG > Equipment > Slot Definition | `EquipmentSlotDefinition.asset` |
| Create > DIG > Equipment > Weapon Category | `WeaponCategoryDefinition.asset` |
| Create > DIG > Equipment > Input Profile | `InputProfileDefinition.asset` |
| Create > DIG > Equipment > Suppression Rule | `SuppressionRule.asset` |

### 2. Custom Inspectors

Enhanced inspectors for complex ScriptableObjects.

**EquipmentSlotDefinition Inspector:**
- Bone selector with skeleton preview
- Drag-and-drop category assignment
- Visual suppression rule editor (node graph style)
- "Test Suppression" button (simulates rules)
- Validation warnings inline

**WeaponCategoryDefinition Inspector:**
- Parent category dropdown (with inheritance tree view)
- Grip type visual explanation
- Input profile preview
- "Find All Weapons Using This Category" button
- Inheritance chain display

**InputProfileDefinition Inspector:**
- Input Action picker (from Input System assets)
- Hold behavior visualizer (timeline style)
- Conflict detection with other profiles
- "Test Inputs" button (shows key bindings)

### 3. Registry Windows

Global views of all assets of each type.

**Weapon Category Registry (EditorWindow):**

| Column | Description |
|--------|-------------|
| Icon | Category icon |
| Name | Category ID |
| Parent | Inherited from |
| Grip | One/Two/Versatile |
| Weapons | Count of weapons using this |
| Status | Valid / Has Warnings / Has Errors |

Features:
- Search and filter
- Bulk operations (rename, re-parent)
- Orphan detection (categories with no weapons)
- Duplicate ID detection

**Equipment Slot Registry (EditorWindow):**

Similar structure showing all slots, their bones, and suppression rule connections.

**Input Profile Registry (EditorWindow):**

Shows all profiles with conflict matrix (highlights duplicate bindings).

### 4. Asset Validators

Per-asset validation with immediate feedback.

**Inline Validation (in Inspectors):**
- Missing required fields → Red warning box
- Duplicate IDs → Error with link to conflicting asset
- Orphaned references → Warning with cleanup button
- Circular inheritance → Error with chain display

**Validate All (Menu Item):**
- `DIG > Equipment > Validate All Definitions`
- Scans all SOs in project
- Generates report

### 5. Quick Find Tools

**Find Assets Using This:**
- Right-click any SO → "Find References in Equipment System"
- Shows all prefabs, other SOs, and scenes referencing it

**Find Weapons by Category:**
- Search field in Category Inspector
- Lists all weapon prefabs using this category

### 6. Bulk Operations

**Bulk Category Re-Assignment:**
- Select multiple weapon prefabs
- Change category in one operation

**Bulk Slot Migration:**
- When slot definitions change
- Update all prefabs automatically

### 7. Hierarchy Visualizers

**Category Inheritance Tree (EditorWindow):**
```
└── Weapon (root)
    ├── Melee
    │   ├── Sword
    │   │   ├── Katana
    │   │   └── Greatsword
    │   └── Knife
    ├── Ranged
    │   ├── Gun
    │   │   ├── Pistol
    │   │   ├── Rifle
    │   │   └── Shotgun
    │   └── Bow
    └── Magic
        ├── Staff
        └── Wand
```

Click to select, drag to re-parent.

**Slot Suppression Graph (EditorWindow):**
- Nodes = Slots
- Edges = Suppression rules
- Colors = Active state simulation
- Interactive: drag to add rules

---

## Editor Tools Convention

### Menu Structure

| Tool Type | Menu Location | Purpose |
|-----------|---------------|---------|
| **Universal** | `DIG/<Category>/` | Permanent tools for normal workflows |
| **Setup** | `DIG/Setup/` | One-time bootstrap/initialization tools |
| **Migration** | `DIG/Migration/<Version>/` | One-time data migration tools |

### File Organization

| Tool Type | Folder | Naming |
|-----------|--------|--------|
| Universal | `Editor/` | Descriptive name |
| Setup | `Editor/Setup/` | `Setup_<Name>.cs` |
| Migration | `Editor/Migration/` | `Migration_<Version>_<Name>.cs` |

### One-Time Tool Header

```csharp
/// <summary>
/// [SETUP] or [MIGRATION] Tool - EPIC XX.X
/// Purpose: [Description]
/// Safe to remove: [Condition when no longer needed]
/// </summary>
```

---

## Tasks

### Phase 1: ScriptableObject Definitions ✅
- [x] Create `EquipmentSlotDefinition` ScriptableObject class
- [x] Create `WeaponCategoryDefinition` ScriptableObject class
- [x] Create `InputProfileDefinition` ScriptableObject class
- [x] Create `SuppressionRule` data structure
- [x] Create `GripType` enum
- [x] Create asset creation menus for all SO types
- [x] Create default assets for current weapon types (via `DIG/Setup/Equipment Defaults`)

### Phase 2: Interface Abstractions ✅
- [x] Define `IAnimatorBridge` interface
- [x] Create `OpsiveAnimatorBridge` implementation
- [x] Define `IViewModeHandler` interface
- [x] Create `ThirdPersonViewHandler` implementation
- [x] Refactor `WeaponEquipVisualBridge` to use interfaces (deferred / partially complete)

### Phase 3: Slot System Refactor ✅
- [x] Merge `EquipmentSlotConfig` features into `EquipmentSlotDefinition`
- [x] Refactor `DIGEquipmentProvider` to use `EquipmentSlotDefinition` array
- [x] Add input binding and modifier key support to `EquipmentSlotDefinition`
- [x] Deprecate `EquipmentSlotConfig` with `[Obsolete]` attribute
- [x] Implement suppression rule evaluation (Phase 5)

### Phase 4: Category System Refactor ✅
- [x] Deprecate `AnimationWeaponType` enum with `[Obsolete]`
- [x] Add `WeaponCategoryDefinition WeaponCategory` field to `ItemInfo`
- [x] Update `ItemAnimationConfigAuthoring` to support Category Asset
- [ ] Update ECS references to use `WeaponCategoryDefinition` (Gradual Migration)
- [ ] Implement category inheritance lookup in runtime code (Deferred to ECS Refactor)

### Phase 5: Input System Refactor (Equipping) ✅
- [x] Refactor `DIGEquipmentProvider` to use `EquipmentSlotDefinition` for inputs
- [x] Implement modifier key support (`RequiredModifier`)
- [x] Implement numeric key range support (`UsesNumericKeys`)
- [x] Update Debugger Window for dynamic slot support

### Phase 6: Migration Tooling ✅
- [x] Add `WeaponCategoryDefinition` to `ItemAnimationConfigAuthoring`
- [x] Create custom editor with "Migrate" button for Authoring components
- [x] Partial: Debugger Window "Force Equip" supports slots 0-1
- [ ] Create bulk migration script (Optional)
- [x] Update documentation

### Phase 7: Editor Tool Refactoring ✅
- [x] Move `EquipmentDefinitionCreator.cs` to `Editor/Setup/` (renamed to `Setup_EquipmentDefaults`)
- [x] Rename menu from `DIG/Equipment/Create Default Assets/` to `DIG/Setup/Equipment Defaults/`
- [x] Add `[SETUP]`/`[MIGRATION]`/`[UNIVERSAL]` header comments to tools
- [x] Create `Editor/Setup/` and `Editor/Migration/` folder structure
- [x] Add namespaces to `TransitionInspector.cs` and `AnimatorDumper.cs`
- [x] Update all menu paths to follow convention

### Phase 8: ECS Data Layout Refactor (Buffer Migration) ✅
- [x] Create `EquippedItemElement` Buffer
- [x] Refactor `ItemEquipSystem` to use Dynamic Buffers instead of ActiveEquipmentSlot struct
- [x] Refactor dependent systems (`PlayerToItemInputSystem`, etc.)
- [x] Update `DIGEquipmentProvider` to read from Buffers
- [x] Update `EquipmentSystemDebuggerWindow` to verify dynamic slots
- [x] Remove `ActiveEquipmentSlot` struct (Breaking Change)

### Phase 9: Legacy Code Removal ✅
- [x] Remove scroll wheel weapon cycling (`ScrollDelta` from `WeaponSwitchInput`)
- [x] Remove Q key "switch to last weapon" (`SwitchToLastPressed`, `SwitchToLast` enum)
- [x] Remove H key "holster" (`HolsterPressed`, `Holster` enum)
- [x] Remove `CycleNext`, `CyclePrevious`, `SwitchToSet` from `ItemSwitchType` enum
- [x] Remove `TargetSetName`, `CycleDirection` from `ItemSwitchRequest`
- [x] Remove `FindCycledItem()`, `FindDefaultInSet()` from `ItemSetSwitchSystem`
- [x] Remove `ToolSlotDelta` from `PlayerInput` (legacy keyboard fallback)
- [x] Disable `ToolSwitchingSystem` (survival tools - needs future EPIC14.5 integration)
- [x] Implement `PlayerInputState` static class for MonoBehaviour → ECS communication
- [x] Update `DIGEquipmentProvider.RequestEquip()` to use `PlayerInputState`
- [x] Update `PlayerInputSystem` to read from `PlayerInputState` only

---

## Verification Checklist

### Slot System
- [ ] Can define 10+ slots via ScriptableObjects
- [ ] Slots render on correct bones
- [ ] Suppression rules work correctly
- [ ] Dynamic slot count reflected in debugger

### Category System
- [ ] New weapon category addable without code
- [ ] Category inheritance works
- [ ] Grip type correctly controls off-hand

### Interfaces
- [ ] Can swap Animator bridge at runtime
- [ ] View mode changes work correctly
- [ ] All existing functionality preserved

### Migration
- [ ] All existing weapons work post-migration
- [ ] No data loss during migration
- [ ] Performance unchanged or improved

---

## Success Criteria

- [ ] Zero code changes required to add new weapon category
- [ ] Zero code changes required to add new equipment slot
- [ ] Animator system swappable via single inspector field
- [ ] All EPIC 14.4 content works unmodified
- [ ] System documented for Asset Store quality
- [ ] Migration path functional and tested
