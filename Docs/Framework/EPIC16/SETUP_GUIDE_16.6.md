# SETUP GUIDE 16.6: Loot & Drop System Framework

**Status:** Implemented
**Last Updated:** February 22, 2026
**Requires:** Damageable Authoring on enemies (EPIC 15.28), Death Lifecycle (EPIC 16.3)

This guide covers Unity Editor setup for the loot and drop system. After setup, enemies drop loot on death from designer-configured loot tables, items have rarity and affixes, players pick up loot automatically, and currency flows through the economy system.

---

## What Changed

Previously, there was no data-driven pipeline from enemy death to player inventory. `ItemPickupSystem.TryAddToInventory()` was a stub that returned true and did nothing. `DeathSpawnElement` buffers were baked but never consumed. Designers had to write C# to make enemies drop loot.

Now:

- **Loot tables** — ScriptableObject-based, weighted pools with nested tables, conditions, and bonus rolls
- **Death drops** — enemies with a loot table drop items on death automatically
- **Item database** — central registry of all game items with metadata, prefabs, and classification
- **Item rarity & affixes** — Common through Unique rarity, with prefix/suffix stat modifiers
- **Pickup system** — proximity auto-pickup routes weapons to equipment slots and resources to inventory
- **Loot containers** — chests, crates, barrels with state machine (Sealed→Opening→Open→Looted)
- **Currency system** — Gold/Premium/Crafting with ghost-replicated inventory and transaction buffer
- **Death spawn processing** — `DeathSpawnElement` buffers (authored via `DeathSpawnAuthoring`) now actually spawn their gibs/VFX
- **Loot lifetime** — items despawn after rarity-scaled duration (Common=60s, Legendary=300s)
- **Editor tools** — loot table simulator, item database inspector

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Item registry bootstrap | Loads `Resources/ItemDatabase` on initialization, builds lookup maps |
| Loot table registry | Loads all `LootTableSO` from `Resources/LootTables/` on initialization |
| Pickup proximity scan | Players with `HasEquipment` + `PlayerTag` auto-collect nearby loot |
| Loot lifetime | Items auto-despawn after rarity-scaled duration |
| Currency transactions | `CurrencyTransaction` buffer entries processed each frame, clamped to zero |
| Death spawn processing | `DeathSpawnElement` buffers spawn gibs/VFX at death position |

---

## 1. Item Database Setup

The item database is the central registry of all items. It must exist before loot tables can reference items.

### 1.1 Create the Database

1. In the Project window, right-click > **Create > DIG > Items > Item Database**
2. Name it `ItemDatabase`
3. Place it at `Assets/Resources/ItemDatabase.asset` (must be in Resources for runtime loading)

### 1.2 Create Item Entries

For each item in the game:

1. Right-click > **Create > DIG > Items > Item Entry**
2. Fill in the fields:

#### Identity

| Field | Description | Required |
|-------|-------------|----------|
| **Item Type ID** | Unique integer ID (must be globally unique) | Yes |
| **Display Name** | Human-readable name | Yes |
| **Description** | Tooltip text | No |
| **Icon** | Sprite for UI | No |

#### Prefabs

| Field | Description | Required |
|-------|-------------|----------|
| **World Prefab** | Pickup entity spawned in the world | Yes (for droppable items) |
| **Equip Prefab** | Visual when equipped on player | No |

#### Classification

| Field | Description | Default |
|-------|-------------|---------|
| **Category** | Weapon, Tool, Consumable, etc. | — |
| **Rarity** | Common, Uncommon, Rare, Epic, Legendary, Unique | Common |

#### Stacking

| Field | Description | Default |
|-------|-------------|---------|
| **Is Stackable** | Whether multiples stack in one slot | false |
| **Max Stack** | Maximum stack size | 1 |

#### Weight & Economy

| Field | Description | Default |
|-------|-------------|---------|
| **Weight** | Inventory weight | 0 |
| **Equip Duration** | Seconds to equip | 0.3 |
| **Resource Type** | Associated resource (for resource-type items) | None |
| **Sell Value** | Gold value when selling | 0 |
| **Buy Value** | Gold value when buying | 0 |

#### Equipment (Optional)

| Field | Description |
|-------|-------------|
| **Weapon Category** | Reference to `WeaponCategoryDefinition` SO |
| **Possible Affixes** | Reference to `AffixPoolSO` for procedural stat rolls |

### 1.3 Add Entries to Database

1. Open your `ItemDatabase` asset
2. Add each `ItemEntrySO` to the Entries list
3. The custom editor shows validation warnings for duplicate IDs or missing prefabs

### 1.4 Item Database Editor

Select the `ItemDatabase` asset to see the custom inspector:
- **Search** by name or ID
- **Category filter** toggle with enum popup
- **Filtered list** with ID, Name, Category, Rarity columns
- **Validation foldout** checks for: null entries, duplicate IDs, missing WorldPrefab, empty names, invalid MaxStack

---

## 2. Loot Table Setup

Loot tables define what drops when an enemy dies or a container opens.

### 2.1 Create a Loot Table

1. Right-click > **Create > DIG > Loot > Loot Table**
2. Name it descriptively (e.g., `LT_BoxingJoe`, `LT_BossWolf`)
3. Place in `Assets/Resources/LootTables/` (must be in Resources for runtime loading)

### 2.2 Loot Table Fields

| Field | Description | Default |
|-------|-------------|---------|
| **Table Name** | Human-readable name for tooling | — |
| **Guaranteed Rolls** | Number of always-performed rolls against pools | 1 |
| **Bonus Rolls** | Number of additional probabilistic rolls | 0 |
| **Bonus Roll Chance** | Per-roll success probability (0-1) | 0.5 |
| **Pools** | List of `LootPoolSO` references rolled against independently | — |
| **Conditions** | Min/max level and difficulty gates | — |

### 2.3 Create Loot Pools

1. Right-click > **Create > DIG > Loot > Loot Pool**
2. Add entries to the pool

#### Pool Fields

| Field | Description | Default |
|-------|-------------|---------|
| **Entries** | List of weighted drop entries | — |
| **Allow Duplicates** | Same entry can drop twice in one roll | true |
| **Pool Weight** | Relative weight vs other pools in the table | 1.0 |
| **Min Drops** | Minimum items from this pool per roll | 1 |
| **Max Drops** | Maximum items from this pool per roll | 1 |

#### Pool Entry Fields

| Field | Description |
|-------|-------------|
| **Type** | Item, Currency, Resource, or NestedTable |
| **Item** | `ItemEntrySO` reference (for Item type) |
| **Resource** | ResourceType enum (for Resource type) |
| **Currency** | CurrencyType enum (for Currency type) |
| **Nested Table** | Another `LootTableSO` (for NestedTable, max depth 4) |
| **Min Quantity** / **Max Quantity** | Amount range |
| **Weight** | Weighted selection within pool |
| **Drop Chance** | Applied after selection (0-1) |
| **Min Rarity** / **Max Rarity** | Rarity range for the drop |

### 2.4 Example: BoxingJoe Loot Table

**LT_BoxingJoe:**
- Guaranteed Rolls: 1
- Bonus Rolls: 1, Chance: 0.3

**Pool 1 — Resources** (PoolWeight=1, MinDrops=2, MaxDrops=4):

| Entry | Type | Resource | Weight | Quantity |
|-------|------|----------|--------|----------|
| Stone | Resource | Stone | 5.0 | 1–3 |
| Metal | Resource | Metal | 3.0 | 1–2 |
| Crystal | Resource | Crystal | 1.0 | 1 |

**Pool 2 — Equipment** (PoolWeight=0.3, MinDrops=1, MaxDrops=1):

| Entry | Type | Item | Weight | Drop Chance |
|-------|------|------|--------|-------------|
| Iron Sword | Item | IronSword_Entry | 3.0 | 0.1 |
| Leather Vest | Item | LeatherVest_Entry | 3.0 | 0.1 |
| Health Potion | Item | HealthPotion_Entry | 5.0 | 0.3 |

---

## 3. Assigning Loot Tables to Enemies

### 3.1 Add the Component

1. Select your enemy prefab root (same as `DamageableAuthoring`)
2. Click **Add Component** > search for **Loot Table Authoring**

### 3.2 Inspector Fields

| Field | Description | Default |
|-------|-------------|---------|
| **Loot Table** | Reference to a `LootTableSO` asset | — |
| **Drop Chance Multiplier** | Multiplies all drop chances (difficulty scaling) | 1.0 |
| **Quantity Multiplier** | Multiplies all quantities (generosity scaling) | 1.0 |

> When this enemy dies (triggers `DiedEvent`), `DeathLootSystem` automatically rolls the loot table and writes `PendingLootSpawn` entries. `LootSpawnSystem` then spawns the pickup entities.

---

## 4. Loot Containers (Chests, Crates, Barrels)

### 4.1 Add the Component

1. Create or select a container GameObject in your SubScene
2. Click **Add Component** > search for **Loot Container Authoring**

### 4.2 Inspector Fields

#### Container Settings

| Field | Description | Default |
|-------|-------------|---------|
| **Container Type** | Chest, Crate, Barrel, or BossChest | Chest |
| **Loot Table** | Reference to a `LootTableSO` | — |
| **Open Duration** | Seconds for the opening animation/transition | 1.0 |

#### Respawn

| Field | Description | Default |
|-------|-------------|---------|
| **Is Reusable** | Container resets after respawn time | false |
| **Respawn Time** | Seconds before container resets to Sealed | 300 (5 min) |

#### Key Requirement

| Field | Description | Default |
|-------|-------------|---------|
| **Requires Key** | Player needs a specific key item | false |
| **Required Key Item ID** | ItemTypeId of the required key | 0 |

### 4.3 Container Lifecycle

```
SEALED → OPENING (player interacts) → OPEN (after OpenDuration) → LOOTED → DESTROYED
                                                                        ↓ (if IsReusable)
                                                                    SEALED (after RespawnTime)
```

---

## 5. Item Affixes (Procedural Stats)

Items can roll random stat modifiers from an affix pool, scaling by rarity.

### 5.1 Create Affix Definitions

1. Right-click > **Create > DIG > Items > Affix Definition**

| Field | Description |
|-------|-------------|
| **Affix ID** | Unique integer |
| **Display Name** | e.g., "Sharp", "Sturdy", "of Flames" |
| **Slot** | Implicit (always present), Prefix, or Suffix |
| **Modifiers** | Array of {StatType, Multiplier} — e.g., BaseDamage × 1.2 |
| **Min Value** / **Max Value** | Roll range |
| **Min Tier** / **Max Tier** | Affix tier range (1-5) |
| **Valid Categories** | Item categories this can appear on (empty = all) |
| **Min Rarity** | Minimum item rarity for this affix to roll |
| **Weight** | Selection weight in the pool |

**StatType options:** BaseDamage, AttackSpeed, CritChance, CritMultiplier, Armor, MaxHealthBonus, MovementSpeedBonus, DamageResistance

### 5.2 Create Affix Pools

1. Right-click > **Create > DIG > Items > Affix Pool**
2. Reference this from `ItemEntrySO.PossibleAffixes`

| Field | Description |
|-------|-------------|
| **Implicits** | Always-present affixes |
| **Prefixes** | Prefix pool + MaxPrefixes (default 2) |
| **Suffixes** | Suffix pool + MaxSuffixes (default 2) |

Prefix/suffix count scales by rarity: Common=0, Uncommon=1, Rare=1, Epic=2, Legendary=2, Unique=3.

---

## 6. Currency System

### 6.1 CurrencyInventory

Added automatically to player entities. Three currency types:

| Currency | Field | Usage |
|----------|-------|-------|
| **Gold** | General economy | Buying, selling, repairs |
| **Premium** | Rare/MTX currency | Special items |
| **Crafting** | Crafting materials | Recipes, upgrades |

All fields are `[GhostComponent(AllPredicted)]` — replicated to all clients.

### 6.2 Transactions

To add or remove currency, append a `CurrencyTransaction` to the player's buffer:

```
CurrencyTransaction { Type = Gold, Amount = 100, Source = vendorEntity }
```

`CurrencyTransactionSystem` processes the buffer each frame. Negative amounts deduct (clamped to zero — no negative balances).

---

## 7. Death Spawn Processing

`DeathSpawnAuthoring` (pre-existing) now actually works. When an enemy with a `DeathSpawnElement` buffer dies:

1. `DeathSpawnProcessingSystem` reads the buffer
2. Instantiates each referenced prefab at the corpse position
3. Applies upward + angular scatter velocity if `ApplyExplosiveForce` is true

### Inspector Fields (DeathSpawnAuthoring)

| Field | Description |
|-------|-------------|
| **Prefabs To Spawn** | Array of GameObjects (gibs, blood splatters, effects) |
| **Apply Explosive Force** | Scatter spawned objects with physics velocity |

---

## 8. Editor Tools

### 8.1 Loot Table Custom Inspector

Select any `LootTableSO` asset in the Project window:

- **Pool weight visualization** — horizontal bars showing each pool's share of total weight
- **Drop simulation** — run N rolls (1–100,000), see total drops, empty roll rate, per-item averages

### 8.2 Loot Simulator Window

**Menu:** DIG > Loot Simulator

Standalone simulation tool:

| Parameter | Description |
|-----------|-------------|
| **Loot Table** | SO to simulate |
| **Roll Count** | 1–100,000 rolls |
| **Level** | Player level (1–100) |
| **Difficulty** | Difficulty multiplier (0.1–5.0) |
| **Luck** | Luck modifier (-1.0 to 1.0, shifts drop chances) |

Output: rarity distribution, per-item drop rates and averages, empty roll rate.

### 8.3 Item Database Inspector

Select the `ItemDatabaseSO` asset:

- Search by name or ID
- Category filter
- Per-item Select button to highlight the SO
- Validation: duplicate IDs, missing prefabs, empty names, invalid stacks

---

## 9. Loot Lifetime by Rarity

| Rarity | Lifetime | Color |
|--------|----------|-------|
| Common | 60s | White |
| Uncommon | 90s | Green |
| Rare | 120s | Blue |
| Epic | 300s | Purple |
| Legendary | 300s | Orange |
| Unique | 600s | Gold |

After the lifetime expires, `LootLifetimeSystem` destroys the pickup entity.

---

## 10. After Setup: Reimport SubScene

After placing or modifying loot authoring:

1. Right-click SubScene > **Reimport**
2. Ensure `ItemDatabase.asset` is at `Assets/Resources/ItemDatabase`
3. Ensure `LootTableSO` assets are in `Assets/Resources/LootTables/`

---

## 11. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Item database loads | Enter Play Mode | Console log from ItemRegistryBootstrapSystem (no errors) |
| 3 | Loot tables load | Enter Play Mode | Console log from LootTableRegistryBootstrapSystem |
| 4 | Enemy drops loot | Kill enemy with LootTableAuthoring | Pickup entities spawn at death position |
| 5 | Auto pickup | Walk near dropped loot | Item disappears, appears in inventory |
| 6 | Weapon pickup | Drop a weapon-category item | Appears in QuickSlot (1-9) in ItemSetEntry buffer |
| 7 | Resource pickup | Drop a resource item | Stacks into InventoryItem buffer |
| 8 | Loot lifetime | Drop Common loot, wait 60s | Entity despawns |
| 9 | Loot Simulator | Open DIG > Loot Simulator, run 10,000 rolls | Reasonable distribution, no errors |
| 10 | Death spawn gibs | Kill enemy with DeathSpawnAuthoring | Gib prefabs spawn with scatter |
| 11 | Container open | Interact with chest LootContainerAuthoring | State transitions Sealed→Opening→Open |
| 12 | Currency | Award gold via CurrencyTransaction | CurrencyInventory.Gold increases |

---

## 12. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| No loot drops on death | Missing LootTableAuthoring on enemy prefab | Add component, assign LootTableSO, reimport |
| "LootTable not found" log | LootTableSO not in Resources/LootTables/ | Move asset to correct folder |
| Items don't pick up | Player missing HasEquipment or PlayerTag | Verify PlayerAuthoring bakes both components |
| Weapon goes to wrong slot | QuickSlots 1-9 full | System looks for first available QuickSlot |
| Overweight rejection | InventoryCapacity.MaxWeight exceeded | Increase capacity or reduce item weights |
| Affix stats not applied | AffixPoolSO not assigned on ItemEntrySO | Assign PossibleAffixes reference |
| Database validation errors | Duplicate ItemTypeIds | Ensure each ItemEntrySO has a unique ID |
| Currency goes negative | Should not happen | CurrencyTransactionSystem clamps to zero |
| Container never opens | Missing interaction system wiring | `ContainerInteractionSystem.TryOpenContainer()` must be called by your interaction system |

---

## 13. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Health, damage, death | Damageable Authoring (EPIC 15.28) |
| Death lifecycle, corpses | SETUP_GUIDE_16.3 |
| Item equipment, weapon switching | SETUP_GUIDE_14.2 |
| Interaction system (container opening) | SETUP_GUIDE_16.1 |
| Resource framework (combat resources) | SETUP_GUIDE_16.8 |
| **Loot & drop system** | **This guide (16.6)** |
