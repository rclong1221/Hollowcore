# SETUP GUIDE 16.12: Quest & Objective System

**Status:** Implemented
**Last Updated:** February 23, 2026
**Requires:** Interaction Framework (EPIC 16.1), Loot & Economy (EPIC 16.6), Kill Attribution (existing)

This guide covers Unity Editor setup for the quest and objective system. After setup, designers can create quests as ScriptableObjects, assign them to NPC quest givers, and players receive objectives that track kills, interactions, item pickups, and zone entry — with rewards distributed automatically on completion.

---

## What Changed

Previously, there was no way to structure directed gameplay. Players could fight, loot, and explore, but had no goals, no quest givers, and no reward-for-completion pipeline.

Now:

- **Quest definitions** — ScriptableObject-based, with typed objectives, rewards, prerequisites, and time limits
- **Quest givers** — NPCs offer quests via the existing Interaction framework (InteractionVerb.Talk)
- **Event-driven objectives** — kills, interactions, pickups, and zone entry automatically track progress
- **Reward distribution** — currency, items, and future XP/recipe hooks on quest turn-in
- **Prerequisite chains** — quests gate behind completion of other quests
- **Timed quests** — optional countdown timer with automatic failure
- **Sequential objectives** — objectives unlock in designer-defined order
- **Optional/hidden objectives** — bonus goals and objectives revealed on first progress
- **Quest Workstation** — editor window with quest editor, prerequisite flow viewer, live debug, and validator

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Quest registry bootstrap | Loads `Resources/QuestDatabase` on initialization, builds lookup maps |
| Kill tracking | KillCredited events on player entities auto-emit QuestEvent(Kill) |
| Interaction tracking | InteractionCompleteEvent auto-emits QuestEvent(Interact) |
| Pickup tracking | PickupEvent auto-emits QuestEvent(Collect) |
| Zone tracking | ProximityZoneOccupant auto-emits QuestEvent(ReachZone) after 1 second |
| Objective evaluation | QuestObjectiveEvaluationSystem matches events to active objectives |
| Completion detection | QuestCompletionSystem transitions quests when all required objectives are done |
| Timer countdown | QuestTimerSystem ticks timed quests and fails them on expiry |
| Transient event cleanup | QuestEvent entities destroyed each frame after processing |

---

## 1. Quest Database Setup

The quest database is the central registry loaded at runtime. It must exist before any quest giver can offer quests.

### 1.1 Create the Database

1. In the Project window, right-click > **Create > DIG > Quest > Quest Database**
2. Name it `QuestDatabase`
3. Place it at `Assets/Resources/QuestDatabase.asset` (must be in Resources root)

> This is loaded by `QuestRegistryBootstrapSystem` on game start. If missing, the console logs a warning and the quest system is disabled.

---

## 2. Creating Quests

### 2.1 Create a Quest Definition

1. Right-click > **Create > DIG > Quest > Quest Definition**
2. Name it descriptively (e.g., `Quest_KillWolves`, `Quest_TalkToMerchant`)
3. Place it anywhere in your project (it's referenced by the database, not loaded from Resources)

### 2.2 Identity Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| **Quest Id** | int | Yes | Globally unique integer ID |
| **Display Name** | string | Yes | Name shown in UI (e.g., "Wolf Hunt") |
| **Description** | string (TextArea) | No | Quest description text |
| **Category** | QuestCategory | Yes | Main, Side, Daily, Event, or Tutorial |

### 2.3 Objectives

Click the **Objectives** array and add entries. Each objective has:

| Field | Type | Description |
|-------|------|-------------|
| **Objective Id** | int | Unique within this quest (e.g., 1, 2, 3) |
| **Type** | ObjectiveType | What event triggers progress (see table below) |
| **Target Id** | int | What to match against (see table below) |
| **Required Count** | int (min 1) | How many events needed to complete |
| **Description** | string | Objective text shown in UI (e.g., "Kill 5 Wolves") |
| **Is Optional** | bool | If true, quest can complete without this objective |
| **Is Hidden** | bool | If true, not shown in UI until first progress |
| **Unlock After Objective Id** | int | 0 = available immediately. Otherwise, the ObjectiveId that must complete first |

### 2.4 Objective Types and Target IDs

| Type | Target Id means | How to find the value |
|------|----------------|----------------------|
| **Kill** | Ghost prefab type hash of the enemy | Enter Play Mode, kill the enemy, check console logs or use Live Debug in Quest Workstation |
| **Interact** | Interactable.InteractableID on the target entity | Set in InteractableAuthoring inspector |
| **Collect** | ItemPickup.ItemTypeId | Set in ItemEntrySO inspector |
| **ReachZone** | Interactable.InteractableID on the ProximityZone entity (or entity index if no Interactable) | Set in InteractableAuthoring on the zone |
| **Craft** | Recipe ID (requires EPIC 16.13) | Set in RecipeDefinitionSO inspector |
| **Escort / Survive / Custom** | Designer-defined | Requires custom emitter system |

### 2.5 Rewards

Click the **Rewards** array and add entries:

| Field | Type | Description |
|-------|------|-------------|
| **Type** | QuestRewardType | Currency, Item, Experience, or RecipeUnlock |
| **Value** | int | Currency amount, item type ID, XP amount, or recipe ID |
| **Quantity** | int | Quantity for Item rewards (ignored for Currency) |
| **Currency Type** | CurrencyType | Gold, Premium, or Crafting (only used when Type = Currency) |

#### Reward Type Reference

| Type | Value field means | Quantity field |
|------|-------------------|----------------|
| **Currency** | Amount of currency to award | Ignored |
| **Item** | ResourceType cast to int | Number of items |
| **Experience** | XP amount (stub — requires EPIC 16.14) | Ignored |
| **RecipeUnlock** | Recipe ID (stub — requires EPIC 16.13) | Ignored |

### 2.6 Behavior Fields

| Field | Default | Description |
|-------|---------|-------------|
| **Prerequisite Quest Ids** | Empty | Array of Quest IDs that must be completed (TurnedIn) before this quest is available |
| **Is Repeatable** | false | If true, can be accepted again after turn-in |
| **Time Limit** | 0 | Seconds. 0 = no time limit. Quest fails when timer expires |
| **Auto Complete** | true | If true, quest completes and rewards immediately when all required objectives are done. If false, player must return to the turn-in NPC |
| **Turn In Interactable Id** | 0 | InteractableID of the NPC to turn in to. Only used when AutoComplete is false. 0 = auto-complete |

### 2.7 Add to Database

1. Open your `QuestDatabase` asset
2. Add each `QuestDefinitionSO` to the **Quests** list

---

## 3. Quest Giver NPC Setup

Quest givers are existing interactable entities with a `QuestGiverAuthoring` component.

### 3.1 Prerequisites

The NPC must already have:
- **InteractableAuthoring** with `Can Interact = true`
- **InteractableContext** with `Verb = Talk`
- A unique **Interactable ID** (used for turn-in matching)

### 3.2 Add Quest Giver

1. Select the NPC GameObject in your SubScene
2. Click **Add Component** > search for **Quest Giver**
3. In the **Available Quests** list, drag in the `QuestDefinitionSO` assets this NPC can offer

### 3.3 How It Works

When a player interacts with the NPC (InteractionVerb.Talk):

1. The system checks each quest in the giver's list
2. Skips quests the player already has active, completed, or turned in (unless repeatable)
3. Skips quests whose prerequisites aren't met (checks `CompletedQuestEntry` buffer on player)
4. Creates a QuestInstance entity for the first available quest

### 3.4 Turn-In Setup

If a quest has `AutoComplete = false`:

1. Set `Turn In Interactable Id` on the QuestDefinitionSO to match the NPC's `Interactable ID`
2. The player must return to that NPC and interact again after completing all objectives
3. The system detects the Completed quest and transitions it to TurnedIn, distributing rewards

> For a different turn-in NPC than the quest giver, set the `Turn In Interactable Id` to the other NPC's `Interactable ID`.

---

## 4. Zone Objectives (ReachZone)

To create a "go to this area" objective:

### 4.1 Set Up the Zone

1. Create or select a GameObject in your SubScene
2. Add **Proximity Zone Authoring** (from EPIC 16.1)
3. Set **Radius** to the desired detection radius
4. Add **Interactable Authoring** with a unique **Interactable ID** (this becomes the Target Id for the objective)

### 4.2 Create the Objective

In your QuestDefinitionSO:

| Field | Value |
|-------|-------|
| Type | ReachZone |
| Target Id | The zone's Interactable ID |
| Required Count | 1 |
| Description | "Reach the village" |

> The zone emitter fires after the player has been in the zone for 1 second (prevents drive-by completion).

---

## 5. Sequential Objectives

Objectives can be chained so they unlock in order.

### 5.1 Example: Three-Step Quest

| Objective Id | Description | Unlock After |
|-------------|-------------|--------------|
| 1 | "Talk to the scout" | 0 (available immediately) |
| 2 | "Kill 3 bandits" | 1 (unlocks after objective 1) |
| 3 | "Return to the captain" | 2 (unlocks after objective 2) |

Objectives with `Unlock After Objective Id = 0` start as Active. Others start as Locked and transition to Active when their prerequisite objective completes.

---

## 6. Timed Quests

### 6.1 Setup

Set **Time Limit** on the QuestDefinitionSO to a value in seconds (e.g., 300 for 5 minutes).

### 6.2 Behavior

- Timer starts counting down from the moment the quest is accepted
- `QuestTimerSystem` decrements `TimeRemaining` each frame
- When `TimeRemaining` reaches 0, the quest state transitions to **Failed**
- A `QuestFailed` notification is pushed to the UI

> Set Time Limit = 0 for no timer (the default).

---

## 7. Editor Tooling

### 7.1 Quest Workstation

**Menu:** DIG > Quest Workstation

A four-tab editor window:

#### Quest Editor Tab

- **Database selector** — assign or auto-load QuestDatabaseSO from Resources
- **Search bar** — filter by name or ID
- **Category filter** — dropdown to show only Main, Side, Daily, etc.
- **Quest list** — left panel with color-coded category badges
- **Inline inspector** — right panel shows the selected QuestDefinitionSO's full inspector
- **+ New Quest** button — creates a new SO with auto-incremented ID

#### Flow Viewer Tab

- **Visual prerequisite graph** — quests as colored nodes, prerequisites as Bezier curves
- **Color coding** — Main=gold, Side=blue, Daily=green, Event=red, Tutorial=gray
- **Zoom + pan** — scroll wheel to zoom, middle-mouse drag to pan
- **Topological layout** — quests auto-arranged left-to-right by dependency depth

#### Live Debug Tab (Play Mode only)

- **Active quest instances** — all QuestInstance entities with their state
- **Progress bars** — per-objective progress with current/required counts
- **State coloring** — Active=blue, Completed=green, Failed=red, TurnedIn=yellow
- **Player entity** and **time remaining** display

#### Validator Tab

Checks for common data issues. Click **Run Validation** to scan:

| Check | Severity |
|-------|----------|
| Null entries in database | Error |
| Duplicate Quest IDs | Error |
| Empty DisplayName | Warning |
| No objectives defined | Warning |
| Prerequisite references missing quest | Error |
| Circular prerequisite chain | Error |
| Non-auto-complete quest with no TurnInInteractableId | Warning |
| Duplicate Objective IDs within a quest | Error |
| RequiredCount <= 0 | Warning |
| UnlockAfterObjectiveId references missing objective | Error |

Each result has a **Select** button to highlight the offending QuestDefinitionSO in the Project window.

---

## 8. Example: Complete Quest Setup

### "Wolf Hunt" — Kill 5 wolves, return to the hunter

**Step 1:** Create `Quest_WolfHunt` (QuestDefinitionSO)

| Field | Value |
|-------|-------|
| Quest Id | 101 |
| Display Name | Wolf Hunt |
| Description | The hunter needs help clearing wolves from the valley. |
| Category | Side |
| Auto Complete | false |
| Turn In Interactable Id | 50 (the hunter NPC's InteractableID) |

**Step 2:** Add objectives

| Objective Id | Type | Target Id | Required Count | Description |
|-------------|------|-----------|----------------|-------------|
| 1 | Kill | (wolf ghost type hash) | 5 | Kill 5 wolves |
| 2 | Interact | 50 | 1 | Return to the hunter |

Set objective 2's `Unlock After Objective Id = 1`.

**Step 3:** Add rewards

| Type | Currency Type | Value | Quantity |
|------|--------------|-------|----------|
| Currency | Gold | 200 | — |
| Item | — | 3 (BioMass) | 10 |

**Step 4:** Add to QuestDatabase, assign to hunter NPC's QuestGiverAuthoring

**Step 5:** Reimport SubScene

---

## 9. Finding Kill Target IDs

Kill objectives require the enemy's **ghost prefab type hash** as the Target Id. To find this value:

### Option A: Live Debug

1. Open **DIG > Quest Workstation > Live Debug** tab
2. Create a test quest with `TargetId = 0` and `Type = Kill`
3. Kill an enemy in Play Mode
4. Check the console — `KillQuestEventEmitterSystem` reads `GhostInstance.ghostType` from the victim

### Option B: Ghost Debug Inspector

1. Enter Play Mode
2. Select an enemy entity in the Entity Inspector
3. Find the `GhostInstance` component
4. Read the `ghostType` field — this is the Target Id for Kill objectives

> All enemies of the same prefab share the same `ghostType`. "Kill 5 Wolves" works because all wolf instances have the same ghost type hash.

---

## 10. UI Integration

The quest system provides a UI bridge but does **not** include a built-in quest log UI. To display quest data:

### 10.1 Implement Provider Interfaces

Create MonoBehaviours that implement one or more of:

- **IQuestLogProvider** — full quest list panel (`UpdateQuestLog(QuestLogEntry[])`)
- **IObjectiveTrackerProvider** — HUD objective tracker (`UpdateTrackedObjectives(QuestLogEntry)`)
- **IQuestNotificationProvider** — popup notifications (`ShowQuestAccepted(string)`, `ShowQuestCompleted(string)`, etc.)

### 10.2 Register Providers

In your MonoBehaviour's `OnEnable`:

```
QuestUIRegistry.RegisterQuestLog(this);
QuestUIRegistry.RegisterObjectiveTracker(this);
QuestUIRegistry.RegisterNotifications(this);
```

In `OnDisable`, call the corresponding `Unregister` methods.

### 10.3 Data Available to UI

`QuestLogEntry` provides per-quest:
- QuestId, DisplayName, Description, Category, State, TimeRemaining
- `ObjectiveEntry[]` with ObjectiveId, Description, State, CurrentCount, RequiredCount, IsOptional, IsHidden

The bridge system pushes updated data every frame while quest instances exist.

---

## 11. After Setup: Reimport SubScene

After placing or modifying quest-related authoring components:

1. Right-click the SubScene > **Reimport**
2. Ensure `QuestDatabase.asset` is at `Assets/Resources/QuestDatabase`
3. Wait for baking to complete

---

## 12. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Registry loads | Enter Play Mode | Console: "[QuestRegistry] Loaded N quest definitions." |
| 3 | Quest accepted | Interact with quest giver NPC | QuestInstance entity created (visible in Live Debug) |
| 4 | Kill objective | Kill target enemies | Objective CurrentCount increments |
| 5 | Collect objective | Pick up target items | Objective CurrentCount increments |
| 6 | Zone objective | Enter proximity zone for 1+ seconds | Objective completes |
| 7 | Interaction objective | Interact with target interactable | Objective completes |
| 8 | Sequential unlock | Complete objective 1 | Objective 2 transitions from Locked to Active |
| 9 | Auto-complete | Complete all required objectives (AutoComplete=true) | Quest transitions to TurnedIn, rewards distributed |
| 10 | Turn-in | Complete objectives, interact with turn-in NPC | Quest transitions to TurnedIn, rewards distributed |
| 11 | Currency reward | Complete quest with Currency reward | CurrencyInventory.Gold increases |
| 12 | Item reward | Complete quest with Item reward | InventoryItem buffer gains entry |
| 13 | Prerequisites | Quest B requires Quest A completion | Quest B not offered until Quest A is TurnedIn |
| 14 | Timed quest | Accept timed quest, let timer expire | Quest transitions to Failed |
| 15 | Optional objective | Complete quest without optional objective | Quest still completes |
| 16 | Hidden objective | Progress on hidden objective | Objective becomes visible |
| 17 | Repeatable | Turn in repeatable quest, talk to NPC again | Quest offered again |
| 18 | Quest Workstation | Open DIG > Quest Workstation | All 4 tabs render without errors |
| 19 | Validator | Run validation | Reports issues or "All quests passed" |
| 20 | Flow viewer | Open Flow Viewer with prerequisites | Node graph with Bezier connections |
| 21 | Live Debug | Enter Play Mode with active quests | Progress bars and state display |

---

## 13. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| "[QuestRegistry] No QuestDatabaseSO found" | Missing or misplaced database | Place at `Assets/Resources/QuestDatabase.asset` |
| Quest giver doesn't offer quests | NPC missing QuestGiverAuthoring or InteractableContext(Talk) | Add both components, reimport SubScene |
| Kill objective doesn't track | Wrong Target Id | Use Live Debug or Ghost Inspector to find correct ghostType |
| Zone objective doesn't trigger | TimeInZone threshold not met, or wrong Target Id | Stand in zone > 1 second. Check InteractableID matches |
| Rewards not distributed | Quest stuck in Completed (not TurnedIn) | Set AutoComplete=true, or ensure TurnInInteractableId matches an NPC |
| Quest offered again when not repeatable | Duplicate Quest IDs in database | Run Validator to check for duplicates |
| Prerequisites not working | Player missing CompletedQuestEntry buffer | Ensure player authoring bakes the buffer (requires quest system authoring on player prefab) |
| Timed quest never fails | Time Limit set to 0 | Set to desired seconds (0 = no limit) |
| "No IQuestLogProvider registered" warning | No UI MonoBehaviour registered | Implement and register IQuestLogProvider (or ignore if UI not yet built) |
| Validator shows circular prerequisites | Quest A requires B, B requires A | Fix prerequisite chain to be acyclic |
| Objectives don't unlock in sequence | UnlockAfterObjectiveId references wrong ID | Check that IDs match within the quest's Objectives array |

---

## 14. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Interaction framework (Talk verb, proximity zones) | SETUP_GUIDE_16.1 |
| Loot, items, pickups, currency | SETUP_GUIDE_16.6 |
| Crafting objectives and recipe unlock rewards | EPIC 16.13 (planned) |
| XP reward distribution | EPIC 16.14 (planned) |
| Quest persistence across sessions | EPIC 16.15 (planned) |
| Dialogue integration with quest givers | EPIC 16.16 (planned) |
| **Quest & Objective System** | **This guide (16.12)** |
