# SETUP GUIDE 16.16: Dialogue & NPC Framework

**Status:** Implemented
**Last Updated:** February 23, 2026
**Requires:** Interaction Framework (EPIC 16.1), Quest System (EPIC 16.12), Crafting System (EPIC 16.13), Economy (EPIC 16.6), Aggro Framework (EPIC 15.33)

This guide covers Unity Editor setup for the dialogue and NPC framework. After setup, NPCs can speak branching dialogue trees with condition-gated choices, execute gameplay actions (quest grants, item trades, shop access), and emit ambient bark chatter when players are nearby.

---

## What Changed

Previously, NPCs had no speech capability. `InteractionVerb.Talk`, `ProximityEffect.Dialogue`, `TriggerActionType.PlayDialogue`, and `AudioBusType.Dialogue` were all reserved stubs with no consumers.

Now:

- **Branching dialogue trees** -- 7 node types (Speech, PlayerChoice, Condition, Action, Random, Hub, End) with visual editor
- **Condition-gated choices** -- 9 condition types gate which choices players see (quest state, inventory, currency, level, flags, alert level)
- **11 gameplay actions** -- dialogue nodes can grant quests, give/take items and currency, set persistent flags, open shops/crafting, trigger encounters, play voice lines
- **Bark system** -- ambient NPC chatter when players are within proximity, with cooldowns, weighted line pools, and condition-based filtering
- **Context rules** -- NPCs can select different dialogue trees based on game state (e.g., different tree after completing a quest)
- **Multiplayer** -- only the interacting player sees the dialogue UI; server validates all choices
- **Dialogue Workstation** -- editor window with visual node graph, node inspector, bark editor, live session debugger, and tree validator
- **Zero player archetype impact** -- all dialogue state lives on NPC entities; players communicate via RPCs

---

## What's Automatic (No Setup Required)

Once ScriptableObjects are created and `DialogueSpeakerAuthoring` is placed on NPCs, all of the following works automatically:

| Feature | How It Works |
|---------|-------------|
| Registry bootstrap | DialogueBootstrapSystem loads DialogueDatabase and BarkCollections from Resources on game start |
| Session initiation | DialogueInitiationSystem detects InteractionCompleteEvent(Talk) + DialogueSpeakerData and opens dialogue |
| Context rule evaluation | First matching context rule selects the tree; falls back to DefaultTree |
| Condition evaluation | DialogueConditionSystem evaluates all 9 condition types each frame and writes ValidChoicesMask |
| Node chaining | DialogueAdvanceSystem chains through invisible nodes (Condition/Random/Action) in a single frame |
| Action dispatch | DialogueActionSystem executes gameplay actions (quest grants, items, flags, etc.) server-side |
| Session cleanup | DialogueCleanupSystem closes stale sessions and handles player disconnects |
| RPC validation | Server validates all player choices against ValidChoicesMask before advancing |
| Bark proximity checks | BarkTimerSystem uses frame-slot spreading to avoid thundering herd |
| Bark display | BarkDisplaySystem picks weighted random lines and pushes to UI + audio |
| Ghost replication | DialogueSessionState (Ghost:All) replicates to all clients for UI visibility |

---

## 1. ScriptableObject Configuration

The dialogue system requires **three** ScriptableObject assets placed in `Assets/Resources/`. The bootstrap system loads them automatically.

### 1.1 Dialogue Database

The central registry of all dialogue trees.

1. Right-click in Project window > **Create > DIG > Dialogue > Dialogue Database**
2. Name it `DialogueDatabase`
3. Place at `Assets/Resources/DialogueDatabase.asset`

The file MUST be named `DialogueDatabase` and MUST be in a `Resources/` folder. DialogueBootstrapSystem loads it via `Resources.Load<DialogueDatabaseSO>("DialogueDatabase")`.

#### Inspector Fields

| Field | Description |
|-------|-------------|
| **Trees** | List of all DialogueTreeSO assets. Drag trees here to register them. Duplicate TreeIds produce a warning. |

### 1.2 Dialogue Config

Global runtime configuration for sessions and barks.

1. Right-click > **Create > DIG > Dialogue > Dialogue Config**
2. Name it `DialogueConfig`
3. Place at `Assets/Resources/DialogueConfig.asset`

If missing, default values are used.

#### Inspector Fields

| Field | Header | Default | Description |
|-------|--------|---------|-------------|
| **Max Session Duration Ticks** | Session | 1800 | Maximum dialogue session length in server ticks (1800 = 60s at 30Hz). Prevents stuck sessions. |
| **Max Flags Per Npc** | Session | 16 | Maximum dialogue flags per NPC entity (4-32). |
| **Auto Advance Enabled** | Session | true | Whether Speech nodes with Duration > 0 automatically advance. |
| **Bark Proximity Range** | Barks | 8.0 | Distance in meters within which NPCs detect players for bark triggers. |
| **Bark Check Interval** | Barks | 2.0 | Seconds between bark proximity checks per NPC (minimum 0.5). |
| **Bark Check Frame Spread** | Barks | 10 | Number of frame slots for spreading bark checks across NPCs (1-30). Higher values = less CPU per frame but slower response. |

### 1.3 Bark Collections

Bark collections are loaded automatically from `Assets/Resources/BarkCollections/`. Any `BarkCollectionSO` assets in this folder are registered at bootstrap.

1. Create the folder `Assets/Resources/BarkCollections/` if it doesn't exist
2. Place BarkCollectionSO assets inside

---

## 2. Creating a Dialogue Tree

### 2.1 Create the Asset

1. Right-click in Project window > **Create > DIG > Dialogue > Dialogue Tree**
2. Name it descriptively (e.g., `Blacksmith_Main`)

### 2.2 Inspector Fields

| Field | Description |
|-------|-------------|
| **Tree Id** | Unique integer identifier. Must be > 0 and unique across all trees. Referenced by DialogueSpeakerData. |
| **Display Name** | Editor-only label for the Workstation and validator. |
| **Start Node Id** | NodeId of the entry point node. Must match a node in the Nodes array. |
| **Nodes** | Array of all dialogue nodes in this tree. |

### 2.3 Node Types

Each node in the Nodes array has a **Node Type** that determines its behavior:

| Node Type | Purpose | Key Fields |
|-----------|---------|------------|
| **Speech** | NPC speaks text to the player | SpeakerName, Text, AudioClipPath, Duration (0=manual advance), NextNodeId |
| **PlayerChoice** | Player selects from available options | Choices[] (each with Text, NextNodeId, optional Condition) |
| **Condition** | Automatic branch based on game state | ConditionType, ConditionValue, TrueNodeId, FalseNodeId |
| **Action** | Execute gameplay effects (invisible to player) | Actions[] (each with ActionType, IntValue, IntValue2), NextNodeId |
| **Random** | Random weighted branch | RandomEntries[] (each with NodeId, Weight) |
| **Hub** | Re-entrant speech node (like Speech but revisitable) | Same as Speech |
| **End** | Closes the dialogue session | No navigation fields needed |

### 2.4 Condition Types

Used on Condition nodes and on individual choices within PlayerChoice nodes:

| Condition Type | Value Meaning | Example |
|----------------|--------------|---------|
| **None** | Always true | N/A |
| **QuestCompleted** | Quest ID | "Show this only after quest 5 is turned in" |
| **QuestActive** | Quest ID | "Show this while quest 3 is in progress" |
| **HasItem** | ResourceType (int) | "Requires 1 Crystal (ResourceType=4)" |
| **HasCurrency** | Gold amount | "Requires 100 gold" |
| **PlayerLevel** | Minimum level | "Only for level 10+ players" |
| **DialogueFlag** | Flag ID | "Show after flag 1 is set on this NPC" |
| **DialogueFlagClear** | Flag ID | "Show only if flag 2 is NOT set" |
| **Reputation** | (Reserved) | Future EPIC -- always true for now |
| **AlertLevelBelow** | Alert level (0-4) | "Only when NPC is not in combat (< 4)" |

### 2.5 Action Types

Used in the Actions array on Action nodes:

| Action Type | IntValue | IntValue2 | Effect |
|-------------|----------|-----------|--------|
| **AcceptQuest** | Quest ID | -- | Creates quest instance for the player |
| **GiveItem** | ResourceType (int) | Quantity | Adds items to player inventory |
| **TakeItem** | ResourceType (int) | Quantity | Removes items from player inventory |
| **GiveCurrency** | Gold amount | -- | Grants gold to the player |
| **TakeCurrency** | Gold amount | -- | Removes gold (validates balance first) |
| **SetFlag** | Flag ID | -- | Sets a persistent dialogue flag on this NPC |
| **ClearFlag** | Flag ID | -- | Clears a dialogue flag from this NPC |
| **OpenShop** | -- | -- | Opens the station/shop UI for this NPC |
| **OpenCrafting** | -- | -- | Opens the crafting station UI for this NPC |
| **TriggerEncounter** | Encounter ID | -- | Creates a PlayDialogueTrigger for the encounter system |
| **PlayVoiceLine** | -- | -- | Plays a sound event at the NPC's position |

### 2.6 Camera Modes

Each node has a **Camera Mode** field:

| Mode | Behavior |
|------|----------|
| **None** | No camera change |
| **CloseUp** | Camera zooms to speaker's face |
| **OverShoulder** | Over-the-shoulder framing |
| **Custom** | Sent as event for custom camera controller handling |

### 2.7 Register the Tree

After creating the tree, add it to the **Dialogue Database**:

1. Open `Assets/Resources/DialogueDatabase.asset`
2. In the **Trees** list, click **+** and drag the new DialogueTreeSO into the slot

---

## 3. Creating a Bark Collection

### 3.1 Create the Asset

1. Right-click > **Create > DIG > Dialogue > Bark Collection**
2. Name it descriptively (e.g., `Guard_Idle_Barks`)
3. Place it in `Assets/Resources/BarkCollections/`

### 3.2 Inspector Fields

| Field | Default | Description |
|-------|---------|-------------|
| **Bark Id** | 0 | Unique identifier. Must be > 0 and unique. Referenced by DialogueSpeakerAuthoring. |
| **Category** | Greeting | Bark category (Greeting, Idle, Combat, Flee, Death, Trade, Alert). For organization. |
| **Lines** | Empty | Pool of bark lines. One is selected randomly (weighted) each trigger. |
| **Cooldown** | 30 | Minimum seconds between barks from this NPC. |
| **Max Range** | 10 | Distance in meters for bark text/audio display. |
| **Requires Line Of Sight** | false | Only bark when player has line of sight to the NPC. |

### 3.3 Bark Lines

Each entry in the **Lines** array:

| Field | Description |
|-------|-------------|
| **Text** | Localization key for the bark text (displayed as-is until localization framework is connected). |
| **Audio Clip Path** | Path to audio clip asset. Leave empty for text-only barks. |
| **Weight** | Selection weight (0.01 - 10). Higher = more likely to be picked. |
| **Condition Type** | Optional condition for this line. Line is skipped if condition fails. |
| **Condition Value** | Value for the condition check. |

---

## 4. NPC Prefab Setup

### 4.1 Prerequisites

The NPC must already have:

- **InteractableAuthoring** with `Verb` set to **Talk** (InteractionVerb.Talk = 6)
- A valid SubScene placement

### 4.2 Add Dialogue Speaker

1. Select the NPC root GameObject
2. Click **Add Component** > search for **Dialogue Speaker** (listed under DIG/Dialogue)
3. Configure:

| Field | Required | Description |
|-------|----------|-------------|
| **Default Tree** | Yes | Drag a DialogueTreeSO here. Used when no context rules match. |
| **Greeting Loc Key** | No | Localization key for proximity greeting text. |
| **Context Rules** | No | Ordered list of conditional tree overrides (see below). |
| **Bark Collection** | No | Drag a BarkCollectionSO here for ambient chatter. Leave empty for no barks. |

### 4.3 Context Rules

Context rules let an NPC switch to a different dialogue tree based on game state. Rules are evaluated in order; the first match wins.

Each rule has:

| Field | Description |
|-------|-------------|
| **Condition Type** | Which condition to check (same types as node conditions). |
| **Condition Value** | The comparison value (quest ID, item type, etc.). |
| **Tree** | The DialogueTreeSO to use when this condition is true. |

**Example:** A quest giver NPC with three context rules:

1. `QuestCompleted`, Value=5, Tree=`Blacksmith_PostQuest` -- "Thank you for your help!"
2. `QuestActive`, Value=5, Tree=`Blacksmith_DuringQuest` -- "Did you find the ore yet?"
3. (No rule) -- Falls back to DefaultTree: `Blacksmith_Intro` -- "I need help finding rare ore."

Rules are evaluated top-to-bottom. Put the most specific conditions first.

### 4.4 What the Baker Creates

At bake time, `DialogueSpeakerAuthoring` creates the following on the NPC entity:

| Component | Purpose |
|-----------|---------|
| `DialogueSpeakerData` | Default tree ID, greeting text, context rules blob reference, bark collection ID |
| `DialogueSessionState` | Session tracking (starts inactive). Ghost:All for client visibility. |
| `DialogueFlag` buffer (capacity 8) | Persistent per-NPC flags set/cleared by dialogue actions |
| `BarkEmitter` (if bark assigned) | Bark cooldown tracking and collection ID |

### 4.5 After Adding

Right-click the SubScene containing the NPC > **Reimport** to rebake. Ghost prefab changes require SubScene reimport.

---

## 5. Encounter Dialogue Integration

Boss encounters can trigger dialogue or barks via `TriggerActionType.PlayDialogue` (= 7).

### 5.1 Setting Up an Encounter Trigger

On a boss entity's `EncounterTriggerDefinition` buffer:

| Field | Value |
|-------|-------|
| **Action Type** | PlayDialogue (7) |
| **Action Param** | Bark collection ID or dialogue tree ID |

The `EncounterTriggerSystem` creates a `PlayDialogueTrigger` transient entity, which `EncounterDialogueBridgeSystem` processes to either open a full dialogue session or display a bark.

---

## 6. UI Integration

The dialogue system bridges to MonoBehaviour UI through two provider interfaces and a static registry, following the `CombatUIRegistry` pattern.

### 6.1 Provider Interfaces

**IDialogueUIProvider** -- full dialogue panel

| Method | When Called |
|--------|------------|
| `OpenDialogue(DialogueUIState state)` | New dialogue session starts for the local player |
| `AdvanceDialogue(DialogueUIState state)` | Node advances to a new speech or choice |
| `CloseDialogue()` | Session ends (End node, skip, or timeout) |
| `ShowActionFeedback(DialogueActionFeedback feedback)` | An action was executed (quest accepted, item received, etc.) |

**IBarkUIProvider** -- world-space bark bubbles

| Method | When Called |
|--------|------------|
| `ShowBark(string text, float3 position, float range)` | NPC emits a bark line |
| `HideBark()` | Clear the bark display |

### 6.2 DialogueUIState

The `DialogueUIState` struct passed to `OpenDialogue` and `AdvanceDialogue`:

| Field | Type | Description |
|-------|------|-------------|
| **SpeakerName** | string | Resolved speaker name |
| **BodyText** | string | Resolved dialogue text |
| **AudioClipPath** | string | Path to voice audio clip (null if none) |
| **Choices** | DialogueChoiceUI[] | Available choices (filtered by conditions). Null for non-choice nodes. |
| **NodeType** | DialogueNodeType | Current node type |
| **AutoAdvanceSec** | float | Seconds before auto-advance (0 = wait for input) |
| **CameraMode** | DialogueCameraMode | Camera behavior hint |

Each `DialogueChoiceUI` has:

| Field | Type | Description |
|-------|------|-------------|
| **ChoiceIndex** | int | Index to send back in the RPC |
| **Text** | string | Resolved choice text |

### 6.3 Registering a Provider

Create a MonoBehaviour implementing `IDialogueUIProvider` or `IBarkUIProvider`, then register:

```
OnEnable:  DialogueUIRegistry.RegisterDialogue(this);
OnDisable: DialogueUIRegistry.UnregisterDialogue(this);
```

For barks:

```
OnEnable:  DialogueUIRegistry.RegisterBark(this);
OnDisable: DialogueUIRegistry.UnregisterBark(this);
```

### 6.4 Sending Player Input Back

When the player clicks a dialogue choice or advance button, your UI must send an RPC to the server.

**Advancing / selecting a choice:**

Create an entity with `DialogueChoiceRpc` + `SendRpcCommandRequest`:

| Field | Value |
|-------|-------|
| NpcGhostId | The ghost ID of the NPC entity (from GhostInstance.ghostId) |
| ChoiceIndex | The `DialogueChoiceUI.ChoiceIndex` the player selected (0 for non-choice advance) |
| CurrentNodeId | The current node ID (for stale-check) |

**Skipping / closing dialogue:**

Create an entity with `DialogueSkipRpc` + `SendRpcCommandRequest`:

| Field | Value |
|-------|-------|
| NpcGhostId | The ghost ID of the NPC entity |

The server validates all inputs against the current session state before advancing.

### 6.5 Diagnostic Warning

If no `IDialogueUIProvider` is registered after 120 frames (~4 seconds), the console logs: `"[DialogueUIBridge] No IDialogueUIProvider registered after 120 frames."` This is safe to ignore if you haven't built the dialogue UI panel yet.

---

## 7. Editor Tooling

### 7.1 Dialogue Workstation

**Menu:** DIG > Dialogue Workstation

A five-tab editor window:

#### Tree Editor Tab

Visual node graph for editing dialogue trees.

- **Select a tree** using the DialogueTreeSO picker at the top
- **Colored nodes** by type: Blue (Speech), Green (PlayerChoice), Yellow (Condition), Orange (Action), Purple (Random), Cyan (Hub), Red (End)
- **Bezier connections** show node-to-node links
- **Drag** nodes to reposition (positions saved to the SO's NodeEditorPositions array)
- **Right-click context menu:**
  - **Add Node** -- creates a new node at the click position
  - **Delete Node** -- removes the selected node
  - **Set as Start Node** -- makes the selected node the tree's entry point
- **Zoom** with scroll wheel, **Pan** by middle-mouse-dragging
- Selecting a node updates the Node Inspector tab

#### Node Inspector Tab

Edit properties of the selected node:

- Core: Node ID, Node Type
- Speech fields: Speaker Name, Text, Audio Clip Path, Duration
- Navigation: Next Node ID
- Camera Mode
- Condition fields (Condition nodes): Type, Value, True Node ID, False Node ID
- Choices editor (PlayerChoice nodes): add/remove choices, each with text, next node, optional condition
- Actions editor (Action nodes): add/remove actions, each with type and values
- Random entries editor (Random nodes): add/remove entries with node ID and weight

#### Bark Editor Tab

Edit BarkCollectionSO assets:

- Select a collection with the object picker
- Edit core properties: Bark ID, Category, Cooldown, Max Range, Requires Line of Sight
- Edit lines: add/remove lines, each with text, audio path, weight, and optional condition

#### Live Preview Tab (Play Mode Only)

Shows active dialogue sessions in the running game:

- Number of registered trees and bark collections
- For each active session: NPC entity, tree ID, current node ID, interacting player, valid choices mask, start tick
- Current node details: type, text content

#### Validator Tab

Validates dialogue trees for common errors:

1. Select a DialogueDatabaseSO
2. Click **Run Validation**
3. Results show errors and warnings per tree:

| Check | Severity | Description |
|-------|----------|-------------|
| No nodes | Error | Tree has no nodes |
| Missing start node | Error | StartNodeId doesn't match any node |
| Unreachable node | Warning | Node is not reachable from start (flood-fill) |
| Dead-end node | Error | Non-End node has no outgoing connection |
| Broken NextNodeId | Error | NextNodeId points to non-existent node |
| Broken choice target | Error | Choice's NextNodeId points to non-existent node |
| Broken condition branch | Error | TrueNodeId or FalseNodeId points to non-existent node |
| Broken random entry | Error | Random entry's NodeId points to non-existent node |
| Empty speech text | Warning | Speech node has empty Text field |
| No choices | Error | PlayerChoice node has zero choices |
| No random entries | Error | Random node has zero entries |
| No End node | Warning | Tree has no End node |
| Duplicate TreeId | Error | Two trees in the database share a TreeId |

### 7.2 Custom SO Inspector

Selecting a `DialogueTreeSO` in the Project window shows a custom inspector with:

- **Validation header** -- green "Tree valid" or red error messages
- **Default inspector** -- all fields
- **Node summary** -- count of nodes by type, total node count
- **Open in Dialogue Workstation** button

---

## 8. Example: Full NPC Dialogue Setup

### Step 1: Create ScriptableObjects

1. Create `DialogueConfig.asset` at `Assets/Resources/DialogueConfig`
   - Leave defaults (they're good for development)
2. Create `DialogueDatabase.asset` at `Assets/Resources/DialogueDatabase`
3. Create folder `Assets/Resources/BarkCollections/`

### Step 2: Create a Dialogue Tree

1. Create `Guard_Main.asset` via **Create > DIG > Dialogue > Dialogue Tree**
2. Set **Tree Id** = 1, **Display Name** = "Guard Main", **Start Node Id** = 1
3. Add 4 nodes:

| # | NodeId | Type | Key Fields |
|---|--------|------|------------|
| 0 | 1 | Speech | Text="Halt! State your business.", NextNodeId=2 |
| 1 | 2 | PlayerChoice | Choice 0: Text="I'm here to trade.", NextNodeId=3. Choice 1: Text="Just passing through.", NextNodeId=4 |
| 2 | 3 | Speech | Text="The shop is to your left.", NextNodeId=4 |
| 3 | 4 | End | (no fields needed) |

4. Open `DialogueDatabase.asset` and add `Guard_Main` to the Trees list

### Step 3: Create a Bark Collection (Optional)

1. Create `Guard_Idle.asset` via **Create > DIG > Dialogue > Bark Collection**
2. Place in `Assets/Resources/BarkCollections/`
3. Set **Bark Id** = 1, **Category** = Idle, **Cooldown** = 20, **Max Range** = 8
4. Add 3 lines:
   - Text="Keep moving.", Weight=1.0
   - Text="Nothing to see here.", Weight=1.0
   - Text="Stay out of trouble.", Weight=0.5

### Step 4: Set Up the NPC Prefab

1. Open the guard NPC prefab
2. Ensure it has **Interactable Authoring** with Verb = **Talk**
3. Add Component > **Dialogue Speaker**
4. Set **Default Tree** = `Guard_Main`
5. Set **Bark Collection** = `Guard_Idle`
6. Reimport the SubScene

### Step 5: Verify

1. Enter Play Mode
2. Walk up to the guard -- bark text should appear after ~2 seconds
3. Interact (Talk) with the guard -- dialogue panel opens showing "Halt! State your business."
4. Click "I'm here to trade." -- advances to "The shop is to your left."
5. Click advance -- dialogue closes (End node reached)

---

## 9. Example: Condition-Gated Quest Dialogue

This example shows a blacksmith NPC who offers a quest, then gives different dialogue after the quest is completed.

### Trees

**Blacksmith_Intro** (TreeId=10, for players who haven't started the quest):
- Node 1 (Speech): "I need rare ore from the eastern mines."
- Node 2 (PlayerChoice):
  - Choice 0: "I'll help." → Node 3
  - Choice 1: "Not interested." → Node 4
- Node 3 (Action): AcceptQuest IntValue=5, NextNodeId=5
- Node 5 (Speech): "Thank you! Come back when you have the ore.", NextNodeId=4
- Node 4 (End)

**Blacksmith_PostQuest** (TreeId=11, for players who completed quest 5):
- Node 1 (Speech): "You've done well! Take this reward."
- Node 2 (Action): GiveItem IntValue=4(Crystal) IntValue2=10, GiveCurrency IntValue=200, NextNodeId=3
- Node 3 (Speech): "Come back anytime for smithing.", NextNodeId=4
- Node 4 (End)

### NPC Setup

On the blacksmith's `DialogueSpeakerAuthoring`:
- **Default Tree** = `Blacksmith_Intro`
- **Context Rules**:
  - Rule 0: ConditionType=**QuestCompleted**, ConditionValue=5, Tree=`Blacksmith_PostQuest`

When the player talks to the blacksmith:
- If quest 5 is completed → `Blacksmith_PostQuest` loads (context rule matches first)
- Otherwise → `Blacksmith_Intro` loads (fallback)

---

## 10. Dialogue Flags

Dialogue flags are persistent per-NPC state that survives across multiple dialogue sessions (but not across save/load unless a save module is added).

### How They Work

1. An **Action** node with `SetFlag` IntValue=1 sets flag 1 on the NPC entity
2. A **Condition** node with `DialogueFlag` ConditionValue=1 checks if flag 1 is set
3. A **Condition** with `DialogueFlagClear` checks if the flag is NOT set
4. An **Action** with `ClearFlag` IntValue=1 removes the flag

### Use Cases

- "NPC remembers you helped them" -- SetFlag on first visit, branch on DialogueFlag in subsequent visits
- "One-time dialogue branch" -- SetFlag after showing it, use DialogueFlagClear to hide it next time
- "Multi-stage NPC relationship" -- use multiple flag IDs for progressive unlocks

---

## 11. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Bootstrap | Enter Play Mode | Console: no errors from DialogueBootstrapSystem |
| 3 | NPC entity components | Entity Inspector on NPC | Has DialogueSpeakerData, DialogueSessionState, DialogueFlag buffer |
| 4 | Talk interaction | Interact with NPC (Talk verb) | First Speech node text appears in UI |
| 5 | Choice selection | Select a choice | Advances to correct next node |
| 6 | End node | Reach an End node | Dialogue UI closes |
| 7 | Condition branching | Set up a Condition node gating on quest state | Correct branch taken based on game state |
| 8 | Action: SetFlag | Action node sets flag 1, revisit NPC | DialogueFlag condition detects the flag |
| 9 | Action: AcceptQuest | Action node accepts quest 5 | Quest instance created (QuestProgress + QuestPlayerLink entities) |
| 10 | Action: GiveItem | Action node gives 5 Crystal | Player inventory gains 5 Crystal |
| 11 | Action: GiveCurrency | Action node gives 100 gold | Player CurrencyInventory.Gold increases |
| 12 | Action: TakeCurrency | Action node takes 50 gold (player has enough) | Gold deducted. If insufficient: no change. |
| 13 | Context rules | NPC with context rule for QuestCompleted, complete the quest, talk again | Different tree loads |
| 14 | Bark proximity | Walk within 8m of NPC with bark collection | Bark text appears after cooldown |
| 15 | Bark cooldown | Stay near NPC | Barks spaced by cooldown interval |
| 16 | Multiplayer | Second player observes first player talking | Only interacting player sees dialogue UI |
| 17 | Session timeout | Start dialogue, wait 60s | Session auto-closes |
| 18 | Encounter dialogue | Boss encounter with PlayDialogue trigger | Bark/dialogue fires on trigger condition |
| 19 | Workstation: Tree Editor | Open DIG > Dialogue Workstation, select a tree | Colored node graph renders with connections |
| 20 | Workstation: Validator | Run validation on database | Errors/warnings reported for broken trees |
| 21 | Workstation: Live Preview | Play Mode, start a dialogue session | Session appears in Live Preview tab |
| 22 | Custom inspector | Select DialogueTreeSO in Project | Validation header + node summary shown |

---

## 12. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| "DialogueRegistryManaged not found" in console | DialogueDatabase asset missing from Resources | Create `DialogueDatabase.asset` at `Assets/Resources/DialogueDatabase` |
| Dialogue doesn't open on Talk interaction | Missing DialogueSpeakerAuthoring on NPC | Add **Dialogue Speaker** component and set Default Tree |
| Dialogue doesn't open on Talk interaction | NPC's Interactable Verb is not Talk | Set InteractableAuthoring Verb to **Talk** (value 6) |
| Dialogue doesn't open on Talk interaction | Tree not registered in database | Open DialogueDatabase.asset and add the tree to the Trees list |
| Choices not showing | All choices failed condition checks | Check condition types/values on the choices. Ensure ValidChoicesMask is non-zero. |
| Wrong tree loads | Context rule matching a condition you didn't expect | Context rules evaluate top-to-bottom; reorder so the most specific condition is first |
| Quest not accepted from dialogue | QuestRegistryManaged not available | Ensure EPIC 16.12 quest system is set up with QuestDatabase in Resources |
| Items not granted | Player entity missing InventoryItem buffer | Ensure player prefab has inventory authoring (EPIC 16.6) |
| Currency not granted | Player entity missing CurrencyTransaction buffer | Ensure player prefab has economy authoring (EPIC 16.6) |
| Barks not appearing | Bark collection not in Resources/BarkCollections/ | Move the BarkCollectionSO to `Assets/Resources/BarkCollections/` |
| Barks not appearing | No IBarkUIProvider registered | Implement and register a MonoBehaviour with `DialogueUIRegistry.RegisterBark(this)` |
| "No IDialogueUIProvider registered" warning | No dialogue UI MonoBehaviour | Implement and register your dialogue panel with `DialogueUIRegistry.RegisterDialogue(this)` |
| Dialogue flags lost between sessions | Flags are ECS-only (per-NPC entity lifetime) | Flags persist across dialogue sessions but not across game restarts unless a save module is added |
| Session stuck open | End node missing from tree | Run validator (Workstation > Validator tab) to find trees without End nodes |
| Workstation shows no nodes | DialogueTreeSO has empty Nodes array | Add nodes via the Tree Editor tab or directly in the inspector |
| Custom inspector shows "TreeId must be > 0" | TreeId set to 0 | Set TreeId to a unique positive integer |

---

## 13. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Interaction framework (Talk verb, InteractionCompleteEvent) | EPIC 16.1 |
| Loot, economy, item system (GiveItem, TakeItem, GiveCurrency, TakeCurrency) | SETUP_GUIDE_16.6 |
| Quest acceptance and completion (AcceptQuest, QuestCompleted condition) | SETUP_GUIDE_16.12 |
| Crafting stations (OpenCrafting action) | SETUP_GUIDE_16.13 |
| Progression & XP (PlayerLevel condition) | SETUP_GUIDE_16.14 |
| Save/Load persistence (dialogue flags are NOT persisted by default) | SETUP_GUIDE_16.15 |
| AI alert levels (AlertLevelBelow condition) | EPIC 15.33 |
| Encounter triggers (PlayDialogue trigger type) | EPIC 15.32 |
| Audio (SoundEventRequest for bark/voice audio) | EPIC 15.33 (Aggro sound pipeline) |
| **Dialogue & NPC Framework** | **This guide (16.16)** |
