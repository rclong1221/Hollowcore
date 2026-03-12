# EPIC 16.6: Loot & Drop System Framework

**Status:** **IMPLEMENTED**
**Priority:** High (Core Gameplay Loop)
**Dependencies:**
- `ItemDefinition` / `CharacterItem` / `ItemState` (existing — `DIG.Items`)
- `ItemPickupSystem` (existing — stubbed, needs completion)
- `InventoryItem` buffer / `ResourceType` (existing — `DIG.Shared`)
- `DeathSpawnElement` buffer (existing — authored, no processing system)
- `CorpseLifecycleSystem` (EPIC 16.3 — death hooks)
- `DeathTransitionSystem` / `DiedEvent` (existing)
- `VoxelLootSystem` / `LootSpawnNetworkSystem` (existing — voxel mining path)
- `CargoUtility` / `ShipCargoItem` (existing — container pattern)
- `IEquipmentProvider` (existing — swappable equipment interface)
- `WeaponCategoryDefinition` / `EquipmentSlotDefinition` (existing — data-driven SOs)
- `LootPhysicsSettings` (existing — scatter physics SO)
- `MMLoot` / `MMLootTable` (existing — Feel framework, reference only)

**Feature:** A performant, data-driven loot framework that provides designers with ScriptableObject-based loot tables, weighted drop pools, enemy death drops, container loot, item rarity/affixes, currency, and a complete pickup pipeline — all ECS-native with Burst-compatible hot paths and network-authoritative drop resolution.

---

## Problem

The current item/loot infrastructure has strong foundations but critical gaps that prevent a complete loot loop:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| Voxel mining → loot spawns (VoxelLootSystem) | No loot table / drop pool system |
| ItemDefinition ECS component (static metadata) | No item database / registry SO |
| ItemPickup component + proximity detection | TryAddToInventory() is a stub |
| InventoryItem buffer (resources, ghost-replicated) | No item rarity / quality on entities |
| EquippedItemElement buffer (equipment) | No item stat block (damage ranges, armor) |
| DeathSpawnElement buffer (authored, unused) | No death drop processing system |
| LootPhysicsSettings SO (scatter physics) | No loot containers / chests |
| CargoUtility helper functions | No currency / economy |
| IEquipmentProvider interface (swappable) | No procedural affix generation |
| WeaponCategoryDefinition SO (data-driven) | No conditional drops (level, difficulty) |
| LootRarity enum (UI labels only) | Rarity not on ECS entities |
| MMLootTable (Feel framework, weighted random) | No game-specific loot table SO |

**The gap:** Designers cannot define "BoxingJoe drops 2-4 Stone + 10% chance Rare Sword" without writing C# code. There is no data-driven pipeline from enemy death → loot table roll → item spawn → player pickup → inventory update.

---

## Integration with Existing Systems

This EPIC is **additive** — it builds on top of existing infrastructure. Nothing is deleted. Here's what happens to every existing piece:

### Keep Unchanged (No Modifications)

| System | File | Why It Stays |
|--------|------|-------------|
| `VoxelLootSystem` | `Voxel/Systems/Interaction/VoxelLootSystem.cs` | Independent voxel mining → loot pipeline. Separate from enemy death drops. Continues working as-is. |
| `LootSpawnNetworkSystem` | `Voxel/Systems/Network/LootSpawnNetworkSystem.cs` | Multiplayer voxel loot replication. Unrelated to death loot. |
| `VoxelMaterialDefinition` | `Voxel/Core/VoxelMaterial.cs` | LootPrefab/DropChance/MinDropCount fields are voxel-only. Not replaced. |
| `LootLifetime` (MonoBehaviour) | `Voxel/Interaction/LootLifetime.cs` | Manages lifetime for **GameObject-based** voxel loot. New ECS `LootLifetimeSystem` handles **entity-based** loot separately. Two parallel systems, no conflict. |
| `LootPhysicsSettings` (SO) | `Voxel/Core/LootPhysicsSettings.cs` | Scatter physics config. **Reused** by new `LootSpawnSystem` for death drop scatter. |
| `LootPhysicsSimulator` | `Voxel/Interaction/LootPhysicsSimulator.cs` | Manual physics for DOTS mode. Unrelated to new pipeline. |
| `CargoUtility` | `Runtime/Ship/Cargo/Components/CargoComponents.cs` | Helper functions (`AddToInventory`, `RemoveFromInventory`). **Reused** by completed `ItemPickupSystem` for resource pickups. |
| `InventoryItem` buffer | `Shared/InventoryComponents.cs` | Resource inventory (ghost-replicated). **Reused** as the target for resource loot pickups. |
| `InventoryCapacity` / `InventoryWeightSystem` | `Shared/InventoryComponents.cs` / `Resources/Systems/` | Weight/encumbrance system. **Reused** for pickup weight checks. |
| `ResourceType` enum | `Shared/ResourceTypes.cs` | Stone/Metal/BioMass/etc. **Reused** in `LootPoolEntry.Resource` field. |
| `ItemCategory` enum | `Items/Components/ItemComponents.cs` | Weapon/Tool/Consumable/etc. **Reused** in `ItemEntrySO.Category` and `AffixDefinitionSO.ValidCategories`. |
| `CharacterItem` / `ItemState` | `Items/Components/ItemComponents.cs` | Item runtime state machine. Untouched — loot system creates items, equip pipeline manages them. |
| `ItemSetEntry` / `EquippedItemElement` | `Items/Components/ItemSetComponents.cs` / `EquipmentSlots.cs` | Equipment buffers. **Reused** as pickup targets for equipment loot. |
| `IEquipmentProvider` | `Items/Interfaces/IEquipmentProvider.cs` | Swappable equipment interface. Untouched — already modular. |
| `WeaponCategoryDefinition` / `EquipmentSlotDefinition` | `Items/Definitions/` | Data-driven weapon/slot SOs. **Reused** by `ItemEntrySO.WeaponCategory` reference. |
| `DeathSpawnAuthoring` | `Player/Authoring/DeathSpawnAuthoring.cs` | Bakes `DeathSpawnElement` buffer. Untouched — new `DeathSpawnProcessingSystem` finally **consumes** what this already authors. |
| `ItemPickupAuthoring` | `Items/Authoring/ItemPickupAuthoring.cs` | Bakes `ItemPickup` component. Untouched. |
| `MMLoot` / `MMLootTable` | `Feel/MMTools/Foundation/MMLoot/` | Feel framework's generic weighted random. **Ignored** — not ECS-compatible, not deterministic, not network-safe. We build our own `LootTableSO` with `Unity.Mathematics.Random`. |
| `DeathTransitionSystem` | `Player/Systems/DeathTransitionSystem.cs` | Fires `DiedEvent`. Untouched — new `DeathLootSystem` reads `DiedEvent` downstream. |
| `CorpseLifecycleSystem` | `Combat/Systems/CorpseLifecycleSystem.cs` | Corpse phases. Untouched — loot spawns at death, corpse lifecycle is independent. |

### Modify (Complete Existing Stubs)

| System | File | What Changes |
|--------|------|-------------|
| `ItemPickupSystem` | `Items/Systems/ItemPickupSystem.cs` | **Complete the stub.** `TryAddToInventory()` currently returns `true` and does nothing. Replace with real logic: resource items → `CargoUtility.AddToInventory()`, equipment → `ItemSetEntry` buffer, stackable → find stack + increment, weight check → reject if overweight. The system structure and proximity detection are kept as-is. |

### Reconcile (Deprecate Old, Create New)

| Item | Old Location | New Location | Migration |
|------|-------------|-------------|-----------|
| `LootRarity` enum | `Widgets/Rendering/LootLabelRenderer.cs` (lines 198-205) | `Items/Components/ItemRarity.cs` | Create new `ItemRarity` enum with same values (Common=0 through Legendary=4) plus `Unique=5`. Update `LootLabelRenderer.GetRarityColor()` to accept `ItemRarity` instead of `LootRarity`. Deprecate `LootRarity` with `[System.Obsolete("Use ItemRarity instead")]`. Both enums have identical values 0-4 so casting is safe during migration. |

### Extend (Add Fields to Existing)

| Component | File | What's Added |
|-----------|------|-------------|
| `ItemDefinition` | `Items/Components/ItemComponents.cs` | Optionally add `ItemRarity Rarity` field (default Common=0, backward compatible). Or keep rarity on `ItemEntrySO` only and bake it separately — either approach works without breaking existing items. |

### Create New (No Overlap)

Everything else in EPIC 16.6 (40 new files) has **no existing equivalent** and is purely additive:
- `ItemDatabaseSO` / `ItemEntrySO` — no item registry exists today
- `LootTableSO` / `LootPoolSO` / `LootTableResolver` — no drop table system exists
- `DeathLootSystem` / `LootSpawnSystem` — no death → loot pipeline exists
- `ItemStatBlock` / `ItemAffix` / `AffixDefinitionSO` — no stat/affix system exists
- `CurrencyInventory` / `CurrencyTransactionSystem` — no currency system exists
- `LootContainerState` / `ContainerInteractionSystem` — no chest/container system exists
- All presentation systems (labels, beams, feedback) — new
- All editor tooling (inspectors, simulator window) — new

### The Two Loot Pipelines (Coexisting)

After EPIC 16.6, two independent loot pipelines will exist side-by-side:

```
PIPELINE A — Voxel Mining (existing, unchanged):
  VoxelDestroyedEvent → VoxelLootSystem → GameObject instantiation
  → LootLifetime (MonoBehaviour) → LootPhysicsSettings scatter
  → LootSpawnNetworkSystem (multiplayer RPC batching)

PIPELINE B — Enemy Death / Containers (new):
  DiedEvent → DeathLootSystem → LootTableResolver → PendingLootSpawn
  → LootSpawnSystem → ECS entity instantiation → ItemPickup component
  → ItemPickupSystem → InventoryItem / ItemSetEntry buffers
  → LootLifetimeSystem (ECS) → ghost replication
```

Pipeline A handles **voxel/mining** loot (GameObjects, local physics).
Pipeline B handles **enemy/container** loot (ECS entities, server-authoritative).
They share `LootPhysicsSettings` for scatter physics config but are otherwise independent.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    DESIGNER TOOLING LAYER                       │
│  ItemDatabaseSO ─── LootTableSO ─── LootPoolSO ─── AffixPoolSO│
│  (ScriptableObject registry, inspector-driven, zero code)       │
└──────────────────────────────┬──────────────────────────────────┘
                               │ Bake / Runtime Lookup
┌──────────────────────────────▼──────────────────────────────────┐
│                    ECS DATA LAYER                                │
│  ItemRegistry singleton ── LootTableRef ── ItemRarity ── Affix  │
│  ItemStatBlock ── CurrencyInventory ── LootContainerState       │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│                  SYSTEM PIPELINE (Server-Authoritative)          │
│                                                                  │
│  DiedEvent ─┬─► DeathLootSystem ──► LootTableResolver            │
│             │                              │                     │
│  Interact ──┼─► ContainerLootSystem ──► LootTableResolver        │
│             │                              │                     │
│  Voxel ─────┘   (existing VoxelLootSystem) ▼                     │
│                                    LootSpawnSystem               │
│                                         │                        │
│                                    ItemPickupSystem (completed)   │
│                                         │                        │
│                                    InventoryUpdateSystem          │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│                  NETWORK LAYER                                   │
│  LootDropRpc ── PickupRequestRpc ── PickupResultRpc              │
│  (Server rolls, clients see results, server validates pickup)    │
└─────────────────────────────────────────────────────────────────┘
```

### Data Flow (Single Enemy Death → Loot Pickup)

```
Frame N (Server):
  1. SimpleDamageApplySystem: Health <= 0
  2. DeathTransitionSystem: DiedEvent fires, CorpseState enabled
  3. DeathLootSystem: Reads DiedEvent + LootTableRef on dead entity
  4. LootTableResolver: Rolls loot table → List<LootDrop>
  5. LootSpawnSystem: Creates loot entities at corpse position + scatter
  6. LootDropRpc: Broadcasts spawned loot to clients

Frame N+M (Server, player proximity):
  7. ItemPickupSystem: Player within PickupRadius
  8. PickupRequestRpc: Client requests pickup (or auto-pickup fires)
  9. Server validates: inventory space, item still exists, ownership rules
  10. InventoryUpdateSystem: Adds to InventoryItem/ItemSetEntry buffer
  11. PickupResultRpc: Confirms to client, destroys loot entity
```

---

## Phase 0: Item Database & Registry

### 0.1 ItemDatabaseSO (ScriptableObject Registry)

- [ ] Create `ItemDatabaseSO` ScriptableObject — central registry of all items in the game
  - `List<ItemEntrySO>` entries (searchable, filterable in inspector)
  - `Dictionary<int, ItemEntrySO>` lookup table (built on enable, O(1) by ItemTypeId)
  - `GetItem(int itemTypeId)` → `ItemEntrySO`
  - `GetItemsByCategory(ItemCategory)` → `List<ItemEntrySO>`
  - `GetItemsByRarity(ItemRarity)` → `List<ItemEntrySO>`
  - Validation: duplicate ID check, missing prefab check (OnValidate)
  - Custom editor: searchable list with filters, preview panel, bulk operations

**File:** `Assets/Scripts/Items/Definitions/ItemDatabaseSO.cs` (NEW)

### 0.2 ItemEntrySO (Per-Item ScriptableObject)

- [ ] Create `ItemEntrySO` ScriptableObject — one per unique item type
  - `int ItemTypeId` (unique, auto-assigned or manual)
  - `string DisplayName`, `string Description`
  - `Sprite Icon`, `GameObject WorldPrefab`, `GameObject EquipPrefab`
  - `ItemCategory Category` (existing enum: Weapon, Tool, Consumable, Ammo, Equipment)
  - `ItemRarity Rarity` (new enum)
  - `bool IsStackable`, `int MaxStack`
  - `float Weight`
  - `float EquipDuration`
  - `ItemStatBlock BaseStats` (serializable struct, see Phase 4)
  - `AffixPoolSO PossibleAffixes` (optional, for procedural items)
  - `WeaponCategoryDefinition WeaponCategory` (optional, weapon-only)
  - `ResourceType ResourceType` (optional, resource items map to existing enum)
  - `int SellValue`, `int BuyValue` (currency values)
  - Custom editor: stat preview, affix preview, 3D prefab preview

**File:** `Assets/Scripts/Items/Definitions/ItemEntrySO.cs` (NEW)

### 0.3 ItemRarity Enum (ECS-Compatible)

- [ ] Create `ItemRarity` enum — used on both SO and ECS entities
  - `Common = 0` (white)
  - `Uncommon = 1` (green)
  - `Rare = 2` (blue)
  - `Epic = 3` (purple)
  - `Legendary = 4` (orange)
  - `Unique = 5` (gold — quest/boss-specific, non-random)
- [ ] Reconcile with existing `LootRarity` enum in loot label UI — deprecate old enum, migrate references

**File:** `Assets/Scripts/Items/Components/ItemRarity.cs` (NEW)

### 0.4 ItemRegistrySingleton (ECS Runtime)

- [ ] Create `ItemRegistry` managed singleton — baked from ItemDatabaseSO at world creation
  - Holds `NativeHashMap<int, ItemRegistryEntry>` for Burst-compatible lookups
  - `ItemRegistryEntry`: ItemTypeId, Category, Rarity, IsStackable, MaxStack, Weight, BaseStats (blittable subset)
  - Prefab references stored separately in managed `Dictionary<int, ItemEntrySO>` (not Burst-accessible, used for spawn)
  - Created by `ItemRegistryBootstrapSystem` which reads ItemDatabaseSO from Resources or addressable
- [ ] Create `ItemRegistryBootstrapSystem` — creates singleton on world creation

**File:** `Assets/Scripts/Items/Systems/ItemRegistryBootstrapSystem.cs` (NEW)
**File:** `Assets/Scripts/Items/Components/ItemRegistryEntry.cs` (NEW)

---

## Phase 1: Loot Table System

### 1.1 LootTableSO (ScriptableObject)

- [ ] Create `LootTableSO` — designer-facing drop configuration
  - `string TableName` (for debug/logging)
  - `int GuaranteedRolls` (always roll this many times, default 1)
  - `int BonusRolls` (extra rolls with BonusRollChance each)
  - `float BonusRollChance` (0-1, chance per bonus roll)
  - `List<LootPoolSO> Pools` (weighted pool references)
  - `LootTableCondition[] Conditions` (optional: difficulty, level range, etc.)
  - Custom editor: visual pool weights bar, simulated drop preview, expected value calculator

**File:** `Assets/Scripts/Loot/Definitions/LootTableSO.cs` (NEW)

### 1.2 LootPoolSO (ScriptableObject)

- [ ] Create `LootPoolSO` — a weighted collection of possible drops
  - `string PoolName`
  - `List<LootPoolEntry> Entries`
  - `bool AllowDuplicates` (can the same item drop twice from this pool, default true)
  - `float PoolWeight` (relative weight when multiple pools compete, default 1.0)
  - `int MinDrops`, `int MaxDrops` (how many entries to pick per roll, default 1/1)

**File:** `Assets/Scripts/Loot/Definitions/LootPoolSO.cs` (NEW)

### 1.3 LootPoolEntry (Serializable Struct)

- [ ] Create `LootPoolEntry` — a single possible drop within a pool
  - `LootEntryType Type` (Item, Currency, Resource, NestedTable)
  - `ItemEntrySO Item` (if Type=Item)
  - `ResourceType Resource` (if Type=Resource, maps to existing enum)
  - `CurrencyType Currency` (if Type=Currency)
  - `LootTableSO NestedTable` (if Type=NestedTable — recursive tables for rare sub-pools)
  - `int MinQuantity`, `int MaxQuantity` (stack size range)
  - `float Weight` (relative drop weight within pool)
  - `float DropChance` (0-1, absolute chance override — 0 means use weight-based)
  - `ItemRarity MinRarity`, `ItemRarity MaxRarity` (rarity override range — empty means use item default)
  - `LootEntryCondition[] Conditions` (optional per-entry conditions)

**File:** `Assets/Scripts/Loot/Definitions/LootPoolEntry.cs` (NEW)

### 1.4 LootEntryType & Conditions

- [ ] Create `LootEntryType` enum: `Item = 0, Currency = 1, Resource = 2, NestedTable = 3`
- [ ] Create `LootTableCondition` serializable struct:
  - `ConditionType Type` (MinLevel, MaxLevel, MinDifficulty, MaxDifficulty, RequiresFlag, TimeOfDay)
  - `float Value`
  - `string FlagName` (for RequiresFlag)
- [ ] Create `LootEntryCondition` — same structure, applied per-entry

**File:** `Assets/Scripts/Loot/Definitions/LootConditions.cs` (NEW)

### 1.5 LootTableResolver (Pure C# Utility)

- [ ] Create `LootTableResolver` static class — rolls a loot table and returns results
  - `Resolve(LootTableSO table, LootContext context, ref NativeList<LootDrop> results)`
  - `LootContext` struct: `int Level, float DifficultyMultiplier, float LuckModifier, uint RandomSeed`
  - `LootDrop` struct: `int ItemTypeId, int Quantity, ItemRarity Rarity, CurrencyType Currency, ResourceType Resource, LootEntryType Type`
  - Uses `Unity.Mathematics.Random` (deterministic, seed-based) for server-authoritative rolls
  - Weight normalization: `entryWeight / sumOfAllWeights` for each pool
  - Nested table recursion with depth limit (max 4 deep, prevents infinite loops)
  - Condition evaluation: skip entries/pools where conditions fail

**File:** `Assets/Scripts/Loot/Systems/LootTableResolver.cs` (NEW)

---

## Phase 2: Death Loot Pipeline

### 2.1 LootTableRef Component

- [ ] Create `LootTableRef` IComponentData — links an entity to its loot table
  - `int LootTableId` (maps to runtime lookup — baked from LootTableSO)
  - `float DropChanceMultiplier` (global multiplier, default 1.0 — scales all weights)
  - `float QuantityMultiplier` (scales all quantities, default 1.0)
  - `bool HasDropped` (prevents double-drops on multi-frame death)
- [ ] Bake via `LootTableAuthoring` MonoBehaviour on enemy prefabs

**File:** `Assets/Scripts/Loot/Components/LootComponents.cs` (NEW)
**File:** `Assets/Scripts/Loot/Authoring/LootTableAuthoring.cs` (NEW)

### 2.2 LootTableRegistrySingleton

- [ ] Create managed `LootTableRegistry` — maps LootTableId → LootTableSO at runtime
  - Populated by `LootTableRegistryBootstrapSystem` scanning all LootTableSO assets
  - `GetTable(int lootTableId)` → `LootTableSO`
  - O(1) lookup via dictionary

**File:** `Assets/Scripts/Loot/Systems/LootTableRegistryBootstrapSystem.cs` (NEW)

### 2.3 DeathLootSystem

- [ ] Create `DeathLootSystem` — server-authoritative death drop processing
  - `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
  - `[UpdateInGroup(typeof(SimulationSystemGroup))]`
  - `[UpdateAfter(typeof(DeathTransitionSystem))]`
  - Queries entities with `DiedEvent` (enabled) + `LootTableRef`
  - For each: roll LootTableResolver, create `PendingLootSpawn` buffer entries
  - Sets `LootTableRef.HasDropped = true` to prevent re-processing
  - Respects CorpseLifecycle: loot spawns at corpse position, lifetime >= CorpseLifetime

**File:** `Assets/Scripts/Loot/Systems/DeathLootSystem.cs` (NEW)

### 2.4 DeathSpawnProcessingSystem

- [ ] Create `DeathSpawnProcessingSystem` — processes existing DeathSpawnElement buffer
  - `[UpdateAfter(typeof(DeathLootSystem))]`
  - Reads `DeathSpawnElement` buffer on entities with `DiedEvent`
  - Spawns prefabs at corpse position + PositionOffset
  - Applies explosive force if `ApplyExplosiveForce` flag set
  - This completes the existing DeathSpawnElement pipeline that was authored but never consumed

**File:** `Assets/Scripts/Loot/Systems/DeathSpawnProcessingSystem.cs` (NEW)

### 2.5 LootSpawnSystem

- [ ] Create `LootSpawnSystem` — instantiates loot entities from PendingLootSpawn
  - `[UpdateAfter(typeof(DeathLootSystem))]`
  - Reads `PendingLootSpawn` entries, instantiates item prefabs via `ItemRegistrySingleton`
  - Applies scatter physics using existing `LootPhysicsSettings` pattern
  - Adds `LootEntity` tag + `LootLifetimeECS` component (configurable, default 120s)
  - Adds `ItemPickup` component (existing) with resolved ItemTypeId and Quantity
  - Network: creates ghost entities (server-spawned, replicated to clients)
  - Frame cap: max 32 spawns per frame (queue remainder)

**File:** `Assets/Scripts/Loot/Systems/LootSpawnSystem.cs` (NEW)

### 2.6 PendingLootSpawn Buffer

- [ ] Create `PendingLootSpawn` IBufferElementData (transient, on dead entity)
  - `int ItemTypeId`
  - `int Quantity`
  - `ItemRarity Rarity`
  - `LootEntryType Type`
  - `CurrencyType Currency`
  - `ResourceType Resource`
  - `float3 SpawnPosition`
  - Capacity: 8

**File:** `Assets/Scripts/Loot/Components/LootComponents.cs` (append to 2.1 file)

---

## Phase 3: Pickup Pipeline Completion

### 3.1 Complete ItemPickupSystem

- [ ] Replace stubbed `TryAddToInventory()` in existing `ItemPickupSystem`
  - **Resource items** (`ResourceType != None`): Add to `InventoryItem` buffer via `CargoUtility.AddToInventory()`
  - **Equipment items** (`ItemCategory == Weapon/Tool/Equipment`): Add to `ItemSetEntry` buffer if slot available
  - **Stackable items** (`IsStackable`): Find existing stack, increment up to MaxStack, overflow creates new stack
  - **Weight check**: Reject if `InventoryCapacity.CurrentWeight + item.Weight > MaxWeight` (return false, item stays on ground)
  - **Currency items**: Add to `CurrencyInventory` component (Phase 5)
- [ ] Auto-pickup vs interaction-based split (existing `RequiresInteraction` field on `ItemPickup`):
  - Auto-pickup: resources, currency, ammo (triggered by proximity)
  - Interaction: equipment, consumables (requires player input)
- [ ] Pickup feedback: create `PickupEvent` on player entity for UI/sound systems to consume

**File:** `Assets/Scripts/Items/Systems/ItemPickupSystem.cs` (MODIFY — complete stub)

### 3.2 Pickup Network Authority

- [ ] Server validates all pickups — client sends request, server checks:
  - Item entity still exists (not picked up by another player)
  - Player has inventory space
  - Player is within PickupRadius
  - Item ownership rules (personal loot vs FFA)
- [ ] `PickupRequestRpc` — client → server
- [ ] `PickupResultRpc` — server → client (success/fail + reason)
- [ ] On success: server destroys loot entity (ghost despawn propagates)

**File:** `Assets/Scripts/Loot/Network/LootNetworkComponents.cs` (NEW)

### 3.3 Loot Ownership Rules

- [ ] Create `LootOwnership` IComponentData on loot entities
  - `LootOwnershipMode Mode` (FreeForAll, KillerOnly, GroupShared, RoundRobin)
  - `Entity OwnerEntity` (valid for KillerOnly mode)
  - `int GroupId` (for GroupShared)
  - `float OwnershipTimer` (seconds before loot becomes FFA, default 30s)
- [ ] `FreeForAll` (default): anyone can pick up
- [ ] `KillerOnly`: only the killer can pick up for OwnershipTimer seconds, then FFA
- [ ] `GroupShared`: any group member can pick up
- [ ] `RoundRobin`: distributed among group (future, requires party system)

**File:** `Assets/Scripts/Loot/Components/LootComponents.cs` (append)

### 3.4 Loot Lifetime & Cleanup

- [ ] Create `LootLifetimeSystem` — destroys expired loot entities
  - `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
  - `[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]`
  - Queries `LootEntity` + `LootLifetimeECS`, destroys when elapsed > lifetime
  - Configurable per-rarity: Common=60s, Uncommon=90s, Rare+=120s, Legendary=300s

**File:** `Assets/Scripts/Loot/Systems/LootLifetimeSystem.cs` (NEW)

---

## Phase 4: Item Stats & Affixes

### 4.1 ItemStatBlock Component

- [ ] Create `ItemStatBlock` IComponentData — runtime stats on item entities
  - `float BaseDamage` (weapons)
  - `float AttackSpeed` (weapons)
  - `float CritChance` (0-1)
  - `float CritMultiplier` (default 1.5)
  - `float Armor` (defense)
  - `float MaxHealthBonus`
  - `float MovementSpeedBonus` (additive %)
  - `float DamageResistance` (0-1, multiplicative)
  - All fields default 0 (no effect). Systems read additively from equipped items.

**File:** `Assets/Scripts/Items/Components/ItemStatBlock.cs` (NEW)

### 4.2 ItemAffix System

- [ ] Create `ItemAffix` IBufferElementData on item entities (max capacity 4)
  - `int AffixId` (maps to AffixDefinitionSO)
  - `AffixSlot Slot` (Prefix, Suffix, Implicit)
  - `float Value` (rolled value within range)
  - `int Tier` (affix tier for scaling)
- [ ] Create `AffixSlot` enum: `Implicit = 0, Prefix = 1, Suffix = 2`

**File:** `Assets/Scripts/Items/Components/ItemAffix.cs` (NEW)

### 4.3 AffixDefinitionSO

- [ ] Create `AffixDefinitionSO` ScriptableObject — defines a single affix type
  - `int AffixId`
  - `string DisplayName` (e.g., "of the Bear", "Sharp")
  - `AffixSlot Slot`
  - `AffixStatModifier[] Modifiers` (which stats this affects)
  - `float MinValue`, `float MaxValue` (roll range)
  - `int MinTier`, `int MaxTier`
  - `ItemCategory[] ValidCategories` (which item types can roll this)
  - `ItemRarity MinRarity` (minimum rarity to roll this affix)
  - `float Weight` (relative roll weight)

**File:** `Assets/Scripts/Items/Definitions/AffixDefinitionSO.cs` (NEW)

### 4.4 AffixPoolSO

- [ ] Create `AffixPoolSO` ScriptableObject — collection of possible affixes for procedural generation
  - `List<AffixDefinitionSO> Prefixes`
  - `List<AffixDefinitionSO> Suffixes`
  - `List<AffixDefinitionSO> Implicits`
  - `int MaxPrefixes` (per rarity: Common=0, Uncommon=1, Rare=2, Epic=3, Legendary=4)
  - `int MaxSuffixes` (same scaling)

**File:** `Assets/Scripts/Items/Definitions/AffixPoolSO.cs` (NEW)

### 4.5 AffixRollSystem

- [ ] Create `AffixRollSystem` — generates affixes on item creation
  - Called by LootSpawnSystem when spawning equipment-type items
  - Reads `AffixPoolSO` from `ItemEntrySO.PossibleAffixes`
  - Rolls prefix/suffix count based on rarity
  - Rolls specific affixes based on weight and valid categories
  - Rolls values within min/max range (deterministic seed)
  - Writes `ItemAffix` buffer and updates `ItemStatBlock` with modifier values

**File:** `Assets/Scripts/Items/Systems/AffixRollSystem.cs` (NEW)

### 4.6 EquippedStatsSystem

- [ ] Create `EquippedStatsSystem` — aggregates stats from all equipped items
  - Reads `EquippedItemElement` buffer on player
  - For each equipped item entity: reads `ItemStatBlock`
  - Sums into `PlayerEquippedStats` component on player (new, simple aggregate)
  - Combat systems read `PlayerEquippedStats` instead of hardcoded values
  - Runs every frame equipment changes (dirty flag or enableable event)

**File:** `Assets/Scripts/Items/Systems/EquippedStatsSystem.cs` (NEW)
**File:** `Assets/Scripts/Items/Components/PlayerEquippedStats.cs` (NEW)

---

## Phase 5: Currency System

### 5.1 CurrencyType Enum

- [ ] Create `CurrencyType` enum
  - `Gold = 0` (primary currency)
  - `Premium = 1` (premium/special currency)
  - `Crafting = 2` (crafting tokens)
  - Extensible via sequential values

**File:** `Assets/Scripts/Economy/Components/CurrencyType.cs` (NEW)

### 5.2 CurrencyInventory Component

- [ ] Create `CurrencyInventory` IComponentData on player entities
  - `int Gold`
  - `int Premium`
  - `int Crafting`
  - Ghost-replicated (`[GhostField]` on each)
- [ ] Baked via `PlayerAuthoring` or dedicated `CurrencyAuthoring`

**File:** `Assets/Scripts/Economy/Components/CurrencyInventory.cs` (NEW)

### 5.3 CurrencyTransactionSystem

- [ ] Create `CurrencyTransactionSystem` — processes currency add/remove requests
  - `CurrencyTransaction` buffer element: `CurrencyType, int Amount (signed), Entity Source`
  - Server-authoritative: validates sufficient balance for removals
  - Clamps to 0 floor (no negative currency)
  - Generates `CurrencyChangeEvent` for UI

**File:** `Assets/Scripts/Economy/Systems/CurrencyTransactionSystem.cs` (NEW)
**File:** `Assets/Scripts/Economy/Components/CurrencyTransaction.cs` (NEW)

---

## Phase 6: Loot Containers

### 6.1 LootContainerState Component

- [ ] Create `LootContainerState` IComponentData — chests, crates, barrels
  - `ContainerType Type` (Chest, Crate, Barrel, BossChest)
  - `LootContainerPhase Phase` (Sealed, Opening, Open, Looted, Destroyed)
  - `int LootTableId` (maps to LootTableSO via registry)
  - `float OpenDuration` (animation time before loot spawns)
  - `bool IsReusable` (respawning containers)
  - `float RespawnTime` (if reusable)
  - `float LastOpenedTime`
  - `bool RequiresKey` (key item check)
  - `int RequiredKeyItemId`

**File:** `Assets/Scripts/Loot/Components/LootContainerComponents.cs` (NEW)

### 6.2 ContainerType & Phase Enums

- [ ] `ContainerType`: Chest = 0, Crate = 1, Barrel = 2, BossChest = 3
- [ ] `LootContainerPhase`: Sealed = 0, Opening = 1, Open = 2, Looted = 3, Destroyed = 4

**File:** `Assets/Scripts/Loot/Components/LootContainerComponents.cs` (append)

### 6.3 ContainerInteractionSystem

- [ ] Create `ContainerInteractionSystem` — handles player opening containers
  - `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
  - Player interacts with container → set Phase = Opening
  - After OpenDuration → Phase = Open, roll LootTableResolver, spawn loot at container position
  - Key check: if `RequiresKey`, verify player has RequiredKeyItemId in inventory
  - Reusable containers: Phase = Sealed after RespawnTime
  - Non-reusable: Phase = Looted (stays in world as visual) or Destroyed (entity destroyed)

**File:** `Assets/Scripts/Loot/Systems/ContainerInteractionSystem.cs` (NEW)

### 6.4 LootContainerAuthoring

- [ ] Create `LootContainerAuthoring` MonoBehaviour
  - Inspector fields matching LootContainerState
  - LootTableSO reference (resolved to LootTableId at bake time)
  - Baker: bakes LootContainerState + Interactable component (for interaction system)

**File:** `Assets/Scripts/Loot/Authoring/LootContainerAuthoring.cs` (NEW)

---

## Phase 7: Loot Presentation (Client-Side)

### 7.1 Loot Label System

- [ ] Create `LootLabelSystem` — client-side UI for dropped loot
  - `[WorldSystemFilter(ClientSimulation | LocalSimulation)]`
  - `[UpdateInGroup(typeof(PresentationSystemGroup))]`
  - Queries `LootEntity` entities within label range of local player
  - Creates/updates floating name labels with rarity-colored text
  - Rarity color mapping: Common=white, Uncommon=#00FF00, Rare=#0070DD, Epic=#A335EE, Legendary=#FF8000, Unique=#FFD700
  - LOD: full label within 10m, icon-only 10-30m, hidden 30m+
  - Integrates with existing loot label UI if available, otherwise creates minimal TextMeshPro labels

**File:** `Assets/Scripts/Loot/Presentation/LootLabelSystem.cs` (NEW)

### 7.2 Loot Beam / Highlight

- [ ] Create `LootHighlightSystem` — rarity-based visual indicators
  - Rare+: vertical light beam (pillar of light) using LineRenderer or particle
  - Epic+: pulsing glow effect
  - Legendary+: beam + particle aura
  - Common/Uncommon: no extra effects (performance)
  - Uses `LootVisualConfig` ScriptableObject for per-rarity prefab assignment

**File:** `Assets/Scripts/Loot/Presentation/LootHighlightSystem.cs` (NEW)
**File:** `Assets/Scripts/Loot/Config/LootVisualConfig.cs` (NEW)

### 7.3 Pickup Feedback

- [ ] Create `PickupFeedbackSystem` — UI/audio feedback on successful pickup
  - Reads `PickupEvent` on player entity
  - Triggers pickup sound (rarity-scaled)
  - Shows pickup notification in HUD (item name + icon + quantity)
  - Fly-to-inventory animation for item icon

**File:** `Assets/Scripts/Loot/Presentation/PickupFeedbackSystem.cs` (NEW)

---

## Phase 8: Designer Tooling

### 8.1 LootTable Inspector

- [ ] Custom editor for `LootTableSO`:
  - Visual pool weight bar (horizontal stacked bar showing relative weights)
  - "Simulate 1000 Drops" button — rolls table 1000 times, shows distribution histogram
  - Expected value calculator (average items per kill, rarity distribution)
  - Drag-and-drop item assignment
  - Nested table expansion (inline preview of sub-tables)
  - Condition editor with dropdown type selection

**File:** `Assets/Scripts/Loot/Editor/LootTableSOEditor.cs` (NEW)

### 8.2 ItemDatabase Inspector

- [ ] Custom editor for `ItemDatabaseSO`:
  - Searchable/filterable item list (by name, category, rarity)
  - Bulk operations (set all weapons to category, mass-assign rarity)
  - Duplicate ID warning with auto-fix
  - Missing prefab detection
  - "Create New Item" wizard button
  - Export to CSV / Import from CSV (for spreadsheet-based design)

**File:** `Assets/Scripts/Items/Editor/ItemDatabaseSOEditor.cs` (NEW)

### 8.3 Drop Preview Window

- [ ] Create `LootSimulatorWindow` EditorWindow — standalone testing tool
  - Select any enemy prefab with LootTableRef
  - Adjust context parameters (level, difficulty, luck)
  - Roll N times, display results table
  - Histogram of item distribution
  - Rarity distribution pie chart
  - "Export Results" to CSV

**File:** `Assets/Scripts/Loot/Editor/LootSimulatorWindow.cs` (NEW)

### 8.4 Loot Debug Overlay

- [ ] Create `LootDebugSystem` — runtime debug visualization
  - `[WorldSystemFilter(ClientSimulation | LocalSimulation)]`
  - Toggle via debug console
  - Shows all loot entities as colored dots on minimap
  - Hovering shows: ItemTypeId, Rarity, TimeRemaining, OwnershipMode
  - Log: "Entity X dropped [Rare Sword of Fire] at (x,y,z)"

**File:** `Assets/Scripts/Loot/Debug/LootDebugSystem.cs` (NEW)

---

## System Execution Order

```
SimulationSystemGroup (Server|Local):
  DeathTransitionSystem        [existing — fires DiedEvent]
  DeathLootSystem              [NEW, after DeathTransitionSystem]
  DeathSpawnProcessingSystem   [NEW, after DeathLootSystem]
  ContainerInteractionSystem   [NEW]
  LootSpawnSystem              [NEW, after DeathLootSystem + ContainerInteraction]
  ItemPickupSystem             [MODIFIED, after LootSpawnSystem]
  CurrencyTransactionSystem    [NEW, after ItemPickupSystem]
  EquippedStatsSystem          [NEW]
  LootLifetimeSystem           [NEW, OrderLast]

PresentationSystemGroup (Client|Local):
  LootLabelSystem              [NEW]
  LootHighlightSystem          [NEW]
  PickupFeedbackSystem         [NEW]
  LootDebugSystem              [NEW, debug only]
```

---

## File Summary

### New Files

| # | File | Type | Phase |
|---|------|------|-------|
| 1 | `Items/Definitions/ItemDatabaseSO.cs` | ScriptableObject | 0 |
| 2 | `Items/Definitions/ItemEntrySO.cs` | ScriptableObject | 0 |
| 3 | `Items/Components/ItemRarity.cs` | Enum | 0 |
| 4 | `Items/Systems/ItemRegistryBootstrapSystem.cs` | SystemBase | 0 |
| 5 | `Items/Components/ItemRegistryEntry.cs` | Struct | 0 |
| 6 | `Loot/Definitions/LootTableSO.cs` | ScriptableObject | 1 |
| 7 | `Loot/Definitions/LootPoolSO.cs` | ScriptableObject | 1 |
| 8 | `Loot/Definitions/LootPoolEntry.cs` | Serializable Struct | 1 |
| 9 | `Loot/Definitions/LootConditions.cs` | Enum + Struct | 1 |
| 10 | `Loot/Systems/LootTableResolver.cs` | Static Utility | 1 |
| 11 | `Loot/Components/LootComponents.cs` | IComponentData + IBufferElementData | 2 |
| 12 | `Loot/Authoring/LootTableAuthoring.cs` | Baker | 2 |
| 13 | `Loot/Systems/LootTableRegistryBootstrapSystem.cs` | SystemBase | 2 |
| 14 | `Loot/Systems/DeathLootSystem.cs` | SystemBase | 2 |
| 15 | `Loot/Systems/DeathSpawnProcessingSystem.cs` | SystemBase | 2 |
| 16 | `Loot/Systems/LootSpawnSystem.cs` | SystemBase | 2 |
| 17 | `Loot/Network/LootNetworkComponents.cs` | RPC structs | 3 |
| 18 | `Loot/Systems/LootLifetimeSystem.cs` | ISystem, Burst | 3 |
| 19 | `Items/Components/ItemStatBlock.cs` | IComponentData | 4 |
| 20 | `Items/Components/ItemAffix.cs` | IBufferElementData | 4 |
| 21 | `Items/Definitions/AffixDefinitionSO.cs` | ScriptableObject | 4 |
| 22 | `Items/Definitions/AffixPoolSO.cs` | ScriptableObject | 4 |
| 23 | `Items/Systems/AffixRollSystem.cs` | SystemBase | 4 |
| 24 | `Items/Systems/EquippedStatsSystem.cs` | ISystem | 4 |
| 25 | `Items/Components/PlayerEquippedStats.cs` | IComponentData | 4 |
| 26 | `Economy/Components/CurrencyType.cs` | Enum | 5 |
| 27 | `Economy/Components/CurrencyInventory.cs` | IComponentData | 5 |
| 28 | `Economy/Systems/CurrencyTransactionSystem.cs` | SystemBase | 5 |
| 29 | `Economy/Components/CurrencyTransaction.cs` | IBufferElementData | 5 |
| 30 | `Loot/Components/LootContainerComponents.cs` | IComponentData + Enums | 6 |
| 31 | `Loot/Systems/ContainerInteractionSystem.cs` | SystemBase | 6 |
| 32 | `Loot/Authoring/LootContainerAuthoring.cs` | Baker | 6 |
| 33 | `Loot/Presentation/LootLabelSystem.cs` | SystemBase | 7 |
| 34 | `Loot/Presentation/LootHighlightSystem.cs` | SystemBase | 7 |
| 35 | `Loot/Presentation/PickupFeedbackSystem.cs` | SystemBase | 7 |
| 36 | `Loot/Config/LootVisualConfig.cs` | ScriptableObject | 7 |
| 37 | `Loot/Editor/LootTableSOEditor.cs` | Custom Editor | 8 |
| 38 | `Items/Editor/ItemDatabaseSOEditor.cs` | Custom Editor | 8 |
| 39 | `Loot/Editor/LootSimulatorWindow.cs` | EditorWindow | 8 |
| 40 | `Loot/Debug/LootDebugSystem.cs` | SystemBase | 8 |

### Modified Files

| # | File | Changes | Phase |
|---|------|---------|-------|
| 1 | `Items/Systems/ItemPickupSystem.cs` | Complete TryAddToInventory() stub, add inventory logic | 3 |
| 2 | `Items/Components/ItemComponents.cs` | Add ItemRarity field to ItemDefinition (optional) | 0 |

---

## Backward Compatibility

| Feature | Default | Effect |
|---------|---------|--------|
| LootTableRef absent | No death drops | Enemies without LootTableAuthoring behave exactly as before |
| ItemRarity default | Common (0) | Existing items default to Common rarity |
| ItemStatBlock absent | No stat contribution | Existing equipped items contribute zero stats |
| CurrencyInventory absent | No currency | Players without CurrencyAuthoring have no currency |
| LootOwnership.Mode | FreeForAll | All loot is FFA by default (current behavior) |
| ItemAffix buffer absent | No affixes | Existing items are "clean" with no modifiers |
| AffixPoolSO absent on ItemEntrySO | No procedural generation | Item always spawns with base stats only |
| LootContainerState absent | No containers | World objects without LootContainerAuthoring unchanged |

All existing systems (VoxelLootSystem, InventoryWeightSystem, CargoTransferSystem, equipment pipeline) continue to function without modification. The new loot pipeline is additive.

---

## Performance Considerations

### ECS-Native Hot Paths
- `LootTableResolver` uses `Unity.Mathematics.Random` (no managed allocations)
- `LootLifetimeSystem` and `LootSpawnSystem` are Burst-compatible where possible
- `ItemPickupSystem` proximity check uses existing physics overlap (no per-frame distance matrix)
- `EquippedStatsSystem` only recalculates on equipment change (dirty flag)

### Memory
- `PendingLootSpawn` buffer capacity 8 — covers most enemies (4+ drops is generous)
- `ItemAffix` buffer capacity 4 — matches ARPG conventions (2 prefix + 2 suffix)
- `LootTableSO` and `ItemDatabaseSO` are ScriptableObjects — loaded once, shared across entities
- `ItemRegistryEntry` is a blittable struct in `NativeHashMap` — Burst-accessible, no GC

### Scalability
- Max 32 loot spawns per frame (queued) — prevents frame spikes during mass kills
- `LootLifetimeSystem` cleans up expired loot — bounded entity count
- Loot labels LOD'd by distance — 30m+ hidden, keeps draw calls bounded
- Container loot spawns are server-authoritative — no client prediction overhead

### 16KB Archetype Awareness
- `LootTableRef` is a small IComponentData (16 bytes) — safe to add to enemy prefabs
- `ItemStatBlock` goes on ITEM entities, not player entities — no player archetype pressure
- `CurrencyInventory` is a single IComponentData (12 bytes) — safe for player entity
- `PlayerEquippedStats` is a single IComponentData (~32 bytes) — safe for player entity
- `ItemAffix` buffer on ITEM entities only — no player archetype impact

---

## Genre Flexibility

| Genre | Loot Tables | Affixes | Currency | Containers | Rarity |
|-------|-------------|---------|----------|------------|--------|
| Survival | Resource-heavy pools | Minimal (tool durability) | Barter resources | Crates/barrels | Common/Uncommon focus |
| ARPG | Enemy-centric, boss tables | Full affix system | Gold + crafting | Chests, boss chests | Full rarity spectrum |
| Shooter | Ammo/consumable pools | Weapon attachments as affixes | Credits | Supply drops | 3-tier (Common/Rare/Legendary) |
| MMO | Raid tables, shared loot | Extensive prefix/suffix | Multiple currencies | Dungeon chests | Full + Unique tier |
| Roguelike | Random-weighted per floor | Procedural every run | Run currency | Room rewards | Weighted by depth |

The system is genre-agnostic by design. Unused features (affixes, containers, currency) have zero cost — components simply aren't added to entities.

---

## Modularity & Swappability

### Interface Points
- `IEquipmentProvider` (existing) — equipment pipeline already swappable
- `LootTableResolver` is a pure static utility — replace the resolver function to change drop logic without touching systems
- `ItemDatabaseSO` / `LootTableSO` are ScriptableObjects — swap data without code changes
- `LootSpawnSystem` reads from `PendingLootSpawn` buffer — any system can write to this buffer to trigger loot spawns (quests, events, crafting)
- `ItemPickupSystem` uses `PickupEvent` — any system can read pickup events for achievements, quests, analytics

### Replacement Scenarios
- **Different loot algorithm**: Replace `LootTableResolver.Resolve()` — all systems use this single entry point
- **Different item database**: Implement new `ItemRegistryBootstrapSystem` that reads from JSON/Addressables instead of SO
- **Different inventory model**: Modify `ItemPickupSystem.TryAddToInventory()` — single integration point
- **External item system (asset store)**: `IEquipmentProvider` already abstracts equipment; add `IInventoryProvider` interface for full abstraction
- **Server-side loot tables**: Replace `LootTableRegistryBootstrapSystem` to fetch tables from backend API instead of local SOs

---

## Verification Checklist

### Core Pipeline
- [ ] Enemy with LootTableAuthoring dies → loot entities spawn at corpse position
- [ ] Loot entities have correct ItemPickup component (ItemTypeId, Quantity)
- [ ] Player walks near auto-pickup loot → item added to inventory
- [ ] Player interacts with equipment loot → item added to equipment
- [ ] Inventory full → pickup rejected, item stays on ground
- [ ] Loot lifetime expires → entity destroyed

### Loot Tables
- [ ] LootTableSO with 3 pools → correct weighted distribution over 1000 rolls
- [ ] Nested table (pool entry → sub-table) → resolves recursively
- [ ] Conditional entry (MinLevel=5) → skipped when context.Level < 5
- [ ] Guaranteed + bonus rolls → correct number of outputs

### Network
- [ ] Server spawns loot → appears on all clients via ghost replication
- [ ] Client requests pickup → server validates → success/fail response
- [ ] Two players reach loot simultaneously → only one gets it (no duplication)
- [ ] KillerOnly ownership → other players can't pick up for 30s

### Rarity & Affixes
- [ ] Rare+ items spawn with affixes from AffixPoolSO
- [ ] Affix values within MinValue-MaxValue range
- [ ] EquippedStatsSystem aggregates stats from all equipped items correctly
- [ ] Unequipping item removes its stat contribution

### Containers
- [ ] Player interacts with chest → opening animation → loot spawns
- [ ] Reusable container respawns after RespawnTime
- [ ] Key-locked container → requires key item in inventory

### Currency
- [ ] Currency drops add to CurrencyInventory
- [ ] Insufficient balance → transaction rejected

### Performance
- [ ] 100 simultaneous enemy deaths → loot spawned across multiple frames (32/frame cap)
- [ ] 500 loot entities in world → no frame rate impact (LootLifetimeSystem cleanup)
- [ ] Loot labels LOD → draw calls bounded at distance

### Tooling
- [ ] LootTableSO inspector shows weight visualization
- [ ] "Simulate 1000 Drops" produces plausible distribution
- [ ] ItemDatabaseSO detects duplicate IDs on validate
- [ ] LootSimulatorWindow runs and exports results

### Backward Compatibility
- [ ] Enemies without LootTableAuthoring → no change in behavior
- [ ] Existing VoxelLootSystem → still works independently
- [ ] Existing equipment pipeline → unaffected
- [ ] Existing InventoryItem/CargoTransfer → unaffected
