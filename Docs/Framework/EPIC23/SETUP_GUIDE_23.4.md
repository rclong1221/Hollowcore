# EPIC 23.4 Setup Guide — Run Modifiers & Difficulty Scaling

This guide covers the run modifier system (stackable buffs/debuffs per run), difficulty scaling pipeline, and the ascension/heat system for increasing challenge and rewards.

---

## Quick Start (Setup Wizard)

1. Open **DIG > Roguelite Setup** from the Unity menu bar
2. Expand **Setup Validation** — look under **23.4 — Modifiers & Difficulty**
3. Click **Create** next to any missing items (RunModifierPool, AscensionDefinition)
4. Expand **Run Modifiers & Ascension (23.4)** to configure modifiers and ascension tiers inline
5. Enter Play Mode and expand **Modifiers & Difficulty State (23.4)** to see live difficulty values
6. Use **Add Random Modifier** and **Clear Modifiers** test buttons to verify the pipeline

---

## Prerequisites

| Requirement | Details |
|-------------|---------|
| EPIC 23.1 | Run Lifecycle must be fully set up and verified |
| Unity Version | 6000.3.x |
| Packages | Entities, Burst, Collections, Mathematics, NetCode |
| Assemblies | `DIG.Shared` and `DIG.Roguelite` present in project |

---

## 1. Create the Run Modifier Pool

The modifier pool defines all available run modifiers — buffs, debuffs, and neutral effects that stack during a run.

### Via Setup Wizard (Recommended)

1. Open **DIG > Roguelite Setup**
2. In **Setup Validation > 23.4**, click **Create RunModifierPool Asset**
3. An asset is created at `Assets/Resources/RunModifierPool.asset` with 5 example modifiers
4. Configure modifiers in the **Run Modifiers & Ascension (23.4)** foldout

### Via Project Window (Manual)

1. Navigate to `Assets/Resources/`
2. Right-click > **Create > DIG > Roguelite > Run Modifier Pool**
3. Name the asset exactly **RunModifierPool** (the bootstrap system loads `Resources.Load<RunModifierPoolSO>("RunModifierPool")`)
4. Configure modifiers in the Inspector

### RunModifierPool Inspector Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Pool Name | string | "Modifier Pool" | Human-readable label for this pool |
| Modifiers | List | — | List of all modifier definitions (see below) |

The wizard shows a **Modifier Summary** bar below the inline inspector: total count, polarity breakdown (Positive/Negative/Neutral), and target-type breakdown. Validation errors (duplicate IDs, bad stacking) are flagged automatically.

### Modifier Definition Fields

Each entry in the Modifiers list:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Modifier Id | int | — | Unique stable ID. **Never reuse or change once assigned** |
| Display Name | string | — | Shown in modifier selection UI |
| Description | string | — | Player-facing tooltip (TextArea) |
| Icon | Sprite | — | UI icon for this modifier |
| Polarity | enum | Positive | `Positive` (helps player), `Negative` (hurts), or `Neutral` |
| Target | enum | PlayerStat | What the modifier affects (see Target table) |
| Stat Id | int | 0 | Which specific stat under the target (see StatId Conventions) |
| Float Value | float | — | Effect magnitude per stack |
| Is Multiplicative | bool | false | `true` = multiply (1.5 = +50%). `false` = add (+5.0). Multiplicative stacks compound: 1.5² = 2.25 at 2 stacks |
| Stackable | bool | false | Whether this modifier can be acquired more than once per run |
| Max Stacks | int | 1 | Cap on stacks (only relevant if Stackable is true) |
| Required Ascension Level | int | 0 | Minimum ascension level for this modifier to appear. 0 = always |
| Heat Cost | int | 0 | Cost from the voluntary heat budget (ascension system) |

### Alternatively: Individual Modifier ScriptableObjects

For large pools or team workflows, you can also create standalone modifier assets:

- Right-click > **Create > DIG > Roguelite > Run Modifier**
- Each `RunModifierDefinitionSO` has the same fields as above, plus an `IntValue` field
- Reference these from your reward system (EPIC 23.5) when a reward grants a modifier

### Modifier Targets

| Target | Handled By | Description |
|--------|-----------|-------------|
| **EnemyStat** | Framework (DifficultyScalingSystem) | Modifies enemy health, damage, or spawn rate |
| **Economy** | Framework (DifficultyScalingSystem) | Modifies loot, XP, or currency multipliers |
| **Encounter** | Framework (DifficultyScalingSystem) | Modifies encounter spawn rates |
| **PlayerStat** | Game-side bridge (you build) | Your game reads and applies to player stats |
| **RunMechanic** | Game-side bridge (you build) | Your game reads and applies to custom run mechanics |

### StatId Conventions

The framework interprets StatId for EnemyStat, Economy, and Encounter targets. For PlayerStat and RunMechanic, StatId is game-defined — the framework stores them but does not interpret them.

**EnemyStat:**

| StatId | Field Modified | Example |
|--------|---------------|---------|
| 0 | EnemyHealthScale | 1.5 multiplicative = enemies have +50% health |
| 1 | EnemyDamageScale | 1.25 multiplicative = enemies deal +25% damage |
| 2 | EnemySpawnRateScale | 1.5 multiplicative = +50% spawn rate |

**Economy:**

| StatId | Field Modified | Example |
|--------|---------------|---------|
| 0 | LootQuantityScale | 1.3 multiplicative = +30% loot drops |
| 1 | LootQualityBonus | 0.2 additive = +0.2 quality bonus |
| 2 | XPMultiplier | 1.25 multiplicative = +25% XP |
| 3 | CurrencyMultiplier | 1.5 multiplicative = +50% run currency |

**Encounter:**

| StatId | Field Modified | Example |
|--------|---------------|---------|
| 0 | EnemySpawnRateScale | 1.5 multiplicative = +50% spawn rate |

### Example Modifier Pool

```
ID=0  "Tough Enemies"       Negative  EnemyStat:0   ×1.5   Stackable(3)
ID=1  "Aggressive Enemies"  Negative  EnemyStat:1   ×1.25  Stackable(3)
ID=2  "Lucky Loot"          Positive  Economy:0     ×1.3   Non-stackable
ID=3  "XP Boost"            Positive  Economy:2     ×1.25  Stackable(2)
ID=4  "Swarm"               Negative  Encounter:0   ×1.5   Stackable(2)
```

---

## 2. Create the Ascension Definition (Optional)

The ascension system defines increasing difficulty tiers (heat levels) that force specific modifiers onto runs while increasing rewards.

### Via Setup Wizard (Recommended)

1. Open **DIG > Roguelite Setup**
2. In **Setup Validation > 23.4**, click **Create AscensionDefinition Asset**
3. An asset is created at `Assets/Resources/AscensionDefinition.asset` with 3 example tiers

### Via Project Window (Manual)

1. Navigate to `Assets/Resources/`
2. Right-click > **Create > DIG > Roguelite > Ascension Definition**
3. Name the asset exactly **AscensionDefinition**

### AscensionDefinition Inspector Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Definition Name | string | "Ascension" | Human-readable label |
| Tiers | List | — | Ordered list of ascension tiers (see below) |

### Ascension Tier Fields

Each tier:

| Field | Type | Range | Description |
|-------|------|-------|-------------|
| Level | byte | 1+ | Ascension level (1-based). Level 0 is the default with no modifiers |
| Display Name | string | — | Shown in ascension selection UI (e.g., "Heat 1", "Inferno") |
| Description | string | — | What this tier adds (TextArea) |
| Forced Modifier Ids | int[] | — | ModifierIds from the pool forced onto every run at this level |
| Reward Multiplier | float | 0.5–5.0 | Multiplier for XP and currency rewards |
| Bonus Heat Budget | int | 0+ | Additional heat points for voluntary modifier selection |

### How Ascension Works

Ascension tiers are **cumulative** — playing at Ascension Level 3 applies all forced modifiers from tiers 1, 2, AND 3.

1. Player sets `RunState.AscensionLevel` before run start (via lobby UI)
2. On `RunPhase.Preparation`, `AscensionSetupSystem` clears the modifier stack and force-adds all modifiers from tiers up to the current level
3. `DifficultyScalingSystem` multiplies each tier's `RewardMultiplier` together for the final `AscensionRewardMultiplier`
4. Forced ascension modifiers are locked — players cannot remove them during the run

### Example Ascension Definition

```
Level 1  "Heat 1"   ForcedMods=[0]     RewardMult=1.25  HeatBudget=0
Level 2  "Heat 2"   ForcedMods=[1]     RewardMult=1.50  HeatBudget=1
Level 3  "Heat 3"   ForcedMods=[4]     RewardMult=2.00  HeatBudget=2
```

At Heat 3, the player faces: Tough Enemies (tier 1) + Aggressive Enemies (tier 2) + Swarm (tier 3), with 1.25 × 1.50 × 2.00 = 3.75× cumulative reward multiplier and 2 heat budget points for optional additional modifiers.

### Designer Tips for Ascension

- **Forced modifier IDs** must reference valid ModifierIds in the RunModifierPool. Missing IDs are silently skipped
- **Reward multiplier** scales XP and currency — set it high enough to incentivize higher tiers
- **Heat budget** lets players voluntarily add more negative modifiers for even greater rewards
- Keep early tiers accessible (1–2 forced modifiers). Save heavy stacking for high tiers
- The wizard's **Ascension Summary** shows tier count, total forced modifiers, and validation status

---

## 3. Reading Difficulty Values (Game Integration)

The framework produces a `RuntimeDifficultyScale` singleton every frame that combines the zone difficulty curve, active modifiers, and ascension tier into a single set of scaling values.

### RuntimeDifficultyScale Fields

Your game systems should read `RuntimeDifficultyScale` instead of raw difficulty values:

| Field | Default | Description |
|-------|---------|-------------|
| ZoneDifficultyMultiplier | 1.0 | Composite zone + modifier difficulty |
| EnemyHealthScale | 1.0 | Multiply enemy max health by this |
| EnemyDamageScale | 1.0 | Multiply enemy damage output by this |
| EnemySpawnRateScale | 1.0 | Multiply encounter spawn counts by this |
| LootQuantityScale | 1.0 | Multiply loot drop counts by this |
| LootQualityBonus | 0.0 | Add to loot quality roll |
| XPMultiplier | 1.0 | Multiply XP grants by this |
| CurrencyMultiplier | 1.0 | Multiply currency grants by this |
| AscensionRewardMultiplier | 1.0 | Cumulative ascension tier reward bonus |

### How Values Are Calculated

```
Zone Base    = RunConfig.DifficultyPerZone curve evaluated at (currentZone / maxZones)
Modifiers    = aggregated from RunModifierStack (multiplicative compound, additive sum)
Ascension    = cumulative RewardMultiplier across all tiers ≤ current level

Final Scale  = Zone Base × Modifier Stack
Final Reward = Ascension Reward Multiplier (applied to XP/Currency fields)
```

### PlayerStat / RunMechanic Bridge

For modifiers targeting `PlayerStat` or `RunMechanic`, you need a game-side bridge system. This system reads the `RunModifierStack` buffer and applies effects to your game's stat system:

1. Query the `RunModifierStack` buffer on the RunState entity
2. Filter for `Target == ModifierTarget.PlayerStat` or `Target == ModifierTarget.RunMechanic`
3. Apply `EffectiveValue` to your game's stat/mechanic systems based on `StatId`

The Setup Wizard can generate a template for this — see **Setup Validation > 23.2** for the MetaStatApplySystem template pattern (same approach applies here).

---

## 4. Connect the Modifier UI (IModifierUIProvider)

To display modifier choices and active modifier status in your game HUD, implement `IModifierUIProvider`.

### Interface Methods

| Method | When Called | What to Show |
|--------|-----------|-------------|
| `OnModifierChoicesReady(int choiceCount)` | When choices are generated (zone transition) | Open modifier selection screen |
| `OnModifierAcquired(int modifierId)` | When a modifier is added to the stack | Show acquisition feedback |
| `UpdateModifierDisplay(int activeModifierCount, float zoneDifficultyMultiplier)` | On change | Update HUD modifier count and difficulty indicator |

### Registration

```csharp
private void OnEnable()  => ModifierUIRegistry.Register(this);
private void OnDisable() => ModifierUIRegistry.Unregister(this);
```

### Requesting a Modifier from UI

When the player selects a modifier from the choice screen, enable the `ModifierAcquisitionRequest` on the RunState entity:

```csharp
using Unity.Entities;
using DIG.Roguelite;

public void OnModifierSelected(int modifierId)
{
    var world = World.DefaultGameObjectInjectionWorld;
    var em = world.EntityManager;
    var query = em.CreateEntityQuery(ComponentType.ReadWrite<RunState>());
    var runEntity = query.GetSingletonEntity();

    em.SetComponentData(runEntity, new ModifierAcquisitionRequest { ModifierId = modifierId });
    em.SetComponentEnabled<ModifierAcquisitionRequest>(runEntity, true);
}
```

### Reading Pending Choices

To populate the modifier selection screen, read the `PendingModifierChoice` buffer:

```csharp
var runEntity = query.GetSingletonEntity();
var choices = em.GetBuffer<PendingModifierChoice>(runEntity, true);

for (int i = 0; i < choices.Length; i++)
{
    var choice = choices[i];
    // choice.ModifierId, choice.Polarity, choice.Target,
    // choice.FloatValue, choice.IsMultiplicative
    // Look up display name/icon from your RunModifierPoolSO asset
}
```

---

## 5. Modifier Acquisition Flow

### Zone Transition (Automatic)

On `RunPhase.ZoneTransition`, `ModifierAcquisitionSystem` automatically:

1. Generates 3 modifier choices (deterministic from zone seed)
2. Filters by ascension requirement and stack limits
3. Populates the `PendingModifierChoice` buffer
4. Notifies `IModifierUIProvider.OnModifierChoicesReady()`

The game UI displays choices. Player selects one via `ModifierAcquisitionRequest`. The system adds it to `RunModifierStack` and clears the choices.

### Manual Acquisition (Rewards, Events)

Other systems can also grant modifiers by enabling `ModifierAcquisitionRequest`:

```csharp
em.SetComponentData(runEntity, new ModifierAcquisitionRequest { ModifierId = someId });
em.SetComponentEnabled<ModifierAcquisitionRequest>(runEntity, true);
```

This follows the same validation (registry lookup, stack limits) as zone transition choices. The reward system (EPIC 23.5) uses this pattern for Modifier-type rewards.

---

## 6. Verifying the Setup

### Using the Setup Wizard

**Before Play Mode:**

1. Open **DIG > Roguelite Setup**
2. Expand **Setup Validation** — all 23.4 items should show green checks:
   - RunModifierPool asset exists and validates
   - AscensionDefinition asset exists and validates (optional)
3. Expand **Run Modifiers & Ascension (23.4)**:
   - Modifier Summary shows polarity and target breakdowns
   - Ascension Summary shows tier count and forced modifier total

**In Play Mode:**

1. Enter Play Mode
2. Expand **Modifiers & Difficulty State (23.4)**:
   - All RuntimeDifficultyScale fields should show their defaults (1.0)
   - Active modifiers count shows 0
3. Click **Add Random Modifier** — a modifier appears in the list, difficulty values update
4. Click **Add Random Modifier** again — if the same modifier is stackable, stack count increases
5. Click **Clear Modifiers** — returns to defaults

### Console Messages

On startup (Editor/Development builds only):

```
[ModifierBootstrap] Registry: 5 modifiers from 'Default Modifier Pool'. Ascension: 3 tiers.
```

If assets are missing:

```
[ModifierBootstrap] No RunModifierPoolSO found at Resources/RunModifierPool. Registry created empty.
[ModifierBootstrap] No AscensionDefinitionSO found at Resources/AscensionDefinition. Ascension disabled.
```

### System Execution Order

InitializationSystemGroup:

```
ModifierBootstrapSystem  (loads SOs → creates blob singletons, runs once)
```

SimulationSystemGroup:

```
RunLifecycleSystem
  └─ RunInitSystem
      └─ AscensionSetupSystem          (force modifiers on Preparation)
          └─ ModifierAcquisitionSystem  (handle requests, generate choices)
              └─ DifficultyScalingSystem (aggregate → RuntimeDifficultyScale)
```

PresentationSystemGroup:

```
ModifierUIBridgeSystem  (ECS → managed IModifierUIProvider)
```

---

## 7. Excluding Modifiers & Difficulty

If building a game that doesn't need run modifiers:

**Option A — Passive exclusion:** Don't create the `RunModifierPool` or `AscensionDefinition` assets. The bootstrap system creates empty registries. All modifier systems run but are no-ops. `RuntimeDifficultyScale` stays at defaults (all 1.0).

**Option B — Active removal:** Delete the modifier systems from the Roguelite assembly (`ModifierBootstrapSystem`, `AscensionSetupSystem`, `DifficultyScalingSystem`, `ModifierAcquisitionSystem`, `ModifierUIBridgeSystem`). The 23.1 Run Lifecycle and 23.2 Meta-Progression systems are unaffected.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No RunModifierPoolSO found" log | Asset missing or misnamed | Create via wizard or right-click > **Create > DIG > Roguelite > Run Modifier Pool**, name it **RunModifierPool**, place in `Assets/Resources/` |
| Difficulty values always 1.0 | No modifiers active and zone 0 has difficulty 1.0 | Expected at start. Advance zones or add modifiers to see changes |
| Ascension modifiers not applied | AscensionLevel is 0 or tiers reference missing ModifierIds | Set `RunState.AscensionLevel` > 0, verify ForcedModifierIds match pool IDs |
| Modifier choices empty on zone transition | Pool is empty or all modifiers are at max stacks | Add more modifiers to the pool or increase MaxStacks |
| Modifier not stacking past 1 | Stackable is false or already at MaxStacks | Check the modifier definition's Stackable and MaxStacks fields |
| RuntimeDifficultyScale not found | ModifierBootstrapSystem hasn't run | Ensure EPIC 23.1 is set up first (RunState entity must exist) |
| PlayerStat modifiers have no effect | Framework doesn't interpret PlayerStat/RunMechanic | Create a game-side bridge system that reads RunModifierStack |
| Multiplicative modifier seems wrong | Stacking is compound (1.5² = 2.25 at 2 stacks) | This is intentional. Each stack multiplies the previous result |

---

## Setup Checklist

- [ ] EPIC 23.1 set up and verified (RunConfig asset, RunState entity working)
- [ ] `Assets/Resources/RunModifierPool.asset` exists with modifier entries
- [ ] Each modifier has a unique ModifierId
- [ ] Modifier pool validates in wizard (no duplicate IDs, valid MaxStacks)
- [ ] `Assets/Resources/AscensionDefinition.asset` exists (optional)
- [ ] Ascension tiers reference valid ModifierIds from the pool
- [ ] Ascension tiers validate in wizard (unique levels, positive RewardMultiplier)
- [ ] **Play Mode**: RuntimeDifficultyScale appears in wizard with default values
- [ ] **Play Mode**: "Add Random Modifier" updates difficulty values
- [ ] **Zone transition**: Modifier choices populate on ZoneTransition phase
- [ ] **Ascension**: Setting AscensionLevel > 0 forces correct modifiers on Preparation
- [ ] Game-side bridge created if using PlayerStat/RunMechanic modifier targets
- [ ] `IModifierUIProvider` implemented if showing modifier selection UI

---

## File Reference

```
Assets/
├── Resources/
│   ├── RunModifierPool.asset              <- Designer-authored modifier pool
│   └── AscensionDefinition.asset          <- Designer-authored ascension tiers (optional)
│
├── Scripts/Roguelite/
│   ├── Components/
│   │   ├── RunModifierStack.cs            (Buffer + enums + ModifierAcquisitionRequest + PendingModifierChoice)
│   │   └── RuntimeDifficultyScale.cs      (Singleton with all difficulty scaling fields)
│   ├── Definitions/
│   │   ├── RunModifierPoolSO.cs           (Pool SO + RunModifierDefinition struct)
│   │   ├── RunModifierDefinitionSO.cs     (Individual modifier SO for reward references)
│   │   └── AscensionDefinitionSO.cs       (Ascension tiers SO)
│   ├── Utility/
│   │   ├── ModifierRegistryBlobBuilder.cs (Blob types + builders)
│   │   ├── ModifierStackUtility.cs        (Shared stack add/remove logic)
│   │   └── RunSeedUtility.cs              (DeriveModifierSeed for deterministic choices)
│   ├── Systems/
│   │   ├── ModifierBootstrapSystem.cs     (Loads SOs → creates singletons, runs once)
│   │   ├── AscensionSetupSystem.cs        (Forces modifiers on Preparation phase)
│   │   ├── DifficultyScalingSystem.cs     (Aggregates modifiers → RuntimeDifficultyScale)
│   │   ├── ModifierAcquisitionSystem.cs   (Processes requests, generates zone choices)
│   │   └── ModifierUIBridgeSystem.cs      (ECS → managed IModifierUIProvider bridge)
│   └── Bridges/
│       └── ModifierUIRegistry.cs          (IModifierUIProvider interface + static registry)
│
└── Editor/RogueliteWorkstation/
    └── RogueliteSetupWizard.cs            (23.4 validation, pool editor, runtime state panel)
```

### What the Framework Provides vs. What You Build

| Framework Provides | You Build |
|-------------------|-----------|
| Modifier stacking, acquisition, and stack limits | `RunModifierPool.asset` populated with your game's modifiers |
| DifficultyScalingSystem → RuntimeDifficultyScale singleton | Enemy/loot/XP systems that read RuntimeDifficultyScale |
| AscensionSetupSystem, forced modifier application | `AscensionDefinition.asset` populated with your difficulty tiers |
| ModifierAcquisitionSystem, PendingModifierChoice buffer | Modifier selection UI implementing `IModifierUIProvider` |
| Zone-transition choice generation (seed-deterministic) | *(Automatic — no game code needed)* |
| EnemyStat/Economy/Encounter modifier application | PlayerStat/RunMechanic bridge system *(only if using those targets)* |
