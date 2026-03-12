# EPIC 16.16: Dialogue & NPC Framework

**Status:** PLANNED
**Priority:** High (Core Gameplay Loop)
**Dependencies:**
- `InteractionVerb.Talk` enum value=6 (existing -- `DIG.Interaction`, reserved, no consumer)
- `ProximityEffect.Dialogue` enum value=6 (existing -- `DIG.Interaction`, reserved, gizmo color defined, no consumer)
- `InteractionCompleteEvent` IComponentData (existing -- `DIG.Interaction`, EPIC 16.1)
- `InteractableAuthoring` MonoBehaviour (existing -- `DIG.Interaction`, EPIC 16.1)
- `InteractAbilitySystem` (existing -- `DIG.Interaction`, drives interaction verbs)
- `StationSessionState` IComponentData (existing -- `DIG.Interaction`, Ghost:AllPredicted, player session tracking)
- `StationSessionBridgeSystem` (existing -- managed ECS→UI bridge pattern reference)
- `StationUILink` MonoBehaviour (existing -- static registry pattern reference)
- `AIBrain` / `AIBrainState` IComponentData (existing -- `DIG.AI`, Idle/Patrol/Investigate/Combat/Flee/ReturnHome)
- `AlertState` IComponentData (existing -- `DIG.Aggro`, EPIC 15.33, 5-level IDLE→COMBAT)
- `TriggerActionType.PlayDialogue` = 7 (existing -- `DIG.AI`, defined in EncounterTrigger.cs, **STUB: not implemented**)
- `EncounterTriggerSystem` (existing -- `DIG.AI`, processes encounter triggers, PlayDialogue case is empty)
- `AudioBusType.Dialogue` = 3 (existing -- `DIG.Audio`, dedicated mixer bus, priority=200)
- `AudioEmitter` IComponentData (existing -- `DIG.Audio`, spatial audio with priority/occlusion)
- `SoundEventRequest` IComponentData (existing -- `DIG.Aggro`, Position/SourceEntity/Loudness/MaxRange/Category)
- `SoundRadarSystem` (existing -- `DIG.Audio.Accessibility`, subtitle/visual indicators)
- `QuestGiverData` IComponentData (planned -- `DIG.Quest`, EPIC 16.12, quest offer list on NPC)
- `QuestAcceptSystem` (planned -- `DIG.Quest`, EPIC 16.12, creates QuestInstance entities)
- `CurrencyInventory` / `CurrencyTransaction` (existing -- `DIG.Economy`, EPIC 16.6)
- `InventoryItem` IBufferElementData (existing -- `DIG.Shared`, resource inventory)
- `CharacterAttributes.Level` (existing -- `DIG.Combat.Components`, for level-gated dialogue)
- `ProceduralMotionProfile` BakeToBlob() (existing -- `DIG.ProceduralMotion`, BlobAsset pattern reference)
- `AIWorkstationWindow` (existing -- Workstation editor pattern reference)

**Feature:** A server-authoritative, branching dialogue tree system where all conversation state lives on NPC entities (zero new components on player archetype), designers author trees as ScriptableObjects baked to BlobAssets for Burst-compatible traversal, 9 condition types gate branches against quest/inventory/flag state, 11 action types execute game effects (quest accepts, item grants, shop opens), and a bark system provides ambient NPC chatter. Integrates with existing AudioBusType.Dialogue for voice lines and EncounterTrigger.PlayDialogue for boss combat dialogue.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `InteractionVerb.Talk` (=6) | `InteractableComponents.cs` | Reserved | No consumer -- dialogue system will be the consumer |
| `ProximityEffect.Dialogue` (=6) | `InteractableComponents.cs` | Reserved | Gizmo color defined (white 0.2a), no consumer system |
| `InteractableContext` | `InteractableComponents.cs` | Fully implemented | Verb + `FixedString32Bytes ActionNameKey` (localization hook) |
| `InteractionSession` | `StationSessionComponents.cs` | Fully implemented | Panel UI binding for stations/shops |
| `StationSessionState` | `StationSessionComponents.cs` | Ghost:AllPredicted | Tracks player-station binding, reusable for dialogue UI |
| `StationSessionBridgeSystem` | `StationSessionBridgeSystem.cs` | Fully implemented | Template for DialogueUIBridgeSystem |
| `TriggerActionType.PlayDialogue` (=7) | `EncounterTrigger.cs` | **STUB** | Defined, comment: "will be fully implemented when those subsystems exist" |
| `AudioBusType.Dialogue` (=3) | `AudioBusConfig.cs` | Configured | Dedicated mixer bus, priority=200 (highest) |
| `AudioEmitter` | `AudioEmitter.cs` | Fully implemented | 3D spatial, occlusion raycast, LOD culling |
| `SoundEventRequest` | `SoundEventRequest.cs` | Fully implemented | Transient entity: Position, SourceEntity, Loudness, MaxRange, Category |
| `SoundRadarSystem` | `SoundRadarSystem.cs` | Fully implemented | Subtitle/visual accessibility system |
| `AIBrain` / `AIBrainState` | `AIBrain.cs` | Fully implemented | Idle/Patrol/Investigate/Combat/Flee/ReturnHome |
| `AlertState` (5-level) | `AlertState.cs` | Fully implemented | IDLE->CURIOUS->SUSPICIOUS->SEARCHING->COMBAT |
| Localization hooks | `InteractionVerbUtility.cs` | Manual switch | Comment: "replace with table lookups when localization framework added" |

### What's Missing

- **No dialogue session system** -- `InteractionVerb.Talk` fires but nothing consumes it
- **No dialogue tree data** -- no ScriptableObject, BlobAsset, or node graph for conversations
- **No branching/condition logic** -- no way to gate dialogue by quest state, level, flags
- **No dialogue actions** -- no way to grant items, accept quests, open shops from dialogue
- **No bark/ambient system** -- NPCs are completely silent in the world
- **No voice line playback** -- `AudioBusType.Dialogue` exists but nothing emits to it
- **No EncounterTrigger.PlayDialogue** -- stub exists but no implementation
- **No dialogue editor** -- no visual node graph, no preview, no validator

---

## Problem

DIG has a complete interaction framework, AI systems with AlertLevel, an economy, a quest system (EPIC 16.12), and a crafting system (EPIC 16.13) -- but no way for NPCs to speak, no branching conversations, no condition-gated dialogue, no dialogue-triggered game actions. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `InteractionVerb.Talk` (value=6) reserved for NPC talk | No system fires when a player talks to an NPC |
| `ProximityEffect.Dialogue` (value=6) reserved | No proximity-triggered greeting system |
| `InteractableAuthoring` + `InteractAbilitySystem` | No dialogue session initiated from interaction |
| `StationSessionState` + `StationUILink` for panel UI | No dialogue-specific UI session type |
| `QuestGiverData` on NPCs (EPIC 16.12) | No dialogue tree driving quest offer/accept flow |
| `CurrencyInventory` + `CurrencyTransaction` | No dialogue action to give/take currency |
| `InventoryItem` buffer | No dialogue action to give/take items |
| `AlertState` on AI entities | No dialogue condition gating on NPC combat state |
| `AudioBusType.Dialogue` (priority=200) | No voice line emission system |
| `TriggerActionType.PlayDialogue` = 7 | Stub -- no implementation |
| `AIWorkstationWindow` editor pattern | No dialogue design or debug tooling |

**The gap:** A player can fight beside NPCs, pick up their loot, and accept quests via raw interaction events -- but NPCs are silent. There is no "Talk to the blacksmith", no "Yes I will help you", no merchant greeting, no branching story beat, no NPC-gated shop opening via conversation.

---

## Architecture Overview

```
Designer creates DialogueTreeSO -> DialogueDatabaseSO registry
                                         |
                         DialogueBootstrapSystem (InitializationSystemGroup)
                         |-- loads from Resources/
                         |-- builds BlobAssets via BlobBuilder (ProceduralMotionProfile pattern)
                         +-- creates DialogueRegistrySingleton
                                         |
Player uses Talk verb -> InteractAbilitySystem fires InteractionCompleteEvent
                                         |
                         DialogueInitiationSystem
                         |-- checks InteractionCompleteEvent + DialogueSpeakerData on NPC
                         |-- evaluates ContextRules for tree selection
                         +-- writes DialogueSessionState(Active) on NPC entity
                                         |
                         DialogueConditionSystem
                         |-- evaluates 9 condition types against live ECS state
                         +-- writes ValidChoicesMask (byte bitmask, up to 8 choices)
                                         |
                         DialogueUIBridgeSystem (PresentationGroup, Client)
                         +-- reads DialogueSessionState -> DialogueUIRegistry -> IDialogueUIProvider
                                         |
Player selects choice -> DialogueChoiceRpc (client -> server)
                                         |
                         DialogueAdvanceSystem (Server only)
                         |-- validates choice against ValidChoicesMask
                         |-- advances CurrentNodeId
                         |-- Condition nodes: evaluate + branch (invisible to UI)
                         |-- Action nodes: dispatch to DialogueActionSystem
                         |-- Random nodes: weighted selection
                         |-- Hub nodes: return-to anchor
                         +-- End nodes: dispatch to DialogueEndSystem
                                         |
                         DialogueActionSystem (Server only)
                         |-- AcceptQuest     -> QuestAcceptRequest transient entity
                         |-- GiveItem        -> InventoryItem buffer write
                         |-- TakeItem        -> InventoryItem buffer decrement
                         |-- GiveCurrency    -> CurrencyTransaction buffer write
                         |-- TakeCurrency    -> CurrencyTransaction buffer write
                         |-- SetFlag         -> DialogueFlag buffer on NPC
                         |-- ClearFlag       -> remove from DialogueFlag buffer
                         |-- OpenShop        -> StationSession on merchant entity
                         |-- OpenCrafting    -> StationSession on crafting station
                         |-- TriggerEncounter -> EncounterTriggerRequest entity
                         +-- PlayVoiceLine   -> SoundEventRequest on NPC
                                         |
                         DialogueEndSystem (Server only)
                         +-- clears DialogueSessionState.IsActive, releases session
                                         |
Parallel: BarkSystem (Client|Local)
  +-- reads BarkCollectionSO, emits random ambient lines on proximity/timer
```

### Key Design: NPC-Side State, Player-Side Input

All dialogue state (`DialogueSessionState`, `DialogueFlag` buffer) lives on the NPC entity. The player entity carries nothing new beyond the interaction entry point already established by EPIC 16.1. This respects the 16KB player archetype limit unconditionally.

Branching logic is server-authoritative. The client sends only a choice index (`DialogueChoiceRpc`). The server validates the choice against `ValidChoicesMask`, executes any actions, advances the node ID. Ghost replication of `DialogueSessionState` provides responsive UI display.

---

## Data Layer (ScriptableObjects)

### DialogueTreeSO

**File:** `Assets/Scripts/Dialogue/Definitions/DialogueTreeSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Dialogue/Dialogue Tree")]
```

| Field | Type | Purpose |
|-------|------|---------|
| TreeId | int | Unique identifier, referenced by DialogueSpeakerData |
| DisplayName | string | Editor label only |
| StartNodeId | int | Entry point node (typically Speech or Random) |
| Nodes | DialogueNode[] | All nodes in this tree, inline serialized |
| NodeEditorPositions | Vector2[] | Parallel array, editor-only layout data (never baked) |

**`BakeToBlob()`** method: builds `BlobAssetReference<DialogueTreeBlob>` using BlobBuilder (same pattern as `ProceduralMotionProfile.BakeToBlob()`).

### DialogueNode (serializable struct)

**File:** `Assets/Scripts/Dialogue/Definitions/DialogueStructs.cs`

| Field | Type | Purpose |
|-------|------|---------|
| NodeId | int | Unique within tree |
| NodeType | DialogueNodeType | Speech/PlayerChoice/Condition/Action/Random/Hub/End |
| SpeakerName | string | Localization key for speaker label |
| Text | string | Localization key for dialogue line |
| AudioClipPath | string | Addressable path for VO clip (empty = no voice) |
| Duration | float | Auto-advance seconds (0 = wait for input) |
| Choices | DialogueChoice[] | For PlayerChoice nodes |
| Actions | DialogueAction[] | For Action nodes (executed then NextNodeId advanced) |
| NextNodeId | int | Default next node (Speech/Action/Hub) |
| ConditionType | DialogueConditionType | For Condition nodes |
| ConditionValue | int | Threshold / quest ID / item type ID / flag index |
| TrueNodeId | int | Condition pass branch |
| FalseNodeId | int | Condition fail branch |
| RandomEntries | RandomNodeEntry[] | For Random nodes: { NodeId, Weight } |
| CameraMode | DialogueCameraMode | None/CloseUp/OverShoulder/Custom |

### DialogueNodeType enum

```
Speech(0)       -- NPC speaks a line, auto-advance or wait for click
PlayerChoice(1) -- present Choices[] to player
Condition(2)    -- server-side branch, no UI display (invisible)
Action(3)       -- execute Actions[] then advance to NextNodeId
Random(4)       -- pick weighted random from RandomEntries pool
Hub(5)          -- return-to anchor for non-linear conversations
End(6)          -- terminate conversation
```

### DialogueChoice (serializable struct)

| Field | Type | Purpose |
|-------|------|---------|
| ChoiceIndex | int | Stable index within node's Choices array |
| Text | string | Localization key |
| NextNodeId | int | Where this choice leads |
| ConditionType | DialogueConditionType | Gates visibility |
| ConditionValue | int | Threshold / ID |

### DialogueConditionType enum (9 types)

```
None(0)              -- always visible / always true
QuestCompleted(1)    -- CompletedQuestEntry contains QuestId
QuestActive(2)       -- active QuestInstance exists for QuestId
HasItem(3)           -- InventoryItem contains ResourceType >= quantity
HasCurrency(4)       -- CurrencyInventory.Gold >= ConditionValue
PlayerLevel(5)       -- CharacterAttributes.Level >= ConditionValue (EPIC 16.14)
DialogueFlag(6)      -- DialogueFlag buffer on NPC contains FlagId
DialogueFlagClear(7) -- DialogueFlag buffer does NOT contain FlagId
Reputation(8)        -- Future EPIC hook (always true if not implemented)
AlertLevelBelow(9)   -- NPC AlertState.CurrentLevel < ConditionValue
```

### DialogueAction (serializable struct)

| Field | Type | Purpose |
|-------|------|---------|
| ActionType | DialogueActionType | Which action to run |
| IntValue | int | Quest ID / Item type ID / Amount / Flag ID |
| IntValue2 | int | Secondary value (quantity, station ghost ID) |

### DialogueActionType enum (11 types)

```
AcceptQuest(0)       -- QuestAcceptRequest with IntValue=QuestId
GiveItem(1)          -- add IntValue=ResourceType, IntValue2=Quantity to InventoryItem
TakeItem(2)          -- remove from InventoryItem, validates before executing
GiveCurrency(3)      -- positive CurrencyTransaction, IntValue=GoldAmount
TakeCurrency(4)      -- negative CurrencyTransaction, validates balance
SetFlag(5)           -- append DialogueFlag(IntValue) on NPC entity
ClearFlag(6)         -- remove DialogueFlag(IntValue) from NPC buffer
OpenShop(7)          -- trigger StationSession on merchant entity
OpenCrafting(8)      -- trigger StationSession on crafting station
TriggerEncounter(9)  -- write EncounterTriggerRequest with IntValue=EncounterId
PlayVoiceLine(10)    -- write SoundEventRequest on NPC with AudioBusType.Dialogue
```

### DialogueCameraMode enum

```
None(0)         -- no camera change
CloseUp(1)      -- zoom to NPC face (offset from NPC LocalToWorld)
OverShoulder(2) -- player-to-NPC over-shoulder angle
Custom(3)       -- IntValue references a CameraAnchor entity in scene
```

### DialogueDatabaseSO

**File:** `Assets/Scripts/Dialogue/Definitions/DialogueDatabaseSO.cs`

Same pattern as `ItemDatabaseSO` and `QuestDatabaseSO`:
- `List<DialogueTreeSO>` with `Dictionary<int, DialogueTreeSO>` lookup
- Loaded from `Resources/DialogueDatabase`
- Custom editor with search, tree listing, orphan detection

### BarkCollectionSO

**File:** `Assets/Scripts/Dialogue/Definitions/BarkCollectionSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Dialogue/Bark Collection")]
```

| Field | Type | Purpose |
|-------|------|---------|
| BarkId | int | Unique identifier |
| Category | BarkCategory enum | Greeting/Idle/Combat/Flee/Death/Trade/Alert |
| Lines | BarkLine[] | Pool of random lines |
| Cooldown | float | Min seconds between same bark (default 30) |
| MaxRange | float | Audio/text display range (default 10m) |
| RequiresLineOfSight | bool | Only bark when player can see NPC |

```
BarkLine: { Text(string locKey), AudioClipPath(string), Weight(float), ConditionType, ConditionValue }
```

---

## BlobAsset Format (Burst-Compatible)

**File:** `Assets/Scripts/Dialogue/Data/DialogueBlobs.cs`

```
DialogueTreeBlob
  TreeId         : int
  StartNodeId    : int
  Nodes          : BlobArray<DialogueNodeBlob>
  NodeIdToIndex  : BlobHashMap<int, int>    // NodeId -> array index for O(1) lookup

DialogueNodeBlob
  NodeId         : int
  NodeType       : byte (DialogueNodeType)
  NextNodeId     : int
  SpeakerNameHash: int                      // hash of localization key for Burst comparison
  TextHash       : int                      // hash of localization key
  Duration       : float
  CameraMode     : byte
  ConditionType  : byte
  ConditionValue : int
  TrueNodeId     : int
  FalseNodeId    : int
  ChoiceStart    : int                      // index into flat Choices BlobArray
  ChoiceCount    : byte
  ActionStart    : int                      // index into flat Actions BlobArray
  ActionCount    : byte
  RandomStart    : int                      // index into flat RandomEntries BlobArray
  RandomCount    : byte

DialogueChoiceBlob
  ChoiceIndex    : int
  NextNodeId     : int
  ConditionType  : byte
  ConditionValue : int

DialogueActionBlob
  ActionType     : byte
  IntValue       : int
  IntValue2      : int

DialogueRandomEntryBlob
  NodeId         : int
  Weight         : float
```

`DialogueBootstrapSystem` builds a flat `BlobArray` of all trees. Per-tree node arrays are sub-ranges indexed by `TreeStart` + `TreeNodeCount` in a `DialogueTreeIndexBlob`. This allows Burst-compiled `DialogueAdvanceSystem` to traverse nodes without managed references.

---

## ECS Components

### On NPC Entities

**File:** `Assets/Scripts/Dialogue/Components/DialogueComponents.cs`

```
DialogueSpeakerData (IComponentData)
  DefaultTreeId         : int
  GreetingText          : FixedString64Bytes   // localization key for proximity bark
  ContextRules          : BlobAssetReference<BlobArray<DialogueContextEntry>>
  BarkCollectionId      : int                  // 0 = no barks

DialogueContextEntry (in BlobArray)
  ConditionType         : byte
  ConditionValue        : int
  TreeId                : int

DialogueSessionState (IComponentData, Ghost:AllPredicted)
  IsActive              : bool
  CurrentNodeId         : int
  InteractingPlayer     : Entity
  SessionStartTick      : uint
  CurrentTreeId         : int
  ValidChoicesMask      : byte    // bitmask of valid choices (set by ConditionSystem)

DialogueFlag (IBufferElementData, InternalBufferCapacity=8)
  FlagId                : int
  SetAtTick             : uint
```

**Zero components on player entity.** `DialogueSessionState` is Ghost:AllPredicted for responsive client UI. Server is authoritative for all mutations.

### Transient RPCs

**File:** `Assets/Scripts/Dialogue/Components/DialogueRpcs.cs`

```
DialogueChoiceRpc (IRpcCommand)
  NpcGhostId    : int     // which NPC
  ChoiceIndex   : int     // index into current node's Choices array
  CurrentNodeId : int     // client's known node (server validates match)

DialogueSkipRpc (IRpcCommand)
  NpcGhostId    : int     // request to close dialogue
```

### Bark Components

**File:** `Assets/Scripts/Dialogue/Components/BarkComponents.cs`

```
BarkEmitter (IComponentData)
  BarkCollectionId     : int
  LastBarkTime         : float
  BarkCooldown         : float

BarkRequest (IComponentData) -- transient entity
  EmitterEntity        : Entity
  LineIndex            : int
  Position             : float3
```

### Registry Singleton

**File:** `Assets/Scripts/Dialogue/Components/DialogueRegistryManaged.cs`

Managed singleton: `DialogueDatabaseSO` + `Dictionary<int, DialogueTreeSO>` + `BlobAssetReference<DialogueRegistryBlob>` for Burst lookup of tree/node data.

### Config Singleton

**File:** `Assets/Scripts/Dialogue/Components/DialogueConfig.cs`

```
DialogueConfig (IComponentData, singleton)
  MaxSessionDurationTicks : uint   // default 1800 (60s at 30Hz)
  MaxFlagsPerNpc          : byte   // default 16
  AutoAdvanceEnabled      : bool   // default true
  BarkProximityRange      : float  // default 8m
  BarkCheckInterval       : float  // default 2s (frame-spread, not every frame)
```

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Local):
  DialogueBootstrapSystem                  -- loads DialogueDatabaseSO + BarkCollectionSOs,
                                             builds BlobAssets, creates singletons (runs once)

SimulationSystemGroup (Server|Local):
  [after InteractAbilitySystem]
  DialogueInitiationSystem                 -- InteractionCompleteEvent + DialogueSpeakerData -> open session
  DialogueRpcReceiveSystem                 -- receives DialogueChoiceRpc / DialogueSkipRpc
  DialogueConditionSystem                  -- evaluates node conditions, writes ValidChoicesMask
  DialogueAdvanceSystem (Burst where possible) -- validates choice, advances CurrentNodeId
  DialogueActionSystem                     -- executes Action nodes (quest, items, currency, flags)
  DialogueEndSystem                        -- End nodes -> clear DialogueSessionState.IsActive
  DialogueCleanupSystem                    -- destroy stale sessions (disconnected player, timeout)
  EncounterDialogueBridgeSystem            -- processes PlayDialogue trigger actions -> bark/dialogue

SimulationSystemGroup (Client|Local):
  BarkTimerSystem                          -- ticks bark cooldowns, creates BarkRequest on proximity

PresentationSystemGroup (Client|Local):
  DialogueUIBridgeSystem                   -- reads DialogueSessionState -> UI registry -> MonoBehaviour
  BarkDisplaySystem                        -- processes BarkRequest -> world-space text bubble + audio
  DialogueCameraSystem                     -- applies CameraMode offsets during active dialogue
```

### DialogueBootstrapSystem

**File:** `Assets/Scripts/Dialogue/Systems/DialogueBootstrapSystem.cs`

- Loads `DialogueDatabaseSO` from `Resources/DialogueDatabase`
- Loads all `BarkCollectionSO` from `Resources/BarkCollections/`
- Builds `BlobAssetReference<DialogueRegistryBlob>` via BlobBuilder (follows `ProceduralMotionProfile.BakeToBlob()` pattern)
- Creates managed singleton `DialogueRegistryManaged`
- Creates `DialogueConfig` singleton from `DialogueConfigSO`
- `SystemState.Enabled = false` after first update

### DialogueInitiationSystem

**File:** `Assets/Scripts/Dialogue/Systems/DialogueInitiationSystem.cs`

Managed SystemBase, Server|Local. Uses manual `EntityQuery.ToEntityArray()`.

```
For each InteractionCompleteEvent where Verb == InteractionVerb.Talk:
  If target entity has DialogueSpeakerData:
    If DialogueSessionState.IsActive: reject (NPC busy)
    1. Evaluate ContextRules: iterate BlobArray<DialogueContextEntry>,
       first matching condition selects TreeId (DefaultTreeId as fallback)
    2. Write DialogueSessionState:
         IsActive = true, CurrentNodeId = StartNodeId,
         InteractingPlayer = instigator, SessionStartTick = currentTick,
         CurrentTreeId = selectedTreeId
    3. If start node is Condition: evaluate immediately (same frame)
    4. If start node is Action: dispatch to DialogueActionSystem queue
```

### DialogueConditionSystem

**File:** `Assets/Scripts/Dialogue/Systems/DialogueConditionSystem.cs`

Managed SystemBase, Server|Local. Runs before DialogueAdvanceSystem.

For each active `DialogueSessionState`:
1. Read current node from BlobAsset
2. If node is `PlayerChoice`: evaluate each choice's condition against player ECS state
3. Write `ValidChoicesMask` byte (bit N = choice N is available)
4. If node is `Condition`: evaluate node condition, set `CurrentNodeId = TrueNodeId or FalseNodeId`

Condition evaluation reads:
- `QuestCompleted/QuestActive`: query `QuestInstance` entities by `QuestPlayerLink`
- `HasItem`: read `InventoryItem` buffer on player
- `HasCurrency`: read `CurrencyInventory.Gold` on player
- `PlayerLevel`: read `CharacterAttributes.Level` (graceful skip if absent)
- `DialogueFlag/DialogueFlagClear`: read `DialogueFlag` buffer on NPC
- `AlertLevelBelow`: read `AlertState.CurrentLevel` on NPC

### DialogueAdvanceSystem

**File:** `Assets/Scripts/Dialogue/Systems/DialogueAdvanceSystem.cs`

Server only (ServerSimulation | LocalSimulation).

```
For each queued DialogueChoiceRpc:
  1. Resolve NpcGhostId -> entity
  2. Validate: session IsActive, InteractingPlayer == sender,
     CurrentNodeId == rpc.CurrentNodeId (stale prevention)
  3. Validate: ChoiceIndex is set in ValidChoicesMask (rejects spoofed choices)
  4. Write CurrentNodeId = choice.NextNodeId
  5. New node dispatch:
     - Action -> queue to DialogueActionSystem
     - Condition -> evaluate immediately, loop until non-Condition node
     - Random -> pick weighted random from RandomEntries, set CurrentNodeId
     - Hub -> set CurrentNodeId (anchor, UI shows hub text)
     - End -> queue to DialogueEndSystem
     - Speech/PlayerChoice -> ghost replication handles UI update
```

### DialogueActionSystem

**File:** `Assets/Scripts/Dialogue/Systems/DialogueActionSystem.cs`

Managed SystemBase, Server|Local.

| Action | Integration |
|--------|-------------|
| AcceptQuest | Writes `QuestAcceptRequest` transient entity (EPIC 16.12 input) |
| GiveItem | Appends to player `InventoryItem` buffer or increments existing |
| TakeItem | Decrements `InventoryItem`, validates before executing |
| GiveCurrency | Writes positive `CurrencyTransaction` on player |
| TakeCurrency | Writes negative `CurrencyTransaction`, validates balance |
| SetFlag | Appends `DialogueFlag` to NPC buffer if not present |
| ClearFlag | Removes `DialogueFlag` from NPC buffer if present |
| OpenShop | Sets `StationSessionState` on player to enter merchant session |
| OpenCrafting | Sets `StationSessionState` on player to enter crafting session |
| TriggerEncounter | Writes `EncounterTriggerRequest` (existing encounter input) |
| PlayVoiceLine | Writes `SoundEventRequest` on NPC with `AudioBusType.Dialogue` |

Action failures (insufficient currency, no item) write a `DialogueActionFailed` transient entity for UI feedback.

### DialogueEndSystem

**File:** `Assets/Scripts/Dialogue/Systems/DialogueEndSystem.cs`

- Reads End node signals + `DialogueSkipRpc`
- Clears `DialogueSessionState.IsActive`, zeroes `InteractingPlayer`
- Burst ISystem, Server|Local

### DialogueCleanupSystem

**File:** `Assets/Scripts/Dialogue/Systems/DialogueCleanupSystem.cs`

- Queries stale sessions: `IsActive && SessionStartTick + MaxSessionDurationTicks < currentTick`
- Also closes sessions where `InteractingPlayer` entity is destroyed
- Runs OrderLast in SimulationSystemGroup

### EncounterDialogueBridgeSystem

**File:** `Assets/Scripts/Dialogue/Systems/EncounterDialogueBridgeSystem.cs`

- Implements `TriggerActionType.PlayDialogue` (previously stub)
- Reads encounter triggers, creates `BarkRequest` for boss combat yells
- Or initiates full dialogue session if trigger specifies a TreeId

### BarkTimerSystem

**File:** `Assets/Scripts/Dialogue/Systems/BarkTimerSystem.cs`

Client|Local only. Frame-spread using `entityIndex % BarkCheckFrames`.

```
For each BarkEmitter near a player (within BarkProximityRange):
  If cooldown elapsed:
    Select random BarkLine from BarkCollectionSO (weighted, condition-filtered)
    Create BarkRequest transient entity
    Reset cooldown
```

### BarkDisplaySystem

**File:** `Assets/Scripts/Dialogue/Systems/BarkDisplaySystem.cs`

PresentationSystemGroup, Client|Local.

- Reads `BarkRequest` entities
- Spawns world-space text bubble UI (pooled, positioned above NPC head via `LocalToWorld`)
- If `BarkLine.AudioClipPath` is set: creates `SoundEventRequest` with `AudioBusType.Dialogue`
- Auto-fades text after 3 seconds
- Destroys `BarkRequest` entity

### DialogueCameraSystem

**File:** `Assets/Scripts/Dialogue/Systems/DialogueCameraSystem.cs`

PresentationSystemGroup, Client|Local.

- When `DialogueSessionState.IsActive` and current node has `CameraMode != None`:
  - CloseUp: lerp camera toward NPC face position (NPC `LocalToWorld` + face offset)
  - OverShoulder: position camera behind player looking at NPC
  - Custom: read `CameraAnchor` entity transform
- On dialogue end: lerp back to gameplay camera

---

## Authoring

### DialogueSpeakerAuthoring

**File:** `Assets/Scripts/Dialogue/Authoring/DialogueSpeakerAuthoring.cs`

```
[AddComponentMenu("DIG/Dialogue/Dialogue Speaker")]
```

- Fields: `DialogueTreeSO DefaultTree`, `string GreetingLocKey`, `List<DialogueContextRule>`, `BarkCollectionSO BarkCollection`
- Baker adds `DialogueSpeakerData` with BlobRef to context entries
- Baker adds `DialogueSessionState` (IsActive=false)
- Baker adds empty `DialogueFlag` buffer
- Baker adds `BarkEmitter` if BarkCollection is set
- Place alongside `InteractableAuthoring` with `InteractionVerb.Talk`

---

## UI Bridge

**File:** `Assets/Scripts/Dialogue/Bridges/DialogueUIBridgeSystem.cs`

Managed SystemBase, PresentationSystemGroup, Client|Local. Follows `CombatUIBridgeSystem` pattern:
1. Query `DialogueSessionState` where `IsActive && InteractingPlayer == localPlayer`
2. Read current node from `DialogueRegistryManaged`
3. Resolve localization keys via `DialogueLocalization.Resolve()`
4. Filter choices by `ValidChoicesMask`
5. Push `DialogueUIState` to `DialogueUIRegistry` -> `IDialogueUIProvider`

**File:** `Assets/Scripts/Dialogue/Bridges/DialogueUIRegistry.cs` -- static singleton
**File:** `Assets/Scripts/Dialogue/Bridges/IDialogueUIProviders.cs`

```
IDialogueUIProvider
  void OpenDialogue(DialogueUIState state)
  void AdvanceDialogue(DialogueUIState state)
  void CloseDialogue()
  void ShowActionFeedback(DialogueActionFeedback feedback)
```

**File:** `Assets/Scripts/Dialogue/Bridges/DialogueUIState.cs` (plain struct)

```
DialogueUIState
  SpeakerName    : string (resolved)
  BodyText       : string (resolved)
  AudioClipPath  : string
  Choices        : DialogueChoiceUI[] (max 8, pre-filtered by ValidChoicesMask)
  NodeType       : DialogueNodeType
  AutoAdvanceSec : float
  CameraMode     : DialogueCameraMode
```

**File:** `Assets/Scripts/Dialogue/Bridges/DialogueEventQueue.cs` -- static `Queue<DialogueUIEvent>`
**File:** `Assets/Scripts/Dialogue/Bridges/DialogueLocalization.cs`

```csharp
public static class DialogueLocalization
{
    public static string Resolve(string key)
    {
        // Unity Localization package lookup if present.
        // Falls back to returning raw key if no localization installed.
    }
}
```

---

## Config

**File:** `Assets/Scripts/Dialogue/Definitions/DialogueConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Dialogue/Dialogue Config")]
```

| Field | Type | Purpose |
|-------|------|---------|
| MaxSessionDurationTicks | uint | Default 1800 (60s at 30Hz tick rate) |
| MaxFlagsPerNpc | byte | Default 16 |
| AutoAdvanceEnabled | bool | Honor Duration field on Speech nodes |
| BarkProximityRange | float | Default 8m |
| BarkCheckInterval | float | Default 2s |
| BarkCheckFrameSpread | int | Default 10 (frame-slot spread) |

---

## Editor Tooling

### DialogueWorkstationWindow

**File:** `Assets/Editor/DialogueWorkstation/DialogueWorkstationWindow.cs`
- Menu: `DIG/Dialogue Workstation`
- Sidebar + `IDialogueModule` module pattern

### Modules

| Module | File | Purpose |
|--------|------|---------|
| Tree Editor | `Modules/TreeEditorModule.cs` | Visual node graph: colored rects, Bezier connections, drag-to-reposition, zoom+pan |
| Node Inspector | `Modules/NodeInspectorModule.cs` | Edit selected node: text key, audio, condition, actions, camera mode |
| Bark Editor | `Modules/BarkEditorModule.cs` | Edit bark collections: line pool, weights, conditions, cooldowns, audio preview |
| Live Preview | `Modules/LivePreviewModule.cs` | Play-mode: step through tree, resolve conditions against live world |
| Validator | `Modules/ValidatorModule.cs` | 9 error categories (see below) |

### Tree Editor Detail

- Nodes as `GUI.Box` rects with NodeType color coding:
  Speech=blue, PlayerChoice=green, Condition=yellow, Action=orange, Random=purple, Hub=cyan, End=red
- Connections via `Handles.DrawBezier`
- Right-click: Add Node, Delete Node, Set as Start, Duplicate
- Node positions stored in `DialogueTreeSO.NodeEditorPositions` (editor-only, never baked)
- Zoom + pan via scroll wheel + middle mouse

### Validator Checks

| Check | Severity |
|-------|----------|
| Node with no outgoing connection (not End type) | Error |
| Node unreachable from StartNodeId | Warning |
| Choice pointing to missing NodeId | Error |
| Dead-end path (no End node reachable from branch) | Warning |
| Condition referencing QuestId not in QuestDatabaseSO | Warning |
| Condition referencing ItemTypeId not in ItemDatabaseSO | Warning |
| Action AcceptQuest with QuestId not in QuestDatabaseSO | Error |
| Speech node with empty Text key | Warning |
| DialogueFlag FlagId >= DialogueConfig.MaxFlagsPerNpc | Error |

---

## Multiplayer

- **Server-authoritative**: `DialogueSessionState` mutations only on server. Client sends `DialogueChoiceRpc`.
- **Ghost-replicated**: `DialogueSessionState` is `Ghost:AllPredicted` -- interacting client sees responsive node transitions.
- **Single-player UI**: Only the player whose entity matches `InteractingPlayer` sees dialogue.
- **Action validation**: Server re-validates every action (currency, items) before executing. Spoofed choice indices rejected via `ValidChoicesMask`.
- **NPC concurrency**: One session per NPC at a time. `DialogueInitiationSystem` rejects Talk while `IsActive`.
- **Disconnect**: `DialogueCleanupSystem` closes stale sessions.
- **Barks**: Client-only (cosmetic). No server involvement, no ghost replication needed.

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `DialogueSpeakerData` | ~24 bytes | NPC entities only |
| `DialogueSessionState` | ~32 bytes | NPC entities only |
| `DialogueFlag` buffer | ~0 + header | NPC entities only |
| `BarkEmitter` | ~12 bytes | NPC entities only |
| **Total on player entity** | **0 bytes** | None |

No components on player entity. All state on NPC entities.

---

## Integration Points

### Quest System (EPIC 16.12)
- `DialogueConditionType.QuestActive/QuestCompleted` reads quest state
- `DialogueActionType.AcceptQuest` writes `QuestAcceptRequest` transient entity
- Quest giver NPCs have both `DialogueSpeakerAuthoring` and `QuestGiverAuthoring`

### Crafting System (EPIC 16.13)
- `DialogueActionType.OpenCrafting` sets `StationSessionState` on player

### Economy (EPIC 16.6)
- `GiveCurrency/TakeCurrency` write `CurrencyTransaction`, respect atomic pipeline
- `HasCurrency` condition reads `CurrencyInventory.Gold`

### Progression (EPIC 16.14)
- `DialogueConditionType.PlayerLevel` reads `CharacterAttributes.Level`
- Graceful: returns false if EPIC 16.14 not implemented

### AI / Aggro (EPIC 15.33)
- `AlertLevelBelow` condition prevents dialogue with NPCs in combat
- Bark categories (Combat, Flee, Alert) tie to AI state transitions

### Encounter System (existing)
- `TriggerEncounter` action fires encounters from dialogue
- `EncounterDialogueBridgeSystem` implements `PlayDialogue` trigger for boss combat yells

### Audio (existing)
- `PlayVoiceLine` action emits `SoundEventRequest` on `AudioBusType.Dialogue`
- `BarkDisplaySystem` uses same pipeline for ambient bark audio
- `SoundRadarSystem` provides subtitle accessibility for voice lines

---

## File Summary

### New Files (40)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Dialogue/Definitions/DialogueTreeSO.cs` | ScriptableObject |
| 2 | `Assets/Scripts/Dialogue/Definitions/DialogueDatabaseSO.cs` | ScriptableObject |
| 3 | `Assets/Scripts/Dialogue/Definitions/DialogueStructs.cs` | Serializable structs |
| 4 | `Assets/Scripts/Dialogue/Definitions/DialogueEnums.cs` | Enums (7 types) |
| 5 | `Assets/Scripts/Dialogue/Definitions/BarkCollectionSO.cs` | ScriptableObject |
| 6 | `Assets/Scripts/Dialogue/Definitions/DialogueConfigSO.cs` | Config SO |
| 7 | `Assets/Scripts/Dialogue/Data/DialogueBlobs.cs` | BlobAsset structs |
| 8 | `Assets/Scripts/Dialogue/Components/DialogueComponents.cs` | NPC ECS components |
| 9 | `Assets/Scripts/Dialogue/Components/DialogueRpcs.cs` | RPC definitions |
| 10 | `Assets/Scripts/Dialogue/Components/BarkComponents.cs` | Bark ECS components |
| 11 | `Assets/Scripts/Dialogue/Components/DialogueRegistryManaged.cs` | Registry singleton |
| 12 | `Assets/Scripts/Dialogue/Components/DialogueConfig.cs` | Config singleton |
| 13 | `Assets/Scripts/Dialogue/Systems/DialogueBootstrapSystem.cs` | Bootstrap |
| 14 | `Assets/Scripts/Dialogue/Systems/DialogueInitiationSystem.cs` | Session open |
| 15 | `Assets/Scripts/Dialogue/Systems/DialogueRpcReceiveSystem.cs` | RPC handler |
| 16 | `Assets/Scripts/Dialogue/Systems/DialogueConditionSystem.cs` | Condition evaluation |
| 17 | `Assets/Scripts/Dialogue/Systems/DialogueAdvanceSystem.cs` | Node advance |
| 18 | `Assets/Scripts/Dialogue/Systems/DialogueActionSystem.cs` | Action execution |
| 19 | `Assets/Scripts/Dialogue/Systems/DialogueEndSystem.cs` | Session close |
| 20 | `Assets/Scripts/Dialogue/Systems/DialogueCleanupSystem.cs` | Stale session cleanup |
| 21 | `Assets/Scripts/Dialogue/Systems/EncounterDialogueBridgeSystem.cs` | PlayDialogue implementation |
| 22 | `Assets/Scripts/Dialogue/Systems/BarkTimerSystem.cs` | Bark cooldown + proximity check |
| 23 | `Assets/Scripts/Dialogue/Systems/BarkDisplaySystem.cs` | Bark text bubble + audio |
| 24 | `Assets/Scripts/Dialogue/Systems/DialogueCameraSystem.cs` | Dialogue camera modes |
| 25 | `Assets/Scripts/Dialogue/Authoring/DialogueSpeakerAuthoring.cs` | NPC baker |
| 26 | `Assets/Scripts/Dialogue/Bridges/DialogueUIBridgeSystem.cs` | UI bridge |
| 27 | `Assets/Scripts/Dialogue/Bridges/DialogueUIRegistry.cs` | Static registry |
| 28 | `Assets/Scripts/Dialogue/Bridges/IDialogueUIProviders.cs` | Provider interfaces |
| 29 | `Assets/Scripts/Dialogue/Bridges/DialogueEventQueue.cs` | Static event queue |
| 30 | `Assets/Scripts/Dialogue/Bridges/DialogueUIState.cs` | UI data struct |
| 31 | `Assets/Scripts/Dialogue/Bridges/DialogueLocalization.cs` | Localization pass-through |
| 32 | `Assets/Scripts/Dialogue/DIG.Dialogue.asmdef` | Assembly def |
| 33 | `Assets/Editor/DialogueWorkstation/DialogueWorkstationWindow.cs` | Editor window |
| 34 | `Assets/Editor/DialogueWorkstation/IDialogueModule.cs` | Module interface |
| 35 | `Assets/Editor/DialogueWorkstation/Modules/TreeEditorModule.cs` | Visual node graph |
| 36 | `Assets/Editor/DialogueWorkstation/Modules/NodeInspectorModule.cs` | Node property editor |
| 37 | `Assets/Editor/DialogueWorkstation/Modules/BarkEditorModule.cs` | Bark collection editor |
| 38 | `Assets/Editor/DialogueWorkstation/Modules/LivePreviewModule.cs` | Play-mode tester |
| 39 | `Assets/Editor/DialogueWorkstation/Modules/ValidatorModule.cs` | Tree validator |
| 40 | `Assets/Scripts/Dialogue/Editor/DialogueTreeSOEditor.cs` | Custom SO inspector |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | `Assets/Scripts/AI/Systems/EncounterTriggerSystem.cs` | Implement `PlayDialogue` case (was stub) -- dispatch to EncounterDialogueBridgeSystem |
| 2 | NPC prefabs (Merchant, QuestGiver, etc.) | Add `DialogueSpeakerAuthoring` + configure trees |
| 3 | `Assets/Scripts/Quest/Systems/QuestAcceptSystem.cs` | Add handler for `QuestAcceptRequest` transient entities |

---

## Verification Checklist

- [ ] Create `DialogueTreeSO`, add Speech + PlayerChoice nodes, assign to `DialogueDatabaseSO`
- [ ] Place `DialogueSpeakerAuthoring` + `InteractableAuthoring(Talk)` on NPC
- [ ] Talk to NPC -> first node appears in dialogue UI
- [ ] Speech with Duration > 0 -> auto-advances
- [ ] PlayerChoice -> options appear, selection advances to correct node
- [ ] End node -> UI closes, session cleared
- [ ] DialogueSkipRpc -> session closes immediately
- [ ] Condition node: QuestActive -> correct branch based on quest state
- [ ] Condition node: HasCurrency -> fails gracefully when broke
- [ ] Condition node: DialogueFlag -> branch changes after SetFlag action
- [ ] Choice gated by HasItem -> hidden when item absent
- [ ] Choice gated by AlertLevelBelow -> hidden when NPC in combat
- [ ] Action: AcceptQuest -> quest appears in player log
- [ ] Action: GiveItem -> item in InventoryItem buffer
- [ ] Action: TakeItem -> removed; fails gracefully if absent
- [ ] Action: GiveCurrency -> Gold increases
- [ ] Action: TakeCurrency -> Gold deducted; fails if insufficient
- [ ] Action: SetFlag -> DialogueFlag written; subsequent visit takes different branch
- [ ] Action: ClearFlag -> flag removed from NPC buffer
- [ ] Action: OpenShop -> StationSession entered, shop UI opens
- [ ] Action: TriggerEncounter -> encounter fires after dialogue
- [ ] Action: PlayVoiceLine -> SoundEventRequest on AudioBusType.Dialogue
- [ ] Random node -> different selections across play-throughs
- [ ] Hub node -> conversation returns to hub after branches
- [ ] Context tree selection -> NPC uses alternate tree when condition matches
- [ ] Camera: CloseUp mode zooms to NPC face during dialogue
- [ ] Bark: NPC emits random greeting when player approaches
- [ ] Bark: combat bark emits during fight
- [ ] Bark: cooldown prevents spam
- [ ] EncounterTrigger.PlayDialogue -> boss yell text appears during combat phase
- [ ] Cleanup: stale session closed after MaxSessionDurationTicks
- [ ] Multiplayer: only interacting player sees dialogue UI
- [ ] Multiplayer: choice index validated server-side via ValidChoicesMask
- [ ] Multiplayer: DialogueSessionState ghost-replicated, responsive on client
- [ ] Dialogue Workstation: Tree Editor shows colored nodes with Bezier connections
- [ ] Dialogue Workstation: drag repositions nodes (saved to SO)
- [ ] Dialogue Workstation: Node Inspector edits text, audio, conditions, actions
- [ ] Dialogue Workstation: Bark Editor edits bark lines with weight/condition
- [ ] Dialogue Workstation: Validator catches dead-end Speech node (Error)
- [ ] Dialogue Workstation: Validator catches unreachable node (Warning)
- [ ] Dialogue Workstation: Validator catches broken QuestId (Error)
- [ ] Localization: all text stored as keys; Resolve() returns key when no localization present
- [ ] Player archetype: zero new components on player entity
- [ ] NPCs without DialogueSpeakerData: zero cost
