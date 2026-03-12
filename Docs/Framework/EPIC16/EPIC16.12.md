# EPIC 16.12: Quest & Objective System

**Status:** PLANNED
**Priority:** High (Core Gameplay Loop)
**Dependencies:**
- `InteractionCompleteEvent` IComponentData (existing -- `DIG.Interaction`, EPIC 16.1)
- `InteractionVerb.Talk` enum value (existing -- `DIG.Interaction`, reserved for NPC talk)
- `ProximityZoneOccupant` IBufferElementData (existing -- `DIG.Interaction`, zone occupancy)
- `DeathState` / `DiedEvent` IComponentData (existing -- `Player.Components`, death events)
- `PickupEvent` IComponentData (existing -- `DIG.Items`, EPIC 16.6)
- `CurrencyInventory` IComponentData (existing -- `DIG.Economy`, EPIC 16.6)
- `InventoryItem` IBufferElementData (existing -- `DIG.Shared`, resource inventory)
- `ItemRegistryBootstrapSystem` (existing -- bootstrap pattern reference)
- `LootTableRegistryBootstrapSystem` (existing -- bootstrap pattern reference)
- `CombatUIBridgeSystem` (existing -- managed UI bridge pattern reference)
- `CombatUIRegistry` (existing -- static registry pattern reference)
- `DamageVisualQueue` (existing -- static queue bridge pattern reference)
- `AIWorkstationWindow` (existing -- Workstation editor pattern reference)
- `EncounterTriggerSystem` (existing -- AI encounter wave system)
- `CombatResultEvent` (existing -- combat hit event for kill attribution)

**Feature:** A server-authoritative, event-driven quest and objective tracking system that decouples quest logic from all game systems via generic QuestEvent transient entities. Quest state lives on separate QuestInstance entities (not the player), objectives complete by observing existing game events (kills, interactions, pickups, zones), and designers define quests entirely in ScriptableObjects.

---

## Problem

DIG has a mature interaction framework (EPIC 16.1), combat pipeline, loot system, and economy -- but no way to structure goals, guide player progression, or reward directed gameplay. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `InteractionCompleteEvent` fires on interaction completion | No system observes interactions for quest objectives |
| `InteractionVerb.Talk` (value=6) reserved for NPC talk | No dialogue or quest giver system uses it |
| `ProximityZoneOccupant` tracks zone entry/exit | No objective tracks "reach this area" |
| `DiedEvent` + `KillCredited` on entity death | No system reads kills for quest objectives |
| `PickupEvent` on item pickup | No system tracks "collect N items" objectives |
| `CurrencyInventory` (Gold/Premium/Crafting) | No quest reward pipeline to distribute currency |
| `EncounterTriggerSystem` for combat encounters | No quest triggers or encounter completion tracking |
| `LootTableCondition.MinLevel / MaxLevel` | No quest prerequisites or unlock conditions |
| `AIWorkstationWindow` editor pattern | No quest design/debug tooling |

**The gap:** A player can fight, loot, craft, and explore -- but has no goals, no directed objectives, no narrative structure. There is no "kill 5 wolves" quest, no quest giver NPC, no reward-for-completion pipeline, no quest log UI.

---

## Architecture Overview

```
Designer creates QuestDefinitionSO → QuestDatabaseSO registry
                                         |
                           QuestRegistryBootstrapSystem (loads from Resources/)
                                         |
                                  BlobAsset singleton
                                         |
QuestGiver interaction → QuestAcceptSystem → QuestInstance entity created
                                              (QuestProgress + QuestPlayerLink + ObjectiveProgress buffer)
                                         |
Game events:                             |
  Kill → KillQuestEventEmitterSystem ────┤
  Interact → InteractionQuestEventEmitter ┤──→ QuestEvent transient entities
  Pickup → CollectQuestEventEmitter ──────┤
  Zone → ZoneQuestEventEmitter ───────────┤
  Craft → CraftQuestEventEmitter ─────────┘
                                         |
                           QuestObjectiveEvaluationSystem
                           (matches events to objectives, increments counts)
                                         |
                           QuestCompletionSystem
                           (all objectives done → Completed state)
                                         |
                           QuestRewardSystem
                           (distribute items, currency, recipe unlocks)
                                         |
                           QuestUIBridgeSystem (PresentationGroup)
                           → QuestUIRegistry → IQuestUIProvider → MonoBehaviour UI
```

### Key Design: Event-Driven Decoupling

Existing systems (combat, interaction, loot) do NOT know about quests. Lightweight "emitter" systems translate game events into generic `QuestEvent` transient entities. The evaluation system consumes these without knowing about combat or interactions. **Adding a new objective type requires only one new emitter system -- no changes to existing systems or the evaluator.**

---

## Data Layer (ScriptableObjects)

### QuestDefinitionSO

**File:** `Assets/Scripts/Quest/Definitions/QuestDefinitionSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Quest/Quest Definition")]
```

| Field | Type | Purpose |
|-------|------|---------|
| QuestId | int | Unique identifier |
| DisplayName | string | UI display name |
| Description | string [TextArea] | Quest description text |
| Category | QuestCategory | Main / Side / Daily / Event / Tutorial |
| Objectives[] | ObjectiveDefinition[] | Inline objective data |
| Rewards[] | QuestReward[] | Items, currency, XP, recipe unlocks |
| PrerequisiteQuestIds | int[] | Must be completed first |
| IsRepeatable | bool | Can re-accept after completion |
| TimeLimit | float | 0 = no limit (seconds) |
| AutoComplete | bool | Complete when all objectives done (vs turn-in) |
| TurnInInteractableId | int | NPC to turn in to (0 = auto-complete) |

### ObjectiveDefinition (serializable struct)

| Field | Type | Purpose |
|-------|------|---------|
| ObjectiveId | int | Unique within quest |
| Type | ObjectiveType | Kill / Interact / Collect / ReachZone / Escort / Survive / Craft / Custom |
| TargetId | int | Context-dependent (prefab hash, interactable ID, item type ID, zone ID) |
| RequiredCount | int | How many (kills, items, interactions) |
| Description | string | "Kill 5 Wolves" |
| IsOptional | bool | Optional bonus objective |
| IsHidden | bool | Revealed on discovery |
| UnlockAfterObjectiveId | int | Sequential ordering (0 = immediately available) |

### QuestDatabaseSO

**File:** `Assets/Scripts/Quest/Definitions/QuestDatabaseSO.cs`

- `List<QuestDefinitionSO>` with `Dictionary<int, QuestDefinitionSO>` lookup
- Loaded from `Resources/QuestDatabase` by bootstrap system
- Custom editor with search, category filter, prerequisite graph validation

### Supporting Enums

**File:** `Assets/Scripts/Quest/Definitions/QuestEnums.cs`

- `QuestCategory`: Main(0), Side(1), Daily(2), Event(3), Tutorial(4)
- `ObjectiveType`: Kill(0), Interact(1), Collect(2), ReachZone(3), Escort(4), Survive(5), Craft(6), Custom(7)
- `QuestState`: Available(0), Active(1), Completed(2), Failed(3), TurnedIn(4)
- `ObjectiveState`: Locked(0), Active(1), Completed(2), Failed(3)

---

## ECS Components

### On QuestInstance Entities (NOT on player)

**File:** `Assets/Scripts/Quest/Components/QuestComponents.cs`

```
QuestProgress (IComponentData, Ghost:All)
  QuestId       : int
  State         : QuestState
  TimeRemaining : float
  AcceptedAtTick: uint

QuestPlayerLink (IComponentData, Ghost:All)
  PlayerEntity  : Entity    // Which player owns this quest

ObjectiveProgress (IBufferElementData, Ghost:All, InternalBufferCapacity=8)
  ObjectiveId   : int
  State         : ObjectiveState
  CurrentCount  : int
  RequiredCount : int
```

**Design rationale:** Quest state on separate entities avoids touching the 16KB player archetype. One QuestInstance entity per active quest per player. Entities are ghost-replicated so clients see real-time progress.

### On Player Entity

**File:** `Assets/Scripts/Quest/Components/QuestPlayerComponents.cs`

```
CompletedQuestEntry (IBufferElementData, Ghost:AllPredicted, InternalBufferCapacity=16)
  QuestId        : int
  CompletedAtTick: uint
```

**Archetype impact:** Buffer header ~16 bytes + inline storage 16 * 8 = 128 bytes. Total ~144 bytes. Within budget given previous buffer capacity reductions.

### QuestEvent Transient Entities

**File:** `Assets/Scripts/Quest/Components/QuestEventComponents.cs`

```
QuestEvent (IComponentData)
  EventType    : ObjectiveType    // Kill, Interact, Collect, ReachZone, Craft
  TargetId     : int              // What was killed/interacted/collected
  Count        : int              // How many (usually 1)
  SourcePlayer : Entity           // Who did it
  Position     : float3           // Where it happened
```

**Key decoupling point.** Existing systems don't know about quests. Emitter systems translate game events into these generic entities. Created and destroyed in the same frame.

### Quest Giver

**File:** `Assets/Scripts/Quest/Components/QuestGiverComponents.cs`

```
QuestGiverData (IComponentData)
  AvailableQuestIds : BlobAssetReference<BlobArray<int>>
```

Placed on NPC entities alongside `InteractableAuthoring` with `InteractionVerb.Talk`.

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Local):
  QuestRegistryBootstrapSystem              — loads QuestDatabaseSO, builds blob (runs once)

SimulationSystemGroup (Server|Local):
  --- Event Emitters (before QuestEvaluationGroup) ---
  KillQuestEventEmitterSystem               — DeathState transitions → QuestEvent(Kill, prefabHash)
  InteractionQuestEventEmitterSystem         — InteractionCompleteEvent → QuestEvent(Interact, id)
  CollectQuestEventEmitterSystem             — PickupEvent → QuestEvent(Collect, itemTypeId)
  ZoneQuestEventEmitterSystem               — ProximityZoneOccupant → QuestEvent(ReachZone, zoneId)
  CraftQuestEventEmitterSystem              — CraftOutputElement → QuestEvent(Craft, recipeId)

  --- Quest Logic Group ---
  QuestAcceptSystem                         — InteractionCompleteEvent + QuestGiverData → create instance
  QuestPrerequisiteSystem                   — checks prerequisites, gates quest availability
  QuestObjectiveEvaluationSystem            — matches QuestEvent to ObjectiveProgress
  QuestCompletionSystem                     — all objectives done → Completed state
  QuestRewardSystem                         — distributes rewards (items, currency, recipe unlocks)
  QuestTimerSystem                          — ticks TimeRemaining, fails expired quests
  QuestEventCleanupSystem                   — destroys all QuestEvent transient entities

PresentationSystemGroup (Client|Local):
  QuestUIBridgeSystem                       — reads QuestProgress → static registry → UI
```

### Event Emitter Design

Each emitter is a small (~50 line) system that reads ONE existing game event and creates a QuestEvent entity via ECB:

- **KillQuestEventEmitter**: Queries `DiedEvent` transitions, gets prefab ghost type hash as `TargetId`, finds killer from `RecentAttackerElement` or `CombatResultEvent.AttackerEntity`
- **InteractionQuestEventEmitter**: Queries `InteractionCompleteEvent`, reads `Interactable.InteractableID` as `TargetId`
- **CollectQuestEventEmitter**: Queries `PickupEvent`, reads `ItemPickup.ItemTypeId` as `TargetId`
- **ZoneQuestEventEmitter**: Queries `ProximityZoneOccupant` buffer entries with `TimeInZone > threshold`
- **CraftQuestEventEmitter**: Reads completed `CraftOutputElement` entries

### QuestObjectiveEvaluationSystem (Core Matching)

```
For each QuestEvent:
  For each QuestInstance where QuestPlayerLink.PlayerEntity == event.SourcePlayer:
    For each ObjectiveProgress in buffer:
      if objective.State == Active
         && objective.Type == event.EventType
         && objective.TargetId == event.TargetId:
        objective.CurrentCount += event.Count
        if objective.CurrentCount >= objective.RequiredCount:
          objective.State = Completed
          // Unlock dependent objectives (UnlockAfterObjectiveId chain)
```

Uses manual `EntityQuery.ToEntityArray()` + `ToComponentDataArray()` per MEMORY.md SystemAPI.Query issues.

---

## Quest Giver / Accept / Turn-In

Quest givers are **existing Interactable entities** with `InteractionVerb.Talk` and a `QuestGiverData` component.

**File:** `Assets/Scripts/Quest/Authoring/QuestGiverAuthoring.cs`
- Placed on NPC alongside `InteractableAuthoring`
- Lists which `QuestDefinitionSO` assets this NPC offers
- Baker builds `BlobArray<int>` of quest IDs

**Accept flow:** Player interacts with QuestGiver → `QuestAcceptSystem` creates QuestInstance entity with `QuestProgress(Active)` + `QuestPlayerLink` + `ObjectiveProgress` buffer.

**Turn-in flow:** Player interacts with NPC whose `InteractableID == quest.TurnInInteractableId` while quest state == Completed → `QuestRewardSystem` awards rewards, adds `CompletedQuestEntry` to player.

---

## UI Bridge

**File:** `Assets/Scripts/Quest/UI/QuestUIBridgeSystem.cs`

Managed SystemBase in PresentationSystemGroup (Client|Local). Follows CombatUIBridgeSystem pattern:
- Reads all `QuestProgress` + `ObjectiveProgress` for local player (via `QuestPlayerLink`)
- Pushes to `QuestUIRegistry` static singleton
- UI MonoBehaviours implement `IQuestLogProvider`, `IObjectiveTrackerProvider`, `IQuestNotificationProvider`
- Diagnostic warnings if providers are missing

**File:** `Assets/Scripts/Quest/UI/QuestUIRegistry.cs` — static registry
**File:** `Assets/Scripts/Quest/UI/IQuestUIProviders.cs` — provider interfaces

---

## Editor Tooling

### QuestWorkstationWindow

**File:** `Assets/Editor/QuestWorkstation/QuestWorkstationWindow.cs`
- Menu: `DIG/Quest Workstation`
- Sidebar + `IQuestModule` module pattern

### Modules

| Module | File | Purpose |
|--------|------|---------|
| Quest Editor | `Modules/QuestEditorModule.cs` | List/search/filter quests, edit objectives/rewards inline |
| Flow Viewer | `Modules/QuestFlowModule.cs` | Visual prerequisite graph (node layout via Rect math) |
| Live Debug | `Modules/QuestLiveDebugModule.cs` | Play-mode: active quests, progress bars, event log |
| Validator | `Modules/QuestValidatorModule.cs` | Broken refs, unreachable quests, circular prerequisites |

---

## Multiplayer

- **Server-authoritative**: QuestInstance entities created/mutated on server only
- **Ghost-replicated**: `QuestProgress` + `ObjectiveProgress` are `Ghost:All` — clients see real-time progress
- **Per-player**: Each player gets own QuestInstance entities via `QuestPlayerLink`
- **Party sharing**: Future extension — `QuestEvent.SourcePlayer` broadened to party members via lookup

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `CompletedQuestEntry` buffer | ~144 bytes | Player entity |
| Quest progress data | 0 bytes | Separate QuestInstance entities |
| **Total on player** | **~144 bytes** | |

---

## File Summary

### New Files (31)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Quest/Definitions/QuestDefinitionSO.cs` | ScriptableObject |
| 2 | `Assets/Scripts/Quest/Definitions/QuestDatabaseSO.cs` | ScriptableObject |
| 3 | `Assets/Scripts/Quest/Definitions/QuestEnums.cs` | Enums |
| 4 | `Assets/Scripts/Quest/Components/QuestComponents.cs` | ECS components |
| 5 | `Assets/Scripts/Quest/Components/QuestPlayerComponents.cs` | Player buffer |
| 6 | `Assets/Scripts/Quest/Components/QuestEventComponents.cs` | Transient events |
| 7 | `Assets/Scripts/Quest/Components/QuestGiverComponents.cs` | NPC components |
| 8 | `Assets/Scripts/Quest/Systems/QuestRegistryBootstrapSystem.cs` | Bootstrap |
| 9 | `Assets/Scripts/Quest/Systems/Emitters/KillQuestEventEmitterSystem.cs` | Emitter |
| 10 | `Assets/Scripts/Quest/Systems/Emitters/InteractionQuestEventEmitterSystem.cs` | Emitter |
| 11 | `Assets/Scripts/Quest/Systems/Emitters/CollectQuestEventEmitterSystem.cs` | Emitter |
| 12 | `Assets/Scripts/Quest/Systems/Emitters/ZoneQuestEventEmitterSystem.cs` | Emitter |
| 13 | `Assets/Scripts/Quest/Systems/Emitters/CraftQuestEventEmitterSystem.cs` | Emitter |
| 14 | `Assets/Scripts/Quest/Systems/QuestAcceptSystem.cs` | Quest accept |
| 15 | `Assets/Scripts/Quest/Systems/QuestPrerequisiteSystem.cs` | State machine |
| 16 | `Assets/Scripts/Quest/Systems/QuestObjectiveEvaluationSystem.cs` | Core matching |
| 17 | `Assets/Scripts/Quest/Systems/QuestCompletionSystem.cs` | Completion check |
| 18 | `Assets/Scripts/Quest/Systems/QuestRewardSystem.cs` | Reward distribution |
| 19 | `Assets/Scripts/Quest/Systems/QuestTimerSystem.cs` | Time limits |
| 20 | `Assets/Scripts/Quest/Systems/QuestEventCleanupSystem.cs` | Cleanup |
| 21 | `Assets/Scripts/Quest/UI/QuestUIBridgeSystem.cs` | UI bridge |
| 22 | `Assets/Scripts/Quest/UI/QuestUIRegistry.cs` | Static registry |
| 23 | `Assets/Scripts/Quest/UI/IQuestUIProviders.cs` | Interfaces |
| 24 | `Assets/Scripts/Quest/Authoring/QuestGiverAuthoring.cs` | NPC authoring |
| 25 | `Assets/Editor/QuestWorkstation/QuestWorkstationWindow.cs` | Editor window |
| 26 | `Assets/Editor/QuestWorkstation/IQuestModule.cs` | Module interface |
| 27 | `Assets/Editor/QuestWorkstation/Modules/QuestEditorModule.cs` | Editor |
| 28 | `Assets/Editor/QuestWorkstation/Modules/QuestFlowModule.cs` | Flow viewer |
| 29 | `Assets/Editor/QuestWorkstation/Modules/QuestLiveDebugModule.cs` | Debug |
| 30 | `Assets/Editor/QuestWorkstation/Modules/QuestValidatorModule.cs` | Validator |
| 31 | `Assets/Scripts/Quest/DIG.Quest.asmdef` | Assembly def |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | Player prefab (Warrok_Server) | Add `CompletedQuestEntry` buffer via authoring |

---

## Verification Checklist

- [ ] Create QuestDefinitionSO in editor, assign to QuestDatabaseSO
- [ ] Place QuestGiverAuthoring on NPC in scene
- [ ] Enter play mode, interact with NPC → quest accepted notification
- [ ] Kill target enemies → objective progress increments
- [ ] Collect items → collect objective progress increments
- [ ] Enter proximity zone → zone objective completes
- [ ] All objectives complete → rewards distributed (currency, items)
- [ ] Turn-in quest at NPC → CompletedQuestEntry added to player
- [ ] Prerequisite quest not complete → new quest unavailable
- [ ] Timed quest expires → quest fails
- [ ] Optional objectives → quest completes without them
- [ ] Hidden objectives → revealed on first progress
- [ ] Sequential objectives → unlock chain works
- [ ] Quest Workstation: flow viewer shows prerequisite graph
- [ ] Quest Workstation: live debug shows active quests with progress bars
- [ ] Quest Workstation: validator catches broken references
- [ ] Multiplayer: two players have independent quest progress
- [ ] Multiplayer: ghost-replicated progress visible to both clients
- [ ] No new components on player entity (only CompletedQuestEntry buffer)
- [ ] Entities without quest system: zero cost, no system processes them
