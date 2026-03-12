# EPIC 23.6 — Run Analytics, History & Editor Tooling — Setup Guide

This guide covers what designers and developers need to configure for per-run analytics, run history persistence, and the Run Workstation editor tooling.

---

## 1. Prerequisites

- **EPIC 23.1** (Run Lifecycle) must be set up — `RunConfigSO` in `Resources/`, RunState entity active
- **EPIC 23.2** (Meta-Progression) must be set up — `MetaBank` entity exists for run history storage
- Verify in **DIG > Roguelite Setup** that 23.1 and 23.2 validations pass

---

## 2. Run Statistics (Automatic)

**RunStatistics** tracking is automatic — no setup required. The framework's `RunStatisticsTrackingSystem` automatically tracks:

- Total kills (via `KillCredited` component)
- Damage dealt/taken (via `CombatResultEvent`)
- Run currency earned/spent (via `RunState.RunCurrency` changes)
- Zones cleared with per-zone timing breakdowns

Stats reset on each new run (when `RunPhase.Preparation` begins).

### What the framework tracks vs. what's game-specific

| Tracked automatically | Game must implement |
|---|---|
| TotalKills | EliteKills, BossKills (requires elite/boss tags on enemies) |
| DamageDealt, DamageTaken | ItemsCollected (requires item pickup events) |
| RunCurrencySpent/Earned | ModifiersAcquired (framework tracks if using 23.4) |
| ZonesCleared, FastestZoneTime, SlowestZoneTime | — |

---

## 3. Analytics UI Provider (Optional)

To display post-run stats or live run analytics in your game's HUD, implement `IRunAnalyticsProvider`:

```
Your game script (MonoBehaviour or adapter) implements:
  - OnRunStatisticsChanged(RunStatisticsSnapshot stats)
  - OnZoneCompleted(ZoneTimingSnapshot zoneTiming)
  - OnRunHistoryChanged(IReadOnlyList<RunHistoryEntry> history)
```

### Registration

Register your provider on Awake/Enable:
```
RunAnalyticsRegistry.Register(myProvider);
```

Unregister on Disable/Destroy:
```
RunAnalyticsRegistry.Unregister(myProvider);
```

The `RunAnalyticsBridgeSystem` pushes ECS data to your provider via change detection (not every frame).

### Reading stats outside the provider

You can poll `RunAnalyticsRegistry.LastStats` at any time for the most recent `RunStatisticsSnapshot`.

---

## 4. Run History

Run history is automatically recorded when a run ends (via `MetaCurrencyConversionSystem`). Each completed run creates a `RunHistoryEntry` on the MetaBank entity containing:

- RunId, Seed, AscensionLevel
- EndReason (PlayerDeath, BossDefeated, Abandoned, TimedOut)
- ZonesCleared, Score, Duration
- MetaCurrencyEarned, TotalKills
- Timestamp (Unix seconds)

### Persistence

Run history is saved by `RunHistorySaveModule` (TypeId=17). It persists automatically with the save system — no additional setup needed. History is capped at 50 entries (oldest removed first).

### Accessing history from game code

Read the `RunHistoryEntry` buffer directly from the MetaBank entity, or receive updates via `IRunAnalyticsProvider.OnRunHistoryChanged()`.

---

## 5. Run Workstation (Editor Tooling)

Open via **DIG > Run Workstation** in the Unity menu bar.

### Tab 1: Zone Sequence

- Assign a `RunConfigSO` to edit its zone sequence
- Visual timeline shows all zones in order
- Add/remove zones with + / - buttons
- Difficulty curve editor (embedded AnimationCurve)
- Zone type distribution bars (Combat, Elite, Boss, Shop, Event, Rest, Treasure)
- Validation warnings: missing boss zone, no shop, sequence too short

### Tab 2: Encounter Pools

- Assign an `EncounterPoolSO` to edit
- Weight distribution bars show effective spawn probability per entry
- **Preview Spawn**: enter a seed + zone index → see exact enemy composition logged to Console

### Tab 3: Rewards

- Assign a `RewardPoolSO` to edit
- Rarity distribution chart across pool entries
- **Roll Rewards**: enter a seed + zone index → see exact reward choices logged to Console
- Useful for verifying reward balance without entering play mode

### Tab 4: Modifiers

- Assign a `RunModifierPoolSO` and/or `AscensionDefinitionSO`
- Polarity summary (positive/negative/neutral counts)
- Ascension tier editor with cumulative difficulty graph
- Graph shows multiplicative difficulty scaling across tiers

### Tab 5: Meta Tree

- Assign a `MetaUnlockTreeSO` to view/edit
- Category distribution bars (StatBoost, StarterItem, NewAbility, Cosmetic, etc.)
- **Validation**: detects duplicate UnlockIds, orphan prerequisites, zero-cost nodes
- **Simulate Unlock Path**: enter meta-currency per run → calculates runs to unlock all nodes

---

## 6. Run Simulator (Balance Testing)

### Dry Run

From code or a custom editor script:
```csharp
var result = RunSimulator.Simulate(myRunConfig, seed: 12345, ascensionLevel: 0);
// result.Zones — list of SimulatedZone with enemies, rewards, difficulty
// result.FinalScore, TotalCurrencyEarned, etc.
```

Simulates the full run deterministically: zone sequence, encounters, rewards, difficulty curve. No play mode needed.

### Monte Carlo

For statistical balance analysis across many runs:

```csharp
var monteCarlo = new RunMonteCarloSimulator();
monteCarlo.Start(myRunConfig, ascensionLevel: 3, runCount: 1000);
// Runs async via EditorApplication.update — progress bar with cancel
// When done: monteCarlo.Result has aggregate statistics
```

Results include:
- Average score, currency earned, zones cleared
- Difficulty per zone (averaged across all runs)
- Reward/modifier frequency distributions
- Estimated runs to full meta-tree unlock

Draw results in a custom editor window:
```csharp
RunMonteCarloSimulator.DrawResults(monteCarlo.Result);
```

---

## 7. Validation Checklist

Open **DIG > Roguelite Setup** and check the **23.6 — Analytics & Tooling** section:

| Check | Required? | What to do if missing |
|---|---|---|
| RunStatisticsTrackingSystem | Yes (auto) | Should exist at `Assets/Scripts/Analytics/Systems/`. If missing, the framework analytics module is incomplete |
| IRunAnalyticsProvider | Optional | Create a game-specific MonoBehaviour that implements the interface and registers with `RunAnalyticsRegistry` |
| Run Workstation editor window | Yes (auto) | Should be accessible via DIG > Run Workstation. If missing, check `Assets/Editor/RunWorkstation/` |

---

## 8. Creating ScriptableObject Assets

All assets referenced by the Run Workstation tabs are created via the Unity **Assets > Create > DIG > Roguelite** menu:

| Asset Type | Create Menu Path | Where to Save |
|---|---|---|
| `RunConfigSO` | DIG > Roguelite > Run Config | `Assets/Resources/RunConfig.asset` |
| `ZoneSequenceSO` | DIG > Roguelite > Zone Sequence | `Assets/Data/ZoneSequences/` |
| `EncounterPoolSO` | DIG > Roguelite > Encounter Pool | `Assets/Data/EncounterPools/` |
| `RewardPoolSO` | DIG > Roguelite > Reward Pool | `Assets/Resources/RewardPool.asset` |
| `RunModifierPoolSO` | DIG > Roguelite > Modifier Pool | `Assets/Resources/RunModifierPool.asset` |
| `AscensionDefinitionSO` | DIG > Roguelite > Ascension Definition | `Assets/Resources/AscensionDefinition.asset` |
| `MetaUnlockTreeSO` | DIG > Roguelite > Meta Unlock Tree | `Assets/Resources/MetaUnlockTree.asset` |

Assets in `Resources/` are loaded automatically by bootstrap systems. Assets in `Data/` are referenced by other SOs (e.g., RunConfigSO references its ZoneSequenceSO).
