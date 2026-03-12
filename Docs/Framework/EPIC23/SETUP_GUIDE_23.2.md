# EPIC 23.2 Setup Guide — Meta-Progression & Permanent Unlocks

This guide covers everything needed to set up the persistent meta-progression system that survives across rogue-lite runs. Aimed at developers and designers working in the Unity Editor.

---

## Quick Start

1. Open **DIG > Roguelite Setup** from the Unity menu bar
2. Expand the **Setup Validation** section — look under **23.2 — Meta-Progression**
3. Click **Create** next to any missing items (MetaUnlockTree asset, MetaStatApply bridge, MetaUI provider)
4. Expand the **Meta Unlock Tree (23.2)** foldout to configure unlocks — the inline inspector shows validation, category counts, and total cost
5. Set **Meta Currency Conversion Rate** in your RunConfig asset (from EPIC 23.1)
6. Enter Play Mode and expand **Meta-Progression State (23.2)** to test with the **Grant 100 Meta**, **Grant 500 Meta**, and **Reset Meta** buttons

---

## Prerequisites

| Requirement | Details |
|-------------|---------|
| EPIC 23.1 | Run Lifecycle must be fully set up and verified |
| Unity Version | 6000.3.x |
| Packages | Entities, Burst, Collections, Mathematics, NetCode |
| Assemblies | `DIG.Shared` and `DIG.Roguelite` present in project |

---

## 1. Create the Meta Unlock Tree Asset

The unlock tree defines all purchasable permanent upgrades. This is the primary designer-facing asset.

### Via Setup Wizard (Recommended)

1. Open **DIG > Roguelite Setup**
2. In **Setup Validation > 23.2 — Meta-Progression**, click **Create MetaUnlockTree Asset**
3. An asset is created at `Assets/Resources/MetaUnlockTree.asset` with 4 example unlocks
4. Configure it in the **Meta Unlock Tree (23.2)** foldout section

### Via Project Window (Manual)

1. Navigate to `Assets/Resources/`
2. Right-click > **Create > DIG > Roguelite > Meta Unlock Tree**
3. Name the asset exactly **MetaUnlockTree** — the bootstrap system loads this name from `Resources/`
4. Configure unlocks in the Inspector

> **Important:** The asset must be named `MetaUnlockTree` and located in a `Resources/` folder. If missing at startup, the framework creates an empty MetaBank and logs a warning.

### Unlock Entry Fields

Each entry in the unlock list:

| Field | Type | Description |
|-------|------|-------------|
| Unlock Id | int | Unique stable ID. **Never change once assigned** — persisted in save files |
| Display Name | string | Shown in the unlock tree UI |
| Description | string | Tooltip/detail text for players |
| Category | MetaUnlockCategory | Determines how Float/Int values are interpreted (see table below) |
| Cost | int | Meta-currency required to purchase |
| Prerequisite Id | int | UnlockId that must be purchased first. Set to **-1** for no prerequisite |
| Float Value | float | Category-specific: stat amount, multiplier, discount, etc. |
| Int Value | int | Category-specific: item ID, ability ID, stat ID, etc. |
| Icon | Sprite | UI icon for the unlock tree display |

### Unlock Categories

| Category | FloatValue | IntValue | Example |
|----------|-----------|----------|---------|
| StatBoost | Stat bonus amount | Stat ID (game-defined) | +5.0 Vitality |
| StarterItem | — | Item type ID | Start with Health Potion |
| NewAbility | — | Ability ID | Unlock Dash ability |
| Cosmetic | — | Cosmetic ID | Character skin |
| RunModifier | — | Modifier ID (23.4) | Heat modifier access |
| ZoneAccess | — | Zone type ID (23.3) | Unlock Lava Caves |
| ShopUpgrade | Discount percentage (0-1) | — | 10% shop discount |
| CurrencyBonus | Earn rate multiplier | — | 1.2× run currency |

### Example Unlock Tree

```
ID=0  "Thicker Skin"     StatBoost      Cost=50    Prereq=-1   Float=5.0  Int=1 (Vitality)
ID=1  "Sharp Blade"      StatBoost      Cost=75    Prereq=-1   Float=3.0  Int=0 (Strength)
ID=2  "Iron Hide"        StatBoost      Cost=150   Prereq=0    Float=10.0 Int=1 (Vitality)
ID=3  "Starting Potion"  StarterItem    Cost=100   Prereq=-1   Float=0    Int=42
ID=4  "Better Deals"     ShopUpgrade    Cost=200   Prereq=-1   Float=0.1  Int=0
```

In this example, "Iron Hide" requires "Thicker Skin" (Prereq=0) — players must buy Thicker Skin before Iron Hide becomes available.

### Validation

The wizard validates your tree automatically and reports:

- **Duplicate UnlockIds** — every ID must be unique
- **Negative costs** — costs must be >= 0
- **Missing prerequisites** — a PrerequisiteId must reference an existing UnlockId in the tree
- **Category counts** — shows breakdown of how many unlocks per category
- **Total cost** — sum of all unlock costs (useful for economy balancing)

Validation errors appear in red at the bottom of the **Meta Unlock Tree (23.2)** foldout.

### Designer Tips

- **ID stability**: Once an unlock is live and players have save data, never reuse or reassign an UnlockId. Add new unlocks with new IDs only.
- **Prerequisite chains**: Build skill-tree-style progression by linking prerequisites. Keep chains shallow (2-3 deep) unless the design calls for deep specialization.
- **Cost curves**: Early unlocks should be cheap (50-100), mid-tier moderate (150-300), and capstone unlocks expensive (500+). The wizard shows total cost to help balance.
- **Multiple trees**: Create additional `MetaUnlockTreeSO` assets for separate progression paths (Combat, Economy, Exploration). Ensure all UnlockIds are globally unique across trees.

---

## 2. Configure Meta-Currency Economy

Meta-currency conversion is set in the **RunConfig** asset (created during EPIC 23.1 setup).

### Setting the Conversion Rate

1. Select your **RunConfig** asset (in `Assets/Resources/`)
2. Find the **Meta Currency Conversion Rate** field
3. Set a value between 0 and 1:
   - `0.5` = 50% of run currency becomes meta-currency
   - `1.0` = 100% conversion (generous)
   - `0.25` = 25% conversion (grindy)

### How Conversion Works

When a run ends (death, victory, abandon, or timeout):

1. Final score is calculated
2. `metaEarned = (int)(RunCurrency × MetaCurrencyConversionRate)`
3. Meta-currency is added to the persistent MetaBank
4. Lifetime stats are updated (total runs, wins, best score, best zone, playtime)
5. A run history entry is recorded (up to 50 most recent runs)

### Economy Balancing

| Conversion Rate | Feel | Good For |
|----------------|------|----------|
| 0.25 | Slow grind, unlocks feel earned | Hardcore rogue-lites |
| 0.50 | Balanced progression | Most games |
| 0.75-1.0 | Fast unlocks, low friction | Casual or short-session games |

Use the wizard's **Grant 100 Meta** / **Grant 500 Meta** buttons in Play Mode to test purchasing flow without completing full runs.

---

## 3. Create the Game-Side Bridge (MetaStatApplySystem)

The framework tracks which unlocks are purchased but **does not know your game's stat/item/ability systems**. You must create a bridge system that reads unlocked entries and applies their effects.

### Via Setup Wizard (Recommended)

1. Open **DIG > Roguelite Setup**
2. In **Setup Validation > 23.2**, click **Create MetaStatApply Template**
3. A template system is created at `Assets/Scripts/Game/Roguelite/MetaStatApplySystem.cs`
4. Open the file and fill in the `switch` cases for each category your game uses

### What to Customize

The generated template has a `switch` on `MetaUnlockCategory` with TODO comments for each case. For each category your unlock tree uses, add your game-specific logic:

- **StatBoost**: Read `entry.FloatValue` (bonus amount) and `entry.IntValue` (stat ID), apply to your character stat system
- **StarterItem**: Read `entry.IntValue` (item type ID), add to player inventory at run start
- **NewAbility**: Read `entry.IntValue` (ability ID), enable in your ability system
- **CurrencyBonus**: Read `entry.FloatValue` (multiplier), store as a run-currency earn rate modifier
- **ShopUpgrade**: Read `entry.FloatValue` (discount %), apply to shop pricing

Categories you don't use can be left as empty `break` cases.

### When Effects Apply

The bridge system runs every frame, but only re-applies when the number of purchased unlocks changes. This means:

- On session start: effects apply after save data loads
- On unlock purchase: effects re-apply immediately
- No per-frame cost when unlock count is stable

---

## 4. Connect the Meta-Progression UI

To display the meta-progression screen (unlock tree, currency balance, run results), your game implements the `IMetaUIProvider` interface.

### What to Build

Create a MonoBehaviour on your meta-progression canvas that implements `IMetaUIProvider` and register it with `MetaUIRegistry`:

| Method | When Called | What to Show |
|--------|-----------|-------------|
| `OnMetaCurrencyChanged(int newBalance, int delta)` | After run-end conversion or unlock purchase | Currency balance, +delta animation |
| `OnUnlockPurchased(int unlockId, MetaUnlockCategory category, int remainingCurrency)` | After successful purchase | Updated node state, new balance |
| `OnUnlockPurchaseFailed(int unlockId, string reason)` | When purchase is rejected | Error toast with reason |
| `UpdateMetaScreen(int metaCurrency, int totalRunsAttempted, int totalRunsWon, int bestScore, byte bestZoneReached, float totalPlaytime)` | Every frame during MetaScreen phase | Full stats display |
| `OnRunResultsReady(int metaCurrencyEarned, int totalMetaCurrency, RunEndReason endReason, int finalScore, byte zonesCleared)` | On transition to MetaScreen | Run results summary |

### Registration

Register your provider in `OnEnable` and unregister in `OnDisable`:

```csharp
private void OnEnable()  => MetaUIRegistry.Register(this);
private void OnDisable() => MetaUIRegistry.Unregister(this);
```

Only one provider can be active at a time. Registering a second provider logs a warning and replaces the first.

### Requesting Unlock Purchases from UI

When a player clicks an unlock node in your UI, send a purchase request to the ECS framework:

```csharp
using Unity.Entities;
using DIG.Roguelite;

public void OnUnlockNodeClicked(int unlockId)
{
    var world = World.DefaultGameObjectInjectionWorld;
    var em = world.EntityManager;
    var query = em.CreateEntityQuery(ComponentType.ReadWrite<MetaBank>());
    var bankEntity = query.GetSingletonEntity();

    em.SetComponentData(bankEntity, new MetaUnlockRequest { UnlockId = unlockId });
    em.SetComponentEnabled<MetaUnlockRequest>(bankEntity, true);
}
```

The framework processes the request on the next frame:
- **Success**: Deducts cost, marks unlock as purchased, calls `OnUnlockPurchased`
- **Failure**: Calls `OnUnlockPurchaseFailed` with a reason (insufficient currency, prerequisite not met, already purchased)

### Validation Indicator

The wizard's validation section checks whether any script in your project implements `IMetaUIProvider`. A green checkmark appears when found.

---

## 5. Save/Load Integration

Meta-progression is **automatically persisted** if your project uses the DIG persistence system (EPIC 16.15). No additional setup is required.

### What's Saved

| Data | Save Module | TypeId | Details |
|------|-------------|--------|---------|
| MetaBank fields + unlock flags | MetaProgressionSaveModule | 16 | Currency, lifetime stats, IsUnlocked per UnlockId |
| Run history | RunHistorySaveModule | 17 | Last 50 completed run summaries |

Both modules are registered automatically in `PersistenceBootstrapSystem`.

### Save Compatibility Rules

| Scenario | Behavior |
|----------|----------|
| **Adding new unlocks** to the tree | New entries appear as locked. Existing unlock states are preserved (matched by UnlockId) |
| **Removing unlocks** from the tree | Saved states for removed IDs are silently ignored on load |
| **Changing UnlockId** of an existing unlock | **Breaking** — the old save state is orphaned, the renamed entry appears as a new locked unlock |

### Manual Save Triggering

Meta-progression auto-saves with the regular save cycle. To force a save after a critical change (e.g., large meta-currency grant), create a `SaveRequest` entity following the standard persistence pattern.

---

## 6. Verifying the Setup

### Using the Setup Wizard (Play Mode)

The fastest verification path:

1. Enter Play Mode
2. Open **DIG > Roguelite Setup**
3. Check the **Meta-Progression State (23.2)** section:
   - All MetaBank fields should display (MetaCurrency = 0, Runs Attempted = 0, etc.)
   - Unlock count badge shows `0/N` purchased
   - Run history count shows 0
4. Click **Grant 100 Meta** — MetaCurrency should update to 100
5. Click **Reset Meta** — all values should return to zero

### Console Messages

On startup:

```
[MetaBootstrap] MetaBank created with 5 unlocks from 'Combat Upgrades'.
```

If the MetaUnlockTree asset is missing:

```
[MetaBootstrap] No MetaUnlockTreeSO found at Resources/MetaUnlockTree. MetaBank created with empty unlock tree.
```

After a run ends:

```
[MetaConversion] Run 1: 50 run currency × 0.50 = 25 meta-currency. Total: 25
```

After an unlock purchase:

```
[MetaUnlock] Purchase of unlock ID 0: Success
```

### Entity Debugger (Window > Entities > Systems)

| Entity | Components | Expected State |
|--------|-----------|---------------|
| **MetaUnlockTree** | `MetaUnlockTreeSingleton` | BlobAssetReference is valid (non-null) |
| **MetaBank** | `MetaBank`, `MetaUnlockEntry` buffer, `RunHistoryEntry` buffer, `MetaUnlockRequest` | MetaCurrency = 0, MetaUnlockRequest disabled |

### System Execution Order

SimulationSystemGroup:

```
RunLifecycleSystem
  └─ RunInitSystem
      └─ PermadeathSystem
          └─ RunEndSystem
              └─ MetaCurrencyConversionSystem
                  └─ MetaUnlockPurchaseSystem
```

PresentationSystemGroup:

```
RunUIBridgeSystem
MetaUIBridgeSystem
```

---

## 7. Excluding Meta-Progression

If building a game that doesn't need meta-progression:

**Option A — Passive exclusion (recommended):** Don't create the `MetaUnlockTree` asset. The bootstrap system creates an empty MetaBank. All meta systems run but are no-ops without unlock data.

**Option B — Active removal:** Delete the meta-progression systems from the Roguelite assembly (`MetaBootstrapSystem`, `MetaCurrencyConversionSystem`, `MetaUnlockPurchaseSystem`, `MetaUIBridgeSystem`). The 23.1 run lifecycle systems are unaffected.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No MetaUnlockTreeSO found" warning | Asset missing or misnamed | Create via wizard or **Create > DIG > Roguelite > Meta Unlock Tree**, name it exactly **MetaUnlockTree**, place in `Assets/Resources/` |
| Meta-currency always 0 after run | Conversion rate is 0 | Open RunConfig asset, set **Meta Currency Conversion Rate** to a positive value (e.g., 0.5) |
| Unlock purchase silently fails | Prerequisite not met or insufficient currency | Check console for `[MetaUnlock] Purchase` logs. Verify the prerequisite chain in the unlock tree |
| Unlocks reset after restart | Save system not running | Verify PersistenceBootstrapSystem is active and save file writes succeed (check for I/O errors in console) |
| Old unlocks missing after adding new ones | UnlockId was changed | UnlockIds must be stable. Add new unlocks with new IDs — never reassign existing ones |
| MetaBank entity not found | Bootstrap hasn't run | Ensure `MetaBootstrapSystem` is in InitializationSystemGroup (default). Check Entities > Systems window |
| Wizard shows "No MetaBank entity" in Play Mode | Wrong world selected or bootstrap failed | Verify the correct world is active. Check console for bootstrap errors |
| Grant Meta buttons don't work | Not in Play Mode | The test buttons only function during Play Mode |

---

## Setup Checklist

Use this checklist to confirm everything is configured:

- [ ] **MetaUnlockTree asset** exists at `Assets/Resources/MetaUnlockTree.asset`
- [ ] **Unlock entries** configured with unique, stable IDs and valid prerequisites
- [ ] **MetaCurrencyConversionRate** set to a positive value in the RunConfig asset
- [ ] **MetaStatApplySystem** created and customized for your game's stat/item/ability systems
- [ ] **IMetaUIProvider** implemented on a MonoBehaviour, registered with MetaUIRegistry
- [ ] **Play Mode test**: MetaBank appears in wizard, Grant Meta buttons work
- [ ] **Run end test**: Console shows `[MetaConversion]` log with correct currency conversion
- [ ] **Unlock purchase test**: Purchase succeeds, `OnUnlockPurchased` fires, currency deducted
- [ ] **Save/load test**: Exit and re-enter Play Mode — meta-currency and unlocks persist

---

## File Reference

```
Assets/
├── Resources/
│   └── MetaUnlockTree.asset              ← Designer-authored unlock tree
│
├── Scripts/
│   ├── Roguelite/
│   │   ├── Components/
│   │   │   ├── MetaBank.cs               (MetaBank singleton + currency converted signal)
│   │   │   ├── MetaUnlockEntry.cs        (Unlock buffer + category enum + purchase request)
│   │   │   └── RunHistoryEntry.cs        (Run history buffer)
│   │   ├── Definitions/
│   │   │   └── MetaUnlockTreeSO.cs       (ScriptableObject + validation)
│   │   ├── Utility/
│   │   │   └── MetaUnlockBlobBuilder.cs  (BlobAsset builder)
│   │   ├── Systems/
│   │   │   ├── MetaBootstrapSystem.cs
│   │   │   ├── MetaCurrencyConversionSystem.cs
│   │   │   ├── MetaUnlockPurchaseSystem.cs
│   │   │   └── MetaUIBridgeSystem.cs
│   │   └── Bridges/
│   │       └── MetaUIRegistry.cs         (IMetaUIProvider interface + registry)
│   │
│   ├── Game/Roguelite/
│   │   └── MetaStatApplySystem.cs        ← Game-side bridge (generated by wizard)
│   │
│   └── Persistence/
│       ├── Core/
│       │   └── SaveModuleTypeIds.cs      (TypeIds 16 + 17)
│       ├── Modules/
│       │   ├── MetaProgressionSaveModule.cs
│       │   └── RunHistorySaveModule.cs
│       └── Systems/
│           └── PersistenceBootstrapSystem.cs
│
├── Editor/
│   └── RogueliteWorkstation/
│       └── RogueliteSetupWizard.cs       (Wizard with 23.2 validation, tree editor, runtime state)
│
└── Docs/EPIC23/
    └── SETUP_GUIDE_23.2.md              ← This file
```

### What the Framework Provides vs. What You Build

| Framework Provides | You Build |
|-------------------|-----------|
| MetaBank entity, currency conversion, unlock purchase validation | `MetaStatApplySystem` — reads unlocks, applies to your stats/items/abilities |
| MetaUnlockTreeSO definition + Inspector | `MetaUnlockTree.asset` — configured with your game's unlocks |
| MetaUIRegistry + IMetaUIProvider interface | MonoBehaviour implementing `IMetaUIProvider` for your game's UI |
| MetaProgressionSaveModule + RunHistorySaveModule | *(Automatic — no game code needed)* |
| RunHistoryEntry recording (last 50 runs) | Run history display UI *(optional — reads buffer for leaderboard/stats)* |
