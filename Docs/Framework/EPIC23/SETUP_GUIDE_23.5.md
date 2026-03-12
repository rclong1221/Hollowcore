# EPIC 23.5 Setup Guide — Reward & Choice Systems

This guide covers the reward choice system (zone-clear picks), shop system (run-currency purchases), and run event system (narrative risk/reward encounters). All generation is seed-deterministic and presentation-agnostic — your game provides the UI.

---

## Quick Start (Setup Wizard)

1. Open **DIG > Roguelite Setup** from the Unity menu bar
2. Expand **Setup Validation** — look under **23.5 — Rewards & Choices**
3. Click **Create** next to any missing items (RewardPool, EventPool)
4. Expand **Reward & Event Pools (23.5)** to configure reward entries and event definitions inline
5. Enter Play Mode and the wizard's **Rewards & Shop State (23.5)** panel shows live buffer contents
6. Use the **Runtime State** test buttons to transition zones and trigger rewards

---

## Prerequisites

| Requirement | Details |
|-------------|---------|
| EPIC 23.1 | Run Lifecycle must be fully set up and verified |
| EPIC 23.4 | Required only if using Modifier-type rewards or shop pricing with difficulty scaling |
| Unity Version | 6000.3.x |
| Packages | Entities, Burst, Collections, Mathematics |
| Assets | At minimum, a RewardPool with 3+ entries |

---

## 1. Create Reward Definition Assets

Each reward is a ScriptableObject that defines what the player receives. You create these first, then add them to a pool.

### Creating RewardDefinitionSO Assets

- Right-click in Project > **Create > DIG > Roguelite > Reward Definition**
- Name descriptively (e.g., "Reward_GoldBonus_50", "Reward_Healing_25pct", "Reward_Modifier_ToughEnemies")
- Organize in a folder like `Assets/Data/Rewards/`

### RewardDefinitionSO Inspector Fields

| Section | Field | Type | Description |
|---------|-------|------|-------------|
| **Identity** | Reward Id | int | Unique stable ID. **Never reuse or change once assigned** |
| | Display Name | string | Shown in reward selection UI |
| | Description | string | Player-facing tooltip (TextArea) |
| | Icon | Sprite | UI icon for this reward |
| **Classification** | Type | RewardType | What happens when the player receives this (see Type table) |
| | Rarity | byte | 0=Common, 1=Uncommon, 2=Rare, 3=Epic, 4=Legendary |
| **Values** | Int Value | int | Amount for currency, HP, item ID, etc. |
| | Float Value | float | Heal %, stat multiplier, etc. |
| **References** | Loot Table | LootTableSO | For `Item` type — resolves through existing loot pipeline (nullable) |
| | Modifier | RunModifierDefinitionSO | For `Modifier` type — references a 23.4 modifier SO (nullable) |
| **Constraints** | Min Zone Index | int | Earliest zone this can appear. 0 = any zone |
| | Max Zone Index | int | Latest zone this can appear. 0 = any zone |
| | Required Ascension Level | int | Minimum ascension level. 0 = always available |

### Reward Types — What Each Does

| Type | IntValue Used As | FloatValue Used As | What Happens |
|------|------------------|--------------------|-------------|
| **RunCurrency** | Amount to grant | — | Adds to `RunState.RunCurrency` |
| **MetaCurrency** | Amount to grant | — | Adds to `MetaBank.MetaCurrency` (persists across runs) |
| **Item** | — | — | Resolves `LootTable` → creates PendingLootSpawn entities → existing loot pipeline |
| **Healing** | — | Heal % (0.0–1.0) | Restores that % of max health. Default 50% if FloatValue ≤ 0 |
| **MaxHPUp** | HP amount | — | Permanently increases max HP for this run. Default +10 if IntValue ≤ 0 |
| **Modifier** | — | — | Enables `ModifierAcquisitionRequest` on RunState → 23.4 adds to stack |
| **StatBoost** | Game-defined | Game-defined | No-op in framework. Game-side bridge reads choice data and applies |
| **AbilityUnlock** | Game-defined | Game-defined | No-op in framework. Game-side bridge reads choice data and applies |

### Designer Tips for Rewards

- **RunCurrency** rewards with IntValue 10–50 are good early-game. Scale with zone index via constraints
- **Healing** with FloatValue 0.25 (25% heal) is a solid mid-tier reward. FloatValue 1.0 = full heal (Legendary-worthy)
- **Item** rewards need a `LootTableSO` assigned. If null, the reward does nothing
- **Modifier** rewards need a `RunModifierDefinitionSO` assigned (from 23.4). If null, the reward does nothing
- **StatBoost/AbilityUnlock** are framework stubs. Your game must create a bridge system that reads `PendingRewardChoice` buffer entries with these types and applies game-specific effects

---

## 2. Create the Reward Pool

The pool defines which rewards can appear in zone-clear choices and shop inventory, along with their selection weights and rarity filters.

### Via Setup Wizard (Recommended)

1. Open **DIG > Roguelite Setup**
2. In **Setup Validation > 23.5**, click **Create RewardPool Asset**
3. An empty pool is created at `Assets/Resources/RewardPool.asset`
4. Add your RewardDefinitionSO assets as entries in the **Reward & Event Pools (23.5)** foldout

### Via Project Window (Manual)

1. Navigate to `Assets/Resources/`
2. Right-click > **Create > DIG > Roguelite > Reward Pool**
3. Name the asset exactly **RewardPool** (bootstrap loads `Resources.Load<RewardPoolSO>("RewardPool")`)

### RewardPool Inspector Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Pool Name | string | — | Human-readable label |
| Choice Count | int | 3 | How many options to present on zone clear. Industry standard is 3 |
| Allow Duplicates | bool | false | Whether the same reward can appear multiple times in one choice set |
| Entries | List | — | Weighted reward entries (see below) |

### RewardPoolEntry Fields

Each entry in the Entries list:

| Field | Type | Description |
|-------|------|-------------|
| Reward | RewardDefinitionSO | Reference to the reward asset |
| Weight | float | Selection weight (higher = more likely). 10.0 is 10× more likely than 1.0 |
| Min Rarity | byte | Only include if the reward's rarity ≥ this value |
| Max Rarity | byte | Only include if the reward's rarity ≤ this value. 0 = no upper limit |

The wizard shows a **Reward Pool Summary** below the inline inspector: entry count, ChoiceCount, total weight, and a type-distribution breakdown.

### How Choice Generation Works

On `RunPhase.ZoneTransition` in a **Combat zone** (not Shop or Event):

1. System filters pool entries by zone index constraints, ascension requirement, and rarity range
2. Performs weighted random selection (seed-deterministic from zone seed)
3. Populates the `PendingRewardChoice` buffer with `ChoiceCount` entries
4. If `AllowDuplicates` is false, each selected entry is removed from the remaining pool
5. UI adapter receives notification via `IRewardUIProvider.OnPendingChoicesChanged()`

### Pool Balance Guidelines

| Rarity | Suggested % of Pool | Weight Range |
|--------|-------------------|-------------|
| Common (0) | 40–50% | 8.0–10.0 |
| Uncommon (1) | 25–30% | 4.0–6.0 |
| Rare (2) | 15–20% | 2.0–3.0 |
| Epic (3) | 5–10% | 0.5–1.5 |
| Legendary (4) | 1–3% | 0.1–0.5 |

---

## 3. Create Run Events (Optional)

Events are narrative risk/reward encounters that appear in Event zones. Each event presents a story moment with multiple choices, each with different success probabilities and outcomes.

### Creating RunEventDefinitionSO Assets

- Right-click in Project > **Create > DIG > Roguelite > Run Event**
- Organize in a folder like `Assets/Data/Events/`

### RunEventDefinitionSO Inspector Fields

| Section | Field | Type | Description |
|---------|-------|------|-------------|
| **Identity** | Event Id | int | Unique stable ID |
| | Display Name | string | Title shown in event dialog |
| | Narrative Text | string | Story text (TextArea, 3–6 lines) |
| | Illustration | Sprite | Scene illustration for the event |
| **Choices** | Choices | List | Player options (see below) |
| **Constraints** | Min Zone Index | int | Earliest zone. 0 = any |
| | Max Zone Index | int | Latest zone. 0 = any |
| | Weight | float | Selection weight in the event pool. Min 0, default 1.0 |

### EventChoice Fields

Each choice within an event:

| Field | Type | Description |
|-------|------|-------------|
| Choice Text | string | Button/option text (e.g., "Open the chest", "Walk away") |
| Outcome Text | string | Result text shown after choosing (TextArea) |
| Reward | RewardDefinitionSO | Positive outcome — granted on success (nullable) |
| Curse | RunModifierDefinitionSO | Negative outcome — applied on failure (nullable) |
| Success Probability | float | 0.0–1.0. Chance of getting Reward vs Curse |

### Creating the Event Pool

### Via Setup Wizard

1. In **Setup Validation > 23.5**, click **Create EventPool Asset**
2. Created at `Assets/Resources/EventPool.asset`
3. Add your RunEventDefinitionSO assets to the Events list

### Via Project Window

1. Navigate to `Assets/Resources/`
2. Right-click > **Create > DIG > Roguelite > Event Pool**
3. Name exactly **EventPool**
4. Drag your event SOs into the Events list

### How Event Selection Works

On `RunPhase.ZoneTransition` in an **Event zone**:

1. System filters events by zone index constraints
2. Performs weighted random selection (seed-deterministic from event seed)
3. Selected event is stored on `RewardUIRegistry.ActiveEvent`
4. UI adapter receives notification via `IRewardUIProvider.OnActiveEventChanged()`

### Event Design Guidelines

- **Success Probability 1.0** = guaranteed reward, no risk. Good for early events and tutorials
- **Success Probability 0.5** = coin flip. Fair for moderate rewards
- **Below 0.3** feels unfair unless the reward is enormous (Legendary item, large currency)
- Always offer a "safe" choice (walk away, no risk) alongside risky ones
- 2–3 choices per event is ideal. More causes decision fatigue
- Use Curse modifiers sparingly — forced negatives frustrate players unless well-telegraphed

---

## 4. Set Up Zone Type Routing

The reward system needs to know whether each zone transition is **Combat** (reward choice), **Shop**, or **Event**. There are two options:

### Option A: ZoneContextSingleton (Recommended)

Your zone system creates or updates a `ZoneContextSingleton` entity to specify the current zone type:

```csharp
// In your zone transition system
var contextEntity = EntityManager.CreateEntity();
EntityManager.AddComponentData(contextEntity, new ZoneContextSingleton
{
    CurrentType = ZoneTransitionType.Shop  // or .Combat or .Event
});
```

`ZoneTransitionType` values:

| Value | Systems That Activate |
|-------|----------------------|
| `Combat` (0) | ChoiceGenerationSystem (reward picks) |
| `Shop` (1) | ShopGenerationSystem (shop inventory) |
| `Event` (2) | EventPresentationSystem (narrative event) |

### Option B: Default Pattern (No Setup Required)

If no `ZoneContextSingleton` exists, the systems use a simple rotating pattern based on zone index:

| Zone Index | Type | Pattern |
|-----------|------|---------|
| 0, 3, 6, 9... | Combat | `index % 3 == 0` |
| 1, 4, 7, 10... | Shop | `index % 3 == 1` |
| 2, 5, 8, 11... | Event | `index % 3 == 2` |

This is useful for prototyping but most games will want explicit control via Option A.

---

## 5. Connect the Reward UI (IRewardUIProvider)

To display reward choices, shop inventory, and events in your game HUD, implement `IRewardUIProvider`.

### Interface Methods

| Method | When Called | What to Show |
|--------|-----------|-------------|
| `OnPendingChoicesChanged(IReadOnlyList<RewardChoiceSnapshot>)` | When choices are generated or cleared | Show/hide reward selection UI. Empty list = hide |
| `OnShopInventoryChanged(IReadOnlyList<ShopEntrySnapshot>)` | When shop is generated or items are purchased | Show/update shop UI. Entries include IsSoldOut flag |
| `OnActiveEventChanged(RunEventDefinitionSO)` | When an event is selected or cleared | Show/hide event dialog. `null` = no active event |

### RewardChoiceSnapshot Fields

| Field | Description |
|-------|-------------|
| RewardId | Matches the RewardDefinitionSO.RewardId |
| Type | RewardType enum |
| Rarity | 0–4 rarity tier |
| IntValue | Currency amount, HP, etc. |
| FloatValue | Heal %, multiplier, etc. |
| SlotIndex | Which choice slot (0, 1, 2...) — pass this back when selecting |

### ShopEntrySnapshot Fields

| Field | Description |
|-------|-------------|
| RewardId | Matches the RewardDefinitionSO.RewardId |
| Type | RewardType enum |
| Rarity | 0–4 rarity tier |
| IntValue / FloatValue | Reward values |
| Price | Cost in RunCurrency |
| IsSoldOut | Whether this slot has been purchased |

### Registration

```csharp
using DIG.Roguelite.Rewards;

public class MyRewardUI : MonoBehaviour, IRewardUIProvider
{
    void OnEnable()  => RewardUIRegistry.Register(this);
    void OnDisable() => RewardUIRegistry.Unregister(this);

    public void OnPendingChoicesChanged(IReadOnlyList<RewardChoiceSnapshot> choices) { /* ... */ }
    public void OnShopInventoryChanged(IReadOnlyList<ShopEntrySnapshot> entries) { /* ... */ }
    public void OnActiveEventChanged(RunEventDefinitionSO evt) { /* ... */ }
}
```

### Sending Player Selections Back to ECS

When the player picks a reward or buys from shop, call the static queue methods:

```csharp
// Player picks reward choice at slot index 1 (0-indexed)
RewardUIRegistry.QueueRewardSelection(1);

// Player buys shop item at slot index 3
RewardUIRegistry.QueueShopPurchase(3);
```

The `RewardInputBridgeSystem` consumes these queued requests each frame and adds the appropriate ECS request components (`RewardSelectionRequest` or `ShopPurchaseRequest`) to the RunState entity.

---

## 6. Shop Pricing Formula

Shop prices scale with zone progression and difficulty:

```
BasePrice            = RewardDefinitionSO.IntValue (fallback: 10 if ≤ 0)
ZoneMultiplier       = 1 + (CurrentZoneIndex × 0.15)
DifficultyMultiplier = 1 / RuntimeDifficultyScale.CurrencyMultiplier
FinalPrice           = ceil(BasePrice × ZoneMultiplier × DifficultyMultiplier)
                       (minimum 1)
```

### Examples

| Zone | Base | Zone Mult | Difficulty | Final Price |
|------|------|-----------|-----------|-------------|
| 0 | 50 | 1.00 | 1.0 | 50 |
| 2 | 50 | 1.30 | 1.0 | 65 |
| 4 | 50 | 1.60 | 1.0 | 80 |
| 4 | 50 | 1.60 | 1.5 (Lucky Loot active) | 54 |
| 7 | 100 | 2.05 | 1.0 | 205 |

The shop always contains up to **6 items** (or the pool size, whichever is smaller). Items are randomly selected from the same reward pool used for zone-clear choices, with the same zone/ascension filtering.

---

## 7. Verifying the Setup

### Using the Setup Wizard

**Before Play Mode:**

1. Open **DIG > Roguelite Setup**
2. Expand **Setup Validation** — all 23.5 items should show green checks:
   - RewardPool asset exists at `Assets/Resources/RewardPool.asset`
   - EventPool asset exists (optional)
   - IRewardUIProvider implemented (optional but recommended)
3. Expand **Reward & Event Pools (23.5)**:
   - Reward Pool Summary shows entry count, ChoiceCount, total weight, and type distribution
   - Event pool shows event count

**In Play Mode:**

1. Enter Play Mode
2. Use the **Runtime State** test buttons to transition to `ZoneTransition` phase
3. The **Rewards & Shop State (23.5)** panel shows:
   - Pending reward choices (on Combat zones): slot index, reward ID, type, rarity, values
   - Shop inventory (on Shop zones): slot index, reward ID, price, sold-out status
4. Advance to the next zone or change zone type to see different systems activate

### Console Messages (Editor/Development Builds)

On reward selection:
```
[RewardSelection] Applied reward 'Gold Bonus' (type=RunCurrency)
```

On shop purchase:
```
[ShopPurchase] Bought 'Health Potion' for 45 RunCurrency
```

### System Execution Order

InitializationSystemGroup:

```
RewardRegistryBootstrapSystem  (loads SOs, creates managed singleton, adds buffers to RunState)
  └─ UpdateAfter: RunConfigBootstrapSystem (23.1)
```

SimulationSystemGroup:

```
ChoiceGenerationSystem         (generates reward choices on Combat zone transitions)
  └─ RewardSelectionSystem     (processes player reward picks)
ShopGenerationSystem           (generates shop inventory on Shop zone transitions)
  └─ ShopPurchaseSystem        (processes player purchases)
EventPresentationSystem        (selects event on Event zone transitions)
RewardInputBridgeSystem        (drains UI queues → ECS request components)
```

PresentationSystemGroup:

```
RewardUIBridgeSystem           (ECS → managed IRewardUIProvider, change-detected)
```

---

## 8. Excluding the Reward System

If building a game without rewards:

**Option A — Passive exclusion:** Don't create the `RewardPool` asset. The bootstrap system creates a registry with no pool. All reward systems run but produce no choices, no shop, and no events.

**Option B — Active removal:** Delete the `Assets/Scripts/Rewards/` folder entirely. The 23.1, 23.2, and 23.4 systems are unaffected — they have no dependencies on 23.5.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| No reward choices appear | RewardPool asset missing or empty | Create via wizard. Add at least `ChoiceCount` entries |
| No reward choices on zone clear | Zone is not Combat type | Check zone routing (Section 4). Default: `zoneIndex % 3 == 0` |
| Shop never appears | Zone is not Shop type | Check zone routing. Default: `zoneIndex % 3 == 1` |
| Shop prices seem wrong | DifficultyMultiplier or zone index unexpected | Check RuntimeDifficultyScale.CurrencyMultiplier in 23.4 wizard panel |
| Events never trigger | EventPool empty or zone not Event type | Add events to pool. Default: `zoneIndex % 3 == 2` |
| Modifier reward does nothing | `Modifier` field null on RewardDefinitionSO | Assign a RunModifierDefinitionSO (from 23.4) |
| Modifier reward does nothing | `ModifierAcquisitionRequest` not on RunState | Ensure EPIC 23.4 is set up (RunConfigBootstrapSystem adds it) |
| Item reward produces no loot | `LootTable` field null on RewardDefinitionSO | Assign a LootTableSO from the existing loot system |
| Healing reward does nothing | Player entity missing `Health` or `PlayerTag` | Ensure player entity has both components |
| UI not updating | No `IRewardUIProvider` registered | Implement and register via `RewardUIRegistry.Register()` |
| Same rewards every run | Seed determinism working correctly | Same seed = same rewards. Change `RunState.Seed` for different results |
| Choices regenerate on re-entering zone | Buffer was cleared between transitions | Choices only generate when the buffer is empty. Ensure buffer isn't cleared unexpectedly |

---

## Setup Checklist

- [ ] EPIC 23.1 set up and verified (RunConfig asset, RunState entity working)
- [ ] Created RewardDefinitionSO assets for your game's rewards
- [ ] `Assets/Resources/RewardPool.asset` exists with at least 3 entries (one per ChoiceCount)
- [ ] Each reward entry has a unique RewardId
- [ ] Reward entries have appropriate weights (higher = more common)
- [ ] Item-type rewards have LootTableSO assigned
- [ ] Modifier-type rewards have RunModifierDefinitionSO assigned (requires 23.4)
- [ ] `Assets/Resources/EventPool.asset` exists with events (optional — for Event zones)
- [ ] Zone routing configured (ZoneContextSingleton or default `% 3` pattern)
- [ ] `IRewardUIProvider` implemented and registered (optional but recommended)
- [ ] **Play Mode**: Reward choices appear on Combat zone transitions
- [ ] **Play Mode**: Shop populates on Shop zone transitions with correct pricing
- [ ] **Play Mode**: Events appear on Event zone transitions
- [ ] Wizard validation shows green for all 23.5 items

---

## File Reference

```
Assets/
├── Resources/
│   ├── RewardPool.asset                   <- Designer-authored reward pool
│   └── EventPool.asset                    <- Designer-authored event pool (optional)
│
├── Data/                                  (suggested organization)
│   ├── Rewards/                           <- RewardDefinitionSO assets
│   └── Events/                            <- RunEventDefinitionSO assets
│
├── Scripts/Rewards/
│   ├── Components/
│   │   ├── PendingRewardChoice.cs         (Buffer + RewardSelectionRequest)
│   │   ├── ShopInventoryEntry.cs          (Buffer + ShopPurchaseRequest)
│   │   └── ZoneContext.cs                 (ZoneContextSingleton + ZoneTransitionType enum)
│   ├── Definitions/
│   │   ├── RewardDefinitionSO.cs          (Individual reward SO + RewardType enum)
│   │   ├── RewardPoolSO.cs               (Pool SO + RewardPoolEntry struct)
│   │   ├── EventPoolSO.cs                (Event pool SO)
│   │   └── RunEventDefinitionSO.cs       (Event SO + EventChoice struct)
│   ├── Utility/
│   │   └── RewardApplicationUtility.cs   (Shared reward application logic)
│   ├── Systems/
│   │   ├── RewardRegistryBootstrapSystem.cs  (Loads SOs → managed singleton, adds buffers)
│   │   ├── ChoiceGenerationSystem.cs         (Generates reward choices on Combat zones)
│   │   ├── RewardSelectionSystem.cs          (Processes player reward picks)
│   │   ├── ShopGenerationSystem.cs           (Generates shop inventory on Shop zones)
│   │   ├── ShopPurchaseSystem.cs             (Processes player shop purchases)
│   │   ├── EventPresentationSystem.cs        (Selects event on Event zones)
│   │   ├── RewardInputBridgeSystem.cs        (UI queue → ECS request components)
│   │   └── RewardUIBridgeSystem.cs           (ECS → managed IRewardUIProvider, change-detected)
│   └── Bridges/
│       └── RewardUIRegistry.cs            (IRewardUIProvider interface + static registry + queue)
│
└── Editor/RogueliteWorkstation/
    └── RogueliteSetupWizard.cs            (23.5 validation, pool editors, runtime state panel)
```

### What the Framework Provides vs. What You Build

| Framework Provides | You Build |
|-------------------|-----------|
| Seed-deterministic choice generation from pool | `RewardPool.asset` populated with your game's rewards |
| Shop inventory generation with price scaling | *(Automatic from pool — no game code needed)* |
| Event selection from weighted pool | `EventPool.asset` populated with your game's events |
| Reward application (currency, healing, HP, items, modifiers) | `RewardDefinitionSO` assets with correct types and values |
| `IRewardUIProvider` interface + `RewardUIRegistry` | MonoBehaviour implementing `IRewardUIProvider` for your HUD |
| Queue-based UI → ECS bridge (QueueRewardSelection/QueueShopPurchase) | UI buttons that call the queue methods |
| Zone type routing via `ZoneContextSingleton` | Zone system that sets `ZoneContextSingleton.CurrentType` |
| StatBoost/AbilityUnlock type stubs | Game-side bridge system for StatBoost/AbilityUnlock rewards |
