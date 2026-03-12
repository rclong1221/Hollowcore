# EPIC 16.13: Crafting & Recipe System

**Status:** PLANNED
**Priority:** High (Core Gameplay Loop)
**Dependencies:**
- `InteractionSession` IComponentData (existing -- `DIG.Interaction`, EPIC 16.1)
- `StationSessionState` IComponentData (existing -- `DIG.Interaction`, player session tracking)
- `AsyncProcessingState` IComponentData (existing -- `DIG.Interaction`, timer mechanism)
- `AsyncProcessingSystem` ISystem (existing -- `DIG.Interaction`, ticks processing timer)
- `StationSessionSystem` (existing -- session enter/exit logic)
- `StationSessionBridgeSystem` (existing -- managed ECS→UI bridge)
- `StationUILink` MonoBehaviour (existing -- static registry pattern)
- `InteractionVerb.Craft` enum value (existing -- reserved for crafting)
- `InventoryItem` IBufferElementData (existing -- `DIG.Shared`, resource inventory)
- `InventoryCapacity` IComponentData (existing -- weight system)
- `ResourceType` enum (existing -- Stone, Metal, BioMass, Crystal, etc.)
- `CurrencyInventory` IComponentData (existing -- `DIG.Economy`, Gold/Premium/Crafting)
- `CurrencyTransaction` IBufferElementData (existing -- atomic currency operations)
- `ItemEntrySO` / `ItemDatabaseSO` (existing -- `DIG.Items`, EPIC 16.6)
- `ItemRegistryBootstrapSystem` (existing -- bootstrap singleton pattern reference)
- `AffixRollSystem` static utility (existing -- `DIG.Items`, affix rolling for equipment)
- `ItemAffix` IBufferElementData (existing -- item affixes)
- `LootSpawnSystem` (existing -- item entity creation pattern reference)

**Feature:** A server-authoritative crafting and recipe system where all crafting state lives on STATION entities (zero new components on player archetype beyond an 8-byte link), integrating with existing station sessions for player-station binding, inventory for ingredient consumption, economy for costs, and the affix system for equipment output.

---

## Problem

DIG has a complete interaction framework with station sessions, async processing timers, a full item/inventory system, an economy with crafting currency, and an affix rolling system for equipment -- but no recipe definitions, no ingredient consumption logic, no crafting queue management, and no crafting UI. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `InteractionSession` + `StationSessionState` for player-station binding | No crafting-specific station type or queue |
| `AsyncProcessingState` + `AsyncProcessingSystem` (timer mechanism) | Single-slot only, no multi-item queue |
| `InteractionVerb.Craft` (value=8) reserved | No system reads or uses it |
| `CurrencyType.Crafting` in economy | No recipe costs consume crafting currency |
| `InventoryItem` buffer with ResourceType + Quantity | No system consumes resources for crafting |
| `ItemEntrySO` with SellValue/BuyValue | No vendor or crafting cost integration |
| `AffixRollSystem.RollAffixes()` for equipment | No crafted equipment generation |
| `LootSpawnSystem` creates item entities from loot | No crafted item entity creation |
| `InteractionSetupWizard` has "Crafting Station" preset | Preset adds generic station, not crafting-specific |
| `AIWorkstationWindow` editor pattern | No crafting design/debug tooling |

**The gap:** A player can collect Stone, Metal, BioMass, and Crystal via loot drops -- but cannot combine them into weapons, armor, or consumables. There are no recipes, no crafting benches, no crafting UI.

---

## Architecture Overview

```
Designer creates RecipeDefinitionSO → RecipeDatabaseSO registry
                                         |
                          RecipeRegistryBootstrapSystem (loads from Resources/)
                                         |
                                  Managed + Blittable registries
                                         |
Player interacts with CraftingStation → StationSessionBridgeSystem opens UI
                                         |
                          CraftingUILink.OpenCraftingUI(stationType, tier)
                                         |
Player selects recipe → CraftRequestRpc (client → server)
                                         |
                          CraftRpcReceiveSystem → CraftRequest buffer on station
                                         |
                          CraftValidationSystem (Server only)
                          ├── validate: player in session, recipe exists, station match
                          ├── validate: ingredients available, currency sufficient, queue not full
                          ├── consume: decrement InventoryItem, write CurrencyTransaction
                          └── enqueue: CraftQueueElement(State=Queued)
                                         |
                          CraftQueueProcessingSystem (Burst ISystem, Server only)
                          └── tick: CraftTimeElapsed += dt * SpeedMultiplier
                              └── State=Complete when elapsed >= total
                                         |
                          CraftOutputGenerationSystem → CraftOutputElement
                                         |
Player clicks "Collect" → CollectCraftRpc → CraftOutputCollectionSystem
                          ├── Resource output → InventoryItem buffer
                          ├── Item output → AffixRollSystem → create entity via ECB
                          └── Currency output → CurrencyTransaction
                                         |
                          CraftingUIBridgeSystem (PresentationGroup, Client)
                          → reads ghost-replicated CraftQueueElement + CraftOutputElement
                          → drives crafting UI panel
```

---

## Data Layer (ScriptableObjects)

### RecipeDefinitionSO

**File:** `Assets/Scripts/Crafting/Definitions/RecipeDefinitionSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Crafting/Recipe Definition")]
```

| Field | Type | Purpose |
|-------|------|---------|
| RecipeId | int | Unique identifier |
| DisplayName | string | UI name |
| Description | string [TextArea] | Flavor text |
| Icon | Sprite | UI icon |
| Category | RecipeCategory | Weapons/Armor/Consumables/Ammo/Materials/Tools/Upgrades |
| Tier | int [Range(1,5)] | Controls station tier requirement |
| SortOrder | int | UI ordering within category |
| Ingredients[] | RecipeIngredient[] | Resource or item inputs |
| CurrencyCosts[] | CurrencyCost[] | Gold/Crafting currency costs |
| Output | RecipeOutput | What gets produced |
| CraftingTime | float | Seconds (0 = instant) |
| RequiredStation | StationType | Workbench/Forge/AlchemyTable/Armory/Engineering/Any |
| RequiredStationTier | int | Minimum station tier (0 = any) |
| UnlockCondition | RecipeUnlockCondition | AlwaysAvailable/PlayerLevel/PreviousRecipe/QuestComplete/SchematicItem |
| UnlockValue | int | Level, reputation, etc. |
| PrerequisiteRecipeIds | int[] | Must have crafted these first |

### Supporting Structs & Enums

**File:** `Assets/Scripts/Crafting/Definitions/RecipeStructs.cs`

```
RecipeIngredient   : { IngredientType(Resource|Item), ResourceType, ItemTypeId, Quantity }
CurrencyCost       : { CurrencyType, Amount }
RecipeOutput       : { RecipeOutputType(Item|Resource|Currency), ItemTypeId, ResourceType,
                       Quantity, MinRarity, MaxRarity, RollAffixes }
StationType enum   : Any(0), Workbench(1), Forge(2), AlchemyTable(3), Armory(4), Engineering(5)
RecipeCategory enum: Weapons(0), Armor(1), Consumables(2), Ammo(3), Materials(4), Tools(5), Upgrades(6)
RecipeUnlockCondition enum: AlwaysAvailable(0), PlayerLevel(1), PreviousRecipe(2),
                            QuestComplete(3), SchematicItem(4)
```

### RecipeDatabaseSO

**File:** `Assets/Scripts/Crafting/Definitions/RecipeDatabaseSO.cs`

Same pattern as `ItemDatabaseSO`: list + dictionary lookup. Loaded from `Resources/RecipeDatabase`.

---

## ECS Components

### On STATION Entities

**File:** `Assets/Scripts/Crafting/Components/CraftingStationComponents.cs`

```
CraftingStation (IComponentData, Ghost:All)
  StationType     : StationType
  StationTier     : byte (1-5)
  SpeedMultiplier : float (default 1.0, station upgrades modify)
  MaxQueueSize    : byte (1-4)

CraftQueueElement (IBufferElementData, Ghost:All, InternalBufferCapacity=4)
  RecipeId         : int
  RequestingPlayer : Entity
  CraftTimeTotal   : float
  CraftTimeElapsed : float
  State            : CraftState (Queued/InProgress/Complete/Failed)
  RandomSeed       : uint

CraftOutputElement (IBufferElementData, Ghost:All, InternalBufferCapacity=4)
  RecipeId           : int
  OutputItemTypeId   : int
  OutputQuantity     : int
  OutputType         : byte (RecipeOutputType)
  OutputResourceType : byte (ResourceType)
  ForPlayer          : Entity
```

### Transient Request Buffers (on Station, NOT ghost-replicated)

**File:** `Assets/Scripts/Crafting/Components/CraftRequestComponents.cs`

```
CraftRequest        (IBufferElementData, Capacity=2) — "start crafting recipe X"
CollectCraftRequest (IBufferElementData, Capacity=2) — "collect output at index Y"
CancelCraftRequest  (IBufferElementData, Capacity=1) — "cancel queued craft at index Z"
```

### On Player Entity (minimal -- child entity pattern)

**File:** `Assets/Scripts/Crafting/Components/PlayerCraftingComponents.cs`

```
CraftingKnowledgeLink (IComponentData, Ghost:AllPredicted) — 8 bytes on player
  KnowledgeEntity : Entity

--- On CHILD entity (not player) ---
CraftingKnowledgeTag (IComponentData)
KnownRecipeElement (IBufferElementData, Ghost:AllPredicted, InternalBufferCapacity=16)
  RecipeId : int
```

**Archetype impact:** Only `CraftingKnowledgeLink` (8 bytes) added to player. Follows `TargetingModuleLink` child entity pattern.

### RPC Definitions

**File:** `Assets/Scripts/Crafting/Components/CraftingRpcs.cs`

```
CraftRequestRpc  (IRpcCommand) : RecipeId, StationGhostId
CollectCraftRpc  (IRpcCommand) : OutputIndex, StationGhostId
CancelCraftRpc   (IRpcCommand) : QueueIndex, StationGhostId
```

### Registry Singleton

**File:** `Assets/Scripts/Crafting/Components/RecipeRegistryManaged.cs`

Managed singleton: `RecipeDatabaseSO` + `Dictionary<int, RecipeDefinitionSO>` + `NativeHashMap<int, RecipeRegistryEntry>` (blittable for Burst).

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Local):
  RecipeRegistryBootstrapSystem             — loads RecipeDatabaseSO, builds registry (runs once)

SimulationSystemGroup (Server only):
  [after StationSessionSystem]
  CraftRpcReceiveSystem                     — receives RPCs, writes to station buffers
  CraftValidationSystem                     — validates requests, consumes ingredients, enqueues
  CraftQueueProcessingSystem (Burst ISystem) — ticks CraftQueueElement timers
  CraftOutputGenerationSystem               — creates outputs from completed crafts
  CraftOutputCollectionSystem               — awards outputs to players on collect
  CraftCancellationSystem                   — cancels queued crafts, refunds ingredients
  RecipeUnlockSystem                        — processes recipe unlock requests

PresentationSystemGroup (Client|Local):
  CraftingUIBridgeSystem                    — watches StationSessionState + CraftingStation → UI
```

### Key System Details

**CraftValidationSystem** (managed SystemBase, Server only):
1. Reads `CraftRequest` buffer on each station
2. Validates: player in session, recipe exists, station type/tier match, player knows recipe, has ingredients + currency, queue not full
3. Consumes ingredients atomically (decrement `InventoryItem.Quantity`, write negative `CurrencyTransaction`)
4. Enqueues `CraftQueueElement` with `State=Queued`
5. Clears `CraftRequest` buffer

**CraftQueueProcessingSystem** (Burst ISystem, Server only):
1. For each station: advance first `Queued` to `InProgress`
2. Tick `CraftTimeElapsed += dt * SpeedMultiplier`
3. When elapsed >= total: transition to `Complete`

**CraftOutputCollectionSystem** (managed SystemBase, Server only):
- Resource output → add to player's `InventoryItem` buffer
- Item output with `RollAffixes` → `AffixRollSystem.RollAffixes()` + create entity via ECB (same as `LootSpawnSystem`)
- Currency output → `CurrencyTransaction` buffer

**CraftCancellationSystem**: Only cancels `Queued` state (not InProgress) to prevent timing exploits. Refunds ingredients.

---

## Authoring

### CraftingStationAuthoring

**File:** `Assets/Scripts/Crafting/Authoring/CraftingStationAuthoring.cs`

```
[AddComponentMenu("DIG/Crafting/Crafting Station")]
```

- Fields: StationType, StationTier [Range(1,5)], SpeedMultiplier [Range(0.5,3)], MaxQueueSize [Range(1,4)]
- Baker adds: `CraftingStation` + all 5 buffers
- Place alongside `StationAuthoring` + `InteractableAuthoring`

### CraftingKnowledgeAuthoring

**File:** `Assets/Scripts/Crafting/Authoring/CraftingKnowledgeAuthoring.cs`

```
[AddComponentMenu("DIG/Crafting/Crafting Knowledge")]
```

- Field: `int[] StarterRecipeIds`
- Baker creates child entity with `CraftingKnowledgeTag` + `KnownRecipeElement` buffer
- Links via `CraftingKnowledgeLink` on player (8 bytes)

---

## UI Bridge

**File:** `Assets/Scripts/Crafting/Bridges/CraftingUIBridgeSystem.cs`
- Managed SystemBase, PresentationSystemGroup, Client|Local
- Watches local player's `StationSessionState` -- when at station with `CraftingStation`, opens crafting UI
- Reads ghost-replicated `CraftQueueElement` + `CraftOutputElement` for progress display

**File:** `Assets/Scripts/Crafting/Bridges/CraftingUILink.cs`
- MonoBehaviour on station GO, static registry by SessionID
- `OpenCraftingUI()` / `CloseCraftingUI()` / `RefreshQueue()` / `RefreshOutputs()`

**File:** `Assets/Scripts/Crafting/Bridges/CraftingEventQueue.cs`
- Static queue for UI feedback: CraftStarted, CraftCompleted, CraftFailed, InsufficientIngredients, etc.

---

## Editor Tooling

### CraftingWorkstationWindow

**File:** `Assets/Editor/CraftingWorkstation/CraftingWorkstationWindow.cs`
- Menu: `DIG/Crafting Workstation`
- Sidebar + `ICraftingModule` pattern

### Modules

| Module | File | Purpose |
|--------|------|---------|
| Recipe Editor | `Modules/RecipeEditorModule.cs` | List/search/filter recipes, edit ingredients/outputs inline |
| Balance Sim | `Modules/BalanceSimulatorModule.cs` | Monte Carlo resource sink analysis |
| Station Config | `Modules/StationConfigModule.cs` | Scene station list, recipe availability per station |
| Live Debug | `Modules/LiveDebugModule.cs` | Play-mode: station queues, progress bars, output buffers |

### RecipeDefinitionSOEditor

**File:** `Assets/Scripts/Crafting/Editor/RecipeDefinitionSOEditor.cs`
- Custom inspector: ingredient slots, output preview, cost summary, validation

---

## Multiplayer

- **Server-authoritative**: All mutation on server only (validate, consume, tick, generate, award)
- **Ghost-replicated**: `CraftingStation`, `CraftQueueElement`, `CraftOutputElement` are `Ghost:All`
- **RPC communication**: Client sends `CraftRequestRpc`/`CollectCraftRpc`/`CancelCraftRpc`
- **Concurrent stations**: `InteractionSession.AllowConcurrentUsers` controls multi-player access
- **Duplication prevention**: Ingredients consumed atomically before queue entry. Cancel only for Queued state.

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `CraftingKnowledgeLink` | 8 bytes | Player entity |
| KnownRecipeElement buffer | 0 bytes | Child entity (via link) |
| Station components | N/A | Station entities (no archetype limit) |
| **Total on player** | **8 bytes** | |

---

## File Summary

### New Files (29)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Crafting/Definitions/RecipeDefinitionSO.cs` | ScriptableObject |
| 2 | `Assets/Scripts/Crafting/Definitions/RecipeDatabaseSO.cs` | ScriptableObject |
| 3 | `Assets/Scripts/Crafting/Definitions/RecipeStructs.cs` | Enums + structs |
| 4 | `Assets/Scripts/Crafting/Components/CraftingStationComponents.cs` | Station ECS |
| 5 | `Assets/Scripts/Crafting/Components/CraftRequestComponents.cs` | Request buffers |
| 6 | `Assets/Scripts/Crafting/Components/PlayerCraftingComponents.cs` | Player link + child |
| 7 | `Assets/Scripts/Crafting/Components/RecipeRegistryManaged.cs` | Registry singleton |
| 8 | `Assets/Scripts/Crafting/Components/CraftingRpcs.cs` | RPC definitions |
| 9 | `Assets/Scripts/Crafting/Systems/RecipeRegistryBootstrapSystem.cs` | Bootstrap |
| 10 | `Assets/Scripts/Crafting/Systems/CraftRpcReceiveSystem.cs` | Server RPC handler |
| 11 | `Assets/Scripts/Crafting/Systems/CraftValidationSystem.cs` | Validation + consume |
| 12 | `Assets/Scripts/Crafting/Systems/CraftQueueProcessingSystem.cs` | Burst timer |
| 13 | `Assets/Scripts/Crafting/Systems/CraftOutputGenerationSystem.cs` | Output creation |
| 14 | `Assets/Scripts/Crafting/Systems/CraftOutputCollectionSystem.cs` | Award to player |
| 15 | `Assets/Scripts/Crafting/Systems/CraftCancellationSystem.cs` | Cancel + refund |
| 16 | `Assets/Scripts/Crafting/Systems/RecipeUnlockSystem.cs` | Recipe learning |
| 17 | `Assets/Scripts/Crafting/Authoring/CraftingStationAuthoring.cs` | Station baker |
| 18 | `Assets/Scripts/Crafting/Authoring/CraftingKnowledgeAuthoring.cs` | Player baker |
| 19 | `Assets/Scripts/Crafting/Bridges/CraftingUIBridgeSystem.cs` | UI bridge |
| 20 | `Assets/Scripts/Crafting/Bridges/CraftingUILink.cs` | MonoBehaviour bridge |
| 21 | `Assets/Scripts/Crafting/Bridges/CraftingEventQueue.cs` | Static event queue |
| 22 | `Assets/Scripts/Crafting/Editor/RecipeDefinitionSOEditor.cs` | Custom inspector |
| 23 | `Assets/Scripts/Crafting/DIG.Crafting.asmdef` | Assembly def |
| 24 | `Assets/Editor/CraftingWorkstation/CraftingWorkstationWindow.cs` | Editor window |
| 25 | `Assets/Editor/CraftingWorkstation/ICraftingModule.cs` | Module interface |
| 26 | `Assets/Editor/CraftingWorkstation/Modules/RecipeEditorModule.cs` | Recipe editor |
| 27 | `Assets/Editor/CraftingWorkstation/Modules/BalanceSimulatorModule.cs` | Balance sim |
| 28 | `Assets/Editor/CraftingWorkstation/Modules/StationConfigModule.cs` | Station config |
| 29 | `Assets/Editor/CraftingWorkstation/Modules/LiveDebugModule.cs` | Debug module |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | Player prefab (Warrok_Server) | Add `CraftingKnowledgeAuthoring` |
| 2 | `InteractionSetupWizard.cs` | Update "Crafting Station" preset to add `CraftingStationAuthoring` |

---

## Cross-EPIC Integration

- **Quest rewards → Recipe unlocks**: `QuestRewardSystem` (EPIC 16.12) writes `RecipeUnlockRequest` → `RecipeUnlockSystem` processes
- **Crafting → Quest objectives**: `CraftQuestEventEmitterSystem` (EPIC 16.12) reads completed `CraftOutputElement` → emits `QuestEvent(Craft, recipeId)`
- **Level-gated recipes**: `RecipeUnlockCondition.PlayerLevel` checks `CharacterAttributes.Level` (EPIC 16.14)

---

## Verification Checklist

- [ ] Create RecipeDefinitionSO in editor, assign to RecipeDatabaseSO
- [ ] Place CraftingStationAuthoring on station in scene alongside StationAuthoring
- [ ] Enter play mode, interact with station → crafting UI opens
- [ ] Select recipe with sufficient ingredients → craft starts, queue element appears
- [ ] Timer ticks → progress bar fills → State transitions to Complete
- [ ] Collect output → item appears in inventory / resource added to buffer
- [ ] Insufficient ingredients → CraftingEventQueue reports failure, no consumption
- [ ] Cancel queued craft → ingredients refunded to InventoryItem buffer
- [ ] Station tier too low → craft rejected
- [ ] Crafted equipment with RollAffixes → random affixes applied
- [ ] Multiple recipes queued → processed in order
- [ ] Queue full → new craft rejected
- [ ] Crafting Workstation: recipe editor shows all recipes with filters
- [ ] Crafting Workstation: balance sim shows resource sink analysis
- [ ] Crafting Workstation: live debug shows station queues with progress bars
- [ ] Multiplayer: two players at same station have independent queues
- [ ] Multiplayer: CraftRequestRpc → server validates → ghost-replicated queue updates
- [ ] Multiplayer: duplication prevention (atomic ingredient consumption)
- [ ] Player archetype: only +8 bytes (CraftingKnowledgeLink)
