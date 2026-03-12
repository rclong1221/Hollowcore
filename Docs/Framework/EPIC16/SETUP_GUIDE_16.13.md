# SETUP GUIDE 16.13: Crafting & Recipe System

**Status:** Implemented
**Last Updated:** February 23, 2026
**Requires:** Interaction Framework (EPIC 16.1), Loot & Economy (EPIC 16.6), Quest System (EPIC 16.12, for recipe unlock rewards)

This guide covers Unity Editor setup for the crafting and recipe system. After setup, designers can create recipes as ScriptableObjects, place crafting stations in scenes, and players can craft items, resources, and currency through a queue-based system with real-time progress tracking.

---

## What Changed

Previously, there was no way for players to transform raw resources into useful items. The interaction framework had a `Craft` verb stub and `AsyncProcessingState` timer, but no actual crafting pipeline.

Now:

- **Recipe definitions** — ScriptableObject-based, with typed ingredients, costs, outputs, and station requirements
- **Crafting stations** — placeable entities with type, tier, speed multiplier, and queue capacity
- **Queue-based crafting** — players queue recipes at stations, timers tick server-side, outputs wait for collection
- **Resource and currency costs** — recipes consume inventory items and/or currency
- **Affix rolling** — equipment outputs can roll random affixes (min/max rarity)
- **Recipe unlocks** — players learn recipes via starter knowledge, quest rewards, or the unlock system
- **Multiplayer-safe** — server-authoritative validation, ghost-replicated queue/outputs, RPC communication
- **Crafting Workstation** — editor window with recipe editor, balance simulator, station config, and live debug

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Recipe registry bootstrap | Loads `Resources/RecipeDatabase` on initialization, builds lookup maps |
| RPC handling | Client craft/collect/cancel RPCs automatically routed to correct station entity |
| Ingredient validation | Server validates station type, tier, player knowledge, ingredients, and currency before starting |
| Queue processing | Burst-compiled timer ticks crafts server-side with station speed multiplier |
| Output generation | Completed queue items automatically become collectible outputs |
| Ghost replication | Queue progress and available outputs replicate to all clients in real time |
| Knowledge link wiring | CraftingKnowledgeLinkSystem auto-discovers parent player entity at runtime |
| Quest integration | Craft completions emit quest events (EPIC 16.12), quest rewards can unlock recipes |

---

## 1. Recipe Database Setup

The recipe database is the central registry loaded at runtime. It must exist before any crafting can occur.

### 1.1 Create the Database

1. In the Project window, right-click > **Create > DIG > Crafting > Recipe Database**
2. Name it `RecipeDatabase`
3. Place it at `Assets/Resources/RecipeDatabase.asset` (must be in Resources root)

> This is loaded by `RecipeRegistryBootstrapSystem` on game start. If missing, the console logs a warning and the crafting system is disabled.

---

## 2. Creating Recipes

### 2.1 Create a Recipe Definition

1. Right-click > **Create > DIG > Crafting > Recipe Definition**
2. Name it descriptively (e.g., `Recipe_IronSword`, `Recipe_HealthPotion`)
3. Place it anywhere in your project (referenced by the database, not loaded from Resources)

### 2.2 Identity Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| **Recipe Id** | int | Yes | Globally unique integer ID (must be > 0) |
| **Display Name** | string | Yes | Name shown in UI (e.g., "Iron Sword") |
| **Description** | string (TextArea) | No | Flavor text |
| **Icon** | Sprite | No | UI icon for the recipe |
| **Category** | RecipeCategory | Yes | Weapons, Armor, Consumables, Ammo, Materials, Tools, or Upgrades |
| **Tier** | int | No | Recipe tier (1-5), used for station tier gating |
| **Sort Order** | int | No | Controls display order within category |

### 2.3 Ingredients

Click the **Ingredients** array and add entries. Each ingredient has:

| Field | Type | Description |
|-------|------|-------------|
| **Ingredient Type** | IngredientType | `Resource` or `Item` |
| **Resource Type** | ResourceType | Which resource (Stone, Metal, BioMass, Crystal, TitanBone, ThermalGlass, Isotope). Only used when Type = Resource |
| **Item Type Id** | int | Specific item ID. Only used when Type = Item |
| **Quantity** | int (min 1) | How many required |

### 2.4 Currency Costs

Click the **Currency Costs** array to add gold or crafting currency costs:

| Field | Type | Description |
|-------|------|-------------|
| **Currency Type** | CurrencyType | Gold, Premium, or Crafting |
| **Amount** | int | Amount deducted on craft start |

### 2.5 Output

Every recipe produces exactly one output:

| Field | Type | Description |
|-------|------|-------------|
| **Output Type** | RecipeOutputType | `Item`, `Resource`, or `Currency` |
| **Item Type Id** | int | Item ID to produce. Only used when OutputType = Item |
| **Resource Type** | ResourceType | Resource to produce. Only used when OutputType = Resource |
| **Quantity** | int (min 1) | How many to produce |
| **Roll Affixes** | bool | If true, item output gets random affixes (equipment only) |
| **Min Rarity** | int | Minimum rarity tier for affix rolling |
| **Max Rarity** | int | Maximum rarity tier for affix rolling |

> MinRarity must be <= MaxRarity when Roll Affixes is enabled. The custom inspector validates this.

### 2.6 Station Requirements

| Field | Type | Description |
|-------|------|-------------|
| **Crafting Time** | float | Seconds to craft (0 = instant) |
| **Required Station** | StationType | Any, Workbench, Forge, AlchemyTable, Armory, or Engineering |
| **Required Station Tier** | int | Minimum station tier needed (1-5) |

### 2.7 Unlock Conditions

| Field | Type | Description |
|-------|------|-------------|
| **Unlock Condition** | RecipeUnlockCondition | When this recipe becomes available |
| **Unlock Value** | int | Context-dependent value (see below) |
| **Prerequisite Recipe Ids** | int[] | Recipe IDs that must have been crafted first |

#### Unlock Condition Reference

| Condition | Unlock Value means | Notes |
|-----------|-------------------|-------|
| **AlwaysAvailable** | Ignored | Recipe known from the start (or use Starter Recipes on authoring) |
| **PlayerLevel** | Required level | Stub — requires EPIC 16.14 |
| **PreviousRecipe** | Prerequisite Recipe ID | Must have crafted this recipe at least once |
| **QuestComplete** | Quest ID | Recipe unlocked as quest reward (EPIC 16.12 integration) |
| **SchematicItem** | Item Type ID | Player must have found the schematic item |

### 2.8 Add to Database

1. Open your `RecipeDatabase` asset
2. Add each `RecipeDefinitionSO` to the **Recipes** list

> The custom inspector on RecipeDefinitionSO shows a validation header and a summary box with ingredient costs, output preview, and station requirements.

---

## 3. Crafting Station Setup

Crafting stations are scene entities where players interact to craft items. They combine three authoring components.

### 3.1 Prerequisites

The station entity must have:
- **StationAuthoring** — from the Interaction framework (provides InteractionSession)
- **InteractableAuthoring** — with `Can Interact = true` and a suitable verb

### 3.2 Add Crafting Station Component

1. Select the station GameObject in your SubScene
2. Click **Add Component** > search for **Crafting Station**
3. Configure the inspector fields:

| Field | Range | Default | Description |
|-------|-------|---------|-------------|
| **Station Type** | Dropdown | Workbench | What kind of station (determines which recipes are available) |
| **Station Tier** | 1-5 | 1 | Higher tiers unlock higher-tier recipes |
| **Speed Multiplier** | 0.5-3.0 | 1.0 | Multiplier for craft time (2.0 = twice as fast) |
| **Max Queue Size** | 1-4 | 4 | Maximum number of recipes queued simultaneously |

### 3.3 Station Types

| Type | Intended Use |
|------|-------------|
| **Any** | Universal station (matches all recipes with RequiredStation = Any) |
| **Workbench** | General crafting, basic items, materials |
| **Forge** | Metal weapons, armor, tools |
| **AlchemyTable** | Potions, consumables, toxins |
| **Armory** | Heavy armor, shields, upgrades |
| **Engineering** | Ammo, gadgets, advanced tools |

> A recipe with `RequiredStation = Any` can be crafted at any station type. A recipe with `RequiredStation = Forge` can only be crafted at a Forge.

### 3.4 Station Tier Gating

A station of tier N can craft any recipe with `RequiredStationTier <= N`. So a Tier 3 Forge can craft Tier 1, 2, and 3 Forge recipes.

---

## 4. Player Crafting Knowledge Setup

Player crafting knowledge lives on a **child entity** to avoid the 16KB player archetype limit. This requires a specific setup on the player prefab.

### 4.1 Add the Authoring Component

1. Open the player prefab (e.g., `Warrok_Server`)
2. Create a **child GameObject** named `CraftingKnowledge` (or similar)
3. On the child, click **Add Component** > search for **Crafting Knowledge**
4. In the **Starter Recipe Ids** array, enter the Recipe IDs the player should know from the start

> The child entity pattern follows `TargetingModuleAuthoring`. The `CraftingKnowledgeLinkSystem` automatically discovers the parent-child relationship at runtime via `LinkedEntityGroup` — no manual link setup needed.

### 4.2 Starter Recipes

Starter recipes are the recipes a player knows immediately without any unlock condition. Enter their Recipe IDs in the array:

```
Starter Recipe Ids:
  Size: 3
  Element 0: 1    (e.g., Basic Bandage)
  Element 1: 2    (e.g., Stone Knife)
  Element 2: 5    (e.g., Campfire)
```

### 4.3 Learning New Recipes

Players learn new recipes through:

| Method | How It Works |
|--------|-------------|
| **Starter Recipes** | Baked into the child entity at spawn |
| **Quest Rewards** | QuestRewardSystem writes RecipeUnlockRequest when reward type = RecipeUnlock |
| **Manual Unlock** | Any system can write to the RecipeUnlockRequest buffer on the knowledge entity |

The `RecipeUnlockSystem` processes requests each frame, checks for duplicates, and adds new entries to the `KnownRecipeElement` buffer.

---

## 5. Editor Tooling

### 5.1 Crafting Workstation

**Menu:** DIG > Crafting Workstation

A four-tab editor window:

#### Recipe Editor Tab

- **Database selector** — assign or auto-load RecipeDatabaseSO from Resources
- **Search bar** — filter by name or ID
- **Category filter** — dropdown to show Weapons, Armor, Consumables, etc.
- **Station type filter** — dropdown to filter by required station
- **Recipe list** — left panel with category color coding (Forge=orange, AlchemyTable=green, etc.)
- **Inline inspector** — right panel shows the selected RecipeDefinitionSO's full inspector
- **+ New Recipe** button — creates a new SO with auto-incremented ID and adds to database

#### Balance Simulator Tab

Monte Carlo resource sink analysis:

- **Iterations** slider — number of simulation runs (100-10,000)
- **Run Simulation** button — calculates total resource and currency costs across all recipes
- Per-recipe breakdown: ingredient totals, currency costs, craft time
- Useful for checking economy balance (e.g., "how much Metal does a full armor set cost?")

#### Station Config Tab

- **Scene station list** — all `CraftingStationAuthoring` components in open scenes
- Select a station to see:
  - Station type, tier, speed, queue size
  - All recipes available at that station (filtered by type and tier)
  - Ingredient summary across available recipes

#### Live Debug Tab (Play Mode only)

- **Active station count** — all CraftingStation entities in the world
- **Per-station display:**
  - Station type, tier, speed multiplier, queue capacity
  - Queue entries with state badges (Queued/InProgress/Complete)
  - Progress bars for active crafts (elapsed/total seconds)
  - Output buffer with ready-to-collect items
- **Color coding** — Workbench=brown, Forge=orange, AlchemyTable=green, Armory=blue, Engineering=yellow

### 5.2 Recipe Inspector

Each `RecipeDefinitionSO` has a custom inspector showing:

- **Validation header** — errors (RecipeId <= 0, zero quantities, MinRarity > MaxRarity) and warnings (empty name, no ingredients)
- **Green "Recipe valid" badge** when all checks pass
- **Default inspector** — all fields editable as normal
- **Summary box** — formatted overview:
  - Costs: resource/item ingredients and currency
  - Produces: output type, quantity, affix rolling info
  - Station: required station type and tier
  - Time: craft duration in seconds

---

## 6. UI Integration

The crafting system provides a UI bridge but does **not** include a built-in crafting panel. To display crafting data:

### 6.1 Implement CraftingUILink

Create a MonoBehaviour that inherits from `CraftingUILink`:

1. Override `OpenCraftingUI(StationType stationType, byte tier)` — show crafting panel
2. Override `CloseCraftingUI()` — hide crafting panel
3. Override `RefreshQueue(CraftQueueEntry[] queue)` — update queue display
4. Override `RefreshOutputs(CraftOutputEntry[] outputs)` — update collectible outputs

### 6.2 Place on Station GameObjects

1. Add your CraftingUILink MonoBehaviour to the station's companion GameObject
2. Set the **Session ID** to match the station's `InteractionSession` ID
3. The bridge system automatically calls your overrides when the local player enters/exits a station session

### 6.3 Sending Craft Commands

From your UI code, create and send RPCs to request actions:

- **Start crafting**: Send `CraftRequestRpc` with the Recipe ID and station ghost ID
- **Collect output**: Send `CollectCraftRpc` with the output index and station ghost ID
- **Cancel queued craft**: Send `CancelCraftRpc` with the queue index and station ghost ID

### 6.4 UI Event Queue

Poll `CraftingEventQueue.TryDequeue()` each frame for feedback events:

| Event Type | When |
|-----------|------|
| **CraftStarted** | Recipe successfully queued |
| **CraftCompleted** | Craft finished, output ready |
| **CraftFailed** | Validation failed (generic) |
| **InsufficientIngredients** | Missing ingredients or currency |
| **RecipeUnlocked** | Player learned a new recipe |
| **OutputCollected** | Player collected a finished output |

Each event includes `RecipeId` and `StationEntity` for context.

---

## 7. Example: Complete Crafting Setup

### "Iron Sword" — Forge a sword from metal and crystal

**Step 1:** Create `Recipe_IronSword` (RecipeDefinitionSO)

| Field | Value |
|-------|-------|
| Recipe Id | 101 |
| Display Name | Iron Sword |
| Description | A sturdy blade forged from refined metal. |
| Category | Weapons |
| Tier | 2 |
| Crafting Time | 15.0 |
| Required Station | Forge |
| Required Station Tier | 2 |
| Unlock Condition | AlwaysAvailable |

**Step 2:** Add ingredients

| Ingredient Type | Resource Type | Quantity |
|----------------|--------------|---------|
| Resource | Metal | 5 |
| Resource | Crystal | 2 |

**Step 3:** Add currency cost

| Currency Type | Amount |
|--------------|--------|
| Gold | 50 |

**Step 4:** Configure output

| Field | Value |
|-------|-------|
| Output Type | Item |
| Item Type Id | 201 (iron sword item ID) |
| Quantity | 1 |
| Roll Affixes | true |
| Min Rarity | 1 |
| Max Rarity | 3 |

**Step 5:** Add to RecipeDatabase, add 101 to player's Starter Recipe Ids (or gate behind quest/schematic)

**Step 6:** Place a Forge station in the SubScene (StationAuthoring + InteractableAuthoring + CraftingStationAuthoring with StationType=Forge, StationTier=2)

**Step 7:** Reimport SubScene

---

## 8. After Setup: Reimport SubScene

After placing or modifying crafting-related authoring components:

1. Right-click the SubScene > **Reimport**
2. Ensure `RecipeDatabase.asset` is at `Assets/Resources/RecipeDatabase`
3. Wait for baking to complete

> Ghost prefab changes (player knowledge, station buffers) require SubScene reimport to regenerate ghost serialization variants.

---

## 9. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Registry loads | Enter Play Mode | Console: "[RecipeRegistry] Loaded N recipe definitions." |
| 3 | Station entity | Enter Play Mode, check Entity Inspector | Station has CraftingStation + CraftQueueElement + CraftOutputElement |
| 4 | Player knowledge | Enter Play Mode, check player child entities | CraftingKnowledgeTag entity with KnownRecipeElement buffer containing starter recipes |
| 5 | Knowledge link | Enter Play Mode | CraftingKnowledgeLink on player resolves to knowledge child entity |
| 6 | Craft request | Interact with station, send CraftRequestRpc for known recipe | CraftQueueElement added with State=Queued |
| 7 | Timer ticks | Watch queue in Live Debug | CraftTimeElapsed increases each frame |
| 8 | Speed multiplier | Station with SpeedMultiplier=2.0 | Craft completes in half the listed time |
| 9 | Output generated | Craft completes | CraftOutputElement appears in station's output buffer |
| 10 | Collect resource | Send CollectCraftRpc for resource output | InventoryItem quantity increases |
| 11 | Collect currency | Send CollectCraftRpc for currency output | CurrencyTransaction buffer gets positive entry |
| 12 | Cancel queued | Cancel a Queued (not InProgress) craft | Ingredients refunded, queue entry removed |
| 13 | Cancel denied | Try to cancel InProgress craft | No change (cancel only works on Queued state) |
| 14 | Station type gate | Try crafting a Forge recipe at a Workbench | Validation fails, CraftingEventQueue gets InsufficientIngredients |
| 15 | Station tier gate | Try crafting Tier 3 recipe at Tier 1 station | Validation fails |
| 16 | Recipe knowledge | Try crafting unknown recipe | Validation fails |
| 17 | Recipe unlock | Complete quest with RecipeUnlock reward | Recipe added to KnownRecipeElement buffer |
| 18 | Quest tracking | Craft a recipe tracked by a quest objective | Quest objective progress increments |
| 19 | Multiplayer | Two players at same station | Independent queues, outputs tagged to correct player |
| 20 | Ghost replication | Remote client watches station | Queue progress and outputs visible in real time |
| 21 | Crafting Workstation | Open DIG > Crafting Workstation | All 4 tabs render without errors |
| 22 | Balance Sim | Run simulation with recipes | Resource/currency totals displayed |
| 23 | Live Debug | Enter Play Mode with active crafts | Progress bars and output buffers visible |

---

## 10. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| "[RecipeRegistry] No RecipeDatabaseSO found" | Missing or misplaced database | Place at `Assets/Resources/RecipeDatabase.asset` |
| Station doesn't appear in Live Debug | Missing CraftingStationAuthoring or not in SubScene | Add component, reimport SubScene |
| "Validation failed" on every craft | Player has no CraftingKnowledgeLink | Add CraftingKnowledgeAuthoring to child GO of player prefab, reimport |
| Player doesn't know starter recipes | Starter Recipe Ids not set or child entity not baked | Check CraftingKnowledgeAuthoring inspector, reimport SubScene |
| Knowledge link not wiring | CraftingKnowledgeAuthoring not on child GO | Must be on a child GameObject (not root player GO) |
| Craft starts but timer never advances | CraftQueueProcessingSystem not running | Check WorldSystemFilter — requires ServerSimulation or LocalSimulation |
| Output generated but can't collect | ForPlayer doesn't match requesting player | Check CraftRpcReceiveSystem RPC → player resolution via CommandTarget |
| Cancel doesn't refund ingredients | Craft was InProgress (not Queued) | By design — only Queued crafts can be cancelled for refund |
| Quest objective doesn't track crafts | CraftQuestEventEmitterSystem not running or recipe ID mismatch | Check quest objective TargetId matches Recipe ID |
| Recipe unlock from quest not working | Player missing CraftingKnowledgeLink or knowledge entity missing RecipeUnlockRequest buffer | Verify child entity setup on player prefab |
| "No CraftingUILink registered" warning | No UI MonoBehaviour registered for station session ID | Implement CraftingUILink subclass (or ignore if UI not yet built) |
| Instant craft (time=0) output not appearing | Expected behavior — instant crafts skip queue | Output goes directly to CraftOutputElement buffer |
| Custom inspector shows errors | RecipeId <= 0 or output quantity <= 0 | Fix values flagged in the validation header |
| Balance simulator shows no data | No RecipeDatabaseSO assigned | Assign database in Recipe Editor tab first, or ensure it's at Resources/RecipeDatabase |

---

## 11. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Interaction framework (station sessions, interactables) | SETUP_GUIDE_16.1 |
| Loot, items, inventory, currency | SETUP_GUIDE_16.6 |
| Quest objectives tracking crafts, recipe unlock rewards | SETUP_GUIDE_16.12 |
| XP rewards for crafting | EPIC 16.14 (planned) |
| Crafting progress persistence | EPIC 16.15 (planned) |
| **Crafting & Recipe System** | **This guide (16.13)** |
